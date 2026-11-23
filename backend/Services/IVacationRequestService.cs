using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

/// <summary>
/// Employees plan leave as drafts and submit them together; an admin then approves each request
/// (which books the days) or denies it.
/// Admins booking leave themselves, for anyone, skip this and use <see cref="IVacationService"/>.
/// </summary>
public interface IVacationRequestService
{
    /// <summary>Adds a draft. Nobody is told until the employee submits.</summary>
    Task<VacationRequestDto> CreateAsync(string userId, CreateVacationRequestDto dto, CancellationToken ct = default);
    Task<IEnumerable<VacationRequestDto>> GetMineAsync(string userId, CancellationToken ct = default);
    Task DeleteDraftAsync(string userId, int requestId, CancellationToken ct = default);

    /// <summary>Sends all of the employee's drafts for review, with one admin email and notification.</summary>
    Task<IEnumerable<VacationRequestDto>> SubmitAsync(string userId, CancellationToken ct = default);

    /// <summary>Withdraws a submitted request that hasn't been reviewed yet.</summary>
    Task CancelAsync(string userId, int requestId, CancellationToken ct = default);

    /// <summary>Every submitted request (no drafts), pending first. Pending ones carry the balance and who else is off.</summary>
    Task<IEnumerable<VacationRequestDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<VacationRangeResultDto> ApproveAsync(int requestId, string adminUserId, CancellationToken ct = default);
    Task RejectAsync(int requestId, string adminUserId, string? reason, CancellationToken ct = default);
}
