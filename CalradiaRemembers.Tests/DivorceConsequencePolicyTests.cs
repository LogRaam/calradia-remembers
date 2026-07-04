// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceConsequencePolicyTests
   {
      [Test]
      public void GIVEN_a_blessed_valued_non_mutual_split_WHEN_asking_whether_a_clan_would_grudge_THEN_it_is_true()
      {
         DivorceConsequencePolicy.ClanWouldGrudge(true, true, false).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_mutual_blessed_divorce_both_clans_valuing_the_match_WHEN_resolving_THEN_everything_softens()
      {
         DivorceFacts facts = RepudiatedBlessedBothValued();
         facts.MutualConsent = true;

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.SpouseClanReaction.Kind.Should().Be(ReactionKind.Relief);
         result.DivorcerClanReaction.Kind.Should().Be(ReactionKind.Relief);
         result.UnblessedFamilyReaction.Kind.Should().Be(ReactionKind.None);
      }

      [Test]
      public void GIVEN_a_mutual_consent_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceFacts facts = RepudiatedBlessedBothValued();
         facts.MutualConsent = true;

         DivorceConsequencePolicy.SpouseGrudges(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_mutual_divorce_from_an_unblessed_marriage_WHEN_resolving_THEN_everyone_is_relieved_or_grateful()
      {
         var facts = new DivorceFacts {
            MutualConsent = true,
            MarriageWasBlessed = false,
            SpouseRegardForPlayer = 0,
            SpouseClanValuedAlliance = false,
            DivorcerClanValuedAlliance = false
         };

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.SpouseClanReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.DivorcerClanReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.UnblessedFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);

         // Minimal blame: nobody in this grid grudges the player.
         result.SpouseReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.SpouseClanReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.DivorcerClanReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
      }

      [Test]
      public void GIVEN_a_none_reaction_WHEN_inspecting_it_THEN_both_the_magnitude_and_the_relation_nudge_are_zero()
      {
         PartyReaction none = DivorceConsequencePolicy.Resolve(RepudiatedBlessedBothValued()).UnblessedFamilyReaction;

         none.Kind.Should().Be(ReactionKind.None);
         none.GrudgeMagnitude.Should().Be(0);
         none.RelationNudge.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_plant_grudge_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_zero_and_the_magnitude_is_positive()
      {
         PartyReaction reaction = DivorceConsequencePolicy.Resolve(RepudiatedBlessedBothValued()).SpouseReaction;

         reaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         reaction.GrudgeMagnitude.Should().BeGreaterThan(0);
         reaction.RelationNudge.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_relief_or_gratitude_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_positive_and_the_grudge_magnitude_is_zero()
      {
         DivorceFacts facts = RepudiatedBlessedBothValued();
         facts.MutualConsent = true;

         PartyReaction relief = DivorceConsequencePolicy.Resolve(facts).SpouseReaction;
         relief.Kind.Should().Be(ReactionKind.Relief);
         relief.RelationNudge.Should().BeGreaterThan(0);
         relief.GrudgeMagnitude.Should().Be(0);

         facts.MarriageWasBlessed = false;
         PartyReaction gratitude = DivorceConsequencePolicy.Resolve(facts).UnblessedFamilyReaction;
         gratitude.Kind.Should().Be(ReactionKind.Gratitude);
         gratitude.RelationNudge.Should().BeGreaterThan(0);
         gratitude.GrudgeMagnitude.Should().Be(0);
      }

      // ── The full grid, one representative case each ─────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_from_a_blessed_marriage_both_clans_valuing_it_WHEN_resolving_THEN_it_is_maximum_outrage()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessedBothValued());

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.SpouseClanReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DivorcerClanReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.UnblessedFamilyReaction.Kind.Should().Be(ReactionKind.None);
      }

      [Test]
      public void GIVEN_a_repudiated_spouse_from_an_unblessed_marriage_WHEN_resolving_THEN_the_spouse_grudges_but_the_unblessed_family_is_grateful()
      {
         var facts = new DivorceFacts {
            MutualConsent = false,
            MarriageWasBlessed = false,
            SpouseRegardForPlayer = 0,
            SpouseClanValuedAlliance = false,
            DivorcerClanValuedAlliance = false
         };

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.UnblessedFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.SpouseClanReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.DivorcerClanReaction.Kind.Should().Be(ReactionKind.Gratitude);
      }

      // ── Helper predicates ────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_true()
      {
         DivorceConsequencePolicy.SpouseGrudges(RepudiatedBlessedBothValued()).Should().BeTrue();
      }

      // ── Shape guarantees per reaction kind ──────────────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_resolving_THEN_the_grudge_is_known_to_the_player()
      {
         DivorceConsequencePolicy.Resolve(RepudiatedBlessedBothValued()).SpouseReaction.KnownToPlayer.Should().BeTrue();
      }

      // ── Spouse regard scaling the grudge magnitude ──────────────────────

      [Test]
      public void GIVEN_a_repudiated_spouse_who_already_disliked_the_player_WHEN_resolving_THEN_the_grudge_is_larger()
      {
         DivorceFacts lowRegard = RepudiatedBlessedBothValued();
         lowRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold;

         DivorceFacts highRegard = RepudiatedBlessedBothValued();
         highRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold + 1;

         int lowRegardMagnitude = DivorceConsequencePolicy.Resolve(lowRegard).SpouseReaction.GrudgeMagnitude;
         int highRegardMagnitude = DivorceConsequencePolicy.Resolve(highRegard).SpouseReaction.GrudgeMagnitude;

         lowRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude + DivorceConsequencePolicy.SpouseGrudgeLowRegardBonus);
         highRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude);
         lowRegardMagnitude.Should().BeGreaterThan(highRegardMagnitude);
      }

      [Test]
      public void GIVEN_an_unblessed_marriage_WHEN_asking_whether_a_clan_would_grudge_THEN_it_is_false()
      {
         DivorceConsequencePolicy.ClanWouldGrudge(false, true, false).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_unblessed_marriage_WHEN_resolving_THEN_the_clan_is_grateful_regardless_of_whether_it_valued_the_alliance()
      {
         // MarriageWasBlessed is the deciding fact for the unblessed branch; a "valued the alliance" flag
         // that was never blessed in the first place still reads as a match the clan is glad to see end.
         var facts = new DivorceFacts {
            MutualConsent = false,
            MarriageWasBlessed = false,
            SpouseRegardForPlayer = 0,
            SpouseClanValuedAlliance = true,
            DivorcerClanValuedAlliance = true
         };

         DivorceConsequencePolicy.Resolve(facts).SpouseClanReaction.Kind.Should().Be(ReactionKind.Gratitude);
      }

      [Test]
      public void GIVEN_mutual_consent_WHEN_asking_whether_a_clan_would_grudge_THEN_it_is_softened_to_false()
      {
         DivorceConsequencePolicy.ClanWouldGrudge(true, true, true).Should().BeFalse();
      }

      [Test]
      public void GIVEN_null_facts_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceConsequencePolicy.SpouseGrudges(null).Should().BeFalse();
      }

      // ── Null guard ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_null_facts_WHEN_resolving_THEN_it_throws()
      {
         Assert.Throws<ArgumentNullException>(() => DivorceConsequencePolicy.Resolve(null));
      }

      // ── Clan valued-alliance vs disapproving clan ───────────────────────

      [Test]
      public void GIVEN_one_clan_valued_the_alliance_and_another_did_not_WHEN_resolving_THEN_one_grudges_and_the_other_feels_relief()
      {
         DivorceFacts facts = RepudiatedBlessedBothValued();
         facts.SpouseClanValuedAlliance = true;
         facts.DivorcerClanValuedAlliance = false;

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseClanReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DivorcerClanReaction.Kind.Should().Be(ReactionKind.Relief);
      }

      #region private

      private static DivorceFacts RepudiatedBlessedBothValued()
         => new() {
            MutualConsent = false,
            MarriageWasBlessed = true,
            SpouseRegardForPlayer = 0,
            SpouseClanValuedAlliance = true,
            DivorcerClanValuedAlliance = true
         };

      #endregion
   }
}