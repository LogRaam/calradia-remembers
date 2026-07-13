// Code written by Gabriel Mailhot, 04/07/2026.
// This policy decides three things a chat turn and an NPC's encyclopedia page both depend on: whether a
// kindness is worth remembering at all (anti-noise floor), how much warmth survives to be voiced today
// (decay, mirrored to a grudge's own decay so a kindness lingers longer), and which few memories actually
// surface when several compete (per-NPC cap) or when a fresh betrayal sours them (TarnishedMagnitude). Get
// the floor wrong and every trivial courtesy clutters the "Kindnesses" encyclopedia section and the LLM
// prompt with noise; get the cap or the tarnish math wrong and an NPC's felt regard stops tracking what the
// player actually did, which is the whole premise the CHANGELOG calls "how NPCs feel about you, made
// coherent."

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
      // A stale, fully-decayed entry must not squat a cap slot forever: TryAdmit only counts entries with
      // positive EFFECTIVE magnitude as "live". Without this, one ancient kindness could permanently block
      // every later one from ever being admitted.
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

      // Guards the point of the per-NPC cap (PerNpcCap = 3, "never a bookkeeper's ledger" per the mirrored
      // GrudgePolicy header): a merely-equal newcomer must not evict a stronger standing kindness just for
      // being newer, or the surfaced set would churn instead of tracking what matters most.
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

      // Pins the STRICT inequality (newcomerMagnitude > weakest, not >=) that decides the cap's tie
      // behaviour: a genuinely stronger newcomer must still get in and replace the weakest live entry, or
      // the cap would ossify around whatever formed first.
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

      // SelectDominant feeds both the prompt and the "Kindnesses" encyclopedia section directly. A fully
      // decayed entry must never be named as if it were live warmth: the mod's promise is that kindness
      // eventually fades to nothing, not that it lingers forever once formed.
      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var appreciations = new List<Appreciation> {NewAppreciation(5, 0)};

         AppreciationPolicy.SelectDominant(appreciations, 100000, PlainTraits()).Should().BeEmpty();
      }

      // The payoff the CHANGELOG describes as "a fresh grudge SOURS old kindness... the warmth dims in
      // proportion to what you now owe them": at TarnishRatio 1.0 an equally strong grudge should cancel
      // the warmth outright, not merely discount it, or betrayal would read as barely denting goodwill.
      [Test]
      public void GIVEN_a_grudge_as_strong_as_the_appreciation_WHEN_tarnishing_THEN_it_roughly_cancels_the_warm_feeling()
      {
         AppreciationPolicy.TarnishedMagnitude(60, 60).Should().Be(0);
      }

      // A grudge can strip warmth down to nothing but must never push the result negative: negative warmth
      // would double-count the betrayal (the grudge already carries its own magnitude on its own ledger),
      // and a negative int leaking into prompt text or the encyclopedia would read as nonsense.
      [Test]
      public void GIVEN_a_grudge_far_stronger_than_the_appreciation_WHEN_tarnishing_THEN_it_floors_at_zero_never_negative()
      {
         AppreciationPolicy.TarnishedMagnitude(30, 100).Should().Be(0);
      }

      // Encyclopedia rendering and prompt building both read this for holders who may not have a given
      // appreciation at all; a null must degrade to "no warmth" rather than throw and take down the chat
      // pipeline or the page for every other NPC.
      [Test]
      public void GIVEN_a_null_appreciation_WHEN_reading_effective_magnitude_THEN_it_reads_as_zero()
      {
         AppreciationPolicy.EffectiveMagnitude(null, 100, PlainTraits()).Should().Be(0);
      }

      // SelectDominant is called for holders who have never formed a single appreciation, the common case.
      // Returning null instead of empty would NRE the very first foreach over a brand-new relationship, in
      // both the prompt builder and the encyclopedia loop.
      [Test]
      public void GIVEN_a_null_collection_WHEN_selecting_dominant_THEN_it_returns_empty_not_null()
      {
         AppreciationPolicy.SelectDominant(null, 0, PlainTraits()).Should().NotBeNull().And.BeEmpty();
      }

      // TryPlant passes a holder's live list straight through; a holder with no ledger entries yet (a
      // relationship that has never produced a kindness before) must read as "not at cap", or the very
      // first kindness anyone shows that NPC would be silently refused.
      [Test]
      public void GIVEN_a_null_live_list_WHEN_admitting_a_newcomer_THEN_it_is_admitted()
      {
         AppreciationPolicy.TryAdmit(null, 50, 0, PlainTraits(), out Appreciation toEvict).Should().BeTrue();
         toEvict.Should().BeNull();
      }

      // Pins that tarnishing is proportional, not all-or-nothing: a minor lingering grudge should not erase
      // a much larger standing kindness outright, keeping the "dims in proportion" framing honest for the
      // common case of a small grudge against a big debt of gratitude.
      [Test]
      public void GIVEN_a_softer_active_grudge_WHEN_tarnishing_THEN_the_warmth_is_reduced_but_not_wiped_out()
      {
         int tarnished = AppreciationPolicy.TarnishedMagnitude(60, 20);

         tarnished.Should().BeLessThan(60).And.BeGreaterThan(0);
      }

      // Nothing is left to tarnish. Guards the ordering inside TarnishedMagnitude: without the early
      // zero-check, an already-zero appreciation minus a large grudge reduction would go negative instead
      // of staying pinned at zero.
      [Test]
      public void GIVEN_an_already_faded_appreciation_WHEN_tarnishing_THEN_it_stays_at_zero_regardless_of_the_grudge()
      {
         AppreciationPolicy.TarnishedMagnitude(0, 50).Should().Be(0);
      }

      // Cross-checks the actual EffectiveMagnitude curves against the real GrudgePolicy, not just the
      // BaseDecayPerDay constant ratio (locked separately below): this is the test that would catch a
      // decay-curve change making a kindness fade as fast as, or faster than, a grievance of equal weight,
      // the ROADMAP's "kindness lingers" promise this whole pillar exists to keep.
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

      // Mirrors the grudge system's own attributable guardrail. Without it, an ambient positive event not
      // actually caused by the player (party luck, an unrelated windfall) could still register as the
      // player's kindness, inflating trust for something they never did.
      [Test]
      public void GIVEN_an_unattributable_kindness_WHEN_deciding_formation_THEN_it_refuses_however_large()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: 100, attributable: false)).Should().BeFalse();
      }

      // Long-unplayed campaigns can rack up huge day counts; this pins that decay never overshoots into a
      // negative magnitude that would leak into prompt text or the encyclopedia as nonsense.
      [Test]
      public void GIVEN_enough_elapsed_time_WHEN_reading_effective_magnitude_THEN_it_floors_at_zero_never_negative()
      {
         Appreciation appreciation = NewAppreciation(20, 0);

         AppreciationPolicy.EffectiveMagnitude(appreciation, 100000, PlainTraits()).Should().Be(0);
      }

      // ── Per-holder cap eviction (TryAdmit) ──────────────────────────────

      // Baseline sanity below PerNpcCap: guards against an off-by-one that would start evicting before the
      // ledger is actually full, which would make even a brand-new relationship start losing kindnesses
      // prematurely.
      [Test]
      public void GIVEN_fewer_live_appreciations_than_the_cap_WHEN_admitting_a_newcomer_THEN_it_is_always_admitted_with_nothing_to_evict()
      {
         var live = new List<Appreciation> {NewAppreciation(10, 0)};

         bool admitted = AppreciationPolicy.TryAdmit(live, 5, 0, PlainTraits(), out Appreciation toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().BeNull();
      }

      // The mirror of GrudgePolicy's Mercy-raises-the-floor logic. Without it every NPC, cold ledger-keeper
      // or not, would feel a trivial courtesy exactly as warmly as a generous one, flattening the trait
      // personality the prompt is supposed to voice out of the appreciation system entirely.
      [Test]
      public void GIVEN_high_calculating_WHEN_deciding_formation_THEN_the_bar_is_raised_and_a_middling_kindness_no_longer_forms()
      {
         var calculatingTraits = new TraitFacts {Calculating = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor, traits: calculatingTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeFalse("a cold ledger-keeper is barely moved by a mere kindness");
      }

      // The trait-personality half of formation: a warm-hearted NPC should register a lesser kindness that
      // a plain one would ignore. If this breaks, Generosity stops differentiating how NPCs actually feel
      // gratitude, even though the prompt keeps describing them as generous.
      [Test]
      public void GIVEN_high_generosity_WHEN_deciding_formation_THEN_the_bar_is_lowered_and_a_lesser_kindness_still_forms()
      {
         var generousTraits = new TraitFacts {Generosity = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - AppreciationPolicy.GenerosityFormationFloorReliefPerLevel * 2, traits: generousTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeTrue("a warm heart is moved by even a lesser kindness");
      }

      // The same trait mirroring applied to decay rather than formation, wired live from AppreciationBridge
      // (a hero's real Generosity level). If broken, a generous NPC would form warmth more easily but
      // forget it at the same plain rate, an inconsistent personality.
      [Test]
      public void GIVEN_high_generosity_WHEN_reading_effective_magnitude_THEN_it_decays_slower_than_a_plain_holder()
      {
         Appreciation appreciation = NewAppreciation(50, 0);
         var generous = new TraitFacts {Generosity = 2};

         int plainDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, PlainTraits());
         int generousDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, generous);

         generousDecayed.Should().BeGreaterThan(plainDecayed);
      }

      // Mercy stacks with Generosity in FormationFloor (both subtract independently); pins that a merciful
      // but not especially generous NPC still gets the appropriate relief, not just Generosity alone.
      [Test]
      public void GIVEN_high_mercy_WHEN_deciding_formation_THEN_the_bar_is_also_lowered()
      {
         var mercifulTraits = new TraitFacts {Mercy = 2};
         AppreciationCandidateFacts facts = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - AppreciationPolicy.MercyFormationFloorReliefPerLevel * 2, traits: mercifulTraits);

         AppreciationPolicy.ShouldForm(facts).Should().BeTrue();
      }

      // Same stacking, applied to decay: Mercy must slow fading on its own, independent of Generosity, so
      // the two traits genuinely combine rather than one silently subsuming the other.
      [Test]
      public void GIVEN_high_mercy_WHEN_reading_effective_magnitude_THEN_it_also_decays_slower()
      {
         Appreciation appreciation = NewAppreciation(50, 0);
         var merciful = new TraitFacts {Mercy = 2};

         int plainDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, PlainTraits());
         int mercifulDecayed = AppreciationPolicy.EffectiveMagnitude(appreciation, 20, merciful);

         mercifulDecayed.Should().BeGreaterThan(plainDecayed);
      }

      // Pins the INCLUSIVE edge of MagnitudeFloor (>=): a kindness exactly at the documented tuning
      // constant must form, or the published floor value would quietly be a lie by one point.
      [Test]
      public void GIVEN_magnitude_at_the_floor_WHEN_deciding_formation_THEN_it_forms()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: AppreciationPolicy.MagnitudeFloor)).Should().BeTrue();
      }

      // ── Formation ────────────────────────────────────────────────────────

      // The complementary edge: one point under the floor must be firmly rejected, giving the anti-noise
      // guardrail a real, test-pinned boundary that a future retune cannot silently move.
      [Test]
      public void GIVEN_magnitude_below_the_floor_WHEN_deciding_formation_THEN_it_refuses()
      {
         AppreciationPolicy.ShouldForm(Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - 1)).Should().BeFalse();
      }

      // ── Per-NPC cap ──────────────────────────────────────────────────────

      // PerNpcCap = 3 is what keeps a long relationship's prompt section and encyclopedia page from
      // reading, in GrudgePolicy's own words, "like a bookkeeper's ledger": without ranking and
      // truncation every past kindness would flood the LLM context instead of just what matters now.
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

      // Baseline for the tarnish mechanic: a holder with nothing against the player must see their
      // remembered kindness completely untouched, so TarnishedMagnitude never silently discounts warmth
      // it has no grudge to justify discounting.
      [Test]
      public void GIVEN_no_active_grudge_WHEN_tarnishing_THEN_the_appreciation_keeps_its_full_warmth()
      {
         AppreciationPolicy.TarnishedMagnitude(60, 0).Should().Be(60);
      }

      // EffectiveMagnitude must be a true point-in-time read: this guards a fencepost bug where an
      // appreciation formed "today" would already show a day of decay it never actually experienced.
      [Test]
      public void GIVEN_no_time_has_passed_WHEN_reading_effective_magnitude_THEN_it_reads_as_the_stored_magnitude()
      {
         Appreciation appreciation = NewAppreciation(42, 500);

         AppreciationPolicy.EffectiveMagnitude(appreciation, 500, PlainTraits()).Should().Be(42);
      }

      // A null candidate is an upstream caller bug; ShouldForm must fail closed (no appreciation formed)
      // rather than NRE and take down whatever action pipeline called it mid-conversation.
      [Test]
      public void GIVEN_null_facts_WHEN_deciding_formation_THEN_it_refuses()
      {
         AppreciationPolicy.ShouldForm(null).Should().BeFalse();
      }

      // ── Decay: locked SLOWER than the grudge base rate ──────────────────

      // Stake beyond the inline note below: this locks the RATIO itself, not just each side's number, so a
      // future retune of either BaseDecayPerDay constant that drifts the 1:2 relationship fails loudly here
      // instead of silently breaking the "kindness lingers twice as long as a grievance" promise.
      [Test]
      public void GIVEN_the_baseline_decay_rate_WHEN_compared_to_the_grudge_base_rate_THEN_it_is_exactly_half()
      {
         // Locks the ROADMAP's "kindness lingers" framing: an appreciation fades at half the speed of a
         // grudge of the same starting magnitude. If either constant moves, this test flags it.
         AppreciationPolicy.BaseDecayPerDay.Should().Be(GrudgePolicy.BaseDecayPerDay / 2.0);
      }

      // Guards the basic direction of decay: if this ever went flat or reversed, both this pillar's "fades
      // slowly" promise and the shared kindness-outlasts-grudge comparison above would quietly stop holding.
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