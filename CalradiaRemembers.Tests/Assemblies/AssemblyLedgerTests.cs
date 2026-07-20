// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the assembly ledger: the guarded record the roster panel reads and the executor commits at the lift.
// The cancel path matters most here, exercised through the real container: a withdrawn motion must vanish from
// what commits, and an illegal move (agreeing a reversed motion, reversing a committed one) must be refused.

#region

using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyLedgerTests
    {
        // A proposed motion enters on the table, not yet binding. If it entered as Agreed it would commit at the
        // lift without anyone consenting to it.
        [Test]
        public void GIVEN_a_new_motion_WHEN_proposed_THEN_it_is_recorded_as_proposed()
        {
            var ledger = new AssemblyLedger();

            AssemblyResolutionEntry e = ledger.Propose("m1", "assign_role", "hero_1", "Scout role", "Scout ahead", turn: 1);

            e.Status.Should().Be(ResolutionStatus.Proposed);
            ledger.Entries.Should().ContainSingle().Which.Id.Should().Be("m1");
        }

        // The cancel path, end to end: an AGREED motion the player takes back before the lift becomes Reversed and
        // drops out of what commits, so nothing touches the world. This is the whole reason for commit-on-lift.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_reversed_before_the_lift_THEN_it_no_longer_commits()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "give_gold", "hero_1", "Purse", "A purse of gold", turn: 1);
            ledger.Agree("m1").Should().BeTrue();

            ledger.Reverse("m1").Should().BeTrue();

            ledger.Entries.Single().Status.Should().Be(ResolutionStatus.Reversed);
            ledger.PendingCommit().Should().BeEmpty();
        }

        // Cancelling before agreement works too: a motion floated and immediately withdrawn leaves no commit.
        [Test]
        public void GIVEN_a_merely_proposed_motion_WHEN_reversed_THEN_it_is_withdrawn_and_commits_nothing()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "patrol", "hero_1", "Patrol", "Patrol the north", turn: 1);

            ledger.Reverse("m1").Should().BeTrue();

            ledger.PendingCommit().Should().BeEmpty();
        }

        // A reversed motion is dead: the ledger refuses to agree it back to life, so a cancelled decision cannot
        // sneak into the commit through a later click.
        [Test]
        public void GIVEN_a_reversed_motion_WHEN_agreeing_it_again_THEN_the_ledger_refuses()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "patrol", "hero_1", "Patrol", "Patrol the north", turn: 1);
            ledger.Reverse("m1");

            ledger.Agree("m1").Should().BeFalse();
            ledger.Entries.Single().Status.Should().Be(ResolutionStatus.Reversed);
        }

        // The lift commits only the agreed motions. A ledger holding one agreed and one still-proposed motion must
        // offer exactly the agreed one for commit, so an un-consented motion never runs.
        [Test]
        public void GIVEN_a_mix_of_agreed_and_proposed_WHEN_asked_what_commits_THEN_only_the_agreed_is_offered()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "give_gold", "hero_1", "Purse", "Gold", turn: 1);
            ledger.Propose("m2", "patrol", "hero_2", "Patrol", "North", turn: 1);
            ledger.Agree("m1");

            ledger.PendingCommit().Select(e => e.Id).Should().ContainSingle().Which.Should().Be("m1");
        }

        // At the lift the bridge runs the effect and records the honest outcome. A failure must carry its reason so
        // the panel can tell the player WHY a decision did not take, which is the point of the verification surface.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_the_bridge_fails_at_the_lift_THEN_it_is_marked_failed_with_a_reason()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "give_gold", "hero_1", "Purse", "Gold", turn: 1);
            ledger.Agree("m1");

            ledger.MarkFailed("m1", "not enough gold").Should().BeTrue();

            AssemblyResolutionEntry e = ledger.Entries.Single();
            e.Status.Should().Be(ResolutionStatus.Failed);
            e.FailureReason.Should().Be("not enough gold");
        }

        // A committed effect is beyond recall: once applied at the lift, the ledger refuses to reverse it, because
        // a real world change now exists that a council withdrawal cannot undo.
        [Test]
        public void GIVEN_an_applied_motion_WHEN_asked_to_reverse_THEN_the_ledger_refuses()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "give_gold", "hero_1", "Purse", "Gold", turn: 1);
            ledger.Agree("m1");
            ledger.MarkApplied("m1").Should().BeTrue();

            ledger.Reverse("m1").Should().BeFalse();
            ledger.Entries.Single().Status.Should().Be(ResolutionStatus.Applied);
        }

        // The panel groups by hero: EntriesFor returns only the motions landing on that member, so one lord's row
        // never shows another's business.
        [Test]
        public void GIVEN_motions_on_several_heroes_WHEN_asked_for_one_hero_THEN_only_his_motions_return()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose("m1", "give_gold", "hero_1", "Purse", "Gold", turn: 1);
            ledger.Propose("m2", "patrol", "hero_2", "Patrol", "North", turn: 1);
            ledger.Propose("m3", "assign_role", "hero_1", "Scout", "Scout", turn: 2);

            ledger.EntriesFor("hero_1").Select(e => e.Id).Should().BeEquivalentTo(new[] {"m1", "m3"});
        }

        // Defensive: acting on an unknown motion id must fail quietly rather than throw, so a stale click from the
        // UI (a row that was just reversed) cannot crash the council.
        [Test]
        public void GIVEN_an_unknown_id_WHEN_agreeing_or_reversing_THEN_it_fails_without_throwing()
        {
            var ledger = new AssemblyLedger();

            ledger.Agree("nope").Should().BeFalse();
            ledger.Reverse("nope").Should().BeFalse();
            ledger.MarkFailed("nope", "reason").Should().BeFalse();
        }
    }
}
