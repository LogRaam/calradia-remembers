// Code written by Gabriel Mailhot, 05/08/2026.
// join_as_vassal fix (player report, 2026-08-05): "asked the legitimate ruler to become her vassal, the LLM
// narrated acceptance and threw 6000 denars, but the game only ever registered the player as a mercenary."
// Verifies the two guards the bridge consults before swearing the player's clan into vassalage, so a
// narrated oath must actually change the player's kingdom standing from mercenary/independent to full
// vassal, or be surfaced as refused - never a silent no-op that leaves the fiction and the save diverged.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class VassalageOfferPolicyTests
    {
        // The ordinary happy path this whole feature exists for: the kingdom's actual ruler agrees, in
        // conversation, and the player's clan owes no prior vassal oath. Without this, the fix would have no
        // reachable success case at all. This same input also covers a clan CURRENTLY serving as a mercenary
        // (paid contract, not an oath, so playerAlreadyVassal is false for it too): ApplyByJoinToKingdom ends
        // that contract internally on its own (decompile-confirmed), so mercenary-to-vassal is a single
        // legitimate step, not a state this gate should block.
        [Test]
        public void GIVEN_the_npc_is_the_kingdom_ruler_and_the_player_is_not_already_a_vassal_WHEN_resolving_join_as_vassal_THEN_the_oath_is_allowed()
        {
            VassalageOfferPolicy.CanJoin(npcIsKingdomRuler: true, playerAlreadyVassal: false, playerAtWarWithKingdom: false).Should().BeTrue();
        }

        // STAKE: the player report was a narrated acceptance ("the LLM narrated acceptance and threw 6000
        // denars") that never touched the save - an empty promise. join_as_vassal must never repeat that
        // silent gap: only the kingdom's actual ruler can grant vassalage (a lord "speaking with authority"
        // is not enough, unlike end_mercenary's looser rule), so any other lord's word must be refused, not
        // silently swallowed.
        [Test]
        public void GIVEN_the_npc_is_not_the_kingdom_ruler_WHEN_resolving_join_as_vassal_THEN_the_oath_is_refused()
        {
            VassalageOfferPolicy.CanJoin(npcIsKingdomRuler: false, playerAlreadyVassal: false, playerAtWarWithKingdom: false).Should().BeFalse();
        }

        // STAKE: a clan already sworn to a kingdom cannot be re-sworn by a passing conversation with another
        // ruler (or the same one) - that would silently overwrite a real standing (fiefs, obligations) the
        // player already holds. This pins the boundary the "Settle that obligation first" refusal message
        // depends on.
        [Test]
        public void GIVEN_the_player_is_already_a_full_vassal_WHEN_resolving_join_as_vassal_THEN_the_oath_is_refused()
        {
            VassalageOfferPolicy.CanJoin(npcIsKingdomRuler: true, playerAlreadyVassal: true, playerAtWarWithKingdom: false).Should().BeFalse();
        }

        // STAKE (v1.34.2 governance hardening, finding 4): join_as_vassal never checked whether the player was AT
        // WAR with the kingdom being joined, so a player actively fighting a realm could swear fealty to it, a
        // state the fiction can never justify. Even the actual ruler, offered to a free clan, must be refused
        // while the war stands, or the executor would seal an oath to the enemy the player is at war with.
        [Test]
        public void GIVEN_the_player_is_at_war_with_the_kingdom_WHEN_resolving_join_as_vassal_THEN_the_oath_is_refused()
        {
            VassalageOfferPolicy.CanJoin(npcIsKingdomRuler: true, playerAlreadyVassal: false, playerAtWarWithKingdom: true).Should().BeFalse();
        }

        // Both guards failing at once must still refuse, not throw or default to true - belt-and-suspenders
        // on the boolean AND the gate compiles to.
        [Test]
        public void GIVEN_the_npc_is_not_the_ruler_and_the_player_is_already_a_vassal_WHEN_resolving_join_as_vassal_THEN_the_oath_is_refused()
        {
            VassalageOfferPolicy.CanJoin(npcIsKingdomRuler: false, playerAlreadyVassal: true, playerAtWarWithKingdom: false).Should().BeFalse();
        }
    }
}
