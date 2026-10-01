// Code written by Gabriel Mailhot, 01/10/2026.
// Five court verbs (give_influence, lend_troops, give_troops, spend_influence, sway_opinion) used to write their
// cooldown marker into the memory's own text ("At the player's word, turned against Lady Decantia. [cr_sway:lord_1_1_2]"),
// the only cooldown store there was before audit M-B6 gave them NpcProfile.CourtActionCooldowns. The text copy
// was never removed, so the marker showed in the Memories panel and reached the character's prompt (Maifailun,
// Nexus, 01/10/2026, screenshot). New memories no longer carry it; this moves each old one into the structured
// store, keeping the latest day, and takes it out of the prose. Idempotent: a clean profile changes nothing, so
// it runs on every session launch with no version flag.

#region

using System.Text.RegularExpressions;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The court verbs' hidden cooldown markers, as they were once written into memory text.</summary>
    public static class CourtMarkerText
    {
        private static readonly Regex Marker = new Regex(@"\s*\[cr_(?:influence_spent|influence|troops_given|troops|sway:[^\]\s]+)\]",
                                                         RegexOptions.Compiled);

        /// <summary>The text without any court marker.</summary>
        public static string Strip(string text)
            => string.IsNullOrEmpty(text) ? text : Marker.Replace(text, "").Trim();

        /// <summary>
        ///   Moves every marker found in the profile's memories into its cooldown store (the latest day wins, so
        ///   a cooldown never restarts or shortens) and strips it from the prose. True when anything changed.
        /// </summary>
        public static bool Scrub(NpcProfile profile)
        {
            if (profile?.Events == null) return false;

            var changed = false;
            for (var i = 0; i < profile.Events.Count; i++)
            {
                NotableEvent ev = profile.Events[i];
                if (string.IsNullOrEmpty(ev?.summary)) continue;

                MatchCollection found = Marker.Matches(ev.summary);
                if (found.Count == 0) continue;

                foreach (Match m in found)
                {
                    string key = m.Value.Trim();
                    if (profile.CourtActionCooldowns.TryGetValue(key, out int day) && day >= ev.gameDay) continue;

                    profile.CourtActionCooldowns[key] = ev.gameDay;
                }

                profile.Events[i] = ev with {summary = Strip(ev.summary)};
                changed = true;
            }

            return changed;
        }
    }
}
