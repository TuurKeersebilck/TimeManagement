namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>
/// A clock stopped at one instant, so behaviour that depends on "today" can be tested on a chosen
/// date instead of whatever day the suite happens to run.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
