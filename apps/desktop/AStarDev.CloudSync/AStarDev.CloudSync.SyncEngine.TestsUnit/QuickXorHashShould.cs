using System.Diagnostics;
using System.Text;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class QuickXorHashShould
{
    private const string PerfVariable = "CLOUDSYNC_PERF";

    public static TheoryData<string, string> Vectors => new()
    {
        { string.Empty, "AAAAAAAAAAAAAAAAAAAAAAAAAAA=" },
        { "a", "YQAAAAAAAAAAAAAAAQAAAAAAAAA=" },
        { "abc", "YRDDGAAAAAAAAAAAAwAAAAAAAAA=" },
        { "The quick brown fox jumps over the lazy dog", "bMSlbysmxJL6S75XwfMcQZOpcr4=" }
    };

    public static TheoryData<int> EdgeLengths => [0, 1, 7, 8, 9, 19, 20, 21, 159, 160, 161, 319, 320, 321, 1024 * 1024 + 3];

    public static TheoryData<int> SplitSizes => [1, 7, 8, 9, 20, 159, 160, 161, 4096];

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

    [Theory]
    [MemberData(nameof(EdgeLengths))]
    public void match_the_byte_at_a_time_algorithm_at_every_word_and_block_edge(int length)
    {
        var content = RandomContent(length);

        QuickXorHash.Compute(content).ShouldBe(ReferenceHash(content));
    }

    [Theory]
    [MemberData(nameof(SplitSizes))]
    public void match_the_byte_at_a_time_algorithm_when_appended_in_pieces_of_any_size(int pieceSize)
    {
        var content = RandomContent(10_007);
        var hash = new QuickXorHash();

        foreach (var piece in content.Chunk(pieceSize))
            hash.Append(piece);

        hash.Finish().ShouldBe(ReferenceHash(content));
    }

    [Fact]
    public void give_the_same_digest_when_finished_twice_and_include_what_was_appended_in_between()
    {
        var content = RandomContent(500);
        var hash = new QuickXorHash();
        hash.Append(content.AsSpan(0, 200));

        hash.Finish().ShouldBe(hash.Finish());
        hash.Append(content.AsSpan(200));
        hash.Finish().ShouldBe(ReferenceHash(content));
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void hash_at_least_three_times_faster_than_the_byte_at_a_time_algorithm()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(PerfVariable) == "1", $"Opt-in throughput test: set {PerfVariable}=1 and run a Release build.");
        var content = RandomContent(64 * 1024 * 1024);
        QuickXorHash.Compute(content.AsSpan(0, 1024 * 1024)).ShouldBe(ReferenceHash(content.AsSpan(0, 1024 * 1024)));

        var reference = MegabytesPerSecond(content.Length, () => ReferenceHash(content));
        var single = MegabytesPerSecond(content.Length, () => QuickXorHash.Compute(content));
        var parallel = MegabytesPerSecond(content.Length * 4, () => Parallel.For(0, 4, _ => QuickXorHash.Compute(content)));
        TestContext.Current.TestOutputHelper?.WriteLine($"byte-at-a-time {reference:F0} MB/s, word-wise {single:F0} MB/s, word-wise on 4 threads {parallel:F0} MB/s");

        single.ShouldBeGreaterThanOrEqualTo(reference * 3);
    }

    private static double MegabytesPerSecond(long bytes, Action run)
    {
        run();
        var best = TimeSpan.MaxValue;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            run();
            if (watch.Elapsed < best) best = watch.Elapsed;
        }

        return bytes / 1_000_000d / best.TotalSeconds;
    }

    private static byte[] RandomContent(int length)
    {
        var content = new byte[length];
        new Random(length).NextBytes(content);

        return content;
    }

    private static ContentFingerprint ReferenceHash(ReadOnlySpan<byte> content)
    {
        var block = new byte[20];
        var bitOffset = 0;

        foreach (var value in content)
        {
            var position = bitOffset / 8;
            var shifted = value << (bitOffset % 8);
            block[position] ^= (byte)shifted;
            block[(position + 1) % 20] ^= (byte)(shifted >> 8);
            bitOffset = (bitOffset + 11) % 160;
        }

        var lengthBytes = BitConverter.GetBytes((long)content.Length);
        for (var index = 0; index < lengthBytes.Length; index++) block[12 + index] ^= lengthBytes[index];

        return new ContentFingerprint(Convert.ToBase64String(block));
    }
}
