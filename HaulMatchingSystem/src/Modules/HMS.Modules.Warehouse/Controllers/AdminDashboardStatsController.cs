using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardStatsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public AdminDashboardStatsController(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection") ?? "";

    // ─── GET /api/admin/dashboard/stats ───────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        // Normalize query dates to UTC (Npgsql requires UTC Kind for timestamptz).
        // Frontend sends date-only strings ("2026-09-02") => Kind=Unspecified => must fix.
        DateTime? Normalize(DateTime? d) => d.HasValue
            ? DateTime.SpecifyKind(d.Value, DateTimeKind.Utc)
            : d;

        // Default range: last 12 months
        // If "to" is date-only (midnight), extend to end of that day so the last day's payments are included.
        var toDate = Normalize(to) ?? DateTime.UtcNow;
        if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
            toDate = toDate.Date.AddDays(1);
        var fromDate = Normalize(from) ?? toDate.AddMonths(-11).AddDays(-toDate.Day + 1); // start of month, 12 months ago

        // Cache for 30s to avoid hammering the DB on frequent dashboard refreshes.
        var cacheKey = $"admin-dashboard-stats:{fromDate:O}:{toDate:O}";
        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Ok(cached);

        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // ── 1. Customer stats ──
        const string totalCustomersSql = """
            SELECT COUNT(*) FROM identity.users
            WHERE role = 'Customer' AND is_deleted = FALSE;
        """;
        var totalCustomers = await QueryScalarAsync(conn, totalCustomersSql, ct);

        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        const string newCustomersMonthSql = """
            SELECT COUNT(*) FROM identity.users
            WHERE role = 'Customer' AND is_deleted = FALSE AND created_at >= @firstOfMonth;
        """;
        var newCustomersThisMonth = await QueryScalarAsync(conn, newCustomersMonthSql, ct, ("@firstOfMonth", firstOfMonth));

        // ── 2. Revenue stats (payments with status = 'Paid') ──
        const string totalRevenueSql = """
            SELECT COALESCE(SUM(amount), 0) FROM warehouse.payments
            WHERE status = 'Paid' AND is_deleted = FALSE;
        """;
        var totalRevenue = await QueryDecimalAsync(conn, totalRevenueSql, ct);

        const string revenueMonthSql = """
            SELECT COALESCE(SUM(amount), 0) FROM warehouse.payments
            WHERE status = 'Paid' AND is_deleted = FALSE AND paid_at >= @firstOfMonth;
        """;
        var revenueThisMonth = await QueryDecimalAsync(conn, revenueMonthSql, ct, ("@firstOfMonth", firstOfMonth));

        // ── 3. Revenue by month (last 12 months for chart) ──
        // Bucket months in UTC explicitly (AT TIME ZONE 'UTC' converts timestamptz -> naive UTC timestamp)
        // so chart buckets align with the UTC firstOfMonth used elsewhere, regardless of session TZ.
        const string revenueByMonthSql = """
            SELECT
                TO_CHAR(d, 'YYYY-MM') AS month_key,
                TO_CHAR(d, 'Mon YYYY') AS label,
                COALESCE(SUM(p.amount), 0) AS revenue,
                COUNT(p.id) AS payment_count
            FROM generate_series(
                date_trunc('month', @fromDate::timestamptz AT TIME ZONE 'UTC'),
                date_trunc('month', @toDate::timestamptz AT TIME ZONE 'UTC'),
                '1 month'
            ) d
            LEFT JOIN warehouse.payments p
                ON date_trunc('month', p.paid_at AT TIME ZONE 'UTC') = d
                AND p.status = 'Paid' AND p.is_deleted = FALSE
            GROUP BY d
            ORDER BY d;
        """;
        var revenueByMonth = await QueryListAsync(conn, revenueByMonthSql, ct,
            ("@fromDate", fromDate),
            ("@toDate", toDate));

        // ── 4. Revenue by payment type ──
        // Paid_at in the future relative to "to" (which we extended to end-of-day) is excluded.
        const string revenueByTypeSql = """
            SELECT
                payment_type,
                COALESCE(SUM(amount), 0) AS total,
                COUNT(*) AS count
            FROM warehouse.payments
            WHERE status = 'Paid' AND is_deleted = FALSE
                AND paid_at >= @fromDate AND paid_at < @toDateExclusive
            GROUP BY payment_type
            ORDER BY total DESC;
        """;
        var revenueByType = await QueryListAsync(conn, revenueByTypeSql, ct,
            ("@fromDate", fromDate),
            ("@toDateExclusive", toDate));

        // ── 5. Operational stats ──
        const string activeTripsSql = """
            SELECT COUNT(*) FROM transport.trips WHERE status IN ('Active', 'Scheduled');
        """;
        var activeTrips = await QueryScalarAsync(conn, activeTripsSql, ct);

        const string inTransitSql = """
            SELECT COUNT(*) FROM warehouse.shipments
            WHERE (status = 'In_Transit' OR status = 'Matched') AND is_deleted = FALSE;
        """;
        var inTransitShipments = await QueryScalarAsync(conn, inTransitSql, ct);

        const string totalShipmentsSql = """
            SELECT COUNT(*) FROM warehouse.shipments WHERE is_deleted = FALSE;
        """;
        var totalShipments = await QueryScalarAsync(conn, totalShipmentsSql, ct);

        var shipmentsByStatusSql = """
            SELECT status, COUNT(*) AS count
            FROM warehouse.shipments WHERE is_deleted = FALSE
            GROUP BY status ORDER BY count DESC;
        """;
        var shipmentsByStatus = await QueryListAsync(conn, shipmentsByStatusSql, ct);

        // ── 6. Total drivers ──
        const string totalDriversSql = """
            SELECT COUNT(*) FROM identity.users
            WHERE role = 'Driver' AND is_deleted = FALSE;
        """;
        var totalDrivers = await QueryScalarAsync(conn, totalDriversSql, ct);

        var result = new
        {
            // Customers
            totalCustomers,
            newCustomersThisMonth,

            // Revenue
            totalRevenue,
            revenueThisMonth,
            revenueByMonth,
            revenueByType,

            // Operational
            activeTrips,
            inTransitShipments,
            totalShipments,
            totalDrivers,
            shipmentsByStatus,

            // Filter range
            fromDate,
            toDate,
            lastUpdated = DateTime.UtcNow
        };

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
        return Ok(result);
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
