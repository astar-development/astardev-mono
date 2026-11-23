using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>The stable code of each <see cref="IpcError"/> case, in the <c>CS-IPC</c> block. Never renumber a shipped code.</summary>
internal static class IpcErrorCodes
{
    /// <summary>Code of <see cref="ConnectionFailed"/>.</summary>
    public static readonly ErrorCode ConnectionFailed = Read("CS-IPC-0001");

    /// <summary>Code of <see cref="NotConnected"/>.</summary>
    public static readonly ErrorCode NotConnected = Read("CS-IPC-0002");

    /// <summary>Code of <see cref="ConnectionLost"/>.</summary>
    public static readonly ErrorCode ConnectionLost = Read("CS-IPC-0003");

    /// <summary>Code of <see cref="FrameTooLarge"/>.</summary>
    public static readonly ErrorCode FrameTooLarge = Read("CS-IPC-0004");

    /// <summary>Code of <see cref="MalformedFrame"/>.</summary>
    public static readonly ErrorCode MalformedFrame = Read("CS-IPC-0005");

    /// <summary>Code of <see cref="HandlerFailed"/>.</summary>
    public static readonly ErrorCode HandlerFailed = Read("CS-IPC-0006");

    /// <summary>Code of <see cref="ListenFailed"/>.</summary>
    public static readonly ErrorCode ListenFailed = Read("CS-IPC-0007");

    /// <summary>Turns code text into an <see cref="ErrorCode"/>. Empty (<c>default</c>) when the text is not a valid code, so a bad code from the peer never fails a reply.</summary>
    public static ErrorCode Read(string text) => ErrorCode.Create(text).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
