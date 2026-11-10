// Code written by Gabriel Mailhot, 10/11/2026.
// What RelationBaselineLedger.Prune drops, and that dropping it changes nothing a player can see. tashmetu (Nexus,
// 09/11/2026): in a long campaign this ledger reached 18.2 million bytes, the record that broke his save; Gabriel
// (09/11/2026): "Ce que CR peut faire c'est de reduire le poids de ses donnees." If the prune drops too much, a history
// built in play is read again as a blind start (a true feud forgotten); if it drops too little, the save keeps growing.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Relations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Relations
{
   [TestFixture]
   public sealed class RelationBaselineLedgerPruneTests
   {
      // STAKE: a pair back where it began is dropped, and the bond the player reads is exactly the same after as before.
      [Test]
      public void GIVEN_a_pair_back_at_its_start_WHEN_pruned_THEN_it_is_dropped_and_the_bond_reads_the_same()
      {
         var ledger = new RelationBaselineLedger();
         ledger.Saw("lord_a", "lord_b", -40);
         var facts = new PairBondFacts {Personal = -40, Baseline = ledger.BaselineOf("lord_a", "lord_b"), SameRealm = true};
         PairBond before = PairBondPolicy.Assess(facts);

         ledger.Prune((a, b) => -40).Should().Be(1);

         ledger.BaselineOf("lord_a", "lord_b").Should().BeNull();
         PairBondPolicy.Assess(new PairBondFacts {Personal = -40, Baseline = null, SameRealm = true}).Should().BeEquivalentTo(before);
      }

      // STAKE: a history built in play (a feud grown from -10 to -60) is kept whole.
      [Test]
      public void GIVEN_a_pair_whose_value_moved_since_its_start_WHEN_pruned_THEN_it_is_kept()
      {
         var ledger = new RelationBaselineLedger();
         ledger.Saw("lord_a", "lord_b", -10);

         ledger.Prune((a, b) => -60).Should().Be(0);

         ledger.BaselineOf("lord_a", "lord_b").Should().Be(-10);
      }

      // STAKE: a pair with someone no longer in the world weighs on the save for nothing.
      [Test]
      public void GIVEN_a_pair_one_of_whom_is_gone_WHEN_pruned_THEN_it_is_dropped()
      {
         var ledger = new RelationBaselineLedger();
         ledger.Saw("lord_a", "dead_lord", 20);
         ledger.Saw("lord_a", "lord_b", 0);

         ledger.Prune((a, b) => a == "dead_lord" || b == "dead_lord" ? (int?) null : 30).Should().Be(1);

         ledger.BaselineOf("lord_a", "dead_lord").Should().BeNull();
         ledger.BaselineOf("lord_a", "lord_b").Should().Be(0);
      }

      // STAKE: values past the scale's end read as its end, as the bond reads them; a start of 120 against a value of 100
      // is back at its start.
      [Test]
      public void GIVEN_a_start_past_the_scale_end_WHEN_pruned_against_the_end_THEN_it_counts_as_back_at_its_start()
      {
         var ledger = new RelationBaselineLedger {Baselines = new Dictionary<string, int> {["lord_a|lord_b"] = 120}};

         ledger.Prune((a, b) => 100).Should().Be(1);
      }

      // STAKE: a key the ledger cannot read as two ids is never thrown away on a guess.
      [Test]
      public void GIVEN_a_key_that_is_not_two_ids_WHEN_pruned_THEN_it_is_kept()
      {
         var ledger = new RelationBaselineLedger {Baselines = new Dictionary<string, int> {["odd"] = 5, ["a|b|c"] = 5}};

         ledger.Prune((a, b) => null).Should().Be(0);
         ledger.Baselines.Should().HaveCount(2);
      }
   }
}
