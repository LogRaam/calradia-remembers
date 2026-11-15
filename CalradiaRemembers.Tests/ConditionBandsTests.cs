// Code written by Gabriel Mailhot, 14/11/2026.
// ConditionBands says, on the engine's own scales, how a seat and a company are faring: the words a steward and a
// quartermaster speak in, read by the SDK's SeatStandingFacts and PartyStandingFacts into the prompt. Moved out of the
// engine glue unchanged (Gabriel, 14/11/2026, the glue-decisions candidates). If a mark moves: a governor calls a
// content town rebellious, a quartermaster calls a well-fed company starving, or the companion's mission report and the
// steward disagree on whether the same town is settled (the 50 they share was written twice until this move).

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conditions;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class ConditionBandsTests
   {
      // STAKE: below 25 is where vanilla itself starts worrying; 50 splits the restless half from the content one.
      [TestCase(24.99f, LoyaltyBand.Rebellious)]
      [TestCase(25f, LoyaltyBand.Restless)]
      [TestCase(49.99f, LoyaltyBand.Restless)]
      [TestCase(50f, LoyaltyBand.Content)]
      [TestCase(74.99f, LoyaltyBand.Content)]
      [TestCase(75f, LoyaltyBand.Devoted)]
      public void GIVEN_a_towns_loyalty_WHEN_banded_THEN_the_steward_names_it_as_before(float loyalty, LoyaltyBand band)
      {
         ConditionBands.Loyalty(loyalty).Should().Be(band);
      }

      // STAKE: the companion's mission report and the steward read one town the same way: settled exactly when content or devoted.
      [TestCase(49.99f, false)]
      [TestCase(50f, true)]
      [TestCase(90f, true)]
      [TestCase(10f, false)]
      public void GIVEN_a_towns_loyalty_WHEN_asked_if_settled_THEN_it_agrees_with_the_stewards_band(float loyalty, bool settled)
      {
         ConditionBands.IsSettled(ConditionBands.Loyalty(loyalty)).Should().Be(settled);
      }

      // STAKE: an unknown loyalty (no town) is never called settled.
      [Test]
      public void GIVEN_an_unknown_loyalty_WHEN_asked_if_settled_THEN_it_is_not()
      {
         ConditionBands.IsSettled(LoyaltyBand.Unknown).Should().BeFalse();
      }

      // STAKE: low security IS banditry on the roads.
      [TestCase(24.99f, SecurityBand.Lawless)]
      [TestCase(25f, SecurityBand.Uneasy)]
      [TestCase(59.99f, SecurityBand.Uneasy)]
      [TestCase(60f, SecurityBand.Settled)]
      public void GIVEN_a_seats_security_WHEN_banded_THEN_it_reads_as_before(float security, SecurityBand band)
      {
         ConditionBands.Security(security).Should().Be(band);
      }

      // STAKE: which way the food is going decides what a steward does; a half-unit wobble is steady.
      [TestCase(-0.51f, FoodTrend.Falling)]
      [TestCase(-0.5f, FoodTrend.Steady)]
      [TestCase(0.5f, FoodTrend.Steady)]
      [TestCase(0.51f, FoodTrend.Rising)]
      public void GIVEN_a_daily_food_change_WHEN_banded_THEN_the_trend_reads_as_before(float change, FoodTrend trend)
      {
         ConditionBands.FoodDirection(change).Should().Be(trend);
      }

      // STAKE: the granary against the engine's own ceiling.
      [TestCase(0.09f, FoodBand.Empty)]
      [TestCase(0.10f, FoodBand.Thin)]
      [TestCase(0.35f, FoodBand.Adequate)]
      [TestCase(0.75f, FoodBand.Full)]
      public void GIVEN_a_granarys_share_WHEN_banded_THEN_it_reads_as_before(float share, FoodBand band)
      {
         ConditionBands.FoodStores(share).Should().Be(band);
      }

      // STAKE: on the engine's 0-100 morale, where 50 is the settled middle it drifts back towards.
      [TestCase(24.99f, CampMood.Mutinous)]
      [TestCase(25f, CampMood.Sullen)]
      [TestCase(45f, CampMood.Steady)]
      [TestCase(70f, CampMood.Steady)]
      [TestCase(70.01f, CampMood.HighHearted)]
      public void GIVEN_a_companys_morale_WHEN_banded_THEN_the_camp_mood_reads_as_before(float morale, CampMood mood)
      {
         ConditionBands.Mood(morale).Should().Be(mood);
      }

      // STAKE: a quartermaster says "a week or so", not "eleven"; no food left is starving.
      [TestCase(0, ProvisionBand.Starving)]
      [TestCase(1, ProvisionBand.Short)]
      [TestCase(6, ProvisionBand.Short)]
      [TestCase(7, ProvisionBand.Sufficient)]
      [TestCase(20, ProvisionBand.Sufficient)]
      [TestCase(21, ProvisionBand.Plentiful)]
      public void GIVEN_days_of_food_WHEN_banded_THEN_the_quartermaster_reads_as_before(int days, ProvisionBand band)
      {
         ConditionBands.Provisions(days).Should().Be(band);
      }

      // STAKE: the roster's own wounded count, banded.
      [TestCase(0.20f, WoundedBand.Few)]
      [TestCase(0.21f, WoundedBand.Many)]
      [TestCase(0.50f, WoundedBand.Many)]
      [TestCase(0.51f, WoundedBand.Most)]
      public void GIVEN_a_wounded_share_WHEN_banded_THEN_it_reads_as_before(float share, WoundedBand band)
      {
         ConditionBands.Wounded(share).Should().Be(band);
      }

      // STAKE: a bandit captor may suspect a valuable, ransomable noble in a house of landed nobility or one with real sway.
      [Test]
      public void GIVEN_the_players_house_WHEN_a_captor_judges_it_THEN_landed_nobility_or_real_sway_looks_important()
      {
         PlayerImportancePolicy.LooksImportant(new Stature(Stature.LandedNobleTier, false), 0f).Should().BeTrue();
         PlayerImportancePolicy.LooksImportant(new Stature(1, false), 150f).Should().BeTrue();
         PlayerImportancePolicy.LooksImportant(new Stature(1, false), 149.9f).Should().BeFalse();
      }

      // STAKE: when the game cannot be asked, the coming of age is the engine's own default, the same everywhere.
      [Test]
      public void GIVEN_no_age_model_WHEN_the_coming_of_age_is_read_THEN_it_is_the_engines_default()
      {
         ComingOfAge.YearsOr(null).Should().Be(18);
         ComingOfAge.YearsOr(16).Should().Be(16);
      }

      // STAKE: a great house or a ruler keeps face in public; a lesser lord does not (the station-2 mark, now named).
      [Test]
      public void GIVEN_a_station_WHEN_asked_if_great_house_or_ruler_THEN_station_two_and_above_answer_yes()
      {
         new Stature(Stature.GreatHouseTier, false).IsGreatHouseOrRuler.Should().BeTrue();
         new Stature(1, true).IsGreatHouseOrRuler.Should().BeTrue();
         new Stature(Stature.LandedNobleTier, false).IsGreatHouseOrRuler.Should().BeFalse();
      }
   }
}
