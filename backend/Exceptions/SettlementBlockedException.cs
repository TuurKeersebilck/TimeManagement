using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Exceptions;

public class SettlementBlockedException(IEnumerable<BlockerDto> blockers)
    : Exception("Cannot confirm settlement — unresolved issues exist.")
{
    public IReadOnlyList<BlockerDto> Blockers { get; } = blockers.ToList();
}
