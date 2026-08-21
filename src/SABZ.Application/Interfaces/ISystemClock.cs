namespace SABZ.Application.Interfaces;

/// <summary>
/// Centralised current-time access (UTC). Keeps date-dependent logic such as
/// monitoring due/upcoming evaluation deterministic and testable, and avoids
/// scattered DateTime.UtcNow calls.
/// </summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}
