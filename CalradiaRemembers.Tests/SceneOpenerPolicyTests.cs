// Code written by Gabriel Mailhot, 22/10/2026.
// Lordfadooboo (Nexus, 21/10/2026) asked that a conversation not open cold; Gabriel (22/10/2026): the character sets the
// scene by default, as captive scenes already do. This decides when an opening is the character's own scene-setting turn
// and what becomes of the canned greeting. If it is wrong, a staged scene (an interception, a tale meeting) opens twice,
// a conversation picked up from earlier today restarts as if the player had just walked up, a player who turned the
// switch off still pays the call, or a failed call leaves the character with no greeting at all.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SceneOpenerPolicyTests
   {
      private static readonly SceneOpenerFacts Ordinary = new() {SettingOn = true, OpenedByGreeting = true, ModelAvailable = true};

      // STAKE: Lordfadooboo's cold open: the player walks up and the character sets the scene before the first word.
      [Test]
      public void GIVEN_an_ordinary_conversation_opened_by_greeting_WHEN_it_opens_THEN_the_character_sets_the_scene()
      {
         SceneOpenerPolicy.ShouldOpen(Ordinary).Should().BeTrue();
      }

      // STAKE: a player who turned the switch off for cost (froggyluv, 20/10/2026) never pays the extra call.
      [Test]
      public void GIVEN_the_switch_off_WHEN_a_conversation_opens_THEN_the_canned_greeting_alone_opens_it()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = false, OpenedByGreeting = true, ModelAvailable = true}).Should().BeFalse();
      }

      // STAKE: an interception, a tale meeting or any staged scene has its own opening: a second one would greet twice.
      [Test]
      public void GIVEN_a_conversation_not_opened_by_the_greeting_WHEN_it_opens_THEN_no_scene_is_added()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = false, ModelAvailable = true}).Should().BeFalse();
      }

      // STAKE: a sitting kept from earlier today goes on where it stopped (fkasad, 20/09/2026); it is not a new arrival.
      [Test]
      public void GIVEN_a_sitting_put_back_from_earlier_today_WHEN_it_opens_THEN_no_scene_is_set()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = true, ModelAvailable = true, ResumedSitting = true})
                          .Should().BeFalse();
      }

      // STAKE: after a wanderer's backstory in the game's own dialogue, the two are already talking: no "as you came up".
      [Test]
      public void GIVEN_game_dialogue_carried_into_the_chat_WHEN_it_opens_THEN_no_scene_is_set()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = true, ModelAvailable = true, VanillaLinesCarried = true})
                          .Should().BeFalse();
      }

      // STAKE: Gabriel 28/11/2026 ("toujours une intro ... remplacer l'injection de la conversation Vanille"): with the composed
      // intro on, his chat with Monchug opens on the intro, even with the game's lines carried and the scene switch off.
      [Test]
      public void GIVEN_the_composed_intro_WHEN_game_lines_were_carried_and_the_scene_switch_is_off_THEN_the_intro_still_opens_the_chat()
      {
         var facts = new SceneOpenerFacts {SettingOn = false, OpenedByGreeting = true, ModelAvailable = true, VanillaLinesCarried = true, ComposedIntro = true};

         SceneOpenerPolicy.ShouldOpen(facts).Should().BeTrue();
         SceneOpenerPolicy.IntroReplacesGameLines(facts).Should().BeTrue();
      }

      // STAKE: without the composed intro, the game's lines stay on screen as they always were (the default must not move).
      [Test]
      public void GIVEN_no_composed_intro_WHEN_game_lines_were_carried_THEN_they_stay_on_screen()
      {
         SceneOpenerPolicy.IntroReplacesGameLines(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = true, ModelAvailable = true}).Should().BeFalse();
      }

      // STAKE: a tale meeting or any staged scene keeps its own opening and lines: the intro never stands in for them.
      [Test]
      public void GIVEN_the_composed_intro_WHEN_the_scene_is_staged_THEN_no_intro_and_the_lines_stay()
      {
         var facts = new SceneOpenerFacts {OpenedByGreeting = false, ModelAvailable = true, ComposedIntro = true};

         SceneOpenerPolicy.ShouldOpen(facts).Should().BeFalse();
         SceneOpenerPolicy.IntroReplacesGameLines(facts).Should().BeFalse();
      }

      // STAKE: Gabriel 28/11/2026, "je pourrais rencontrer le lord sur la MAP et ensuite dans sa ville": a sitting put back from
      // earlier today opens on an intro of where they meet now; without the composed intro it goes on where it stopped, as before.
      [Test]
      public void GIVEN_a_sitting_put_back_from_earlier_today_WHEN_the_composed_intro_is_on_THEN_it_opens_on_the_intro()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {OpenedByGreeting = true, ResumedSitting = true, ModelAvailable = true, ComposedIntro = true}).Should().BeTrue();
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = true, ResumedSitting = true, ModelAvailable = true}).Should().BeFalse();
      }

      // STAKE: with no model to call, an error line would greet the player instead of the character.
      [Test]
      public void GIVEN_no_model_available_WHEN_a_conversation_opens_THEN_no_scene_is_asked()
      {
         SceneOpenerPolicy.ShouldOpen(new SceneOpenerFacts {SettingOn = true, OpenedByGreeting = true, ModelAvailable = false}).Should().BeFalse();
      }

      // STAKE: the greeting gives way only to a scene that arrived; a failed call or a player who spoke first leaves the
      // character greeting him, never an error line or an empty chat.
      [TestCase(SceneOpenerOutcome.Delivered, false)]
      [TestCase(SceneOpenerOutcome.Failed, true)]
      [TestCase(SceneOpenerOutcome.Abandoned, true)]
      [TestCase(SceneOpenerOutcome.Composed, true)]
      public void GIVEN_how_the_scene_turn_ended_WHEN_the_greeting_is_settled_THEN_it_stands_unless_the_scene_arrived(SceneOpenerOutcome outcome, bool stands)
      {
         SceneOpenerPolicy.GreetingStands(outcome).Should().Be(stands);
      }

      // STAKE (Stage 3, 28/11/2026): a composed opening already set the scene above the greeting; the player's first turn
      // asking for an opening tableau again would describe the hall twice. A greeting that stood alone still owes it.
      [TestCase(SceneOpenerOutcome.Delivered, false)]
      [TestCase(SceneOpenerOutcome.Composed, false)]
      [TestCase(SceneOpenerOutcome.Failed, true)]
      [TestCase(SceneOpenerOutcome.Abandoned, true)]
      public void GIVEN_how_the_scene_turn_ended_WHEN_the_next_turn_is_built_THEN_it_owes_the_opening_only_if_no_scene_was_set(SceneOpenerOutcome outcome, bool owed)
      {
         SceneOpenerPolicy.OpeningNarrationOwed(outcome).Should().Be(owed);
      }

      // STAKE: the direction never lets the scene invent what the player can check, nor act for him.
      [Test]
      public void GIVEN_the_stage_direction_WHEN_it_is_read_THEN_it_forbids_invented_people_deeds_and_speaking_for_the_player()
      {
         SceneOpenerPolicy.Cue.Should().Contain("no one who is not present").And.Contain("Do not speak or act for the player")
                          .And.Contain("do not take any action");
      }
   }
}
