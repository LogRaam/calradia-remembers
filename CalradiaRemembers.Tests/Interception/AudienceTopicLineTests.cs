// Code written by Gabriel Mailhot, 18/09/2026.
// What these pin: that the player is told what an NPC wants, before deciding whether to hear it.
//
// fkasad (Nexus, 18/09/2026): "The invitation to talk should specify the topic the NPC wants to discuss
// (that's how it works in real life)." The reason it matters is the sentence before it in his report:
//
//   "Maybe one in ten NPCs has something interesting to say, but unfortunately, I'm forced to decline the
//    conversation because of the other nine."
//
// Declining blind is the rational play when nine of ten are platitudes at the price of a model call, and it
// costs him the tenth as well. The subject was already in hand when the invitation was raised; it was simply
// never shown.

#region

using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Interception
{
   [TestFixture]
   public sealed class AudienceTopicLineTests
   {
      // THE REPORTED CASE: the player reads what it is about and can choose.
      [Test]
      public void GIVEN_a_matter_to_raise_WHEN_the_invitation_is_written_THEN_it_names_the_subject()
      {
         AudienceTopicLine.For("the siege of Zeonica and who broke it")
                          .Should().Be(" It seems to be about the siege of Zeonica and who broke it.");
      }

      // NOTHING TO NAME MEANS NOTHING SAID. A clause that promises a subject and then supplies none teaches the
      // player that the clause is noise, which costs more than never having written it.
      [Test]
      public void GIVEN_no_subject_WHEN_the_invitation_is_written_THEN_it_promises_nothing()
      {
         AudienceTopicLine.For(null).Should().BeEmpty();
         AudienceTopicLine.For("").Should().BeEmpty();
         AudienceTopicLine.For("   ").Should().BeEmpty();
      }

      // A summary is prose written by a model and can run long; an inquiry box cannot. Cut it rather than let
      // the box swallow the buttons, the failure nineteen MCM hints already shipped with once.
      [Test]
      public void GIVEN_a_summary_longer_than_the_box_WHEN_written_THEN_it_is_cut_and_marked()
      {
         string line = AudienceTopicLine.For(new string('x', 400));

         line.Length.Should().BeLessThan(150);
         line.Should().EndWith("...").And.NotEndWith("....",
            "a cut summary trails off; a fourth dot is exactly how the bug this test caught looked");
      }

      // One full stop, not two. The summary is a sentence and usually carries its own.
      [Test]
      public void GIVEN_a_summary_that_ends_in_a_full_stop_WHEN_written_THEN_the_clause_does_not_double_it()
      {
         AudienceTopicLine.For("the bandits on the Ortysia road.")
                          .Should().EndWith("road.").And.NotEndWith("..");
      }

      // A model writes multi-line prose sometimes, and an inquiry body renders a newline as a break mid-clause.
      [Test]
      public void GIVEN_a_summary_across_several_lines_WHEN_written_THEN_it_reads_as_one_clause()
      {
         AudienceTopicLine.For("the siege\r\nof Zeonica").Should().NotContain("\n").And.NotContain("\r");
      }
   }
}
