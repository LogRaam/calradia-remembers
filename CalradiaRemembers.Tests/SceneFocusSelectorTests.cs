// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SceneFocusSelectorTests
   {
      [Test]
      public void GIVEN_a_pool_of_one_entry_WHEN_picking_an_index_THEN_it_is_always_zero()
      {
         SceneFocusSelector.PickIndex(1, 0, _ => throw new System.Exception("should not roll")).Should().Be(0);
      }

      [Test]
      public void GIVEN_no_prior_round_WHEN_picking_an_index_THEN_any_draw_from_the_full_pool_is_allowed()
      {
         SceneFocusSelector.PickIndex(5, -1, _ => 3).Should().Be(3);
      }

      [Test]
      public void GIVEN_an_out_of_range_last_index_WHEN_picking_an_index_THEN_it_is_treated_as_no_prior_round()
      {
         //Arrange: a stale lastIndex from a smaller pool (e.g. after a pool swap) must not crash the seam.
         SceneFocusSelector.PickIndex(5, 9, _ => 4).Should().Be(4);
      }

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
   }
}
