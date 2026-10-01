// Code written by Gabriel Mailhot, 01/10/2026.
// The grounded quest menu (ViableQuestScanner) tells a lord, by type token, which tasks exist for him to give; he copies
// the token into his [QUEST] block, and the SDK parser turns it into a quest type. A token the parser does not know is a
// task the lord offers and the game never records: the broken promise the menu exists to prevent.
//
// cr.testall's quest_menu used to check the menu against a hand-written list of known tokens, and failed on 01/10/2026
// over scout_town, a perfectly good entry: the list had drifted behind the scanner for the third time (its own comment
// records the first two). The referee is now the parser itself, here before the game is ever launched, and in game.

#region

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NpcMemoryService.Core.Parsing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestMenuTokensTests
    {
        // Every task the menu can name is one the lord's [QUEST] block can give: the parser knows each token.
        [Test]
        public void GIVEN_every_task_the_quest_menu_can_offer_WHEN_a_lord_copies_its_token_THEN_the_parser_records_that_task()
        {
            if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

            string scanner = File.ReadAllText(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "Quests", "ViableQuestScanner.cs"));
            string[] tokens = Regex.Matches(scanner, "TypeToken = \"([a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();

            tokens.Should().NotBeEmpty("the scanner names its tasks by TypeToken");

            var parser = new SectionResponseParser();
            string[] unknown = tokens.Where(tok => parser.Parse($"[DIALOGUE]x[/DIALOGUE]\n[QUEST]\ntype: {tok}\n[/QUEST]").QuestGiven == null).ToArray();

            unknown.Should().BeEmpty("a menu token the parser does not know is a task offered and never recorded");
        }
    }
}
