// Code written by Gabriel Mailhot, 05/10/2026.
// One word, one meaning in the prompt. fkasad (Nexus, 05/10/2026): the shipped guidelines carried a section
// "=== CONTEXT AND MOOD ===" about the conversation's TONE, and the prompt later carries "YOUR DAY", the character's own
// MOOD; "Is there any risk of confusion there?" There was: a short-tempered day read beside "respect the mood" made a
// lord who came to thank the player curt. The guidelines now speak of the conversation's tone, and "mood" is the day's.

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
   public sealed class ShippedGuidelinesMoodTests
   {
      // If a guidelines file names a section "MOOD" again, the model has two moods to reconcile and no rule between them.
      [Test]
      public void GIVEN_the_shipped_guidelines_WHEN_read_THEN_no_section_is_about_mood()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string folder = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData");
         string[] offenders = Directory.GetFiles(folder, "behavior_guidelines*.txt")
                                       .Where(f => File.ReadAllLines(f).Any(l => l.StartsWith("===") && l.ToUpperInvariant().Contains("MOOD")))
                                       .Select(Path.GetFileName)
                                       .ToArray();

         offenders.Should().BeEmpty("\"mood\" in the prompt means the character's day (YOUR DAY), not the conversation's tone");
      }
   }
}
