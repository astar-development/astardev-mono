using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal static class TestMarker
{
    public static ReauthenticationMarker Create() => new(new FakeAccountStore(), NullLogger<ReauthenticationMarker>.Instance);
}
