using System;
using System.Globalization;

namespace Fellowship_overlay.Core
{
    public static class LineParser
    {
        // fast split for '|' (quoted names don't contain '|')
        private static string[] Split(string line) => line.Split('|');

        public static AuraEvent? TryParseAura(string line, string myName, string? myGuid)
        {
            var p = Split(line);
            if (p.Length < 11) return null;

            var ts = DateTimeOffset.Parse(p[0], null, DateTimeStyles.AssumeUniversal);
            var ev = p[1];
            if (ev != "EFFECT_APPLIED" && ev != "EFFECT_REMOVED") return null;

            string tgtGuid = p[4];
            string tgtName = Unquote(p[5]);
            if (!IsMe(tgtName, tgtGuid, myName, myGuid)) return null;

            int spellId = int.Parse(p[6]);
            string spellName = Unquote(p[7]);
            double duration = double.Parse(p[8], CultureInfo.InvariantCulture);
            int stacks = int.Parse(p[9]);
            var auraType = p[10]; // BUFF/DEBUFF
            if (auraType != "BUFF") return null;

            var type = ev == "EFFECT_APPLIED" ? AuraEventType.Applied : AuraEventType.Removed;
            return new AuraEvent(ts, type, tgtGuid, tgtName, spellId, spellName, duration, stacks);
        }

        private static string Unquote(string s) => s.Length>=2 && s[0]=='"' && s[^1]=='"' ? s[1..^1] : s;
        private static bool IsMe(string tgtName, string tgtGuid, string myName, string? myGuid)
            => string.Equals(tgtName, myName, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrEmpty(myGuid) && tgtGuid == myGuid);
    }
}
