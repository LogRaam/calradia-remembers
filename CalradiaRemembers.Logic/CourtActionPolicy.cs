// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 1: pure decision logic for the four new conversation actions that give
// roleplay real vanilla consequences (give_influence, lend_troops, sway_opinion, release_prisoner).
// The engine-coupled facts are gathered in CalradiaRemembers.GameState.CourtActionResolver; this class
// only reasons over plain numbers, so the tuning stays protected by tests.
// 04/07/2026: added give_troops and spend_influence, the RECIPROCAL direction of lend_troops/give_influence
// (the PLAYER reinforces a needy NPC instead of the NPC favouring the player) — closes the last open
// Appreciations source, TroopsOrInfluenceSpentForThem.

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
        ///   The mirror of <see cref="TroopGiftCount" />, and the reason both are named: the two counts change
        ///   sides between them. Here the NPC gives and the player receives, so the party a fraction is kept
        ///   back from is the NPC's, not the player's.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class TroopLoanFacts
        {
           /// <summary>The NPC's standing with the player, which scales how many they would peel off.</summary>
           public int Relation { get; set; }

           /// <summary>The NPC's own regulars, never heroes. A retained fraction is kept back from these.</summary>
           public int NpcTotalRegularTroops { get; set; }

           /// <summary>How many more the player's party can hold.</summary>
           public int PlayerRoom { get; set; }
        }

        /// <summary>
        ///   How many regular troops (never heroes) an NPC would peel off for the player: scaled by
        ///   relation (<see cref="TroopLoanMaxCount" /> cap), never dipping the NPC's own party below
        ///   <see cref="TroopLoanMinRetainFraction" /> of its strength, and never more than the
        ///   player's remaining party room.
        /// </summary>
        public static int TroopLoanCount(TroopLoanFacts facts)
        {
           if (facts == null) return 0;

           int relation = facts.Relation;
           int npcTotalRegularTroops = facts.NpcTotalRegularTroops;
           int playerRoom = facts.PlayerRoom;

            if (relation <= 0 || npcTotalRegularTroops <= 0 || playerRoom <= 0) return 0;

            int desired = Math.Min(relation / TroopLoanRelationDivisor, TroopLoanMaxCount);
            int mustKeep = (int) Math.Ceiling(npcTotalRegularTroops * TroopLoanMinRetainFraction);
            int spare = Math.Max(0, npcTotalRegularTroops - mustKeep);

            return Math.Max(0, Math.Min(desired, Math.Min(spare, playerRoom)));
        }

        // ── give_troops (reciprocal of lend_troops: the player reinforces a needy NPC's own party) ──

        /// <summary>Tuning: an NPC's party counts as "weakened" only once its size limit is at least this large (a tiny elite escort is never "under-strength").</summary>
        public const int GiveTroopsMinPartySizeLimit = 15;

        /// <summary>Tuning: the fraction of their party's size limit an NPC's healthy troop count must fall below to count as weakened.</summary>
        public const float GiveTroopsWeakenedFraction = 0.5f;

        /// <summary>Tuning: minimum personal relation before an NPC will accept troops from the player at all — charity toward the needy, not a deep bond, so this sits far below lend_troops' own-initiative threshold.</summary>
        public const int GiveTroopsMinRelation = 0;

        /// <summary>Tuning: the fraction of the player's OWN healthy troops that must remain after a gift.</summary>
        public const float GiveTroopsPlayerMinRetainFraction = 0.70f;

        /// <summary>Tuning: the player's own party must never be reduced below this many healthy troops, regardless of size.</summary>
        public const int GiveTroopsPlayerFloor = 15;

        /// <summary>Tuning: the hard cap on a single reinforcement gift.</summary>
        public const int GiveTroopsMaxCount = 8;

        /// <summary>Tuning: relation points per +1 troop offered, once eligible.</summary>
        public const int GiveTroopsRelationDivisor = 6;

        /// <summary>Tuning: cooldown (in-game days) before the player can reinforce the same NPC again.</summary>
        public const int GiveTroopsCooldownDays = 14;

        /// <summary>
        ///   True when an NPC's own party is genuinely under-strength and worth reinforcing: fielding a party big
        ///   enough to matter (<see cref="GiveTroopsMinPartySizeLimit" />) yet under <see cref="GiveTroopsWeakenedFraction" />
        ///   of its own size limit in healthy troops.
        /// </summary>
        public static bool IsPartyWeakened(int healthyTroops, int partySizeLimit)
            => partySizeLimit >= GiveTroopsMinPartySizeLimit && healthyTroops < partySizeLimit * GiveTroopsWeakenedFraction;

        /// <summary>
        ///   What the player has to spare, and what the NPC's party can take. THREE INTS in a row where one is
        ///   a RELATION and the other two are TROOP COUNTS belonging to two different parties: swap the two
        ///   counts and the player hands over troops measured against the wrong party's room.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class TroopGiftFacts
        {
           /// <summary>The player's standing with them, which scales how many are offered.</summary>
           public int Relation { get; set; }

           /// <summary>
           ///   The player's own healthy regulars. A floor and a retained fraction are kept back from these.
           /// </summary>
           public int PlayerHealthyTroops { get; set; }

           /// <summary>How many more the NPC's party can hold.</summary>
           public int NpcRoom { get; set; }
        }

        /// <summary>
        ///   How many of the player's OWN healthy regular troops they could spare for a weakened NPC's party:
        ///   scaled modestly by relation (<see cref="GiveTroopsMaxCount" /> cap), never dipping the player's own
        ///   party below <see cref="GiveTroopsPlayerMinRetainFraction" /> of its current strength nor
        ///   <see cref="GiveTroopsPlayerFloor" /> troops outright, and never more than the NPC party's own
        ///   remaining room.
        /// </summary>
        public static int TroopGiftCount(TroopGiftFacts facts)
        {
           if (facts == null) return 0;

           int relation = facts.Relation;
           int playerHealthyTroops = facts.PlayerHealthyTroops;
           int npcRoom = facts.NpcRoom;

            if (relation < GiveTroopsMinRelation || playerHealthyTroops <= 0 || npcRoom <= 0) return 0;

            int mustKeep = Math.Max(GiveTroopsPlayerFloor, (int) Math.Ceiling(playerHealthyTroops * GiveTroopsPlayerMinRetainFraction));
            int spare = Math.Max(0, playerHealthyTroops - mustKeep);
            if (spare <= 0) return 0;

            int desired = Math.Min(GiveTroopsMaxCount, Math.Max(1, relation / GiveTroopsRelationDivisor + 1));

            return Math.Max(0, Math.Min(desired, Math.Min(spare, npcRoom)));
        }

        // ── spend_influence (reciprocal of give_influence: the player backs a needy lord's clan at court) ──

        /// <summary>Tuning: an NPC's own clan counts as needing court backing when its influence sits below this.</summary>
        public const int SpendInfluenceNpcNeedThreshold = 30;

        /// <summary>Tuning: minimum personal relation before an NPC will accept the player spending influence on their behalf — mirrors <see cref="GiveTroopsMinRelation" />'s much lower charity bar.</summary>
        public const int SpendInfluenceMinRelation = 0;

        /// <summary>Tuning: cooldown (in-game days) before the player can back the same NPC's clan again.</summary>
        public const int SpendInfluenceCooldownDays = 7;

        /// <summary>True when the given clan's influence is low enough to plausibly welcome the player's backing at court.</summary>
        public static bool ClanNeedsInfluence(float clanInfluence) => clanInfluence < SpendInfluenceNpcNeedThreshold;

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

        /// <summary>
        ///   True when a captive belongs to the player's OWN household and so is never merchandise: the player
        ///   themself (<paramref name="isMainHero" />) or any member of <c>Clan.PlayerClan</c>
        ///   (<paramref name="sharesPlayerClan" />). The held-prisoner pool (sell_prisoner / release_prisoner)
        ///   must exclude these: a member of the player's own house is not a hostage to be bought back into the
        ///   player's own cage or handled as goods. Excluding only the player left kin (a captured brother or
        ///   child) exposed as tradeable stock.
        /// </summary>
        public static bool IsPlayerHousehold(bool isMainHero, bool sharesPlayerClan)
            => isMainHero || sharesPlayerClan;

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
