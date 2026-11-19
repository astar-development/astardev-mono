namespace AStarDev.CloudSync.Domain;

/// <summary>Base of every failure that crosses the provider boundary. Provider-specific exceptions are translated into these.</summary>
public abstract class ProviderException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed class RateLimitException(string message, Exception? innerException = null) : ProviderException(message, innerException);

public sealed class TransientProviderException(string message, Exception? innerException = null) : ProviderException(message, innerException);

public sealed class AuthenticationFailureException(string message, Exception? innerException = null) : ProviderException(message, innerException);

public sealed class PermanentProviderException(string message, Exception? innerException = null) : ProviderException(message, innerException);
