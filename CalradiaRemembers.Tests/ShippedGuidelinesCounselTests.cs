// Code written by Gabriel Mailhot, 24/10/2026.
// Counsel, not sermons. fearente (Nexus, 24/10/2026), on Gemini 3.8 Flash and Gemma 4: "when I expected a character to
// support a relatively harmless decision, the response was something like, 'These lands have swallowed many arrogant
// people like you'"; told of two Dromons bought at Diatma, his wife lectured on Lucon's council and, after he reassured
// her, "launched into another speech about pride leading to one's downfall". The shipped guidelines licensed friction
// ("judge by deeds, not flattery", "name the danger", "you are no yes-man") and nothing bounded it. Every station's
// file now carries one rule: a concern is said once, the player's answer is taken, and those close to the player are
// on their side. If a file loses it, characters of that station go back to lecturing on weak models.

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
   public sealed class ShippedGuidelinesCounselTests
   {
      // The rule must reach every station: a lord, a companion, a gang leader, a notable and a wanderer each get their
      // own file, and the wife in the report was a lady (the base file) while a companion's "name the danger" is the
      // strongest licence of all.
      [Test]
      public void GIVEN_any_station_WHEN_the_player_tells_of_an_ordinary_plan_THEN_a_concern_is_said_once_and_the_answer_taken()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string folder = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData");
         string[] files = Directory.GetFiles(folder, "behavior_guidelines*.txt");
         string[] missing = files.Where(f =>
                                  {
                                     string text = File.ReadAllText(f);

                                     return !text.Contains("Counsel, not sermons") || !text.Contains("take their word and move on")
                                            || !text.Contains("you are on their side");
                                  })
                                 .Select(Path.GetFileName)
                                 .ToArray();

         files.Should().HaveCountGreaterThanOrEqualTo(5, "every station has its own guidelines file");
         missing.Should().BeEmpty("a station without the rule lectures the player on weak models (fearente, 24/10/2026)");
      }
   }
}
