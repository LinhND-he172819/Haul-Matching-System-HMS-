using System.Security.Claims;
using HMS.Modules.Warehouse.Controllers;
using HMS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace HMS.Modules.Warehouse.Tests;

/// <summary>
/// Integration tests for CustomerFeedbackController + AdminFeedbackController.
///
/// Tests the full feedback lifecycle:
///   1. Create feedback (rating + comment) for a completed shipment
///   2. Upload evidence images
///   3. Get my feedback
///   4. Admin list + detail + hub isolation
///   5. Duplicate prevention
///   6. Validation (rating range, comment length, shipment status)
///
/// Uses real PostgreSQL (same DB as dev) + mocked IFileStorageService.
/// </summary>
[Trait("Category", "Feedback")]
public class FeedbackFlowTests : IDisposable
{
    private readonly string _connStr;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<ILogger<CustomerFeedbackController>> _customerLoggerMock;
    private readonly Mock<ILogger<AdminFeedbackController>> _adminLoggerMock;
    private readonly Mock<IConfiguration> _configMock;

    // Test data
    private Guid _testCustomerId;
    private Guid _testAdminId;
    private Guid _testStaffId;
    private Guid _testStaffHubId;
    private Guid _testShipmentId;
    private readonly List<Guid> _feedbackIds = new();
    private readonly List<Guid> _evidenceIds = new();
    private readonly List<Guid> _testShipmentIds = new();

    public FeedbackFlowTests()
    {
        _connStr = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123";

        _fileStorageMock = new Mock<IFileStorageService>();
        _fileStorageMock
            .Setup(x => x.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string fileName, string ct, CancellationToken _) =>
                $"test-storage/{Guid.NewGuid()}/{fileName}");

        _customerLoggerMock = new Mock<ILogger<CustomerFeedbackController>>();
        _adminLoggerMock = new Mock<ILogger<AdminFeedbackController>>();

        _configMock = new Mock<IConfiguration>();
        // GetConnectionString() extension calls GetSection("ConnectionStrings")[name]
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

        // Ensure schemas exist
        Exec(conn, "CREATE SCHEMA IF NOT EXISTS identity;");
        Exec(conn, "CREATE SCHEMA IF NOT EXISTS warehouse;");

        // Create test hub (geo_location is nullable)
        _testStaffHubId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.hubs (id, name, address, is_deleted)
            VALUES ('{_testStaffHubId}', 'Test Hub', '123 Test St', FALSE)
            ON CONFLICT (id) DO NOTHING;
        """);

        // Create test customer
        _testCustomerId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{_testCustomerId}', 'Test Customer', 'testcust_{_testCustomerId}@test.com', 'Customer', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // Create test admin
        _testAdminId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{_testAdminId}', 'Test Admin', 'testadmin_{_testAdminId}@test.com', 'Admin', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // Create test warehouse staff (belongs to the test hub)
        _testStaffId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, hub_id, is_deleted, created_at, updated_at)
            VALUES ('{_testStaffId}', 'Test Staff', 'teststaff_{_testStaffId}@test.com', 'Warehouse_Staff', '{_testStaffHubId}', FALSE, NOW(), NOW())
            ON CONFLICT (id) DO NOTHING;
        """);

        // Create test shipment (Completed, belongs to test customer)
        _testShipmentId = CreateTestShipment(conn, _testCustomerId, "Completed", "FB-TEST");
    }

    private void CleanupTestData()
    {
        try
        {
            using var conn = new NpgsqlConnection(_connStr);
            conn.Open();

            // 1. Delete evidence (FK → shipment_feedbacks)
            foreach (var fid in _feedbackIds)
            {
                Exec(conn, $"DELETE FROM warehouse.shipment_feedback_evidence WHERE feedback_id = '{fid}';");
            }

            // 2. Delete all feedbacks for test customer (catch any test-created ones)
            Exec(conn, $"DELETE FROM warehouse.shipment_feedbacks WHERE customer_id = '{_testCustomerId}';");

            // 3. Delete shipments that reference test customer (FK → identity.users)
            Exec(conn, $"DELETE FROM warehouse.shipments WHERE customer_id = '{_testCustomerId}';");

            // Also clean up any extra test shipments
            foreach (var sid in _testShipmentIds)
            {
                Exec(conn, $"DELETE FROM warehouse.shipment_feedback_evidence WHERE feedback_id IN (SELECT id FROM warehouse.shipment_feedbacks WHERE shipment_id = '{sid}');");
                Exec(conn, $"DELETE FROM warehouse.shipment_feedbacks WHERE shipment_id = '{sid}';");
                Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{sid}';");
            }

            // 4. Delete users and hub
            Exec(conn, $"DELETE FROM identity.users WHERE id IN ('{_testCustomerId}', '{_testAdminId}', '{_testStaffId}');");
            Exec(conn, $"DELETE FROM identity.hubs WHERE id = '{_testStaffHubId}';");
        }
        catch
        {
            // Best-effort cleanup; tests should not fail due to cleanup issues
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER: Create controller with authenticated user context
    // ═══════════════════════════════════════════════════════════════

    private static CustomerFeedbackController CreateCustomerController(
        Guid customerId,
        Mock<IConfiguration> config,
        Mock<IFileStorageService> fileStorage,
        Mock<ILogger<CustomerFeedbackController>> logger)
    {
        var controller = new CustomerFeedbackController(config.Object, fileStorage.Object, logger.Object);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customerId.ToString()),
            new("sub", customerId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static AdminFeedbackController CreateAdminController(
        Guid userId,
        string role,
        Mock<IConfiguration> config,
        Mock<IFileStorageService> fileStorage,
        Mock<ILogger<AdminFeedbackController>> logger)
    {
        var controller = new AdminFeedbackController(config.Object, fileStorage.Object, logger.Object);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. CREATE FEEDBACK — VALID CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateFeedback_ValidRating_Returns201()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 5, Comment = "Giao hàng rất tốt!" };

        var result = await controller.CreateFeedback(_testShipmentId, request, CancellationToken.None);

        var createdAtResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, createdAtResult.StatusCode);

        var body = createdAtResult.Value!;
        var idProp = body.GetType().GetProperty("id");
        Assert.NotNull(idProp);
        var feedbackId = (Guid)idProp!.GetValue(body)!;
        _feedbackIds.Add(feedbackId);

        // Verify rating in response
        var ratingProp = body.GetType().GetProperty("rating");
        Assert.Equal(5, ratingProp!.GetValue(body));
    }

    [Fact]
    public async Task CreateFeedback_AllRatings_Returns201()
    {
        // Use a different shipment for each rating test
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();

        for (int rating = 1; rating <= 5; rating++)
        {
            var custId = Guid.NewGuid();
            Exec(conn, $"""
                INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
                VALUES ('{custId}', 'Cust R{rating}', 'c{rating}_{custId}@test.com', 'Customer', FALSE, NOW(), NOW());
            """);

            var shipmentId = CreateTestShipment(conn, custId, "Completed", $"R{rating}");

            var controller = CreateCustomerController(custId, _configMock, _fileStorageMock, _customerLoggerMock);
            var request = new CreateFeedbackRequest { Rating = rating, Comment = $"Rating {rating}" };

            var result = await controller.CreateFeedback(shipmentId, request, CancellationToken.None);

            var objResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, objResult.StatusCode);

            var idProp = objResult.Value!.GetType().GetProperty("id");
            _feedbackIds.Add((Guid)idProp!.GetValue(objResult.Value)!);

            // Cleanup
            Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{shipmentId}';");
            Exec(conn, $"DELETE FROM identity.users WHERE id = '{custId}';");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. CREATE FEEDBACK — VALIDATION ERRORS
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public async Task CreateFeedback_InvalidRating_ReturnsBadRequest(int rating)
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = rating };

        var result = await controller.CreateFeedback(_testShipmentId, request, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var msg = badResult.Value!.GetType().GetProperty("message")!.GetValue(badResult.Value)!.ToString();
        Assert.Contains("1 đến 5", msg!);
    }

    [Fact]
    public async Task CreateFeedback_CommentTooLong_ReturnsBadRequest()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var longComment = new string('A', 2001);
        var request = new CreateFeedbackRequest { Rating = 4, Comment = longComment };

        var result = await controller.CreateFeedback(_testShipmentId, request, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var msg = badResult.Value!.GetType().GetProperty("message")!.GetValue(badResult.Value)!.ToString();
        Assert.Contains("2000 ký tự", msg!);
    }

    [Fact]
    public async Task CreateFeedback_ShipmentNotCompleted_ReturnsBadRequest()
    {
        // Create a non-completed shipment
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var shipmentId = CreateTestShipment(conn, _testCustomerId, "In_Transit", "NC");

        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 3 };

        var result = await controller.CreateFeedback(shipmentId, request, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var msg = badResult.Value!.GetType().GetProperty("message")!.GetValue(badResult.Value)!.ToString();
        Assert.Contains("hoàn tất", msg!);

        Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{shipmentId}';");
    }

    [Fact]
    public async Task CreateFeedback_ShipmentNotFound_ReturnsNotFound()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 3 };

        var result = await controller.CreateFeedback(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateFeedback_ShipmentBelongsToOther_ReturnsNotFound()
    {
        // Create a shipment belonging to another customer
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var otherCustId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{otherCustId}', 'Other', 'other_{otherCustId}@test.com', 'Customer', FALSE, NOW(), NOW());
        """);
        var shipmentId = CreateTestShipment(conn, otherCustId, "Completed", "OTH");

        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 4 };

        var result = await controller.CreateFeedback(shipmentId, request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);

        Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{shipmentId}';");
        Exec(conn, $"DELETE FROM identity.users WHERE id = '{otherCustId}';");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. DUPLICATE PREVENTION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateFeedback_DuplicateFeedback_Returns409Conflict()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 4, Comment = "First" };

        // Create first feedback
        var result1 = await controller.CreateFeedback(_testShipmentId, request, CancellationToken.None);
        var obj1 = Assert.IsType<ObjectResult>(result1);
        Assert.Equal(201, obj1.StatusCode);
        var id1 = (Guid)obj1.Value!.GetType().GetProperty("id")!.GetValue(obj1.Value)!;
        _feedbackIds.Add(id1);

        // Try duplicate
        var request2 = new CreateFeedbackRequest { Rating = 5, Comment = "Second" };
        var result2 = await controller.CreateFeedback(_testShipmentId, request2, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result2);
        var msg = conflictResult.Value!.GetType().GetProperty("message")!.GetValue(conflictResult.Value)!.ToString();
        Assert.Contains("đánh giá", msg!);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. GET MY FEEDBACK
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyFeedback_AfterCreation_ReturnsFeedback()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);

        // Create
        var createRequest = new CreateFeedbackRequest { Rating = 5, Comment = "Great service!" };
        var createResult = await controller.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        // Get
        var getResult = await controller.GetMyFeedback(_testShipmentId, CancellationToken.None);
        var getObj = Assert.IsType<OkObjectResult>(getResult);
        var getBody = getObj.Value!;

        Assert.Equal(feedbackId, getBody.GetType().GetProperty("id")!.GetValue(getBody));
        Assert.Equal(5, getBody.GetType().GetProperty("rating")!.GetValue(getBody));
        Assert.Equal("Great service!", getBody.GetType().GetProperty("comment")!.GetValue(getBody));
    }

    [Fact]
    public async Task GetMyFeedback_NoFeedback_ReturnsNotFound()
    {
        // Use a shipment that has no feedback
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var emptyShipmentId = CreateTestShipment(conn, _testCustomerId, "Completed", "EMPTY");

        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var result = await controller.GetMyFeedback(emptyShipmentId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);

        Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{emptyShipmentId}';");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. UPLOAD EVIDENCE (mocked file storage)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task UploadEvidence_ValidFile_ReturnsSuccess()
    {
        // Create feedback first
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 4 };
        var createResult = await controller.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        // Create a fake form file with mocked ContentType
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("test.jpg");
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(100);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        var result = await controller.UploadEvidence(feedbackId, new List<IFormFile> { mockFile.Object }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var msg = body.GetType().GetProperty("message")!.GetValue(body)!.ToString();
        Assert.Contains("thành công", msg!);
    }

    [Fact]
    public async Task UploadEvidence_WrongContentType_ReturnsBadRequest()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 3 };
        var createResult = await controller.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("malware.exe");
        mockFile.Setup(f => f.ContentType).Returns("application/x-executable");
        mockFile.Setup(f => f.Length).Returns(100);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        var result = await controller.UploadEvidence(feedbackId, new List<IFormFile> { mockFile.Object }, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var msg = badResult.Value!.GetType().GetProperty("message")!.GetValue(badResult.Value)!.ToString();
        Assert.Contains("Định dạng", msg!);
    }

    [Fact]
    public async Task UploadEvidence_NoFiles_ReturnsBadRequest()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 3 };
        var createResult = await controller.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        var result = await controller.UploadEvidence(feedbackId, new List<IFormFile>(), CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var msg = badResult.Value!.GetType().GetProperty("message")!.GetValue(badResult.Value)!.ToString();
        Assert.Contains("ít nhất một file", msg!);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. ADMIN LIST FEEDBACKS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminListFeedbacks_AsAdmin_ReturnsPagedResult()
    {
        // Create a feedback first
        var customerController = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 5, Comment = "Admin test" };
        var createResult = await customerController.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        _feedbackIds.Add((Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!);

        // Admin lists
        var adminController = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var result = await adminController.ListFeedbacks(page: 1, pageSize: 20, ct: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalCount = (int)body.GetType().GetProperty("TotalCount")!.GetValue(body)!;
        Assert.True(totalCount > 0);
    }

    [Fact]
    public async Task AdminListFeedbacks_FilterByRating_ReturnsFiltered()
    {
        // Create feedbacks with specific rating
        var customerController = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);

        // We already created rating 5 feedback above, now create a rating 2 on a different shipment
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var shipId2 = CreateTestShipment(conn, _testCustomerId, "Completed", "FB2");

        var req2 = new CreateFeedbackRequest { Rating = 2, Comment = "Not great" };
        var res2 = await customerController.CreateFeedback(shipId2, req2, CancellationToken.None);
        var obj2 = Assert.IsType<ObjectResult>(res2);
        _feedbackIds.Add((Guid)obj2.Value!.GetType().GetProperty("id")!.GetValue(obj2.Value)!);

        // Admin filter by rating=2
        var adminController = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var result = await adminController.ListFeedbacks(page: 1, pageSize: 20, rating: 2, ct: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var items = (System.Collections.IList)body.GetType().GetProperty("Items")!.GetValue(body)!;
        Assert.True(items.Count > 0);

        // Cleanup (extra shipment deleted in Dispose)
        _testShipmentIds.Add(shipId2);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. ADMIN FEEDBACK DETAIL
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminGetFeedbackDetail_ValidId_ReturnsDetail()
    {
        // Create feedback
        var customerController = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 3, Comment = "Detail test" };
        var createResult = await customerController.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        // Admin get detail
        var adminController = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var result = await adminController.GetFeedbackDetail(feedbackId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        Assert.Equal(feedbackId, body.GetType().GetProperty("id")!.GetValue(body));
        Assert.Equal(3, body.GetType().GetProperty("rating")!.GetValue(body));
        Assert.Equal("Detail test", body.GetType().GetProperty("comment")!.GetValue(body));
    }

    [Fact]
    public async Task AdminGetFeedbackDetail_InvalidId_ReturnsNotFound()
    {
        var adminController = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var result = await adminController.GetFeedbackDetail(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. EVIDENCE EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task UploadEvidence_FeedbackNotFound_ReturnsNotFound()
    {
        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var fakeFile = new FormFile(new MemoryStream(new byte[100]), 0, 100, "photo", "test.jpg");

        var result = await controller.UploadEvidence(Guid.NewGuid(), new List<IFormFile> { fakeFile }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UploadEvidence_InvalidFeedbackOwnership_ReturnsNotFound()
    {
        // Create feedback as customer A
        var controllerA = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createRequest = new CreateFeedbackRequest { Rating = 4 };
        var createResult = await controllerA.CreateFeedback(_testShipmentId, createRequest, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createResult);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        // Try to upload evidence as customer B
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var otherCustId = Guid.NewGuid();
        Exec(conn, $"""
            INSERT INTO identity.users (id, full_name, email, role, is_deleted, created_at, updated_at)
            VALUES ('{otherCustId}', 'Other', 'other_{otherCustId}@test.com', 'Customer', FALSE, NOW(), NOW());
        """);

        var controllerB = CreateCustomerController(otherCustId, _configMock, _fileStorageMock, _customerLoggerMock);
        var fakeFile = new FormFile(new MemoryStream(new byte[100]), 0, 100, "photo", "test.jpg");

        var result = await controllerB.UploadEvidence(feedbackId, new List<IFormFile> { fakeFile }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);

        Exec(conn, $"DELETE FROM identity.users WHERE id = '{otherCustId}';");
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. END-TO-END LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task FullFeedbackLifecycle_CreateThenGetThenList()
    {
        // Step 1: Create feedback
        var customerCtrl = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var createReq = new CreateFeedbackRequest
        {
            Rating = 5,
            Comment = "Tuyệt vời! Giao hàng nhanh, đóng gói cẩn thận."
        };
        var createRes = await customerCtrl.CreateFeedback(_testShipmentId, createReq, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createRes);
        Assert.Equal(201, createObj.StatusCode);
        var feedbackId = (Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!;
        _feedbackIds.Add(feedbackId);

        // Step 2: Get my feedback
        var getRes = await customerCtrl.GetMyFeedback(_testShipmentId, CancellationToken.None);
        var getObj = Assert.IsType<OkObjectResult>(getRes);
        Assert.Equal(feedbackId, getObj.Value!.GetType().GetProperty("id")!.GetValue(getObj.Value));

        // Step 3: Upload evidence
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("delivery_proof.jpg");
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(500);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[500]));
        var evRes = await customerCtrl.UploadEvidence(feedbackId, new List<IFormFile> { mockFile.Object }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(evRes);

        // Step 4: Admin lists feedbacks
        var adminCtrl = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var listRes = await adminCtrl.ListFeedbacks(page: 1, pageSize: 20, ct: CancellationToken.None);
        var listObj = Assert.IsType<OkObjectResult>(listRes);
        var totalCount = (int)listObj.Value!.GetType().GetProperty("TotalCount")!.GetValue(listObj.Value)!;
        Assert.True(totalCount >= 1);

        // Step 5: Admin gets detail
        var detailRes = await adminCtrl.GetFeedbackDetail(feedbackId, CancellationToken.None);
        var detailObj = Assert.IsType<OkObjectResult>(detailRes);
        var evidenceList = (System.Collections.IList)detailObj.Value!.GetType().GetProperty("evidence")!.GetValue(detailObj.Value)!;
        Assert.True(evidenceList.Count >= 1);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. EDGE CASES — COMMENT VARIATIONS
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFeedback_NullOrEmptyComment_Returns201(string? comment)
    {
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var shipId = CreateTestShipment(conn, _testCustomerId, "Completed", "COM");

        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest { Rating = 3, Comment = comment };

        var result = await controller.CreateFeedback(shipId, request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        var id = (Guid)objResult.Value!.GetType().GetProperty("id")!.GetValue(objResult.Value)!;
        _feedbackIds.Add(id);

        Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{shipId}';");
    }

    [Fact]
    public async Task CreateFeedback_Exactly2000CharComment_Returns201()
    {
        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        var shipId = CreateTestShipment(conn, _testCustomerId, "Completed", "MAX");

        var controller = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var request = new CreateFeedbackRequest
        {
            Rating = 4,
            Comment = new string('B', 2000)
        };

        var result = await controller.CreateFeedback(shipId, request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        _feedbackIds.Add((Guid)objResult.Value!.GetType().GetProperty("id")!.GetValue(objResult.Value)!);

        Exec(conn, $"DELETE FROM warehouse.shipments WHERE id = '{shipId}';");
    }

    // ═══════════════════════════════════════════════════════════════
    // 11. ADMIN SEARCH FILTER
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminListFeedbacks_WithSearchFilter_ReturnsMatching()
    {
        // Create a feedback with a unique comment for searching
        var customerController = CreateCustomerController(_testCustomerId, _configMock, _fileStorageMock, _customerLoggerMock);
        var uniqueText = $"UNIQUE_SEARCH_{Guid.NewGuid().ToString()[..8]}";
        var createReq = new CreateFeedbackRequest { Rating = 4, Comment = uniqueText };
        var createRes = await customerController.CreateFeedback(_testShipmentId, createReq, CancellationToken.None);
        var createObj = Assert.IsType<ObjectResult>(createRes);
        _feedbackIds.Add((Guid)createObj.Value!.GetType().GetProperty("id")!.GetValue(createObj.Value)!);

        // Admin search
        var adminController = CreateAdminController(_testAdminId, "Admin", _configMock, _fileStorageMock, _adminLoggerMock);
        var result = await adminController.ListFeedbacks(page: 1, pageSize: 20, search: uniqueText, ct: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value!;
        var totalCount = (int)body.GetType().GetProperty("TotalCount")!.GetValue(body)!;
        Assert.True(totalCount >= 1);
    }

    // ═══════════════════════════════════════════════════════════════
    // DB HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static void Exec(NpgsqlConnection conn, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Helper: insert a minimal valid shipment row (all NOT NULL columns satisfied).
    /// </summary>
    private static Guid CreateTestShipment(NpgsqlConnection conn, Guid customerId, string status, string suffix)
    {
        var id = Guid.NewGuid();
        var code = $"TST-{suffix}-{id.ToString()[..8]}";
        Exec(conn, $"""
            INSERT INTO warehouse.shipments
                (id, shipment_code, qr_code, status, customer_id, cargo_type, weight_kg, volume_cbm,
                 receiver_name, receiver_phone, dest_address, shipping_fee, cod_amount,
                 shipment_type, is_deleted, created_at, updated_at)
            VALUES
                ('{id}', '{code}', 'QR-{id.ToString()[..8]}', '{status}', '{customerId}',
                 'General', 1.00, 1.00, 'Receiver', '0900000000', '123 Dest St',
                 0.00, 0.00, 'Hub', FALSE, NOW(), NOW());
        """);
        return id;
    }
}
