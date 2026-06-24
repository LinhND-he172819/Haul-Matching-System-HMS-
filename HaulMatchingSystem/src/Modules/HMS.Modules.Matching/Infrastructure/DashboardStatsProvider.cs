using System.Data;
using HMS.Shared.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Matching.Infrastructure
{
    public class DashboardStatsProvider : IDashboardStatsProvider
    {
        private readonly MatchingDbContext _db;

        public DashboardStatsProvider(MatchingDbContext db)
        {
            _db = db;
        }

        public async Task<(int activeTrips, int inTransitShipments, double avgUtilisation, int agingHubItems)> GetStatsAsync(CancellationToken ct)
        {
            try
            {
                var activeTrips = await CountByStatusAsync("trips", ["Active"], ct);
                var inTransitShipments = await CountByStatusAsync("shipments", ["In_Transit", "Matched"], ct);
                var agingHubItems = await CountByStatusAsync("shipments", ["In_Warehouse"], ct);

                return (activeTrips, inTransitShipments, 0, agingHubItems);
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }

        private async Task<int> CountByStatusAsync(
            string tableName,
            IReadOnlyCollection<string> statuses,
            CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var statusColumn = await ResolveColumnAsync(connection, tableName, ["status", "Status"], cancellationToken);
                if (statusColumn is null)
                {
                    return 0;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT COUNT(*)::int
                    FROM public.{QuoteIdentifier(tableName)}
                    WHERE {QuoteIdentifier(statusColumn)} = ANY (@statuses)
                    """;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "statuses";
                parameter.Value = statuses.ToArray();
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync(cancellationToken);

                return result is null ? 0 : Convert.ToInt32(result);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static async Task<string?> ResolveColumnAsync(
            System.Data.Common.DbConnection connection,
            string tableName,
            IReadOnlyCollection<string> candidates,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT attname
                FROM pg_attribute
                WHERE attrelid = to_regclass(@table_name)
                    AND attname = ANY (@candidates)
                    AND NOT attisdropped
                ORDER BY array_position(@candidates, attname)
                LIMIT 1;
                """;

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "table_name";
            tableParameter.Value = $"public.{tableName}";
            command.Parameters.Add(tableParameter);

            var candidatesParameter = command.CreateParameter();
            candidatesParameter.ParameterName = "candidates";
            candidatesParameter.Value = candidates.ToArray();
            command.Parameters.Add(candidatesParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result?.ToString();
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }
    }
}
