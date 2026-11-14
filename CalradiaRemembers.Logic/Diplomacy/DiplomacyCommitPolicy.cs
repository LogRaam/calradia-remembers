// Code written by Gabriel Mailhot, 14/11/2026.
// The diplomacy authority door. VonLemberg's team (Bellum Civile bridge, RFC of 14/11/2026, P0-B): about eight CR paths
// apply war, peace or a change of realm directly (mediate_peace, the council, scenario consequences, arrests...), while
// Bellum Civile may refuse a direct peace or route it through a treaty; "The central risk is a mismatch between what the LLM
// says happened and what the game actually committed." Gabriel, same day: "go pour les étapes 1 et 2". Every such change
// now asks the registered authorities first; this decides what their answers mean and what the player is told, so CR never
// reports a peace or a war that did not happen (the mod declares, the model never contradicts the game).

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Diplomacy
{
    /// <summary>A diplomatic change CR is about to make (the public door mirrors it as CrDiplomacyKind, same values).</summary>
    public enum DiplomacyChange
    {
        /// <summary>Faction A declares war on faction B.</summary>
        DeclareWar = 0,

        /// <summary>Factions A and B make peace.</summary>
        MakePeace = 1,

        /// <summary>A clan joins a kingdom as a vassal.</summary>
        ClanJoinsKingdom = 2,

        /// <summary>A clan joins a kingdom as a mercenary.</summary>
        ClanJoinsAsMercenary = 3,

        /// <summary>A clan leaves its kingdom.</summary>
        ClanLeavesKingdom = 4,

        /// <summary>A clan ends its mercenary service.</summary>
        ClanLeavesMercenaryService = 5
    }

    /// <summary>What an authority answers to a diplomatic change CR is about to make.</summary>
    public enum DiplomacyVerdict
    {
        /// <summary>No opinion: CR makes the change itself, as it always did.</summary>
        PassThrough = 0,

        /// <summary>The change is not allowed: CR makes nothing and says so.</summary>
        Rejected = 1,

        /// <summary>The change is put to the authority's own process (a treaty, a vote): CR makes nothing and says it has not happened yet.</summary>
        Pending = 2,

        /// <summary>The authority made the change itself: CR does not make it a second time.</summary>
        Applied = 3
    }

    /// <summary>Reads the authorities' answers to a diplomatic change. Pure.</summary>
    public static class DiplomacyCommitPolicy
    {
        /// <summary>The answer that counts: the first that is not <see cref="DiplomacyVerdict.PassThrough" />, in the authorities' order; PassThrough when none.</summary>
        public static DiplomacyVerdict Decide(IEnumerable<DiplomacyVerdict> answers)
            => (answers ?? Enumerable.Empty<DiplomacyVerdict>()).FirstOrDefault(a => a != DiplomacyVerdict.PassThrough);

        /// <summary>Whether CR itself applies the change: only when no authority took it.</summary>
        public static bool CrApplies(DiplomacyVerdict verdict) => verdict == DiplomacyVerdict.PassThrough;

        /// <summary>Whether the change has happened once the door is passed: applied by CR, or by the authority.</summary>
        public static bool HasHappened(DiplomacyVerdict verdict) => verdict == DiplomacyVerdict.PassThrough || verdict == DiplomacyVerdict.Applied;

        /// <summary>
        ///   What the player reads when the change did not happen: <paramref name="change" /> names it ("Peace between Vlandia and
        ///   Sturgia"), <paramref name="authorityMessage" /> is the authority's own reason, when it gave one. Null when it happened.
        /// </summary>
        public static string NotHappenedLine(DiplomacyVerdict verdict, string change, string authorityMessage)
        {
            string what = string.IsNullOrWhiteSpace(change) ? "This change" : change.Trim();
            string why = string.IsNullOrWhiteSpace(authorityMessage) ? null : authorityMessage.Trim().TrimEnd('.');

            switch (verdict)
            {
                case DiplomacyVerdict.Rejected:
                    return why == null ? $"({what} did not happen: the realm's own rules refused it.)" : $"({what} did not happen: {why}.)";
                case DiplomacyVerdict.Pending:
                    return why == null ? $"({what} is put to the realm's decision and has not happened yet.)" : $"({what} has not happened yet: {why}.)";
                default:
                    return null;
            }
        }
    }
}
