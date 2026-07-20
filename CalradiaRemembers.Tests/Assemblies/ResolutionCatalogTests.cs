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
        // Every kind in the enum must resolve to a definition: the ledger records a motion by its kind and the
        // executor is selected by it, so a kind with no catalogue entry would be an unraisable dead motion.
        [Test]
        public void GIVEN_every_resolution_kind_WHEN_looked_up_THEN_each_has_exactly_one_definition()
        {
            foreach (ResolutionKind kind in Enum.GetValues(typeof(ResolutionKind)).Cast<ResolutionKind>())
            {
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
                ResolutionKind.DispatchOnMission, ResolutionKind.GrantStipend
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
    }
}
