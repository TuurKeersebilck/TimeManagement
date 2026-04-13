using System.Security.Cryptography;
using System.Text;
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
    ILogger<TimeAdjustmentRequestService> logger) : ITimeAdjustmentRequestService
{
    private const int TokenExpiryDays = 7;

    public async Task<AdjustmentRequestDto> CreateRequestAsync(
        string userId,
        CreateAdjustmentRequestDto dto,
        string approvalBaseUrl,
        CancellationToken ct = default)
    {
        // Must have at least one requested time
        if (dto.RequestedClockIn == null && dto.RequestedBreakStart == null &&
            dto.RequestedBreakEnd == null && dto.RequestedClockOut == null)
            throw new ValidationException("At least one time field must be specified in the adjustment request.");

        // Can't request future dates
        if (dto.Date > DateOnly.FromDateTime(DateTime.Now))
            throw new ValidationException("Cannot submit an adjustment request for a future date.");

        // Only one pending request per day per user
        var existingPending = await db.TimeAdjustmentRequests
            .AnyAsync(r => r.UserId == userId && r.Date == dto.Date &&
                           r.Status == AdjustmentRequestStatus.Pending, ct);
        if (existingPending)
            throw new ValidationException(
                "A pending adjustment request already exists for this date. Please wait for it to be reviewed.");

        // Generate approval token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        var request = new TimeAdjustmentRequest
        {
            UserId = userId,
            Date = dto.Date,
            RequestedClockIn = dto.RequestedClockIn,
            RequestedBreakStart = dto.RequestedBreakStart,
            RequestedBreakEnd = dto.RequestedBreakEnd,
            RequestedClockOut = dto.RequestedClockOut,
            Reason = dto.Reason,
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(TokenExpiryDays),
            TokenUsed = false,
            RequestedAt = DateTime.UtcNow,
        };

        db.TimeAdjustmentRequests.Add(request);
        await db.SaveChangesAsync(ct);

        // Load user for email
        var user = await userManager.FindByIdAsync(userId);
        var approveLink = $"{approvalBaseUrl.TrimEnd('/')}/api/timeadjustmentrequests/approve/{Uri.EscapeDataString(rawToken)}";

        // Send approval email to all admins
        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.Email != null)
            .ToListAsync(ct);

        foreach (var admin in admins)
        {
            try
            {
                await emailService.SendAdjustmentRequestEmailAsync(
                    admin.Email!,
                    admin.FullName,
                    user?.FullName ?? "Unknown",
                    dto.Date,
                    dto.RequestedClockIn,
                    dto.RequestedBreakStart,
                    dto.RequestedBreakEnd,
                    dto.RequestedClockOut,
                    dto.Reason,
                    approveLink);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send adjustment request email to admin {AdminId}", admin.Id);
            }
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
                RequestedClockIn = r.RequestedClockIn,
                RequestedBreakStart = r.RequestedBreakStart,
                RequestedBreakEnd = r.RequestedBreakEnd,
                RequestedClockOut = r.RequestedClockOut,
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

        if (request == null)
            return "Invalid or unknown approval link.";

        if (request.TokenUsed)
            return "This approval link has already been used.";

        if (request.ExpiresAt < DateTime.UtcNow)
            return "This approval link has expired.";

        if (request.Status != AdjustmentRequestStatus.Pending)
            return $"This request has already been {request.Status.ToString().ToLower()}.";

        // Upsert each clock event for the requested day
        await UpsertClockEventAsync(request, ClockEventType.ClockIn, request.RequestedClockIn, ct);
        await UpsertClockEventAsync(request, ClockEventType.BreakStart, request.RequestedBreakStart, ct);
        await UpsertClockEventAsync(request, ClockEventType.BreakEnd, request.RequestedBreakEnd, ct);
        await UpsertClockEventAsync(request, ClockEventType.ClockOut, request.RequestedClockOut, ct);

        request.Status = AdjustmentRequestStatus.Approved;
        request.TokenUsed = true;
        request.ReviewedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Adjustment request {RequestId} for user {UserId} on {Date} approved.",
            request.Id, request.UserId, request.Date);

        return $"Approved. Time log for {request.User.FullName} on {request.Date:yyyy-MM-dd} has been updated.";
    }

    private async Task UpsertClockEventAsync(
        TimeAdjustmentRequest request,
        ClockEventType type,
        TimeSpan? requestedTime,
        CancellationToken ct)
    {
        if (requestedTime == null) return;

        var existing = await db.ClockEvents
            .FirstOrDefaultAsync(e => e.UserId == request.UserId &&
                                      e.Date == request.Date &&
                                      e.Type == type, ct);

        if (existing != null)
        {
            existing.RecordedTime = requestedTime.Value;
            existing.ActualTime = requestedTime.Value; // Approved override — treat as authoritative
        }
        else
        {
            db.ClockEvents.Add(new ClockEvent
            {
                UserId = request.UserId,
                Date = request.Date,
                Type = type,
                ActualTime = requestedTime.Value,
                RecordedTime = requestedTime.Value,
                Description = type == ClockEventType.ClockOut
                    ? await GetExistingDescriptionAsync(request.UserId, request.Date, ct)
                    : null,
            });
        }
    }

    private async Task<string?> GetExistingDescriptionAsync(string userId, DateOnly date, CancellationToken ct)
    {
        return await db.ClockEvents
            .Where(e => e.UserId == userId && e.Date == date && e.Type == ClockEventType.ClockOut)
            .Select(e => e.Description)
            .FirstOrDefaultAsync(ct);
    }

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLower();
    }

    private static AdjustmentRequestDto MapToDto(TimeAdjustmentRequest r, string employeeName) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        EmployeeName = employeeName,
        Date = r.Date,
        RequestedClockIn = r.RequestedClockIn,
        RequestedBreakStart = r.RequestedBreakStart,
        RequestedBreakEnd = r.RequestedBreakEnd,
        RequestedClockOut = r.RequestedClockOut,
        Reason = r.Reason,
        Status = r.Status,
        RequestedAt = r.RequestedAt,
        ReviewedAt = r.ReviewedAt,
    };
}
