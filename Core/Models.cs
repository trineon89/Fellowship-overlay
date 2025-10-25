using System;

namespace Fellowship_overlay.Core
{
    public enum AuraEventType { Applied, Removed }

    public sealed record AuraEvent(
        DateTimeOffset Timestamp,
        AuraEventType Type,
        string TargetGuid,
        string TargetName,
        int SpellId,
        string SpellName,
        double DurationSeconds,
        int Stacks
    );

    public sealed class Buff
    {
        public int SpellId { get; init; }
        public string Name { get; set; } = "";
        public int Stacks { get; set; } = 1;
        public DateTimeOffset AppliedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; } // null = indefinite (-1)
        public bool IsExpired(DateTimeOffset now) => ExpiresAt is DateTimeOffset t && t <= now;
    }
}
