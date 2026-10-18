// Code written by Gabriel Mailhot, 01/08/2026.
// The mailbox scheduler's first ingredient: turning a letter's SUBJECT into a base priority weight, before
// aging or budget ever enter the picture. A 100h player accumulates dozens of correspondents; not every
// subject deserves the same claim on their attention, so intrigue that demands a decision must outrank a
// tournament congratulation.

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Turns a letter's <see cref="LetterReason" /> into the base weight the mailbox scheduler
    ///   (<see cref="LetterMailboxPolicy" />) competes on. Every value here is a tuning dial, not a magic
    ///   number: named so Gabriel can retune a single tier without hunting through the switch.
    /// </summary>
    public static class LetterWeights
    {
        // ── Intrigue / actionable: the player must decide or react, so these lead the mailbox ──────────

        /// <summary>Extortion over the player's own secrets: the single highest-stakes letter reason.</summary>
        public const int WeightBlackmail = 90;

        /// <summary>An ally calling for military aid mid-war: time-sensitive, ignoring it has consequences.</summary>
        public const int WeightReinforcementRequest = 85;

        /// <summary>A jealous confrontation or threat: high emotional stakes, often the opening of a conflict.</summary>
        public const int WeightJealousThreat = 85;

        /// <summary>An enemy pressuring the player for sensitive information: adversarial and actionable.</summary>
        public const int WeightCorruptionAttempt = 80;

        /// <summary>An antagonist exploiting a private letter of the player's they intercepted on the road: adversarial and actionable, a sibling of blackmail over a secret now fallen into the wrong hands.</summary>
        public const int WeightLeakedCorrespondence = 85;

        /// <summary>A lord offering to buy a prisoner the player holds: an actionable trade the player can settle from the letter, near a quest update in weight.</summary>
        public const int WeightPrisonerOffer = 78;

        /// <summary>A marriage proposal: a genuine decision the player must eventually answer.</summary>
        public const int WeightMarriageProposal = 80;

        /// <summary>A quest giver checking in on a stamped deed: ties directly into active play.</summary>
        public const int WeightQuestUpdate = 75;

        /// <summary>A lord's open enmity, sometimes a challenge to settle things by the blade: consequential.</summary>
        public const int WeightStanceHostility = 75;

        /// <summary>A faction leader proposing alliance: a strategic decision, not idle chatter.</summary>
        public const int WeightPoliticalAlliance = 75;

        /// <summary>A gang leader's favor request: actionable, but a smaller stake than a faction alliance.</summary>
        public const int WeightGangFavor = 70;

        /// <summary>A bastard's mother writing (not extorting): personal stakes, still below open intrigue.</summary>
        public const int WeightBastardMotherNote = 70;

        /// <summary>An intercept that never landed, arriving as a letter instead: the fallback of a missed event.</summary>
        public const int WeightInterceptionMissed = 55;

        // ── Personal / emotional: matters to the relationship, rarely urgent to the wider game ─────────

        /// <summary>A lover who wrote and heard nothing back, writing again: the waiting is its own escalation.</summary>
        public const int WeightAwaitingReply = 60;

        /// <summary>A lover missing the player: the relationship's heartbeat, not a plot event.</summary>
        public const int WeightRomanticCorrespondence = 55;

        /// <summary>The player's own spouse writing from afar: the same tier as any other romantic longing.</summary>
        public const int WeightSpouseCorrespondence = 55;

        /// <summary>A lord's unbidden goodwill or warning: welcome, but never as urgent as open hostility.</summary>
        public const int WeightStanceFavor = 50;

        /// <summary>
        ///   A friend relaying news of the realm (a looming war, hard times, a rumour that reached them):
        ///   ambient realm news, pleasant to receive and not urgent, so it sits above pure flavor like a birth
        ///   announcement or a tournament win yet below a personal note such as a lord's unbidden goodwill.
        /// </summary>
        public const int WeightRealmTidings = 40;

        /// <summary>
        ///   A lord poaching the player into their service. A real proposition, but deliberately LOW-weight so it
        ///   stays a rare, memorable letter (Gabriel: "pas trop souvent") rather than routine mailbox noise; the
        ///   collector's own long cooldown and one-offer-per-lord dedupe do the rest.
        /// </summary>
        public const int WeightServiceOffer = 45;

        // ── Flavor: pleasant, never urgent, the first tier to yield when the mailbox is full ────────────

        /// <summary>A birth announcement: a lovely thing to read, never something the player must act on today.</summary>
        public const int WeightBirthAnnouncement = 35;

        /// <summary>Tournament congratulations: pure flavor.</summary>
        public const int WeightTournamentVictory = 30;

        /// <summary>A grounded per-opponent tournament note (brag / gracious / congrats): flavor, same tier as a victory acclaim.</summary>
        public const int WeightTournamentResult = 30;

        /// <summary>
        ///   A battle congratulation's FLOOR before <see cref="BattleVictoryWeight" /> adds the underdog
        ///   bonus: an even fight is still just flavor, so it starts no higher than a tournament win.
        /// </summary>
        public const int BattleBaseWeight = 30;

        // ── Guaranteed-bypass reasons: never compete for budget (see LetterMailboxPolicy.BypassesBudget) ─

        /// <summary>
        ///   The sentinel weight for a reason that always bypasses the budget entirely. It never actually
        ///   competes against a budgeted candidate, since the caller filters these reasons out before
        ///   building the candidate list, but it must still outrank everything if a caller ever mishandles
        ///   that split and lets one through by mistake.
        /// </summary>
        public const int WeightGuaranteedBypassSentinel = 100;

        // ── Battle-victory underdog bonus: named tuning dials for BattleVictoryWeight ────────────────────

        /// <summary>The ratio (enemy strength / player strength) at or below which a fight was no real fight.</summary>
        public const double StompRatioThreshold = 0.5;

        /// <summary>The flat weight for a stomp: barely worth a letter at all, well below ordinary flavor.</summary>
        public const int BattleStompWeight = 20;

        /// <summary>How many weight points each full point of underdog ratio (above even) adds.</summary>
        public const int BattleUnderdogSlope = 40;

        /// <summary>The ceiling on the underdog bonus, so no battle letter can out-rank Blackmail-tier intrigue.</summary>
        public const int BattleUnderdogBonusCap = 65;

        // ── Graceful degrade for an unmapped reason: production must never crash on a forgotten mapping ────

        /// <summary>
        ///   The weight <see cref="BaseWeight" /> returns for a <see cref="LetterReason" /> nobody has mapped
        ///   yet. Low enough to sink to the bottom of the mailbox rather than compete, but never a crash: a
        ///   Sprint that adds a reason and forgets this switch must not take down the live daily tick over it.
        ///   Dev-time safety still exists, just moved to a test that iterates every enum value instead.
        /// </summary>
        public const int UnmappedDefaultWeight = 25;

        // ── Correspondence-interest damping floor (see CorrespondenceInterestPolicy) ─────────────────────

        /// <summary>
        ///   The floor <see cref="WeightWithInterest" /> never falls below, so interest dampers alone can
        ///   never fully silence a correspondent: even a sender the player has repeatedly ignored still gets
        ///   a chance, however slim, to compete for the mailbox.
        /// </summary>
        public const int EffectiveWeightFloor = 10;

        /// <summary>
        ///   The base priority weight for a letter's subject. Every <see cref="LetterReason" /> is meant to
        ///   be covered explicitly; a forgotten mapping degrades to <see cref="UnmappedDefaultWeight" />
        ///   rather than throwing, because a live daily tick must never crash over a Sprint's oversight. The
        ///   oversight itself is still caught, just at dev time, by a test that iterates every enum value and
        ///   pins its exact expected weight.
        /// </summary>
        public static int BaseWeight(LetterReason reason)
        {
            switch (reason)
            {
                case LetterReason.Blackmail: return WeightBlackmail;
                case LetterReason.ReinforcementRequest: return WeightReinforcementRequest;
                case LetterReason.JealousThreat: return WeightJealousThreat;
                case LetterReason.CorruptionAttempt: return WeightCorruptionAttempt;
                case LetterReason.LeakedCorrespondence: return WeightLeakedCorrespondence;
                case LetterReason.PrisonerOffer: return WeightPrisonerOffer;
                case LetterReason.ServiceOffer: return WeightServiceOffer;
                case LetterReason.MarriageProposal: return WeightMarriageProposal;
                case LetterReason.QuestUpdate: return WeightQuestUpdate;
                case LetterReason.StanceHostility: return WeightStanceHostility;
                case LetterReason.PoliticalAlliance: return WeightPoliticalAlliance;
                case LetterReason.GangFavor: return WeightGangFavor;
                case LetterReason.BastardMotherNote: return WeightBastardMotherNote;
                case LetterReason.InterceptionMissed: return WeightInterceptionMissed;

                case LetterReason.AwaitingReply: return WeightAwaitingReply;
                case LetterReason.RomanticCorrespondence: return WeightRomanticCorrespondence;
                case LetterReason.SpouseCorrespondence: return WeightSpouseCorrespondence;
                case LetterReason.StanceFavor: return WeightStanceFavor;
                case LetterReason.RealmTidings: return WeightRealmTidings;

                case LetterReason.BirthAnnouncement: return WeightBirthAnnouncement;
                case LetterReason.TournamentVictory: return WeightTournamentVictory;
                case LetterReason.TournamentResult: return WeightTournamentResult;
                case LetterReason.BattleVictory: return BattleBaseWeight;

                case LetterReason.ChildSupportRequest:
                case LetterReason.SpouseDivorceDemand:
                case LetterReason.SpouseDivorceEscalation:
                case LetterReason.CompanionFarewell:
                case LetterReason.PlayerLetterReply:
                case LetterReason.WarSummons:
                case LetterReason.WarLevyDemand:
                case LetterReason.PatrolReport:
                case LetterReason.FromAnotherMod:
                    return WeightGuaranteedBypassSentinel;

                default:
                    return UnmappedDefaultWeight;
            }
        }

        /// <summary>
        ///   A letter's base weight and the three adaptive interest dampers applied on top of it. FOUR INTS in
        ///   a row where the first is a WEIGHT and the other three are NEGATIVE dampers on three different
        ///   scopes: put the base weight anywhere but first and the sum is still an int, still plausible, and
        ///   still wrong. Each damper is clamped to its own floor by the caller; this only sums and floors.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class InterestDampers
        {
           /// <summary>The letter's already-computed base weight.</summary>
           public int BaseWeight { get; set; }

           /// <summary>Damper for how little the player engages with THIS correspondent.</summary>
           public int SenderInterest { get; set; }

           /// <summary>Damper for how little the player engages with this SUBJECT.</summary>
           public int SubjectInterest { get; set; }

           /// <summary>Damper for how little the player engages with letters at all.</summary>
           public int GlobalInterest { get; set; }
        }

        /// <summary>
        ///   Applies the adaptive correspondence-interest dampers (see <see cref="CorrespondenceInterestPolicy" />)
        ///   on top of a letter's already-computed base weight. Each of <c>SenderInterest</c>,
        ///   <c>SubjectInterest</c> and <c>GlobalInterest</c> is a damper the caller
        ///   has already clamped to its own floor (never positive), so this method only ever sums and floors;
        ///   it never re-clamps the individual interests itself. <see cref="EffectiveWeightFloor" /> guarantees
        ///   that no correspondent is ever fully silenced by interest alone, however disinterested the player
        ///   has signalled.
        /// </summary>
        public static int WeightWithInterest(InterestDampers facts)
        {
           facts ??= new InterestDampers();

           int baseWeight = facts.BaseWeight;
           int senderInterest = facts.SenderInterest;
           int subjectInterest = facts.SubjectInterest;
           int globalInterest = facts.GlobalInterest;

            return Math.Max(EffectiveWeightFloor, baseWeight + senderInterest + subjectInterest + globalInterest);
        }

        /// <summary>
        ///   The weight for a battle-victory letter, scaled by how outmatched the player was
        ///   (<paramref name="disadvantageRatio" /> = enemy strength / player strength; 1.0 is even, above 1
        ///   means the player was outnumbered). An even or favourable fight is plain flavor
        ///   (<see cref="BattleBaseWeight" />); a genuine underdog win climbs toward intrigue-tier weight,
        ///   capped so a battle letter can never out-rank Blackmail; and a rout so lopsided it was no fight
        ///   at all (below <see cref="StompRatioThreshold" />) floors to <see cref="BattleStompWeight" />,
        ///   because stomping a much weaker enemy is not the remarkable deed this bonus exists to reward.
        /// </summary>
        public static int BattleVictoryWeight(double disadvantageRatio)
        {
            if (disadvantageRatio < StompRatioThreshold) return BattleStompWeight;

            double bonus = Math.Max(0.0, (disadvantageRatio - 1.0) * BattleUnderdogSlope);
            bonus = Math.Min(bonus, BattleUnderdogBonusCap);

            return (int)Math.Round(BattleBaseWeight + bonus);
        }
    }
}
