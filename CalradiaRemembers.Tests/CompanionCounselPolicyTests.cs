// Code written by Gabriel Mailhot, 22/07/2026.
// Protects the PRACTICAL COUNSEL found-topic. The owner's own hard rule governs every test here: "a companion
// advising the player to vary their food when they already carry eight kinds is worse than silence", the
// counsel MUST be true when spoken, computed from live state only. Get any of these branches wrong and the
// mod puts stale or false advice in a companion's mouth, exactly the failure the design rule exists to forbid.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionCounselPolicyTests
   {
      private static PartyCounselFacts WellStockedFacts() => new PartyCounselFacts {
         DistinctFoodKinds = 8, Morale = 80f, WoundedTroops = 0, HasSurgeon = true, CarriedWeight = 10f, CarryCapacity = 100f
      };

      // ── The hard rule itself ─────────────────────────────────────────────────

      // The owner's own worst case, verbatim: a party carrying eight kinds of food must NEVER be counselled to
      // vary its diet. This is the single test that most directly pins the "MUST be true when spoken" rule.
      [Test]
      public void GIVEN_a_well_stocked_party_WHEN_selected_THEN_no_counsel_fires()
         => CompanionCounselPolicy.Select(WellStockedFacts()).Kind.Should().Be(PartyCounselKind.None);

      // ── Food monotony ────────────────────────────────────────────────────────

      // Below the monotony threshold, the counsel is genuinely true: the party really is eating the same thing
      // over and over, which is exactly when Bannerlord's own variety bonus is being left on the table.
      [Test]
      public void GIVEN_food_kinds_below_the_monotony_threshold_WHEN_selected_THEN_food_monotony_fires()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.DistinctFoodKinds = CompanionCounselPolicy.FoodVarietyMonotonyThreshold - 1;

         CompanionCounselPolicy.Select(facts).Kind.Should().Be(PartyCounselKind.FoodMonotony);
      }

      // Carrying NO food at all is a starvation problem, not a monotony one, the counsel must not misdescribe
      // it as "vary your diet" when the real issue is having nothing to eat in the first place.
      [Test]
      public void GIVEN_zero_food_kinds_WHEN_selected_THEN_food_monotony_does_not_fire()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.DistinctFoodKinds = 0;

         CompanionCounselPolicy.Select(facts).Kind.Should().NotBe(PartyCounselKind.FoodMonotony);
      }

      // ── Low morale ───────────────────────────────────────────────────────────

      // Reuses PartyMoraleMood's OWN "sullen camp" bar (LowAt) rather than a second, competing threshold, the
      // mod must never have two different opinions about when the camp's mood counts as low.
      [Test]
      public void GIVEN_morale_at_partymoralemoods_low_bar_WHEN_selected_THEN_low_morale_fires()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.Morale = PartyMoraleMood.LowAt;

         CompanionCounselPolicy.Select(facts).Kind.Should().Be(PartyCounselKind.LowMorale);
      }

      [Test]
      public void GIVEN_morale_just_above_the_low_bar_WHEN_selected_THEN_low_morale_does_not_fire()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.Morale = PartyMoraleMood.LowAt + 1f;

         CompanionCounselPolicy.Select(facts).Kind.Should().NotBe(PartyCounselKind.LowMorale);
      }

      // ── Wounded, no surgeon ──────────────────────────────────────────────────

      // A wounded party with a skilled healer already among them is being tended to, advising the player to
      // find a surgeon they already have would be exactly the kind of false counsel the rule forbids.
      [Test]
      public void GIVEN_wounded_troops_but_a_surgeon_present_WHEN_selected_THEN_wounded_no_surgeon_does_not_fire()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.WoundedTroops = 5;
         facts.HasSurgeon = true;

         CompanionCounselPolicy.Select(facts).Kind.Should().NotBe(PartyCounselKind.WoundedNoSurgeon);
      }

      [Test]
      public void GIVEN_wounded_troops_and_no_surgeon_WHEN_selected_THEN_wounded_no_surgeon_fires()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.WoundedTroops = 5;
         facts.HasSurgeon = false;

         CompanionCounselPolicy.Select(facts).Kind.Should().Be(PartyCounselKind.WoundedNoSurgeon);
      }

      // ── Overloaded ───────────────────────────────────────────────────────────

      // Reuses vanilla's own overloaded state (weight at/above capacity, the speed penalty the player already
      // feels) rather than inventing a new threshold that could disagree with what the game itself considers
      // overloaded.
      [Test]
      public void GIVEN_carried_weight_at_capacity_WHEN_selected_THEN_overloaded_fires()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.CarriedWeight = 100f;
         facts.CarryCapacity = 100f;

         CompanionCounselPolicy.Select(facts).Kind.Should().Be(PartyCounselKind.Overloaded);
      }

      // A capacity of zero or less means the fact could not actually be read, this must never be
      // misinterpreted as "infinitely overloaded".
      [Test]
      public void GIVEN_an_unavailable_capacity_fact_WHEN_selected_THEN_overloaded_never_fires()
      {
         PartyCounselFacts facts = WellStockedFacts();
         facts.CarriedWeight = 500f;
         facts.CarryCapacity = 0f;

         CompanionCounselPolicy.Select(facts).Kind.Should().NotBe(PartyCounselKind.Overloaded);
      }

      // ── Strongest true condition wins ────────────────────────────────────────

      // A party can be low on morale AND overloaded AND food-monotonous at once, but only one counsel can
      // headline a single audience, the strongest true condition must win, exactly like the top-level topic
      // selection it feeds into.
      [Test]
      public void GIVEN_several_conditions_hold_at_once_WHEN_selected_THEN_the_strongest_one_wins()
      {
         PartyCounselFacts facts = new PartyCounselFacts {
            DistinctFoodKinds = 1, // mild monotony
            Morale = 0f, // severe, far below the low bar
            WoundedTroops = 0,
            HasSurgeon = true,
            CarriedWeight = 10f,
            CarryCapacity = 100f
         };

         CompanionCounselPolicy.Select(facts).Kind.Should().Be(PartyCounselKind.LowMorale);
      }

      // ── Null facts ───────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_no_facts_at_all_WHEN_selected_THEN_no_counsel_fires()
         => CompanionCounselPolicy.Select(null).Kind.Should().Be(PartyCounselKind.None);
   }
}
