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

            AssemblyResolutionEntry e = ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "assign_role", TargetHeroId = "hero_1", Label = "Scout role", Detail = "Scout ahead", Turn = 1});

            e.Status.Should().Be(ResolutionStatus.Proposed);
            ledger.Entries.Should().ContainSingle().Which.Id.Should().Be("m1");
        }

        // The cancel path, end to end: an AGREED motion the player takes back before the lift becomes Reversed and
        // drops out of what commits, so nothing touches the world. This is the whole reason for commit-on-lift.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_reversed_before_the_lift_THEN_it_no_longer_commits()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "A purse of gold", Turn = 1});
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
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "patrol", TargetHeroId = "hero_1", Label = "Patrol", Detail = "Patrol the north", Turn = 1});

            ledger.Reverse("m1").Should().BeTrue();

            ledger.PendingCommit().Should().BeEmpty();
        }

        // A reversed motion is dead: the ledger refuses to agree it back to life, so a cancelled decision cannot
        // sneak into the commit through a later click.
        [Test]
        public void GIVEN_a_reversed_motion_WHEN_agreeing_it_again_THEN_the_ledger_refuses()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "patrol", TargetHeroId = "hero_1", Label = "Patrol", Detail = "Patrol the north", Turn = 1});
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
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1});
            ledger.Propose(new AssemblyResolutionEntry {Id = "m2", Kind = "patrol", TargetHeroId = "hero_2", Label = "Patrol", Detail = "North", Turn = 1});
            ledger.Agree("m1");

            ledger.PendingCommit().Select(e => e.Id).Should().ContainSingle().Which.Should().Be("m1");
        }

        // At the lift the bridge runs the effect and records the honest outcome. A failure must carry its reason so
        // the panel can tell the player WHY a decision did not take, which is the point of the verification surface.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_the_bridge_fails_at_the_lift_THEN_it_is_marked_failed_with_a_reason()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1});
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
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1});
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
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1});
            ledger.Propose(new AssemblyResolutionEntry {Id = "m2", Kind = "patrol", TargetHeroId = "hero_2", Label = "Patrol", Detail = "North", Turn = 1});
            ledger.Propose(new AssemblyResolutionEntry {Id = "m3", Kind = "assign_role", TargetHeroId = "hero_1", Label = "Scout", Detail = "Scout", Turn = 2});

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

        // Council audit M9: a duplicate id must be refused. Find/Agree/Reverse/MarkApplied all resolve by id, so a
        // second entry sharing an id could never be agreed or committed, yet would sit in the panel forever. The
        // ledger refuses it (returns null) and keeps the original, so the caller learns nothing was recorded.
        [Test]
        public void GIVEN_an_id_already_on_the_table_WHEN_proposing_it_again_THEN_the_ledger_refuses_and_keeps_the_first()
        {
            var ledger = new AssemblyLedger();
            ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "First", Turn = 1});

            AssemblyResolutionEntry second = ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "patrol", TargetHeroId = "hero_2", Label = "Patrol", Detail = "Second", Turn = 2});

            second.Should().BeNull();
            ledger.Entries.Should().ContainSingle().Which.Detail.Should().Be("First");
        }

        // Council audit M9: a null or blank id is unusable (nothing could ever act on the motion), so it is refused
        // outright rather than recorded as an unreachable row.
        [Test]
        public void GIVEN_a_blank_id_WHEN_proposing_THEN_the_ledger_refuses_and_records_nothing()
        {
            var ledger = new AssemblyLedger();

            ledger.Propose(new AssemblyResolutionEntry {Id = "", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1}).Should().BeNull();
            ledger.Propose(new AssemblyResolutionEntry {Id = "   ", Kind = "give_gold", TargetHeroId = "hero_1", Label = "Purse", Detail = "Gold", Turn = 1}).Should().BeNull();
            ledger.Entries.Should().BeEmpty();
        }

        // m2: the grounding hint (e.g. the settlement a quest pledge concerns) is set at proposal time, not bolted
        // on after, so the lift executor can never read a half-built entry whose target the caller forgot to attach.
        [Test]
        public void GIVEN_a_target_hint_WHEN_proposed_THEN_it_is_stored_on_the_entry()
        {
            var ledger = new AssemblyLedger();

            AssemblyResolutionEntry e = ledger.Propose(new AssemblyResolutionEntry {Id = "m1", Kind = "quest", TargetHeroId = "hero_1", Label = "Clear bandits", Detail = "Ride to Pravend", Turn = 1, TargetHint = "Pravend"});

            e.TargetHint.Should().Be("Pravend");
        }

        // Council audit M11: Entries is publicly settable so the mod can round-trip the ledger through JSON SyncData,
        // which means a corrupt or hand-edited save could restore blank-id or duplicate-id rows the lift could never
        // resolve. Sanitize is the load-time repair: it drops the blanks and the duplicates (keeping the first) so a
        // loaded council is as sound as a freshly built one.
        [Test]
        public void GIVEN_a_round_tripped_ledger_with_blank_and_duplicate_ids_WHEN_sanitized_THEN_only_sound_unique_entries_remain()
        {
            var ledger = new AssemblyLedger
            {
                Entries = new System.Collections.Generic.List<AssemblyResolutionEntry>
                {
                    new AssemblyResolutionEntry {Id = "m1", Detail = "Keep"},
                    new AssemblyResolutionEntry {Id = "", Detail = "Blank, drop"},
                    new AssemblyResolutionEntry {Id = "m1", Detail = "Duplicate, drop"},
                    new AssemblyResolutionEntry {Id = "m2", Detail = "Keep too"}
                }
            };

            int removed = ledger.Sanitize();

            removed.Should().Be(2);
            ledger.Entries.Select(e => e.Id).Should().Equal("m1", "m2");
            ledger.Entries.First(e => e.Id == "m1").Detail.Should().Be("Keep");
        }
    }
}
