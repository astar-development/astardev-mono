namespace AStarDev.CloudSync.Domain;

/// <summary>The provider's bookmark for "everything that changed since this point". Replaying it fetches only newer changes.</summary>
/// <param name="Value">Opaque provider text. Store and return it unchanged; never parse it.</param>
public readonly record struct DeltaToken(string Value);
