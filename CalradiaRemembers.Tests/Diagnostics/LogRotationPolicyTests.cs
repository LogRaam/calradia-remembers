// Code written by Gabriel Mailhot, 27/08/2026.
// The log file is trimmed at every launch to the last couple of sessions so it never balloons (player request).
// "Keep the last two" is an off-by-one trap: keep one too few and a bug report loses the run before the crash;
// keep one too many and the file never really shrinks. These pin the boundary, the empty/absent cases, and that
// a within-budget file is returned untouched so a launch does no needless rewrite.

#region

using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Diagnostics
{
   [TestFixture]
   public sealed class LogRotationPolicyTests
   {
      private const string Marker = "=== Session started ";

      private static string Session(int n, string body) => $"{Marker}{n} ===\n{body}\n";

      // The headline case: three past sessions, we want room for one new one after this, so keep only the last
      // existing block. The oldest two must be gone, the newest kept whole.
      [Test]
      public void GIVEN_more_sessions_than_the_budget_WHEN_trimming_THEN_only_the_most_recent_blocks_remain()
      {
         string content = Session(1, "old") + Session(2, "older-recent") + Session(3, "newest");

         string trimmed = LogRotationPolicy.KeepLastSessions(content, Marker, 1);

         trimmed.Should().Be(Session(3, "newest"));
         trimmed.Should().NotContain("old\n"); // session 1's body is gone
      }

      // Keeping two existing blocks retains the last two in order, dropping only the oldest.
      [Test]
      public void GIVEN_a_budget_of_two_WHEN_trimming_THEN_the_last_two_blocks_remain_in_order()
      {
         string content = Session(1, "a") + Session(2, "b") + Session(3, "c");

         LogRotationPolicy.KeepLastSessions(content, Marker, 2).Should().Be(Session(2, "b") + Session(3, "c"));
      }

      // Anything before the first KEPT marker is dropped, including preamble text that precedes the first marker
      // (a partial line left by a crash), so a trimmed file always opens cleanly on a session header.
      [Test]
      public void GIVEN_preamble_before_the_first_marker_WHEN_trimming_THEN_it_is_dropped_with_the_old_blocks()
      {
         string content = "leftover partial line\n" + Session(1, "a") + Session(2, "b");

         LogRotationPolicy.KeepLastSessions(content, Marker, 1).Should().Be(Session(2, "b"));
      }

      // Already within budget: the file is returned as-is (same reference), so a launch with a short log does no
      // pointless rewrite of the file.
      [Test]
      public void GIVEN_fewer_sessions_than_the_budget_WHEN_trimming_THEN_the_content_is_returned_unchanged()
      {
         string content = Session(1, "only");

         string result = LogRotationPolicy.KeepLastSessions(content, Marker, 2);

         result.Should().BeSameAs(content);
      }

      // A file with no marker at all (some other tool wrote it, or it is empty) is left untouched rather than
      // wiped: the trimmer only ever acts on its own session structure.
      [Test]
      public void GIVEN_no_marker_in_the_content_WHEN_trimming_THEN_it_is_returned_unchanged()
      {
         string content = "no session markers here at all";

         LogRotationPolicy.KeepLastSessions(content, Marker, 1).Should().BeSameAs(content);
      }

      // A zero budget means keep none of the OLD runs (the new header will be the only thing in the file after);
      // the result is empty regardless of how many sessions the file held.
      [Test]
      public void GIVEN_a_zero_budget_WHEN_trimming_THEN_nothing_is_kept()
      {
         LogRotationPolicy.KeepLastSessions(Session(1, "a") + Session(2, "b"), Marker, 0).Should().BeEmpty();
      }

      // Null content is treated as empty, never a crash: log housekeeping must be the last thing to break a launch.
      [Test]
      public void GIVEN_null_content_WHEN_trimming_THEN_the_result_is_empty()
      {
         LogRotationPolicy.KeepLastSessions(null, Marker, 2).Should().BeEmpty();
      }
   }
}
