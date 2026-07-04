// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceConsequencePolicyTests
   {
      [Test]
      public void GIVEN_a_mutual_consent_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         DivorceConsequencePolicy.SpouseGrudges(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_mutual_divorce_from_a_blessed_marriage_WHEN_resolving_THEN_the_spouse_is_relieved_and_family_and_friends_feel_nothing()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.None);
         result.FriendReaction.Kind.Should().Be(ReactionKind.None);
      }

      [Test]
      public void GIVEN_a_mutual_divorce_from_an_unblessed_marriage_WHEN_resolving_THEN_the_family_is_grateful_and_nobody_grudges()
      {
         var facts = new DivorceFacts {MutualConsent = true, MarriageWasBlessed = false, SpouseRegardForPlayer = 0};

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.FriendReaction.Kind.Should().Be(ReactionKind.None);

         // Minimal blame: nobody in this grid grudges the player.
         result.SpouseReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.FriendReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
      }

      [Test]
      public void GIVEN_a_none_reaction_WHEN_inspecting_it_THEN_both_the_magnitude_and_the_relation_nudge_are_zero()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         PartyReaction none = DivorceConsequencePolicy.Resolve(facts).FriendReaction;

         none.Kind.Should().Be(ReactionKind.None);
         none.GrudgeMagnitude.Should().Be(0);
         none.RelationNudge.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_plant_grudge_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_zero_and_the_magnitude_is_positive()
      {
         PartyReaction reaction = DivorceConsequencePolicy.Resolve(RepudiatedBlessed()).SpouseReaction;

         reaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         reaction.GrudgeMagnitude.Should().BeGreaterThan(0);
         reaction.RelationNudge.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_relief_or_gratitude_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_positive_and_the_grudge_magnitude_is_zero()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         PartyReaction relief = DivorceConsequencePolicy.Resolve(facts).SpouseReaction;
         relief.Kind.Should().Be(ReactionKind.Relief);
         relief.RelationNudge.Should().BeGreaterThan(0);
         relief.GrudgeMagnitude.Should().Be(0);

         facts.MarriageWasBlessed = false;
         PartyReaction gratitude = DivorceConsequencePolicy.Resolve(facts).DirectFamilyReaction;
         gratitude.Kind.Should().Be(ReactionKind.Gratitude);
         gratitude.RelationNudge.Should().BeGreaterThan(0);
         gratitude.GrudgeMagnitude.Should().Be(0);
      }

      // ── The full grid, one representative case each ─────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_from_a_blessed_marriage_WHEN_resolving_THEN_the_spouse_the_family_and_the_friends_all_grudge()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.FriendReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
      }

      [Test]
      public void GIVEN_a_repudiated_spouse_from_an_unblessed_marriage_WHEN_resolving_THEN_the_spouse_and_friends_grudge_but_the_family_is_grateful()
      {
         var facts = new DivorceFacts {MutualConsent = false, MarriageWasBlessed = false, SpouseRegardForPlayer = 0};

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.FriendReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
      }

      // ── Helper predicates ────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_true()
      {
         DivorceConsequencePolicy.SpouseGrudges(RepudiatedBlessed()).Should().BeTrue();
      }

      // ── Shape guarantees per reaction kind ──────────────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_resolving_THEN_the_spouse_grudge_is_known_to_the_player()
      {
         DivorceConsequencePolicy.Resolve(RepudiatedBlessed()).SpouseReaction.KnownToPlayer.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_resolving_THEN_the_family_grudge_is_known_but_the_friend_grudge_is_hidden()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.DirectFamilyReaction.KnownToPlayer.Should().BeTrue();
         result.FriendReaction.KnownToPlayer.Should().BeFalse();
      }

      // ── Magnitudes clear the grudge-formation floor ─────────────────────

      [Test]
      public void GIVEN_the_repudiated_grid_WHEN_reading_every_grudge_magnitude_THEN_all_clear_the_formation_floor()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.SpouseReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
         result.DirectFamilyReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
         result.FriendReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
      }

      // ── Spouse regard scaling the grudge magnitude ──────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_who_already_disliked_the_player_WHEN_resolving_THEN_the_grudge_is_larger()
      {
         DivorceFacts lowRegard = RepudiatedBlessed();
         lowRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold;

         DivorceFacts highRegard = RepudiatedBlessed();
         highRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold + 1;

         int lowRegardMagnitude = DivorceConsequencePolicy.Resolve(lowRegard).SpouseReaction.GrudgeMagnitude;
         int highRegardMagnitude = DivorceConsequencePolicy.Resolve(highRegard).SpouseReaction.GrudgeMagnitude;

         lowRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude + DivorceConsequencePolicy.SpouseGrudgeLowRegardBonus);
         highRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude);
         lowRegardMagnitude.Should().BeGreaterThan(highRegardMagnitude);
      }

      // ── Null guard ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_null_facts_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceConsequencePolicy.SpouseGrudges(null).Should().BeFalse();
      }

      [Test]
      public void GIVEN_null_facts_WHEN_resolving_THEN_it_throws()
      {
         Assert.Throws<ArgumentNullException>(() => DivorceConsequencePolicy.Resolve(null));
      }

      #region private

      private static DivorceFacts RepudiatedBlessed()
         => new() {MutualConsent = false, MarriageWasBlessed = true, SpouseRegardForPlayer = 0};

      #endregion
   }
}
