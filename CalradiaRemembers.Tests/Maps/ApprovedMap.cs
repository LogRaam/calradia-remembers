// Code written by Gabriel Mailhot, 21/09/2026.
// The trigger. A generated document with nothing forcing its regeneration is stale by the second commit, and a stale
// map is worse than none because it is trusted. This project has no CI that can build the mod (it needs the game's
// DLLs), but it has a pipeline all the same: the suite runs on every change and nothing is committed until it is
// green. So each map is regenerated in memory by a test and compared with the committed copy.
//
//   - identical: the dimension that map watches did not move. That is information, not a gap.
//   - different: the test FAILS and shows the difference. Regenerate, and the new map rides in the same commit as the
//     code, where its diff is the architectural summary of the change.
//
// To regenerate:   CR_UPDATE_MAPS=1 dotnet test CalradiaRemembers.Tests -p:NCrunch=1 --filter "FullyQualifiedName~Maps"

#region

using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   /// <summary>Compares a freshly generated map with its committed copy, or rewrites the copy when asked to.</summary>
   internal static class ApprovedMap
   {
      public const string UpdateVariable = "CR_UPDATE_MAPS";

      private const int DiffLinesShown = 25;

      /// <summary>
      ///   <paramref name="repoRelativePath" /> is relative to the MOD repository, which is the umbrella for every
      ///   map, the SDK's included: Gabriel wants one place to look, and both suites run on every change.
      /// </summary>
      public static void Verify(string repoRelativePath, string generated)
      {
         if (!SourceTree.IsReachable)
            Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}, so regenerating this map here would describe a system "
                        + "that is missing a whole layer. This is what an NCrunch workspace looks like: it copies only what the "
                        + "test project REFERENCES, and the mod is deliberately not referenced. Run `dotnet test`, or "
                        + "Tools/Update-Maps.ps1.");

         string path = Path.Combine(SourceTree.ModRepo!, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
         string fresh = Normalize(generated);

         if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
         {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, fresh);

            return;
         }

         if (!File.Exists(path))
            Assert.Fail($"{repoRelativePath} does not exist yet. {HowToRegenerate}");

         string committed = Normalize(File.ReadAllText(path));
         if (committed == fresh) return;

         Assert.Fail($"{repoRelativePath} no longer matches the code. {HowToRegenerate}\n\n{Difference(committed, fresh)}");
      }

      #region private

      private static string HowToRegenerate
         => $"Regenerate with `{UpdateVariable}=1 dotnet test CalradiaRemembers.Tests -p:NCrunch=1 --filter \"FullyQualifiedName~Maps\"` "
          + "and commit the map WITH the change that moved it: its diff is the summary of what changed.";

      private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";

      /// <summary>Lines only one side has. Not a real diff, and it does not need to be: it only has to say WHAT moved.</summary>
      private static string Difference(string committed, string fresh)
      {
         var before = new HashSet<string>(committed.Split('\n'));
         var after = new HashSet<string>(fresh.Split('\n'));

         List<string> added = fresh.Split('\n').Where(l => !before.Contains(l)).Distinct().ToList();
         List<string> removed = committed.Split('\n').Where(l => !after.Contains(l)).Distinct().ToList();

         var lines = new List<string> {$"{added.Count} line(s) the code now produces, {removed.Count} line(s) it no longer does:"};
         lines.AddRange(added.Take(DiffLinesShown).Select(l => "  + " + l));
         lines.AddRange(removed.Take(DiffLinesShown).Select(l => "  - " + l));

         if (added.Count + removed.Count > DiffLinesShown * 2) lines.Add("  ...");

         return string.Join("\n", lines);
      }

      #endregion
   }
}
