// Code written by Gabriel Mailhot, 14/11/2026.
// What PlayerReportComposer decides: what a player's "report a mistake" carries. Gabriel (14/11/2026): "on voit tout le
// contexte, les logs, la conversation et le joueur peut ajouter un texte explicatif du problème"; "réinjecter le rapport du
// joueur dans le log avec tout le contexte". If this breaks, a report misses the prompt or the player's words and needs a
// second round with the player, leaks an API key into a public channel, or shares a Hardcore conversation the player
// chose to keep.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerReportComposerTests
   {
      private static PlayerReportFacts Facts(bool removed = false)
         => new() {
            PlayerText = "She asked for 1000 denars twice.",
            Header = new List<string> {"Calradia Remembers: v2.8.10", "Model: z-ai/glm-4.6"},
            Transcript = new List<string> {"Iskar: I would like to give you 1,000 denars.", "Lady Arwa: *She takes the coin.*"},
            Exchanges = new List<ReportExchange> {
               new() {Time = "19:03:13", Model = "z-ai/glm-4.6", Subject = "lord_arwa", SystemPrompt = "You are Lady Arwa.", Messages = new List<string> {"user: hello"}, Reply = "Well met."}
            },
            LogLines = new List<string> {"[19:03:38] [ACTION] change_relation: Arwa 56->58", "You are Lady Arwa. (a prompt line)", "[19:03:40] Player -> Arwa: \"we can get married\""},
            RemoveConversationText = removed
         };

      // THE REQUEST: the report holds the player's words, the conversation, the prompt, the reply and the log.
      [Test]
      public void GIVEN_a_report_WHEN_composed_THEN_it_carries_the_whole_context()
      {
         string report = PlayerReportComposer.Compose(Facts());

         report.Should().Contain("She asked for 1000 denars twice.")
               .And.Contain("Lady Arwa: *She takes the coin.*")
               .And.Contain("You are Lady Arwa.")
               .And.Contain("Well met.")
               .And.Contain("[ACTION] change_relation")
               .And.StartWith(PlayerReportComposer.BeginMarker);
      }

      // STAKE: a key in a prompt, a log or the player's text never reaches a public channel.
      [Test]
      public void GIVEN_a_key_anywhere_WHEN_composed_THEN_it_is_masked()
      {
         var facts = new PlayerReportFacts {
            PlayerText = "my key is sk-or-v1-0123456789abcdef0123",
            LogLines = new List<string> {"Authorization: Bearer abcdef0123456789", "\"api_key\": \"secretvalue123\""}
         };

         string report = PlayerReportComposer.Compose(facts);

         report.Should().NotContain("0123456789abcdef0123").And.NotContain("abcdef0123456789").And.NotContain("secretvalue123").And.Contain("[masked]");
      }

      // STAKE: a player who keeps what was said (Hardcore, by default) shares no line of it.
      [Test]
      public void GIVEN_the_player_keeps_the_conversation_WHEN_composed_THEN_no_line_of_it_is_shared()
      {
         string report = PlayerReportComposer.Compose(Facts(removed: true));

         report.Should().Contain("She asked for 1000 denars twice.")
               .And.Contain(PlayerReportComposer.Removed)
               .And.Contain("[ACTION] change_relation")
               .And.NotContain("She takes the coin")
               .And.NotContain("You are Lady Arwa.")
               .And.NotContain("Well met.")
               .And.NotContain("we can get married");
      }

      // STAKE: the window shows the start and says the file holds the rest.
      [Test]
      public void GIVEN_a_long_report_WHEN_previewed_THEN_it_is_cut_and_says_how_much_is_left()
      {
         string preview = PlayerReportComposer.Preview(new string('a', 120), 100);

         preview.Should().StartWith(new string('a', 100)).And.Contain("20 more characters in the report file");
      }
   }
}
