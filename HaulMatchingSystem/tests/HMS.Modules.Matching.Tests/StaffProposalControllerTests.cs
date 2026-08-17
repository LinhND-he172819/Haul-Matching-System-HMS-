using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Controllers;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HMS.Modules.Matching.Tests
{
    /// <summary>
    /// Unit tests for StaffProposalController.
    /// Tests: GetProposals (with search, filter, pagination), GetProposalDetail, Approve, Reject.
    /// </summary>
    public class StaffProposalControllerTests
    {
        private readonly Mock<IStaffProposalService> _service;
        private readonly Mock<ILogger<StaffProposalController>> _logger;
        private readonly StaffProposalController _controller;

        private readonly Guid _staffId = Guid.NewGuid();
        private const string _adminRole = "Admin";
        private const string _staffRole = "Warehouse_Staff";

        public StaffProposalControllerTests()
        {
            _service = new Mock<IStaffProposalService>();
            _logger = new Mock<ILogger<StaffProposalController>>();
            _controller = new StaffProposalController(_service.Object, _logger.Object);
            SetupControllerUser(_staffId, _adminRole);
        }

        // ──────────────────────────────────────────────
        // HELPER: Setup ClaimsPrincipal on controller
        // ──────────────────────────────────────────────

        private void SetupControllerUser(Guid userId, string role, Guid? hubId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            if (hubId.HasValue)
                claims.Add(new Claim("HubId", hubId.Value.ToString()));

            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        private static PagedResult<StaffProposalSummaryDto> CreateSampleResult(int count = 3)
        {
            var items = new List<StaffProposalSummaryDto>();
            for (int i = 0; i < count; i++)
            {
                items.Add(new StaffProposalSummaryDto
                {
                    ProposalId = Guid.NewGuid(),
                    Status = "PendingReview",
                    CreatedAt = DateTime.UtcNow,
                    ProposalSource = "Customer",
                    SenderName = $"Sender {i}",
                    SenderPhone = "0901234567",
                    PickupAddress = $"Address {i}",
                    ReceiverName = $"Receiver {i}",
                    DeliveryAddress = $"Dest {i}",
                    WeightKg = 10 + i,
                    VolumeCbm = 1 + i,
                    ShipmentId = Guid.NewGuid(),
                    TripPostId = Guid.NewGuid()
                });
            }
            return new PagedResult<StaffProposalSummaryDto>
            {
                Items = items,
                Page = 1,
                PageSize = 20,
                TotalCount = count
            };
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — basic listing
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_ReturnsOkWithPagedResult()
        {
            var result = CreateSampleResult(5);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, null, 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var pagedResult = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(5, pagedResult.TotalCount);
            Assert.Equal(5, pagedResult.Items.Count);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — with status filter
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_WithStatusFilter_PassesToService()
        {
            var result = CreateSampleResult(2);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.Is<string?>(st => st == "PendingReview"),
                    It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                status: "PendingReview", null, null, null, 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(2, paged.TotalCount);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — with search parameter
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_WithSearch_PassesSearchToService()
        {
            var result = CreateSampleResult(1);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.Is<string?>(search => search == "linh"),
                    It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, search: "linh", 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(1, paged.TotalCount);
            _service.Verify(s => s.GetProposalsAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                "linh", It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProposals_WithEmptySearch_PassesNullToService()
        {
            var result = CreateSampleResult(8);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, search: "", 1, 20, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — with proposalSource filter
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_WithProposalSource_PassesToService()
        {
            var result = CreateSampleResult(3);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.Is<string?>(src => src == "Driver"),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, proposalSource: "Driver", null, null, 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(3, paged.TotalCount);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — with driverId filter
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_WithDriverId_PassesToService()
        {
            var driverId = Guid.NewGuid();
            var result = CreateSampleResult(1);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(),
                    It.Is<Guid?>(d => d == driverId),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, driverId, null, 1, 20, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — combined filters + search
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_CombinedFiltersAndSearch_AllParametersPassedCorrectly()
        {
            var result = CreateSampleResult(2);
            _service.Setup(s => s.GetProposalsAsync(
                    It.Is<Guid>(id => id == _staffId),
                    It.Is<string?>(r => r == _adminRole),
                    It.IsAny<Guid?>(),
                    It.Is<string?>(st => st == "Approved"),
                    It.Is<string?>(src => src == "Customer"),
                    It.IsAny<Guid?>(),
                    It.Is<string?>(search => search == "quyên"),
                    It.Is<int>(p => p == 2),
                    It.Is<int>(ps => ps == 10),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                status: "Approved", proposalSource: "Customer",
                driverId: null, search: "quyên",
                page: 2, pageSize: 10, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(2, paged.TotalCount);
            _service.Verify(s => s.GetProposalsAsync(
                _staffId, _adminRole, null,
                "Approved", "Customer", null,
                "quyên", 2, 10,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — pagination defaults
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_DefaultPagination_PassesPage1PageSize20()
        {
            var result = CreateSampleResult(0);
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.Is<int>(p => p == 1),
                    It.Is<int>(ps => ps == 20),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, null, 1, 20, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — empty results
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_NoResults_ReturnsOkWithEmptyList()
        {
            var result = new PagedResult<StaffProposalSummaryDto>
            {
                Items = new List<StaffProposalSummaryDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            };
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, search: "nonexistent", 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Empty(paged.Items);
            Assert.Equal(0, paged.TotalCount);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — exception returns 500
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_ServiceThrows_Returns500()
        {
            _service.Setup(s => s.GetProposalsAsync(
                    It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Unexpected database error"));

            var actionResult = await _controller.GetProposals(
                null, null, null, null, 1, 20, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(500, objectResult.StatusCode);
        }

        // ──────────────────────────────────────────────
        // GET /api/staff/proposals — Warehouse Staff role uses hubId
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposals_WarehouseStaff_PassesHubId()
        {
            var hubId = Guid.NewGuid();
            SetupControllerUser(_staffId, _staffRole, hubId);

            var result = CreateSampleResult(3);
            _service.Setup(s => s.GetProposalsAsync(
                    It.Is<Guid>(id => id == _staffId),
                    It.Is<string?>(r => r == _staffRole),
                    It.Is<Guid?>(h => h == hubId),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var actionResult = await _controller.GetProposals(
                null, null, null, null, 1, 20, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var paged = Assert.IsType<PagedResult<StaffProposalSummaryDto>>(okResult.Value);
            Assert.Equal(3, paged.TotalCount);
        }

        // ──────────────────────────────────────────────
        // POST /approve — success
        // ──────────────────────────────────────────────

        [Fact]
        public async Task ApproveProposal_Success_ReturnsOk()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.ApproveProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var actionResult = await _controller.ApproveProposal(proposalId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        [Fact]
        public async Task ApproveProposal_NotFound_ReturnsBadRequest()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.ApproveProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Proposal not found"));

            var actionResult = await _controller.ApproveProposal(proposalId, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Contains("not found", badRequest.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApproveProposal_Forbidden_ReturnsForbid()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.ApproveProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Không có quyền duyệt đề xuất này"));

            var actionResult = await _controller.ApproveProposal(proposalId, CancellationToken.None);

            Assert.IsType<ForbidResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // POST /reject — success
        // ──────────────────────────────────────────────

        [Fact]
        public async Task RejectProposal_Success_ReturnsOk()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.RejectProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var actionResult = await _controller.RejectProposal(proposalId,
                new RejectProposalRequestByStaff { Reason = "Not valid" }, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        [Fact]
        public async Task RejectProposal_EmptyReason_PassesEmptyToService()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.RejectProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var actionResult = await _controller.RejectProposal(proposalId,
                new RejectProposalRequestByStaff { Reason = "" }, CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult);
        }

        [Fact]
        public async Task RejectProposal_ServiceThrowsInvalidOperation_ReturnsBadRequest()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.RejectProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot reject proposal in current state"));

            var actionResult = await _controller.RejectProposal(proposalId,
                new RejectProposalRequestByStaff { Reason = "Bad state" }, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // GET /{proposalId} — detail
        // ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposalDetail_Found_ReturnsOk()
        {
            var proposalId = Guid.NewGuid();
            var detail = new StaffProposalDetailDto
            {
                ProposalId = proposalId,
                Status = "PendingReview",
                SenderName = "Test Sender"
            };
            _service.Setup(s => s.GetProposalDetailAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);

            var actionResult = await _controller.GetProposalDetail(proposalId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var dto = Assert.IsType<StaffProposalDetailDto>(okResult.Value);
            Assert.Equal(proposalId, dto.ProposalId);
            Assert.Equal("PendingReview", dto.Status);
        }

        [Fact]
        public async Task GetProposalDetail_NotFound_ReturnsNotFoundWithMessage()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.GetProposalDetailAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((StaffProposalDetailDto?)null);

            var actionResult = await _controller.GetProposalDetail(proposalId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        // ──────────────────────────────────────────────
        // POST /approve — unexpected state returns 400
        // ──────────────────────────────────────────────

        [Fact]
        public async Task ApproveProposal_InvalidOperationException_ReturnsBadRequest()
        {
            var proposalId = Guid.NewGuid();
            _service.Setup(s => s.ApproveProposalAsync(
                    proposalId, _staffId, It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected state"));

            var actionResult = await _controller.ApproveProposal(proposalId, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        }
    }
}
