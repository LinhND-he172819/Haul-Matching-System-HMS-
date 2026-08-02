# 🛡️ HARDENING & BUG FIXING REPORT

**Session**: 15–16 — OLA Vulnerability Fixes, Exception Semantics, DB Schema Migration  
**Date**: 2025-07-18  
**Scope**: ForbiddenException hierarchy, Customer Ownership (OLA), Payment OLA, UnauthorizedAccessException → ForbiddenException migration, DB missing columns/tables fix

---

## 📊 Overall Results

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| **Build** | 0 errors | **0 errors** | ✅ Clean |
| **Shared.Core.Tests** | 120/123 (3 fail) | **128/128 (0 fail)** | ✅ +8 new, 3 fixed |
| **Matching.Tests** | 34/34 | **45/45 (0 fail)** | ✅ +11 new (Proposal OLA + Payment OLA) |
| **Transport.Tests** | 59/59 | **59/59 (0 fail)** | ✅ No regression |
| **Identity.Tests** | 1/1 | **1/1 (0 fail)** | ✅ No regression |
| **Realtime.Tests** | 2/2 | **2/2 (0 fail)** | ✅ No regression |
| **Total** | 216/219 (3 fail) | **235/235 (0 fail)** | ✅ +19 new, 3 fixed |
| **TypeScript** | 0 errors | **0 errors** | ✅ Clean |
| **Hub Isolation** | 3 list methods vulnerable | **All 6 query + 5 write methods secured** | ✅ Fail Closed |
| **OLA (Customer)** | 3 endpoints vulnerable | **All 3 fixed + tested** | ✅ Session 15 |
| **ForbiddenException** | N/A | **Created + all controllers migrated** | ✅ Session 15 |

---

## A. Fail Closed Hub Isolation — StaffProposalService

### Vulnerability Found
Three list methods (`GetProposalsAsync`, `GetQuotationsAsync`, `GetPaymentsAsync`) had the pattern:
```csharp
if (role == "Warehouse_Staff" && hubId.HasValue)
{
    whereClauses.Add("tp.created_by_staff_hub = @hubId");
}
```
**Problem**: If `role == "Warehouse_Staff"` AND `hubId` is null → **NO filter applied** → Staff sees ALL data across ALL hubs.

### Fix Applied
Changed to Fail Closed pattern in all 3 list methods:
```csharp
if (role == "Warehouse_Staff")
{
    if (!hubId.HasValue)
        throw new UnauthorizedAccessException("Warehouse_Staff missing HubId — access denied.");
    whereClauses.Add("tp.created_by_staff_hub = @hubId");
    parameters.Add(new NpgsqlParameter("hubId", hubId.Value));
}
```

### Hub Ownership on Read/Write Operations
Added Hub ownership verification to 3 additional methods:

| Method | Check Added | Implementation |
|--------|-------------|----------------|
| `GetProposalDetailAsync` | ✅ | Queries `tp.created_by_staff_hub` via trip_post join. If mismatch → `UnauthorizedAccessException` |
| `ApproveProposalAsync` | ✅ | Calls `VerifyProposalHubOwnershipAsync` before proceeding with approval |
| `RejectProposalAsync` | ✅ | Calls `VerifyProposalHubOwnershipAsync` before proceeding with rejection |

### Helper Methods Added
- `VerifyProposalHubOwnershipAsync(conn, tx, tripPostId, expectedHubId)` — Verifies trip_post Hub match
- `BuildProposalDetailFromReaderAsync(reader, proposalId, conn, ct)` — Extracted to avoid duplication

---

## B. Fail Closed Hub Isolation — QuotationService

### Vulnerability Found
All 4 write methods (`CreateQuotationAsync`, `UpdateQuotationAsync`, `SendQuotationAsync`, `CancelQuotationAsync`) and 1 read method (`GetQuotationAsync`) had **NO HubId parameter at all**.

### Fix Applied
1. **Interface updated** (`IQuotationService`): Added `string? role, Guid? hubId` parameters to all 5 methods
2. **Implementation updated** (`QuotationService`): Each method now:
   - Validates `hubId.HasValue` when role is `Warehouse_Staff`
   - Calls `VerifyProposalHubAsync` to check proposal → trip_post → Hub match
3. **Helper method added**: `VerifyProposalHubAsync(conn, tx, proposalId, expectedHubId)` — verifies via proposal → trip_post join

### Controller Updated (`StaffQuotationController`)
All 5 endpoints now extract and pass `role` + `hubId` from JWT claims:
- `POST proposals/{id}/quotations` (create)
- `PUT quotations/{id}` (update)
- `POST quotations/{id}/send` (send)
- `POST quotations/{id}/cancel` (cancel)
- `GET quotations/{id}` (detail)

---

## C. StaffQuotationController — Additional Security

### `GET /api/staff/quotations/{quotationId}` — Hub Isolation
**Found**: No Hub check on single quotation detail — any Staff could read any quotation by ID.  
**Fixed**: Added Hub ownership verification via SQL join on proposal → trip_post Hub.

### UnauthorizedAccessException Handling
Added `catch (UnauthorizedAccessException)` → `Forbid()` (HTTP 403) to all 5 endpoints in StaffQuotationController and 2 GET endpoints in StaffProposalController.

---

## D. Write API Audit — Complete

| Controller | Endpoints | Ownership Check | Status |
|-----------|-----------|-----------------|--------|
| **StaffProposalController** | approve/reject (POST) | Hub via `VerifyProposalHubOwnershipAsync` | ✅ Fixed |
| **StaffProposalController** | list/detail (GET) | Hub filter in SQL + Hub ownership | ✅ Fixed |
| **StaffQuotationController** | create/update/send/cancel (POST/PUT) | Hub via `VerifyProposalHubAsync` | ✅ Fixed |
| **StaffQuotationController** | list/detail (GET) | Hub filter in SQL + Hub ownership | ✅ Fixed |
| **StaffPaymentController** | list (GET) | Hub filter in SQL | ✅ OK (read-only) |
| **CustomerProposalController** | create/cancel (POST/DELETE) | customerId ownership | ✅ OK |
| **CustomerQuotationController** | get/deposit/final (GET/POST) | customerId ownership | ✅ OK |
| **DriverProposalController** | accept/reject (POST) | driverId → trip ownership | ✅ OK |
| **DriverMatchingController** | accept/reject (POST) | driverId → trip ownership | ✅ OK |
| **TripPostsController** | all endpoints | Hub via `OriginHubId` check | ✅ OK |

### Advisory Findings (All 3 Fixed in Session 15 ✅)
| # | Endpoint | Severity | Issue | Status |
|---|----------|----------|-------|--------|
| 1 | Customer `POST .../proposals` | MEDIUM | Missing `shipment.CustomerId == customerId` check | ✅ Fixed (Proposal ownership check) |
| 2 | Customer `GET .../shipments/{id}/payments` | LOW | No customerId filter — any customer can read payment totals | ✅ Fixed (Payment Summary ownership check) |
| 3 | Customer `GET .../quotations/{id}/payment-history` | LOW | No customerId filter — any customer can read payment history | ✅ Fixed (Payment History ownership check) |
| 4 | `POST /api/transport/sync-offline` | LOW | No `[Authorize]` — anonymous GPS data ingestion | ⏸ Deferred |

---

## I. ForbiddenException — New Exception Semantics (Session 15)

### Problem
The codebase used `UnauthorizedAccessException` (System namespace) for permission-denied scenarios. This is semantically HTTP 401 (Unauthorized), but the intent is HTTP 403 (Forbidden) — "you're authenticated but not authorized".

### Fix Applied
1. **Created** `ForbiddenException` in `HMS.Shared.Core/Exceptions/ForbiddenException.cs`
   - Inherits `Exception`, has `message` + `innerException` constructor overloads
2. **Updated** `ExceptionHandlingMiddleware.cs` — maps `ForbiddenException` → HTTP 403
3. **Migrated** all `UnauthorizedAccessException` → `ForbiddenException` across:
   - `StaffProposalService.cs` (Hub ownership checks)
   - `QuotationService.cs` (Hub verification checks)
   - `StaffProposalController.cs` (catch blocks)
   - `StaffQuotationController.cs` (catch blocks)
   - `StaffPaymentController.cs` (catch blocks)
   - `CustomerProposalController.cs` (catch blocks)
   - `CustomerQuotationController.cs` (catch blocks)
   - `DriverProposalController.cs` (catch blocks)

---

## J. OLA Vulnerability Fixes — Customer Ownership (Session 15)

### J1. Proposal Creation — Customer Ownership
**Vulnerability**: Any authenticated customer could create proposals for shipments belonging to other customers.
**Fix**: Added ownership check in `ProposalService.CreateProposalAsync`:
```csharp
if (shipment.CustomerId != customerId)
    throw new ForbiddenException("Shipment does not belong to this customer.");
```
**Prerequisite**: Added `[Column("customer_id")] public Guid? CustomerId` to `Shipment.cs` EF entity.

### J2. Payment Summary — Customer Ownership
**Vulnerability**: Any customer could query payment summary for any shipment (`GET /api/customer/shipments/{shipmentId}/payments`).
**Fix**: 
- Added `Guid? customerId` parameter to `IPaymentService.GetPaymentSummaryAsync` and implementation
- SQL ownership verification: `s.customer_id = @customer_id` on `warehouse.shipments`
- Controller extracts `customerId` from JWT via `GetCurrentUserId()` and passes to service

### J3. Payment History — Customer Ownership
**Vulnerability**: Any customer could query payment history for any quotation (`GET /api/customer/quotations/{quotationId}/payment-history`).
**Fix**:
- Added `Guid? customerId` parameter to `IPaymentService.GetPaymentHistoryAsync` and implementation  
- SQL ownership verification via join: `warehouse.quotations q JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id WHERE sp.customer_id = @customer_id`
- Controller extracts `customerId` from JWT via `GetCurrentUserId()` and passes to service

---

## K. New Tests — Session 15

### K1. Proposal OLA — Unit Tests (`ProposalServiceTests.cs`)
| Test | Description |
|------|-------------|
| `CreateProposal_WrongCustomer_ThrowsForbidden` | Customer A's shipment, Customer B tries to propose → `ForbiddenException` |
| `AcceptProposal_ProposalNotForThisDriver` | Updated to expect `ForbiddenException` instead of `UnauthorizedAccessException` |

### K2. Proposal OLA — Integration Tests (`ProposalServiceIntegrationTests.cs`)
| Flow | Description |
|------|-------------|
| Flow 9 | `CreateProposal_WrongCustomer_ThrowsForbidden` — full EF pipeline test |
| Flow 10 | `AcceptProposal_NotForThisDriver_ThrowsForbidden` — driver ownership via EF |
| Flow 6 | Updated to expect `ForbiddenException` instead of `UnauthorizedAccessException` |

### K3. Payment OLA — Controller Integration Tests (`PaymentOwnershipTests.cs`) — 8 Tests
| Test | Description |
|------|-------------|
| `GetPaymentSummary_CustomerA_OwnShipment_ReturnsData` | Correct customer gets data |
| `GetPaymentSummary_CustomerB_OtherShipment_ThrowsForbidden` | Wrong customer → ForbidResult |
| `GetPaymentHistory_CustomerA_OwnQuotation_ReturnsData` | Correct customer gets data |
| `GetPaymentHistory_CustomerB_OtherQuotation_ThrowsForbidden` | Wrong customer → ForbidResult |
| `GetPaymentSummary_DifferentCustomers_GetDifferentIds` | Cross-customer isolation verified |
| `GetPaymentHistory_DifferentCustomers_GetDifferentIds` | Cross-customer isolation verified |
| `GetPaymentSummary_NoUserIdClaim_Returns500` | Missing JWT → 500 ObjectResult |
| `GetPaymentHistory_NoUserIdClaim_Returns500` | Missing JWT → 500 ObjectResult |

---

## E. Shipment State Machine — Test Fixes

### Root Cause Analysis
The `ShipmentTransitionGuard` implementation had evolved (added `PendingReview`, `PendingDeposit`, `Completed`, `PendingReview → PendingDeposit → PendingReview` cycle) but 3 tests were not updated:

### Test 1: `TerminalState_CannotTransition_To_Any(Delivered)`
- **Expected**: `Delivered` is terminal (no outgoing transitions)
- **Actual**: `Delivered → Completed` is valid (final payment complete)
- **Fix**: Separated `Delivered` from the terminal test, added `Delivered_CanTransition_To_Completed` and `Delivered_CannotTransition_To_Unallowed` tests

### Test 2: `GetAllowedTransitions_Delivered_ReturnsEmpty`
- **Expected**: `Delivered` returns empty set
- **Actual**: Returns `{Completed}`
- **Fix**: Renamed to `GetAllowedTransitions_Delivered_ReturnsCompleted`, asserts `Single(Completed)`

### Test 3: `PendingDepositOnlyAllowsMatchedOrCancelled`
- **Expected**: PendingDeposit has 2 transitions: Matched, Cancelled
- **Actual**: Has 3 transitions: Matched, PendingReview, Cancelled (revert when quotation cancelled)
- **Fix**: Updated to `Assert.Equal(3, allowed.Count)` + added `Assert.Contains(ShipmentStatus.PendingReview, allowed)`

### Final Test Results
```
Shared.Core.Tests:   128/128 PASS ✅ (was 120/123)
Matching.Tests:       34/34 PASS ✅
Transport.Tests:      59/59 PASS ✅
TypeScript:            0 errors  ✅
```

---

## F. Deep Link Verification
- **Result**: NO react-router, NO URL-based routing
- **Implementation**: State-based via `Page` union type in `App.tsx`
- **Routing**: `page` state variable drives navigation, `getPortalPrefix()` returns 'admin' or 'staff'
- **Conclusion**: No deep link vulnerability exists

---

## G. Role Synchronization
- **Storage**: `localStorage` stores `accessToken`, `refreshToken`, `fullName`, `role`
- **Login**: `authApi.ts` stores JWT claims including role
- **Guard**: `App.tsx` checks `localStorage.getItem('role')` on every render
- **Logout**: Removes all 4 localStorage keys
- **Session fix**: Token refresh preserves existing role claim
- **Conclusion**: Role synchronization is consistent across frontend

---

## L. Files Modified (Session 14)

| File | Changes |
|------|---------|
| `StaffProposalService.cs` | Fail Closed in 3 list methods, Hub ownership in detail + approve/reject, 2 helper methods |
| `IQuotationService.cs` | Added `role`/`hubId` params to 5 methods |
| `QuotationService.cs` | Hub verification in all 5 methods + `VerifyProposalHubAsync` helper |
| `StaffQuotationController.cs` | Pass role/hubId in all 5 endpoints, `Forbid()` for ForbiddenException |
| `StaffProposalController.cs` | Added `Forbid()` for ForbiddenException in 2 GET endpoints |
| `ShipmentTransitionGuardTests.cs` | Fixed 2 tests for Delivered state |
| `ShipmentCustomerDriverLifecycleTests.cs` | Fixed 1 test for PendingDeposit transitions |

## M. Files Modified (Session 15)

| File | Changes |
|------|---------|
| `ForbiddenException.cs` | **NEW** — Custom exception for HTTP 403 semantics |
| `ExceptionHandlingMiddleware.cs` | Added ForbiddenException → 403 mapping |
| `Shipment.cs` | Added `[Column("customer_id")] public Guid? CustomerId` |
| `ProposalService.cs` | Customer ownership check in CreateProposalAsync, ForbiddenException in Cancel/Accept |
| `IPaymentService.cs` | Added `Guid? customerId` to 3 method signatures |
| `PaymentService.cs` | Ownership SQL in GetPaymentSummaryAsync + GetPaymentHistoryAsync, ForbiddenException |
| `StaffProposalService.cs` | UnauthorizedAccessException → ForbiddenException |
| `QuotationService.cs` | UnauthorizedAccessException → ForbiddenException |
| `StaffProposalController.cs` | catch UnauthorizedAccessException → catch ForbiddenException |
| `StaffQuotationController.cs` | catch UnauthorizedAccessException → catch ForbiddenException |
| `StaffPaymentController.cs` | catch UnauthorizedAccessException → catch ForbiddenException |
| `CustomerProposalController.cs` | catch UnauthorizedAccessException → catch ForbiddenException |
| `CustomerQuotationController.cs` | catch UnauthorizedAccessException → catch ForbiddenException, passes customerId to payment methods |
| `DriverProposalController.cs` | catch UnauthorizedAccessException → catch ForbiddenException |
| `ProposalServiceTests.cs` | Added CustomerId to shipments, new OLA test, ForbiddenException expectations |
| `ProposalServiceIntegrationTests.cs` | New Flow 9 (wrong customer), Flow 10 (wrong driver), ForbiddenException |
| `PaymentOwnershipTests.cs` | **NEW** — 8 controller-level integration tests for Payment OLA |

---

## N. Final Test Results (Session 15 — After All Changes)

```
HMS.Shared.Core.Tests:     128/128 PASS ✅
HMS.Modules.Matching.Tests:  45/45 PASS ✅ (was 34, +11 new)
HMS.Modules.Transport.Tests: 59/59 PASS ✅
HMS.Modules.Identity.Tests:   1/1  PASS ✅
HMS.Modules.Realtime.Tests:   2/2  PASS ✅
────────────────────────────────────────────
TOTAL:                       235/235 PASS ✅
```

### Remaining Advisory (Deferred)
| # | Endpoint | Severity | Issue |
|---|----------|----------|-------|
| 4 | `POST /api/transport/sync-offline` | LOW | No `[Authorize]` — anonymous GPS data ingestion (by design — Traccar integration) |

---

## O. Session 16 — DB Schema Migration (500 Error Fix)

### Problem Found
After Session 15 code changes, 3 staff features returned **HTTP 500** errors:
1. `GET /api/staff/proposals` — Staff proposal listing
2. `GET /api/staff/quotations` — Staff quotation listing
3. `GET /api/staff/payments` — Staff payment listing

### Root Cause
`StaffProposalService.cs` (Raw Npgsql, ~1003 lines) references DB columns/tables that **never existed** in the actual PostgreSQL schema. The code was written assuming a schema that was never fully applied via migration. Specifically:

| Table | Missing Columns |
|-------|----------------|
| `transport.trip_posts` | `created_by_staff_hub`, `origin`, `destination`, `departure_time`, `max_weight`, `max_volume` + FK |
| `shared.notifications` | Entire table (id, user_id, title, message, entity_type, entity_id, is_read, created_at, updated_at) |
| `warehouse.quotations` | `currency`, `accepted_at`, `quoted_by`, `quoted_at`, `cancelled_at`, `expired_at` |
| `warehouse.shipment_proposals` | `reviewer_id`, `reviewer_name`, `approved_by`, `rejected_by`, `reject_reason`, `is_deleted` |
| `warehouse.shipments` | `commodity`, `shipment_code` |

### Fix Applied
1. Created `docs/FIX_SESSION16_MISSING_COLUMNS.sql` — comprehensive migration script
2. Executed via `docker exec hms_postgres psql -U postgres -d hms_db -f /tmp/fix_session16.sql`
3. Added `warehouse.shipment_proposals.is_deleted` column separately (was missing from original script)
4. Backfilled existing rows: auto-generated `shipment_code` for shipments, `reviewer_name` from JOIN for proposals

### Post-Fix Verification
- ✅ `GET /api/staff/proposals` → **200 OK**
- ✅ `GET /api/staff/quotations` → **200 OK**  
- ✅ `GET /api/staff/payments` → **200 OK**
- ✅ Build: **0 errors**, 29 warnings (all pre-existing)
- ✅ Full test suite: **235/235 PASS** (no regressions)
- ✅ Debug error details removed from all 3 controllers (user-friendly messages restored)

### Files Modified (Session 16)

| File | Changes |
|------|---------|
| `docs/FIX_SESSION16_MISSING_COLUMNS.sql` | **NEW** — DB migration script for all missing columns/tables |
| `StaffProposalController.cs` | Removed temporary debug error details, restored user-friendly messages |
| `StaffQuotationController.cs` | Removed temporary debug error details, restored user-friendly messages |
| `StaffPaymentController.cs` | Removed temporary debug error details, restored user-friendly messages |

### Key Lesson Learned
> **Code-first ≠ DB-first**: When using Raw Npgsql (not EF Core), schema changes must be manually migrated. Always verify DB schema matches SQL queries in service layer.

---

## P. Final Test Results (Session 16 — After All Changes)

```
HMS.Shared.Core.Tests:     128/128 PASS ✅
HMS.Modules.Matching.Tests:  45/45 PASS ✅
HMS.Modules.Transport.Tests: 59/59 PASS ✅
HMS.Modules.Identity.Tests:   1/1  PASS ✅
HMS.Modules.Realtime.Tests:   2/2  PASS ✅
────────────────────────────────────────────
TOTAL:                       235/235 PASS ✅
```

### All Verified APIs (Session 16)

| # | Endpoint | Status | Description |
|---|----------|--------|-------------|
| 1 | `GET /api/staff/proposals` | ✅ 200 | Staff proposal listing (Hub Isolation enforced) |
| 2 | `GET /api/staff/quotations` | ✅ 200 | Staff quotation listing (Hub Isolation enforced) |
| 3 | `GET /api/staff/payments` | ✅ 200 | Staff payment listing (Hub Isolation enforced) |
