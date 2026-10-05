// Code written by Gabriel Mailhot, 05/10/2026.
// How long a background model call (a letter, a conversation summary, a courier's secret, a letter's memory) may run.
// CarolusIV (Nexus, 05/10/2026): "a lot of log entries about letter generation timing out because the LLM responded too
// slowly... I do pretty well with 180s max on my modest hardware." His MCM "LLM Response Timeout" was already 180, but
// these calls each carried a hard-coded 60 seconds of their own and never read it. They now honour the player's
// setting, and never wait less than the 60 seconds they always had. If this breaks, a slow local model loses every
// letter and every summary while its conversations work.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BackgroundCallTimeoutTests
   {
      // CarolusIV's case: a player who gave the model 180 seconds gives his letters 180 seconds too.
      [Test]
      public void GIVEN_a_player_who_raised_the_timeout_WHEN_a_letter_is_written_THEN_it_gets_the_same_time()
      {
         BackgroundCallTimeout.Seconds(playerTimeoutSeconds: 180).Should().Be(180);
      }

      // A player who lowered the timeout to fail fast in conversation does not starve the letters below what they
      // always had: a letter nobody waits on is no reason to cut it short.
      [Test]
      public void GIVEN_a_player_who_lowered_the_timeout_WHEN_a_letter_is_written_THEN_it_still_gets_the_old_minute()
      {
         BackgroundCallTimeout.Seconds(playerTimeoutSeconds: 30).Should().Be(BackgroundCallTimeout.FloorSeconds);
         BackgroundCallTimeout.FloorSeconds.Should().Be(60, "the 60 seconds every background call had before");
      }
   }
}
