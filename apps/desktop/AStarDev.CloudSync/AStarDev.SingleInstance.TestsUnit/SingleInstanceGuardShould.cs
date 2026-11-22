using System.Net.Sockets;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AStarDev.SingleInstance.TestsUnit;

public sealed class SingleInstanceGuardShould : IDisposable
{
    private readonly TemporaryDataFolder data = new();
    private readonly RecordingLogger logger = new();
    private readonly ServiceProvider provider;

    public SingleInstanceGuardShould() => provider = new ServiceCollection()
        .AddLogging(logging => logging.ClearProviders().AddProvider(logger))
        .AddSingleInstance()
        .BuildServiceProvider();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private ISingleInstanceGuard Guard => provider.GetRequiredService<ISingleInstanceGuard>();

    [Fact]
    public async Task return_primary_when_no_instance_is_running()
    {
        var claim = await Guard.ClaimAsync(data.Options(), CancellationToken);

        await using var primary = claim.ShouldBeOfType<InstanceClaim.Primary>().Instance;
    }

    [Fact]
    public async Task hand_off_a_second_launch_and_ask_the_running_instance_to_show_itself()
    {
        await using var primary = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;
        int activations = 0;
        primary.ActivationRequested += (_, _) => Interlocked.Increment(ref activations);

        var second = await Guard.ClaimAsync(data.Options(), CancellationToken);

        second.ShouldBe(InstanceClaimFactory.CreateHandedOff());
        activations.ShouldBe(1);
    }

    [Fact]
    public async Task return_primary_for_the_next_launch_after_the_running_instance_is_disposed()
    {
        var first = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;
        await first.DisposeAsync();

        var next = await Guard.ClaimAsync(data.Options(), CancellationToken);

        await using var primary = next.ShouldBeOfType<InstanceClaim.Primary>().Instance;
    }

    [Fact]
    public async Task return_primary_and_accept_hand_offs_when_a_crashed_instance_left_its_socket_file_behind()
    {
        data.Folder.Create();
        await data.Folder.FileSystem.File.WriteAllTextAsync(data.SocketPath, "stale", CancellationToken);
        await using var primary = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;

        var second = await Guard.ClaimAsync(data.Options(), CancellationToken);

        second.ShouldBe(InstanceClaimFactory.CreateHandedOff());
    }

    [Fact]
    public async Task return_not_answered_and_log_a_warning_when_the_lock_is_held_but_nothing_listens()
    {
        data.Folder.Create();
        await using var heldLock = new FileStream(data.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var claim = await Guard.ClaimAsync(data.Options(), CancellationToken);

        claim.ShouldBe(InstanceClaimFactory.CreateNotAnswered());
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task return_not_answered_after_the_timeout_when_the_running_instance_never_answers()
    {
        data.Folder.Create();
        await using var heldLock = new FileStream(data.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var silentListener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        silentListener.Bind(new UnixDomainSocketEndPoint(data.SocketPath));
        silentListener.Listen();

        var claim = await Guard.ClaimAsync(data.Options(TimeSpan.FromMilliseconds(300)), CancellationToken);

        claim.ShouldBe(InstanceClaimFactory.CreateNotAnswered());
    }

    [Fact]
    public async Task return_primary_for_two_users_with_different_data_folders()
    {
        using var otherUser = new TemporaryDataFolder();
        await using var first = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;

        var second = await Guard.ClaimAsync(otherUser.Options(), CancellationToken);

        await using var secondPrimary = second.ShouldBeOfType<InstanceClaim.Primary>().Instance;
    }

    [Fact]
    public async Task return_primary_for_exactly_one_of_many_racing_launches()
    {
        var claims = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Guard.ClaimAsync(data.Options(TimeSpan.FromSeconds(2)), CancellationToken), CancellationToken)));
        var primaries = claims.OfType<InstanceClaim.Primary>().ToList();

        primaries.Count.ShouldBe(1);
        claims.Where(claim => claim is not InstanceClaim.Primary).ShouldAllBe(claim => claim is InstanceClaim.HandedOff || claim is InstanceClaim.NotAnswered);
        await primaries[0].Instance.DisposeAsync();
    }

    [Fact]
    public async Task return_lock_unavailable_and_log_an_error_when_the_data_folder_cannot_be_created()
    {
        data.Folder.Parent!.Create();
        await data.Folder.FileSystem.File.WriteAllTextAsync(data.Folder.FullName, "a file where the folder should be", CancellationToken);

        var claim = await Guard.ClaimAsync(data.Options(), CancellationToken);

        claim.ShouldBe(InstanceClaimFactory.CreateLockUnavailable());
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task return_lock_unavailable_when_the_lock_file_name_is_too_long_for_the_file_system()
    {
        var options = SingleInstanceOptionsFactory.Create(data.Folder, new string('n', 300), TimeSpan.FromMilliseconds(300));

        var claim = await Guard.ClaimAsync(options, CancellationToken);

        claim.ShouldBe(InstanceClaimFactory.CreateLockUnavailable());
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task return_lock_unavailable_when_the_data_folder_is_read_only()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.UserName == "root", "Unix permissions only, and root ignores them.");
        data.Folder.Create();
        File.SetUnixFileMode(data.Folder.FullName, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var claim = await Guard.ClaimAsync(data.Options(), CancellationToken);

        File.SetUnixFileMode(data.Folder.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        claim.ShouldBe(InstanceClaimFactory.CreateLockUnavailable());
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task restrict_the_socket_of_the_running_instance_to_its_owner()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
        await using var primary = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;

        File.GetUnixFileMode(data.SocketPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task stop_listening_and_log_an_error_when_accepting_keeps_failing()
    {
        var broken = HandOffProtocol.CreateSocket();
        broken.Dispose();
        await using var heldLock = new MemoryStream();
        var socketFile = data.Folder.FileSystem.FileInfo.New(data.SocketPath);

        await using var primary = PrimaryInstance.Start(heldLock, Option.Some(broken), socketFile, logger);

        await WaitUntilAsync(() => logger.Entries.Any(entry => entry.Level == LogLevel.Error));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
            await Task.Delay(20, limit.Token);
    }

    [Fact]
    public async Task return_primary_and_log_a_warning_when_the_socket_path_is_too_long()
    {
        var options = SingleInstanceOptionsFactory.Create(data.Folder, new string('s', 120), TimeSpan.FromMilliseconds(300));

        var claim = await Guard.ClaimAsync(options, CancellationToken);

        await using var primary = claim.ShouldBeOfType<InstanceClaim.Primary>().Instance;
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task not_activate_the_running_instance_for_a_request_other_than_show_and_keep_answering()
    {
        await using var primary = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;
        int activations = 0;
        primary.ActivationRequested += (_, _) => Interlocked.Increment(ref activations);
        using (var stranger = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            await stranger.ConnectAsync(new UnixDomainSocketEndPoint(data.SocketPath), CancellationToken);
            _ = await stranger.SendAsync("hello\n"u8.ToArray(), SocketFlags.None, CancellationToken);
        }

        var second = await Guard.ClaimAsync(data.Options(), CancellationToken);

        (second, activations).ShouldBe((InstanceClaimFactory.CreateHandedOff(), 1));
    }

    [Fact]
    public async Task hand_off_a_second_launch_even_when_an_activation_handler_throws()
    {
        await using var primary = (await Guard.ClaimAsync(data.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;
        primary.ActivationRequested += (_, _) => throw new InvalidOperationException("The window is gone.");

        var second = await Guard.ClaimAsync(data.Options(), CancellationToken);

        second.ShouldBe(InstanceClaimFactory.CreateHandedOff());
    }

    [Fact]
    public async Task cancel_the_claim_when_the_caller_cancels_during_the_hand_off()
    {
        data.Folder.Create();
        await using var heldLock = new FileStream(data.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var silentListener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        silentListener.Bind(new UnixDomainSocketEndPoint(data.SocketPath));
        silentListener.Listen();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(() => Guard.ClaimAsync(data.Options(TimeSpan.FromSeconds(5)), cancellation.Token));
    }

    [Fact]
    public async Task cancel_the_claim_when_the_caller_cancels()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Guard.ClaimAsync(data.Options(), cancellation.Token));
    }

    [Fact]
    public async Task keep_a_data_folder_path_that_holds_personal_data_out_of_the_logs()
    {
        using var personal = new TemporaryDataFolder("jane.doe@example.com");
        await using var primary = (await Guard.ClaimAsync(personal.Options(), CancellationToken)).ShouldBeOfType<InstanceClaim.Primary>().Instance;
        _ = await Guard.ClaimAsync(personal.Options(), CancellationToken);

        logger.Entries.ShouldNotBeEmpty();
        logger.Entries.ShouldAllBe(entry => !entry.Message.Contains("jane.doe", StringComparison.OrdinalIgnoreCase) && !entry.Message.Contains(personal.Folder.FullName, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        provider.Dispose();
        logger.Dispose();
        data.Dispose();
    }
}
