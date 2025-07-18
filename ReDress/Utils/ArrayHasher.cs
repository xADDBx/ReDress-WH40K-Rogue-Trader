using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace ReDress;
public static class ArrayHasher {
    private static readonly XxHash64 m_Hasher = new();
    public static ulong ComputeHash(List<EntityPartStorage.CustomColor> data, int height, int width) {
        var floats = new float[3 * height * width];
        data.SelectMany<EntityPartStorage.CustomColor, float>(c => [c.R, c.G, c.B]).ToArray().AsSpan();
        for (int i = 0; i < data.Count; i++) {
            var tmp = data[i];
            floats[3 * i] = tmp.R;
            floats[3 * i + 1] = tmp.G;
            floats[3 * i + 2] = tmp.B;
        }
        var span = MemoryMarshal.AsBytes((Span<float>)floats);

        m_Hasher.Append(BitConverter.GetBytes(height));
        m_Hasher.Append(BitConverter.GetBytes(width));
        m_Hasher.Append(span);
        var hash = m_Hasher.GetCurrentHashAsUInt64();
        m_Hasher.Reset();
        return hash;
    }
}
