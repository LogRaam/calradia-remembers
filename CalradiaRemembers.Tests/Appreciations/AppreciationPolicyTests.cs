// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;
using GrudgePolicy = CalradiaRemembers.Logic.Grudges.GrudgePolicy;
using TraitFacts = CalradiaRemembers.Logic.Appreciations.TraitFacts;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
   [TestFixture]
   public sealed class AppreciationPolicyTests
   {
      [Test]
      public void GIVEN_a_full_house_but_some_entries_are_fully_faded_WHEN_admitting_THEN_only_the_live_ones_count_toward_the_cap()
      {
         var mostlyFaded = new List<Appreciation> {
            NewAppreciation(5, 0), // fully decays by day 100000
            NewAppreciation(50, 0)
         };

         bool admitted = AppreciationPolicy.TryAdmit(mostlyFaded, 1, 100000, PlainTraits(), out Appreciation toEvict);

         admitted.Should().BeTrue("only one appreciation (magnitude 50) is still live at that day — well under the cap");
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_full_house_WHEN_the_newcomer_is_no_stronger_than_the_weakest_live_appreciation_THEN_it_is_refused()
      {
         var live = new List<Appreciation> {
            NewAppreciation(90, 0),
            NewAppreciation(50, 0),
            NewAppreciation(20, 0)
         };

         bool admitted = AppreciationPolicy.TryAdmit(live, 20, 0, PlainTraits(), out Appreciation toEvict);

         admitted.Should().BeFalse("an equal-or-weaker newcomer would just add noise, the exact case the cap guards against");
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_full_house_WHEN_the_newcomer_is_strictly_stronger_than_the_weakest_live_appreciation_THEN_it_is_admitted_and_the_weakest_is_named_for_eviction()
      {
         var live = new List<Appreciation> {
            NewAppreciation(90, 0),
            NewAppreciation(50, 0),
            NewAppreciation(20, 0)
         };

         bool admitted = AppreciationPolicy.TryAdmit(live, 21, 0, PlainTraits(), out Appreciation toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().NotBeNull();
         toEvict.Magnitude.Should().Be(20, "the weakest live appreciation yields to a strictly stronger newcomer");
      }

      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var appreciations = new List<Appreciation> {NewAppreciation(5, 0)};

         AppreciationPolicy.SelectDominant(appreciations, 100000, PlainTraits()).Should().BeEmpty();
      }

      [Test]
      public void GIVEN_a_grudge_as_strong_as_the_appreciation_WHEN_tarnishing_THEN_it_roughly_cancels_the_warm_feeling()
      {
         AppreciationPolicy.TarnishedMagnitude(60, 60).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_grudge_far_stronger_than_the_appreciation_WHEN_tarnishing_THEN_it_floors_at_zero_never_negative()
      {
         AppreciationPolicy.TarnishedMagnitude(30, 100).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_null_appreciation_WHEN_reading_effective_magnitude_THEN_it_reads_as_zero()
      {
         AppreciationPolicy.EffectiveMagnitude(null, 100, PlainTraits()).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_null_collection_WHEN_selecting_dominant_THEN_it_returns_empty_not_null()
      {
         AppreciationPolicy.SelectDominant(null, 0, PlainTraits()).Should().NotBeNull().And.BeEmpty();
      }

      [Test]
      public void GIVEN_a_null_live_list_WHEN_admitting_a_newcomer_THEN_it_is_admitted()
      {
         AppreciationPolicy.TryAdmit(null, 50, 0, PlainTraits(), out Appreciation toEvict).Should().BeTrue();
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_softer_active_grudge_WHEN_tarnishing_THEN_the_warmth_is_reduced_but_not_wiped_out()
      {
         int tarnished = AppreciationPolicy.TarnishedMagnitude(60, 20);

         tarnished.Should().BeLessThan(60).And.BeGreaterThan(0);
      }

      [Test]
      public void GIVEN_an_already_faded_appreciation_WHEN_tarnishing_THEN_it_stays_at_zero_regardless_of_the_grudge()
      {
         AppreciationPolicy.TarnishedMagnitude(0, 50).Should().Be(0);
      }

      [Test]
      public void GIVEN_an_appreciation_and_a_grudge_of_the_same_starting_magnitude_WHEN_the_same_time_elapses_THEN_the_appreciation_has_faded_less()
      {
         Appreciation appreciation = NewAppreciation(50, 0);
         var grudge = new Grudge {
            Id = "holder|0|0", HolderId = "holder", TargetId = "player",
            Source = GrudgeSource.FailedQuest,
            CauseSummary = "x", EventDay = 0, Magnitude = 50, Rigidity = 50
         };

         int appreciationToday = AppreciationPolicy.EffectiveMagnitude(appreciation, 30, PlainTraits());
         int grudgeToday = GrudgePolicy.EffectiveMagnitude(grudge, 30, new Logic.Grudges.TraitFacts());

         appreciationToday.Should().BeGreaterThan(grudgeToday, "a kindness of the same starting weight lingers longer than a grievance");
      }

      [Test]
      public void GIVEN_an_unattributable_kindness_WHEN_deciding_formation_THEN_it_refuses_however_large()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: 100, attributable: false)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_enough_elapsed_time_WHEN_reading_effective_magnitude_THEN_it_floors_at_zero_never_negative()
      {
         Appreciation appreciation = NewAppreciation(20, 0);

         AppreciationPolicy.EffectiveMagnitude(appreciation, 100000, PlainTraits()).Should().Be(0);
      }

      // ── Per-holder cap eviction (TryAdmit) ──────────────────────────────

      [Test]
      public void GIVEN_fewer_live_appreciations_than_the_cap_WHEN_admitting_a_newcomer_THEN_it_is_always_admitted_with_nothing_to_evict()
      {
         var live = new List<Appreciation> {NewAppreciation(10, 0)};

         bool admitted = AppreciationPolicy.TryAdmit(live, 5, 0, PlainTraits(), out Appreciation toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_high_calculating_WHEN_deciding_formation_THEN_the_bar_is_raised_and_a_middling_kindness_no_longer_forms()
      {
         var calculatingTraits = new TraitFacts {Calculating = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor, traits: calculatingTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeFalse("a cold ledger-keeper is barely moved by a mere kindness");
      }

      [Test]
      public void GIVEN_high_generosity_WHEN_deciding_formation_THEN_the_bar_is_lowered_and_a_lesser_kindness_still_forms()
      {
         var generousTraits = new TraitFacts {Generosity = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - AppreciationPolicy.GenerosityFormationFloorReliefPerLevel * 2, traits: generousTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeTrue("a warm heart is moved by even a lesser kindness");
      }

      [Test]
      public void GIVEN_high_generosity_WHEN_reading_effective_magnitude_THEN_it_decays_slower_than_a_plain_holder()
      {
         Appreciation appreciation = NewAppreciation(50, 0);
         var generous = new TraitFacts {Generosity = 2};

         int plainDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, PlainTraits());
         int generousDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, generous);

         generousDecayed.Should().BeGreaterThan(plainDecayed);
      }

      [Test]
      public void GIVEN_high_mercy_WHEN_deciding_formation_THEN_the_bar_is_also_lowered()
      {
         var mercifulTraits = new TraitFacts {Mercy = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - AppreciationPolicy.MercyFormationFloorReliefPerLevel * 2, traits: mercifulTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_high_mercy_WHEN_reading_effective_magnitude_THEN_it_also_decays_slower()
      {
         Appreciation appreciation = NewAppreciation(50, 0);
         var merciful = new TraitFacts {Mercy = 2};

         int plainDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, PlainTraits());
         int mercifulDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, merciful);

         mercifulDecayed.Should().BeGreaterThan(plainDecayed);
      }

      [Test]
      public void GIVEN_magnitude_at_the_floor_WHEN_deciding_formation_THEN_it_forms()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: AppreciationPolicy.MagnitudeFloor)).Should().BeTrue();
      }

      // ── Formation ────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_magnitude_below_the_floor_WHEN_deciding_formation_THEN_it_refuses()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - 1)).Should().BeFalse();
      }

      // ── Per-NPC cap ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_more_live_appreciations_than_the_cap_WHEN_selecting_dominant_THEN_only_the_top_ones_surface()
      {
         var appreciations = new List<Appreciation> {
            NewAppreciation(90, 0),
            NewAppreciation(80, 0),
            NewAppreciation(70, 0),
            NewAppreciation(60, 0),
            NewAppreciation(50, 0)
         };

         List<Appreciation> dominant = AppreciationPolicy.SelectDominant(appreciations, 0, PlainTraits()).ToList();

         dominant.Should().HaveCount(AppreciationPolicy.PerNpcCap);
         dominant.Select(a => a.Magnitude).Should().BeInDescendingOrder();
         dominant.Select(a => a.Magnitude).Should().Contain(new[] {90, 80, 70});
      }

      // ── The cross-interaction payoff: TarnishedMagnitude ────────────────

      [Test]
      public void GIVEN_no_active_grudge_WHEN_tarnishing_THEN_the_appreciation_keeps_its_full_warmth()
      {
         AppreciationPolicy.TarnishedMagnitude(60, 0).Should().Be(60);
      }

      [Test]
      public void GIVEN_no_time_has_passed_WHEN_reading_effective_magnitude_THEN_it_reads_as_the_stored_magnitude()
      {
         Appreciation appreciation = NewAppreciation(42, 500);

         AppreciationPolicy.EffectiveMagnitude(appreciation, 500, PlainTraits()).Should().Be(42);
      }

      [Test]
      public void GIVEN_null_facts_WHEN_deciding_formation_THEN_it_refuses()
      {
         AppreciationPolicy.ShouldForm(null).Should().BeFalse();
      }

      // ── Decay: locked SLOWER than the grudge base rate ──────────────────

      [Test]
      public void GIVEN_the_baseline_decay_rate_WHEN_compared_to_the_grudge_base_rate_THEN_it_is_exactly_half()
      {
         // Locks the ROADMAP's "kindness lingers" framing: an appreciation fades at half the speed of a
         // grudge of the same starting magnitude. If either constant moves, this test flags it.
         AppreciationPolicy.BaseDecayPerDay.Should().Be(GrudgePolicy.BaseDecayPerDay / 2.0);
      }

      [Test]
      public void GIVEN_time_passing_WHEN_reading_effective_magnitude_THEN_it_softens()
      {
         Appreciation appreciation = NewAppreciation();

         int today = AppreciationPolicy.EffectiveMagnitude(appreciation, 100, PlainTraits());
         int muchLater = AppreciationPolicy.EffectiveMagnitude(appreciation, 200, PlainTraits());

         muchLater.Should().BeLessThan(today);
      }

      #region private

      private static AppreciationCandidateFacts Candidate(
         AppreciationSource source = AppreciationSource.QuestDoneForThem,
         int magnitude = 20,
         bool attributable = true,
         TraitFacts? traits = null)
         => new() {
            Source = source,
            Magnitude = magnitude,
            IsAttributable = attributable,
            Traits = traits ?? PlainTraits()
         };

      private static Appreciation NewAppreciation(int magnitude = 50, int eventDay = 100)
         => new() {
            Id = "holder|3|100",
            HolderId = "holder",
            TargetId = "player",
            Source = AppreciationSource.QuestDoneForThem,
            CauseSummary = "a debt repaid in kind",
            EventDay = eventDay,
            Magnitude = magnitude
         };

      private static TraitFacts PlainTraits() => new();

      #endregion
   }
}