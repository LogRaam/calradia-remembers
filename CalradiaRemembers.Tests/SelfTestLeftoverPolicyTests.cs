// Code written by Gabriel Mailhot, 15/11/2026.
// What SelfTestLeftoverPolicy decides: which captives a cr.test all test left in the player's prison. Gabriel's play test
// of 15/11/2026 crashed on one: a test nemesis captured, kept, captured again (held twice), executed once, and the
// leftover copy walked out alive when the player's prisoners were freed. If this misses a leftover, a save made after
// cr.test all carries the test's captive into play; if it names the player's own prisoners, every test fails for nothing.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SelfTestLeftoverPolicyTests
   {
      // THE REPORT: the test kept the nemesis it captured.
      [Test]
      public void GIVEN_a_captive_the_test_took_and_kept_WHEN_compared_THEN_he_is_named()
      {
         SelfTestLeftoverPolicy.Leftovers(new Dictionary<string, int>(), new Dictionary<string, int> {["Nabb"] = 1})
                               .Should().Equal("Nabb");
      }

      // THE REPORT: the same captive held twice, the copy an execution leaves behind.
      [Test]
      public void GIVEN_a_captive_taken_again_while_held_WHEN_compared_THEN_he_is_named_with_the_count()
      {
         SelfTestLeftoverPolicy.Leftovers(new Dictionary<string, int> {["Nabb"] = 1}, new Dictionary<string, int> {["Nabb"] = 2})
                               .Should().Equal("Nabb (x2)");
      }

      // STAKE: the player's own prisoners, there before the test, never fail it.
      [Test]
      public void GIVEN_the_players_own_prisoners_untouched_WHEN_compared_THEN_nothing_is_named()
      {
         var held = new Dictionary<string, int> {["Lucon"] = 1};
         SelfTestLeftoverPolicy.Leftovers(held, new Dictionary<string, int>(held)).Should().BeEmpty();
      }

      // STAKE: a test that frees a captive, or executes him, leaves nothing behind.
      [Test]
      public void GIVEN_a_captive_freed_by_the_test_WHEN_compared_THEN_nothing_is_named()
      {
         SelfTestLeftoverPolicy.Leftovers(new Dictionary<string, int> {["Lucon"] = 1}, new Dictionary<string, int>()).Should().BeEmpty();
      }
   }
}
