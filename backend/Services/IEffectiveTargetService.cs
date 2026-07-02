namespace TimeManagementBackend.Services;

public interface IEffectiveTargetService
{
    /// <summary>
    /// Returns the effective working-hours target for a user on a given date, after applying
    /// weekend, public holiday, and leave adjustments. Returns 0 when no target is configured
    /// (never a deficit).
    /// </summary>
    Task<decimal> GetEffectiveTargetAsync(string userId, DateOnly date, CancellationToken ct = default);
}
