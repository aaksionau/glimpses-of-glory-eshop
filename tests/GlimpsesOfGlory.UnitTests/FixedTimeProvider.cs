namespace GlimpsesOfGlory.UnitTests;

// TimeProvider double frozen at one instant.
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
