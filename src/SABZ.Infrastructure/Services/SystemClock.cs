using SABZ.Application.Interfaces;

namespace SABZ.Infrastructure.Services;

/// <summary>Default UTC clock. Replace in tests to make time-dependent logic deterministic.</summary>
public class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
