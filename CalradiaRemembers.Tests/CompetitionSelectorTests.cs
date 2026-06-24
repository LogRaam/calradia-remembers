// Code written by Gabriel Mailhot, 20/06/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompetitionSelectorTests
   {
      [Test]
      public void GIVEN_a_household_all_recently_seen_WHEN_selecting_THEN_no_one_competes()
      {
         //Arrange — none past the absolute neglect floor.
         var partners = new List<PartnerStanding> {Standing("a", 2), Standing("b", 5)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_neglected_but_monogamous_partner_WHEN_selecting_THEN_they_do_not_merely_compete()
      {
         //Arrange — a monogamous partner is betrayed, not in competition; the selector skips them.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("exclusive", 25, false)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_partner_just_past_the_floor_but_within_the_inequity_gap_WHEN_selecting_THEN_they_do_not_react()
      {
         //Arrange — neglected (>= floor) but only InequityGapDays-1 more than the favoured partner.
         var partners = new List<PartnerStanding> {
            Standing("favoured", CompetitionSelector.NeglectThresholdDays - 1, false),
            Standing("close", CompetitionSelector.NeglectThresholdDays - 1 + CompetitionSelector.InequityGapDays - 1)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      [Test]
      public void GIVEN_everyone_neglected_evenly_WHEN_selecting_THEN_no_one_feels_singled_out()
      {
         //Arrange — both past the floor, but the gap between them is below the inequity threshold.
         var partners = new List<PartnerStanding> {Standing("a", 20), Standing("b", 20)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull("an even, if thin, distribution of attention is no slight");
      }

      [Test]
      public void GIVEN_fewer_than_two_partners_WHEN_selecting_THEN_no_one_competes()
      {
         //Arrange
         var partners = new List<PartnerStanding> {Standing("solo", 40)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull("rivalry needs a household of several");
      }

      [Test]
      public void GIVEN_one_partner_neglected_far_more_than_the_favoured_one_WHEN_selecting_THEN_they_compete()
      {
         //Arrange — the player dotes on the anchor, leaving the other to wither.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("slighted", 25)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().Be("slighted");
      }

      [Test]
      public void GIVEN_several_eligible_partners_WHEN_selecting_THEN_the_most_neglected_wins()
      {
         //Arrange
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("aggrieved", 16),
            Standing("most_aggrieved", 25)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().Be("most_aggrieved");
      }

      [Test]
      public void GIVEN_the_only_eligible_partner_is_on_cooldown_WHEN_selecting_THEN_they_are_skipped()
      {
         //Arrange
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("slighted", 25, onCooldown: true)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      #region private

      private static PartnerStanding Standing(string id, int neglect, bool tolerates = true, bool onCooldown = false)
         => new() {
            PartnerId = id,
            ToleratesSharing = tolerates,
            NeglectDays = neglect,
            OnCooldown = onCooldown
         };

      #endregion
   }
}