// Code written by Gabriel Mailhot, 04/10/2026.
// The tale editor and the game must agree (docs/design/quest-editor/goal.md, "the one real risk: two validators
// drifting apart"). The game checks a quest file in C# (AuthoredQuestLoader); the editor checks it in JavaScript, inside
// Docs/TaleEditor.html, so an author sees the problems before the game does. If the two disagree, the editor says
// "all good" and the game refuses the tale, or the editor refuses a tale the game would play.
//
// So a shared corpus, docs/design/quest-editor/fixtures: each <name>.json is a file an author could write (one per rule
// of the loader, the files written before branches, the worked examples, and the ways the game reads JSON leniently),
// and <name>.expected.txt is what the game reports about it, one problem per line. The game's loader must report
// exactly that, and the editor's own checks, taken out of the shipped HTML and run under Node, must report exactly the
// same. A "not valid JSON (...)" line is compared without the reader's message, which is Newtonsoft's own wording.
//
// To write the expected files for a NEW fixture: CR_UPDATE_EDITOR_FIXTURES=1, then read every line it wrote: an
// expected file is a statement of what an author must be told, not a recording.

#region

using System.Diagnostics;
using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class QuestEditorParityTests
   {
      private const string UpdateVariable = "CR_UPDATE_EDITOR_FIXTURES";
      private static readonly Lazy<Dictionary<string, List<string>>> NodeAnswers = new(RunNodeChecks);

      private static string Fixtures => Path.Combine(SourceTree.ModRepo!, "docs", "design", "quest-editor", "fixtures");
      private static string Editor => Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "Extension", "TaleEditor.html");
      private static string Runner => Path.Combine(SourceTree.ModRepo!, "docs", "design", "quest-editor", "tools", "run-editor.mjs");

      public static IEnumerable<string> FixtureNames()
      {
         if (!SourceTree.IsReachable) return new[] {"(source tree not reachable)"};

         return Directory.GetFiles(Fixtures, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal)!;
      }

      // What the game tells an author about each fixture is what the fixture says it must: the loader's rules, pinned one
      // file at a time (files written before branches included: they load exactly as they did).
      [TestCaseSource(nameof(FixtureNames))]
      public void GIVEN_each_fixture_WHEN_the_game_reads_it_THEN_it_reports_what_the_fixture_expects(string fixture)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         List<string> game = GameAnswer(fixture);
         string expectedPath = Path.Combine(Fixtures, fixture + ".expected.txt");
         if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
         {
            File.WriteAllText(expectedPath, string.Join("\n", game) + (game.Count > 0 ? "\n" : ""));

            return;
         }

         File.Exists(expectedPath).Should().BeTrue($"{fixture}.expected.txt says what the game must report; write it ({UpdateVariable}=1) and read it");
         game.Should().Equal(Expected(fixture));
      }

      // The editor tells the author exactly what the game would: the same problems, in the same words and order, for
      // every fixture, from the very code the shipped HTML runs.
      [TestCaseSource(nameof(FixtureNames))]
      public void GIVEN_each_fixture_WHEN_the_editor_checks_it_THEN_it_reports_exactly_what_the_game_reports(string fixture)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         NodeAnswers.Value.Should().ContainKey(fixture + ".json");
         NodeAnswers.Value[fixture + ".json"].Select(Normalize).Should().Equal(GameAnswer(fixture), "the editor and the game must agree on " + fixture);
      }

      // Beyond the fixtures, every tale the repository holds (the one that ships and the drafts) reads the same in both.
      [Test]
      public void GIVEN_every_tale_in_the_repository_WHEN_checked_by_the_game_and_by_the_editor_THEN_they_agree()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string[] folders = {Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "Quests"), Path.Combine(SourceTree.ModRepo!, "docs", "design", "quests")};
         Dictionary<string, List<string>> editor = RunNode("check", folders);

         foreach (string file in folders.SelectMany(f => Directory.GetFiles(f, "*.json")))
         {
            string name = Path.GetFileName(file);
            List<string> game = AuthoredQuestLoader.ParseAll(new[] {(name, File.ReadAllText(file))}).Problems.Select(Normalize).ToList();
            editor[name].Select(Normalize).Should().Equal(game, "the editor and the game must agree on " + name);
         }
      }

      public static IEnumerable<string> TwineFixtureNames()
      {
         if (!SourceTree.IsReachable) return new[] {"(source tree not reachable)"};

         return Directory.GetFiles(Path.Combine(Fixtures, "twine")).Where(f => !f.EndsWith(".expected.json", StringComparison.Ordinal))
                         .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal)!;
      }

      private static readonly Lazy<JObject> TwineAnswers = new(() => JObject.Parse(Node("twine", new[] {Path.Combine(Fixtures, "twine")})));

      // A Twine story (Harlowe or SugarCube, published HTML or Twee) becomes the tale its fixture says: passages as stages
      // and scene beats, links as choices, tags as objectives, variables as flags, and the same notes for the author.
      [TestCaseSource(nameof(TwineFixtureNames))]
      public void GIVEN_each_twine_story_WHEN_imported_THEN_it_becomes_its_expected_tale_with_its_notes(string story)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         JToken imported = TwineAnswers.Value[story]!;
         string expectedPath = Path.Combine(Fixtures, "twine", story + ".expected.json");
         if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
         {
            File.WriteAllText(expectedPath, imported.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");

            return;
         }

         JToken.DeepEquals(imported, JToken.Parse(File.ReadAllText(expectedPath))).Should().BeTrue($"{story} must import to {story}.expected.json");
      }

      // What the import writes, the game plays: each imported fixture loads through the real loader with nothing to report.
      [TestCaseSource(nameof(TwineFixtureNames))]
      public void GIVEN_each_twine_story_WHEN_imported_THEN_the_game_loads_the_tale_with_nothing_to_report(string story)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string quest = TwineAnswers.Value[story]!["quest"]!.ToString();

         AuthoredQuestLoader.ParseAll(new[] {(story, quest)}).Problems.Should().BeEmpty();
      }

      // Never dropped silently (goal): a macro or a tag the import cannot map is named to the author, in each format.
      [Test]
      public void GIVEN_macros_and_tags_the_import_cannot_map_WHEN_imported_THEN_each_is_named_to_the_author()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         List<string> Notes(string story) => TwineAnswers.Value[story]!["notes"]!.Select(n => (string) n!).ToList();

         Notes("harlowe_ferryman.html").Should().Contain(n => n.Contains("(print: $heard_of_debt) is not mapped"));
         Notes("sugarcube_ferryman.html").Should().Contain(n => n.Contains("<<audio \"river\" play>> is not mapped"));
         Notes("twee_miller.twee").Should().Contain(n => n.Contains("tag \"sorrowful\" is not understood"));
         Notes("harlowe_ferryman.html").Should().Contain(n => n.Contains("'Notes to self' is never linked from the start"));
      }

      private static List<string> GameAnswer(string fixture)
      {
         string json = File.ReadAllText(Path.Combine(Fixtures, fixture + ".json"));

         return AuthoredQuestLoader.ParseAll(new[] {(fixture + ".json", json)}).Problems.Select(Normalize).ToList();
      }

      private static List<string> Expected(string fixture)
         => File.ReadAllText(Path.Combine(Fixtures, fixture + ".expected.txt")).Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();

      /// <summary>A malformed file is compared by what the author is told (it is not valid JSON), not by the reader's own wording.</summary>
      internal static string Normalize(string problem)
         => Regex.Replace(problem.Replace("\r\n", "\n"), @"not valid JSON \(.*\)\.$", "not valid JSON (...).", RegexOptions.Singleline);

      private static Dictionary<string, List<string>> RunNodeChecks() => RunNode("check", new[] {Fixtures});

      internal static Dictionary<string, List<string>> RunNode(string mode, IEnumerable<string> targets)
         => JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(Node(mode, targets))!;

      /// <summary>Runs the editor's modules under Node. Node missing is a failure, never a silent skip: the parity is the guard.</summary>
      internal static string Node(string mode, IEnumerable<string> targets)
      {
         var start = new ProcessStartInfo("node") {RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false};
         foreach (string a in new[] {Runner, mode, Editor}.Concat(targets)) start.ArgumentList.Add(a);
         start.StandardOutputEncoding = System.Text.Encoding.UTF8;

         Process process;
         try { process = Process.Start(start)!; }
         catch (System.ComponentModel.Win32Exception)
         {
            Assert.Fail("Node is not installed or not on PATH; the editor's checks cannot be held to the game's without it.");

            return "";
         }

         Task<string> output = process.StandardOutput.ReadToEndAsync();
         string error = process.StandardError.ReadToEnd();
         process.WaitForExit();
         if (process.ExitCode != 0) Assert.Fail($"The editor's modules failed under Node:\n{error}");

         return output.Result;
      }
   }
}
