using AStarDev.CloudSync.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Maps each strongly typed Domain value to the primitive it is stored as, so entities carry the Domain types and never a bare <see cref="Guid"/> or <see cref="string"/>.</summary>
internal static class StronglyTypedIdConverters
{
    private static readonly Dictionary<Type, ValueConverter> Converters = new()
    {
        [typeof(AccountId)] = new ValueConverter<AccountId, Guid>(id => id.Value, value => new AccountId(value)),
        [typeof(SyncJobId)] = new ValueConverter<SyncJobId, Guid>(id => id.Value, value => new SyncJobId(value)),
        [typeof(ItemId)] = new ValueConverter<ItemId, Guid>(id => id.Value, value => new ItemId(value)),
        [typeof(SnapshotId)] = new ValueConverter<SnapshotId, Guid>(id => id.Value, value => new SnapshotId(value)),
        [typeof(OperationId)] = new ValueConverter<OperationId, Guid>(id => id.Value, value => new OperationId(value)),
        [typeof(ConflictId)] = new ValueConverter<ConflictId, Guid>(id => id.Value, value => new ConflictId(value)),
        [typeof(RemoteItemId)] = new ValueConverter<RemoteItemId, string>(id => id.Value, value => new RemoteItemId(value)),
        [typeof(RelativePath)] = new ValueConverter<RelativePath, string>(path => path.Value, value => new RelativePath(value)),
        [typeof(DeltaToken)] = new ValueConverter<DeltaToken, string>(token => token.Value, value => new DeltaToken(value)),
        [typeof(ETag)] = new ValueConverter<ETag, string>(tag => tag.Value, value => new ETag(value))
    };

    /// <summary>Finds the converter for a property type, looking through <see cref="Nullable{T}"/>.</summary>
    /// <param name="propertyType">The CLR type of the entity property.</param>
    /// <param name="converter">The converter when the type is one of ours.</param>
    /// <returns><see langword="true"/> when <paramref name="propertyType"/> is a strongly typed Domain value.</returns>
    public static bool TryGet(Type propertyType, out ValueConverter converter)
        => Converters.TryGetValue(Nullable.GetUnderlyingType(propertyType) ?? propertyType, out converter!);
}
