// Code written by Gabriel Mailhot, 20/06/2026.
// The dramatic structure of a captive scene (Intro/RisingTension/Initiate/Intensify/Climax/Aftermath/Conclude,
// across one or more "acts") is owned entirely by this state machine so the LLM never has to invent pacing and
// never loops the same beat. It decides three things every turn: which stage plays next, who acts (the lead
// captor, another single witness, or the whole group at once), and when the scene is actually over. Get the
// sequence wrong and a scene either stalls (never reaches Conclude, the player stuck with no way out) or skips
// its come-down beat (Aftermath) straight to Conclude with no closure narration. The hard MaxActs cap is the
// last-resort guarantee every scene terminates even when every roll favours "another round" (this whole arc
// draw exists because the same captor used to retell the identical scene every time).

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveSceneDirectorTests
   {
      // A single additional witness's turn must be a SHORT sub-act, no Intensify: pins the "compressed" shape
      // (Initiate then straight to Climax) that keeps a lone extra witness's turn shorter than the lead's own
      // fuller act, per DecideNextAct's AnotherSingle branch.
      [Test]
      public void GIVEN_a_collective_scene_with_one_other_WHEN_they_take_their_turn_THEN_their_act_is_initiate_then_climax()
      {
         //Arrange
         // one other, the per-member roll succeeds (AlwaysLow → 0 < 70) → AnotherSingle queued.
         var director = new CaptiveSceneDirector(true, 1, StubRandomSource.AlwaysLow, false, CaptiveSceneIntent.Interrogation);

         //Act
         AdvancePastLeadArc(director);

         //Assert
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.AnotherSingle);
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate);

         //Act
         director.AdvanceStory();

         //Assert
         director.CurrentStage.Should().Be(CaptiveSceneStage.Climax); // a single other's act has no Intensify
      }

      // ── Collective aggressors ──────────────────────────────────────────────

      // GroupTogether only exists when 2+ others are present (QueueOthers gates it on others >= 2): pins that
      // the "together" roll can actually fire and produce the fuller [Initiate, Intensify, Climax] sub-act,
      // distinct from the compressed sequential-singles path a lone extra witness always takes.
      [Test]
      public void GIVEN_a_collective_scene_with_two_others_WHEN_the_dice_pick_together_THEN_after_the_lead_a_group_act_opens()
      {
         //Arrange
         // others>=2 and the "together" roll succeeds (AlwaysLow → 0 < 50).
         var director = new CaptiveSceneDirector(true, 2, StubRandomSource.AlwaysLow, false, CaptiveSceneIntent.Interrogation);

         //Act
         AdvancePastLeadArc(director);

         //Assert
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.GroupTogether);
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate);
         director.IsConcluding.Should().BeFalse();
      }

      // Aftermath is a real, normal beat the player advances through with Continue, never the terminal itself
      // (only the FOLLOWING Continue reaches Conclude); once Conclude IS reached, further advances must be a
      // no-op, or the player could be shown the come-down narration twice or bounce between the two stages.
      [Test]
      public void GIVEN_a_concluding_scene_WHEN_advance_is_requested_THEN_it_stays_concluded()
      {
         //Arrange — past the lead's Climax, the dice say stop: the tail is Aftermath, then Conclude.
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.AlwaysHigh, false, CaptiveSceneIntent.Interrogation);
         AdvancePastLeadArc(director);
         director.CurrentStage.Should().Be(CaptiveSceneStage.Aftermath);
         director.IsConcluding.Should().BeFalse("Aftermath is a normal beat, not the terminal");

         //Act — the beat after Aftermath is the true terminal.
         director.AdvanceStory();

         //Assert
         director.IsConcluding.Should().BeTrue();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);

         //Act — advancing further stays concluded.
         director.AdvanceStory();

         //Assert
         director.IsConcluding.Should().BeTrue();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);
      }

      // Pins the baseline every scene must start from; a wrong default (e.g. starting mid-concluded, or with
      // a stale intervention flag carried from construction) would corrupt the very first beat shown.
      [Test]
      public void GIVEN_a_new_scene_WHEN_created_THEN_the_opening_beat_is_the_leads_intro()
      {
         //Arrange/Act
         CaptiveSceneDirector director = Solo();

         //Assert
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intro);
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.Lead);
         director.IsConcluding.Should().BeFalse();
         director.ReactToIntervention.Should().BeFalse();
      }

      // THE anti-loop guarantee. Without MaxActs, a captor whose dice always say "another round" would never
      // reach Conclude, and the player would have no way out of the scene via Continue alone.
      [Test]
      public void GIVEN_a_scene_that_always_rolls_continue_WHEN_advanced_relentlessly_THEN_it_still_concludes_within_the_act_cap()
      {
         //Arrange
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.AlwaysLow, false, CaptiveSceneIntent.Interrogation);

         //Act
         // Even with the dice always favouring another round, the hard act cap must end the scene.
         for (var i = 0; i < 50 && !director.IsConcluding; i++)
            director.AdvanceStory();

         //Assert
         director.IsConcluding.Should().BeTrue("the MaxActs cap guarantees termination");
      }

      // A repeated act by the same lead must skip Intro: only the very first act of a scene carries the
      // "walking in" build-up. Getting this wrong would replay the entrance narration mid-scene, as if the
      // captor left and came back in.
      [Test]
      public void GIVEN_a_solo_scene_past_the_lead_climax_WHEN_the_dice_say_continue_THEN_a_new_lead_act_opens_at_initiate()
      {
         //Arrange
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.AlwaysLow, false, CaptiveSceneIntent.Interrogation); // always rolls "continue"

         //Act
         AdvancePastLeadArc(director);

         //Assert
         director.IsConcluding.Should().BeFalse();
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.Lead);
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate); // a fresh act carries no Intro
      }

      // ── Concluding after the lead act ──────────────────────────────────────

      // Pins that "stop" lands on Aftermath first, not straight on Conclude: skipping the come-down beat would
      // cut the scene off with no closure narration, exactly the gap Aftermath was added to close (the
      // prisoner's state, parting words, the room going cold again, before the cell).
      [Test]
      public void GIVEN_a_solo_scene_past_the_lead_climax_WHEN_the_dice_say_stop_THEN_the_scene_concludes()
      {
         //Arrange
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.AlwaysHigh, false, CaptiveSceneIntent.Interrogation); // never rolls "continue"

         //Act
         AdvancePastLeadArc(director); // Intro→…→Climax→(decide)

         //Assert — the decide step lands on Aftermath first, not Conclude directly.
         director.IsConcluding.Should().BeFalse();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Aftermath);
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.Lead);

         //Act — one more Continue reaches the true terminal.
         director.AdvanceStory();

         //Assert
         director.IsConcluding.Should().BeTrue();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);
         director.CurrentAggressor.Should().Be(CaptiveAggressorKind.Lead);
      }

      // ReactToIntervention must be a one-shot per-beat signal. If it stuck across AdvanceStory calls, the
      // captor would keep reacting to a player intervention that has already been addressed and moved past.
      [Test]
      public void GIVEN_an_intervention_was_flagged_WHEN_the_story_advances_THEN_the_intervention_flag_clears()
      {
         //Arrange
         CaptiveSceneDirector director = Solo();

         //Act
         director.DevelopInPlace();

         //Assert
         director.ReactToIntervention.Should().BeTrue();

         //Act
         director.AdvanceStory();

         //Assert
         director.ReactToIntervention.Should().BeFalse();
      }
      // ── Construction & guards ──────────────────────────────────────────────

      // The director's determinism/testability depends entirely on an injected IRandomSource (every branch
      // arranged deterministically in tests, per the class doc). A null source must fail loudly at
      // construction, not NRE unpredictably deep inside a later roll, mid-scene.
      [Test]
      public void Given_NoRandomSource_When_Constructing_Then_ItThrows()
      {
         //Arrange/Act
         Func<CaptiveSceneDirector> act = () => new CaptiveSceneDirector(false, 0, null!, false, CaptiveSceneIntent.Interrogation);

         //Assert
         act.Should().Throw<ArgumentNullException>();
      }

      // ── The opening arc ────────────────────────────────────────────────────

      // SlowBurn is the fix for a specific gap: a lord scene under Domination/Training used to skip straight
      // to the act, with no "play with the prey" psychological build-up. RisingTension only belongs to the
      // OPENING act; every arc, however it opens, must still converge on the same Aftermath-then-Conclude tail.
      [Test]
      public void GIVEN_a_context_that_draws_the_slowburn_arc_WHEN_walked_THEN_rising_tension_precedes_initiate_and_the_tail_is_aftermath_then_conclude()
      {
         //Arrange — lord + Domination, roll 60 lands in the SlowBurn band (Classic[0,20) / SlowBurn[20,70) / Sudden[70,80)),
         //          and 60 also misses the 55 ContinueChance threshold so the scene stops after the one act.
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.Returning(60), false, CaptiveSceneIntent.Domination);

         //Assert — the opening act is SlowBurn: Intro, then RisingTension BEFORE the act begins.
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intro);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.RisingTension);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intensify);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Climax);

         //Act — past Climax, the dice say stop: the tail is Aftermath (a normal beat), then Conclude.
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Aftermath);
         director.IsConcluding.Should().BeFalse();

         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);
         director.IsConcluding.Should().BeTrue();
      }

      // The blunt end of the same variety fix: a bandit-leaning arc with no build-up at all (no RisingTension,
      // no Intensify), so the same captor archetype does not always run the identical pacing every visit; it
      // still converges on the same universal tail as every other arc.
      [Test]
      public void GIVEN_a_context_that_draws_the_sudden_arc_WHEN_walked_THEN_it_skips_straight_to_climax_and_the_tail_is_aftermath_then_conclude()
      {
         //Arrange — lord + Domination, roll 75 lands in the Sudden band [70,80) and also misses ContinueChance.
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.Returning(75), false, CaptiveSceneIntent.Domination);

         //Assert — the opening act is Sudden: no RisingTension, no Intensify.
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intro);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Climax);

         //Act — same Aftermath-then-Conclude tail as every other arc.
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Aftermath);
         director.IsConcluding.Should().BeFalse();

         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);
         director.IsConcluding.Should().BeTrue();
      }

      // Combines the anti-loop cap with the Aftermath requirement: even when MaxActs forces termination, the
      // scene must still be routed through the come-down beat, never bypass it as a shortcut just because the
      // cap, not the dice, is what ended the scene.
      [Test]
      public void GIVEN_a_relentless_bandit_scene_WHEN_advanced_past_the_act_cap_THEN_it_still_reaches_conclude_via_aftermath()
      {
         //Arrange — dice always favour "another round", so only the hard MaxActs cap stops the scene;
         //          the terminal must still be reached through the Aftermath tail, not skip it.
         var director = new CaptiveSceneDirector(false, 0, StubRandomSource.AlwaysLow, true, CaptiveSceneIntent.Torture);

         //Act
         for (var i = 0; i < 50 && director.CurrentStage != CaptiveSceneStage.Conclude; i++)
            director.AdvanceStory();

         //Assert
         director.CurrentStage.Should().Be(CaptiveSceneStage.Conclude);
         director.IsConcluding.Should().BeTrue();
      }

      // Pins the baseline "Classic" shape (the structure every scene used before the SlowBurn/Sudden variety
      // work): this is the reference arc the other two are deliberate variations OF, so it must keep working
      // unchanged.
      [Test]
      public void GIVEN_the_lead_act_WHEN_advanced_in_turn_THEN_it_walks_intro_initiate_intensify_climax()
      {
         //Arrange/Act
         CaptiveSceneDirector director = Solo();

         //Assert
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intro);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Intensify);
         director.AdvanceStory();
         director.CurrentStage.Should().Be(CaptiveSceneStage.Climax);
         director.IsConcluding.Should().BeFalse();
      }

      // ── "Try something else" (develop in place) ────────────────────────────

      // "The player alone decides when the story moves on, via Continue" (production doc): DevelopInPlace must
      // NEVER auto-advance the stage, or a player typing an exploratory action would involuntarily skip ahead
      // in the scene's structure.
      [Test]
      public void GIVEN_the_player_tries_something_else_WHEN_developing_in_place_THEN_the_stage_holds_and_intervention_is_flagged()
      {
         //Arrange
         CaptiveSceneDirector director = Solo();
         director.AdvanceStory(); // at Initiate

         //Act
         director.DevelopInPlace();

         //Assert
         director.CurrentStage.Should().Be(CaptiveSceneStage.Initiate); // did NOT advance
         director.ReactToIntervention.Should().BeTrue();
      }

      #region private

      /// <summary>Walks the four beats of the lead's opening act and then triggers the next-act decision.</summary>
      private static void AdvancePastLeadArc(CaptiveSceneDirector director)
      {
         director.AdvanceStory(); // Initiate
         director.AdvanceStory(); // Intensify
         director.AdvanceStory(); // Climax
         director.AdvanceStory(); // decide next act
      }

      // ── Helpers ────────────────────────────────────────────────────────────

      private static CaptiveSceneDirector Solo() => new(false, 0, StubRandomSource.AlwaysHigh, false, CaptiveSceneIntent.Interrogation);

      #endregion
   }
}