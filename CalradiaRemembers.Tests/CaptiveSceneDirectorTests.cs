// Code written by Gabriel Mailhot, 20/06/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveSceneDirectorTests
   {
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

      [Test]
      public void Given_NoRandomSource_When_Constructing_Then_ItThrows()
      {
         //Arrange/Act
         Func<CaptiveSceneDirector> act = () => new CaptiveSceneDirector(false, 0, null!, false, CaptiveSceneIntent.Interrogation);

         //Assert
         act.Should().Throw<ArgumentNullException>();
      }

      // ── The opening arc ────────────────────────────────────────────────────

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