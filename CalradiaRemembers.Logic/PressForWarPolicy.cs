// Code written by Gabriel Mailhot, 18/10/2026.
// press_for_war: the player urges a lord, in conversation, to push his realm into war against another. Gabriel
// (18/10/2026): "je veux aussi ajouter le fait qu'on puisse influencer en chat pour la guerre"; any lord ("tous les
// lords, c'est strategique"); the cost is "le risque que la faction visee apprenne que le joueur parle contre eux";
// the player sees nothing special, "sauf peut-etre qu'une guerre a plus de chance d'eclater". His rule of 02/07/2026
// stands: only the ruler declares; a lord may only press. So a convinced lord's house files the game's own war
// proposal (a kingdom decision, voted by the clans, decided by the ruler), the way the game's own houses do; the
// mod never declares the war. What this policy decides: whether a lord can press at all (told to him before he
// speaks, never refuted after), how his house leans by the game's own support, and how long he takes to reach court.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The facts one lord's pressing for war against one realm rests on, read from the game.</summary>
    public sealed class PressForWarFacts
    {
        /// <summary>The lord's house belongs to a realm (only a realm's houses propose its wars).</summary>
        public bool HouseInARealm { get; init; }

        /// <summary>The lord's house is the player's own (the player proposes for it, not the model).</summary>
        public bool IsPlayersHouse { get; init; }

        /// <summary>The target is another realm, still standing.</summary>
        public bool TargetIsAnotherRealm { get; init; }

        /// <summary>The two realms are already at war.</summary>
        public bool AlreadyAtWar { get; init; }

        /// <summary>The two realms are allied.</summary>
        public bool Allied { get; init; }

        /// <summary>Days since the two realms last made peace, or null when they never did.</summary>
        public int? DaysSincePeace { get; init; }

        /// <summary>A war proposal already waits in the lord's realm (the game allows one at a time).</summary>
        public bool WarProposalPending { get; init; }

        /// <summary>A pressing by this house already waits to be carried to court.</summary>
        public bool HouseAlreadyPressing { get; init; }

        /// <summary>The house's influence.</summary>
        public float HouseInfluence { get; init; }

        /// <summary>The game's influence cost for this house to propose a war.</summary>
        public int ProposalCost { get; init; }
    }

    /// <summary>How the lord's house stands on the war, by the game's own support for it.</summary>
    public enum HouseLeaning
    {
        Against,
        Torn,
        For
    }

    /// <summary>Whether and how a lord may press his realm toward war at the player's word. Pure.</summary>
    public static class PressForWarPolicy
    {
        /// <summary>The game's own rule: no war proposal against a realm the proposer's made peace with this recently.</summary>
        public const int MinDaysSincePeace = 20;

        /// <summary>TUNE: days a convinced lord takes to carry the matter to his realm's court.</summary>
        public const int DaysToReachCourt = 3;

        /// <summary>
        ///   The game's support a house must clear to back the war itself (the game's own houses propose a war only above
        ///   it, KingdomDecisionProposalBehavior.ConsiderWar), and below its negative a house is against it.
        /// </summary>
        public const float SupportThreshold = 50f;

        /// <summary>TUNE: the reach of the talk as court gossip (sway_opinion's is 20: this is graver, a war).</summary>
        public const int RumourMagnitude = 35;

        /// <summary>TUNE: how hard the talk wounds a lord of the target realm who hears of it (a sway's scale, 0 to 100).</summary>
        public const int ExposureMagnitude = 40;

        /// <summary>Why this lord cannot press his realm against this target now, or null when he can.</summary>
        public static string WhyNot(PressForWarFacts f)
        {
            if (f == null) return "nothing is known of it";
            if (!f.HouseInARealm) return "his house serves no realm";
            if (f.IsPlayersHouse) return "his house is the player's own";
            if (!f.TargetIsAnotherRealm) return "the target is not another realm";
            if (f.AlreadyAtWar) return "the two realms are already at war";
            if (f.Allied) return "the two realms are allied";
            if (f.DaysSincePeace is < MinDaysSincePeace) return "the two realms made peace too recently";
            if (f.WarProposalPending) return "a war proposal already waits in his realm";
            if (f.HouseAlreadyPressing) return "his house is already carrying such a matter to court";
            if (f.HouseInfluence < f.ProposalCost) return "his house lacks the influence to propose it";

            return null;
        }

        /// <summary>How the house leans, by the game's own support for the war (DeclareWarDecision.CalculateSupport).</summary>
        public static HouseLeaning Leaning(float support)
            => support > SupportThreshold ? HouseLeaning.For
               : support < -SupportThreshold ? HouseLeaning.Against
               : HouseLeaning.Torn;

        /// <summary>The leaning in the lord's own words, for the prompt.</summary>
        public static string LeaningWords(HouseLeaning leaning)
            => leaning switch
            {
                HouseLeaning.For => "your house would welcome it",
                HouseLeaning.Against => "your house is against it",
                _ => "your house is torn on it"
            };

        /// <summary>Words of a realm's name that say what it is, not which: every empire is an "Empire".</summary>
        private static readonly string[] GenericWords = {"kingdom", "empire", "khanate", "realm", "sultanate", "the", "of"};

        /// <summary>
        ///   The player named the realm in this conversation, by any distinctive word of its name, its people's or its
        ///   ruler's ("Vlandia", "the Vlandians", "Derthert"), as sway_opinion asks a case to name its subject (Maifailun,
        ///   01/10/2026: a bow is no case). Null words mean the path carries no conversation, which this rule does not judge.
        /// </summary>
        public static bool PlayerSpokeOfRealm(string playerWords, IEnumerable<string> names)
        {
            if (playerWords == null) return true;
            if (string.IsNullOrWhiteSpace(playerWords) || names == null) return false;

            return names.Where(n => !string.IsNullOrWhiteSpace(n))
                        .SelectMany(n => n.Split(new[] {' ', '\t', ',', '.', '\'', '-'}, StringSplitOptions.RemoveEmptyEntries))
                        .Where(part => part.Length >= 4 && !GenericWords.Contains(part.ToLowerInvariant()))
                        .Any(part => playerWords.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>The day the convinced lord's house files the proposal.</summary>
        public static int DueDay(int today) => today + DaysToReachCourt;
    }
}
