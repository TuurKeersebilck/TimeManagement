using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface ITimeAdjustmentRequestService
{
    Task<AdjustmentRequestDto> CreateRequestAsync(string userId, CreateAdjustmentRequestDto dto, string approvalBaseUrl, CancellationToken ct = default);
    Task<IEnumerable<AdjustmentRequestDto>> GetAllRequestsAsync(CancellationToken ct = default);
    Task<string> ApproveAsync(string rawToken, CancellationToken ct = default);
    Task ApproveByIdAsync(int requestId, string adminUserId, CancellationToken ct = default);
    Task RejectAsync(int requestId, string adminUserId, CancellationToken ct = default);
}
