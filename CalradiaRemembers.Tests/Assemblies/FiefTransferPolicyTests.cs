// Code written by Gabriel Mailhot, 04/08/2026.
// Documents grant_fief/revoke_fief's pure gate: whether a fief transfer between the crown and a vassal is
// currently legal. A fief is CLAN-owned in vanilla, never hero-owned, so the stake across every test here is the
// same one COUNCIL_ACTIONS.md's Kimi review pins: only a true kingdom sovereign may move one at all, and only
// when the named fief is actually where the pledge says it is (the player's own clan for a grant, the spoliated
// vassal's own clan for a revoke). Getting either AND wrong either lets a non-sovereign clan leader hand out or
// seize a fief that was never theirs to move, or refuses a legal transfer the lift should have carried out.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class FiefTransferPolicyTests
    {
        // The ordinary case: a true sovereign giving away a fief their own clan actually holds is legal.
        [Test]
        public void GIVEN_a_sovereign_and_a_player_owned_fief_WHEN_asked_to_grant_THEN_it_is_legal()
        {
            FiefTransferPolicy.CanGrant(playerIsKingdomLeader: true, fiefOwnedByPlayerClan: true).Should().BeTrue();
        }

        // Gabriel's own condition, corrected by the Kimi review: "chef de clan" is not enough, since a clan's
        // own fiefs already belong to that clan. Without true KINGDOM sovereignty, granting a "crown" fief is
        // meaningless (there is no crown to speak from), so it must be refused however the fief is owned.
        [Test]
        public void GIVEN_the_player_is_not_the_kingdom_sovereign_WHEN_asked_to_grant_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanGrant(playerIsKingdomLeader: false, fiefOwnedByPlayerClan: true).Should().BeFalse();
        }

        // The fief must be a REAL crown fief the player's own clan holds right now: naming a fief already given
        // away, or one that was never the player's, must be refused, never conjured from nothing.
        [Test]
        public void GIVEN_the_named_fief_is_not_the_players_own_WHEN_asked_to_grant_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanGrant(playerIsKingdomLeader: true, fiefOwnedByPlayerClan: false).Should().BeFalse();
        }

        // Neither condition alone suffices: a non-sovereign naming a fief they do not even own must still be
        // refused (the AND, not an OR, is the whole point of this gate).
        [Test]
        public void GIVEN_neither_condition_holds_WHEN_asked_to_grant_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanGrant(playerIsKingdomLeader: false, fiefOwnedByPlayerClan: false).Should().BeFalse();
        }

        // The ordinary case: a true sovereign stripping a fief the SPOLIATED vassal's own clan actually holds is legal.
        [Test]
        public void GIVEN_a_sovereign_and_a_vassal_owned_fief_WHEN_asked_to_revoke_THEN_it_is_legal()
        {
            FiefTransferPolicy.CanRevoke(playerIsKingdomLeader: true, fiefOwnedBySpoliatedClan: true).Should().BeTrue();
        }

        // Gabriel's own condition (COUNCIL_ACTIONS.md): "uniquement si on est le chef du clan", corrected by the
        // Kimi review to the actual kingdom sovereign, since a clan leader has no fief to reclaim from a
        // clanmate (their clan already owns it). Without true sovereignty, a "royal" confiscation is a hollow
        // threat the lift must refuse, never a consequence-free tyranny button.
        [Test]
        public void GIVEN_the_player_is_not_the_kingdom_sovereign_WHEN_asked_to_revoke_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanRevoke(playerIsKingdomLeader: false, fiefOwnedBySpoliatedClan: true).Should().BeFalse();
        }

        // The named fief must be a REAL holding of the spoliated vassal's own clan right now: naming a fief they
        // never held (or already lost) would confiscate nothing, a punishment the table never actually paid.
        [Test]
        public void GIVEN_the_named_fief_is_not_the_vassals_own_WHEN_asked_to_revoke_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanRevoke(playerIsKingdomLeader: true, fiefOwnedBySpoliatedClan: false).Should().BeFalse();
        }

        // Neither condition alone suffices: a non-sovereign naming a fief the vassal does not even hold must
        // still be refused.
        [Test]
        public void GIVEN_neither_condition_holds_WHEN_asked_to_revoke_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanRevoke(playerIsKingdomLeader: false, fiefOwnedBySpoliatedClan: false).Should().BeFalse();
        }

        // The relation hit and grudge magnitude are documented, testable constants (COUNCIL_ACTIONS.md's Kimi
        // review: -40 to -60 relation, maximum grudge intensity), not magic numbers buried in the lift executor:
        // pin them here so a future tuning change is a one-line, reviewed decision, not a silent drift.
        [Test]
        public void GIVEN_the_revoke_tuning_constants_WHEN_read_THEN_they_match_the_documented_design()
        {
            FiefTransferPolicy.RevokeRelationHit.Should().BeInRange(-60, -40);
            FiefTransferPolicy.RevokeGrudgeMagnitude.Should().Be(100);
        }

        // Same discipline for grant_fief's own cost: Gabriel's "on ne veut pas que ce soit facile" plus the
        // Kimi review's 100-300 range, pinned here so the figure is documented and a future tuning change is a
        // one-line, reviewed decision rather than a silent drift in the lift executor.
        [Test]
        public void GIVEN_the_grant_influence_cost_WHEN_read_THEN_it_matches_the_documented_design()
        {
            FiefTransferPolicy.GrantInfluenceCost.Should().BeInRange(100, 300);
        }

        // swap_fiefs (Partie 8, COUNCIL_ACTIONS.md's Kimi review): the ordinary case, a true sovereign trading a
        // fief their own clan holds for one the seated vassal's own clan holds, is legal. This is the literal AND
        // of CanGrant's and CanRevoke's own two conditions: the stake is that a swap must never be EASIER to
        // strike than either half alone, or the crown could trade land under a laxer rule than granting or
        // revoking it outright would allow.
        [Test]
        public void GIVEN_a_sovereign_and_both_fiefs_owned_as_expected_WHEN_asked_to_swap_THEN_it_is_legal()
        {
            FiefTransferPolicy.CanSwap(playerIsKingdomLeader: true, playerFiefOwnedByPlayerClan: true, vassalFiefOwnedBySeatedVassalClan: true)
                .Should().BeTrue();
        }

        // Without true kingdom sovereignty, a swap moves crown land exactly like a grant or revoke would, so it
        // must be refused the same way, whatever the two fiefs are owned by.
        [Test]
        public void GIVEN_the_player_is_not_the_kingdom_sovereign_WHEN_asked_to_swap_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanSwap(playerIsKingdomLeader: false, playerFiefOwnedByPlayerClan: true, vassalFiefOwnedBySeatedVassalClan: true)
                .Should().BeFalse();
        }

        // The player's own half of the trade must be real: naming a fief the player's clan does not actually
        // hold would give away nothing while still taking the vassal's own fief in return, an unbalanced non-swap.
        [Test]
        public void GIVEN_the_named_player_fief_is_not_the_players_own_WHEN_asked_to_swap_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanSwap(playerIsKingdomLeader: true, playerFiefOwnedByPlayerClan: false, vassalFiefOwnedBySeatedVassalClan: true)
                .Should().BeFalse();
        }

        // The vassal's own half of the trade must be equally real: naming a fief the seated vassal does not hold
        // would take the player's own fief for nothing in return, the symmetric unbalanced non-swap.
        [Test]
        public void GIVEN_the_named_vassal_fief_is_not_the_vassals_own_WHEN_asked_to_swap_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanSwap(playerIsKingdomLeader: true, playerFiefOwnedByPlayerClan: true, vassalFiefOwnedBySeatedVassalClan: false)
                .Should().BeFalse();
        }

        // No single condition alone suffices: any two-of-three (or fewer) must still refuse the whole trade,
        // never partially. This is the pure gate's own half of the atomicity guarantee CouncilLift.SettleSwapFiefs
        // relies on: the AND, not an OR, is the whole point.
        [Test]
        public void GIVEN_any_single_condition_is_missing_WHEN_asked_to_swap_THEN_it_is_refused()
        {
            FiefTransferPolicy.CanSwap(playerIsKingdomLeader: false, playerFiefOwnedByPlayerClan: false, vassalFiefOwnedBySeatedVassalClan: false)
                .Should().BeFalse();
        }
    }
}
