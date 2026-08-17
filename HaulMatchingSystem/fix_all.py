import sys

path = r"C:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\src\Modules\HMS.Modules.Matching\Application\Services\StaffProposalService.cs"

with open(path, "rb") as f:
    raw = f.read()

c = raw.decode("cp1252")
lines = c.split("\r\n")
print(f"Total lines: {len(lines)}")

# FIX 2a: Hub check in ApproveProposalAsync (line 313, 0-indexed 312)
# Line 313: if (role == "Warehouse_Staff")
# Line 317: var proposalHub = await VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId, hubId.Value, ct);
idx313 = 312
idx317 = 316
if 'if (role == "Warehouse_Staff")' in lines[idx313]:
    lines[idx313] = '                if (role == "Warehouse_Staff" && proposal.TripPostId.HasValue)'
    print("FIX 2a-1: Approve hub check updated")
if "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId," in lines[idx317]:
    lines[idx317] = lines[idx317].replace(
        "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId, hubId.Value, ct);",
        "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId.Value, hubId.Value, ct);"
    )
    print("FIX 2a-1: Approve hub VerifyProposalHubOwnershipAsync updated")

# FIX 2b: Trip validation in ApproveProposalAsync (lines 330-341, 0-indexed 329-340)
# Replace lines 329-340 (lines[329] = "// 3. Validate trip" .. lines[340] = last throw)
new_trip = [
    "                // 3. Validate capacity",
    "                if (shipment.WeightKg <= 0 || shipment.VolumeCbm <= 0)",
    '                    throw new InvalidOperationException("Weight v\xe0 Volume ph\x1ea3i l\x1edbn hon 0.");',
    "",
    "                // 3b. Validate trip \u2014 Customer proposals use trip_post_id; Driver proposals use requested_trip_id",
    "                if (proposal.TripPostId.HasValue)",
    "                {",
    "                    var tripPost = await ReadTripPostAsync(conn, tx, proposal.TripPostId.Value, ct)",
    '                        ?? throw new InvalidOperationException("Trip Post kh\xf4ng t\x1ed1n t\x1ea1i.");',
    "                    var trip = await ReadTripForUpdateAsync(conn, tx, tripPost.TripId, ct)",
    '                        ?? throw new InvalidOperationException("Trip kh\xf4ng t\x1ed1n t\x1ea1i.");',
    '                    if (trip.Status == "Completed" || trip.Status == "Cancelled")',
    '                        throw new InvalidOperationException("Trip \x0111\xe3 ho\xe0n th\xe0nh ho\x1ec7c b\x1ecb h\x1ee7y.");',
    "                }",
    "                else if (proposal.RequestedTripId.HasValue)",
    "                {",
    "                    var trip = await ReadTripForUpdateAsync(conn, tx, proposal.RequestedTripId.Value, ct)",
    '                        ?? throw new InvalidOperationException("Requested trip kh\xf4ng t\x1ed1n t\x1ea1i.");',
    '                    if (trip.Status == "Completed" || trip.Status == "Cancelled")',
    '                        throw new InvalidOperationException("Trip \x0111\xe3 ho\xe0n th\xe0nh ho\x1ec7c b\x1ecb h\x1ee7y.");',
    "                }",
]
lines[329:341] = new_trip
print("FIX 2b: Trip validation updated")

# After splice, line numbers shift. Let me recompute.
# Find the key markers
def find_line(lines, text, start=0):
    for i in range(start, len(lines)):
        if text in lines[i]:
            return i
    return -1

# FIX 2c: Notification in ApproveProposalAsync
notif_idx = find_line(lines, "// 6. Notification (outside transaction)", 300)
if notif_idx >= 0:
    print(f"FIX 2c: Found notification at line {notif_idx+1}")
    # Line notif_idx = comment, notif_idx+1 = await SaveNotificationAsync
    # notif_idx+2 = "title",
    # notif_idx+3 = "body",
    # notif_idx+4 = "Proposal", proposalId, ct);
    title_line = lines[notif_idx+2]
    body_line = lines[notif_idx+3]
    params_line = lines[notif_idx+4]
    
    new_notif = [
        "                // 6. Notification (outside transaction) \u2014 only if customer exists",
        "                if (proposal.CustomerId.HasValue)",
        "                {",
        "                    try",
        "                    {",
        "                        await SaveNotificationAsync(conn, proposal.CustomerId.Value,",
        title_line,
        body_line,
        params_line,
        "                    }",
        "                    catch (Exception ex)",
        "                    {",
        '                        _logger.LogWarning(ex, "Failed to save approval notification for proposal {ProposalId}", proposalId);',
        "                    }",
        "                }",
    ]
    lines[notif_idx:notif_idx+5] = new_notif
    print("FIX 2c: Approve notification updated")
else:
    print("FIX 2c: NOT FOUND")

# FIX 2d: SignalR in ApproveProposalAsync
sig_idx = find_line(lines, "// 7. SignalR", 300)
if sig_idx >= 0:
    print(f"FIX 2d: Found SignalR at line {sig_idx+1}")
    # Find "ProposalApproved" line
    approve_idx = find_line(lines, 'EventType = "ProposalApproved"', sig_idx)
    if approve_idx >= 0:
        # Find closing brace after "_logger.LogWarning(ex, "Failed to send SignalR for proposal approval");"
        logwarn_idx = find_line(lines, "Failed to send SignalR for proposal approval", sig_idx)
        if logwarn_idx >= 0:
            # logwarn_idx+1 should be "}"
            # Old block: lines[sig_idx .. logwarn_idx+1]
            old_count = logwarn_idx + 2 - sig_idx
            new_sig = [
                "                // 7. SignalR \u2014 notify customer if exists, or notify driver",
                "                var approveTargetUserId = proposal.CustomerId ?? proposal.DriverId;",
                "                if (approveTargetUserId.HasValue)",
                "                {",
                "                    try",
                "                    {",
                '                        await _dispatcher.SendProposalStatusToCustomerAsync(approveTargetUserId.Value, new ProposalEventPayload',
                "                        {",
                '                            EventType = "ProposalApproved",',
                '                            ProposalId = proposalId,',
                "                            Timestamp = DateTime.UtcNow",
                "                        });",
                "                    }",
                "                    catch (Exception ex)",
                "                    {",
                '                        _logger.LogWarning(ex, "Failed to send SignalR for proposal approval");',
                "                    }",
                "                }",
            ]
            lines[sig_idx:sig_idx+old_count] = new_sig
            print("FIX 2d: Approve SignalR updated")

# Now find and fix RejectProposalAsync
# FIX 3a: Hub check in RejectProposalAsync
reject_hub_idx = find_line(lines, '// 1b. Hub ownership verification for Warehouse_Staff', 400)
if reject_hub_idx < 0:
    reject_hub_idx = find_line(lines, '1b. Hub ownership', 400)
if reject_hub_idx >= 0:
    print(f"FIX 3a: Found reject hub at line {reject_hub_idx+1}")
    # Find the "if (role == "Warehouse_Staff")" line after it
    for i in range(reject_hub_idx, reject_hub_idx+5):
        if 'if (role == "Warehouse_Staff")' in lines[i]:
            lines[i] = '                if (role == "Warehouse_Staff" && proposal.TripPostId.HasValue)'
            print(f"FIX 3a: Reject hub check updated at line {i+1}")
            break
    # Find VerifyProposalHubOwnershipAsync line
    for i in range(reject_hub_idx, reject_hub_idx+10):
        if "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId," in lines[i]:
            lines[i] = lines[i].replace(
                "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId, hubId.Value, ct);",
                "VerifyProposalHubOwnershipAsync(conn, tx, proposal.TripPostId.Value, hubId.Value, ct);"
            )
            print(f"FIX 3a: Reject hub VerifyProposalHubOwnershipAsync updated at line {i+1}")
            break
else:
    print("FIX 3a: NOT FOUND")

# FIX 3b: Notification in RejectProposalAsync  
notif_reject_idx = find_line(lines, "// 5. Notification", 420)
if notif_reject_idx >= 0:
    print(f"FIX 3b: Found reject notification at line {notif_reject_idx+1}")
    # Check next line is SaveNotificationAsync
    if "SaveNotificationAsync(conn, proposal.CustomerId," in lines[notif_reject_idx+1]:
        title_line_r = lines[notif_reject_idx+2]
        body_line_r = lines[notif_reject_idx+3]
        params_line_r = lines[notif_reject_idx+4]
        
        new_notif_r = [
            "                // 5. Notification \u2014 only if customer exists",
            "                if (proposal.CustomerId.HasValue)",
            "                {",
            "                    try",
            "                    {",
            "                        await SaveNotificationAsync(conn, proposal.CustomerId.Value,",
            title_line_r,
            body_line_r,
            params_line_r,
            "                    }",
            "                    catch (Exception ex)",
            "                    {",
            '                        _logger.LogWarning(ex, "Failed to save rejection notification for proposal {ProposalId}", proposalId);',
            "                    }",
            "                }",
        ]
        lines[notif_reject_idx:notif_reject_idx+5] = new_notif_r
        print("FIX 3b: Reject notification updated")

# FIX 3c: SignalR in RejectProposalAsync
sig_reject_idx = find_line(lines, "// 6. SignalR", 440)
if sig_reject_idx < 0:
    sig_reject_idx = find_line(lines, "SignalR", 440)
if sig_reject_idx >= 0:
    print(f"FIX 3c: Found reject SignalR at line {sig_reject_idx+1}")
    reject_evt_idx = find_line(lines, 'EventType = "ProposalRejected"', sig_reject_idx)
    if reject_evt_idx >= 0:
        logwarn_r_idx = find_line(lines, "Failed to send SignalR for proposal rejection", sig_reject_idx)
        if logwarn_r_idx >= 0:
            old_count_r = logwarn_r_idx + 2 - sig_reject_idx
            new_sig_r = [
                "                // 6. SignalR \u2014 notify customer if exists, or notify driver",
                "                var rejectTargetUserId = proposal.CustomerId ?? proposal.DriverId;",
                "                if (rejectTargetUserId.HasValue)",
                "                {",
                "                    try",
                "                    {",
                '                        await _dispatcher.SendProposalStatusToCustomerAsync(rejectTargetUserId.Value, new ProposalEventPayload',
                "                        {",
                '                            EventType = "ProposalRejected",',
                '                            ProposalId = proposalId,',
                "                            Timestamp = DateTime.UtcNow",
                "                        });",
                "                    }",
                "                    catch (Exception ex)",
                "                    {",
                '                        _logger.LogWarning(ex, "Failed to send SignalR for proposal rejection");',
                "                    }",
                "                }",
            ]
            lines[sig_reject_idx:sig_reject_idx+old_count_r] = new_sig_r
            print("FIX 3c: Reject SignalR updated")

# Save
result = "\r\n".join(lines)
with open(path, "wb") as f:
    f.write(result.encode("cp1252"))

print(f"\nDone! File saved. New line count: {len(lines)}")

# Verify
verify_lines = result.split("\r\n")
print(f"Verify - Has IsDBNull(2): {any('IsDBNull(2)' in l for l in verify_lines)}")
print(f"Verify - Has RequestedTripId.HasValue: {any('RequestedTripId.HasValue' in l for l in verify_lines)}")
print(f"Verify - Has approveTargetUserId: {any('approveTargetUserId' in l for l in verify_lines)}")
print(f"Verify - Has rejectTargetUserId: {any('rejectTargetUserId' in l for l in verify_lines)}")
print(f"Verify - Has proposal.CustomerId.Value: {sum(1 for l in verify_lines if 'proposal.CustomerId.Value' in l)}")
print(f"Verify - Has proposal.TripPostId.HasValue: {sum(1 for l in verify_lines if 'TripPostId.HasValue' in l)}")
