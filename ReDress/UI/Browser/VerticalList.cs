﻿using UnityEngine;
using static ReDress.UIHelpers;

namespace ReDress;

/// <summary>
/// A vertical paginated UI list for displaying and interacting with a collection of items of type <typeparamref name="T"/>.
/// Supports optional detail toggling and customizable pagination settings.
/// </summary>
/// <typeparam name="T">The type of items to display. Must be non-nullable.</typeparam>
public class VerticalList<T> : IPagedList where T : notnull {
    protected int PageWidth = 600;
    protected int CurrentPage = 1;
    protected int PagedItemsCount = 0;
    protected int TotalPages = 1;
    protected int ItemCount = 0;
    protected bool ShowDivBetweenItems = true;
    protected readonly Dictionary<object, T> ToggledDetailGUIs = [];
    protected IEnumerable<T> PagedItems = [];
    protected IEnumerable<T> Items = [];
    protected int PageLimit;
    public float? TrackedWidth = null;
    public float? TrackedWidth2 = null;
    protected int EffectivePageLimit {
        get {
            return PageLimit;
        }
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="VerticalList{T}"/> class.
    /// </summary>
    /// <param name="initialItems">
    /// Optional initial collection of items to populate the browser with.
    /// <para>
    /// If null, the browser starts empty until <see cref="RegisterShowAllItems"/> or
    /// <see cref="VerticalList{T}.QueueUpdateItems(IEnumerable{T}, int?)"/> is called.
    /// </para>
    /// </param>
    /// <param name="showDivBetweenItems">Whether to draw a divider between items in the list.</param>
    /// <param name="overridePageWidth">Optional override for the width of the list. Default width is 600.</param>
    /// <param name="overridePageLimit">Optional override for the number of items per page. Default PageLimit is a setting with 25 as initial value.</param>
    public VerticalList(IEnumerable<T>? initialItems = null, bool showDivBetweenItems = true, int? overridePageWidth = null, int? overridePageLimit = null) {
        if (overridePageWidth.HasValue) {
            PageWidth = overridePageWidth.Value;
        }
        PageLimit = overridePageLimit ?? Main.m_Settings.BrowserPageLimit;
        if (initialItems != null) {
            QueueUpdateItems(initialItems);
        }
        ShowDivBetweenItems = showDivBetweenItems;
    }
    /// <summary>
    /// Clears all expanded detail sections.
    /// </summary>
    public void ClearDetails() => ToggledDetailGUIs.Clear();
    /// <summary>
    /// Toggles a collapsible detail section for the specified item.
    /// </summary>
    /// <param name="target">The item associated with the detail section.</param>
    /// <param name="key">A unique key identifying the detail section (can be different from the item itself). It's an object because T can be a struct, but structs can't be used as keys.</param>
    /// <param name="title">Optional title displayed on the disclosure toggle.</param>
    /// <param name="width">Optional width for the toggle control.</param>
    /// <returns><c>true</c> if the toggle changed state; otherwise, <c>false</c>.</returns>
    public bool DetailToggle(T target, object key, string? title = null, int width = 400) {
        var changed = false;
        if (key == null) key = target;
        var expanded = ToggledDetailGUIs.ContainsKey(key);
        if (DisclosureToggle(ref expanded, title, Width(width))) {
            changed = true;
            if (expanded) {
                ToggledDetailGUIs[key] = target;
            }
        }
        return changed;
    }
    /// <summary>
    /// Queues an update to replace the current item list with a new collection. Runs on the main thread.
    /// </summary>
    /// <param name="newItems">The new items to display.</param>
    /// <param name="forcePage">If provided, forces the list to jump to the specified page after update.</param>
    /// <param name="onlyDisplayedItems">Whether the update actually changes the base item collection (or just restricts it to a subset due e.g. a search</param>
    public virtual void QueueUpdateItems(IEnumerable<T> newItems, int? forcePage = null, bool onlyDisplayedItems = false) {
        Main.ScheduleForMainThread(new(() => {
            UpdateItems(newItems, forcePage, onlyDisplayedItems);
        }));
    }
    /// <summary>
    /// Runs an update to replace the current item list with a new collection. Prefer the usage of <see cref="QueueUpdateItems(IEnumerable{T}, int?)"./>
    /// </summary>
    /// <param name="newItems">The new items to display.</param>
    /// <param name="forcePage">If provided, forces the list to jump to the specified page after update.</param>
    /// <param name="onlyDisplayedItems">Whether the update actually changes the base item collection (or just restricts it to a subset due e.g. a search</param>
    internal virtual void UpdateItems(IEnumerable<T> newItems, int? forcePage = null, bool onlyDisplayedItems = false) {
        if (forcePage != null) {
            CurrentPage = 1;
        }
        Items = newItems;
        ItemCount = Items.Count();
        UpdatePages();
    }
    public virtual void UpdatePages() {
        if (EffectivePageLimit > 0) {
            TotalPages = (int)Math.Ceiling((double)ItemCount / EffectivePageLimit);
            CurrentPage = Math.Max(Math.Min(CurrentPage, TotalPages), 1);
        } else {
            CurrentPage = 1;
            TotalPages = 1;
        }
        UpdatePagedItems();
    }
    protected virtual void UpdatePagedItems() {
        var offset = Math.Min(ItemCount, (CurrentPage - 1) * EffectivePageLimit);
        PagedItemsCount = Math.Min(EffectivePageLimit, ItemCount - offset);
        PagedItems = Items.Skip(offset).Take(PagedItemsCount);
        if (ReferenceEquals(this, Main.IncludeBrowser)) {
            void UpdateWidths() {
                TrackedWidth = CalculateLargestLabelSize(PagedItems.Select(i => Main.m_Settings.AssetMapping![(i as string)!]));
                TrackedWidth2 = CalculateLargestLabelSize(PagedItems.Select(i => (i as string)!), GUI.skin.textArea);
            }
            if (GUIUtility.guiDepth > 0) {
                UpdateWidths();
            } else {
                Main.ScheduleForGuiThread(UpdateWidths);
            }
        }
    }
    protected void PageGUI() {
        Label($"{SharedStrings.ShowingText.Orange()} {PagedItemsCount.ToString().Cyan()} / {ItemCount.ToString().Cyan()} {SharedStrings.ResultsText.Orange()},   " +
            $"{SharedStrings.PageText.Orange()}: {CurrentPage.ToString().Cyan()} / {Math.Max(1, TotalPages).ToString().Cyan()}");
        if (TotalPages > 1) {
            Space(25);
            if (Button("-")) {
                if (CurrentPage <= 1) {
                    CurrentPage = TotalPages;
                } else {
                    CurrentPage -= 1;
                }
                UpdatePagedItems();
            }
            if (Button("+")) {
                if (CurrentPage >= TotalPages) {
                    CurrentPage = 1;
                } else {
                    CurrentPage += 1;
                }
                UpdatePagedItems();
            }
        }
    }
    protected virtual void HeaderGUI() {
        using (HorizontalScope()) {
            PageGUI();
        }
    }
    /// <summary>
    /// Renders the paged list using the provided item GUI rendering callback.
    /// </summary>
    /// <param name="onItemGUI">A delegate that renders an individual item of type <typeparamref name="T"/>.</param>
    public virtual void OnGUI(Action<T> onItemGUI) {
        using (VerticalScope(PageWidth)) {
            HeaderGUI();
            foreach (var item in PagedItems) {
                if (ShowDivBetweenItems) {
                    DrawDiv();
                }
                onItemGUI(item);
            }
        }
    }
    /// <summary>
    /// Renders a detail panel for an item if it is currently expanded.
    /// </summary>
    /// <param name="key">The key identifying the detail section.</param>
    /// <param name="onDetailGUI">The delegate that renders the detail UI for the item.</param>
    /// <returns><c>true</c> if the detail panel was rendered; otherwise, <c>false</c>.</returns>
    public bool DetailGUI(object key, Action<T> onDetailGUI) {
        ToggledDetailGUIs.TryGetValue(key, out var target);
        if (target != null) {
            onDetailGUI(target);
            return true;
        } else {
            return false;
        }
    }
}
