// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box (Gabriel, 30/10/2026), after tashmetu relayed two players buried in letters: "toutes les lettres sont
// envoyees et vont dans la boite aux lettres. Les plus importantes ont un icone urgent. Les lettres avec signet ont un
// icone de signet. Les autres sont neutres. Et les lettres jetees enlevent le sujet des lettres.. par exemple un mari
// jaloux qui ecrit et la lettre est jetee sera considere comme 'le joueur ne veut plus entendre parler de ce sujet de la
// part de cet expediteur'. Les lettres sont classees par ordre de reception. Une lettre lue est enlevee de la boite aux
// lettres. [...] c'est le joueur qui classe, elimine, abonne et desabonne les conversations par lettres."
// This decides what a letter's subject is (so one throw silences the like of it, and only that), which letters are
// urgent, which subjects can never be silenced, whether a letter now arriving is silenced, and the box's order.
// Bound by "it silences the letter, not the person": nothing here changes what a character does in the world.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>One letter waiting in the box, as the box needs to place and mark it.</summary>
    public sealed class LetterBoxEntry
    {
        /// <summary>The sender's hero id.</summary>
        public string SenderId { get; init; }

        /// <summary>Why the sender wrote.</summary>
        public LetterReason Reason { get; init; }

        /// <summary>The game day the letter reached the player.</summary>
        public int ArrivedOnDay { get; init; }

        /// <summary>Its place in the sender's own list of letters: two letters arriving the same day keep the order they came in.</summary>
        public int Sequence { get; init; }

        /// <summary>The player marked this correspondent with the ribbon.</summary>
        public bool Awaited { get; init; }
    }

    /// <summary>Subjects, urgency, silencing and order for the letter box. Pure.</summary>
    public static class LetterBoxPolicy
    {
        /// <summary>
        ///   The subject a letter is about, as the player would name it: what one throw silences from that sender. Letters
        ///   of one conversation share a subject (a divorce demand and its escalation; a love letter and "you did not answer
        ///   me"), so throwing one does not leave its sequel to arrive. Null for what is never silenced: a reply to a letter
        ///   the player wrote (they asked for it), and another mod's letter (its subject is that mod's to know; throwing it
        ///   raises the mod's own set-aside event instead).
        /// </summary>
        public static string SubjectOf(LetterReason reason) => reason switch {
            LetterReason.PlayerLetterReply => null,
            LetterReason.FromAnotherMod => null,
            LetterReason.TournamentVictory or LetterReason.BattleVictory or LetterReason.TournamentResult => "congratulations",
            LetterReason.RomanticCorrespondence or LetterReason.AwaitingReply => "romance",
            LetterReason.SpouseCorrespondence => "spouse",
            LetterReason.SpouseDivorceDemand or LetterReason.SpouseDivorceEscalation => "divorce",
            LetterReason.MarriageProposal => "marriage",
            LetterReason.RealmTidings or LetterReason.LeakedCorrespondence => "news",
            LetterReason.StanceFavor => "friendship",
            LetterReason.StanceHostility or LetterReason.InterceptionMissed => "hostility",
            LetterReason.CompanionFarewell => "farewell",
            LetterReason.PatrolReport => "patrol",
            LetterReason.JealousThreat => "jealousy",
            LetterReason.WarSummons or LetterReason.WarLevyDemand => "war_duty",
            LetterReason.PrisonerOffer or LetterReason.ServiceOffer => "offers",
            LetterReason.BirthAnnouncement or LetterReason.ChildSupportRequest or LetterReason.BastardMotherNote => "children",
            LetterReason.CorruptionAttempt or LetterReason.Blackmail or LetterReason.GangFavor => "dark_dealings",
            LetterReason.ReinforcementRequest or LetterReason.PoliticalAlliance or LetterReason.StandingAim or LetterReason.QuestUpdate => "requests",
            _ => reason.ToString()
        };

        /// <summary>The subject as the player reads it in the box ("war_duty" reads "war duty").</summary>
        public static string SubjectLabel(string subject) => string.IsNullOrEmpty(subject) ? "" : subject.Replace('_', ' ');

        /// <summary>
        ///   The letters that carry a deadline or a consequence the player must not miss: they wear the urgent mark. A war
        ///   summons or levy (the game holds the player to its deadline), a prisoner offer, a divorce under way, blackmail, a
        ///   jealous threat, a child's support.
        /// </summary>
        public static bool IsUrgent(LetterReason reason) => reason is LetterReason.WarSummons or LetterReason.WarLevyDemand
            or LetterReason.PrisonerOffer or LetterReason.SpouseDivorceDemand or LetterReason.SpouseDivorceEscalation
            or LetterReason.Blackmail or LetterReason.JealousThreat or LetterReason.ChildSupportRequest;

        /// <summary>
        ///   The letters a throw never silences, urgent or not: what the game holds the player to, or must always tell. A war
        ///   summons or levy (the next one still carries a deadline), a divorce under way ("the player must always be told",
        ///   LetterDeliveryPolicy.IsGuaranteedDelivery), a child's support (it opens the pension's quest), a patrol's report
        ///   (on it the player decides whether the men come home, Gabriel 16/10/2026). Throwing one
        ///   discards that letter only (Claude's default, 30/10/2026, put to Gabriel). A jealous threat or blackmail is
        ///   urgent but CAN be silenced: Gabriel's own example is the jealous husband whose letter, thrown, means the player
        ///   no longer wants to hear of it; what he does in the world is unchanged.
        /// </summary>
        public static bool IsNeverSilenced(LetterReason reason) => reason is LetterReason.WarSummons or LetterReason.WarLevyDemand
            or LetterReason.SpouseDivorceDemand or LetterReason.SpouseDivorceEscalation or LetterReason.ChildSupportRequest
            or LetterReason.PatrolReport;

        /// <summary>True when throwing this letter silences its subject from its sender.</summary>
        public static bool ThrowSilences(LetterReason reason) => SubjectOf(reason) != null && !IsNeverSilenced(reason);

        /// <summary>True when a letter now arriving from a sender is one the player asked not to receive.</summary>
        public static bool IsSilenced(IEnumerable<string> silencedSubjects, LetterReason reason)
        {
            if (silencedSubjects == null || !ThrowSilences(reason)) return false;

            string subject = SubjectOf(reason);

            return silencedSubjects.Contains(subject);
        }

        /// <summary>The sender's silenced subjects once this letter is thrown (unchanged when the throw silences nothing).</summary>
        public static List<string> AfterThrow(IEnumerable<string> silencedSubjects, LetterReason reason)
        {
            var list = silencedSubjects?.ToList() ?? new List<string>();
            if (!ThrowSilences(reason)) return list;

            string subject = SubjectOf(reason);
            if (!list.Contains(subject)) list.Add(subject);

            return list;
        }

        /// <summary>The box's order (Gabriel: "classees par ordre de reception"): oldest arrival first, then as they came.</summary>
        public static IReadOnlyList<LetterBoxEntry> Ordered(IEnumerable<LetterBoxEntry> entries)
            => entries == null
                ? new List<LetterBoxEntry>()
                : entries.Where(e => e != null).OrderBy(e => e.ArrivedOnDay).ThenBy(e => e.Sequence).ThenBy(e => e.SenderId, System.StringComparer.Ordinal).ToList();

        /// <summary>What the map bar's badge says: nothing at zero, the count otherwise, capped at two digits, all the small badge holds.</summary>
        public static string BadgeText(int unread) => unread <= 0 ? "" : unread > MaxBadgeCount ? MaxBadgeCount.ToString() : unread.ToString();

        /// <summary>TUNE: the largest count the map bar's badge prints in full (it is 28 units wide).</summary>
        public const int MaxBadgeCount = 99;
    }
}
