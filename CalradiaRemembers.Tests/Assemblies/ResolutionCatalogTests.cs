// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the resolution catalogue: that every motion kind has exactly one definition, and that the by-scope
// query discriminates (a companions' council must not be offered a clan-lords' motion, and vice versa). This
// feeds the assembly menu: a wrong scope mapping would put a fief grant on a companions' council that cannot
// carry it, or hide a motion a council should have.

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class ResolutionCatalogTests
    {
        // Every SCOPED MENU kind must resolve to a definition: the assembly menu selects a motion by its kind, so
        // a menu kind with no catalogue entry would be an unraisable dead motion. Quest is deliberately excluded:
        // it is the universal free task pledge, not a scoped menu motion, so it carries no ResolutionDefinition.
        [Test]
        public void GIVEN_every_scoped_menu_kind_WHEN_looked_up_THEN_each_has_exactly_one_definition()
        {
            foreach (ResolutionKind kind in Enum.GetValues(typeof(ResolutionKind)).Cast<ResolutionKind>())
            {
                if (kind == ResolutionKind.Quest) continue;

                ResolutionDefinition def = ResolutionCatalog.Get(kind);
                def.Kind.Should().Be(kind);
                def.Label.Should().NotBeNullOrWhiteSpace();
            }
        }

        // The companions' council raises its own set and NOTHING from another scope: the clan-lords' GrantFief
        // must not appear here. If it did, the player could move a fief grant on a council of non-noble hirelings.
        [Test]
        public void GIVEN_a_companions_council_WHEN_the_catalogue_is_queried_THEN_only_companions_motions_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.Companions).Select(d => d.Kind).ToList();

            kinds.Should().Contain(new[]
            {
                ResolutionKind.AssignPartyRole, ResolutionKind.AppointGovernor,
                ResolutionKind.DispatchOnMission, ResolutionKind.GrantStipend, ResolutionKind.RejoinParty
            });
            kinds.Should().NotContain(ResolutionKind.GrantFief);
        }

        // The mirror check: the clan-lords' scope returns its motion and none of the companions' set, proving the
        // filter discriminates both ways rather than leaking a common list.
        [Test]
        public void GIVEN_a_clan_lords_council_WHEN_the_catalogue_is_queried_THEN_only_clan_lords_motions_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.ClanLords).Select(d => d.Kind).ToList();

            kinds.Should().Equal(ResolutionKind.GrantFief);
        }

        // The family council has no motions yet (they arrive with that variant); the query must return an empty
        // list, never throw, so wiring the family variant later is purely additive.
        [Test]
        public void GIVEN_a_scope_with_no_motions_yet_WHEN_queried_THEN_it_returns_empty_without_throwing()
        {
            ResolutionCatalog.ForScope(CouncilScope.Family).Should().BeEmpty();
        }

        // M4: the catalogue enum and the runtime string vocabulary used to be disjoint ("quest" existed only as a
        // free string, no enum member; the 5 enum kinds had no string). KeyOf/TryParseKey are the one bridge, so
        // every kind round-trips to its key and back: get this wrong and the two vocabularies drift apart again.
        [Test]
        public void GIVEN_every_kind_WHEN_its_key_is_parsed_back_THEN_it_round_trips()
        {
            foreach (ResolutionKind kind in Enum.GetValues(typeof(ResolutionKind)).Cast<ResolutionKind>())
            {
                string key = ResolutionCatalog.KeyOf(kind);
                key.Should().NotBeNullOrWhiteSpace();
                ResolutionCatalog.TryParseKey(key, out ResolutionKind parsed).Should().BeTrue();
                parsed.Should().Be(kind);
            }
        }

        // A kind string the model never emits (garbage, or a future kind not yet mapped) must not resolve, so the
        // lift can reject it cleanly instead of mistaking it for a real motion.
        [Test]
        public void GIVEN_an_unknown_key_WHEN_parsed_THEN_it_is_refused()
        {
            ResolutionCatalog.TryParseKey("nonsense", out _).Should().BeFalse();
            ResolutionCatalog.TryParseKey("", out _).Should().BeFalse();
            ResolutionCatalog.TryParseKey(null, out _).Should().BeFalse();
        }

        // M4: only a kind with a live executor is executable. Quest, AppointGovernor (CouncilLift.SettleAppointGovernor),
        // AssignPartyRole (CouncilLift.SettleAssignPartyRole), closing bug d, RejoinParty (CouncilLift.SettleRejoinParty)
        // and, the fourth executor, DispatchOnMission (CouncilLift.SettleDispatchMission) qualify today; the
        // remaining scoped menu motions are recorded but not yet carried out, so teaching them would promise a
        // deed the lift silently drops (the MarkFailed "this kind of decision is not yet settled" path the audit
        // flagged).
        [Test]
        public void GIVEN_the_kinds_WHEN_asked_which_execute_THEN_only_quest_appoint_governor_assign_party_role_rejoin_party_and_dispatch_mission_do_today()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.Quest).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.AppointGovernor).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.AssignPartyRole).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.RejoinParty).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.DispatchOnMission).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.GrantFief).Should().BeFalse();
            ResolutionCatalog.IsExecutable(ResolutionKind.GrantStipend).Should().BeFalse();
        }

        // Companions is both AppointGovernor's and AssignPartyRole's own scope: its executable key set must now
        // surface "appoint_governor" and "assign_party_role" alongside the universal "quest", or the lift-gate
        // change above would be invisible to anything reading the catalogue the way the prompt-offering wire does.
        [Test]
        public void GIVEN_a_companions_council_WHEN_asked_for_executable_kinds_THEN_appoint_governor_and_assign_party_role_are_offered()
        {
            var keys = ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions);

            keys.Should().Contain("appoint_governor");
            keys.Should().Contain("assign_party_role");
        }

        // Bug d (the third executable menu motion): rejoin_party is also Companions-scoped, so its key must
        // surface here too, or a rejoinable companion seated at the table would have nothing to pledge with.
        [Test]
        public void GIVEN_a_companions_council_WHEN_asked_for_executable_kinds_THEN_rejoin_party_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().Contain("rejoin_party");
        }

        // The fourth executable menu motion: dispatch_mission is also Companions-scoped, so its key must surface
        // here too, or a dispatchable companion seated at the table would have no errand to pledge to.
        [Test]
        public void GIVEN_a_companions_council_WHEN_asked_for_executable_kinds_THEN_dispatch_mission_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().Contain("dispatch_mission");
        }

        // M4 (Family had ZERO catalogue kinds): every scope, Family included, must expose at least one executable
        // kind, or a family council could seat and debate yet never act. Quest is universal, so every scope has it.
        [Test]
        public void GIVEN_any_scope_WHEN_asked_for_executable_kinds_THEN_quest_is_always_offered()
        {
            foreach (CouncilScope scope in Enum.GetValues(typeof(CouncilScope)).Cast<CouncilScope>())
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().Contain("quest");
        }

        // The runtime executor keys on the literal string "quest" (CouncilResolutionPolicy.QuestKind). This pins
        // that the catalogue's own key for Quest is exactly that string, so the two authorities can never drift.
        [Test]
        public void GIVEN_the_quest_kind_WHEN_keyed_THEN_it_matches_the_runtime_quest_key()
        {
            ResolutionCatalog.KeyOf(ResolutionKind.Quest).Should().Be(CouncilResolutionPolicy.QuestKind);
        }
    }
}
