using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Fellowship_overlay.Core
{
    public sealed class BuffStore
    {
        private readonly ConcurrentDictionary<int, Buff> _buffs = new();

        public IReadOnlyCollection<Buff> Snapshot()
            => _buffs.Values.ToArray();

        public void Apply(AuraEvent e)
        {
            if (e.Type == AuraEventType.Applied)
            {
                var expires = e.DurationSeconds > 0
                    ? e.Timestamp.AddSeconds(e.DurationSeconds)
                    : (DateTimeOffset?)null;

                _buffs.AddOrUpdate(
                    e.SpellId,
                    _ => new Buff
                    {
                        SpellId = e.SpellId,
                        Name = e.SpellName,
                        Stacks = Math.Max(1, e.Stacks),
                        AppliedAt = e.Timestamp,
                        ExpiresAt = expires
                    },
                    (_, b) =>
                    {
                        b.Name = e.SpellName;
                        b.Stacks = Math.Max(1, e.Stacks);
                        b.AppliedAt = e.Timestamp;
                        b.ExpiresAt = expires;
                        return b;
                    });
            }
            else // Removed
            {
                _buffs.TryRemove(e.SpellId, out _);
            }
        }

        public void Prune(DateTimeOffset now)
        {
            foreach (var kv in _buffs)
                if (kv.Value.IsExpired(now))
                    _buffs.TryRemove(kv.Key, out _);
        }
    }
}
