using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/customer/dashboard")]
[Authorize(Roles = "Customer")]
public class CustomerDashboardStatsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public CustomerDashboardStatsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        return userId;
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection") ?? "";

    // ─── GET /api/customer/dashboard/stats ────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // ── Total shipments ──
        const string totalSql = """
            SELECT COUNT(*) FROM warehouse.shipments
            WHERE customer_id = @cid AND is_deleted = FALSE;
        """;
        var totalShipments = await QueryScalarAsync(conn, totalSql, ct, ("@cid", customerId));

        // ── Shipments by status ──
        const string statusSql = """
            SELECT status, COUNT(*) AS count
            FROM warehouse.shipments
            WHERE customer_id = @cid AND is_deleted = FALSE
            GROUP BY status ORDER BY count DESC;
        """;
        var shipmentsByStatus = await QueryListAsync(conn, statusSql, ct, ("@cid", customerId));

        // ── Total cost (from quotation shipping_fee for this customer's shipments) ──
        const string totalCostSql = """
            SELECT COALESCE(SUM(q.shipping_fee), 0)
            FROM warehouse.quotations q
            JOIN warehouse.shipment_proposals sp ON q.proposal_id = sp.id
            JOIN warehouse.shipments s ON sp.shipment_id = s.id
            WHERE s.customer_id = @cid AND s.is_deleted = FALSE
              AND q.status != 'Cancelled';
        """;
        var totalEstimatedCost = await QueryDecimalAsync(conn, totalCostSql, ct, ("@cid", customerId));

        // ── Total deposit amount (from quotations) ──
        const string totalDepositSql = """
            SELECT COALESCE(SUM(q.deposit_amount), 0)
            FROM warehouse.quotations q
            JOIN warehouse.shipment_proposals sp ON q.proposal_id = sp.id
            JOIN warehouse.shipments s ON sp.shipment_id = s.id
            WHERE s.customer_id = @cid AND s.is_deleted = FALSE
              AND q.status != 'Cancelled';
        """;
        var totalDepositRequired = await QueryDecimalAsync(conn, totalDepositSql, ct, ("@cid", customerId));

        // ── Total actually paid ──
        const string totalPaidSql = """
            SELECT COALESCE(SUM(p.amount), 0)
            FROM warehouse.payments p
            WHERE p.customer_id = @cid AND p.status = 'Paid' AND p.is_deleted = FALSE;
        """;
        var totalPaid = await QueryDecimalAsync(conn, totalPaidSql, ct, ("@cid", customerId));

        // ── Deposit actually paid ──
        const string depositPaidSql = """
            SELECT COALESCE(SUM(p.amount), 0)
            FROM warehouse.payments p
            WHERE p.customer_id = @cid AND p.status = 'Paid' AND p.is_deleted = FALSE
              AND p.payment_type = 'Deposit';
        """;
        var depositPaid = await QueryDecimalAsync(conn, depositPaidSql, ct, ("@cid", customerId));

        // ── Final payment actually paid ──
        const string finalPaidSql = """
            SELECT COALESCE(SUM(p.amount), 0)
            FROM warehouse.payments p
            WHERE p.customer_id = @cid AND p.status = 'Paid' AND p.is_deleted = FALSE
              AND p.payment_type = 'FinalPayment';
        """;
        var finalPaid = await QueryDecimalAsync(conn, finalPaidSql, ct, ("@cid", customerId));

        return Ok(new
        {
            totalShipments,
            shipmentsByStatus,
            totalEstimatedCost,
            totalDepositRequired,
            totalPaid,
            depositPaid,
            finalPaid,
            outstandingAmount = totalEstimatedCost - totalPaid
        });
    }

    // ─── Helpers ──────────────────────────────────────────────────────
    // COUNT(*) in PostgreSQL returns bigint; use long to avoid overflow on large tables.
    private static async Task<long> QueryScalarAsync(
        NpgsqlConnection conn, string sql, CancellationToken ct,
        params (string name, object value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    private static async Task<decimal> QueryDecimalAsync(
        NpgsqlConnection conn, string sql, CancellationToken ct,
        params (string name, object value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToDecimal(result ?? 0m);
    }

    private static async Task<List<Dictionary<string, object?>>> QueryListAsync(
        NpgsqlConnection conn, string sql, CancellationToken ct,
        params (string name, object value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            list.Add(row);
        }
        return list;
    }
}
