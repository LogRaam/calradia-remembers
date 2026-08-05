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

        // The companions' council raises its own set and NOTHING from another scope: the WarCouncil's GrantFief
        // must not appear here. If it did, the player could move a fief grant on a council of non-noble hirelings.
        [Test]
        public void GIVEN_a_companions_council_WHEN_the_catalogue_is_queried_THEN_only_companions_motions_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.Companions).Select(d => d.Kind).ToList();

            kinds.Should().Contain(new[]
            {
                ResolutionKind.AssignPartyRole, ResolutionKind.AppointGovernor,
                ResolutionKind.DispatchOnMission, ResolutionKind.GrantStipend, ResolutionKind.RejoinParty,
                ResolutionKind.ExpelFromClan
            });
            kinds.Should().NotContain(ResolutionKind.GrantFief);
        }

        // GrantFief was RE-SCOPED off ClanLords to WarCouncil (Partie 2, COUNCIL_ACTIONS.md's Kimi review: a
        // fief belongs to the CLAN, so "granting" one within the player's own clan, ClanLords' own scope, is
        // mechanically vacant). PledgeAgainst (Partie 1's schemes kind) then joined GiveGold as the clan-lords'
        // SECOND motion, LendTroops (Partie 1's highest-detail item, COUNCIL_ACTIONS.md) joined as its THIRD,
        // GiveTroops (Partie 1's give_troops, the reciprocal of lend_troops) joined next as its FOURTH, and
        // SwearOath (R7-light, COUNCIL_ACTIONS.md's Partie 8) joined as its FIFTH, and SponsorWard (Kimi's
        // design, MINIMAL v1, COUNCIL_ACTIONS.md's Partie 8, "sponsor_ward") joined last as its SIXTH, so the
        // scope must return exactly those six, in declaration order.
        [Test]
        public void GIVEN_a_clan_lords_council_WHEN_the_catalogue_is_queried_THEN_give_gold_and_pledge_against_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.ClanLords).Select(d => d.Kind).ToList();

            kinds.Should().Equal(ResolutionKind.GiveGold, ResolutionKind.PledgeAgainst, ResolutionKind.LendTroops,
                ResolutionKind.GiveTroops, ResolutionKind.SwearOath, ResolutionKind.SponsorWard);
        }

        // The family council carried NO motion at all until PledgeAgainst (Partie 1's schemes kind) became its
        // first: a seated lord's own standing rivalry is exactly as plausible within the player's inner circle
        // as among the wider clan-lords' table. SwearOath (R7-light) joined as its SECOND motion (a personal
        // vow of coin or protection is exactly as credible within the family as at the wider clan-lords' table),
        // and SponsorWard (Kimi's design, MINIMAL v1, "sponsor_ward") joined as its THIRD. The query must now
        // return exactly those three motions, in declaration order.
        [Test]
        public void GIVEN_the_family_council_WHEN_the_catalogue_is_queried_THEN_only_pledge_against_returns()
        {
            ResolutionCatalog.ForScope(CouncilScope.Family).Select(d => d.Kind)
                .Should().Equal(ResolutionKind.PledgeAgainst, ResolutionKind.SwearOath, ResolutionKind.SponsorWard);
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
        // AssignPartyRole (CouncilLift.SettleAssignPartyRole), closing bug d, RejoinParty (CouncilLift.SettleRejoinParty),
        // the fourth executor DispatchOnMission (CouncilLift.SettleDispatchMission), and the FIFTH executor, the
        // first that touches persisted save state, GrantStipend (CouncilLift.SettleGrantStipend, CouncilStipendBehavior)
        // qualify today; the remaining scoped menu motions are recorded but not yet carried out, so teaching them
        // would promise a deed the lift silently drops (the MarkFailed "this kind of decision is not yet settled"
        // path the audit flagged).
        [Test]
        public void GIVEN_the_kinds_WHEN_asked_which_execute_THEN_only_quest_appoint_governor_assign_party_role_rejoin_party_dispatch_mission_and_grant_stipend_do_today()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.Quest).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.AppointGovernor).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.AssignPartyRole).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.RejoinParty).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.DispatchOnMission).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.GrantStipend).Should().BeTrue();
        }

        // Partie 2 (COUNCIL_ACTIONS.md): grant_fief and revoke_fief gained a real executor
        // (CouncilLift.SettleGrantFief/SettleRevokeFief), the first two GOVERNANCE kinds to do so; a table that
        // could still only record them, never carry them out, would keep promising a deed the lift silently drops.
        [Test]
        public void GIVEN_grant_fief_and_revoke_fief_WHEN_asked_if_they_execute_THEN_both_are_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.GrantFief).Should().BeTrue();
            ResolutionCatalog.IsExecutable(ResolutionKind.RevokeFief).Should().BeTrue();
        }

        // Neither kind is a decision about two FACTIONS (unlike declare_war/make_peace): both bind to a seated
        // WarCouncil vassal's own fate (the beneficiary for a grant, the spoliated lord for a revoke), so both
        // must keep the ordinary seated-member re-validation at the lift, never bypass it.
        [Test]
        public void GIVEN_grant_fief_and_revoke_fief_WHEN_asked_if_they_are_faction_centric_THEN_neither_is()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.GrantFief).Should().BeFalse();
            ResolutionCatalog.IsFactionCentric(ResolutionKind.RevokeFief).Should().BeFalse();
        }

        // grant_fief and revoke_fief are WarCouncil-only (Partie 2's own scoping call): the seated vassal is the
        // OTHER clan's own leader, a genuine crown/vassal pair the catalogue's earlier ClanLords scaffold never
        // was. Both must surface alongside declare_war/give_gold/give_influence, in declaration order.
        // ArrangeMarriage (Partie 1, "juste arrange_mariage qui est valide") joined the same WarCouncil scope
        // next, SwapFiefs (Partie 8, COUNCIL_ACTIONS.md's Kimi review, REUSING grant_fief's/revoke_fief's
        // own machinery) joined after it, LendTroops (Partie 1's highest-detail item, COUNCIL_ACTIONS.md)
        // joined next, GiveTroops (Partie 1's give_troops, the reciprocal of lend_troops) joined after that,
        // and SwearOath (R7-light, COUNCIL_ACTIONS.md's Partie 8) joined last, so the full declaration-order
        // list now ends with it too.
        [Test]
        public void GIVEN_a_war_council_WHEN_the_catalogue_is_queried_THEN_grant_fief_and_revoke_fief_also_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.WarCouncil).Select(d => d.Kind).ToList();

            kinds.Should().Equal(ResolutionKind.DeclareWar, ResolutionKind.GiveGold, ResolutionKind.GiveInfluence,
                ResolutionKind.GrantFief, ResolutionKind.RevokeFief, ResolutionKind.ArrangeMarriage, ResolutionKind.SwapFiefs,
                ResolutionKind.LendTroops, ResolutionKind.GiveTroops, ResolutionKind.SwearOath);
        }

        // The empty-promise guard: ClanLords, Companions and Family carry NO GrantFief/RevokeFief catalogue
        // entry at all (a fief is clan-owned, so a transfer within the player's own clan or a hired hand's
        // council is mechanically vacant), so their executable set must never surface either key.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_asked_for_executable_kinds_THEN_grant_fief_and_revoke_fief_are_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.ClanLords, CouncilScope.Companions, CouncilScope.Family})
            {
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("grant_fief");
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("revoke_fief");
            }
        }

        // Partie 8 (COUNCIL_ACTIONS.md's Kimi review): expel_from_clan gained a real executor
        // (CouncilLift.SettleExpelFromClan) the day it landed; a table that could still only record it, never
        // carry it out, would keep promising a deed the lift silently drops.
        [Test]
        public void GIVEN_expel_from_clan_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.ExpelFromClan).Should().BeTrue();
        }

        // expel_from_clan is a seated companion's own fate (the target cast out), not a decision about two
        // FACTIONS, so it must never bypass the ordinary seated-member re-validation the way declare_war/
        // make_peace do (covered generically by GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are
        // below; pinned here too for the kind's own documentation).
        [Test]
        public void GIVEN_expel_from_clan_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.ExpelFromClan).Should().BeFalse();
        }

        // Companions-scope-only (a deliberate deviation from this build's own original brief, Family/ClanLords:
        // see ExpulsionPolicy's own header, neither of those rosters ever seats a live companion). Its key must
        // surface alongside the other companion-fate kinds, and it must never leak to a scope that carries no
        // ExpelFromClan catalogue entry at all.
        [Test]
        public void GIVEN_a_companions_council_WHEN_asked_for_executable_kinds_THEN_expel_from_clan_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().Contain("expel_from_clan");
        }

        [Test]
        public void GIVEN_a_family_clan_lords_war_council_or_parley_WHEN_asked_for_executable_kinds_THEN_expel_from_clan_is_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil, CouncilScope.Parley})
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("expel_from_clan");
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

        // The fifth executable menu motion, and the first that touches persisted save state: grant_stipend is
        // also Companions-scoped, so its key must surface here too, or a seated companion could never actually be
        // granted a stipend even once CouncilLift.SettleGrantStipend exists to carry it out.
        [Test]
        public void GIVEN_a_companions_council_WHEN_asked_for_executable_kinds_THEN_grant_stipend_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().Contain("grant_stipend");
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

        // The WarCouncil's first motion: it must appear under its own scope, and only there. Getting this wrong
        // would put a war declaration on an internal council that has no business raising it, or hide it from
        // the one council built for it. GiveGold (Partie 1) added a SECOND WarCouncil motion alongside
        // DeclareWar, GiveInfluence (also Partie 1, WarCouncil ONLY per COUNCIL_ACTIONS.md's Kimi review) a
        // THIRD, and GrantFief/RevokeFief (Partie 2, re-scoped off an earlier ClanLords scaffold, see
        // GIVEN_a_war_council_WHEN_the_catalogue_is_queried_THEN_grant_fief_and_revoke_fief_also_return below)
        // a FOURTH and FIFTH: a war council's own allied lords plausibly have coin, clan influence, and fiefs of
        // their own to pledge or lose.
        [Test]
        public void GIVEN_a_war_council_WHEN_the_catalogue_is_queried_THEN_declare_war_give_gold_and_give_influence_are_present()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.WarCouncil).Select(d => d.Kind).ToList();

            kinds.Should().Contain(new[] {ResolutionKind.DeclareWar, ResolutionKind.GiveGold, ResolutionKind.GiveInfluence});
        }

        // declare_war has a live lift executor (CouncilLift.SettleDeclareWar) from the day it lands, unlike the
        // still-dormant GrantFief: the table must be able to actually carry the decision out, not just record it.
        [Test]
        public void GIVEN_declare_war_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.DeclareWar).Should().BeTrue();
        }

        // The mod's lift skips the ordinary seated-member re-validation ONLY for a faction-centric kind
        // (CouncilLift.Settle's dispatch order), so the predicate must single out declare_war and nothing else,
        // or a member-pledge kind would wrongly bypass the re-check that catches a proposer who has since died
        // or been taken prisoner.
        [Test]
        public void GIVEN_declare_war_WHEN_asked_if_it_is_faction_centric_THEN_it_is()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.DeclareWar).Should().BeTrue();
        }

        [Test]
        public void GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are()
        {
            foreach (ResolutionKind kind in Enum.GetValues(typeof(ResolutionKind)).Cast<ResolutionKind>())
            {
                if (kind == ResolutionKind.DeclareWar || kind == ResolutionKind.MakePeace) continue;

                ResolutionCatalog.IsFactionCentric(kind).Should().BeFalse();
            }
        }

        // Every scope, WarCouncil included, must expose at least one executable kind so a war council can act,
        // not merely debate; declare_war rides alongside the universal quest key here.
        [Test]
        public void GIVEN_a_war_council_WHEN_asked_for_executable_kinds_THEN_declare_war_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("declare_war");
        }

        // declare_war's symmetric counterpart: it must appear under its own scope, Parley, and only there, so a
        // parley (which exists specifically to negotiate peace) is the one council where make_peace can be
        // raised. Tribute (Partie 8) joined as Parley's second motion, ReleasePrisoner (rounding out the
        // Parley toolkit, 2026-08-04) its third, GiveHostage (Kimi's "v1 invite d'honneur", 2026-08-04) its
        // fourth, and NonAggressionPact (COUNCIL_ACTIONS.md's Partie 8, 2026-08-05) its fifth, so the query must
        // now return all five, in declaration order.
        [Test]
        public void GIVEN_a_parley_WHEN_the_catalogue_is_queried_THEN_make_peace_tribute_release_prisoner_give_hostage_and_non_aggression_pact_return()
        {
            var kinds = ResolutionCatalog.ForScope(CouncilScope.Parley).Select(d => d.Kind).ToList();

            kinds.Should().Equal(ResolutionKind.MakePeace, ResolutionKind.Tribute, ResolutionKind.ReleasePrisoner,
                ResolutionKind.GiveHostage, ResolutionKind.NonAggressionPact);
        }

        // make_peace has a live lift executor (CouncilLift.SettleMakePeace) from the day it lands, mirroring
        // declare_war: the table must be able to actually carry the decision out, not just record it.
        [Test]
        public void GIVEN_make_peace_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.MakePeace).Should().BeTrue();
        }

        // make_peace's subject is two FACTIONS exactly like declare_war, so the mod's lift must dispatch it the
        // same way (bypassing the seated-member re-validation and going straight to its own faction re-check).
        [Test]
        public void GIVEN_make_peace_WHEN_asked_if_it_is_faction_centric_THEN_it_is()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.MakePeace).Should().BeTrue();
        }

        // A parley must be able to act, not merely debate: make_peace rides alongside the universal quest key.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_make_peace_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().Contain("make_peace");
        }

        // Partie 1 (bringing the existing 1:1 resource verbs to the council): give_gold has a live lift executor
        // (CouncilLift.SettleGiveGold) from the day it lands, exactly like every other member-pledge motion
        // above; the table must be able to actually hand the gold over, not just record the pledge.
        [Test]
        public void GIVEN_give_gold_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.GiveGold).Should().BeTrue();
        }

        // give_gold is a seated member's own promise (their purse, their pledge), not a decision about two
        // factions, so it must never bypass the ordinary seated-member re-validation the way declare_war/
        // make_peace do.
        [Test]
        public void GIVEN_give_gold_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.GiveGold).Should().BeFalse();
        }

        // Both scopes give_gold was granted must actually surface its key alongside the universal quest, or a
        // council seated with a moneyed lord would have no way to reach CouncilLift.SettleGiveGold at all.
        [Test]
        public void GIVEN_a_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_give_gold_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("give_gold");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("give_gold");
        }

        // The empty-promise guard, the other direction: Companions and Family carry NO GiveGold catalogue entry
        // at all (a hired hand or dynastic kin is not a moneyed lord pledging his own purse), so their executable
        // set must never surface it, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_or_family_council_WHEN_asked_for_executable_kinds_THEN_give_gold_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("give_gold");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("give_gold");
        }

        // Partie 1 (bringing the existing 1:1 resource verbs to the council): give_influence has a live lift
        // executor (CouncilLift.SettleGiveInfluence) from the day it lands, reusing the same
        // CourtActionPolicy.InfluenceGrant/CanAffordInfluenceGrant math the 1:1 give_influence verb already
        // uses; the table must be able to actually move the influence, not just record the pledge.
        [Test]
        public void GIVEN_give_influence_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.GiveInfluence).Should().BeTrue();
        }

        // give_influence is a seated ally's own clan promise (their house's own standing), not a decision about
        // two factions, so it must never bypass the ordinary seated-member re-validation the way declare_war/
        // make_peace do.
        [Test]
        public void GIVEN_give_influence_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.GiveInfluence).Should().BeFalse();
        }

        // WarCouncil is the ONLY scope give_influence was granted (COUNCIL_ACTIONS.md's Kimi review: influence
        // is a CLAN-level resource, and the 1:1 verb already excludes giver == Clan.PlayerClan, so ClanLords,
        // where every seated lord IS the player's own clan, would be mechanically vacant): its key must surface
        // alongside the universal quest, or a war council seated with an influential ally would have no way to
        // reach CouncilLift.SettleGiveInfluence at all.
        [Test]
        public void GIVEN_a_war_council_WHEN_asked_for_executable_kinds_THEN_give_influence_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("give_influence");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry NO GiveInfluence
        // catalogue entry at all (within the player's own clan the transfer would be clan-to-itself, a hired
        // hand or dynastic kin holds no clan influence to pledge either), so their executable set must never
        // surface it, whatever the world facts.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_asked_for_executable_kinds_THEN_give_influence_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().NotContain("give_influence");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("give_influence");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("give_influence");
        }

        // Partie 1 (COUNCIL_ACTIONS.md's schemes block): pledge_against has a live lift executor
        // (CouncilLift.SettlePledgeAgainst) from the day it lands, REUSING the existing 1:1 pledge_against
        // system end to end; the table must be able to actually launch the scheme, not just record the pledge.
        [Test]
        public void GIVEN_pledge_against_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.PledgeAgainst).Should().BeTrue();
        }

        // pledge_against is a seated member's own promise (their own scheme against a rival), not a decision
        // about two FACTIONS, so it must never bypass the ordinary seated-member re-validation the way
        // declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_pledge_against_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.PledgeAgainst).Should().BeFalse();
        }

        // Both scopes pledge_against was granted must actually surface its key alongside the universal quest, or
        // a council seated with a genuinely aggrieved lord would have no way to reach
        // CouncilLift.SettlePledgeAgainst at all.
        [Test]
        public void GIVEN_a_clan_lords_or_family_council_WHEN_asked_for_executable_kinds_THEN_pledge_against_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("pledge_against");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().Contain("pledge_against");
        }

        // The empty-promise guard, the other direction: Companions, WarCouncil and Parley carry NO PledgeAgainst
        // catalogue entry at all (a hired hand's own rivalry has no lord's standing behind it, and WarCouncil
        // already carries its own faction-scale motions, declare_war/make_peace), so their executable set must
        // never surface it, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_war_council_or_parley_council_WHEN_asked_for_executable_kinds_THEN_pledge_against_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("pledge_against");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().NotContain("pledge_against");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().NotContain("pledge_against");
        }

        // Partie 1 (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est valide"): arrange_marriage has a live lift
        // executor (CouncilLift.SettleArrangeMarriage) from the day it lands, REUSING the existing 1:1
        // arrange_marriage system end to end; the table must be able to actually seal the match, not just
        // record the pledge.
        [Test]
        public void GIVEN_arrange_marriage_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.ArrangeMarriage).Should().BeTrue();
        }

        // arrange_marriage binds two named KIN, not two FACTIONS, so it must never bypass the ordinary
        // seated-member re-validation the way declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_arrange_marriage_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.ArrangeMarriage).Should().BeFalse();
        }

        // WarCouncil is the ONLY scope arrange_marriage was granted (COUNCIL_ACTIONS.md's Kimi review: a
        // marriage alliance is a genuine cross-clan currency, exactly the pool WarCouncil already seats): its key
        // must surface alongside the universal quest, or a war council seated with a marriageable ally would
        // have no way to reach CouncilLift.SettleArrangeMarriage at all.
        [Test]
        public void GIVEN_a_war_council_WHEN_asked_for_executable_kinds_THEN_arrange_marriage_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("arrange_marriage");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry NO ArrangeMarriage
        // catalogue entry at all (within the player's own clan, or a hired hand's/dynastic kin's council, there
        // is no genuine cross-clan house to marry into), so their executable set must never surface it, whatever
        // the world facts.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_asked_for_executable_kinds_THEN_arrange_marriage_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().NotContain("arrange_marriage");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("arrange_marriage");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("arrange_marriage");
        }

        // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "swap_fiefs"): swap_fiefs has a live lift executor
        // (CouncilLift.SettleSwapFiefs) from the day it lands, REUSING grant_fief's/revoke_fief's own machinery
        // end to end; the table must be able to actually carry out the exchange, not just record the pledge.
        [Test]
        public void GIVEN_swap_fiefs_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.SwapFiefs).Should().BeTrue();
        }

        // swap_fiefs binds the player's own clan and a seated vassal's own clan to an exchange, not a decision
        // about two FACTIONS, so it must never bypass the ordinary seated-member re-validation the way
        // declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_swap_fiefs_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.SwapFiefs).Should().BeFalse();
        }

        // WarCouncil is the ONLY scope swap_fiefs was granted (mirroring GrantFief/RevokeFief/ArrangeMarriage's
        // own scoping): its key must surface alongside the universal quest, or a war council seated with a
        // fief-holding vassal would have no way to reach CouncilLift.SettleSwapFiefs at all.
        [Test]
        public void GIVEN_a_war_council_WHEN_asked_for_executable_kinds_THEN_swap_fiefs_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("swap_fiefs");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry NO SwapFiefs
        // catalogue entry at all (a fief is clan-owned, so an exchange within the player's own clan or a hired
        // hand's council is mechanically vacant, the same reasoning GrantFief/RevokeFief already document), so
        // their executable set must never surface it, whatever the world facts.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_asked_for_executable_kinds_THEN_swap_fiefs_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().NotContain("swap_fiefs");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("swap_fiefs");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("swap_fiefs");
        }

        // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): tribute has a live lift executor
        // (CouncilLift.SettleTribute) from the day it lands; the table must be able to actually bind the enemy
        // leader to the daily payment, not just record the pledge.
        [Test]
        public void GIVEN_tribute_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.Tribute).Should().BeTrue();
        }

        // tribute is the seated ENEMY leader's own promise to pay from their own coffers, not a decision about
        // two FACTIONS the way make_peace is (even though both are struck at the same Parley table), so it must
        // never bypass the ordinary seated-member re-validation the way declare_war/make_peace do (covered
        // generically by GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are
        // above; pinned here too for the kind's own documentation).
        [Test]
        public void GIVEN_tribute_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.Tribute).Should().BeFalse();
        }

        // A parley must be able to bind the enemy to a tribute, not merely make peace or debate: tribute rides
        // alongside the universal quest key and make_peace.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_tribute_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().Contain("tribute");
        }

        // The empty-promise guard, the other direction: no OTHER scope carries a Tribute catalogue entry at all
        // (only a Parley ever seats an enemy faction leader to pay tribute from), so the key must never surface
        // elsewhere, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_family_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_tribute_is_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.Companions, CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil})
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("tribute");
        }

        // Rounds out the Parley toolkit (make_peace + tribute + release_prisoner, 2026-08-04): release_prisoner
        // has a live lift executor (CouncilLift.SettleReleasePrisoner) from the day it lands, REUSING the
        // existing 1:1 free_prisoner mechanic end to end; the table must be able to actually free the captive,
        // not just record the pledge.
        [Test]
        public void GIVEN_release_prisoner_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.ReleasePrisoner).Should().BeTrue();
        }

        // release_prisoner is the seated enemy leader's own ASK of the player (their house benefits from the
        // freed captive), not a decision about two FACTIONS the way make_peace is (even though both are struck
        // at the same Parley table), so it must never bypass the ordinary seated-member re-validation the way
        // declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_release_prisoner_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.ReleasePrisoner).Should().BeFalse();
        }

        // A parley must be able to free a captive as a concession, not merely make peace, bind a tribute, or
        // debate: release_prisoner rides alongside the universal quest key, make_peace, and tribute.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_release_prisoner_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().Contain("release_prisoner");
        }

        // The empty-promise guard, the other direction: no OTHER scope carries a ReleasePrisoner catalogue entry
        // at all (only a Parley ever seats an enemy faction leader to ask this concession of), so the key must
        // never surface elsewhere, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_family_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_release_prisoner_is_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.Companions, CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil})
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("release_prisoner");
        }

        // Kimi's "v1 invite d'honneur" (COUNCIL_ACTIONS.md's Partie 8, "give_hostage", 2026-08-04): give_hostage
        // has a live lift executor (CouncilLift.SettleGiveHostage) from the day it lands; the table must be able
        // to actually move the relative into the player's court, not just record the pledge.
        [Test]
        public void GIVEN_give_hostage_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.GiveHostage).Should().BeTrue();
        }

        // give_hostage is the seated enemy leader's own kinsman given up, not a decision about two FACTIONS the
        // way make_peace is (even though both are struck at the same Parley table), so it must never bypass the
        // ordinary seated-member re-validation the way declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_give_hostage_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.GiveHostage).Should().BeFalse();
        }

        // A parley must be able to bind the enemy leader to a hostage of good faith, not merely make peace, bind
        // a tribute, or free a captive: give_hostage rides alongside the universal quest key and its three Parley
        // siblings.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_give_hostage_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().Contain("give_hostage");
        }

        // The empty-promise guard, the other direction: no OTHER scope carries a GiveHostage catalogue entry at
        // all (only a Parley ever seats an enemy faction leader to give a kinsman as a hostage of good faith), so
        // the key must never surface elsewhere, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_family_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_give_hostage_is_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.Companions, CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil})
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("give_hostage");
        }

        // ── ResolutionEligibilityPolicy: the give_hostage offering fact actually gates the teaching ──

        // STAKE: mirrors the swear_oath fact-gating tests below (the same "prove SOME concession is possible,
        // never over-offer" discipline). If the fact were ignored, the table would teach give_hostage even when
        // the seated enemy leader has no eligible relative to give at all.
        [Test]
        public void GIVEN_the_give_hostage_fact_is_false_WHEN_asking_what_is_offered_THEN_it_is_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedHostageEligibleRelativeAvailable: false);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts)
                .Should().NotContain("give_hostage");
        }

        [Test]
        public void GIVEN_the_give_hostage_fact_is_true_WHEN_asking_what_is_offered_THEN_it_is_included()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedHostageEligibleRelativeAvailable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts)
                .Should().Contain("give_hostage");
        }

        // STAKE: even with the fact true, a scope the catalogue never seats a give_hostage entry for (ClanLords)
        // must never offer it, since the by-scope lookup reads the catalogue FIRST, before facts are applied.
        [Test]
        public void GIVEN_the_give_hostage_fact_is_true_but_the_scope_is_clan_lords_WHEN_asking_what_is_offered_THEN_it_is_still_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedHostageEligibleRelativeAvailable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts)
                .Should().NotContain("give_hostage");
        }

        // Partie 1 (COUNCIL_ACTIONS.md's lend_troops, the highest-detail item in the spec): lend_troops has a live
        // lift executor (CouncilLift.SettleLendTroops) from the day it lands; the table must be able to actually
        // set the delegation in motion, not just record the pledge.
        [Test]
        public void GIVEN_lend_troops_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.LendTroops).Should().BeTrue();
        }

        // lend_troops is a seated lord's own promise (his own party, his own troops), not a decision about two
        // FACTIONS, so it must never bypass the ordinary seated-member re-validation the way declare_war/
        // make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_lend_troops_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.LendTroops).Should().BeFalse();
        }

        // Both scopes lend_troops was granted (mirroring give_gold's own ClanLords/WarCouncil scoping) must
        // actually surface its key alongside the universal quest, or a council seated with a lord fielding a real
        // party would have no way to reach CouncilLift.SettleLendTroops at all.
        [Test]
        public void GIVEN_a_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_lend_troops_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("lend_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("lend_troops");
        }

        // The empty-promise guard, the other direction: Companions, Family and Parley carry NO LendTroops
        // catalogue entry at all (a hired hand, dynastic kin, or negotiating enemy leader is not a lord fielding a
        // real party of his own to lend from), so their executable set must never surface it, whatever the world
        // facts.
        [Test]
        public void GIVEN_a_companions_family_or_parley_council_WHEN_asked_for_executable_kinds_THEN_lend_troops_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("lend_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("lend_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().NotContain("lend_troops");
        }

        // Partie 1 (COUNCIL_ACTIONS.md's give_troops, the reciprocal of lend_troops): give_troops has a live
        // lift executor (CouncilLift.SettleGiveTroops) from the day it lands; the table must be able to
        // actually move the player's own troops, not just record the pledge.
        [Test]
        public void GIVEN_give_troops_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.GiveTroops).Should().BeTrue();
        }

        // give_troops is a seated lord's own reinforcement (the player's own party, moving to the lord's own
        // party), not a decision about two FACTIONS, so it must never bypass the ordinary seated-member
        // re-validation the way declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_give_troops_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.GiveTroops).Should().BeFalse();
        }

        // Both scopes give_troops was granted (mirroring lend_troops' own ClanLords/WarCouncil scoping) must
        // actually surface its key alongside the universal quest, or a council seated with a lord whose party
        // has room would have no way to reach CouncilLift.SettleGiveTroops at all.
        [Test]
        public void GIVEN_a_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_give_troops_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("give_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("give_troops");
        }

        // The empty-promise guard, the other direction: Companions, Family and Parley carry NO GiveTroops
        // catalogue entry at all (a hired hand, dynastic kin, or negotiating enemy leader is not a lord fielding
        // a real party of his own worth reinforcing), so their executable set must never surface it, whatever
        // the world facts.
        [Test]
        public void GIVEN_a_companions_family_or_parley_council_WHEN_asked_for_executable_kinds_THEN_give_troops_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("give_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().NotContain("give_troops");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().NotContain("give_troops");
        }

        // R7-light (COUNCIL_ACTIONS.md's Partie 8, "swear_oath"): swear_oath has a live lift executor
        // (CouncilLift.SettleSwearOath) from the day it lands; the table must be able to actually track the
        // vow, not just record the pledge.
        [Test]
        public void GIVEN_swear_oath_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.SwearOath).Should().BeTrue();
        }

        // swear_oath is a seated member's own vow (like give_gold or pledge_against), not a decision about two
        // FACTIONS, so it must never bypass the ordinary seated-member re-validation the way declare_war/
        // make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_swear_oath_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.SwearOath).Should().BeFalse();
        }

        // All three scopes swear_oath was granted (ClanLords, Family, WarCouncil, mirroring pledge_against's
        // own two scopes plus WarCouncil's own keep_peace need) must actually surface its key alongside the
        // universal quest, or a council seated with a member who could plausibly swear one of the three
        // whitelisted kinds would have no way to reach CouncilLift.SettleSwearOath at all.
        [Test]
        public void GIVEN_a_clan_lords_family_or_war_council_WHEN_asked_for_executable_kinds_THEN_swear_oath_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("swear_oath");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().Contain("swear_oath");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().Contain("swear_oath");
        }

        // The empty-promise guard, the other direction: Companions and Parley carry NO SwearOath catalogue entry
        // at all (a hired hand has no standing purse, party, or faction of their own to swear against, and a
        // negotiating enemy leader's own concessions are Parley's own separate toolkit), so their executable set
        // must never surface it, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_or_parley_council_WHEN_asked_for_executable_kinds_THEN_swear_oath_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("swear_oath");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().NotContain("swear_oath");
        }

        // ── ResolutionEligibilityPolicy: the swear_oath offering fact actually gates the teaching ──

        // STAKE: this is the "prove SOME oath is possible, never over-offer" discipline every other loose gate
        // in the catalogue already follows (MemberHasGold, AnySeatedMemberCanPledgeAgainst...). If the fact were
        // ignored, the table would teach swear_oath even when nobody seated could plausibly swear anything.
        [Test]
        public void GIVEN_the_swear_oath_fact_is_false_WHEN_asking_what_is_offered_THEN_it_is_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSwearOath: false);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts)
                .Should().NotContain("swear_oath");
        }

        [Test]
        public void GIVEN_the_swear_oath_fact_is_true_WHEN_asking_what_is_offered_THEN_it_is_included()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSwearOath: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts)
                .Should().Contain("swear_oath");
        }

        // STAKE: even with the fact true, a scope the catalogue never seats a swear_oath entry for (Companions)
        // must never offer it, since the by-scope lookup reads the catalogue FIRST, before facts are applied.
        [Test]
        public void GIVEN_the_swear_oath_fact_is_true_but_the_scope_is_companions_WHEN_asking_what_is_offered_THEN_it_is_still_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSwearOath: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts)
                .Should().NotContain("swear_oath");
        }

        // Rounds out the Parley toolkit with a FIFTH concession (COUNCIL_ACTIONS.md's Partie 8,
        // "non_aggression_pact", 2026-08-05): it has a live lift executor (CouncilLift.SettleNonAggressionPact)
        // from the day it lands; the table must be able to actually strike the pact, not just record the pledge.
        [Test]
        public void GIVEN_non_aggression_pact_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.NonAggressionPact).Should().BeTrue();
        }

        // non_aggression_pact is the seated enemy leader's own promise struck at the table, not a decision about
        // two FACTIONS the way make_peace is (even though both are struck at the same Parley table, and this
        // kind's OWN executor still resolves the actual faction pair), so it must never bypass the ordinary
        // seated-member re-validation the way declare_war/make_peace do (covered generically by
        // GIVEN_every_member_pledge_kind_WHEN_asked_if_it_is_faction_centric_THEN_none_of_them_are above; pinned
        // here too for the kind's own documentation).
        [Test]
        public void GIVEN_non_aggression_pact_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.NonAggressionPact).Should().BeFalse();
        }

        // A parley must be able to strike a non-aggression pact, not merely make peace, bind a tribute, free a
        // captive, or take a hostage: non_aggression_pact rides alongside the universal quest key and its four
        // Parley siblings.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_non_aggression_pact_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().Contain("non_aggression_pact");
        }

        // The empty-promise guard, the other direction: no OTHER scope carries a NonAggressionPact catalogue
        // entry at all (only a Parley ever seats an enemy faction leader to strike this pact with), so the key
        // must never surface elsewhere, whatever the world facts.
        [Test]
        public void GIVEN_a_companions_family_clan_lords_or_war_council_WHEN_asked_for_executable_kinds_THEN_non_aggression_pact_is_absent()
        {
            foreach (CouncilScope scope in new[] {CouncilScope.Companions, CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil})
                ResolutionCatalog.ExecutableKindKeysForScope(scope).Should().NotContain("non_aggression_pact");
        }

        // ── ResolutionEligibilityPolicy: the non_aggression_pact offering fact actually gates the teaching ──

        // STAKE: mirrors the tribute/give_hostage fact-gating tests above (the same "prove SOME concession is
        // possible, never over-offer" discipline). If the fact were ignored, the table would teach
        // non_aggression_pact even when nobody seated leads a house distinct from the player's own, or when the
        // two are already bound by a live pact.
        [Test]
        public void GIVEN_the_non_aggression_pact_fact_is_false_WHEN_asking_what_is_offered_THEN_it_is_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedPactPartnerAvailable: false);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts)
                .Should().NotContain("non_aggression_pact");
        }

        [Test]
        public void GIVEN_the_non_aggression_pact_fact_is_true_WHEN_asking_what_is_offered_THEN_it_is_included()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedPactPartnerAvailable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts)
                .Should().Contain("non_aggression_pact");
        }

        // STAKE: even with the fact true, a scope the catalogue never seats a non_aggression_pact entry for
        // (WarCouncil) must never offer it, since the by-scope lookup reads the catalogue FIRST, before facts
        // are applied.
        [Test]
        public void GIVEN_the_non_aggression_pact_fact_is_true_but_the_scope_is_war_council_WHEN_asking_what_is_offered_THEN_it_is_still_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedPactPartnerAvailable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, facts)
                .Should().NotContain("non_aggression_pact");
        }

        // Kimi's design, MINIMAL v1 (COUNCIL_ACTIONS.md's Partie 8, "sponsor_ward"): sponsor_ward has a live lift
        // executor (CouncilLift.SettleSponsorWard) from the day it lands; the table must be able to actually
        // start the mentorship bond, not just record the pledge.
        [Test]
        public void GIVEN_sponsor_ward_WHEN_asked_if_it_executes_THEN_it_is_executable()
        {
            ResolutionCatalog.IsExecutable(ResolutionKind.SponsorWard).Should().BeTrue();
        }

        // sponsor_ward is a seated member's own kindness taken up, not a decision about two FACTIONS the way
        // make_peace/declare_war are, so it must never bypass the ordinary seated-member re-validation (covered
        // generically above; pinned here too for the kind's own documentation).
        [Test]
        public void GIVEN_sponsor_ward_WHEN_asked_if_it_is_faction_centric_THEN_it_is_not()
        {
            ResolutionCatalog.IsFactionCentric(ResolutionKind.SponsorWard).Should().BeFalse();
        }

        // Mirrors PledgeAgainst's own two-scope entry exactly (ClanLords and Family, never Companions or
        // WarCouncil): a companions' council or a war council must never see sponsor_ward, whatever the world
        // facts, since neither catalogue carries an entry for it at all.
        [Test]
        public void GIVEN_a_clan_lords_or_family_council_WHEN_asked_for_executable_kinds_THEN_sponsor_ward_is_offered()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.ClanLords).Should().Contain("sponsor_ward");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Family).Should().Contain("sponsor_ward");
        }

        [Test]
        public void GIVEN_a_companions_or_war_council_WHEN_asked_for_executable_kinds_THEN_sponsor_ward_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Companions).Should().NotContain("sponsor_ward");
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.WarCouncil).Should().NotContain("sponsor_ward");
        }

        // The empty-promise guard, the other direction: Parley never carries a SponsorWard catalogue entry
        // either (a Parley seats only the enemy leader as a negotiation counterpart, never the player's own
        // house), so the key must never surface there, whatever the world facts.
        [Test]
        public void GIVEN_a_parley_WHEN_asked_for_executable_kinds_THEN_sponsor_ward_is_absent()
        {
            ResolutionCatalog.ExecutableKindKeysForScope(CouncilScope.Parley).Should().NotContain("sponsor_ward");
        }

        // ── ResolutionEligibilityPolicy: the sponsor_ward offering fact actually gates the teaching ──

        // STAKE: mirrors the pledge_against/give_hostage fact-gating tests above. If the fact were ignored, the
        // table would teach sponsor_ward even when no seated member is free to mentor, or the player's own clan
        // has no eligible young companion at all - a hollow promise the lift would refuse outright.
        [Test]
        public void GIVEN_the_sponsor_ward_fact_is_false_WHEN_asking_what_is_offered_THEN_it_is_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSponsorWard: false);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts)
                .Should().NotContain("sponsor_ward");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts)
                .Should().NotContain("sponsor_ward");
        }

        [Test]
        public void GIVEN_the_sponsor_ward_fact_is_true_WHEN_asking_what_is_offered_THEN_it_is_included()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSponsorWard: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts)
                .Should().Contain("sponsor_ward");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts)
                .Should().Contain("sponsor_ward");
        }

        // STAKE: even with the fact true, a scope the catalogue never seats a sponsor_ward entry for (Companions,
        // WarCouncil, Parley) must never offer it, since the by-scope lookup reads the catalogue FIRST, before
        // facts are applied.
        [Test]
        public void GIVEN_the_sponsor_ward_fact_is_true_but_the_scope_is_companions_war_council_or_parley_WHEN_asking_what_is_offered_THEN_it_is_still_withheld()
        {
            var facts = new ResolutionWorldFacts(false, false, false, false, false, anySeatedMemberCanSponsorWard: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("sponsor_ward");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, facts).Should().NotContain("sponsor_ward");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts).Should().NotContain("sponsor_ward");
        }
    }
}
