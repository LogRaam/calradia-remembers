// Code written by Gabriel Mailhot, 27/10/2026.
// Every mention of a console command in the code, its messages and the guides must say what the console really accepts.
// Gabriel (27/10/2026), after being told several times to type "cr.testall intimate_acts", which ran the whole sweep
// because cr.testall took no name while cr.test <name> existed all along: "pourquoi n'avais-tu pas vu que .test faisait
// deja ce que tu me disais de faire avec testall <action>? [...] je tente de voir comment on peut prevenir ca". About
// 180 comments had taught the wrong form, and each one read later confirmed it. ConsoleCommandHelp's usage line is the
// truth; this guard holds every mention to it. On its first run it also found three messages telling the player to type
// cr.chat_reset, which does not exist (cr.reset does). If it is wrong, a player or a modder copies a command out of a
// message or a guide and it fails, or does something other than what the text promised.

#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConsoleCommandMentionsTests
   {
      // cr.name, then optionally one argument: <placeholder>, [optional] or a bare word.
      private static readonly Regex Mention = new(@"(?<![A-Za-z0-9_.])cr\.([a-z][a-z0-9_]*)(?:[ `]+(<[^>\n]*>|\[[^\]\n]*\]|[A-Za-z0-9_]+))?");

      private static readonly string[] TestKeywords = {"all", "list", "chat", "mech", "full"};

      // Former names a text may still tell as history ("it advertised cr.compose, renamed cr.letter_compose"): never
      // given as an instruction. Add one only with the sentence that tells it.
      private static readonly string[] FormerNames = {"compose", "testall"};

      // The guides a reader copies commands out of.
      private static readonly string[] Guides = {
         "CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md", "CalradiaRemembers/CLAUDE.md", "EXTENSION_API.md", "CalradiaRemembers/Extension/HOWTO_Actions.md",
         "CalradiaRemembers/Extension/HOWTO_Quests.md", "CalradiaRemembers/Extension/HOWTO_Voicing.md", "CalradiaRemembers/Extension/HOWTO_WorldEvents.md"
      };

      // STAKE: a message or a guide that tells the player to type a command the console does not have (three notices
      // said cr.chat_reset; the command is cr.reset). Comments may still name a command that is gone, as history.
      [Test]
      public void GIVEN_a_command_named_in_a_message_or_a_guide_WHEN_checked_THEN_the_console_has_it()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         HashSet<string> registered = Registered();

         var dead = new List<string>();
         foreach ((string where, string text) in Literals().Concat(GuideTexts()))
         foreach (Match m in Mention.Matches(text))
         {
            string name = m.Groups[1].Value;
            bool family = name.EndsWith("_") && registered.Any(r => r.StartsWith(name));
            if (!registered.Contains(name) && !family && !FormerNames.Contains(name) && !IsHistory(text, m.Index)) dead.Add($"{where}: {m.Value}");
         }

         dead.Should().BeEmpty("every command a reader is told to type must exist (see ConsoleCommandHelp)");
      }

      // STAKE: the cr.testall mistake itself: a command that takes nothing, written with an argument after it (a test
      // name, a <placeholder>, an [option], a number), runs something other than what the text promises.
      [Test]
      public void GIVEN_a_command_that_takes_no_argument_WHEN_it_is_written_with_one_THEN_the_guard_names_it()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         Dictionary<string, string> usage = Usages();
         HashSet<string> tests = SelfTestNames();

         var wrong = new List<string>();
         foreach ((string where, string text) in Everything())
         foreach (Match m in Mention.Matches(text))
         {
            string name = m.Groups[1].Value, arg = m.Groups[2].Value;
            if (arg.Length == 0 || !usage.TryGetValue(name, out string u) || u.Length > 0) continue;
            bool argumentShaped = arg.Contains('_') || arg[0] == '<' || arg[0] == '[' || arg.All(char.IsDigit) || tests.Contains(arg);
            if (argumentShaped) wrong.Add($"{where}: {m.Value} (cr.{name} takes no argument)");
         }

         wrong.Should().BeEmpty("a mention must match the usage ConsoleCommandHelp declares for the command");
      }

      // STAKE: cr.test followed by a name that is no self-test fails in game with "Unknown test"; a renamed test leaves
      // every description that names it pointing at nothing.
      [Test]
      public void GIVEN_cr_test_with_a_name_WHEN_checked_THEN_the_name_is_a_self_test()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         HashSet<string> tests = SelfTestNames();

         var unknown = new List<string>();
         foreach ((string where, string text) in Everything())
         foreach (Match m in Mention.Matches(text))
         {
            string arg = m.Groups[2].Value;
            if (m.Groups[1].Value != "test" || !arg.Contains('_')) continue;
            if (!tests.Contains(arg) && !TestKeywords.Contains(arg)) unknown.Add($"{where}: {m.Value}");
         }

         unknown.Should().BeEmpty("cr.test <name> must name a SelfTest in CrSelfTest");
      }

      // STAKE: the guard itself: a mention like the one that started it is caught, and its fix is not.
      [Test]
      public void GIVEN_the_mistake_that_started_it_WHEN_read_THEN_the_mention_shape_is_understood()
      {
         Match wrong = Mention.Match("run cr.testall intimate_acts now");
         wrong.Groups[1].Value.Should().Be("testall");
         wrong.Groups[2].Value.Should().Be("intimate_acts");

         Match family = Mention.Match("the <c>cr.test nemesis_turn</c> harness");
         family.Groups[2].Value.Should().Be("nemesis_turn");

         Mention.Match("see `cr.courier_test <settlement>`").Groups[2].Value.Should().Be("<settlement>");
      }

      // "Renamed from cr.discover", "(was cr.compose)": a former name, told as history, is not an instruction.
      private static bool IsHistory(string text, int at)
      {
         string before = text.Substring(System.Math.Max(0, at - 14), at - System.Math.Max(0, at - 14)).ToLowerInvariant();

         return before.EndsWith("from ") || before.EndsWith("was ");
      }

      private static HashSet<string> Registered()
         => new(SourceTree.Production.SelectMany(f => Regex.Matches(f.Text, @"CommandLineArgumentFunction\(""([a-z0-9_]+)"",\s*""cr""\)")
                                                              .Cast<Match>().Select(m => m.Groups[1].Value)));

      private static Dictionary<string, string> Usages()
      {
         string help = SourceTree.Production.Single(f => f.RelativePath.EndsWith("ConsoleCommandHelp.cs")).Text;

         return Regex.Matches(help, @"\[""([a-z0-9_]+)""\]\s*=\s*new ConsoleCommandDoc\(""([^""]*)""")
                     .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
      }

      private static HashSet<string> SelfTestNames()
      {
         string selfTest = SourceTree.Production.Single(f => f.RelativePath.EndsWith("CrSelfTest.cs")).Text;

         return new HashSet<string>(Regex.Matches(selfTest, @"new SelfTest\(""([a-z0-9_]+)""").Cast<Match>().Select(m => m.Groups[1].Value));
      }

      // The text of every string literal in the production code: what the player reads in a message or the console.
      private static IEnumerable<(string Where, string Text)> Literals()
         => SourceTree.Production.SelectMany(f => f.Root.DescendantTokens()
                                                   .Where(t => t.IsKind(SyntaxKind.StringLiteralToken) || t.IsKind(SyntaxKind.InterpolatedStringTextToken))
                                                   .Select(t => (Where(f, t.GetLocation()), t.ValueText)));

      // The text of every comment in the production code.
      private static IEnumerable<(string Where, string Text)> Comments()
         => SourceTree.Production.SelectMany(f => f.Root.DescendantTrivia(descendIntoTrivia: true)
                                                   .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                                                               || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                                                               || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                                                   .Select(t => (Where(f, t.GetLocation()), t.ToFullString())));

      private static IEnumerable<(string Where, string Text)> GuideTexts()
         => Guides.Select(g => Path.Combine(SourceTree.ModRepo!, g)).Where(File.Exists)
                  .SelectMany(p => File.ReadAllLines(p).Select((line, i) => ($"{Path.GetFileName(p)}:{i + 1}", line)));

      private static IEnumerable<(string Where, string Text)> Everything() => Literals().Concat(Comments()).Concat(GuideTexts());

      private static string Where(SourceFile f, Location location) => $"{f.RelativePath}:{location.GetLineSpan().StartLinePosition.Line + 1}";
   }
}
