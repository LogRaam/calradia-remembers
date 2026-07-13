// Code written by Gabriel Mailhot, 01/07/2026.
// Pins the pure avoid-repeat rule behind a captive scene's driving "focus": pick a random index into the
// flavour-text pool, but never the one the previous round already used.
//
// WHY IT MATTERS: without this, a captor could open two consecutive rounds with the identical focus line,
// which is exactly the flatness/repetition complaint (ROADMAP: "Continue-sequence quality") the scene
// director's variety pass was built to fix. CaptiveSceneText.PickFocus feeds this straight into the prompt,
// so a wrong index here is a repeated line the player actually sees.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SceneFocusSelectorTests
   {
      // The core no-repeat guarantee, swept across every pool size / lastIndex / roll combination: the
      // returned index must never equal lastIndex, or a captor could open two rounds with the same line.
      [Test]
      public void GIVEN_a_pool_larger_than_one_WHEN_picking_an_index_THEN_it_never_returns_the_last_index()
      {
         for (var poolSize = 2; poolSize <= 6; poolSize++)
            for (var lastIndex = 0; lastIndex < poolSize; lastIndex++)
               for (var candidate = 0; candidate < poolSize - 1; candidate++)
               {
                  int picked = SceneFocusSelector.PickIndex(poolSize, lastIndex, _ => candidate);
                  picked.Should().NotBe(lastIndex);
                  picked.Should().BeInRange(0, poolSize - 1);
               }
      }

      // The other half of the guarantee: avoiding the repeat must not also shrink the pool the player actually
      // sees over time. Every index except the last-used one must still be reachable by some roll, or the
      // "variety" pass would quietly narrow the pool instead of widening it.
      [Test]
      public void GIVEN_a_pool_larger_than_one_WHEN_scanning_every_candidate_roll_THEN_every_index_but_the_last_one_is_reachable()
      {
         const int poolSize = 5;
         const int lastIndex = 2;

         List<int> reachable = Enumerable.Range(0, poolSize - 1)
                                         .Select(candidate => SceneFocusSelector.PickIndex(poolSize, lastIndex, _ => candidate))
                                         .Distinct()
                                         .OrderBy(i => i)
                                         .ToList();

         reachable.Should().BeEquivalentTo(new[] {0, 1, 3, 4});
      }

      // A single-entry pool has no alternative to avoid the repeat with, so the rule must short-circuit to
      // index 0 WITHOUT rolling at all (the throwing roll proves no draw is attempted): rolling here would be
      // wasted work at best, or (with a naive implementation) an infinite retry loop at worst.
      [Test]
      public void GIVEN_a_pool_of_one_entry_WHEN_picking_an_index_THEN_it_is_always_zero()
      {
         SceneFocusSelector.PickIndex(1, 0, _ => throw new Exception("should not roll")).Should().Be(0);
      }

      // CaptiveSceneText.PickFocus draws from either the DARK or the POSITIVE pool depending on scene intent,
      // so lastIndex from one pool can be stale (out of range) against the other after a pool swap. This must
      // degrade to "no prior round" rather than crash or silently misbehave on an out-of-bounds index.
      [Test]
      public void GIVEN_an_out_of_range_last_index_WHEN_picking_an_index_THEN_it_is_treated_as_no_prior_round()
      {
         //Arrange: a stale lastIndex from a smaller pool (e.g. after a pool swap) must not crash the seam.
         SceneFocusSelector.PickIndex(5, 9, _ => 4).Should().Be(4);
      }

      // A fresh scene resets ChatViewModel._lastSceneFocusIndex to -1: the opening beat has no prior round to
      // avoid, so the full pool (including what would otherwise be the excluded index) must be reachable.
      [Test]
      public void GIVEN_no_prior_round_WHEN_picking_an_index_THEN_any_draw_from_the_full_pool_is_allowed()
      {
         SceneFocusSelector.PickIndex(5, -1, _ => 3).Should().Be(3);
      }
   }
}