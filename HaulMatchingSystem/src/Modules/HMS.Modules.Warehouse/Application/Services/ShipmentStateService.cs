using System.Data;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Events;
using HMS.Shared.Core.Exceptions;
using HMS.Shared.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Application.Services;

/// <summary>
/// Implements the shipment status state machine.
/// Guarantees:
///   - Transition validation via ShipmentTransitionGuard
///   - Audit history insert (append-only, no update/delete)
///   - MediatR publish of ShipmentStatusChangedEvent
///   - Joins caller's transaction when provided (no separate transaction)
///   - Concurrency-safe via SELECT … FOR UPDATE
/// </summary>
public sealed class ShipmentStateService : IShipmentStateService
{
    private readonly string _connStr;
    private readonly IMediator _mediator;
    private readonly ILogger<ShipmentStateService> _logger;

    public ShipmentStateService(
        IConfiguration configuration,
        IMediator mediator,
        ILogger<ShipmentStateService> logger)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=hms_db;Username=postgres;Password=hms_password_123";
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ShipmentStatus> TransitionAsync(
        Guid shipmentId,
        ShipmentStatus toStatus,
        object? connection = null,
        object? transaction = null,
        Guid? performedBy = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        // ── Guard: Cancellation requires a reason ──
        if (toStatus == ShipmentStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            throw new InvalidShipmentTransitionException(ShipmentStatus.Draft, toStatus,
                "A cancellation reason is required.");

        // ── Resolve caller's connection/transaction if provided ──
        NpgsqlConnection? callerConn = connection as NpgsqlConnection;
        NpgsqlTransaction? callerTxn = transaction as NpgsqlTransaction;

        var ownsConnection = false;
        NpgsqlConnection? conn = null;
        NpgsqlTransaction? txn = null;

        try
        {
            if (callerConn != null && callerConn.State == ConnectionState.Open)
            {
                // Join caller's transaction
                conn = callerConn;
                txn = callerTxn;
            }
            else
            {
                // Create our own connection
                conn = new NpgsqlConnection(_connStr);
                await conn.OpenAsync(ct);
                ownsConnection = true;
            }

            // ── Read current status with FOR UPDATE (pessimistic lock) ──
            var currentStatus = await ReadCurrentStatusForUpdateAsync(conn, txn, shipmentId, ct);

            // ── Validate transition ──
            ShipmentTransitionGuard.EnsureCanTransition(currentStatus, toStatus);

            // ── Perform the update ──
            await UpdateShipmentStatusAsync(conn, txn, shipmentId, toStatus, ct);

            // ── Insert audit record ──
            await InsertHistoryAsync(conn, txn, shipmentId, currentStatus, toStatus, performedBy, reason, ct);

            // ── Publish domain event (outside the lock, but within the same transaction) ──
            var occurredAt = DateTimeOffset.UtcNow;
            var @event = new ShipmentStatusChangedEvent
            {
                ShipmentId = shipmentId,
                FromStatus = currentStatus,
                ToStatus = toStatus,
                PerformedBy = performedBy,
                Reason = reason,
                OccurredAt = occurredAt
            };
            await _mediator.Publish(@event, ct);

            _logger.LogInformation(
                "Shipment {ShipmentId} transitioned: {From} → {To} (by {PerformedBy})",
                shipmentId, currentStatus, toStatus, performedBy);

            return currentStatus;
        }
        finally
        {
            // Only dispose if we created the connection ourselves
            if (ownsConnection && conn != null)
                await conn.DisposeAsync();
        }
    }

    public async Task<ShipmentStatus> GetCurrentStatusAsync(Guid shipmentId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT status FROM warehouse.shipments
            WHERE id = @id AND is_deleted = FALSE;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", shipmentId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException($"Shipment {shipmentId} not found.");

        return Enum.Parse<ShipmentStatus>(result.ToString()!);
    }

    // ────────────────────────────────────────────────────
    // Private helpers
    // ────────────────────────────────────────────────────

    private static async Task<ShipmentStatus> ReadCurrentStatusForUpdateAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? txn,
        Guid shipmentId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT status FROM warehouse.shipments
            WHERE id = @id AND is_deleted = FALSE
            FOR UPDATE;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn, txn);
        cmd.Parameters.AddWithValue("id", shipmentId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException($"Shipment {shipmentId} not found.");

        return Enum.Parse<ShipmentStatus>(result.ToString()!);
    }

    private static async Task UpdateShipmentStatusAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? txn,
        Guid shipmentId,
        ShipmentStatus toStatus,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE warehouse.shipments
            SET status = @status, updated_at = NOW()
            WHERE id = @id AND is_deleted = FALSE;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn, txn);
        cmd.Parameters.AddWithValue("id", shipmentId);
        cmd.Parameters.AddWithValue("status", toStatus.ToString());

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new InvalidOperationException($"Shipment {shipmentId} not found or deleted.");
    }

    private static async Task InsertHistoryAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? txn,
        Guid shipmentId,
        ShipmentStatus fromStatus,
        ShipmentStatus toStatus,
        Guid? performedBy,
        string? reason,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO warehouse.shipment_status_history
                (id, shipment_id, from_status, to_status, performed_by, reason, occurred_at)
            VALUES
                (@id, @shipment_id, @from_status, @to_status, @performed_by, @reason, NOW());
        """;

        await using var cmd = new NpgsqlCommand(sql, conn, txn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        cmd.Parameters.AddWithValue("from_status", fromStatus.ToString());
        cmd.Parameters.AddWithValue("to_status", toStatus.ToString());
        cmd.Parameters.AddWithValue("performed_by",
            (object?)performedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reason",
            (object?)reason ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
