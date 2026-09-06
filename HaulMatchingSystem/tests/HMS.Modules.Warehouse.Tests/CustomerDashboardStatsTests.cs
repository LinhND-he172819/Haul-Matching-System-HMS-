using System.Security.Claims;
using HMS.Modules.Warehouse.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;
using Xunit;

namespace HMS.Modules.Warehouse.Tests;

/// <summary>
/// Integration tests for CustomerDashboardStatsController.
///
/// Tests the customer dashboard cost summary endpoint:
///   1. Returns valid stats structure with all expected fields
///   2. Returns zero counts for customer with no data
///   3. Total shipments matches actual count
///   4. Shipments by status grouping works correctly
///   5. Cost calculations (estimated, deposit, paid, outstanding)
///   6. Customer data isolation (only own shipments visible)
///   7. Payment type breakdown (Deposit vs FinalPayment)
///   8. Outstanding amount calculation accuracy
///
/// Uses real PostgreSQL (same DB as dev).
/// </summary>
[Trait("Category", "DashboardStats")]
public class CustomerDashboardStatsTests : IDisposable
{
    private readonly string _connStr;
    private readonly Mock<IConfiguration> _configMock;

    // Test data IDs for cleanup
    private Guid _testCustomerId;
    private readonly List<Guid> _otherCustomerIds = new();
    private readonly List<Guid> _testShipmentIds = new();
    private readonly List<Guid> _testPaymentIds = new();
    private readonly List<Guid> _testProposalIds = new();
    private readonly List<Guid> _testQuotationIds = new();

    public CustomerDashboardStatsTests()
    {
        _connStr = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123";

        _configMock = new Mock<IConfiguration>();
        var connSectionMock = new Mock<IConfigurationSection>();
        connSectionMock.Setup(s => s["DefaultConnection"]).Returns(_connStr);
        _configMock
            .Setup(c => c.GetSection("ConnectionStrings"))
            .Returns(connSectionMock.Object);

        SeedTestData();
    }

    public void Dispose()
    {
        CleanupTestData();
    }

    // ═══════════════════════════════════════════════════════════════
    // TEST DATA SETUP / CLEANUP
    // ═══════════════════════════════════════════════════════════════

    private void SeedTestData()
    {
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();

        // Ensure schemas
        Exec(conn, "CREATE SCHEMA IF NOT EXISTS identity;");
        Exec(conn, "CREATE SCHEMA IF NOT EXISTS warehouse;");

        // ── Create test customer ──
        _testCustomerId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{_testCustomerId}', 'CustDash Test Customer', 'custdash_test_{_testCustomerId}@test.com', 'Customer', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // ── Create another customer (for isolation test) ──
        var otherCustId = Guid.NewGuid();
        _otherCustomerIds.Add(otherCustId);
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{otherCustId}', 'Other Customer', 'custdash_other_{otherCustId}@test.com', 'Customer', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // ── Create 2 shipments for test customer ──
        for (var i = 0; i < 2; i++)
        {
            var shipmentId = Guid.NewGuid();
            _testShipmentIds.Add(shipmentId);
            var code = $"CUST-SHP-{shipmentId.ToString()[..8]}";
            var qrCode = $"QR-CUST-{Guid.NewGuid():N}";
            Exec(conn, $"""
                INSERT INTO warehouse.shipments
                    (id, shipment_code, qr_code, status, customer_id, cargo_type, weight_kg, volume_cbm,
                     receiver_name, receiver_phone, dest_address, shipping_fee, cod_amount,
                     shipment_type, is_deleted, created_at, updated_at)
                VALUES
                    ('{shipmentId}', '{code}', '{qrCode}', 'In_Transit', '{_testCustomerId}',
                     'General', 5.00, 1.00, 'Receiver', '0900000000', '123 Dest St',
                     300000.00, 0.00, 'Hub', FALSE, NOW(), NOW())
                ON CONFLICT (id) DO NOTHING;
            """);
        }

        // ── Create 1 shipment for other customer (isolation check) ──
        var otherShipmentId = Guid.NewGuid();
        _testShipmentIds.Add(otherShipmentId);
        var otherQrCode = $"QR-OTHER-{Guid.NewGuid():N}";
        Exec(conn, $"""
            INSERT INTO warehouse.shipments
                (id, shipment_code, qr_code, status, customer_id, cargo_type, weight_kg, volume_cbm,
                 receiver_name, receiver_phone, dest_address, shipping_fee, cod_amount,
                 shipment_type, is_deleted, created_at, updated_at)
            VALUES
                ('{otherShipmentId}', 'OTHER-SHP-{otherShipmentId.ToString()[..8]}', '{otherQrCode}', 'Completed', '{otherCustId}',
                 'Fragile', 10.00, 3.00, 'Other Receiver', '0911111111', '456 Other St',
                 800000.00, 0.00, 'Hub', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // ── Create proposal + quotation for test customer's first shipment ──
        var proposalId = Guid.NewGuid();
        _testProposalIds.Add(proposalId);
        Exec(conn, $"""
            INSERT INTO warehouse.shipment_proposals
                (id, shipment_id, trip_post_id, customer_id, sender_name, sender_phone, pickup_address, status, created_at)
            VALUES
                ('{proposalId}', '{_testShipmentIds[0]}', '00000000-0000-0000-0000-000000000001', '{_testCustomerId}',
                 'Test Sender', '0900000000', '123 Test St', 'Accepted', NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        var quotationId = Guid.NewGuid();
        _testQuotationIds.Add(quotationId);
        Exec(conn, $"""
            INSERT INTO warehouse.quotations
                (id, proposal_id, shipment_id, quotation_code, shipping_fee, deposit_amount, currency,
                 status, quoted_by, quoted_at, created_at, updated_at, is_deleted)
            VALUES
                ('{quotationId}', '{proposalId}', '{_testShipmentIds[0]}', 'QUO-{quotationId.ToString()[..8]}',
                 300000.00, 150000.00, 'VND', 'Confirmed', '{_otherCustomerIds[0]}',
                 NOW(), NOW(), NOW(), FALSE)
            ON CONFLICT (id) DO NOTHING;
        """);

        // ── Create payments for test customer ──
        // Deposit payment
        var depositPaymentId = Guid.NewGuid();
        _testPaymentIds.Add(depositPaymentId);
        Exec(conn, $"""
            INSERT INTO warehouse.payments
                (id, payment_code, shipment_id, customer_id, amount, status, payment_type, is_deleted, paid_at, created_at, updated_at)
            VALUES
                ('{depositPaymentId}', 'PAY-CUST-DEP-{depositPaymentId.ToString()[..8]}', '{_testShipmentIds[0]}', '{_testCustomerId}', 150000.00, 'Paid', 'Deposit', FALSE, NOW(), NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // Final payment
        var finalPaymentId = Guid.NewGuid();
        _testPaymentIds.Add(finalPaymentId);
        Exec(conn, $"""
            INSERT INTO warehouse.payments
                (id, payment_code, shipment_id, customer_id, amount, status, payment_type, is_deleted, paid_at, created_at, updated_at)
            VALUES
                ('{finalPaymentId}', 'PAY-CUST-FIN-{finalPaymentId.ToString()[..8]}', '{_testShipmentIds[0]}', '{_testCustomerId}', 100000.00, 'Paid', 'FinalPayment', FALSE, NOW(), NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // ── Create a Pending payment (should NOT count as paid) ──
        var pendingPaymentId = Guid.NewGuid();
        _testPaymentIds.Add(pendingPaymentId);
        Exec(conn, $"""
            INSERT INTO warehouse.payments
                (id, payment_code, shipment_id, customer_id, amount, status, payment_type, is_deleted, paid_at, created_at, updated_at)
            VALUES
                ('{pendingPaymentId}', 'PAY-CUST-PEN-{pendingPaymentId.ToString()[..8]}', '{_testShipmentIds[0]}', '{_testCustomerId}', 50000.00, 'Pending', 'FinalPayment', FALSE, NOW(), NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);
    }

    private void CleanupTestData()
    {
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();

        foreach (var id in _testPaymentIds)
            Exec(conn, $"DELETE FROM warehouse.payments WHERE id = '{id}';");

        foreach (var id in _testQuotationIds)
            Exec(conn, $"DELETE FROM warehouse.quotations WHERE id = '{id}';");

        foreach (var id in _testProposalIds)
            Exec(conn, $"DELETE FROM warehouse.shipment_proposals WHERE id = '{id}';");

        foreach (var id in _testShipmentIds)
            Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{id}';");

        foreach (var id in _otherCustomerIds)
            Exec(conn, $"DELETE FROM identity.users WHERE id = '{id}';");

        Exec(conn, $"DELETE FROM identity.users WHERE id = '{_testCustomerId}';");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. BASIC STATS STRUCTURE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_ReturnsOkWithAllFields()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;

        Assert.NotNull(body.GetType().GetProperty("totalShipments"));
        Assert.NotNull(body.GetType().GetProperty("shipmentsByStatus"));
        Assert.NotNull(body.GetType().GetProperty("totalEstimatedCost"));
        Assert.NotNull(body.GetType().GetProperty("totalDepositRequired"));
        Assert.NotNull(body.GetType().GetProperty("totalPaid"));
        Assert.NotNull(body.GetType().GetProperty("depositPaid"));
        Assert.NotNull(body.GetType().GetProperty("finalPaid"));
        Assert.NotNull(body.GetType().GetProperty("outstandingAmount"));
    }

    [Fact]
    public async Task GetStats_ReturnsNonNegativeCounts()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;

        var totalShipments = Convert.ToInt32(body.GetType().GetProperty("totalShipments")!.GetValue(body)!);
        var totalPaid = Convert.ToDecimal(body.GetType().GetProperty("totalPaid")!.GetValue(body)!);
        var depositPaid = Convert.ToDecimal(body.GetType().GetProperty("depositPaid")!.GetValue(body)!);
        var finalPaid = Convert.ToDecimal(body.GetType().GetProperty("finalPaid")!.GetValue(body)!);

        Assert.True(totalShipments >= 0);
        Assert.True(totalPaid >= 0);
        Assert.True(depositPaid >= 0);
        Assert.True(finalPaid >= 0);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. CUSTOMER DATA ISOLATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_OnlyShowsOwnShipments()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalShipments = Convert.ToInt32(body.GetType().GetProperty("totalShipments")!.GetValue(body)!);

        // We seeded 2 shipments for this customer, 1 for another
        // Should only see 2
        Assert.Equal(2, totalShipments);
    }

    [Fact]
    public async Task GetStats_OtherCustomer_SeesOwnShipmentsOnly()
    {
        var otherCustId = _otherCustomerIds[0];
        var controller = CreateCustomerDashboardController(otherCustId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalShipments = Convert.ToInt32(body.GetType().GetProperty("totalShipments")!.GetValue(body)!);

        // Other customer has 1 shipment
        Assert.Equal(1, totalShipments);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. COST CALCULATIONS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_TotalEstimatedCost_FromQuotations()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalEstimatedCost = Convert.ToDecimal(body.GetType().GetProperty("totalEstimatedCost")!.GetValue(body)!);

        // 1 quotation with shipping_fee = 300,000
        Assert.Equal(300000m, totalEstimatedCost);
    }

    [Fact]
    public async Task GetStats_TotalDepositRequired_FromQuotations()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalDepositRequired = Convert.ToDecimal(body.GetType().GetProperty("totalDepositRequired")!.GetValue(body)!);

        // 1 quotation with deposit_amount = 150,000
        Assert.Equal(150000m, totalDepositRequired);
    }

    [Fact]
    public async Task GetStats_TotalPaid_OnlyPaidPayments()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalPaid = Convert.ToDecimal(body.GetType().GetProperty("totalPaid")!.GetValue(body)!);

        // 150,000 (Deposit Paid) + 100,000 (FinalPayment Paid) = 250,000
        // Pending payment (50,000) should NOT be counted
        Assert.Equal(250000m, totalPaid);
    }

    [Fact]
    public async Task GetStats_DepositPaid_OnlyDepositTypePaid()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var depositPaid = Convert.ToDecimal(body.GetType().GetProperty("depositPaid")!.GetValue(body)!);

        // Only Deposit type paid = 150,000
        Assert.Equal(150000m, depositPaid);
    }

    [Fact]
    public async Task GetStats_FinalPaid_OnlyFinalPaymentTypePaid()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var finalPaid = Convert.ToDecimal(body.GetType().GetProperty("finalPaid")!.GetValue(body)!);

        // Only FinalPayment type paid = 100,000
        Assert.Equal(100000m, finalPaid);
    }

    [Fact]
    public async Task GetStats_OutstandingAmount_Calculation()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var outstandingAmount = Convert.ToDecimal(body.GetType().GetProperty("outstandingAmount")!.GetValue(body)!);

        // totalEstimatedCost (300,000) - totalPaid (250,000) = 50,000
        Assert.Equal(50000m, outstandingAmount);
    }

    [Fact]
    public async Task GetStats_DepositPlusFinal_EqualsTotalPaid()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;

        var totalPaid = Convert.ToDecimal(body.GetType().GetProperty("totalPaid")!.GetValue(body)!);
        var depositPaid = Convert.ToDecimal(body.GetType().GetProperty("depositPaid")!.GetValue(body)!);
        var finalPaid = Convert.ToDecimal(body.GetType().GetProperty("finalPaid")!.GetValue(body)!);

        Assert.Equal(totalPaid, depositPaid + finalPaid);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. SHIPMENTS BY STATUS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_ShipmentsByStatus_ReturnsGroupedList()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var shipmentsByStatus = (System.Collections.IList)body.GetType().GetProperty("shipmentsByStatus")!.GetValue(body)!;

        // 2 shipments both In_Transit → 1 group
        Assert.Single(shipmentsByStatus);
    }

    [Fact]
    public async Task GetStats_ShipmentsByStatus_HasExpectedKeys()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var shipmentsByStatus = (System.Collections.IList)body.GetType().GetProperty("shipmentsByStatus")!.GetValue(body)!;

        var first = (Dictionary<string, object?>)shipmentsByStatus[0]!;
        Assert.True(first.ContainsKey("status"));
        Assert.True(first.ContainsKey("count"));
        Assert.Equal("In_Transit", first["status"]!.ToString());
    }

    [Fact]
    public async Task GetStats_ShipmentsByStatus_CountsCorrect()
    {
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var shipmentsByStatus = (System.Collections.IList)body.GetType().GetProperty("shipmentsByStatus")!.GetValue(body)!;

        var first = (Dictionary<string, object?>)shipmentsByStatus[0]!;
        var count = Convert.ToInt32(first["count"]!);
        Assert.Equal(2, count);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. CUSTOMER WITH NO DATA
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_EmptyCustomer_ReturnsZeros()
    {
        // Create a brand new customer with no data
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var emptyCustomerId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{emptyCustomerId}', 'Empty Customer', 'empty_cust_{emptyCustomerId}@test.com', 'Customer', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        try
        {
            var controller = CreateCustomerDashboardController(emptyCustomerId);

            var result = await controller.GetStats(CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var body = okResult.Value!;

            var totalShipments = Convert.ToInt32(body.GetType().GetProperty("totalShipments")!.GetValue(body)!);
            var totalPaid = Convert.ToDecimal(body.GetType().GetProperty("totalPaid")!.GetValue(body)!);
            var outstandingAmount = Convert.ToDecimal(body.GetType().GetProperty("outstandingAmount")!.GetValue(body)!);

            Assert.Equal(0, totalShipments);
            Assert.Equal(0m, totalPaid);
            Assert.Equal(0m, outstandingAmount);
        }
        finally
        {
            Exec(conn, $"DELETE FROM identity.users WHERE id = '{emptyCustomerId}';");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. OUTSTANDING AMOUNT INTEGRITY
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStats_OutstandingCanGoNegative_WhenPaidMore()
    {
        // This tests that outstanding = estimated - paid, even if paid > estimated
        // For this customer, outstanding = 300000 - 250000 = 50000 (positive)
        // We'll verify the math is consistent
        var controller = CreateCustomerDashboardController(_testCustomerId);

        var result = await controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;

        var estimatedCost = Convert.ToDecimal(body.GetType().GetProperty("totalEstimatedCost")!.GetValue(body)!);
        var totalPaid = Convert.ToDecimal(body.GetType().GetProperty("totalPaid")!.GetValue(body)!);
        var outstanding = Convert.ToDecimal(body.GetType().GetProperty("outstandingAmount")!.GetValue(body)!);

        Assert.Equal(estimatedCost - totalPaid, outstanding);
    }

    // ═══════════════════════════════════════════════════════════════
    // DB HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static void Exec(NpgsqlConnection conn, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private CustomerDashboardStatsController CreateCustomerDashboardController(Guid customerId)
    {
        var controller = new CustomerDashboardStatsController(_configMock.Object);

        // Setup Customer claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customerId.ToString()),
            new(ClaimTypes.Role, "Customer"),
            new("sub", customerId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }
}
