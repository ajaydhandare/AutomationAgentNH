namespace NewHorizon.Automation.Application.Abstractions;

/// <summary>
/// Injected time. Working-hours gating and retention windows are date-sensitive, so tests must be
/// able to move the clock rather than sleep.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Server-local time, used only for the working-hours window: planners configure "08:00–18:00"
    /// in their own time, not UTC.
    /// </summary>
    TimeOnly LocalTimeOfDay { get; }
}
