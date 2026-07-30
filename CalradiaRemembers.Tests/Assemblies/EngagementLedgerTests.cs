// Code written by Gabriel Mailhot, 30/07/2026.
// The engagements ledger is the live MIRROR of a council conversation's standing decisions: what the right-hand
// column shows, and what applies to the world when the council lifts. It has no cancel button by design (Gabriel
// ruled that an immersion break, 2026-07-30); a decision is walked back purely by the conversation no longer
// asserting it, so the extraction snapshot drops it and this ledger must drop it too. What breaks if reconcile is
// wrong: a decision the table rescinded would still be on the panel and would still apply at the lift (the exact
// dishonesty the "player sees it, then it applies" rule exists to prevent), or a decision restated each turn
// would pile up as duplicate rows. Gabriel asked specifically for the appears-then-retraction pair pinned here.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class EngagementLedgerTests
    {
        // STAKE (Gabriel's explicit ask, half one): a decision spoken in the council must SHOW UP in the ledger,
        // because the right column is the only place the player sees what the table has committed to before it
        // touches the world. If it did not appear, an agreement would apply at the lift the player never saw form.
        [Test]
        public void GIVEN_a_declaration_in_the_snapshot_WHEN_reconciling_THEN_it_appears_in_the_ledger()
        {
            var ledger = new EngagementLedger();

            ledger.Reconcile(Snapshot(Item("raid-sturgia", "Aeldric", "Aeldric leads the raid on Sturgia", EngagementStatus.Agreed)));

            ledger.Engagements.Should().ContainSingle()
                  .Which.Text.Should().Be("Aeldric leads the raid on Sturgia");
        }

        // STAKE (Gabriel's explicit ask, half two): once the table asks to no longer honour a decision, the
        // conversation stops asserting it, so the next extraction snapshot omits it, and the point must LEAVE the
        // ledger. If it lingered, a rescinded decision would still commit at the lift, breaking the promise that
        // only what the player currently sees standing is applied.
        [Test]
        public void GIVEN_a_standing_declaration_WHEN_a_later_snapshot_no_longer_carries_it_THEN_it_is_removed()
        {
            var ledger = new EngagementLedger();
            ledger.Reconcile(Snapshot(Item("raid-sturgia", "Aeldric", "Aeldric leads the raid on Sturgia", EngagementStatus.Agreed)));

            // The table talked it back: the conversation no longer stands for the raid, so the snapshot drops it.
            ledger.Reconcile(Snapshot());

            ledger.Engagements.Should().BeEmpty();
        }

        // STAKE: a decision the table keeps restating across several turns is ONE engagement, not one per turn.
        // Keying on the stable id is what prevents the panel filling with duplicate rows of the same commitment.
        [Test]
        public void GIVEN_the_same_declaration_across_two_snapshots_WHEN_reconciling_twice_THEN_it_is_not_duplicated()
        {
            var ledger = new EngagementLedger();
            EngagementSnapshotItem[] same = Snapshot(Item("dowry", "the clan", "A dowry of 5000 for the marriage", EngagementStatus.Discussed));

            ledger.Reconcile(same);
            ledger.Reconcile(same);

            ledger.Engagements.Should().ContainSingle();
        }

        // STAKE: a decision that starts as talk and later settles must move Discussed -> Agreed IN PLACE, because
        // only Agreed engagements apply at the lift; if the update forked a second row the discussed one would
        // never clear and the settled one might never be seen.
        [Test]
        public void GIVEN_a_discussed_declaration_WHEN_it_becomes_agreed_in_a_later_snapshot_THEN_its_status_moves_in_place()
        {
            var ledger = new EngagementLedger();
            ledger.Reconcile(Snapshot(Item("peace-vlandia", "the clan", "Offer peace to Vlandia", EngagementStatus.Discussed)));

            ledger.Reconcile(Snapshot(Item("peace-vlandia", "the clan", "Offer peace to Vlandia", EngagementStatus.Agreed)));

            ledger.Engagements.Should().ContainSingle().Which.Status.Should().Be(EngagementStatus.Agreed);
        }

        // STAKE: only AGREED engagements commit at the lift. A decision still under discussion is something the
        // player has NOT seen the table settle, so applying it would be exactly the unseen-agreement dishonesty
        // the whole design guards against.
        [Test]
        public void GIVEN_a_mix_of_discussed_and_agreed_WHEN_asking_what_commits_THEN_only_the_agreed_ones_are_returned()
        {
            var ledger = new EngagementLedger();
            ledger.Reconcile(Snapshot(
                Item("raid", "Aeldric", "Aeldric leads the raid", EngagementStatus.Agreed),
                Item("levy", "the clan", "Raise a new levy", EngagementStatus.Discussed)));

            ledger.PendingCommit().Select(e => e.Key).Should().BeEquivalentTo("raid");
        }

        // STAKE: an extraction hiccup (a keyless or textless item) must never plant an empty row. An empty row on
        // the panel would read as a decision, and a keyless one could never be matched or retracted afterwards.
        [Test]
        public void GIVEN_snapshot_items_with_no_key_or_no_text_WHEN_reconciling_THEN_they_are_ignored()
        {
            var ledger = new EngagementLedger();

            ledger.Reconcile(Snapshot(
                Item("", "someone", "A decision with no key", EngagementStatus.Agreed),
                Item("empty", "someone", "   ", EngagementStatus.Agreed)));

            ledger.Engagements.Should().BeEmpty();
        }

        #region private

        private static EngagementSnapshotItem Item(string key, string subject, string text, EngagementStatus status)
            => new EngagementSnapshotItem {Key = key, Subject = subject, Text = text, Status = status};

        private static EngagementSnapshotItem[] Snapshot(params EngagementSnapshotItem[] items) => items;

        #endregion
    }
}
