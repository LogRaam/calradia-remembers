// Code written by Gabriel Mailhot, 03/07/2026.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
   [TestFixture]
   public sealed class GrudgePolicyTests
   {
      [Test]
      public void GIVEN_a_delivered_prisoner_grudge_WHEN_mapping_the_resolution_kind_THEN_it_wants_a_real_deed()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.DeliveredPrisoner, 50).Should().Be(GrudgeResolutionKind.Deed);
      }

      [Test]
      public void GIVEN_a_full_house_but_some_entries_are_resolved_or_fully_decayed_WHEN_admitting_THEN_only_the_live_ones_count_toward_the_cap()
      {
         var mostlyDead = new List<Grudge> {
            NewGrudge(90, eventDay: 0, resolved: true),
            NewGrudge(5, 10, 0), // fully decays by day 100000
            NewGrudge(50, eventDay: 0)
         };

         bool admitted = GrudgePolicy.TryAdmit(mostlyDead, 1, 100000, PlainTraits(), out Grudge toEvict);

         admitted.Should().BeTrue("only one grudge (magnitude 50) is still live at that day — well under the cap");
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_full_house_WHEN_the_newcomer_is_no_stronger_than_the_weakest_live_grudge_THEN_it_is_refused()
      {
         var live = new List<Grudge> {
            NewGrudge(90, eventDay: 0),
            NewGrudge(50, eventDay: 0),
            NewGrudge(20, eventDay: 0)
         };

         bool admitted = GrudgePolicy.TryAdmit(live, 20, 0, PlainTraits(), out Grudge toEvict);

         admitted.Should().BeFalse("an equal-or-weaker newcomer would just add noise, the exact case the cap guards against");
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_full_house_WHEN_the_newcomer_is_strictly_stronger_than_the_weakest_live_grudge_THEN_it_is_admitted_and_the_weakest_is_named_for_eviction()
      {
         var live = new List<Grudge> {
            NewGrudge(90, eventDay: 0),
            NewGrudge(50, eventDay: 0),
            NewGrudge(20, eventDay: 0)
         };

         bool admitted = GrudgePolicy.TryAdmit(live, 21, 0, PlainTraits(), out Grudge toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().NotBeNull();
         toEvict.Magnitude.Should().Be(20, "the weakest live grudge yields to a strictly stronger newcomer");
      }

      [Test]
      public void GIVEN_a_fully_decayed_grudge_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var grudges = new List<Grudge> {NewGrudge(5, 10, 0)};

         GrudgePolicy.SelectDominant(grudges, 100000, PlainTraits()).Should().BeEmpty();
      }

      [Test]
      public void GIVEN_a_hard_jealousy_grudge_WHEN_mapping_the_resolution_kind_THEN_only_time_heals_it()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Jealousy, GrudgePolicy.SoftRigidityCeiling)
                     .Should().Be(GrudgeResolutionKind.Time);
      }

      [Test]
      public void GIVEN_a_null_collection_WHEN_selecting_dominant_THEN_it_returns_empty_not_null()
      {
         GrudgePolicy.SelectDominant(null, 0, PlainTraits()).Should().NotBeNull().And.BeEmpty();
      }

      [Test]
      public void GIVEN_a_null_live_list_WHEN_admitting_a_newcomer_THEN_it_is_admitted()
      {
         GrudgePolicy.TryAdmit(null, 50, 0, PlainTraits(), out Grudge toEvict).Should().BeTrue();
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_a_possessive_holder_WHEN_a_jealousy_candidate_forms_THEN_the_magnitude_is_amplified()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.Jealousy, 10, possessiveAmplifier: true);

         GrudgePolicy.EffectiveFormationMagnitude(facts).Should().Be(10 + GrudgePolicy.PossessiveJealousyAmplifierBonus);
      }

      [Test]
      public void GIVEN_a_possessive_holder_WHEN_a_non_jealousy_candidate_forms_THEN_the_amplifier_does_not_apply()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.FailedQuest, 10, possessiveAmplifier: true);

         GrudgePolicy.EffectiveFormationMagnitude(facts).Should().Be(10, "the possessive amplifier is jealousy-only");
      }

      [Test]
      public void GIVEN_a_resolvable_grudge_WHEN_resolving_it_THEN_it_is_marked_resolved_on_that_day()
      {
         Grudge grudge = NewGrudge(rigidity: 20);

         bool resolved = GrudgePolicy.Resolve(grudge, 500);

         resolved.Should().BeTrue();
         grudge.IsResolved.Should().BeTrue();
         grudge.ResolvedDay.Should().Be(500);
      }

      [Test]
      public void GIVEN_a_resolved_grudge_WHEN_reading_effective_magnitude_THEN_it_reads_as_zero()
      {
         Grudge grudge = NewGrudge(90, 10, resolved: true);

         GrudgePolicy.EffectiveMagnitude(grudge, grudge.EventDay, PlainTraits()).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_resolved_grudge_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var grudges = new List<Grudge> {NewGrudge(100, resolved: true)};

         GrudgePolicy.SelectDominant(grudges, 0, PlainTraits()).Should().BeEmpty();
      }

      [Test]
      public void GIVEN_a_soft_divorce_grudge_WHEN_mapping_the_resolution_kind_THEN_a_gift_suffices()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Divorce, GrudgePolicy.SoftRigidityCeiling - 1)
                     .Should().Be(GrudgeResolutionKind.Gift);
      }

      [Test]
      public void GIVEN_a_soft_grudge_WHEN_checking_resolvability_THEN_it_is_resolvable()
      {
         Grudge grudge = NewGrudge(rigidity: 20);

         GrudgePolicy.IsResolvable(grudge).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_vendetta_WHEN_attempting_to_resolve_it_THEN_it_refuses_and_leaves_it_untouched()
      {
         Grudge grudge = NewGrudge(rigidity: GrudgePolicy.VendettaRigidity);

         bool resolved = GrudgePolicy.Resolve(grudge, 500);

         resolved.Should().BeFalse();
         grudge.IsResolved.Should().BeFalse();
         grudge.ResolvedDay.Should().Be(-1);
      }

      // ── Resolution ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_vendetta_WHEN_checking_resolvability_THEN_it_is_unresolvable()
      {
         Grudge grudge = NewGrudge(rigidity: GrudgePolicy.VendettaRigidity);

         GrudgePolicy.IsResolvable(grudge).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_vendetta_WHEN_reading_effective_magnitude_THEN_it_never_decays()
      {
         Grudge grudge = NewGrudge(50, GrudgePolicy.VendettaRigidity, 0);

         GrudgePolicy.EffectiveMagnitude(grudge, 5000, PlainTraits()).Should().Be(50);
      }

      [Test]
      public void GIVEN_an_already_resolved_grudge_WHEN_checking_resolvability_THEN_it_is_not_resolvable_again()
      {
         Grudge grudge = NewGrudge(rigidity: 20, resolved: true);

         GrudgePolicy.IsResolvable(grudge).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_unattributable_event_WHEN_deciding_formation_THEN_it_refuses_however_large()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: 100, attributable: false)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_any_source_that_reaches_maximum_rigidity_WHEN_mapping_the_resolution_kind_THEN_it_is_unforgivable()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.FailedQuest, GrudgePolicy.VendettaRigidity)
                     .Should().Be(GrudgeResolutionKind.Unforgivable, "100 rigidity means unforgivable whatever the source");
      }

      [Test]
      public void GIVEN_bastard_blackmail_WHEN_mapping_the_resolution_kind_THEN_it_is_always_transactional_regardless_of_rigidity()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.BastardBlackmail, 5).Should().Be(GrudgeResolutionKind.Gift);
         GrudgePolicy.ResolutionKindFor(GrudgeSource.BastardBlackmail, 90).Should().Be(GrudgeResolutionKind.Gift);
      }

      [Test]
      public void GIVEN_enough_elapsed_time_WHEN_reading_effective_magnitude_THEN_it_floors_at_zero_never_negative()
      {
         Grudge grudge = NewGrudge(20, 20, 0);

         GrudgePolicy.EffectiveMagnitude(grudge, 100000, PlainTraits()).Should().Be(0);
      }

      [Test]
      public void GIVEN_extreme_negative_regard_and_high_calculating_WHEN_computing_rigidity_THEN_it_clamps_at_one_hundred()
      {
         GrudgeCandidateFacts facts = Candidate(traits: new TraitFacts {Calculating = 2});

         GrudgePolicy.RigidityAt(-100, facts).Should().Be(100);
      }

      [Test]
      public void GIVEN_extreme_positive_regard_and_high_mercy_WHEN_computing_rigidity_THEN_it_clamps_at_zero_not_negative()
      {
         GrudgeCandidateFacts facts = Candidate(traits: new TraitFacts {Mercy = 2});

         GrudgePolicy.RigidityAt(100, facts).Should().Be(0);
      }

      // ── Per-holder cap eviction (TryAdmit) ──────────────────────────────

      [Test]
      public void GIVEN_fewer_live_grudges_than_the_cap_WHEN_admitting_a_newcomer_THEN_it_is_always_admitted_with_nothing_to_evict()
      {
         var live = new List<Grudge> {NewGrudge(10, eventDay: 0)};

         bool admitted = GrudgePolicy.TryAdmit(live, 5, 0, PlainTraits(), out Grudge toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().BeNull();
      }

      [Test]
      public void GIVEN_high_calculating_WHEN_computing_rigidity_THEN_it_pushes_rigidity_up()
      {
         GrudgeCandidateFacts plain = Candidate();
         GrudgeCandidateFacts calculating = Candidate(traits: new TraitFacts {Calculating = 2});

         GrudgePolicy.RigidityAt(0, calculating).Should().BeGreaterThan(GrudgePolicy.RigidityAt(0, plain));
      }

      [Test]
      public void GIVEN_high_calculating_WHEN_deciding_formation_THEN_the_bar_is_lowered_and_a_lesser_slight_still_forms()
      {
         var calculatingTraits = new TraitFacts {Calculating = 2};
         GrudgeCandidateFacts facts = Candidate(magnitude: GrudgePolicy.MagnitudeFloor - GrudgePolicy.CalculatingFormationFloorPerLevel * 2, traits: calculatingTraits);

         GrudgePolicy.ShouldForm(facts).Should().BeTrue("a calculating holder tallies even a lesser slight");
      }

      [Test]
      public void GIVEN_high_calculating_WHEN_reading_effective_magnitude_THEN_it_never_decays()
      {
         Grudge grudge = NewGrudge(50, 50, 0);
         var calculating = new TraitFacts {Calculating = GrudgePolicy.NeverDecayCalculatingLevel};

         GrudgePolicy.EffectiveMagnitude(grudge, 1000, calculating).Should().Be(50);
      }

      [Test]
      public void GIVEN_high_mercy_WHEN_computing_rigidity_THEN_it_pulls_rigidity_down()
      {
         GrudgeCandidateFacts plain = Candidate();
         GrudgeCandidateFacts merciful = Candidate(traits: new TraitFacts {Mercy = 2});

         GrudgePolicy.RigidityAt(0, merciful).Should().BeLessThan(GrudgePolicy.RigidityAt(0, plain));
      }

      [Test]
      public void GIVEN_high_mercy_WHEN_deciding_formation_THEN_the_bar_is_raised_and_a_middling_slight_no_longer_forms()
      {
         var mercifulTraits = new TraitFacts {Mercy = 2};
         GrudgeCandidateFacts facts = Candidate(magnitude: GrudgePolicy.MagnitudeFloor, traits: mercifulTraits);

         GrudgePolicy.ShouldForm(facts).Should().BeFalse("a forgiving holder needs a bigger slight to form a grudge at all");
      }

      [Test]
      public void GIVEN_high_mercy_WHEN_reading_effective_magnitude_THEN_it_decays_faster_than_a_plain_holder()
      {
         Grudge grudge = NewGrudge(50, 50, 0);
         var merciful = new TraitFacts {Mercy = 2};

         int plainDecayed = GrudgePolicy.EffectiveMagnitude(grudge, 20, PlainTraits());
         int mercifulDecayed = GrudgePolicy.EffectiveMagnitude(grudge, 20, merciful);

         mercifulDecayed.Should().BeLessThan(plainDecayed);
      }

      // ── Rigidity (regard sets how hard resolution is) ───────────────────

      [Test]
      public void GIVEN_high_regard_for_the_target_WHEN_computing_rigidity_THEN_it_is_soft()
      {
         int rigid = GrudgePolicy.RigidityAt(80, Candidate());

         rigid.Should().BeLessThan(GrudgePolicy.BaseRigidityAtNeutralRegard, "the NPC likes the target, so the grudge is reconcilable");
      }

      [Test]
      public void GIVEN_magnitude_at_the_floor_WHEN_deciding_formation_THEN_it_forms()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: GrudgePolicy.MagnitudeFloor)).Should().BeTrue();
      }

      // ── Formation ────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_magnitude_below_the_floor_WHEN_deciding_formation_THEN_it_refuses()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: GrudgePolicy.MagnitudeFloor - 1)).Should().BeFalse();
      }

      // ── Per-NPC cap ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_more_live_grudges_than_the_cap_WHEN_selecting_dominant_THEN_only_the_top_ones_surface()
      {
         var grudges = new List<Grudge> {
            NewGrudge(90, eventDay: 0),
            NewGrudge(80, eventDay: 0),
            NewGrudge(70, eventDay: 0),
            NewGrudge(60, eventDay: 0),
            NewGrudge(eventDay: 0)
         };

         List<Grudge> dominant = GrudgePolicy.SelectDominant(grudges, 0, PlainTraits()).ToList();

         dominant.Should().HaveCount(GrudgePolicy.PerNpcCap);
         dominant.Select(g => g.Magnitude).Should().BeInDescendingOrder();
         dominant.Select(g => g.Magnitude).Should().Contain(new[] {90, 80, 70});
      }

      [Test]
      public void GIVEN_negative_regard_for_the_target_WHEN_computing_rigidity_THEN_it_is_hard()
      {
         int rigid = GrudgePolicy.RigidityAt(-80, Candidate());

         rigid.Should().BeGreaterThan(GrudgePolicy.BaseRigidityAtNeutralRegard, "disliking the target on top of the grudge makes it far harder to win over");
      }

      [Test]
      public void GIVEN_null_facts_WHEN_deciding_formation_THEN_it_refuses()
      {
         GrudgePolicy.ShouldForm(null).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_executed_kin_source_at_maximum_rigidity_WHEN_mapping_the_resolution_kind_THEN_it_is_unforgivable()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.ExecutedKin, GrudgePolicy.VendettaRigidity)
                     .Should().Be(GrudgeResolutionKind.Unforgivable);
      }

      [Test]
      public void GIVEN_the_executed_kin_source_WHEN_computing_rigidity_THEN_it_is_always_maximum_regardless_of_regard()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.ExecutedKin);

         GrudgePolicy.RigidityAt(100, facts).Should().Be(GrudgePolicy.VendettaRigidity);
         GrudgePolicy.RigidityAt(-100, facts).Should().Be(GrudgePolicy.VendettaRigidity);
      }

      [Test]
      public void GIVEN_the_possessive_amplifier_WHEN_it_pushes_a_borderline_jealousy_event_over_the_floor_THEN_it_forms()
      {
         GrudgeCandidateFacts facts = Candidate(
            GrudgeSource.Jealousy,
            GrudgePolicy.MagnitudeFloor - 1,
            possessiveAmplifier: true);

         GrudgePolicy.ShouldForm(facts).Should().BeTrue();
      }

      // ── Decay ────────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_time_passing_WHEN_reading_effective_magnitude_THEN_it_softens()
      {
         Grudge grudge = NewGrudge();

         int today = GrudgePolicy.EffectiveMagnitude(grudge, 100, PlainTraits());
         int muchLater = GrudgePolicy.EffectiveMagnitude(grudge, 200, PlainTraits());

         muchLater.Should().BeLessThan(today);
      }

      #region private

      private static GrudgeCandidateFacts Candidate(
         GrudgeSource source = GrudgeSource.FailedQuest,
         int magnitude = 20,
         bool attributable = true,
         bool possessiveAmplifier = false,
         TraitFacts? traits = null)
         => new() {
            Source = source,
            Magnitude = magnitude,
            IsAttributable = attributable,
            PossessiveAmplifier = possessiveAmplifier,
            Traits = traits ?? PlainTraits()
         };

      private static Grudge NewGrudge(int magnitude = 50, int rigidity = 50, int eventDay = 100, bool resolved = false)
         => new() {
            Id = "holder|0|100",
            HolderId = "holder",
            TargetId = "player",
            Source = GrudgeSource.FailedQuest,
            CauseSummary = "a broken promise",
            EventDay = eventDay,
            Magnitude = magnitude,
            Rigidity = rigidity,
            IsResolved = resolved
         };

      private static TraitFacts PlainTraits() => new();

      #endregion
   }
}