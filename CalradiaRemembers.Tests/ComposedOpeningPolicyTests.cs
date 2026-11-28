// Code written by Gabriel Mailhot, 28/11/2026.
// The composed intro (Stage 3 of the group-scene plan). Gabriel, 28/11/2026: "au début de chaque CHAT on ajoute l'intro";
// the composer "chooses among true facts and stages them; it invents nothing the game tracks". This decides which openings
// are composed, what the composer and the checker read, and how their replies are read. If it is wrong: an envoy, a captive
// scene or a second talk the same day gets a fresh tableau; a player who turned Scene Narration off pays for a paragraph
// nobody sees; a wrapped paragraph is cut to its first line; a check that said nothing lets an invented raid through; or
// the composer is told the player brings companions who are not there.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ComposedOpeningPolicyTests
   {
      private static readonly ComposedOpeningFacts Ordinary = new() {SwitchOn = true, SceneNarrationOn = true};

      private static ComposedOpeningSheet Sheet(params string[] present) => new() {
         NpcName = "Alympia", PlayerName = "Arwa", Place = "the governor's hall in the keep of Poros", Season = "spring",
         TimeOfDay = "morning", LivedLately = new[] {"yesterday: Fought at Arwa's side near Poros and put the Battanians to flight."},
         RealmNews = new[] {"The Empire is at war with Battania."}, PlayerGear = "Arwa wears a mail hauberk and a red cloak.",
         Present = present, Greeting = "You came back."
      };

      // STAKE: Gabriel's case, an ordinary talk the player starts: the composer opens it.
      [Test]
      public void GIVEN_an_ordinary_opening_with_both_switches_on_WHEN_decided_THEN_it_is_composed()
      {
         ComposedOpeningPolicy.ShouldCompose(Ordinary).Should().BeTrue();
      }

      // STAKE: every new switch off means today's game: no extra call, the opener as it was.
      [Test]
      public void GIVEN_the_switch_off_WHEN_decided_THEN_it_is_not_composed()
      {
         ComposedOpeningPolicy.ShouldCompose(new ComposedOpeningFacts {SceneNarrationOn = true}).Should().BeFalse();
      }

      // STAKE: 1Enfant turned the purple narration off; a paragraph nobody sees would cost a call for nothing.
      [Test]
      public void GIVEN_scene_narration_off_WHEN_decided_THEN_it_is_not_composed()
      {
         ComposedOpeningPolicy.ShouldCompose(new ComposedOpeningFacts {SwitchOn = true}).Should().BeFalse();
      }

      // STAKE: the plan's entry points: an envoy, a captive scene, a briefing and a council keep their own opening.
      [TestCase(true, false, false, false)]
      [TestCase(false, true, false, false)]
      [TestCase(false, false, true, false)]
      [TestCase(false, false, false, true)]
      public void GIVEN_a_staged_opening_WHEN_decided_THEN_it_is_not_composed(bool envoy, bool captive, bool briefing, bool council)
      {
         ComposedOpeningPolicy.ShouldCompose(new ComposedOpeningFacts {
            SwitchOn = true, SceneNarrationOn = true, EnvoyAudience = envoy, CaptiveScene = captive, CompanionBriefing = briefing, Council = council
         }).Should().BeFalse();
      }

      // STAKE: Gabriel 28/11/2026, "toujours une intro a l'ouverture du chat": a second talk the same day opens with an intro too.
      [Test]
      public void GIVEN_a_second_talk_the_same_day_WHEN_decided_THEN_it_is_composed()
      {
         ComposedOpeningPolicy.ShouldCompose(new ComposedOpeningFacts {SwitchOn = true, SceneNarrationOn = true, SpokeToday = true}).Should().BeTrue();
      }

      // STAKE: no facts at all is no reason to call a model.
      [Test]
      public void GIVEN_no_facts_WHEN_decided_THEN_it_is_not_composed()
      {
         ComposedOpeningPolicy.ShouldCompose(null).Should().BeFalse();
      }

      // STAKE: a paragraph that wraps over several lines is shown whole, and each label lands in its own field.
      [Test]
      public void GIVEN_a_reply_whose_setting_wraps_WHEN_parsed_THEN_every_field_is_read_whole()
      {
         ComposedOpeningReply reply = ComposedOpeningPrompt.Parse(
            "doing: She is reading the casualty lists.\n"
            + "setting: The governor's hall in the keep of Poros, the morning after the battle.\n"
            + "The hall smells of smoke and vinegar.\n"
            + "concern: You fear the Battanians will return before the walls are mended.\n"
            + "tension: Arwa's men took the best of the spoils.");

         reply.Doing.Should().Be("She is reading the casualty lists.");
         reply.Setting.Should().Be("The governor's hall in the keep of Poros, the morning after the battle. The hall smells of smoke and vinegar.");
         reply.Concern.Should().Be("You fear the Battanians will return before the walls are mended.");
         reply.Tension.Should().Be("Arwa's men took the best of the spoils.");
         reply.IsUsable.Should().BeTrue();
      }

      // STAKE: a model that writes markdown labels or wraps a field in quotes still gives a clean paragraph.
      [Test]
      public void GIVEN_bold_labels_and_quotes_WHEN_parsed_THEN_they_are_stripped()
      {
         ComposedOpeningReply reply = ComposedOpeningPrompt.Parse("**setting:** \"Rain beats on the shutters.\"\n- **concern**: none");

         reply.Setting.Should().Be("Rain beats on the shutters.");
         reply.Concern.Should().BeEmpty();
      }

      // STAKE: a refusal or a reply with no setting is no opening: the caller falls back to today's.
      [Test]
      public void GIVEN_a_reply_without_a_setting_WHEN_parsed_THEN_it_is_not_usable()
      {
         ComposedOpeningPrompt.Parse("I cannot help with that.").IsUsable.Should().BeFalse();
         ComposedOpeningPrompt.Parse("").IsUsable.Should().BeFalse();
      }

      // STAKE: the concern rides in every turn's prompt; a runaway one is capped.
      [Test]
      public void GIVEN_a_runaway_concern_WHEN_parsed_THEN_it_is_capped()
      {
         ComposedOpeningPrompt.Parse("setting: x\nconcern: " + new string('a', 2000)).Concern.Length.Should().Be(ComposedOpeningPolicy.MaxHiddenChars);
      }

      // STAKE: only a plain ok lets the paragraph through.
      [TestCase("ok")]
      [TestCase("OK.")]
      [TestCase("ok: nothing is invented")]
      [TestCase("ok, it invents nothing")]
      public void GIVEN_an_ok_verdict_WHEN_read_THEN_it_passes(string verdict)
      {
         ComposedOpeningPrompt.ReadVerdict(verdict).Passes.Should().BeTrue();
      }

      // STAKE: the simulator once composed a raid the facts did not hold; the check names it and the opening falls back.
      [Test]
      public void GIVEN_an_invents_verdict_WHEN_read_THEN_it_fails_with_what_it_invents()
      {
         ComposedOpeningVerdict verdict = ComposedOpeningPrompt.ReadVerdict("invents a raid on Varcheg");

         verdict.Passes.Should().BeFalse();
         verdict.Reason.Should().Be("it invents a raid on Varcheg");
      }

      // STAKE: a check that said nothing readable is no check: fail closed.
      [TestCase("")]
      [TestCase("The opening looks fine to me overall, mostly.")]
      [TestCase("okay so let me think")]
      [TestCase("Ok, but it invents a raid on Varcheg")]
      public void GIVEN_an_empty_or_unreadable_verdict_WHEN_read_THEN_it_fails(string verdict)
      {
         ComposedOpeningPrompt.ReadVerdict(verdict).Passes.Should().BeFalse();
      }

      // STAKE: the composer reads the place, the season, the battle they just fought, the talk of the realm and the gear.
      [Test]
      public void GIVEN_a_sheet_WHEN_the_facts_are_written_THEN_place_season_battle_news_and_gear_are_there()
      {
         string facts = ComposedOpeningPrompt.FactsText(Sheet());

         facts.Should().Contain("Where: the governor's hall in the keep of Poros");
         facts.Should().Contain("When: morning, spring");
         facts.Should().Contain("- yesterday: Fought at Arwa's side near Poros");
         facts.Should().Contain("- The Empire is at war with Battania.");
         facts.Should().Contain("How Arwa comes: Arwa wears a mail hauberk");
         facts.Should().Contain("Alympia's first words to Arwa, right after the setting: \"You came back.\"");
      }

      // STAKE: with no companion seated, the composer is told the player comes alone and puts no companion in the hall.
      [Test]
      public void GIVEN_no_companion_seated_WHEN_the_facts_are_written_THEN_the_player_comes_alone()
      {
         ComposedOpeningPrompt.FactsText(Sheet()).Should().Contain("With Arwa: no companion; Arwa comes alone into the scene.");
         ComposedOpeningPrompt.FactsText(Sheet("Ira")).Should().Contain("With Arwa: Ira (in the scene with them).");
      }

      // STAKE: the player reads the paragraph in his language; the labels stay English so the reply is still read.
      [Test]
      public void GIVEN_a_reply_language_WHEN_the_composer_prompt_is_built_THEN_it_asks_for_it_and_keeps_the_labels_english()
      {
         string system = ComposedOpeningPrompt.ComposerSystem(new ComposedOpeningSheet {NpcName = "Alympia", PlayerName = "Arwa", Language = "French"});

         system.Should().Contain("Write the setting, the concern and the tension in French; keep the four labels in English.");
         ComposedOpeningPrompt.ComposerSystem(new ComposedOpeningSheet {NpcName = "Alympia"}).Should().NotContain("keep the four labels");
      }

      // STAKE: the paragraph is shown to the player; a secret the character keeps (stage 2's record, a private memory)
      // must not be staged in it, and the check fails a setting that shows one.
      [Test]
      public void GIVEN_the_composer_and_the_check_WHEN_their_rules_are_read_THEN_a_kept_secret_never_shows_in_the_setting()
      {
         ComposedOpeningPrompt.ComposerSystem(new ComposedOpeningSheet {NpcName = "Alympia", PlayerName = "Arwa"})
                              .Should().Contain("The doing and the setting are what Arwa sees and hears: what Alympia keeps from Arwa (a secret, a private memory) never shows in them");
         ComposedOpeningPrompt.CheckSystem.Should().Contain("FAILS when it shows what the facts say the character keeps from the visitor");
      }

      // STAKE: Gabriel's chat with Rancid Thylea (28/11/2026): the check dropped a whole opening for "a brass astrolabe". The things in
      // the room are the composer's to choose; only what the game tracks is an invention.
      [Test]
      public void GIVEN_the_check_WHEN_its_rules_are_read_THEN_the_things_in_the_room_are_allowed_and_only_tracked_facts_invent()
      {
         ComposedOpeningPrompt.CheckSystem.Should().Contain("the objects, furniture and belongings in the room");
         ComposedOpeningPrompt.CheckSystem.Should().Contain("nothing else is an invention");
      }

      // STAKE: the checker reads the very paragraph and concern the player and the character will get.
      [Test]
      public void GIVEN_a_reply_WHEN_the_check_is_built_THEN_it_holds_the_facts_and_the_opening()
      {
         var reply = new ComposedOpeningReply {Setting = "Smoke hangs in the hall.", Concern = "You fear for the wounded."};

         string check = ComposedOpeningPrompt.CheckUser(Sheet(), reply);

         check.Should().Contain("Where: the governor's hall");
         check.Should().Contain("setting: Smoke hangs in the hall.");
         check.Should().Contain("concern: You fear for the wounded.");
      }

      // STAKE: the composer is told the hour in words that match the engine's own night reading.
      [TestCase(9, false, "morning")]
      [TestCase(15, false, "afternoon")]
      [TestCase(2, true, "the dead of night")]
      [TestCase(5, true, "before dawn")]
      public void GIVEN_an_hour_WHEN_named_THEN_it_reads_as_expected(int hour, bool night, string expected)
      {
         ComposedOpeningPolicy.TimeOfDay(hour, night).Should().Be(expected);
      }
   }
}
