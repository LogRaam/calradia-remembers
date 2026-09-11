// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the intimacy-as-currency eligibility gate (Negotiation Phase 3) — strict, one-directional,
// archetype-filtered, adult-gated. No content here, only who may raise it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class IntimacyCurrencyPolicyTests
   {
      // A cynical, unrelated man toward a female player at an explicit tier — the only "yes".
      private static bool Eligible(
         bool playerIsFemale = true, bool npcIsMale = true, bool kin = false,
         int honor = -1, int calculating = 1, int mercy = -1, bool adult = true)
         => IntimacyCurrencyPolicy.IsEligible(new IntimacyCurrencyPolicy.LeverageArchetype {PlayerIsFemale = playerIsFemale, NpcIsMale = npcIsMale, NpcIsPlayerKin = kin, NpcHonor = honor, NpcCalculating = calculating, NpcMercy = mercy, AdultExplicitOrHigher = adult});

      // The sole positive case in the whole gate: if this ever reads false, the feature has no eligible
      // path left and can never engage even the one archetype it was built for.
      [Test]
      public void GIVEN_a_cynical_unrelated_man_toward_a_female_player_at_an_explicit_tier_WHEN_asked_THEN_eligible()
      {
         //Arrange/Act/Assert
         Eligible().Should().BeTrue();
      }

      // The dynamic is deliberately one-directional (toward a female player only); flipping this would
      // expose a male player to a mechanic explicitly scoped away from him.
      [Test]
      public void GIVEN_a_male_player_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert — one-directional.
         Eligible(playerIsFemale: false).Should().BeFalse();
      }

      // Only a man propositions here by design; a female NPC must never be cast into an archetype defined
      // as an exploitative man toward a female player.
      [Test]
      public void GIVEN_a_woman_npc_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert
         Eligible(npcIsMale: false).Should().BeFalse();
      }

      // Kin are excluded outright, ahead of any trait match; without this a clan relative could be routed
      // into the exploiter archetype the design explicitly rules out.
      [Test]
      public void GIVEN_the_npc_is_the_players_kin_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert
         Eligible(kin: true).Should().BeFalse();
      }

      // The adult tier is the master switch for this whole dynamic; a leak below Explicit would surface
      // sexual-leverage content to players who opted into a lower content tier.
      [Test]
      public void GIVEN_a_tier_below_explicit_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert — the hard adult gate.
         Eligible(adult: false).Should().BeFalse();
      }

      // An honourable man does not exploit this way (production doc); getting this wrong would let a
      // virtuous NPC's own trait profile be overridden into acting out of character.
      [Test]
      public void GIVEN_an_honourable_man_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert — an honourable man does not exploit this way.
         Eligible(honor: 1).Should().BeFalse();
      }

      // Low honour alone is not the archetype: it also needs scheming or callousness. Without this,
      // ordinary dishonourable-but-otherwise-decent NPCs would wrongly qualify.
      [Test]
      public void GIVEN_a_dishonourable_but_neither_scheming_nor_callous_man_WHEN_asked_THEN_not_eligible()
      {
         //Arrange/Act/Assert — low honour alone is not the archetype.
         Eligible(honor: 0, calculating: 0, mercy: 1).Should().BeFalse();
      }

      // Confirms "scheming or callous" is really an OR: a merciless-but-not-conniving man still qualifies.
      // Getting this wrong would narrow the archetype down to master schemers only.
      [Test]
      public void GIVEN_a_dishonourable_callous_man_WHEN_asked_THEN_eligible_even_without_scheming()
      {
         //Arrange/Act/Assert — callousness alone (with low honour) qualifies.
         Eligible(honor: 0, calculating: 0, mercy: -1).Should().BeTrue();
      }
   }
}
