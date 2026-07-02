using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class TimeAdjustmentRequestService(
    AppDbContext db,
    UserManager<User> userManager,
    IEmailService emailService,
    INotificationService notificationService,
    ILogger<TimeAdjustmentRequestService> logger) : ITimeAdjustmentRequestService
{
    private const int TokenExpiryDays = 30;

    public async Task<AdjustmentRequestDto> CreateRequestAsync(
        string userId,
        CreateAdjustmentRequestDto dto,
        string approvalBaseUrl,
        CancellationToken ct = default)
    {
        ValidateSnapshot(dto.DesiredDaySnapshot);

        if (dto.Date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ValidationException("Cannot submit an adjustment request for a future date.");

        var existingPending = await db.TimeAdjustmentRequests
            .AnyAsync(r => r.UserId == userId && r.Date == dto.Date &&
                           r.Status == AdjustmentRequestStatus.Pending, ct);
        if (existingPending)
            throw new ValidationException(
                "A pending adjustment request already exists for this date. Please wait for it to be reviewed.");

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);
        var snapshotJson = JsonSerializer.Serialize(dto.DesiredDaySnapshot,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var request = new TimeAdjustmentRequest
        {
            UserId = userId,
            Date = dto.Date,
            DesiredDaySnapshot = snapshotJson,
            Reason = dto.Reason,
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(TokenExpiryDays),
            TokenUsed = false,
            RequestedAt = DateTimeOffset.UtcNow,
        };

        db.TimeAdjustmentRequests.Add(request);
        await db.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(userId);
        var approveLink = $"{approvalBaseUrl.TrimEnd('/')}/api/timeadjustmentrequests/approve/{Uri.EscapeDataString(rawToken)}";
        var summary = FormatSnapshotSummary(dto.DesiredDaySnapshot);

        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        var notificationEmail = config?.NotificationEmail;

        if (!string.IsNullOrEmpty(notificationEmail) && (config?.EnableAdjustmentRequestEmails ?? true))
        {
            try
            {
                await emailService.SendAdjustmentRequestEmailAsync(
                    notificationEmail, "Admin", user?.FullName ?? "Unknown",
                    dto.Date, summary, dto.Reason, approveLink);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send adjustment request email to {Email}", notificationEmail);
            }
        }
        else
        {
            logger.LogWarning(
                "Adjustment request {RequestId} created but no notification email is configured.",
                request.Id);
        }

        try
        {
            await notificationService.NotifyAdminsAsync(
                $"{user?.FullName ?? "An employee"} submitted a time adjustment request for {dto.Date:d MMM yyyy}.",
                NotificationType.AdjustmentRequest, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create in-app notification for adjustment request {RequestId}", request.Id);
        }

        return MapToDto(request, user?.FullName ?? "Unknown");
    }

    public async Task<IEnumerable<AdjustmentRequestDto>> GetAllRequestsAsync(CancellationToken ct = default)
    {
        return await db.TimeAdjustmentRequests
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new AdjustmentRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                EmployeeName = r.User.FullName,
                Date = r.Date,
                DesiredDaySnapshot = r.DesiredDaySnapshot,
                Reason = r.Reason,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ReviewedAt = r.ReviewedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AdjustmentRequestDto>> GetUserRequestsAsync(string userId, CancellationToken ct = default)
    {
        return await db.TimeAdjustmentRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new AdjustmentRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                EmployeeName = "",
                Date = r.Date,
                DesiredDaySnapshot = r.DesiredDaySnapshot,
                Reason = r.Reason,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ReviewedAt = r.ReviewedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<string> ApproveAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);

        var request = await db.TimeAdjustmentRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ApprovalTokenHash == tokenHash, ct);

        if (request == null) return "Invalid or unknown approval link.";
        if (request.TokenUsed) return "This approval link has already been used.";
        if (request.ExpiresAt < DateTimeOffset.UtcNow) return "This approval link has expired.";
        if (request.Status != AdjustmentRequestStatus.Pending)
            return $"This request has already been {request.Status.ToString().ToLower()}.";

        DesiredDaySnapshotDto snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<DesiredDaySnapshotDto>(request.DesiredDaySnapshot,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Null snapshot");
        }
        catch
        {
            return "This request has an invalid snapshot and cannot be approved.";
        }

        try { ValidateSnapshot(snapshot); }
        catch (ValidationException ex)
        {
            request.Status = AdjustmentRequestStatus.Rejected;
            request.TokenUsed = true;
            request.ReviewedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return $"Request was automatically rejected: {ex.Message}";
        }

        await ReconcileWorkSessionsAsync(request.UserId, request.Date, snapshot, ct);

        request.Status = AdjustmentRequestStatus.Approved;
        request.TokenUsed = true;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Adjustment request {RequestId} approved via token.", request.Id);

        var msg = $"Your time adjustment request for {request.Date:d MMM yyyy} has been approved.";
        try { await notificationService.NotifyUserAsync(request.UserId, msg, NotificationType.AdjustmentApproved, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send approval notification for {RequestId}", request.Id); }

        if (!string.IsNullOrEmpty(request.User.Email))
        {
            try { await emailService.SendAdjustmentOutcomeEmailAsync(request.User.Email, request.User.FullName, request.Date, approved: true); }
            catch (Exception ex) { logger.LogError(ex, "Failed to send approval email for {RequestId}", request.Id); }
        }

        return $"Approved. Time log for {request.User.FullName} on {request.Date:yyyy-MM-dd} has been updated.";
    }

    public async Task ApproveByIdAsync(int requestId, string adminUserId, CancellationToken ct = default)
    {
        var request = await db.TimeAdjustmentRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new ResourceNotFoundException("Adjustment request not found.");

        if (request.Status != AdjustmentRequestStatus.Pending)
            throw new ValidationException($"This request has already been {request.Status.ToString().ToLower()}.");

        var snapshot = JsonSerializer.Deserialize<DesiredDaySnapshotDto>(request.DesiredDaySnapshot,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ValidationException("Adjustment request has an invalid snapshot.");

        ValidateSnapshot(snapshot);
        await ReconcileWorkSessionsAsync(request.UserId, request.Date, snapshot, ct);

        request.Status = AdjustmentRequestStatus.Approved;
        request.TokenUsed = true;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = adminUserId;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Adjustment request {RequestId} approved by admin {AdminId}.", requestId, adminUserId);

        var msg = $"Your time adjustment request for {request.Date:d MMM yyyy} has been approved.";
        try { await notificationService.NotifyUserAsync(request.UserId, msg, NotificationType.AdjustmentApproved, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send approval notification for {RequestId}", requestId); }

        if (!string.IsNullOrEmpty(request.User.Email))
        {
            try { await emailService.SendAdjustmentOutcomeEmailAsync(request.User.Email, request.User.FullName, request.Date, approved: true); }
            catch (Exception ex) { logger.LogError(ex, "Failed to send approval email for {RequestId}", requestId); }
        }
    }

    public async Task RejectAsync(int requestId, string adminUserId, CancellationToken ct = default)
    {
        var request = await db.TimeAdjustmentRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new ResourceNotFoundException("Adjustment request not found.");

        if (request.Status != AdjustmentRequestStatus.Pending)
            throw new ValidationException($"This request has already been {request.Status.ToString().ToLower()}.");

        request.Status = AdjustmentRequestStatus.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = adminUserId;
        request.TokenUsed = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Adjustment request {RequestId} rejected by admin {AdminId}.", requestId, adminUserId);

        var msg = $"Your time adjustment request for {request.Date:d MMM yyyy} has been rejected.";
        try { await notificationService.NotifyUserAsync(request.UserId, msg, NotificationType.AdjustmentRejected, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send rejection notification for {RequestId}", requestId); }

        if (!string.IsNullOrEmpty(request.User.Email))
        {
            try { await emailService.SendAdjustmentOutcomeEmailAsync(request.User.Email, request.User.FullName, request.Date, approved: false); }
            catch (Exception ex) { logger.LogError(ex, "Failed to send rejection email for {RequestId}", requestId); }
        }
    }

    public async Task AdminDirectEditAsync(
        string userId, DateOnly date, DesiredDaySnapshotDto snapshot, string adminUserId, CancellationToken ct = default)
    {
        ValidateSnapshot(snapshot);
        await ReconcileWorkSessionsAsync(userId, date, snapshot, ct);
        await db.SaveChangesAsync(ct);

        var msg = $"An admin has adjusted your time log for {date:d MMM yyyy}.";
        try { await notificationService.NotifyUserAsync(userId, msg, NotificationType.AdminHoursAdjusted, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send AdminHoursAdjusted notification to {UserId}", userId); }
    }

    // ── Core reconcile (replaces UpsertClockEventAsync) ───────────────────────

    private async Task ReconcileWorkSessionsAsync(
        string userId, DateOnly date, DesiredDaySnapshotDto snapshot, CancellationToken ct)
    {
        var existing = await db.WorkSessions
            .Include(s => s.Breaks)
            .Where(s => s.UserId == userId && s.Date == date)
            .ToListAsync(ct);

        var snapshotSessionIds = snapshot.Sessions
            .Where(s => s.WorkSessionId.HasValue)
            .Select(s => s.WorkSessionId!.Value)
            .ToHashSet();

        // Hard-delete sessions not in the snapshot so they never block settlement.
        // BreakRecords cascade via the required FK (ReferentialAction.Cascade in migration).
        db.WorkSessions.RemoveRange(existing.Where(s => !snapshotSessionIds.Contains(s.Id)));

        var now = DateTimeOffset.UtcNow;

        foreach (var sessionDto in snapshot.Sessions)
        {
            var clockIn = sessionDto.ClockIn.ToUniversalTime();
            var clockOut = sessionDto.ClockOut.ToUniversalTime();

            WorkSession session;
            if (sessionDto.WorkSessionId.HasValue)
            {
                session = existing.First(s => s.Id == sessionDto.WorkSessionId.Value);
                // Update effective times only — ServerStamps are immutable once set.
                session.ClockIn = clockIn;
                session.ClockOut = clockOut;
                session.Status = WorkSessionStatus.Closed;
            }
            else
            {
                session = new WorkSession
                {
                    UserId = userId,
                    Date = date,
                    ClockIn = clockIn,
                    ClockInServerStamp = now,
                    ClockOut = clockOut,
                    ClockOutServerStamp = now,
                    Status = WorkSessionStatus.Closed,
                };
                db.WorkSessions.Add(session);
                // No mid-loop SaveChanges: add breaks via navigation property so EF
                // resolves the FK in a single SaveChanges call.
            }

            // Reconcile breaks for this session
            var snapshotBreakIds = sessionDto.Breaks
                .Where(b => b.BreakRecordId.HasValue)
                .Select(b => b.BreakRecordId!.Value)
                .ToHashSet();

            db.BreakRecords.RemoveRange(session.Breaks.Where(b => !snapshotBreakIds.Contains(b.Id)).ToList());

            foreach (var breakDto in sessionDto.Breaks)
            {
                var bStart = breakDto.BreakStart.ToUniversalTime();
                var bEnd = breakDto.BreakEnd.ToUniversalTime();

                if (breakDto.BreakRecordId.HasValue)
                {
                    var existingBreak = session.Breaks.First(b => b.Id == breakDto.BreakRecordId.Value);
                    // Update effective times only — ServerStamps are immutable once set.
                    existingBreak.BreakStart = bStart;
                    existingBreak.BreakEnd = bEnd;
                }
                else
                {
                    session.Breaks.Add(new BreakRecord
                    {
                        BreakStart = bStart,
                        BreakStartServerStamp = now,
                        BreakEnd = bEnd,
                        BreakEndServerStamp = now,
                    });
                }
            }
        }
    }

    // ── Snapshot validation ───────────────────────────────────────────────────

    private static void ValidateSnapshot(DesiredDaySnapshotDto snapshot)
    {
        if (snapshot.Sessions.Count == 0)
            throw new ValidationException("Snapshot must contain at least one session.");

        var now = DateTimeOffset.UtcNow;

        // Sort sessions by ClockIn for overlap checks
        var sessions = snapshot.Sessions
            .OrderBy(s => s.ClockIn.ToUniversalTime())
            .ToList();

        for (var i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var clockIn = s.ClockIn.ToUniversalTime();
            var clockOut = s.ClockOut.ToUniversalTime();

            if (clockIn >= clockOut)
                throw new ValidationException($"Session {i + 1}: ClockIn must be before ClockOut.");

            if (clockOut > now.AddMinutes(5))
                throw new ValidationException($"Session {i + 1}: times cannot be in the future.");

            // Adjacent session overlap check
            if (i > 0)
            {
                var prevOut = sessions[i - 1].ClockOut.ToUniversalTime();
                if (clockIn < prevOut)
                    throw new ValidationException($"Sessions {i} and {i + 1} overlap.");
            }

            // Validate breaks within this session
            var breaks = s.Breaks.OrderBy(b => b.BreakStart.ToUniversalTime()).ToList();
            for (var j = 0; j < breaks.Count; j++)
            {
                var b = breaks[j];
                var bStart = b.BreakStart.ToUniversalTime();
                var bEnd = b.BreakEnd.ToUniversalTime();

                if (bStart >= bEnd)
                    throw new ValidationException($"Session {i + 1}, break {j + 1}: BreakStart must be before BreakEnd.");

                if (bStart < clockIn)
                    throw new ValidationException($"Session {i + 1}, break {j + 1}: break starts before session ClockIn.");

                if (bEnd > clockOut)
                    throw new ValidationException($"Session {i + 1}, break {j + 1}: break ends after session ClockOut.");

                // Adjacent break overlap
                if (j > 0)
                {
                    var prevBreakEnd = breaks[j - 1].BreakEnd.ToUniversalTime();
                    if (bStart < prevBreakEnd)
                        throw new ValidationException($"Session {i + 1}: breaks {j} and {j + 1} overlap.");
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLower();
    }

    private static string FormatSnapshotSummary(DesiredDaySnapshotDto snapshot)
    {
        static string Fmt(DateTimeOffset t) => t.ToUniversalTime().ToString("HH:mm");

        var lines = new List<string>();
        for (var i = 0; i < snapshot.Sessions.Count; i++)
        {
            var s = snapshot.Sessions[i];
            lines.Add($"Session {i + 1}: {Fmt(s.ClockIn)} – {Fmt(s.ClockOut)} UTC");
            foreach (var b in s.Breaks)
                lines.Add($"  Break: {Fmt(b.BreakStart)} – {Fmt(b.BreakEnd)} UTC");
        }
        return string.Join("\n", lines);
    }

    private static AdjustmentRequestDto MapToDto(TimeAdjustmentRequest r, string employeeName) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        EmployeeName = employeeName,
        Date = r.Date,
        DesiredDaySnapshot = r.DesiredDaySnapshot,
        Reason = r.Reason,
        Status = r.Status,
        RequestedAt = r.RequestedAt,
        ReviewedAt = r.ReviewedAt,
    };
}
