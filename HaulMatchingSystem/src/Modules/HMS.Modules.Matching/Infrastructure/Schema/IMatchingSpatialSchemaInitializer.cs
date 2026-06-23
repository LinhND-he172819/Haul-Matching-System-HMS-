namespace HMS.Modules.Matching.Infrastructure.Schema;

public interface IMatchingSpatialSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
