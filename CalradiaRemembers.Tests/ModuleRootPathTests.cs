// Code written by Gabriel Mailhot, 04/10/2026.
// Where the mod reads its own files from. EmperorHaterius (Nexus, 04/10/2026): his CR log said "not found" for every
// file the mod ships (world.txt, character_overrides.json, cultural_traditions.json, jealousy_cultures.json, the
// guidelines) and the council button art could not be built, all from "../../Modules\CalradiaRemembers\...", while
// the chat prefabs, found through the engine's own module path, loaded. The Dark Leather window was see-through, with
// no way out, because its art lived on the same broken path. The engine knows where the module really is; a path
// rebuilt by hand from BasePath.Name only holds when the module sits in the game's own Modules folder AND the process
// started in its bin folder. If this guard fails, a player whose install differs plays with none of the mod's files.

#region

using System.IO;
using System.Linq;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ModuleRootPathTests
   {
      // Every file the mod reads of its own goes through ModulePaths, which asks the engine. One site rebuilding the
      // path by hand is one more feature that silently vanishes on an install like EmperorHaterius's.
      [Test]
      public void GIVEN_the_mod_source_WHEN_it_builds_a_path_to_its_own_files_THEN_it_asks_the_engine_where_the_module_is()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string root = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers");
         string[] offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                                       .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                                                   && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                                                   && Path.GetFileName(f) != "ModulePaths.cs")
                                       .Where(f => File.ReadAllLines(f).Any(l => l.Contains("BasePath.Name") && !l.TrimStart().StartsWith("///")))
                                       .Select(f => f.Substring(root.Length + 1))
                                       .ToArray();

         offenders.Should().BeEmpty("a path rebuilt from BasePath.Name misses the module on any install the engine found elsewhere; use ModulePaths");
      }
   }
}
