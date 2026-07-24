// Code written by Gabriel Mailhot, 21/07/2026.
// Documents the two session-ledger decisions the [RESOLUTION] channel needs on top of AssemblyLedger and
// ResolutionLifecycle: whether a new pledge may be recorded (generalising QuestService's own "one task at a
// time" rule to the council), and which of a member's live pledges a bare "type: withdraw" targets, since the
// block carries no resolution id and the owner rejected a manual delete button. Both share CouncilActorPolicy's
// discipline: an ambiguous case is refused, never guessed.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilResolutionPolicyTests
    {
        private static AssemblyResolutionEntry Entry(string id, string kind, ResolutionStatus status, string detail = "")
            => new() {Id = id, Kind = kind, TargetHeroId = "hero_sley", Detail = detail, Status = status};

        // The floor case: a member with nothing on the table yet must always be recordable, or the very first
        // pledge of a session would be refused.
        [Test]
        public void GIVEN_no_existing_entries_WHEN_recording_THEN_it_is_allowed()
        {
            CouncilResolutionPolicy.CanRecord("quest", new List<AssemblyResolutionEntry>()).Should().BeTrue();
            CouncilResolutionPolicy.CanRecord("quest", null).Should().BeTrue();
        }

        // ── The perception/deed split at a council (ratified 2026-07-24) ─────

        // The heart of the ruling: PERCEPTION runs on the spot, DEEDS defer. A lord angered at the table must be
        // angrier at once so it colours how he weighs later requests, so change_relation alone executes live;
        // the bridge reads exactly this method to decide, so an "immediate" here is a change the player feels
        // during the sitting.
        [Test]
        public void GIVEN_the_perception_verb_WHEN_asking_if_it_runs_at_a_council_THEN_it_executes_immediately()
        {
            CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil(CouncilResolutionPolicy.PerceptionActionType).Should().BeTrue();
            CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil("Change_Relation").Should().BeTrue(); // case-insensitive
        }

        // The other side, and the whole point of the guard: a world transfer or a commitment must NOT be sealed
        // at the table. If any of these returned true, a hallucinated [ACTION] would hand over gold or lend
        // troops on the spot, the exact "nothing executes here" promise the design makes and the code must keep.
        [Test]
        public void GIVEN_a_world_transfer_or_commitment_verb_WHEN_asking_if_it_runs_at_a_council_THEN_it_is_deferred()
        {
            foreach (string verb in new[] {"give_gold", "give_troops", "lend_troops", "give_prisoner", "join_party", "marry", "scheme_assist", "grant_blessing", "pledge_against"})
                CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil(verb).Should().BeFalse($"{verb} is a deed, deferred to [RESOLUTION]");
        }

        // A blank or null type has nothing to run: it must not slip through as "immediate" and hit the dispatch.
        [Test]
        public void GIVEN_a_blank_action_type_WHEN_asking_if_it_runs_at_a_council_THEN_it_is_not_immediate()
        {
            CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil(null).Should().BeFalse();
            CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil("   ").Should().BeFalse();
        }

        // The rule this policy exists to enforce: QuestService.IssueQuest already refuses to stack a new quest
        // on an unfinished one ("one task at a time"). A council pledge must honour the same discipline, or the
        // table could promise the same member two tasks in one session with no way to tell which one is live.
        [Test]
        public void GIVEN_a_live_pledge_of_the_same_kind_WHEN_recording_another_THEN_it_is_refused()
        {
            var existing = new List<AssemblyResolutionEntry> {Entry("m1", "quest", ResolutionStatus.Agreed)};

            CouncilResolutionPolicy.CanRecord("quest", existing).Should().BeFalse();
        }

        // A withdrawn (or otherwise terminal) pledge is no longer live: the member is free to take up a new
        // task of the same kind, or a withdrawn pledge would permanently block them from ever being asked again.
        [Test]
        public void GIVEN_a_withdrawn_pledge_of_the_same_kind_WHEN_recording_another_THEN_it_is_allowed()
        {
            var existing = new List<AssemblyResolutionEntry> {Entry("m1", "quest", ResolutionStatus.Reversed)};

            CouncilResolutionPolicy.CanRecord("quest", existing).Should().BeTrue();
        }

        // A different kind never blocks: this slice only builds "quest", but the rule must not over-reach and
        // refuse an unrelated future catalogue kind just because SOME resolution is live for that member.
        [Test]
        public void GIVEN_a_live_pledge_of_a_different_kind_WHEN_recording_THEN_it_is_allowed()
        {
            var existing = new List<AssemblyResolutionEntry> {Entry("m1", "assign_party_role", ResolutionStatus.Agreed)};

            CouncilResolutionPolicy.CanRecord("quest", existing).Should().BeTrue();
        }

        // The common case: exactly one live pledge for this member always resolves outright, with no need for
        // the withdrawal to even name what it targets.
        [Test]
        public void GIVEN_exactly_one_live_entry_WHEN_selecting_a_withdraw_target_THEN_it_is_returned()
        {
            var entries = new List<AssemblyResolutionEntry> {Entry("m1", "quest", ResolutionStatus.Agreed, "ride to Varcheg")};

            CouncilResolutionPolicy.SelectWithdrawTarget(entries, null).Should().BeSameAs(entries[0]);
        }

        // Nothing to withdraw (every prior pledge from this member is already terminal) must resolve cleanly to
        // null rather than throw or reverse something that no longer applies.
        [Test]
        public void GIVEN_no_live_entries_WHEN_selecting_a_withdraw_target_THEN_it_is_refused()
        {
            var entries = new List<AssemblyResolutionEntry> {Entry("m1", "quest", ResolutionStatus.Applied)};

            CouncilResolutionPolicy.SelectWithdrawTarget(entries, "anything").Should().BeNull();
            CouncilResolutionPolicy.SelectWithdrawTarget(new List<AssemblyResolutionEntry>(), "anything").Should().BeNull();
            CouncilResolutionPolicy.SelectWithdrawTarget(null, "anything").Should().BeNull();
        }

        // Two live pledges from the same member: the detail the model gave for the withdrawal disambiguates,
        // the same bidirectional-substring tolerance CouncilActorPolicy already applies to a name.
        [Test]
        public void GIVEN_two_live_entries_WHEN_the_detail_matches_only_one_THEN_that_one_is_returned()
        {
            var entries = new List<AssemblyResolutionEntry> {
                Entry("m1", "quest", ResolutionStatus.Agreed, "ride to Varcheg and clear the bandits"),
                Entry("m2", "quest", ResolutionStatus.Agreed, "garrison the north gate")
            };

            CouncilResolutionPolicy.SelectWithdrawTarget(entries, "Varcheg").Should().BeSameAs(entries[0]);
        }

        // The case this policy exists to refuse: two live pledges and nothing (or nothing unambiguous) to tell
        // them apart. A wrong guess here withdraws the pledge the player meant to KEEP, which is worse than the
        // withdrawal simply not landing.
        [Test]
        public void GIVEN_two_live_entries_WHEN_the_detail_cannot_disambiguate_THEN_it_is_refused()
        {
            var entries = new List<AssemblyResolutionEntry> {
                Entry("m1", "quest", ResolutionStatus.Agreed, "ride to Varcheg"),
                Entry("m2", "quest", ResolutionStatus.Agreed, "garrison the gate")
            };

            CouncilResolutionPolicy.SelectWithdrawTarget(entries, null).Should().BeNull();
            CouncilResolutionPolicy.SelectWithdrawTarget(entries, "something unrelated").Should().BeNull();
        }
    }
}
