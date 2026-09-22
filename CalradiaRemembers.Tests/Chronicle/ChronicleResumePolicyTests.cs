// Code written by Gabriel Mailhot, 22/09/2026.
// What these pin: that the chronicle does not lose a chapter when a deed behind it is withdrawn.
//
// tashmetu, 22/09/2026, flagging a change in his bridge before publishing it: "this latest round of work is the
// first time we've actually called Feed.Retract, up to now we'd only ever used Feed.Record... for sieges, raids,
// town revolts, and pending motions, instead of narrating the end we just retract the original fact once the
// underlying condition clears."
//
// He asked what happens to a chronicled deed that is later retracted. The answer to his question is reassuring
// (the illuminated prose is a separate persisted blob and is never touched), but finding it turned up something
// he could not have asked about, because it is on our side of the wall: the bookmark is a COUNT into the list of
// deeds. Remove one that has already been written up and the count points one entry too far in, so the NEXT
// illumination skips a deed that was never written. The chronicle loses a chapter and says nothing.
//
// It had never happened because nothing had ever retracted anything. His mod is the first, which is exactly why
// he was right to flag the change rather than ship it quietly.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Chronicle;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Chronicle
{
   [TestFixture]
   public sealed class ChronicleResumePolicyTests
   {
      private static string Deed(int day, string line) => ChronicleResumePolicy.Mark(day, line);

      private static readonly List<string> ThreeDeeds = new() {
         Deed(10, "took the field at Pendraic"),
         Deed(14, "the siege of Ortysia was lifted"),
         Deed(19, "wed the daughter of the Ustean house")
      };

      // THE CASE HIS MOD CREATES. Three deeds written up; the middle one is a siege his bridge retracts once the
      // siege ends. Under the old count the next illumination would have started at index 3 of a list that now
      // holds 2, written nothing, and quietly left the marriage out for ever after.
      [Test]
      public void GIVEN_a_chronicled_deed_is_retracted_WHEN_the_illuminator_resumes_THEN_it_still_starts_after_the_last_one_it_wrote()
      {
         string lastWritten = ThreeDeeds[2];
         var afterRetraction = new List<string> {ThreeDeeds[0], ThreeDeeds[2]};

         ChronicleResumePolicy.ResumeFrom(afterRetraction, lastWritten, 3).Should().Be(2,
            "the last deed it wrote is still there, at a new index, and the chronicle continues after it");
      }

      // AND THE NEW DEED AFTER IT IS NOT LOST. The same shrunk list with one fresh deed appended: the count said
      // 3 and the list holds 3, so the old bookmark would have reported "already up to date" and the new deed
      // would never have been written at all.
      [Test]
      public void GIVEN_a_retraction_and_a_new_deed_WHEN_the_illuminator_resumes_THEN_the_new_deed_is_the_one_it_writes()
      {
         var afterRetraction = new List<string> {ThreeDeeds[0], ThreeDeeds[2], Deed(22, "burned the Khuzait camp at Odokh")};

         ChronicleResumePolicy.ResumeFrom(afterRetraction, ThreeDeeds[2], 3).Should().Be(2);
      }

      // THE ORDINARY DAY, where nothing was retracted and the mark simply sits at the end.
      [Test]
      public void GIVEN_nothing_has_been_withdrawn_WHEN_the_illuminator_resumes_THEN_it_starts_after_the_last_deed()
         => ChronicleResumePolicy.ResumeFrom(ThreeDeeds, ThreeDeeds[2], 3).Should().Be(3);

      // A SAVE FROM BEFORE THE MARK EXISTED keeps exactly the behaviour it has today. The save-migration rule is
      // absolute here: a player mid-campaign must not have their chronicle restart or skip because we changed
      // how the bookmark is written.
      [Test]
      public void GIVEN_an_older_save_with_a_count_and_no_mark_WHEN_the_illuminator_resumes_THEN_the_count_is_used_as_before()
      {
         ChronicleResumePolicy.ResumeFrom(ThreeDeeds, null, 2).Should().Be(2);
         ChronicleResumePolicy.ResumeFrom(ThreeDeeds, "", 2).Should().Be(2);
      }

      // AND THE COUNT IS STILL CLAMPED, which is what the old code did and the reason a retraction never crashed
      // anything: it simply pointed past the end and wrote nothing.
      [Test]
      public void GIVEN_a_count_larger_than_the_deeds_that_remain_WHEN_the_illuminator_resumes_THEN_it_is_clamped_to_the_end()
         => ChronicleResumePolicy.ResumeFrom(ThreeDeeds, null, 99).Should().Be(3);

      // THE LIMIT, stated rather than hidden. If the deed that was retracted is the very one the bookmark names,
      // there is no evidence left of where we stopped, and the count is all there is. It can still skip one, and
      // a full rewrite is the only cure. Worth pinning so nobody later reads the fallback as a guarantee.
      [Test]
      public void GIVEN_the_marked_deed_itself_was_retracted_WHEN_the_illuminator_resumes_THEN_it_falls_back_to_the_clamped_count()
      {
         var withoutTheMarked = new List<string> {ThreeDeeds[0], ThreeDeeds[1]};

         ChronicleResumePolicy.ResumeFrom(withoutTheMarked, ThreeDeeds[2], 3).Should().Be(2);
      }

      // NOTHING WRITTEN YET is the first chronicle of every campaign, and must not depend on a mark at all.
      [Test]
      public void GIVEN_a_chronicle_never_illuminated_WHEN_it_resumes_THEN_it_starts_at_the_beginning()
      {
         ChronicleResumePolicy.ResumeFrom(ThreeDeeds, null, 0).Should().Be(0);
         ChronicleResumePolicy.ResumeFrom(new List<string>(), null, 0).Should().Be(0);
         ChronicleResumePolicy.ResumeFrom(null, "anything", 5).Should().Be(0);
      }

      // TWO DEEDS THAT READ THE SAME ON THE SAME DAY are one sentence twice, and resuming from either gives the
      // same chronicle, so the later one wins and nothing is written twice.
      [Test]
      public void GIVEN_two_identical_deeds_on_one_day_WHEN_the_illuminator_resumes_THEN_it_takes_the_later_one()
      {
         var twins = new List<string> {Deed(10, "a duel at the gates"), Deed(10, "a duel at the gates")};

         ChronicleResumePolicy.ResumeFrom(twins, Deed(10, "a duel at the gates"), 1).Should().Be(2);
      }
   }
}
