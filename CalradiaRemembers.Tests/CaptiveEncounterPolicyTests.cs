// Code written by Gabriel Mailhot, 01/07/2026.

// This policy decides two things for every captive-encounter (CNC) scene: whether the captor acts on
// a given day (EncounterProbability) and which of six intents (Interrogation/PersonalDesire/Domination/
// Torture/Training/Reward) the scene takes (SelectIntent), plus the matching bandit-captor rolls (spoil
// chance, menace ladder, collective-scene chance, participant cap). CaptiveEncounterService.CheckAndTrigger
// feeds these results straight into the "your captor summons you" popup and the whole scene that follows.
// A wrong probability turns the feature into either constant unwanted interruption or one that never
// fires; a wrong intent draw can produce fiction a captor's own kinks/regard contradict (rewarding a
// prisoner they despise), or repeat the same scene every time, the pre-01/07 bug this file also guards.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveEncounterPolicyTests
   {
      // Without this outer cap, a long-held captive with several predatory kinks (base 0.60 + kink
      // bonuses) would clear 100%, turning an occasional forced scene into one that fires on
      // effectively every eligible day.
      [Test]
      public void GIVEN_a_capped_base_plus_bonuses_WHEN_computing_probability_THEN_the_total_is_capped()
      {
         //Arrange: base capped 0.60 + 0.20 bonus = 0.80, but the total caps at 0.70.
         CaptiveEncounterPolicy.EncounterProbability(10, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                               .Should().BeApproximately(0.70f, 0.0001f);
      }

      // ── Compatibility as a FILTER, not a wall (ratified 23/07/2026) ─────────
      // Orientation compatibility used to be an early return at the TOP of the profiled-captor path,
      // before SelectIntent was ever reached: a male player held by a straight male lord (the majority
      // captor pairing) got NO scenes for the whole captivity, and an asexual captor could not even
      // interrogate. The lazy nemesis attach (same day) made that path the common one and turned a
      // corner case into the default male-player experience. The gate now lives inside SelectIntent,
      // the exact shape SelectBanditIntent always had: incompatible only makes the SEXUAL intents
      // unreachable; the scene itself still happens, as an interrogation.

      // The heart of the ruling: an incompatible pairing still gets a scene, and it is the one intent
      // whose framing is non-sexual. Without this, "incompatible" silently meant "captivity goes mute".
      [Test]
      public void GIVEN_an_incompatible_player_WHEN_selecting_intent_THEN_the_scene_is_an_interrogation()
      {
         CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.FreeUse, Kink.Dominance), 0, _ => 0, playerIsCompatible: false)
                               .Should().Be(CaptiveSceneIntent.Interrogation);
      }

      // Torture stays on the GATED side by explicit design call (Gabriel 2026-07-23): at Hardcore its
      // scene framing is sexualised, so prudence keeps it unreachable for an incompatible pairing even
      // when the captor's own kinks (Sadism) would otherwise lean hard toward it. Every roll must land
      // on Interrogation, whatever the dice say.
      [Test]
      public void GIVEN_an_incompatible_player_and_a_sadistic_captor_WHEN_scanning_every_roll_THEN_torture_is_never_drawn()
      {
         for (var roll = 0; roll < 100; roll++)
         {
            int frozen = roll;
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism, Kink.ImpactPlay), -60, max => frozen % max, playerIsCompatible: false)
                                  .Should().Be(CaptiveSceneIntent.Interrogation);
         }
      }

      // The other half of the boundary: a COMPATIBLE pairing must be untouched by the new parameter,
      // or this fix would quietly rewrite every existing captive scene in the game. Same kinks, same
      // dice, same draw as before the parameter existed.
      [Test]
      public void GIVEN_a_compatible_player_WHEN_selecting_intent_THEN_the_weighted_draw_is_unchanged()
      {
         CaptiveSceneIntent before = CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), -60, _ => 0);
         CaptiveSceneIntent after = CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), -60, _ => 0, playerIsCompatible: true);

         after.Should().Be(before);
      }

      // ── Bandit captor rolls (added 01/07/2026) ──────────────────────────────

      // Pins the gender-weighted spoil-of-war branch: a female prisoner has a real (50%) chance of the
      // sexual PersonalDesire intent, versus a male prisoner's near-nil 3% (see the male-prisoner test
      // below), the asymmetry the bandit-captor scene is authored around.
      [Test]
      public void GIVEN_a_female_prisoner_and_a_low_roll_WHEN_selecting_bandit_intent_THEN_it_is_personal_desire()
      {
         //Arrange: female spoil chance is 50, so a roll of 0 always clears it.
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Heterosexual};

         CaptiveEncounterPolicy.SelectBanditIntent(true, romantic, _ => 0).Should().Be(CaptiveSceneIntent.PersonalDesire);
      }

      // ── EncounterProbability ───────────────────────────────────────────────

      // Pins the per-day growth rate itself: the encounter check must intensify the longer the player
      // stays captive, not stay flat, so captivity reads as escalating rather than a fixed coin-flip.
      [Test]
      public void GIVEN_a_few_days_and_no_kinks_WHEN_computing_probability_THEN_it_climbs_with_the_days()
      {
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks()).Should().BeApproximately(0.45f, 0.0001f);
      }

      // Pins that only the curated predatory kinks (PowerImbalance/Dominance/Sadism/FreeUse/
      // MasterSlave) inflate encounter frequency: a gentler kink like Praise must not make a captor
      // trigger forced scenes any more often than one with no kinks at all.
      [Test]
      public void GIVEN_a_kink_that_carries_no_bonus_WHEN_computing_probability_THEN_only_the_base_applies()
      {
         //Arrange: Praise is not one of the predatory bonus kinks.
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.Praise)).Should().BeApproximately(0.45f, 0.0001f);
      }

      // Guards a vanilla-consistency break a tester caught: the chief's men joining a collective scene
      // must never outnumber who actually survived the capture. With only the chief left alive, there
      // is nobody else to conjure into the scene.
      [Test]
      public void GIVEN_a_lone_fighter_WHEN_capping_collective_participants_THEN_no_extra_bandit_can_join()
      {
         //Arrange: the chief IS the one fighter that survived, so there is nobody left to add.
         CaptiveEncounterPolicy.BanditCollectiveParticipantCap(1).Should().Be(0);
      }

      // Pins the base cap independently of the kink-bonus cap tested above: 10 days at 0.15/day would
      // be 1.5 (150%), a meaningless probability that would make the encounter fire on essentially
      // every eligible tick once captivity runs long, regardless of the captor's kinks.
      [Test]
      public void GIVEN_a_long_captivity_WHEN_computing_probability_THEN_the_base_caps()
      {
         //Arrange: 10 days would be 1.5, but the base is capped at 0.60.
         CaptiveEncounterPolicy.EncounterProbability(10, Kinks()).Should().BeApproximately(0.60f, 0.0001f);
      }

      // Pins the other half of the gender-weighted spoil asymmetry (see the female-prisoner test
      // above): a male prisoner almost never draws the sexual PersonalDesire intent and instead falls
      // to the non-sexual menace ladder.
      [Test]
      public void GIVEN_a_male_prisoner_WHEN_selecting_bandit_intent_THEN_the_spoil_chance_is_near_nil()
      {
         //Arrange: male spoil chance is 3, so a roll of 3 already misses it (falls to the menace ladder).
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Bisexual};

         CaptiveEncounterPolicy.SelectBanditIntent(false, romantic, _ => 3).Should().NotBe(CaptiveSceneIntent.PersonalDesire);
      }

      // Pins the low end of the collective-scene asymmetry: a non-sexual bandit scene (extortion /
      // intimidation / revenge) stays mostly private, only a 25% chance the chief's men join in.
      [Test]
      public void GIVEN_a_non_desire_scene_WHEN_reading_the_collective_chance_THEN_it_is_25()
      {
         CaptiveEncounterPolicy.BanditCollectiveChance(CaptiveSceneIntent.Extortion).Should().Be(25);
      }

      // Pins that a spoil-of-war (PersonalDesire) scene draws the chief's men in far more often (55%)
      // than a shakedown (25%, tested above): a captive handed to the band is deliberately not a
      // private scene the way a robbery is.
      [Test]
      public void GIVEN_a_personal_desire_scene_WHEN_reading_the_collective_chance_THEN_it_is_55()
      {
         CaptiveEncounterPolicy.BanditCollectiveChance(CaptiveSceneIntent.PersonalDesire).Should().Be(55);
      }

      // Pins that a Praise kink only SHARPENS Reward once relation has already opened the door to it
      // (the relation gate tested below), never substitutes for warmth: the kink alone must not make a
      // captor reward a prisoner they do not actually favor.
      [Test]
      public void GIVEN_a_praise_kink_and_warm_regard_WHEN_computing_the_total_weight_THEN_it_is_larger_than_without_the_praise_kink()
      {
         //Arrange: the praise kink should sharpen (not merely allow) reward once it is reachable.
         int withoutPraise = CaptureTotal(Kinks(), 20);
         int withPraise = CaptureTotal(Kinks(Kink.Praise), 20);

         withPraise.Should().BeGreaterThan(withoutPraise);
      }

      // Pins the fix for the pre-01/07 bug where intent selection was a deterministic priority ladder
      // and a sadist ALWAYS ran Torture (per ROADMAP: "the same captor used to always run the same
      // scene"). Interrogation is drawn first and keeps a floor weight, so even a sadist's worst-case
      // roll can still land there.
      [Test]
      public void GIVEN_a_sadistic_captor_and_neutral_regard_WHEN_the_roll_lands_at_the_bottom_of_the_range_THEN_it_is_still_interrogation_not_torture()
      {
         //Arrange: Interrogation always keeps a floor weight and is drawn first, so even a sadist's
         //         captive scene is not FORCED into torture: a roll of 0 lands on the floor band.
         CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), 0, _ => 0)
                               .Should().Be(CaptiveSceneIntent.Interrogation);
      }

      // Complements the floor test above: the weighted draw must still genuinely LEAN toward the
      // matching kink at the far end of the roll range, or the "weighted, not deterministic" fix would
      // have swung too far and made Sadism meaningless to the outcome.
      [Test]
      public void GIVEN_a_sadistic_captor_and_neutral_regard_WHEN_the_roll_lands_at_the_top_of_the_range_THEN_it_leans_torture()
      {
         //Arrange: capture the total weight first, then draw the very top of the range. With no
         //         Training/Reward weight (kink absent, relation neutral) the heaviest boosted band,
         //         Torture, sits last and absorbs the top roll.
         int total = CaptureTotal(Kinks(Kink.Sadism), 0);

         CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), 0, _ => total - 1)
                               .Should().Be(CaptiveSceneIntent.Torture);
      }

      // Same vanilla-consistency guard as the lone-fighter case above: pins the cap formula
      // (fighters - 1) right at the boundary above it, one survivor beyond the chief means one extra
      // participant, not more.
      [Test]
      public void GIVEN_a_two_fighter_band_WHEN_capping_collective_participants_THEN_one_extra_bandit_can_join()
      {
         CaptiveEncounterPolicy.BanditCollectiveParticipantCap(2).Should().Be(1);
      }

      // Stress-tests the "no valid intent is ever impossible for a given captor" invariant under the
      // most hostile combination the game can produce: if a future tuning change (e.g. a bigger
      // contempt/torture boost) ever let one intent's weight swamp the floor weights of the others to
      // zero, this is what would catch it.
      [Test]
      public void GIVEN_an_extreme_kink_and_regard_combination_WHEN_scanning_every_possible_roll_THEN_all_four_floor_intents_stay_reachable()
      {
         //Arrange: a maximally hostile draw (deep contempt, every torture/domination/desire kink
         //         stacked) must still leave Interrogation, PersonalDesire, Domination AND Torture
         //         all reachable: no valid intent is ever impossible for a given captor.
         List<Kink> kinks = Kinks(Kink.Sadism, Kink.ImpactPlay, Kink.FearPlay, Kink.PowerImbalance, Kink.Dominance,
            Kink.MasterSlave, Kink.Objectification, Kink.Degradation, Kink.FreeUse, Kink.Breeding,
            Kink.OrgasmControl, Kink.PetPlay);

         List<CaptiveSceneIntent> outcomes = AllOutcomes(kinks, -15);

         outcomes.Should().Contain(CaptiveSceneIntent.Interrogation);
         outcomes.Should().Contain(CaptiveSceneIntent.PersonalDesire);
         outcomes.Should().Contain(CaptiveSceneIntent.Domination);
         outcomes.Should().Contain(CaptiveSceneIntent.Torture);
      }

      // Pins that the spoil-of-war branch respects the captor's authored orientation even for a
      // faceless, synthesized bandit: a heterosexual male captor with a male prisoner must fall through
      // to the non-sexual menace ladder, never be forced into an orientation-incompatible scene.
      [Test]
      public void GIVEN_an_incompatible_orientation_WHEN_selecting_bandit_intent_THEN_it_falls_to_the_menace_ladder()
      {
         //Arrange: male prisoner, roll clears the 3% spoil threshold, but a heterosexual male captor
         //         is not compatible with a male prisoner, so the branch is skipped.
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Heterosexual};

         CaptiveEncounterPolicy.SelectBanditIntent(false, romantic, _ => 0).Should().Be(CaptiveSceneIntent.Extortion);
      }

      // Pins that a roll landing AT or above the collective-chance threshold does not tip the scene
      // collective: the comparison is strictly roll < chance, not <=, an edge a retune could silently
      // flip.
      [Test]
      public void GIVEN_extra_bandits_and_a_roll_at_or_above_the_chance_WHEN_deciding_whether_the_scene_is_collective_THEN_it_is_not()
      {
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.Extortion, 2, _ => 25).Should().BeFalse();
      }

      // Confirms the collective branch is actually reachable once an extra bandit exists and the roll
      // clears the threshold, the counterpart boundary to the test above.
      [Test]
      public void GIVEN_extra_bandits_and_a_roll_under_the_chance_WHEN_deciding_whether_the_scene_is_collective_THEN_it_is()
      {
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.PersonalDesire, 2, _ => 0).Should().BeTrue();
      }

      // Pins that Reward needs an actual positive relation (>= 15), not merely "not hostile": a captor
      // who is only mildly annoyed, not contemptuous, must still never reward the prisoner.
      [Test]
      public void GIVEN_mildly_negative_regard_WHEN_scanning_every_possible_roll_THEN_reward_is_never_drawn()
      {
         AllOutcomes(Kinks(), -1).Should().NotContain(CaptiveSceneIntent.Reward);
      }

      // Pins the short-circuit: with zero possible extra bandits the roll must never be drawn at all
      // (proven by a roll function that throws), preserving the single-draw behaviour this logic was
      // extracted from during the 01/07 refactor out of CaptiveEncounterService.
      [Test]
      public void GIVEN_no_extra_bandits_WHEN_deciding_whether_the_scene_is_collective_THEN_it_never_rolls_and_stays_solo()
      {
         //Arrange: the roll function throws if called, proving the && short-circuits.
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.PersonalDesire, 0, _ => throw new Exception("should not roll"))
                               .Should().BeFalse();
      }

      // Pins that Training is a true GATE, unlike the floor-weighted intents above: without the
      // Training kink there is nothing to condition, so the scene must never be drawn as one.
      [Test]
      public void GIVEN_no_training_kink_WHEN_scanning_every_possible_roll_THEN_training_is_never_drawn()
      {
         AllOutcomes(Kinks(Kink.Sadism, Kink.Praise), 30).Should().NotContain(CaptiveSceneIntent.Training);
      }

      // ── SelectIntent weighted draw (rewritten 01/07/2026) ───────────────────

      // Guards a real case: a captor's Romantic profile (or its Kinks list) can still be null when this
      // fires, e.g. before it is lazily backfilled. A crash here would silently break the whole daily
      // captive-encounter tick rather than just skip the kink bonus.
      [Test]
      public void GIVEN_null_kinks_WHEN_computing_probability_THEN_only_the_base_applies()
      {
         CaptiveEncounterPolicy.EncounterProbability(2, null).Should().BeApproximately(0.30f, 0.0001f);
      }

      // Pins the witness-count floor: with only one extra bandit, exactly one appears onscreen, never
      // zero and never padded above what actually exists.
      [Test]
      public void GIVEN_one_extra_bandit_WHEN_counting_bandit_witnesses_THEN_only_one_is_shown()
      {
         CaptiveEncounterPolicy.BanditWitnessCount(1).Should().Be(1);
      }

      // Pins that multiple predatory kinks stack additively rather than capping at one: a captor with
      // both PowerImbalance and FreeUse is meaningfully more likely to act than one with either alone.
      [Test]
      public void GIVEN_predatory_kinks_WHEN_computing_probability_THEN_their_bonuses_add()
      {
         //Arrange: 3 days (0.45) + PowerImbalance (0.10) + FreeUse (0.10) = 0.65.
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                               .Should().BeApproximately(0.65f, 0.0001f);
      }

      // Pins the explicit contempt override (relation <= -15 forces Reward to 0), distinct from the
      // softer relation gate tested above: even the Praise kink, which sharpens Reward once it is
      // reachable, cannot revive it once real contempt has zeroed it out.
      [Test]
      public void GIVEN_strong_contempt_WHEN_scanning_every_possible_roll_THEN_reward_is_never_drawn()
      {
         AllOutcomes(Kinks(Kink.Praise), -15).Should().NotContain(CaptiveSceneIntent.Reward);
      }

      // Pins the top band of the non-sexual menace ladder: a roll of 75 or more escalates a bandit
      // scene from mere intimidation to outright revenge/violence.
      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_75_or_more_THEN_it_is_revenge()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99
               : 90;
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Revenge);
      }

      // Pins the middle band's edges (45..74): a retune of either boundary would silently reshuffle how
      // often a bandit scene reads as a threat versus a plain shakedown or outright revenge.
      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_between_45_and_74_THEN_it_is_intimidation()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99 // misses the spoil chance
               : 60; // lands in the intimidation band
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Intimidation);
      }

      // Pins the floor of the menace ladder: the most common outcome, a plain shakedown, is whatever
      // lands below the 45 threshold tested above.
      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_under_45_THEN_it_is_extortion()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99 // misses the spoil chance
               : 44; // lands in the extortion band
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Extortion);
      }

      // Confirms the other side of the Training gate tested above: the kink alone is enough to make the
      // intent reachable across the roll range, no relation requirement gates it the way Reward is
      // gated.
      [Test]
      public void GIVEN_the_training_kink_WHEN_scanning_every_possible_roll_THEN_training_is_reachable()
      {
         AllOutcomes(Kinks(Kink.Training), 0).Should().Contain(CaptiveSceneIntent.Training);
      }

      // Pins the display cap: never more than 2 bandit witnesses appear onscreen regardless of how many
      // could join, the same 2-witness cap used for lord witnesses elsewhere in the mod, so a
      // collective scene never crowds the chat window.
      [Test]
      public void GIVEN_three_extra_bandits_WHEN_counting_bandit_witnesses_THEN_it_is_capped_at_two()
      {
         CaptiveEncounterPolicy.BanditWitnessCount(3).Should().Be(2);
      }

      // Confirms the far side of the Reward relation gate: real warmth (>= 20) is what actually opens
      // the door, which mild positivity (below the 15 threshold tested above) does not.
      [Test]
      public void GIVEN_warm_regard_WHEN_scanning_every_possible_roll_THEN_reward_becomes_reachable()
      {
         AllOutcomes(Kinks(), 20).Should().Contain(CaptiveSceneIntent.Reward);
      }

      #region private

      /// <summary>Every distinct intent SelectIntent can draw for this kink/relation pair, scanned across the whole roll range.</summary>
      private static List<CaptiveSceneIntent> AllOutcomes(List<Kink> kinks, int relation)
      {
         int total = CaptureTotal(kinks, relation);
         var outcomes = new List<CaptiveSceneIntent>();

         for (var roll = 0; roll < total; roll++)
         {
            CaptiveSceneIntent outcome = CaptiveEncounterPolicy.SelectIntent(kinks, relation, _ => roll);
            if (!outcomes.Contains(outcome)) outcomes.Add(outcome);
         }

         return outcomes;
      }

      /// <summary>Captures the total weight SelectIntent rolled against, by returning it straight back out of the roll seam.</summary>
      private static int CaptureTotal(List<Kink> kinks, int relation)
      {
         var total = 0;
         CaptiveEncounterPolicy.SelectIntent(kinks, relation, max =>
         {
            total = max;

            return 0;
         });

         return total;
      }

      private static List<Kink> Kinks(params Kink[] kinks) => new(kinks);

      #endregion
   }
}