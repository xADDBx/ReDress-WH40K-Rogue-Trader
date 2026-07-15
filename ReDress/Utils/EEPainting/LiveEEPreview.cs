using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Mechanics.Entities;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using Owlcat.Runtime.Core.Physics.PositionBasedDynamics;
using Owlcat.Runtime.Core.Utility.Locator;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReDress;

/// <summary>
/// Describes what a preview cell should show, expressed as a delta on top of the
/// picked unit's current (already ReDress-modified) appearance.
/// </summary>
public sealed class PreviewSpec {
    public enum Kind {
        /// <summary>The unit exactly as currently configured.</summary>
        AsIs,
        /// <summary>Current appearance plus one extra EE (by asset guid).</summary>
        AddEE,
        /// <summary>Current appearance with one EE (by name) removed.</summary>
        RemoveEE,
        /// <summary>Current appearance with a specific ramp pair forced on one EE.</summary>
        RampPair
    }
    public readonly Kind Type;
    public readonly string? AssetId;
    public readonly string? EEName;
    public readonly int Primary = -1;
    public readonly int Secondary = -1;

    private PreviewSpec(Kind type, string? assetId, string? eeName, int primary, int secondary) {
        Type = type;
        AssetId = assetId;
        EEName = eeName;
        Primary = primary;
        Secondary = secondary;
    }
    public static readonly PreviewSpec AsIs = new(Kind.AsIs, null, null, -1, -1);
    public static PreviewSpec Add(string assetId) => new(Kind.AddEE, assetId, null, -1, -1);
    public static PreviewSpec Remove(string eeName) => new(Kind.RemoveEE, null, eeName, -1, -1);
    public static PreviewSpec Ramps(string eeName, int primary, int secondary) => new(Kind.RampPair, null, eeName, primary, secondary);

    public bool SameAs(PreviewSpec? other) {
        return other != null && Type == other.Type && AssetId == other.AssetId
            && EEName == other.EEName && Primary == other.Primary && Secondary == other.Secondary;
    }
}

public sealed class LiveEEPreview : IDisposable {
    public const int LiveSize = 1024;
    public const int SnapshotSize = 512;
    public const int MaxSnapshots = 48;
    public const float Fov = 30f;
    public const float FrameMargin = 1.12f;
    public const int RenderLayer = 31;
    public const float OrbitSpeed = 0.6f;
    public const float PanSpeed = 1f;
    public const float DoubleClickTime = 0.35f;
    public const int ReadySettleFrames = 2;
    public const int BuildTimeoutFrames = 240;
    public const int VisibleFrameSlack = 3;
    public const float LightScanInterval = 2f;
    public static readonly Color Background = new(0.11f, 0.12f, 0.14f, 1f);
    public static readonly Color AmbientColor = new(0.38f, 0.38f, 0.42f, 1f);
    private static readonly Vector3 m_Park = new(0f, -1500f, 0f);

    private static LiveEEPreview? s_Instance;

    private GameObject? m_Rig;
    private Camera? m_Cam;
    private Light? m_Key, m_Fill;
    private RenderTexture? m_LiveRT;

    private Character? m_Doll;
    private GameObject? m_DollGo;
    private Character? m_DollSource;
    private string? m_DollUnitId;
    private bool m_HasGeometry;
    private Bounds m_Bounds;
    private bool m_BoundsDirty;
    private bool m_PoseForced;
    private (string Name, int Primary, int Secondary)? m_RampPreview;

    private string? m_LiveKey;
    private bool m_LiveReady;
    private int m_ReadyStreak;
    private int m_ApplyFrame;
    private bool m_TimeoutLogged;

    private sealed class Entry {
        public PreviewSpec Spec = PreviewSpec.AsIs;
        public Texture2D? Snapshot;
        public bool WantsRender = true;
        public bool SnapshotDirty = true;
        public int LastDrawnFrame = -1;
        public int ParamsChangedFrame = -1;
        public float Yaw, Pitch;
        public float Zoom = 1f;
        public Vector3 FocusOffset;
    }
    private readonly Dictionary<string, Entry> m_Entries = new();

    private string? m_ActiveKey;
    private bool m_Dragging;
    private string? m_LastClickKey;
    private float m_LastClickTime;
    private enum DragMode { Orbit, Pan }
    private DragMode m_DragMode = DragMode.Orbit;

    private UpdateMode? m_SavedPbdMode;

    private Light[] m_SceneLights = [];
    private readonly List<Light> m_TempDisabledLights = [];
    private float m_NextLightScan;
    private SphericalHarmonicsL2 m_SavedProbe;
    private bool m_SavedFog;
    private bool m_RenderStateSwapped;

    public LiveEEPreview() {
        s_Instance = this;
    }

    internal static bool BypassAddFilter;

    internal static bool IsPreviewCharacter(Character? c) {
        return c != null && s_Instance?.m_Doll == c;
    }

    internal static bool TryGetRampPreview(string? eeName, out (int Primary, int Secondary) pair) {
        var rp = s_Instance?.m_RampPreview;
        if (eeName != null && rp != null && rp.Value.Name == eeName) {
            pair = (rp.Value.Primary, rp.Value.Secondary);
            return true;
        }
        pair = default;
        return false;
    }

    public void DrawCell(Rect rect, string key, PreviewSpec spec) {
        EnsureRig();
        var e = GetEntry(key);
        if (!e.Spec.SameAs(spec)) {
            e.Spec = spec;
            e.WantsRender = true;
            e.SnapshotDirty = true;
        }
        e.LastDrawnFrame = Time.frameCount;

        int id = GUIUtility.GetControlID(key.GetHashCode(), FocusType.Passive);
        HandleInput(id, rect, key, e);

        if (Event.current.type == EventType.Repaint) {
            if (key == m_LiveKey && m_LiveReady && m_LiveRT != null) {
                GUI.DrawTexture(rect, m_LiveRT, ScaleMode.ScaleToFit, false);
            } else if (e.Snapshot != null) {
                GUI.DrawTexture(rect, e.Snapshot, ScaleMode.ScaleToFit, false);
            } else {
                GUI.Box(rect, CanPreview() ? "…" : "No Preview");
            }
        }
    }

    public void InvalidateAll() {
        foreach (var e in m_Entries.Values) {
            e.WantsRender = true;
            e.SnapshotDirty = true;
        }
        DestroyDoll();
    }

    private void HandleInput(int id, Rect rect, string key, Entry e) {
        var ev = Event.current;
        switch (ev.GetTypeForControl(id)) {
            case EventType.MouseDown:
                if ((ev.button == 0 || ev.button == 2) && rect.Contains(ev.mousePosition)) {
                    bool doubleClick = ev.button == 0 && key == m_LastClickKey
                        && Time.realtimeSinceStartup - m_LastClickTime < DoubleClickTime;
                    if (ev.button == 0) {
                        m_LastClickKey = doubleClick ? null : key;
                        m_LastClickTime = Time.realtimeSinceStartup;
                    }
                    if (doubleClick) {
                        e.Yaw = 0f;
                        e.Pitch = 0f;
                        e.Zoom = 1f;
                        e.FocusOffset = Vector3.zero;
                    } else {
                        GUIUtility.hotControl = id;
                        m_Dragging = true;
                        m_DragMode = (ev.button == 2 || ev.shift) ? DragMode.Pan : DragMode.Orbit;
                    }
                    Touch(key, e);
                    ev.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id) {
                    if (m_DragMode == DragMode.Pan) {
                        ViewBasis(e, out var right, out var up, out _, out var framedH);
                        float perPx = framedH / Mathf.Max(1f, rect.height);
                        e.FocusOffset += (right * -ev.delta.x + up * ev.delta.y) * perPx * PanSpeed;
                    } else {
                        e.Yaw += ev.delta.x * OrbitSpeed;
                        e.Pitch -= ev.delta.y * OrbitSpeed;
                        e.Pitch = Mathf.Clamp(e.Pitch, -85f, 85f);
                    }
                    Touch(key, e);
                    ev.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id) {
                    GUIUtility.hotControl = 0;
                    m_Dragging = false;
                    m_DragMode = DragMode.Orbit;
                    Touch(key, e);
                    ev.Use();
                }
                break;
            case EventType.ScrollWheel:
                if (rect.Contains(ev.mousePosition) && (ev.alt || ev.control)) {
                    ViewBasis(e, out var right, out var up, out _, out var framedH);
                    Vector2 cf = new Vector2(ev.mousePosition.x - rect.center.x,
                                             ev.mousePosition.y - rect.center.y) / Mathf.Max(1f, rect.height);
                    Vector3 focus = m_Bounds.center + e.FocusOffset;
                    Vector3 underCursor = focus + (right * cf.x - up * cf.y) * framedH;
                    float newZoom = Mathf.Clamp(e.Zoom * (1f + ev.delta.y * 0.08f), 0.12f, 3f);
                    float k = newZoom / e.Zoom;
                    e.FocusOffset = Vector3.Lerp(underCursor, focus, k) - m_Bounds.center;
                    e.Zoom = newZoom;
                    Touch(key, e);
                    ev.Use();
                }
                break;
        }
    }

    private void Touch(string key, Entry e) {
        m_ActiveKey = key;
        e.SnapshotDirty = true;
        e.ParamsChangedFrame = Time.frameCount;
    }

    private static bool RTMatchesParams(Entry e) => Time.frameCount - e.ParamsChangedFrame >= 2;

    internal void Tick() {
        if (m_Cam == null || m_LiveRT == null) {
            return;
        }

        if (m_Dragging && GUIUtility.hotControl == 0) {
            m_Dragging = false;
        }

        bool anyVisible = false;
        foreach (var e in m_Entries.Values) {
            if (IsVisible(e)) {
                anyVisible = true;
                break;
            }
        }
        if (!anyVisible || !CanPreview()) {
            m_Cam.enabled = false;
            return;
        }

        var unit = Main.PickedUnit!;
        var avatar = unit.View.CharacterAvatar;
        bool dollValid = m_Doll != null && m_DollSource == avatar && m_DollUnitId == unit.UniqueId;

        if (dollValid && m_Doll!.AnimationManager != null) {
            var mgr = m_Doll.AnimationManager;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.1f);
            mgr.Tick(dt);
            mgr.CustomUpdate(dt);
            if (!m_PoseForced) {
                var anim = mgr.m_LocoMotionHandle?.ActiveAnimation;
                if (anim != null) {
                    anim.Update(1f, 1f);
                    m_PoseForced = true;
                }
            }
        }
        if (m_DollUnitId != null && m_DollUnitId != unit.UniqueId) {
            ClearEntries();
            DestroyDoll();
            m_DollUnitId = null;
        }

        var targetKey = PickTargetKey();
        if (targetKey == null) {
            m_Cam.enabled = false;
            return;
        }

        if (targetKey != m_LiveKey || !dollValid) {
            if (dollValid && m_LiveKey != null && m_LiveReady
                && m_Entries.TryGetValue(m_LiveKey, out var old) && old.SnapshotDirty) {
                if (RTMatchesParams(old)) {
                    Snapshot(old);
                } else {
                    old.WantsRender = true;
                }
            }
            m_LiveKey = null;
            m_LiveReady = false;
            var target = m_Entries[targetKey];
            if (!BuildDollFor(target.Spec, unit, avatar)) {
                target.WantsRender = false;
                target.SnapshotDirty = false;
                m_Cam.enabled = false;
                return;
            }
            m_LiveKey = targetKey;
            m_ReadyStreak = 0;
            m_ApplyFrame = Time.frameCount;
            m_TimeoutLogged = false;
        }

        var entry = m_Entries[m_LiveKey!];
        if (m_BoundsDirty || !m_HasGeometry) {
            m_Bounds = ComputeDollBounds();
            m_BoundsDirty = false;
        }
        m_Cam.enabled = m_HasGeometry;
        if (m_HasGeometry) {
            PositionCamera(entry);
        }

        if (!m_LiveReady) {
            if (m_HasGeometry && IsDollSettled()) {
                if (++m_ReadyStreak >= ReadySettleFrames) {
                    m_LiveReady = true;
                    m_BoundsDirty = true;
                }
            } else {
                m_ReadyStreak = 0;
                if (Time.frameCount - m_ApplyFrame > BuildTimeoutFrames) {
                    if (!m_TimeoutLogged) {
                        m_TimeoutLogged = true;
                        Main.Log.Log($"Warning: preview doll build timed out for {m_LiveKey}");
                    }
                    m_LiveReady = true;
                }
            }
        }

        if (m_LiveReady) {
            entry.WantsRender = false;
            if (!m_HasGeometry) {
                entry.SnapshotDirty = false;
            } else if (entry.SnapshotDirty && !(m_Dragging && m_ActiveKey == m_LiveKey) && RTMatchesParams(entry)) {
                Snapshot(entry);
            }
        }
    }

    private static bool IsVisible(Entry e) => Time.frameCount - e.LastDrawnFrame <= VisibleFrameSlack;

    private string? PickTargetKey() {
        if (m_ActiveKey != null && m_Entries.TryGetValue(m_ActiveKey, out var active) && IsVisible(active)
            && (m_Dragging || active.WantsRender || active.SnapshotDirty)) {
            return m_ActiveKey;
        }
        if (m_LiveKey != null && m_Entries.TryGetValue(m_LiveKey, out var live) && IsVisible(live)
            && (!m_LiveReady || live.WantsRender || live.SnapshotDirty)) {
            return m_LiveKey;
        }
        string? best = null;
        int bestFrame = int.MinValue;
        foreach (var kv in m_Entries) {
            var e = kv.Value;
            if (!e.WantsRender || !IsVisible(e)) {
                continue;
            }
            if (e.LastDrawnFrame > bestFrame || (e.LastDrawnFrame == bestFrame && string.CompareOrdinal(kv.Key, best) < 0)) {
                best = kv.Key;
                bestFrame = e.LastDrawnFrame;
            }
        }
        return best;
    }

    private static bool CanPreview() {
        var unit = Main.PickedUnit;
        return Helpers.IsSave(unit)
            && unit!.View?.CharacterAvatar != null
            && !unit.View.CharacterAvatar.BakedCharacter;
    }

    private bool IsDollSettled() {
        if (m_Doll == null) {
            return false;
        }
        if (m_Doll.AnimationManager != null && !m_PoseForced) {
            return false;
        }
        var atlasSvc = Services.GetInstance<CharacterAtlasService>();
        var dxtSvc = Services.GetInstance<DxtCompressorServiceNew>();
        return !m_Doll.IsDirty
            && m_Doll.OverlaysMerged
            && (!m_Doll.IsAtlasesDirty || m_Doll.m_Atlases.Count == 0)
            && (atlasSvc == null || atlasSvc.RequestsCount == 0)
            && (dxtSvc == null || dxtSvc.RequestsCount == 0);
    }
    private bool BuildDollFor(PreviewSpec spec, AbstractUnitEntity unit, Character avatar) {
        DestroyDoll();
        try {
            m_DollGo = new GameObject("ReDressPreviewDoll");
            m_DollGo.transform.SetParent(m_Rig!.transform, false);
            m_DollGo.transform.localPosition = Vector3.zero;
            m_DollGo.transform.localRotation = Quaternion.identity;
            m_DollGo.transform.localScale = avatar.transform.localScale;

            m_Doll = m_DollGo.AddComponent<Character>();
            m_Doll.PreventUpdate = false;
            m_Doll.IsInDollRoom = true;
            m_Doll.ForbidBeltItemVisualization = avatar.ForbidBeltItemVisualization;
            m_Doll.AnimatorPrefab = avatar.AnimatorPrefab;
            m_Doll.Skeleton = avatar.Skeleton;
            m_Doll.AnimationSet = avatar.AnimationSet;
            m_Doll.AtlasData = avatar.AtlasData;
            m_Doll.OnUpdated += OnDollUpdated;
            try {
                Helpers.UIdCache.Add(m_Doll, unit.UniqueId);
            } catch (ArgumentException) { }
            m_Doll.CopyEquipmentFrom(avatar);
            if (!ApplyDelta(spec)) {
                DestroyDoll();
                return false;
            }
            if (unit is BaseUnitEntity baseUnit) {
                m_Doll.SetSourceUnit(baseUnit);
            }
            m_Doll.OnStart();
            if (m_Doll.Animator != null) {
                m_Doll.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (m_Doll.AnimationManager != null) {
                var mgr = m_Doll.AnimationManager;
                mgr.IsInDollRoom = true;
                mgr.OnAnimationSetChanged();
                try {
                    Game.Instance.AnimationManagerController.Unsubscribe(mgr);
                    Game.Instance.InterpolationController.Remove(mgr);
                } catch (Exception ex) {
                    Main.Log.Log($"Error trying to detach the preview doll animation manager\n{ex}");
                }
                mgr.PlayableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                mgr.Tick(RealTimeController.SystemStepDurationSeconds);
                mgr.CustomUpdate(RealTimeController.SystemStepDurationSeconds);
            }
            SetLayerRecursive(m_DollGo.transform, RenderLayer);

            m_DollSource = avatar;
            m_DollUnitId = unit.UniqueId;
            m_BoundsDirty = true;
            return true;
        } catch (Exception ex) {
            Main.Log.Log($"Error trying to create the preview doll\n{ex}");
            DestroyDoll();
            return false;
        }
    }

    private bool ApplyDelta(PreviewSpec spec) {
        switch (spec.Type) {
            case PreviewSpec.Kind.AsIs:
                return true;
            case PreviewSpec.Kind.AddEE: {
                    var ee = new EquipmentEntityLink() { AssetId = spec.AssetId }.Load();
                    if (ee == null) {
                        Main.Log.Log($"Error trying to load EE {spec.AssetId} for the preview");
                        return false;
                    }
                    if (!m_Doll!.EquipmentEntities.Contains(ee)) {
                        try {
                            BypassAddFilter = true;
                            m_Doll.AddEquipmentEntity(ee);
                        } finally {
                            BypassAddFilter = false;
                        }
                    }
                    return true;
                }
            case PreviewSpec.Kind.RemoveEE: {
                    var ee = m_Doll!.EquipmentEntities.FirstOrDefault(e => e != null && e.name == spec.EEName);
                    if (ee != null) {
                        m_Doll.RemoveEquipmentEntity(ee);
                    }
                    return true;
                }
            case PreviewSpec.Kind.RampPair: {
                    var ee = m_Doll!.EquipmentEntities.FirstOrDefault(e => e != null && e.name == spec.EEName);
                    if (ee == null) {
                        return false;
                    }
                    m_Doll.SetRampIndices(ee, spec.Primary, spec.Secondary);
                    m_RampPreview = (spec.EEName!, spec.Primary, spec.Secondary);
                    return true;
                }
            default:
                return false;
        }
    }

    private void OnDollUpdated(Character c) {
        if (m_DollGo != null) {
            SetLayerRecursive(m_DollGo.transform, RenderLayer);
            c.UpdateSkeleton();
            m_BoundsDirty = true;
        }
    }

    private void DestroyDoll() {
        if (m_Doll != null) {
            m_Doll.OnUpdated -= OnDollUpdated;
            Helpers.UIdCache.Remove(m_Doll);
        }
        if (m_DollGo != null) {
            UnityEngine.Object.Destroy(m_DollGo);
            m_DollGo = null;
        }
        m_Doll = null;
        m_DollSource = null;
        m_RampPreview = null;
        m_HasGeometry = false;
        m_PoseForced = false;
        m_LiveKey = null;
        m_LiveReady = false;
    }

    private void ViewBasis(Entry e, out Vector3 right, out Vector3 up, out float dist, out float framedH) {
        var rot = Quaternion.Euler(e.Pitch, e.Yaw, 0f);
        var look = Quaternion.LookRotation(-(rot * Vector3.forward), Vector3.up);
        right = look * Vector3.right;
        up = look * Vector3.up;
        float r = Mathf.Max(m_Bounds.extents.magnitude, 0.05f);
        float baseDist = r / Mathf.Sin(Mathf.Deg2Rad * Fov * 0.5f) * FrameMargin;
        dist = baseDist * e.Zoom;
        framedH = 2f * dist * Mathf.Tan(Mathf.Deg2Rad * Fov * 0.5f);
    }

    private void PositionCamera(Entry e) {
        var rot = Quaternion.Euler(e.Pitch, e.Yaw, 0f);
        var offset = rot * Vector3.forward;
        ViewBasis(e, out _, out _, out var dist, out _);

        Vector3 focus = m_Bounds.center + e.FocusOffset;
        Vector3 camPos = focus + offset * dist;

        var t = m_Cam!.transform;
        t.position = camPos;
        t.rotation = Quaternion.LookRotation((focus - camPos).normalized, Vector3.up);
        m_Cam.fieldOfView = Fov;

        float r = Mathf.Max(m_Bounds.extents.magnitude, 0.05f);
        float dCenter = Vector3.Distance(camPos, m_Bounds.center);
        m_Cam.nearClipPlane = Mathf.Max(0.01f, dCenter - r * 2f);
        m_Cam.farClipPlane = dCenter + r * 2f;

        m_Key!.transform.rotation = t.rotation * Quaternion.Euler(35f, 25f, 0f);
        m_Fill!.transform.rotation = t.rotation * Quaternion.Euler(-20f, -35f, 0f);
    }

    private Bounds ComputeDollBounds() {
        m_HasGeometry = false;
        if (m_DollGo == null) {
            return new Bounds(m_Park, Vector3.one);
        }
        Vector3 root = m_DollGo.transform.position;
        const float maxCenterDistSqr = 10f * 10f;
        const float maxExtent = 7f;
        bool any = false;
        var b = new Bounds();
        IEnumerable<Renderer> renderers = (m_Doll != null && m_Doll.Renderers != null && m_Doll.Renderers.Any(r => r != null && r.enabled))
            ? m_Doll.Renderers
            : m_DollGo.GetComponentsInChildren<Renderer>();
        bool anyLoose = false;
        var loose = new Bounds();
        foreach (var r in renderers) {
            if (r == null || !r.enabled) {
                continue;
            }
            Bounds rb = (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                ? TransformBounds(smr.localToWorldMatrix, smr.sharedMesh.bounds)
                : r.bounds;
            if (!anyLoose) {
                loose = rb;
                anyLoose = true;
            } else {
                loose.Encapsulate(rb);
            }
            if ((rb.center - root).sqrMagnitude > maxCenterDistSqr || rb.extents.magnitude > maxExtent) {
                continue;
            }
            if (!any) {
                b = rb;
                any = true;
            } else {
                b.Encapsulate(rb);
            }
        }
        if (!any && anyLoose) {
            b = loose;
            any = true;
        }
        m_HasGeometry = any;
        return any ? b : new Bounds(m_Park, Vector3.one);
    }

    private void Snapshot(Entry e) {
        if (m_LiveRT == null) {
            return;
        }
        var prevActive = RenderTexture.active;
        var small = Downsample(m_LiveRT, SnapshotSize);
        RenderTexture.active = small;
        if (e.Snapshot == null) {
            EvictSnapshotsIfNeeded();
            e.Snapshot = new Texture2D(SnapshotSize, SnapshotSize, TextureFormat.RGBA32, false, false) {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
        e.Snapshot.ReadPixels(new Rect(0, 0, SnapshotSize, SnapshotSize), 0, 0, false);
        e.Snapshot.Apply(false, false);
        RenderTexture.active = prevActive;
        if (small != m_LiveRT) {
            RenderTexture.ReleaseTemporary(small);
        }
        e.SnapshotDirty = false;
    }

    private void EvictSnapshotsIfNeeded() {
        int count = m_Entries.Values.Count(e => e.Snapshot != null);
        if (count < MaxSnapshots) {
            return;
        }
        foreach (var kv in m_Entries.OrderBy(kv => kv.Value.LastDrawnFrame)) {
            if (count < MaxSnapshots) {
                break;
            }
            var e = kv.Value;
            if (e.Snapshot == null || kv.Key == m_LiveKey || kv.Key == m_ActiveKey) {
                continue;
            }
            UnityEngine.Object.Destroy(e.Snapshot);
            e.Snapshot = null;
            e.WantsRender = true;
            e.SnapshotDirty = true;
            count--;
        }
    }

    private static RenderTexture Downsample(RenderTexture src, int target) {
        var rw = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Default;
        var cur = src;
        int w = src.width;
        while (w > target) {
            int nw = Mathf.Max(target, w / 2);
            var dst = RenderTexture.GetTemporary(nw, nw, 0, RenderTextureFormat.ARGB32, rw);
            dst.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, dst);
            if (cur != src) {
                RenderTexture.ReleaseTemporary(cur);
            }
            cur = dst;
            w = nw;
        }
        return cur;
    }

    private void EnsureRig() {
        if (m_Rig != null) {
            return;
        }

        m_Rig = new GameObject("ReDressPreviewRig") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(m_Rig);
        m_Rig.transform.position = m_Park;

        m_LiveRT = new RenderTexture(LiveSize, LiveSize, 24, RenderTextureFormat.ARGB32,
            QualitySettings.activeColorSpace == ColorSpace.Linear ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Default) {
            name = "ReDressPreviewRT",
            filterMode = FilterMode.Bilinear
        };
        m_LiveRT.Create();

        var camGo = new GameObject("ReDressPreviewCam") { hideFlags = HideFlags.HideAndDontSave };
        camGo.transform.SetParent(m_Rig.transform, false);
        m_Cam = camGo.AddComponent<Camera>();
        m_Cam.enabled = false;
        m_Cam.clearFlags = CameraClearFlags.SolidColor;
        m_Cam.backgroundColor = Background;
        m_Cam.cullingMask = 1 << RenderLayer;
        m_Cam.targetTexture = m_LiveRT;
        m_Cam.allowHDR = false;
        m_Cam.allowMSAA = false;
        m_Cam.useOcclusionCulling = false;
        m_Cam.fieldOfView = Fov;

        m_Key = MakeLight("Key", 1.3f);
        m_Fill = MakeLight("Fill", 0.55f);

        m_Rig.AddComponent<PreviewPump>().Owner = this;

        try {
            var pbd = PositionBasedDynamicsConfig.Instance;
            if (pbd != null) {
                m_SavedPbdMode = pbd.UpdateMode;
                pbd.UpdateMode = UpdateMode.FixedUpdateFrequency;
            }
        } catch (Exception ex) {
            Main.Log.Log($"Error trying to change the cloth sim update mode\n{ex}");
        }

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private Light MakeLight(string name, float intensity) {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(m_Rig!.transform, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.cullingMask = 1 << RenderLayer;
        l.enabled = false;
        return l;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam) {
        if (m_Cam == null || cam != m_Cam) {
            return;
        }
        m_SavedProbe = RenderSettings.ambientProbe;
        m_SavedFog = RenderSettings.fog;
        m_RenderStateSwapped = true;
        try {
            if (Time.unscaledTime >= m_NextLightScan) {
                m_NextLightScan = Time.unscaledTime + LightScanInterval;
                m_SceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
            m_TempDisabledLights.Clear();
            foreach (var l in m_SceneLights) {
                if (l != null && l.enabled && l != m_Key && l != m_Fill) {
                    l.enabled = false;
                    m_TempDisabledLights.Add(l);
                }
            }
            if (m_Key != null) {
                m_Key.enabled = true;
            }
            if (m_Fill != null) {
                m_Fill.enabled = true;
            }

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(AmbientColor);
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = false;
        } catch (Exception ex) {
            Main.Log.Log($"Error trying to isolate the preview lighting\n{ex}");
            RestoreRenderState();
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam) {
        if (m_Cam == null || cam != m_Cam) {
            return;
        }
        RestoreRenderState();
    }

    private void RestoreRenderState() {
        if (!m_RenderStateSwapped) {
            return;
        }
        m_RenderStateSwapped = false;
        foreach (var l in m_TempDisabledLights) {
            if (l != null) {
                l.enabled = true;
            }
        }
        m_TempDisabledLights.Clear();
        if (m_Key != null) {
            m_Key.enabled = false;
        }
        if (m_Fill != null) {
            m_Fill.enabled = false;
        }
        RenderSettings.ambientProbe = m_SavedProbe;
        RenderSettings.fog = m_SavedFog;
    }

    private Entry GetEntry(string key) {
        if (!m_Entries.TryGetValue(key, out var e)) {
            e = new Entry();
            m_Entries[key] = e;
        }
        return e;
    }

    private void ClearEntries() {
        foreach (var e in m_Entries.Values) {
            if (e.Snapshot != null) {
                UnityEngine.Object.Destroy(e.Snapshot);
            }
        }
        m_Entries.Clear();
        m_ActiveKey = null;
        m_LastClickKey = null;
        m_LiveKey = null;
        m_LiveReady = false;
    }

    private static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) {
            SetLayerRecursive(t.GetChild(i), layer);
        }
    }

    private static Bounds TransformBounds(Matrix4x4 m, Bounds b) {
        var c = m.MultiplyPoint3x4(b.center);
        var e = b.extents;
        var ax = m.MultiplyVector(new Vector3(e.x, 0f, 0f));
        var ay = m.MultiplyVector(new Vector3(0f, e.y, 0f));
        var az = m.MultiplyVector(new Vector3(0f, 0f, e.z));
        var ne = new Vector3(
            Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
            Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
            Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        return new Bounds(c, ne * 2f);
    }

    public void Dispose() {
        RestoreRenderState();
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (m_SavedPbdMode != null) {
            try {
                var pbd = PositionBasedDynamicsConfig.Instance;
                if (pbd != null) {
                    pbd.UpdateMode = m_SavedPbdMode.Value;
                }
            } catch (Exception ex) {
                Main.Log.Log($"Error trying to restore the cloth sim update mode\n{ex}");
            }
            m_SavedPbdMode = null;
        }
        ClearEntries();
        DestroyDoll();
        m_DollUnitId = null;
        if (m_LiveRT != null) {
            if (m_Cam != null) {
                m_Cam.targetTexture = null;
            }
            m_LiveRT.Release();
            UnityEngine.Object.Destroy(m_LiveRT);
            m_LiveRT = null;
        }
        if (m_Rig != null) {
            UnityEngine.Object.Destroy(m_Rig);
            m_Rig = null;
        }
        m_Cam = null;
        m_Key = null;
        m_Fill = null;
        if (s_Instance == this) {
            s_Instance = null;
        }
    }
}

internal sealed class PreviewPump : MonoBehaviour {
    public LiveEEPreview? Owner;
    private void LateUpdate() {
        try {
            Owner?.Tick();
        } catch (Exception ex) {
            Main.Log.Log($"Error during preview update\n{ex}");
        }
    }
}
