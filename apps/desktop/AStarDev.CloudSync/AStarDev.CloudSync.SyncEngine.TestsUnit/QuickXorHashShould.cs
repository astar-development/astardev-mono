using System.Text;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class QuickXorHashShould
{
    public static TheoryData<string, string> Vectors => new()
    {
        { string.Empty, "AAAAAAAAAAAAAAAAAAAAAAAAAAA=" },
        { "a", "YQAAAAAAAAAAAAAAAQAAAAAAAAA=" },
        { "abc", "YRDDGAAAAAAAAAAAAwAAAAAAAAA=" },
        { "The quick brown fox jumps over the lazy dog", "bMSlbysmxJL6S75XwfMcQZOpcr4=" }
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void match_the_known_vectors(string text, string expected)
        => QuickXorHash.Compute(Encoding.ASCII.GetBytes(text)).ShouldBe(new ContentFingerprint(expected));

    [Fact]
    public void match_the_known_vectors_for_input_longer_than_the_hash_width()
    {
        QuickXorHash.Compute(Enumerable.Range(0, 1000).Select(index => (byte)(index % 256)).ToArray()).ShouldBe(new ContentFingerprint("eMTc7ksneLHFfWI8T9W9y23sExM="));
        QuickXorHash.Compute(Enumerable.Range(0, 5000).Select(index => (byte)((index * 7 + 3) % 256)).ToArray()).ShouldBe(new ContentFingerprint("4/GMmuF3EXP3HLUJvgacVA3BgPg="));
    }

    [Fact]
    public void give_the_same_result_however_the_input_is_split()
    {
        var content = Enumerable.Range(0, 5000).Select(index => (byte)((index * 7 + 3) % 256)).ToArray();
        var hash = new QuickXorHash();

        hash.Append(content.AsSpan(0, 13));
        hash.Append(content.AsSpan(13, 1000));
        hash.Append(content.AsSpan(1013));

        hash.Finish().ShouldBe(QuickXorHash.Compute(content));
    }
}
