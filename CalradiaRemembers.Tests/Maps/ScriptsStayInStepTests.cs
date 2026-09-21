// Code written by Gabriel Mailhot, 21/09/2026.
// What these pin: that the scripts a human runs still say the same words the code listens for.
//
// Gabriel, 21/09/2026: "un framework de test unitaire n'est qu'un runner.. tu pourrais lancer dotnet test ou le script
// powershell dans un test unitaire." He is right about the principle, and the Maps fixtures already live on it: they
// do not test behaviour, they regenerate documents and compare. What they do NOT do is shell out, and that line is
// worth keeping: a test that ran `dotnet test` would spawn the runner inside itself, and one that ran a build or
// packed an archive would make NCrunch, which re-runs tests as he types, unusable.
//
// So this takes the cheap half of his idea and leaves the expensive half alone. Update-Maps.ps1 is coupled to the
// suite by three magic strings: the two environment variables and the test filter. Rename a constant in C# and the
// script keeps running, cheerfully regenerating nothing, and the failure looks like "the maps are stale again" rather
// than "the script is broken". Reading a text file is in-process and instant, so the coupling is pinned here.

#region

using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class ScriptsStayInStepTests
   {
      private const string UpdateMaps = "Tools/Update-Maps.ps1";
      private const string GenerateRules = "Tools/Generate-Rules.ps1";

      // THE WORD THE SCRIPT SETS AND THE CODE READS. If they ever differ, the script regenerates nothing and says
      // everything is fine.
      [Test]
      public void GIVEN_the_approval_script_WHEN_read_THEN_it_sets_the_variable_the_code_actually_listens_for()
         => Script(UpdateMaps).Should().Contain($"$env:{ApprovedMap.UpdateVariable} = \"1\"");

      // The save-key escape hatch, same reasoning: a script offering a switch the code no longer reads would let a
      // key be retired silently, which is the one thing the suite exists to refuse.
      [Test]
      public void GIVEN_the_approval_script_WHEN_read_THEN_its_save_key_switch_matches_the_guard()
         => Script(UpdateMaps).Should().Contain("CR_ALLOW_SAVE_KEY_REMOVAL");

      // The filter has to select the Maps fixtures. Narrow it by accident (a renamed namespace, a typo) and the
      // script runs a handful of tests, regenerates a handful of maps, and reports success.
      [Test]
      public void GIVEN_the_approval_script_WHEN_read_THEN_its_filter_still_selects_these_fixtures()
      {
         Script(UpdateMaps).Should().Contain("FullyQualifiedName~Maps");

         typeof(MapsAreCurrentTests).FullName.Should().Contain("Maps",
            "the filter matches on the name, so moving these fixtures out of a namespace containing 'Maps' breaks the script silently");
      }

      // The older script was left as a viewer and a shortcut once its generator moved into the suite. If it ever
      // starts writing RULES.md again, there are two generators and the committed file depends on which one ran.
      [Test]
      public void GIVEN_the_older_rules_script_WHEN_read_THEN_it_delegates_rather_than_writing_the_file_itself()
      {
         string script = Script(GenerateRules);

         script.Should().Contain(ApprovedMap.UpdateVariable, "it must hand the writing to the suite's own generator");
         script.Should().NotContain("Set-Content -LiteralPath $outputPath", "that was the second generator, and it is gone");
      }

      // AND THE RUNNER THAT MUST NOT RUN THEM AT ALL. These fixtures are an approval mechanism: they regenerate a
      // document and compare it with the copy in the repository, which only means anything when the sources being
      // read and the maps being compared are the working tree. NCrunch works from a copy, and on 21/09/2026 it
      // failed all twenty of them in milliseconds, three times, each time for a different reason than the one I
      // had guessed. They are excluded there by NCrunch's own config, the way an older test that needs the
      // compiled mod assembly already was.
      //
      // The list is by NAME, so a map added later runs everywhere except where it was told to, and NCrunch goes
      // red again for a reason that has nothing to do with the code. That is what this catches, at `dotnet test`
      // time, where the answer is one line in a file rather than an afternoon of triage. The config is per
      // machine and gitignored, so this says nothing where it is absent.
      [Test]
      public void GIVEN_a_map_test_that_cannot_run_under_NCrunch_WHEN_the_ignore_list_is_read_THEN_it_is_named_there()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         string path = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers.Tests", "CalradiaRemembers.Tests.v3.ncrunchproject");

         if (!File.Exists(path)) Assert.Ignore("no NCrunch config on this machine, so there is nothing to keep in step");

         string config = File.ReadAllText(path);

         List<string> missing = typeof(ScriptsStayInStepTests).Assembly
                                                              .GetTypes()
                                                              .Where(t => t.Namespace == typeof(MapsAreCurrentTests).Namespace)
                                                              .SelectMany(t => t.GetMethods())
                                                              .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                                                              .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
                                                              .Where(name => !config.Contains(name))
                                                              .OrderBy(name => name, StringComparer.Ordinal)
                                                              .ToList();

         missing.Should().BeEmpty("these map tests would run under NCrunch, which cannot see the repository they "
                                + "compare against, and would fail there for no reason: "
                                + string.Join(", ", missing));
      }

      #region private

      private static string Script(string repoRelativePath)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         string path = Path.Combine(SourceTree.ModRepo!, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));

         if (!File.Exists(path)) Assert.Fail($"{repoRelativePath} is gone. If it was retired on purpose, retire this test with it.");

         return File.ReadAllText(path);
      }

      #endregion
   }
}
