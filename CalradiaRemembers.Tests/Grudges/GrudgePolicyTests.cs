// Code written by Gabriel Mailhot, 03/07/2026.
//
// GrudgePolicy is the arbiter behind every NPC grievance in the mod: whether an event is worth
// grudging over at all (formation), how hard it will be to mend (rigidity, seeded from the holder's
// regard for the target), how it fades with time (decay), which grudges are allowed to surface at once
// per NPC (the cap), and what kind of gesture actually mends one (resolution). GrudgeBehavior plants
// through ShouldForm/TryAdmit, EncounterContextBuilder/ChatViewModel colour the prompt through
// SelectDominant, and GrudgeResolutionService routes Amends/Gift/Deed through ResolutionKindFor/Resolve.
// Get any of these wrong and either the NPC forgets a real wound (immersion breaks) or nurses noise
// forever (the "bookkeeper's ledger" the per-NPC cap and anti-noise floors exist to prevent), or offers
// a resolution path that can never actually satisfy the grudge it names.

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
      // DeliveredPrisoner is the one source that skips the soft/hard rigidity split entirely (see the
      // switch in ResolutionKindFor): handing someone to their enemy is a concrete betrayal that no
      // amount of talk can undo, so only a matching real deed is ever offered as the way back.
      [Test]
      public void GIVEN_a_delivered_prisoner_grudge_WHEN_mapping_the_resolution_kind_THEN_it_wants_a_real_deed()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.DeliveredPrisoner, 50).Should().Be(GrudgeResolutionKind.Deed);
      }

      // If stale (resolved or fully decayed) entries counted toward the cap, an NPC whose old wounds
      // simply faded could get stuck refusing every new grievance forever, a "full house" that is
      // actually empty. TryAdmit filters to live grudges first so the cap only ever gates real ones.
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

      // Per TryAdmit's own doc: admitting an equal-or-weaker newcomer over the weakest live grudge would
      // just add noise, the exact case the per-NPC cap (PerNpcCap) exists to prevent so an NPC's
      // grievances never read as an ever-growing bookkeeper's ledger.
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

      // Pins the boundary at STRICT inequality: a tie must not evict, only a newcomer that is genuinely
      // worse than the current weakest grudge earns a slot, and TryAdmit hands that weakest one back via
      // toEvict so GrudgeBehavior.TryPlant knows exactly which entry to remove before adding the new one.
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

      // SelectDominant feeds both the prompt (EncounterContextBuilder) and the encyclopedia Grievances
      // page. Without this filter a wound whose effective magnitude has faded to zero would keep tinting
      // the NPC's tone and listing in the Grievances section long after decay should have retired it.
      [Test]
      public void GIVEN_a_fully_decayed_grudge_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var grudges = new List<Grudge> {NewGrudge(5, 10, 0)};

         GrudgePolicy.SelectDominant(grudges, 100000, PlainTraits()).Should().BeEmpty();
      }

      // Once a Jealousy grudge has hardened past SoftRigidityCeiling, the resolution table drops Amends
      // in favour of Time only: at that point no in-chat gesture (make_amends, a gift) should be able to
      // talk the jealousy away, it has to actually cool on its own.
      [Test]
      public void GIVEN_a_hard_jealousy_grudge_WHEN_mapping_the_resolution_kind_THEN_only_time_heals_it()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Jealousy, GrudgePolicy.SoftRigidityCeiling)
                     .Should().Be(GrudgeResolutionKind.Time);
      }

      // Most NPCs have never had a grudge planted, so their grudge store is null, not an empty list.
      // SelectDominant's caller (EncounterContextBuilder building the prompt) iterates the result
      // directly; returning null here instead of empty would crash prompt-building for every such NPC.
      [Test]
      public void GIVEN_a_null_collection_WHEN_selecting_dominant_THEN_it_returns_empty_not_null()
      {
         GrudgePolicy.SelectDominant(null, 0, PlainTraits()).Should().NotBeNull().And.BeEmpty();
      }

      // Mirrors the null-collection case for SelectDominant: an NPC's very first grudge has no live list
      // to check the cap against yet, so TryAdmit must treat a null list as "nothing to evict, just
      // admit it" rather than refusing the first grudge an NPC ever forms.
      [Test]
      public void GIVEN_a_null_live_list_WHEN_admitting_a_newcomer_THEN_it_is_admitted()
      {
         GrudgePolicy.TryAdmit(null, 50, 0, PlainTraits(), out Grudge toEvict).Should().BeTrue();
         toEvict.Should().BeNull();
      }

      // PossessiveAmplifier models a Possessive/monogamous-strict holder's romantic preferences: the same
      // flirtation stings them harder than it would a more easygoing NPC. EffectiveFormationMagnitude is
      // the single source of truth for the amplified value, so this bonus must show up here.
      [Test]
      public void GIVEN_a_possessive_holder_WHEN_a_jealousy_candidate_forms_THEN_the_magnitude_is_amplified()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.Jealousy, 10, possessiveAmplifier: true);

         GrudgePolicy.EffectiveFormationMagnitude(facts).Should().Be(10 + GrudgePolicy.PossessiveJealousyAmplifierBonus);
      }

      // The possessive/monogamous-strict amplifier is jealousy-only by design (GrudgeCandidateFacts'
      // own doc). A possessive personality has no business inflating an unrelated grievance like a
      // failed quest, that would make the trait bleed into grudge sources it was never meant to touch.
      [Test]
      public void GIVEN_a_possessive_holder_WHEN_a_non_jealousy_candidate_forms_THEN_the_amplifier_does_not_apply()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.FailedQuest, 10, possessiveAmplifier: true);

         GrudgePolicy.EffectiveFormationMagnitude(facts).Should().Be(10, "the possessive amplifier is jealousy-only");
      }

      // This is the mutation GrudgeResolutionService drives when a make_amends/gift/deed gesture lands:
      // stamping IsResolved + ResolvedDay is what makes EffectiveMagnitude and SelectDominant retire the
      // grudge from the prompt and the Grievances page from this day forward.
      [Test]
      public void GIVEN_a_resolvable_grudge_WHEN_resolving_it_THEN_it_is_marked_resolved_on_that_day()
      {
         Grudge grudge = NewGrudge(rigidity: 20);

         bool resolved = GrudgePolicy.Resolve(grudge, 500);

         resolved.Should().BeTrue();
         grudge.IsResolved.Should().BeTrue();
         grudge.ResolvedDay.Should().Be(500);
      }

      // Grudge.Magnitude is documented as never mutated once set, so every caller MUST read through
      // EffectiveMagnitude, not the raw field. If a resolved grudge's raw Magnitude ever leaked through,
      // a mended wound would keep colouring the prompt as if it were still fresh.
      [Test]
      public void GIVEN_a_resolved_grudge_WHEN_reading_effective_magnitude_THEN_it_reads_as_zero()
      {
         Grudge grudge = NewGrudge(90, 10, resolved: true);

         GrudgePolicy.EffectiveMagnitude(grudge, grudge.EventDay, PlainTraits()).Should().Be(0);
      }

      // Same guarantee as the fully-decayed case, but for a grudge the player actually mended: once
      // resolved it must vanish from the encyclopedia's Grievances page and the prompt, or reconciling
      // with an NPC would visibly accomplish nothing.
      [Test]
      public void GIVEN_a_resolved_grudge_WHEN_selecting_dominant_THEN_it_never_surfaces()
      {
         var grudges = new List<Grudge> {NewGrudge(100, resolved: true)};

         GrudgePolicy.SelectDominant(grudges, 0, PlainTraits()).Should().BeEmpty();
      }

      // A mild parting wound (soft rigidity) only needs a Gift to settle, matching the CHANGELOG's
      // signed-divorce framing where a barely-unhappy or mutual-consent parting stings far less than one
      // forced on an unwilling spouse, so it should not demand the same Amends effort a harder case does.
      [Test]
      public void GIVEN_a_soft_divorce_grudge_WHEN_mapping_the_resolution_kind_THEN_a_gift_suffices()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Divorce, GrudgePolicy.SoftRigidityCeiling - 1)
                     .Should().Be(GrudgeResolutionKind.Gift);
      }

      // Per the ROADMAP, the in-chat make_amends verb is only taught when the NPC holds a KNOWN
      // resolvable grudge. If an ordinary soft grudge (rigidity well under VendettaRigidity) failed this
      // check, the player would never be offered a way to reconcile the most common case.
      [Test]
      public void GIVEN_a_soft_grudge_WHEN_checking_resolvability_THEN_it_is_resolvable()
      {
         Grudge grudge = NewGrudge(rigidity: 20);

         GrudgePolicy.IsResolvable(grudge).Should().BeTrue();
      }

      // Resolve's own doc: callers are expected to gate on IsResolvable first, but this refusal is a
      // second, independent guard on the mutation itself. Without it, a caller that skips the pre-check
      // could let an Amends/Gift gesture wipe away a supposedly-unforgivable vendetta.
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

      // A vendetta (rigidity == VendettaRigidity, e.g. ExecutedKin) must never be flagged resolvable, or
      // the chat layer would teach a make_amends/gift/deed verb for a wound the design explicitly calls
      // unforgivable, letting the player talk their way out of the one grudge meant to stick forever.
      [Test]
      public void GIVEN_a_vendetta_WHEN_checking_resolvability_THEN_it_is_unresolvable()
      {
         Grudge grudge = NewGrudge(rigidity: GrudgePolicy.VendettaRigidity);

         GrudgePolicy.IsResolvable(grudge).Should().BeFalse();
      }

      // A true vendetta must stay at full intensity forever: if ordinary time-decay applied here, an
      // "unforgivable" wound would quietly soften and eventually fall out of SelectDominant on its own,
      // contradicting the Unforgivable resolution kind it maps to.
      [Test]
      public void GIVEN_a_vendetta_WHEN_reading_effective_magnitude_THEN_it_never_decays()
      {
         Grudge grudge = NewGrudge(50, GrudgePolicy.VendettaRigidity, 0);

         GrudgePolicy.EffectiveMagnitude(grudge, 5000, PlainTraits()).Should().Be(50);
      }

      // GrudgeResolutionService applies a relation nudge when a grudge resolves. Without this guard a
      // player could re-trigger make_amends on an already-mended grudge and rack up repeated relation
      // gains from a single wound.
      [Test]
      public void GIVEN_an_already_resolved_grudge_WHEN_checking_resolvability_THEN_it_is_not_resolvable_again()
      {
         Grudge grudge = NewGrudge(rigidity: 20, resolved: true);

         GrudgePolicy.IsResolvable(grudge).Should().BeFalse();
      }

      // Per GrudgeCandidateFacts.IsAttributable's own doc, this is the anti-noise guardrail: an event not
      // clearly caused by the target must never form a grudge, no matter how large its raw magnitude.
      // Without it, the player (or another NPC) could be blamed for something they demonstrably did not do.
      [Test]
      public void GIVEN_an_unattributable_event_WHEN_deciding_formation_THEN_it_refuses_however_large()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: 100, attributable: false)).Should().BeFalse();
      }

      // ResolutionKindFor's doc calls out that ANY grudge reaching VendettaRigidity is Unforgivable
      // regardless of source, not just the hardcoded ExecutedKin case. Trait stacking (very Calculating,
      // very disliked target) can push an ordinary source's rigidity all the way to 100 too.
      [Test]
      public void GIVEN_any_source_that_reaches_maximum_rigidity_WHEN_mapping_the_resolution_kind_THEN_it_is_unforgivable()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.FailedQuest, GrudgePolicy.VendettaRigidity)
                     .Should().Be(GrudgeResolutionKind.Unforgivable, "100 rigidity means unforgivable whatever the source");
      }

      // Blackmail is fundamentally transactional (paying to keep it quiet), so unlike most sources it
      // never switches to a harder resolution kind as rigidity rises: the ResolutionKindFor switch
      // returns Gift at both a low and a high rigidity value, proving regard never changes what settles it.
      [Test]
      public void GIVEN_bastard_blackmail_WHEN_mapping_the_resolution_kind_THEN_it_is_always_transactional_regardless_of_rigidity()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.BastardBlackmail, 5).Should().Be(GrudgeResolutionKind.Gift);
         GrudgePolicy.ResolutionKindFor(GrudgeSource.BastardBlackmail, 90).Should().Be(GrudgeResolutionKind.Gift);
      }

      // SelectDominant only surfaces grudges with EffectiveMagnitude > 0. A negative value would still
      // pass that filter and could render as nonsensical "negative intensity" prose in the prompt or the
      // Grievances page, so decay must floor at zero instead of running past it.
      [Test]
      public void GIVEN_enough_elapsed_time_WHEN_reading_effective_magnitude_THEN_it_floors_at_zero_never_negative()
      {
         Grudge grudge = NewGrudge(20, 20, 0);

         GrudgePolicy.EffectiveMagnitude(grudge, 100000, PlainTraits()).Should().Be(0);
      }

      // RigidityAt's doc promises a 0..100 clamp. VendettaRigidity IS 100, and IsResolvable/
      // ResolutionKindFor both compare against that exact constant; an unclamped rigidity drifting past
      // 100 from stacked negative regard and high Calculating would silently misbehave against those checks.
      [Test]
      public void GIVEN_extreme_negative_regard_and_high_calculating_WHEN_computing_rigidity_THEN_it_clamps_at_one_hundred()
      {
         GrudgeCandidateFacts facts = Candidate(traits: new TraitFacts {Calculating = 2});

         GrudgePolicy.RigidityAt(-100, facts).Should().Be(100);
      }

      // Mirrors the upper clamp at the opposite extreme: stacked high regard and high Mercy could
      // otherwise drive the formula below zero, and a negative rigidity has no meaning against the
      // documented 0..100 scale (nor against SoftRigidityCeiling's soft/hard split).
      [Test]
      public void GIVEN_extreme_positive_regard_and_high_mercy_WHEN_computing_rigidity_THEN_it_clamps_at_zero_not_negative()
      {
         GrudgeCandidateFacts facts = Candidate(traits: new TraitFacts {Mercy = 2});

         GrudgePolicy.RigidityAt(100, facts).Should().Be(0);
      }

      // ── Per-holder cap eviction (TryAdmit) ──────────────────────────────

      // Pins the common case: most NPCs sit under PerNpcCap live grudges, and TryAdmit's doc is explicit
      // that below the cap the newcomer is ALWAYS admitted with toEvict null, so the eviction machinery
      // (and its noise-guard refusal) should never even engage until an NPC is genuinely at capacity.
      [Test]
      public void GIVEN_fewer_live_grudges_than_the_cap_WHEN_admitting_a_newcomer_THEN_it_is_always_admitted_with_nothing_to_evict()
      {
         var live = new List<Grudge> {NewGrudge(10, eventDay: 0)};

         bool admitted = GrudgePolicy.TryAdmit(live, 5, 0, PlainTraits(), out Grudge toEvict);

         admitted.Should().BeTrue();
         toEvict.Should().BeNull();
      }

      // CalculatingRigidityPerLevel's doc: a Calculating holder's resolution "wants payment or power",
      // not a sincere word. This pins that trait to the rigidity side of the master rule, on top of
      // (not instead of) the regard-based rigidity every grudge already has.
      [Test]
      public void GIVEN_high_calculating_WHEN_computing_rigidity_THEN_it_pushes_rigidity_up()
      {
         GrudgeCandidateFacts plain = Candidate();
         GrudgeCandidateFacts calculating = Candidate(traits: new TraitFacts {Calculating = 2});

         GrudgePolicy.RigidityAt(0, calculating).Should().BeGreaterThan(GrudgePolicy.RigidityAt(0, plain));
      }

      // CalculatingFormationFloorPerLevel's doc: a Calculating holder "tallies everything", so their
      // formation floor sits below the baseline MagnitudeFloor. This proves a slight too minor to grudge
      // over for a plain-traits NPC still registers for one who keeps score of every slight.
      [Test]
      public void GIVEN_high_calculating_WHEN_deciding_formation_THEN_the_bar_is_lowered_and_a_lesser_slight_still_forms()
      {
         var calculatingTraits = new TraitFacts {Calculating = 2};
         GrudgeCandidateFacts facts = Candidate(magnitude: GrudgePolicy.MagnitudeFloor - GrudgePolicy.CalculatingFormationFloorPerLevel * 2, traits: calculatingTraits);

         GrudgePolicy.ShouldForm(facts).Should().BeTrue("a calculating holder tallies even a lesser slight");
      }

      // NeverDecayCalculatingLevel's doc: "tallies everything, forgets nothing." At or above that
      // Calculating level a grudge must stay at full intensity indefinitely, the same never-fades
      // treatment the vendetta case gets, but driven by trait rather than rigidity.
      [Test]
      public void GIVEN_high_calculating_WHEN_reading_effective_magnitude_THEN_it_never_decays()
      {
         Grudge grudge = NewGrudge(50, 50, 0);
         var calculating = new TraitFacts {Calculating = GrudgePolicy.NeverDecayCalculatingLevel};

         GrudgePolicy.EffectiveMagnitude(grudge, 1000, calculating).Should().Be(50);
      }

      // MercyRigidityReliefPerLevel's doc: for a merciful holder "a sincere amend suffices." This pins
      // the trait-side counterpart to Calculating, softening resolution independently of how the holder
      // already regards the target.
      [Test]
      public void GIVEN_high_mercy_WHEN_computing_rigidity_THEN_it_pulls_rigidity_down()
      {
         GrudgeCandidateFacts plain = Candidate();
         GrudgeCandidateFacts merciful = Candidate(traits: new TraitFacts {Mercy = 2});

         GrudgePolicy.RigidityAt(0, merciful).Should().BeLessThan(GrudgePolicy.RigidityAt(0, plain));
      }

      // MercyFormationFloorPerLevel's doc: more forgiving, harder to offend into a lasting grudge. This
      // is the flip side of the Calculating formation test: the exact same magnitude that clears the
      // plain-traits floor must fail to form for a genuinely merciful holder.
      [Test]
      public void GIVEN_high_mercy_WHEN_deciding_formation_THEN_the_bar_is_raised_and_a_middling_slight_no_longer_forms()
      {
         var mercifulTraits = new TraitFacts {Mercy = 2};
         GrudgeCandidateFacts facts = Candidate(magnitude: GrudgePolicy.MagnitudeFloor, traits: mercifulTraits);

         GrudgePolicy.ShouldForm(facts).Should().BeFalse("a forgiving holder needs a bigger slight to form a grudge at all");
      }

      // MercyDecayBonusPerLevel's doc: a merciful holder "forgives faster." This is the decay-side
      // completion of Mercy's effect (already lowering rigidity and raising the formation floor), so a
      // forgiving NPC's grudges also fade off the prompt sooner than a plain-traits NPC's would.
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

      // This is the pillar's master rule (ROADMAP, ratified 2026-07-03): an NPC can like the player yet
      // still hold something against them, and that liking is exactly what makes the grudge reconcilable.
      // Regard and grudge are deliberately kept as SEPARATE axes; this test is the "liked despite" half.
      [Test]
      public void GIVEN_high_regard_for_the_target_WHEN_computing_rigidity_THEN_it_is_soft()
      {
         int rigid = GrudgePolicy.RigidityAt(80, Candidate());

         rigid.Should().BeLessThan(GrudgePolicy.BaseRigidityAtNeutralRegard, "the NPC likes the target, so the grudge is reconcilable");
      }

      // Pins the inclusive edge of MagnitudeFloor (ShouldForm uses >=, not >): an event exactly at the
      // documented floor must still form, so a future retune of the comparison operator cannot silently
      // raise the effective bar above the constant's stated value.
      [Test]
      public void GIVEN_magnitude_at_the_floor_WHEN_deciding_formation_THEN_it_forms()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: GrudgePolicy.MagnitudeFloor)).Should().BeTrue();
      }

      // ── Formation ────────────────────────────────────────────────────────

      // Pairs with the at-the-floor test to lock both sides of the boundary: MagnitudeFloor exists so a
      // trivial slight (per the header comment, an anti-noise guardrail) never plants a grudge at all,
      // one point under the floor must refuse.
      [Test]
      public void GIVEN_magnitude_below_the_floor_WHEN_deciding_formation_THEN_it_refuses()
      {
         GrudgePolicy.ShouldForm(Candidate(magnitude: GrudgePolicy.MagnitudeFloor - 1)).Should().BeFalse();
      }

      // ── Per-NPC cap ──────────────────────────────────────────────────────

      // PerNpcCap's doc: the maximum number of a holder's live grudges that ever surface at once, "so the
      // system never reads as a bookkeeper's ledger." Without the Take(cap) + descending sort, a heavily
      // grudged NPC would list every slight ever committed in the prompt and the Grievances page, drowning
      // the wounds that actually matter under petty ones.
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

      // The other half of the pillar's master rule: an NPC who dislikes the target AND holds a grudge is
      // far harder to win over than either factor alone would suggest, disliking compounds on top of the
      // wound instead of the two axes being independent.
      [Test]
      public void GIVEN_negative_regard_for_the_target_WHEN_computing_rigidity_THEN_it_is_hard()
      {
         int rigid = GrudgePolicy.RigidityAt(-80, Candidate());

         rigid.Should().BeGreaterThan(GrudgePolicy.BaseRigidityAtNeutralRegard, "disliking the target on top of the grudge makes it far harder to win over");
      }

      // Defensive boundary: TryPlant call sites build a GrudgeCandidateFacts from live game state on
      // every candidate event, and a malformed one must be refused cleanly rather than throwing and
      // taking down whichever system (jealousy, scheme, quest) was trying to plant it.
      [Test]
      public void GIVEN_null_facts_WHEN_deciding_formation_THEN_it_refuses()
      {
         GrudgePolicy.ShouldForm(null).Should().BeFalse();
      }

      // ExecutedKin is called out by name in ResolutionKindFor's doc as always Unforgivable: an executed
      // kinsman's grudge should never offer an Amends/Gift/Deed/Time path, since RigidityAt already
      // forces this source to VendettaRigidity before the resolution table is even consulted.
      [Test]
      public void GIVEN_the_executed_kin_source_at_maximum_rigidity_WHEN_mapping_the_resolution_kind_THEN_it_is_unforgivable()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.ExecutedKin, GrudgePolicy.VendettaRigidity)
                     .Should().Be(GrudgeResolutionKind.Unforgivable);
      }

      // RigidityAt's doc: ExecutedKin is always maximum rigidity "regardless of regard or traits (a
      // vendetta)", checked before the regard/trait math runs at all. Even an NPC who deeply likes the
      // player cannot casually forgive the player executing their kin, the way the master rule would
      // otherwise let high regard soften any other source.
      [Test]
      public void GIVEN_the_executed_kin_source_WHEN_computing_rigidity_THEN_it_is_always_maximum_regardless_of_regard()
      {
         GrudgeCandidateFacts facts = Candidate(GrudgeSource.ExecutedKin);

         GrudgePolicy.RigidityAt(100, facts).Should().Be(GrudgePolicy.VendettaRigidity);
         GrudgePolicy.RigidityAt(-100, facts).Should().Be(GrudgePolicy.VendettaRigidity);
      }

      // EffectiveFormationMagnitude's doc: it is exposed specifically so ShouldForm and any future caller
      // (including migration) evaluate the SAME amplified value. This proves that promise end-to-end: an
      // event one point under MagnitudeFloor before amplification must actually cross the floor and form
      // once the possessive bonus is added, not get checked against the raw, unamplified magnitude.
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

      // The core decay behaviour every non-vendetta, non-never-decay grudge relies on: without it, a
      // years-old minor slight would keep tinting the NPC's tone in the prompt exactly as intensely as
      // the day it happened, instead of fading the way the CHANGELOG describes ("it softens over time").
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