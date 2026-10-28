// Code written by Gabriel Mailhot, 03/10/2026.
// cr.test settings_ranges_agree drives every integer MCM slider end to end and fails on one it does not know.
// On 03/10/2026 it failed in game on the new "Self-Esteem Climb" (fkasad's slider), because the setting had been added
// and its line in the test's table had not. That was only seen once Gabriel ran testall. This guard reads the same
// two files without the game, so a new slider without its line goes red at the desk, the moment it is added. If it is
// wrong, a slider can promise the player a value the code quietly clamps away, unseen until a play session.

#region

using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SettingEnforcersCoverageTests
   {
      // Every integer slider in the menu has its line in the testall table that checks what the code does with it.
      [Test]
      public void GIVEN_every_integer_slider_in_the_menu_WHEN_the_testall_table_is_read_THEN_each_has_its_line()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string settings = SourceTree.Production.Single(f => f.RelativePath.EndsWith("CrSettings.cs")).Text;
         string selfTest = SourceTree.Production.Single(f => f.RelativePath.EndsWith("CrSelfTest.cs")).Text;

         var sliders = Regex.Matches(settings, @"\[SettingPropertyInteger\((?:.|\n)*?public int (\w+)\s*\{")
                            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

         sliders.Should().NotBeEmpty();
         sliders.Where(name => !selfTest.Contains($"[\"{name}\"] = (\""))
                .Should().BeEmpty("each integer slider needs its SettingEnforcers line, or settings_ranges_agree fails in game");
      }
   }
}
