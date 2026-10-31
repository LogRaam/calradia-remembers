// Code written by Gabriel Mailhot, 31/10/2026.
// Documents what the quest journal tells the player. avalanche24 (Nexus, 31/10/2026) sent a screenshot of a task whose
// title and only line were the giver's words ("Sargot's gang leader hoards what he learns. You'll slip into its shadows
// and return with what it hides"), and asked "What do I need to do here exactly?". Gabriel the same day: "Il faudrait
// que les quetes soient decrites simplement pour les joueurs." The game now writes a short title and a "What to do"
// line from what the task is and how the game checks it; if those lines lie, the player does the wrong thing and the
// task never completes, which is worse than the riddle they replace.

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class QuestJournalTextTests
   {
      private static QuestJournalFacts ScoutSargot() => new() {
         Type = QuestType.ScoutTown, GiverName = "Dnin", TargetSettlement = "Sargot"
      };

      // The report itself: the task the player could not read must name the town, the man to find and what to get from him.
      [Test]
      public void GIVEN_avalanche24s_scouting_task_WHEN_shown_THEN_it_says_to_get_inside_Sargot_and_win_over_its_gang_leader()
      {
         string steps = QuestJournalText.WhatToDo(ScoutSargot());

         steps.Should().StartWith(QuestJournalText.WhatToDoPrefix);
         steps.Should().Contain("Sargot").And.Contain("gang leader").And.Contain("Calradia Remembers chat");
      }

      // The title is what the journal list shows: a riddle there is what made the player ask.
      [Test]
      public void GIVEN_avalanche24s_scouting_task_WHEN_titled_THEN_the_title_names_the_deed_and_who_asked()
      {
         QuestJournalText.Title(ScoutSargot()).Should().Be("Scout Sargot from inside (Dnin)");
      }

      // A deed checked by a game event leaves the reward with the giver: the player who is not told to go back never
      // collects it (the reward is paid when the giver next speaks with them).
      [Test]
      public void GIVEN_a_deed_done_away_from_the_giver_WHEN_shown_THEN_it_says_to_go_back_to_the_giver_for_the_reward()
      {
         QuestJournalText.WhatToDo(ScoutSargot()).Should().Contain("talk with Dnin again to collect your reward");
      }

      // A hand-over in the chat is paid on the spot: telling the player to come back would send them on an empty trip.
      [Test]
      public void GIVEN_goods_handed_over_in_the_chat_WHEN_shown_THEN_it_says_the_task_is_settled_on_the_spot()
      {
         var horses = new QuestJournalFacts {Type = QuestType.ProvideGoods, GiverName = "Derthert", RequiredCount = 12, GoodsLabel = "horses"};

         string steps = QuestJournalText.WhatToDo(horses);

         steps.Should().Contain("12 horses").And.Contain("bag").And.Contain("settled on the spot");
         steps.Should().NotContain("again to collect");
      }

      // A letter is delivered through the game's own conversation line, not the chat: the player must be told which line.
      [Test]
      public void GIVEN_a_message_to_carry_WHEN_shown_THEN_it_quotes_the_conversation_line_that_delivers_it()
      {
         var letter = new QuestJournalFacts {Type = QuestType.DeliverLetter, GiverName = "Lucon", TargetHeroName = "Caladog"};

         QuestJournalText.WhatToDo(letter).Should().Contain("I bear a message for you, carried on another's behalf.");
      }

      // A forged weapon must be the player's own work; a bought one is refused at the hand-over.
      [Test]
      public void GIVEN_a_weapon_to_forge_WHEN_shown_THEN_it_names_the_kind_and_warns_a_bought_one_does_not_count()
      {
         var sword = new QuestJournalFacts {Type = QuestType.CraftWeapon, GiverName = "Unqid", GoodsLabel = "forged two-handed sword"};

         QuestJournalText.Title(sword).Should().Be("Forge a two-handed sword (Unqid)");
         QuestJournalText.WhatToDo(sword).Should().Contain("bought one does not count");
      }

      // Being declared on does not complete the task: a player who waits for the enemy to declare waits forever.
      [Test]
      public void GIVEN_a_war_to_declare_WHEN_shown_THEN_it_says_being_declared_on_does_not_count()
      {
         var war = new QuestJournalFacts {Type = QuestType.DeclareWar, GiverName = "Monchug", TargetFactionName = "Vlandia"};

         QuestJournalText.WhatToDo(war).Should().Contain("Vlandia").And.Contain("does not count");
      }

      // The deadline is enforced but the engine's own countdown is hidden for these tasks, so the line is the only place
      // the player sees it; the deadline day itself still counts, so "before" would steal a day.
      [Test]
      public void GIVEN_a_task_with_a_deadline_WHEN_shown_THEN_the_deadline_is_in_the_steps()
      {
         var facts = ScoutSargot();
         facts.DeadlineText = "Summer 12, 1085";

         QuestJournalText.WhatToDo(facts).Should().EndWith("Do it by Summer 12, 1085.");
      }

      // The giver's words stay, as theirs: the flavour is kept, never mistaken for the instructions.
      [Test]
      public void GIVEN_the_givers_own_words_WHEN_shown_THEN_they_are_quoted_as_the_givers()
      {
         QuestJournalText.GiverWords("Dnin", "Sargot's gang leader hoards what he learns.")
                         .Should().Be("Dnin's words: \"Sargot's gang leader hoards what he learns.\"");
      }

      // Every kind of task the game can set gets plain steps and a title when its facts are there: a kind left out
      // would fall back to the riddle avalanche24 reported.
      [Test]
      public void GIVEN_every_kind_of_task_with_its_facts_WHEN_shown_THEN_each_has_a_title_and_steps()
      {
         foreach (QuestType type in Enum.GetValues(typeof(QuestType)).Cast<QuestType>())
         {
            var facts = new QuestJournalFacts {
               Type = type, GiverName = "Dnin", TargetSettlement = "Sargot", TargetHeroName = "Caladog", TargetFactionName = "Vlandia",
               RequiredCount = 10, GoodsLabel = "grain", RequiredItemValue = 2000, RequiredGold = 750, AdvanceGold = 3000, CultureName = "Vlandian"
            };

            if (type != QuestType.External) QuestJournalText.Title(facts).Should().NotBeNullOrWhiteSpace($"{type} needs a title");
            QuestJournalText.WhatToDo(facts).Should().StartWith(QuestJournalText.WhatToDoPrefix, $"{type} needs steps");
         }
      }

      // A task from another mod keeps that mod's own words as its title: a generic one would erase the only name it has.
      [Test]
      public void GIVEN_a_task_from_another_mod_WHEN_titled_THEN_its_own_words_stay_the_title()
      {
         QuestJournalText.Title(new QuestJournalFacts {Type = QuestType.External, GiverName = "Dnin"}).Should().BeNull();
      }

      // A fact the steps need is missing (an old save, a target never bound): no line rather than a line with a hole in
      // it, and the journal keeps the giver's words as before.
      [Test]
      public void GIVEN_a_task_missing_the_town_it_names_WHEN_shown_THEN_no_plain_line_is_written()
      {
         var blank = new QuestJournalFacts {Type = QuestType.ScoutTown, GiverName = "Dnin"};

         QuestJournalText.WhatToDo(blank).Should().BeNull();
         QuestJournalText.Title(blank).Should().BeNull();
      }
   }
}
