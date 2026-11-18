using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class VacationRequestService(
    AppDbContext db,
    IVacationService vacationService,
    IEmailService emailService,
    INotificationService notificationService,
    IConfiguration configuration,
    ILogger<VacationRequestService> logger) : IVacationRequestService
{
    /// <summary>Longer ranges are almost certainly a typo in the year.</summary>
    private const int MaxRangeDays = 366;

    public async Task<VacationRequestDto> CreateAsync(string userId, CreateVacationRequestDto dto, CancellationToken ct = default)
    {
        VacationService.ValidateAmount(dto.Amount);

        if (dto.EndDate < dto.StartDate)
            throw new ValidationException("End date must be on or after start date.");
        if (dto.EndDate.DayNumber - dto.StartDate.DayNumber >= MaxRangeDays)
            throw new ValidationException("A vacation request can span at most one year.");

        // Throws for an admin-only type, or a limited type not assigned to the employee.
        var yearlyBalance = await vacationService.GetYearlyBalanceAsync(userId, dto.VacationTypeId, asAdmin: false, ct);

        var overlaps = await db.VacationRequests
            .Where(r => r.UserId == userId && r.VacationTypeId == dto.VacationTypeId &&
                        (r.Status == VacationRequestStatus.Pending || r.Status == VacationRequestStatus.Draft) &&
                        r.StartDate <= dto.EndDate && r.EndDate >= dto.StartDate)
            .Select(r => r.Status)
            .ToListAsync(ct);
        if (overlaps.Contains(VacationRequestStatus.Draft))
            throw new ValidationException(
                "You already planned some of these dates. Remove that draft first if you want to change it.");
        if (overlaps.Count > 0)
            throw new ValidationException(
                "You already have a pending request for some of these dates. Cancel it first if you want to change it.");

        var plan = await vacationService.PlanRangeAsync(userId, dto.VacationTypeId, dto.StartDate, dto.EndDate, ct);
        if (plan.NewDays.Count == 0)
            throw new ValidationException(
                "There are no working days to request in this period (weekends, public holidays and days you already have are left out).");

        if (yearlyBalance is decimal limit)
        {
            // Same convention as direct bookings: the start date's year carries the balance check.
            var year = dto.StartDate.Year;
            var used = await vacationService.GetUsedVacationDaysAsync(userId, dto.VacationTypeId, year, ct: ct);
            var held = await HeldDaysAsync(userId, dto.VacationTypeId, year, includeDrafts: true, excludeIds: null, ct);
            var needed = plan.NewDays.Count(d => d.Year == year) * dto.Amount;

            if (used + held + needed > limit)
            {
                var heldNote = held > 0 ? $" ({VacationService.FormatDays(held)} already planned or requested)" : "";
                throw new InsufficientVacationBalanceException(
                    $"Insufficient balance. Remaining: {VacationService.FormatDays(Math.Max(0, limit - used - held))} day(s){heldNote}, needed: {VacationService.FormatDays(needed)}.");
            }
        }

        var request = new VacationRequest
        {
            UserId = userId,
            VacationTypeId = dto.VacationTypeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Amount = dto.Amount,
            TotalDays = plan.NewDays.Count * dto.Amount,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            Status = VacationRequestStatus.Draft,
            RequestedAt = DateTimeOffset.UtcNow,
        };

        db.VacationRequests.Add(request);
        await db.SaveChangesAsync(ct);

        return (await ProjectAsync(db.VacationRequests.Where(r => r.Id == request.Id), ct)).Single();
    }

    public async Task<IEnumerable<VacationRequestDto>> GetMineAsync(string userId, CancellationToken ct = default)
        => await ProjectAsync(
            db.VacationRequests.Where(r => r.UserId == userId).OrderByDescending(r => r.RequestedAt), ct);

    public async Task DeleteDraftAsync(string userId, int requestId, CancellationToken ct = default)
    {
        var deleted = await db.VacationRequests
            .Where(r => r.Id == requestId && r.UserId == userId && r.Status == VacationRequestStatus.Draft)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0) return;

        var exists = await db.VacationRequests.AnyAsync(r => r.Id == requestId && r.UserId == userId, ct);
        throw exists
            ? new ValidationException("This request was already sent. Cancel it instead.")
            : new ResourceNotFoundException("Vacation request not found.");
    }

    public async Task<IEnumerable<VacationRequestDto>> SubmitAsync(string userId, CancellationToken ct = default)
    {
        var drafts = await db.VacationRequests
            .Where(r => r.UserId == userId && r.Status == VacationRequestStatus.Draft)
            .OrderBy(r => r.StartDate)
            .ToListAsync(ct);
        if (drafts.Count == 0)
            throw new ValidationException("There is nothing to submit: you have no planned days waiting to be sent.");

        // Things may have changed since the days were planned (an admin booked a day, a holiday was
        // added, a balance was lowered), so count each draft again and check the balance as a whole.
        foreach (var draft in drafts)
        {
            var plan = await vacationService.PlanRangeAsync(userId, draft.VacationTypeId, draft.StartDate, draft.EndDate, ct);
            if (plan.NewDays.Count == 0)
                throw new ValidationException(
                    $"{PeriodLabel(draft.StartDate, draft.EndDate)} no longer has any working days to request. Remove it and submit again.");
            draft.TotalDays = plan.NewDays.Count * draft.Amount;
        }

        var draftIds = drafts.Select(d => d.Id).ToList();
        foreach (var group in drafts.GroupBy(d => new { d.VacationTypeId, d.StartDate.Year }))
        {
            // Throws when the type became admin-only or was unassigned in the meantime.
            var limit = await vacationService.GetYearlyBalanceAsync(userId, group.Key.VacationTypeId, asAdmin: false, ct);
            if (limit is not decimal yearly) continue;

            var used = await vacationService.GetUsedVacationDaysAsync(userId, group.Key.VacationTypeId, group.Key.Year, ct: ct);
            var pending = await HeldDaysAsync(userId, group.Key.VacationTypeId, group.Key.Year, includeDrafts: false, draftIds, ct);
            var needed = group.Sum(d => d.TotalDays);
            if (used + pending + needed > yearly)
                throw new InsufficientVacationBalanceException(
                    $"Insufficient balance for {group.Key.Year}. Remaining: {VacationService.FormatDays(Math.Max(0, yearly - used - pending))} day(s), planned: {VacationService.FormatDays(needed)}. Remove a period and submit again.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var draft in drafts)
        {
            draft.Status = VacationRequestStatus.Pending;
            draft.RequestedAt = now;
        }
        await db.SaveChangesAsync(ct);

        var submitted = await ProjectAsync(
            db.VacationRequests.Where(r => draftIds.Contains(r.Id)).OrderBy(r => r.StartDate), ct);

        await NotifyAdminsOfSubmissionAsync(submitted, ct);
        return submitted;
    }

    public async Task CancelAsync(string userId, int requestId, CancellationToken ct = default)
    {
        var request = await db.VacationRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Vacation request not found.");

        var updated = await db.VacationRequests
            .Where(r => r.Id == requestId && r.Status == VacationRequestStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, VacationRequestStatus.Cancelled)
                .SetProperty(r => r.ReviewedAt, DateTimeOffset.UtcNow), ct);

        if (updated == 0)
            throw new ValidationException(AlreadyReviewedMessage(request.Status));

        await TryAsync(() => notificationService.NotifyAdminsAsync(
            $"{request.User.FullName} cancelled their vacation request for {PeriodLabel(request.StartDate, request.EndDate)}",
            NotificationType.VacationRequest, ct), "cancellation notification", requestId);
    }

    public async Task<IEnumerable<VacationRequestDto>> GetAllAsync(CancellationToken ct = default)
    {
        var requests = await ProjectAsync(
            db.VacationRequests
                .Where(r => r.Status != VacationRequestStatus.Draft)
                .OrderBy(r => r.Status == VacationRequestStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.RequestedAt),
            ct);

        var pending = requests.Where(r => r.Status == VacationRequestStatus.Pending).ToList();
        if (pending.Count > 0)
            await AddReviewContextAsync(pending, ct);

        return requests;
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => db.VacationRequests.CountAsync(r => r.Status == VacationRequestStatus.Pending, ct);

    public async Task<VacationRangeResultDto> ApproveAsync(int requestId, string adminUserId, CancellationToken ct = default)
    {
        var request = await LoadForReviewAsync(requestId, ct);
        var now = DateTimeOffset.UtcNow;

        // Claim the request first, so two admins approving at once can't both book the days.
        var claimed = await db.VacationRequests
            .Where(r => r.Id == requestId && r.Status == VacationRequestStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, VacationRequestStatus.Approved)
                .SetProperty(r => r.ReviewedAt, now)
                .SetProperty(r => r.ReviewedByUserId, adminUserId), ct);
        if (claimed == 0)
            throw new ValidationException(AlreadyReviewedMessage(request.Status));

        VacationRangeResultDto result;
        try
        {
            // Booked as the admin would book it directly. Weekends, holidays and days added in the
            // meantime are skipped again, so the request books exactly what is still free.
            result = await vacationService.CreateVacationRangeAsync(request.UserId, new CreateVacationRangeDto
            {
                VacationTypeId = request.VacationTypeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Amount = request.Amount,
                Note = request.Note,
            }, asAdmin: true, ct);
        }
        catch
        {
            await db.VacationRequests
                .Where(r => r.Id == requestId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.Status, VacationRequestStatus.Pending)
                    .SetProperty(r => r.ReviewedAt, (DateTimeOffset?)null)
                    .SetProperty(r => r.ReviewedByUserId, (string?)null), CancellationToken.None);
            throw;
        }

        logger.LogInformation("Vacation request {RequestId} approved by admin {AdminId}: {Count} day(s) booked.",
            requestId, adminUserId, result.Created.Count());

        await NotifyEmployeeOfOutcomeAsync(request, approved: true, reviewNote: null, ct);
        return result;
    }

    public async Task RejectAsync(int requestId, string adminUserId, string? reason, CancellationToken ct = default)
    {
        var request = await LoadForReviewAsync(requestId, ct);
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        var updated = await db.VacationRequests
            .Where(r => r.Id == requestId && r.Status == VacationRequestStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, VacationRequestStatus.Rejected)
                .SetProperty(r => r.ReviewedAt, DateTimeOffset.UtcNow)
                .SetProperty(r => r.ReviewedByUserId, adminUserId)
                .SetProperty(r => r.ReviewNote, note), ct);
        if (updated == 0)
            throw new ValidationException(AlreadyReviewedMessage(request.Status));

        logger.LogInformation("Vacation request {RequestId} denied by admin {AdminId}.", requestId, adminUserId);

        await NotifyEmployeeOfOutcomeAsync(request, approved: false, note, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<VacationRequest> LoadForReviewAsync(int requestId, CancellationToken ct)
    {
        var request = await db.VacationRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .Include(r => r.VacationType)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new ResourceNotFoundException("Vacation request not found.");

        // Drafts are the employee's own until submitted.
        if (request.Status == VacationRequestStatus.Draft)
            throw new ResourceNotFoundException("Vacation request not found.");
        if (request.Status != VacationRequestStatus.Pending)
            throw new ValidationException(AlreadyReviewedMessage(request.Status));

        return request;
    }

    private static string AlreadyReviewedMessage(VacationRequestStatus status) => status switch
    {
        VacationRequestStatus.Approved => "This request has already been approved.",
        VacationRequestStatus.Rejected => "This request has already been denied.",
        VacationRequestStatus.Cancelled => "This request was cancelled by the employee.",
        VacationRequestStatus.Draft => "This request hasn't been sent yet. Remove the draft instead.",
        _ => "This request was just reviewed by someone else. Refresh to see its status.",
    };

    /// <summary>Days held by the employee's open requests: pending ones, plus drafts if asked.</summary>
    private Task<decimal> HeldDaysAsync(
        string userId, int vacationTypeId, int year, bool includeDrafts, IReadOnlyCollection<int>? excludeIds, CancellationToken ct)
        => db.VacationRequests
            .Where(r => r.UserId == userId && r.VacationTypeId == vacationTypeId && r.StartDate.Year == year &&
                        (r.Status == VacationRequestStatus.Pending ||
                         (includeDrafts && r.Status == VacationRequestStatus.Draft)) &&
                        (excludeIds == null || !excludeIds.Contains(r.Id)))
            .SumAsync(r => r.TotalDays, ct);

    /// <summary>
    /// Projects requests to DTOs. Query filters are ignored so a request keeps showing its type
    /// name in the history after that type was deleted.
    /// </summary>
    private static Task<List<VacationRequestDto>> ProjectAsync(IQueryable<VacationRequest> query, CancellationToken ct)
        => query
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(r => new VacationRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                EmployeeName = r.User.FullName,
                VacationTypeId = r.VacationTypeId,
                VacationTypeName = r.VacationType.Name,
                VacationTypeColor = r.VacationType.Color,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Amount = r.Amount,
                TotalDays = r.TotalDays,
                Note = r.Note,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ReviewedAt = r.ReviewedAt,
                ReviewedByName = r.ReviewedByUser != null ? r.ReviewedByUser.FullName : null,
                ReviewNote = r.ReviewNote,
            })
            .ToListAsync(ct);

    /// <summary>
    /// What an admin wants to see before deciding: the balance after approval, and colleagues
    /// already off (or asking to be off) on the same days. Informational only; nothing is blocked.
    /// </summary>
    private async Task AddReviewContextAsync(List<VacationRequestDto> pending, CancellationToken ct)
    {
        var from = pending.Min(r => r.StartDate);
        var to = pending.Max(r => r.EndDate);

        var daysOff = await db.VacationDays
            .AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .Select(d => new { d.UserId, d.User.FullName, d.Date })
            .ToListAsync(ct);

        var otherPending = await db.VacationRequests
            .AsNoTracking()
            .Where(r => r.Status == VacationRequestStatus.Pending && r.StartDate <= to && r.EndDate >= from)
            .Select(r => new { r.Id, r.UserId, r.User.FullName, r.StartDate, r.EndDate })
            .ToListAsync(ct);

        foreach (var request in pending)
        {
            request.Balance = await BalanceContextAsync(request, ct);

            var approved = daysOff
                .Where(d => d.UserId != request.UserId && d.Date >= request.StartDate && d.Date <= request.EndDate)
                .GroupBy(d => d.FullName)
                .Select(g => new VacationRequestOverlapDto
                {
                    EmployeeName = g.Key,
                    Dates = g.Select(d => d.Date).Distinct().Order().ToList(),
                });

            var requested = otherPending
                .Where(o => o.UserId != request.UserId && o.StartDate <= request.EndDate && o.EndDate >= request.StartDate)
                .GroupBy(o => o.FullName)
                .Select(g => new VacationRequestOverlapDto
                {
                    EmployeeName = g.Key,
                    Dates = g.SelectMany(o => WeekdaysBetween(
                            Max(o.StartDate, request.StartDate), Min(o.EndDate, request.EndDate)))
                        .Distinct().Order().ToList(),
                    IsPending = true,
                })
                .Where(o => o.Dates.Count > 0);

            request.Overlaps = approved.Concat(requested).OrderBy(o => o.Dates[0]).ToList();
        }
    }

    private async Task<VacationRequestBalanceDto> BalanceContextAsync(VacationRequestDto request, CancellationToken ct)
    {
        var year = request.StartDate.Year;
        var used = await vacationService.GetUsedVacationDaysAsync(request.UserId, request.VacationTypeId, year, ct: ct);
        var otherPending = await HeldDaysAsync(
            request.UserId, request.VacationTypeId, year, includeDrafts: false, excludeIds: [request.Id], ct);

        var yearly = await db.EmployeeVacationBalances
            .Where(b => b.UserId == request.UserId && b.VacationTypeId == request.VacationTypeId && !b.VacationType.IsUnlimited)
            .Select(b => (decimal?)b.YearlyBalance)
            .FirstOrDefaultAsync(ct);

        return new VacationRequestBalanceDto
        {
            YearlyBalance = yearly,
            UsedDays = used,
            OtherPendingDays = otherPending,
            RemainingAfterApproval = yearly - used - otherPending - request.TotalDays,
        };
    }

    private async Task NotifyAdminsOfSubmissionAsync(IReadOnlyList<VacationRequestDto> requests, CancellationToken ct)
    {
        var first = requests[0];
        var message = requests.Count == 1
            ? $"{first.EmployeeName} requested {first.VacationTypeName} {(first.StartDate == first.EndDate ? "on" : "from")} {PeriodLabel(first.StartDate, first.EndDate)}"
            : $"{first.EmployeeName} requested {requests.Count} vacation periods ({VacationService.FormatDays(requests.Sum(r => r.TotalDays))} days)";

        await TryAsync(() => notificationService.NotifyAdminsAsync(message, NotificationType.VacationRequest, ct),
            "admin notification", first.Id);

        var config = await db.AppConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(config?.NotificationEmail) || !config.EnableVacationRequestEmails)
            return;

        var appUrl = (configuration["AppUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var items = requests
            .Select(r => new VacationRequestEmailItem(r.VacationTypeName, r.StartDate, r.EndDate, r.TotalDays, r.Note))
            .ToList();
        await TryAsync(() => emailService.SendVacationRequestEmailAsync(
            config.NotificationEmail, first.EmployeeName, items, $"{appUrl}/admin/requests"), "admin email", first.Id);
    }

    private async Task NotifyEmployeeOfOutcomeAsync(VacationRequest request, bool approved, string? reviewNote, CancellationToken ct)
    {
        var period = PeriodLabel(request.StartDate, request.EndDate);
        var message = approved
            ? $"Your {request.VacationType.Name} request for {period} has been approved"
            : $"Your {request.VacationType.Name} request for {period} has been denied"
              + (reviewNote is null ? "" : $": {reviewNote}");

        await TryAsync(() => notificationService.NotifyUserAsync(
            request.UserId, message,
            approved ? NotificationType.VacationApproved : NotificationType.VacationRejected, ct),
            "outcome notification", request.Id);

        if (string.IsNullOrEmpty(request.User.Email)) return;

        await TryAsync(() => emailService.SendVacationRequestOutcomeEmailAsync(
            request.User.Email, request.User.FullName, request.VacationType.Name,
            request.StartDate, request.EndDate, approved, reviewNote), "outcome email", request.Id);
    }

    /// <summary>Notifications and emails are best-effort: the request itself is already saved.</summary>
    private async Task TryAsync(Func<Task> action, string what, int requestId)
    {
        try { await action(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send {What} for vacation request {RequestId}", what, requestId); }
    }

    private static string PeriodLabel(DateOnly start, DateOnly end) => start == end
        ? start.ToString("d MMM yyyy")
        : $"{start:d MMM} until {end:d MMM yyyy}";

    private static IEnumerable<DateOnly> WeekdaysBetween(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            if (!TimeCalculationHelper.IsWeekend(d)) yield return d;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
