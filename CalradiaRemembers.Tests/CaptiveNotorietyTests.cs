// Code written by Gabriel Mailhot, 03/10/2026.
// Lordfadooboo (Nexus, 03/10/2026): Rhagaea, empress, in an Aserai cell, said "few men of the hills trouble
// themselves with the affairs of empresses held in foreign cells". "thats like putin ending up in prison and being
// like aw no one would ever talk to widdle ol me". Her prompt told her no news reaches her cell (right), and nothing
// told her that her capture IS the news; and CR recorded a capture as world news only when the player took part.
// This decides whose capture is talked of, and how far the news carries. If it is wrong, a captive ruler plays
// herself as forgotten again, or every minor lord the AI captures floods the rumour mill.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveNotorietyTests
   {
      // Rhagaea's case: a ruler's capture is talked of everywhere, and she is told so.
      [Test]
      public void GIVEN_a_captive_ruler_WHEN_judged_THEN_her_capture_is_known_as_a_rulers()
      {
         CaptiveNotoriety.Describe(new Stature(5, true), true).Should().Contain("ruler");
      }

      // The head of a noble house is news too, a lesser one.
      [Test]
      public void GIVEN_the_head_of_a_landed_house_WHEN_judged_THEN_the_capture_is_talked_of_less_than_a_rulers()
      {
         CaptiveNotoriety.Describe(new Stature(Stature.LandedNobleTier, false), true).Should().NotBeNullOrEmpty();
         CaptiveNotoriety.NewsMagnitude(new Stature(Stature.LandedNobleTier, false), true)
                         .Should().BeLessThan(CaptiveNotoriety.NewsMagnitude(new Stature(5, true), true));
      }

      // The AI captures lesser lords all the time: those are not world news, or the rumour mill drowns.
      [TestCase(5, false)]
      [TestCase(1, true)]
      [TestCase(0, false)]
      public void GIVEN_a_lord_who_neither_rules_nor_heads_a_landed_house_WHEN_judged_THEN_the_capture_is_no_news(int tier, bool clanLeader)
      {
         CaptiveNotoriety.Describe(new Stature(tier, false), clanLeader).Should().BeNull();
         CaptiveNotoriety.NewsMagnitude(new Stature(tier, false), clanLeader).Should().Be(0);
      }
   }
}
