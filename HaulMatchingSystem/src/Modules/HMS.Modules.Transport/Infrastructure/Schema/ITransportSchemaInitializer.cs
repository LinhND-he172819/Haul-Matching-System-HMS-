namespace HMS.Modules.Transport.Infrastructure.Schema;

public interface ITransportSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
