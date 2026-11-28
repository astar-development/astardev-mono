using System.Runtime.InteropServices;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// OneDrive's QuickXorHash, written from the published algorithm: byte <c>i</c> is XORed into a 160-bit block rotated left by <c>11 * i</c> bits, the block is read least significant byte first,
/// and the 64-bit content length is XORed into its last eight bytes. Streaming: append content in any split, then finish once.
/// Bytes 160 apart land at the same rotation, so content is first XORed into 160 columns eight bytes at a time and only the columns are rotated into the block when finishing.
/// </summary>
internal sealed class QuickXorHash
{
    private const int WidthInBits = 160;
    private const int WidthInBytes = WidthInBits / 8;
    private const int ShiftPerByte = 11;

    private readonly byte[] columns = new byte[WidthInBits];
    private long length;

    /// <summary>Hashes a whole buffer.</summary>
    /// <param name="content">The complete content.</param>
    /// <returns>The base64 digest, which is the text OneDrive reports as <c>quickXorHash</c>.</returns>
    public static ContentFingerprint Compute(ReadOnlySpan<byte> content)
    {
        var hash = new QuickXorHash();
        hash.Append(content);

        return hash.Finish();
    }

    /// <summary>Adds the next part of the content.</summary>
    /// <param name="content">The bytes that follow those already appended. Splitting the content differently never changes the digest.</param>
    public void Append(ReadOnlySpan<byte> content)
    {
        var column = (int)(length % WidthInBits);
        length += content.Length;
        var head = Math.Min(content.Length, (WidthInBits - column) % WidthInBits);
        XorInto(columns.AsSpan(column), content[..head]);
        content = content[head..];
        var words = MemoryMarshal.Cast<byte, ulong>(columns.AsSpan());

        while (content.Length >= WidthInBits)
        {
            var block = MemoryMarshal.Cast<byte, ulong>(content[..WidthInBits]);
            for (var index = 0; index < words.Length; index++) words[index] ^= block[index];
            content = content[WidthInBits..];
        }

        XorInto(columns, content);
    }

    /// <summary>Produces the digest of everything appended. It does not reset the hash, so appending more and finishing again hashes the whole content.</summary>
    /// <returns>The base64 digest.</returns>
    public ContentFingerprint Finish()
    {
        var digest = new byte[WidthInBytes];

        for (var column = 0; column < WidthInBits; column++)
        {
            var bitOffset = column * ShiftPerByte % WidthInBits;
            var position = bitOffset / 8;
            var shifted = columns[column] << (bitOffset % 8);
            digest[position] ^= (byte)shifted;
            digest[(position + 1) % WidthInBytes] ^= (byte)(shifted >> 8);
        }

        var lengthBytes = BitConverter.GetBytes(length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

        for (var index = 0; index < lengthBytes.Length; index++) digest[WidthInBytes - lengthBytes.Length + index] ^= lengthBytes[index];

        return new ContentFingerprint(Convert.ToBase64String(digest));
    }

    private static void XorInto(Span<byte> target, ReadOnlySpan<byte> source)
    {
        for (var index = 0; index < source.Length; index++) target[index] ^= source[index];
    }
}
