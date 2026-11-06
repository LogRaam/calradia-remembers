// Code written by Gabriel Mailhot, 05/11/2026.
// Which piece of the party's goods a gift, a sale or a hand-over means, quality included. avalanche24 (Nexus, 05/11/2026)
// gave a companion a Masterwork Noble Bow; the bag showed a plain "Noble Bow" and the companion wore a plain one. If the
// key loses the quality, a fine piece leaves as a plain one, or the plain piece leaves when the fine one was chosen.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RosterItemKeyTests
   {
      private static readonly List<HeldGoods> PlainAndMasterwork = new List<HeldGoods> {
         new HeldGoods {ItemId = "noble_bow"}, new HeldGoods {ItemId = "noble_bow", ModifierId = "masterwork"}
      };

      // STAKE: the bow picked as Masterwork is the bow that leaves, not the plain one beside it.
      [Test]
      public void GIVEN_a_plain_and_a_masterwork_bow_WHEN_the_masterwork_one_is_picked_THEN_that_very_stack_is_found()
      {
         string key = RosterItemKey.Of("noble_bow", "masterwork");

         RosterItemKey.Pick(PlainAndMasterwork, key).Should().Be(1);
      }

      // STAKE: a gift named in words ("the noble bow") gives the plain piece and keeps the fine one.
      [Test]
      public void GIVEN_a_plain_and_a_masterwork_bow_WHEN_a_key_without_quality_asks_THEN_the_plain_one_is_found()
      {
         RosterItemKey.Pick(PlainAndMasterwork, "noble_bow").Should().Be(0);
      }

      // STAKE: avalanche24's case, a single bow and it is Masterwork: named in words, it is still found.
      [Test]
      public void GIVEN_only_a_masterwork_bow_WHEN_a_key_without_quality_asks_THEN_the_masterwork_one_is_found()
      {
         var held = new List<HeldGoods> {new HeldGoods {ItemId = "noble_bow", ModifierId = "masterwork"}};

         RosterItemKey.Pick(held, "noble_bow").Should().Be(0);
      }

      // STAKE: a fine piece already gone is never replaced by a plain one the player did not choose.
      [Test]
      public void GIVEN_the_masterwork_bow_is_gone_WHEN_its_key_asks_THEN_nothing_is_found()
      {
         var held = new List<HeldGoods> {new HeldGoods {ItemId = "noble_bow"}};

         RosterItemKey.Pick(held, RosterItemKey.Of("noble_bow", "masterwork")).Should().Be(-1);
      }

      // STAKE: a plain piece keeps the bare id, so every offer and old caller that wrote the id still works.
      [Test]
      public void GIVEN_a_plain_piece_WHEN_keyed_THEN_the_key_is_the_bare_item_id_and_reads_back()
      {
         RosterItemKey.Of("grain", null).Should().Be("grain");
         RosterItemKey.ItemIdOf(RosterItemKey.Of("noble_bow", "masterwork")).Should().Be("noble_bow");
         RosterItemKey.ItemIdOf("grain").Should().Be("grain");
      }
   }
}
