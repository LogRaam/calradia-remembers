// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 1: pure decision logic for the four new conversation actions that give
// roleplay real vanilla consequences (give_influence, lend_troops, sway_opinion, release_prisoner).
// The engine-coupled facts are gathered in CalradiaRemembers.GameState.CourtActionResolver; this class
// only reasons over plain numbers, so the tuning stays protected by tests.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure math and gates for the four "court action" verbs. Every constant here is a tuning knob:
    ///   changing one is a one-line, test-covered decision, never a hunt through engine code.
    /// </summary>
    public static class CourtActionPolicy
    {
        // ── give_influence ───────────────────────────────────────────────────

        /// <summary>Tuning: minimum personal relation before an NPC will lend political influence at all.</summary>
        public const int InfluenceMinRelation = 10;

        /// <summary>Tuning: the flat base of an influence grant, before the relation bonus.</summary>
        public const int InfluenceBaseGrant = 5;

        /// <summary>Tuning: relation points per +1 bonus point of influence granted.</summary>
        public const int InfluenceRelationDivisor = 10;

        /// <summary>Tuning: the hard cap on a single influence grant, however deep the bond.</summary>
        public const int InfluenceMaxGrant = 15;

        /// <summary>Tuning: how many times the grant amount the giver's clan must hold in reserve to afford it.</summary>
        public const int InfluenceBufferMultiplier = 3;

        /// <summary>Tuning: cooldown (in-game days) before the same NPC will lend influence again.</summary>
        public const int InfluenceCooldownDays = 7;

        /// <summary>
        ///   The influence a lord would lend at court, scaled by how deep the bond runs
        ///   (<see cref="InfluenceBaseGrant" /> + relation / <see cref="InfluenceRelationDivisor" />),
        ///   capped at <see cref="InfluenceMaxGrant" />. Relation below zero grants nothing.
        /// </summary>
        public static int InfluenceGrant(int relation)
        {
            if (relation <= 0) return 0;

            int amount = InfluenceBaseGrant + relation / InfluenceRelationDivisor;

            return Math.Min(amount, InfluenceMaxGrant);
        }

        /// <summary>
        ///   True when the giver's clan holds comfortably more influence than the grant costs
        ///   (<see cref="InfluenceBufferMultiplier" />x) — a lord does not beggar their own house
        ///   to do the player a favour.
        /// </summary>
        public static bool CanAffordInfluenceGrant(float clanInfluence, int amount)
            => amount > 0 && clanInfluence >= amount * InfluenceBufferMultiplier;

        // ── lend_troops ──────────────────────────────────────────────────────

        /// <summary>Tuning: minimum personal relation before an NPC will lend troops at all.</summary>
        public const int TroopLoanMinRelation = 20;

        /// <summary>Tuning: relation points per +1 troop lent.</summary>
        public const int TroopLoanRelationDivisor = 5;

        /// <summary>Tuning: the hard cap on a single troop loan, however deep the bond.</summary>
        public const int TroopLoanMaxCount = 10;

        /// <summary>Tuning: the fraction of their own strength a party leader must keep for themselves.</summary>
        public const float TroopLoanMinRetainFraction = 0.60f;

        /// <summary>Tuning: the party must field at least this many regular troops before any may be lent.</summary>
        public const int TroopLoanMinPartyStrength = 10;

        /// <summary>Tuning: cooldown (in-game days) before the same NPC will lend troops again.</summary>
        public const int TroopLoanCooldownDays = 14;

        /// <summary>
        ///   How many regular troops (never heroes) an NPC would peel off for the player: scaled by
        ///   relation (<see cref="TroopLoanMaxCount" /> cap), never dipping the NPC's own party below
        ///   <see cref="TroopLoanMinRetainFraction" /> of its strength, and never more than the
        ///   player's remaining party room.
        /// </summary>
        public static int TroopLoanCount(int relation, int npcTotalRegularTroops, int playerRoom)
        {
            if (relation <= 0 || npcTotalRegularTroops <= 0 || playerRoom <= 0) return 0;

            int desired = Math.Min(relation / TroopLoanRelationDivisor, TroopLoanMaxCount);
            int mustKeep = (int) Math.Ceiling(npcTotalRegularTroops * TroopLoanMinRetainFraction);
            int spare = Math.Max(0, npcTotalRegularTroops - mustKeep);

            return Math.Max(0, Math.Min(desired, Math.Min(spare, playerRoom)));
        }

        // ── sway_opinion ─────────────────────────────────────────────────────

        /// <summary>Tuning: relation change dealt to the target when the NPC is swayed AGAINST them.</summary>
        public const int SwayAgainstDelta = -6;

        /// <summary>Tuning: relation change dealt to the target when the NPC is swayed FOR (in favour of) them.</summary>
        public const int SwayForDelta = 4;

        /// <summary>Tuning: cooldown (in-game days) before the same NPC can be swayed again about the same target.</summary>
        public const int SwayCooldownDays = 10;

        /// <summary>The relation delta applied between the NPC and the named third party.</summary>
        public static int SwayDelta(bool against) => against ? SwayAgainstDelta : SwayForDelta;

        /// <summary>
        ///   A candidate third party for a sway_opinion target: their id, the swaying NPC's current
        ///   relation to them, and whether they are the NPC's close kin (spouse, child, parent, or
        ///   sibling). The engine-coupled family check lives in <c>CourtActionResolver</c>; this struct
        ///   only carries the already-resolved facts so the picking rule stays pure and testable.
        /// </summary>
        public readonly struct SwayCandidate
        {
            public string Id { get; }
            public int Relation { get; }
            public bool IsCloseKin { get; }
            public bool IsFactionLeader { get; }

            public SwayCandidate(string id, int relation, bool isCloseKin, bool isFactionLeader = false)
            {
                Id = id;
                Relation = relation;
                IsCloseKin = isCloseKin;
                IsFactionLeader = isFactionLeader;
            }
        }

        /// <summary>
        ///   Picks the most plausible sway_opinion target from a pool of living third parties: NEVER the
        ///   NPC's close kin (nobody turns against their own spouse, child, parent, or sibling on
        ///   request), and NEVER a faction leader (moving against a ruler is a war-scale act, the leader's own
        ///   strategic decision, not a personal rivalry a lord settles at another's word), preferring whoever
        ///   they already regard worst (most negative relation) among the rest, since an existing rift makes a
        ///   turn believable. Falls back to the least-friendly remaining candidate when every relation is
        ///   positive. Null when every candidate is excluded (or the pool is empty).
        /// </summary>
        public static string SelectSwayTarget(IEnumerable<SwayCandidate> candidates)
        {
            if (candidates == null) return null;

            List<SwayCandidate> eligible = candidates.Where(c => !c.IsCloseKin && !c.IsFactionLeader).ToList();
            if (eligible.Count == 0) return null;

            return eligible.OrderBy(c => c.Relation).First().Id;
        }

        // ── pledge_against (RP with teeth, increment 2) ─────────────────────

        /// <summary>Tuning: relation at/below which a lord's dislike of another counts as genuine cause to pledge against them.</summary>
        public const int PledgeMinAnimosity = -20;

        /// <summary>
        ///   Picks the most plausible pledge_against rival from a pool of living third parties: NEVER the
        ///   promisor's own close kin (mirrors <see cref="SelectSwayTarget" />), and only among those they
        ///   hold genuine, standing enmity toward (relation at or below <see cref="PledgeMinAnimosity" />) —
        ///   a pledge needs a real cause, not merely the least friendly face in an amicable pool. Prefers
        ///   the deepest enmity when several qualify. Null when no eligible candidate exists.
        /// </summary>
        public static string SelectPledgeTarget(IEnumerable<SwayCandidate> candidates)
        {
            if (candidates == null) return null;

            List<SwayCandidate> eligible = candidates.Where(c => !c.IsCloseKin && !c.IsFactionLeader && c.Relation <= PledgeMinAnimosity).ToList();
            if (eligible.Count == 0) return null;

            return eligible.OrderBy(c => c.Relation).First().Id;
        }

        // ── release_prisoner ─────────────────────────────────────────────────

        /// <summary>Tuning: how many held prisoners are named in the teaching text at once.</summary>
        public const int ReleasePrisonerMaxNamed = 3;

        /// <summary>Tuning: relation bump granted to the freed hero toward the player who interceded.</summary>
        public const int ReleasePrisonerRelationBump = 5;

        // ── shared cooldown math ────────────────────────────────────────────

        /// <summary>
        ///   True when enough in-game days have passed since <paramref name="lastRecordedDay" /> (null
        ///   meaning "never recorded") for the cooldown to have elapsed. Shared by every verb above: the
        ///   caller finds <paramref name="lastRecordedDay" /> by scanning the NPC's own memory events for
        ///   the action's marker (no new persisted field needed).
        /// </summary>
        public static bool CooldownElapsed(int? lastRecordedDay, int today, int cooldownDays)
            => lastRecordedDay == null || today - lastRecordedDay.Value >= cooldownDays;
    }
}
