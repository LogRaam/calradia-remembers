// Code written by Gabriel Mailhot, 07/11/2026.
// kurushimi00 (Nexus, 06/11/2026): "two characters (Ira and Adram) who are enemies, having faced each other in a tournament
// 8 years ago ... I've had them both in the same conversation together on multiple occasions, where they made peace with
// each other ... Nevertheless, the encyclopedia entry still lists them as enemies from the tourney 8 years ago. Is there a
// way ... such that the player can play a role in healing rifts?" Nothing a conversation said ever reached what one
// character holds against another.
// Gabriel (07/11/2026) ruled for personal grudges, one per direction, over a shared rivalry score: each character makes
// peace in their own voice, and that pardon cancels only what THEY held against the other, up to that day. Ira may forgive
// while Adram still broods; the full reconciliation is both pardons. The past stays written; only its weight goes out. A
// slain or executed kinsman is never pardoned in a conversation (Gabriel, 06/11: "effacer une vieille rivalite de tournoi
// me semble juste, effacer un frere tue au combat, non"), nor a grudge only a deed or nothing at all can close.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>What one character may hold against another, as the pair's regard reads it.</summary>
    public enum PairWrongKind
    {
        /// <summary>A grievance from the pair's generated shared past (a tourney rivalry, a boundary quarrel, an insult).</summary>
        SharedPastGrievance,

        /// <summary>A wrong or a feud written in the campaign's authored history.</summary>
        AuthoredWrong,

        /// <summary>The other took them captive.</summary>
        TakenCaptive,

        /// <summary>The other beat them in battle.</summary>
        BeatenInBattle,

        /// <summary>A grudge in the ledger, held against the other.</summary>
        Grudge,

        /// <summary>The other killed one of their kin in battle.</summary>
        KinKilled,

        /// <summary>The other had one of their kin executed.</summary>
        KinExecuted
    }

    /// <summary>One thing held against the other, and whether a peace made since covers it.</summary>
    public sealed class PairWrongFacts
    {
        /// <summary>What kind of wrong it is.</summary>
        public PairWrongKind Kind { get; init; }

        /// <summary>The day it happened (CampaignTime.ToDays); a generated past is dated in the past.</summary>
        public int Day { get; init; }

        /// <summary>For a grudge, what its holder said could close it.</summary>
        public GrudgeResolutionKind Resolution { get; init; }

        /// <summary>The day the holder made peace with the other, or null if they never did.</summary>
        public int? PeaceDay { get; init; }
    }

    /// <summary>Which wrongs a peace made in conversation lays to rest.</summary>
    public static class PairPeacePolicy
    {
        /// <summary>
        ///   A peace made this many days after the last one between the same two, in the same direction, adds nothing:
        ///   the scene said it already, and a model repeating the act every line must not stack memories.
        /// </summary>
        public const int RepeatDays = 3;

        /// <summary>True when a peace can lay this kind of wrong to rest at all.</summary>
        public static bool Pardonable(PairWrongKind kind, GrudgeResolutionKind resolution)
        {
            switch (kind)
            {
                case PairWrongKind.KinKilled:
                case PairWrongKind.KinExecuted:
                    return false;
                case PairWrongKind.Grudge:
                    return resolution != GrudgeResolutionKind.Deed && resolution != GrudgeResolutionKind.Unforgivable;
                default:
                    return true;
            }
        }

        /// <summary>True when the holder's peace, made on or after the day of this wrong, cancels its weight.</summary>
        public static bool Cancels(PairWrongFacts w)
        {
            if (w?.PeaceDay == null) return false;

            return w.Day <= w.PeaceDay.Value && Pardonable(w.Kind, w.Resolution);
        }

        /// <summary>True when a new peace in this direction would only repeat one made a moment ago.</summary>
        public static bool IsRepeat(int? lastPeaceDay, int today) => lastPeaceDay.HasValue && today - lastPeaceDay.Value < RepeatDays;

        /// <summary>The memory line the holder keeps: "Made peace with Adram at Gabriel's table, in Pravend."</summary>
        public static string MemoryLine(string otherName, string playerName, string place)
        {
            string at = string.IsNullOrWhiteSpace(place) ? "" : $", in {place}";

            return $"Made peace with {otherName} at {(string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName)}'s table{at}.";
        }
    }
}
