namespace TimeManagementBackend.Exceptions;

public class BreakTooShortException(int requiredMinutes, int elapsedMinutes)
    : Exception($"Minimum break duration is {requiredMinutes} min; only {elapsedMinutes} min elapsed.")
{
    public int RequiredMinutes { get; } = requiredMinutes;
    public int ElapsedMinutes { get; } = elapsedMinutes;
}
