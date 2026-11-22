using System.Runtime.InteropServices;
using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary><see cref="ILocalIdentityReader"/> that calls libc <c>stat</c> on 64-bit Linux and reads the leading <c>st_dev</c> and <c>st_ino</c> fields, which sit first in the structure on x86-64 and arm64. Any other platform yields no identity.</summary>
internal sealed partial class UnixLocalIdentityReader : ILocalIdentityReader
{
    private const int StatBufferSize = 512;

    /// <inheritdoc/>
    /// <remarks>The identity text is <c>device:inode</c> in decimal, so equal text means the same device and inode.</remarks>
    public LocalIdentity? Read(string fullPath)
    {
        if (!IsSupported()) return null;

        return Try.Run(() => ReadStat(fullPath)) switch
        {
            Success<LocalIdentity?> success => success.Value,
            Failure<LocalIdentity?> => null
        };
    }

    private static bool IsSupported() => OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;

    private static LocalIdentity? ReadStat(string fullPath)
    {
        var buffer = new byte[StatBufferSize];
        if (Stat(fullPath, buffer) != 0) return null;

        var device = BitConverter.ToUInt64(buffer, 0);
        var inode = BitConverter.ToUInt64(buffer, sizeof(ulong));

        return new LocalIdentity($"{device}:{inode}");
    }

    [LibraryImport("libc", EntryPoint = "stat", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Stat(string path, [Out] byte[] buffer);
}
