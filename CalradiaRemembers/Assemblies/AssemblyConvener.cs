// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1: who actually TRAVELS, and who sends a stand-in. Ratified (Gabriel 2026-07-25): only the
// player's OWN house crosses the map in person, a family member, a companion, or a lord of the player's clan,
// and when they travel they go with escort enough not to fear bandits (the escort itself lands with the
// forced-movement slice). Every other accepted invitee, an ally and above all an ENEMY at parley, sends an
// AMBASSADOR rather than being force-marched across hostile ground. This is the deliberate answer to CR's
// courier scar tissue: we only ever force parties WE can support to move, never arbitrary AI lords.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Turns an RSVP verdict into the invitee's opening seat, encoding who travels and who sends a stand-in.</summary>
    public static class AssemblyConvener
    {
        /// <summary>
        ///   Convenes an assembly and seats every invitee in one pass: records the roster, then for each hero
        ///   reads the campaign, runs the RSVP policy, and applies the opening status. Shared by the settlement
        ///   menu and the console command so both convene identically. No-op without an active behavior.
        /// </summary>
        public static void Convene(Settlement host, Hero initiator, IEnumerable<Hero> invitees, int today, int deadline)
        {
            if (host == null || initiator == null || AssemblyBehavior.Instance == null) return;

            List<Hero> list = (invitees ?? Enumerable.Empty<Hero>()).Where(h => h != null).Distinct().ToList();
            AssemblyBehavior.Instance.Begin(host.StringId, initiator.StringId, today, deadline, list.Select(h => h.StringId));

            foreach (Hero h in list)
            {
                AssemblyInvitationFacts facts = AssemblyInvitationFactsBuilder.Build(h, host, initiator);
                RsvpOutcome rsvp = AssemblyRsvpPolicy.Decide(facts);
                AssemblyBehavior.Instance.SetStatus(h.StringId, InitialStatus(h, initiator, facts, rsvp));
            }

            // Send the travellers on their way (only EnRoute clan parties; the console command sets statuses
            // itself, so it kicks travel separately).
            AssemblyBehavior.Instance.KickTravel();
        }

        /// <summary>
        ///   A declined invitee sits out. A companion is already at your side, so Present at once. A member of
        ///   your own clan (family, or a clan lord with a party) travels in person, so EnRoute. Every other
        ///   accepted invitee, an ally or an enemy at parley, sends an Ambassador rather than crossing the map.
        /// </summary>
        /// <summary>The escort a traveller needs so bandits will not dare waylay them (Gabriel 2026-07-25). TUNE.</summary>
        public const int MinEscortTroops = 20;

        public static AssemblyInviteeStatus InitialStatus(Hero invitee, Hero initiator, AssemblyInvitationFacts facts, RsvpOutcome rsvp)
        {
            if (rsvp.Decision == RsvpDecision.Decline) return AssemblyInviteeStatus.Declined;
            if (facts.IsCompanionOfInitiator) return AssemblyInviteeStatus.Present;

            // Only your own house travels, and only with a real warband at their back: a clan lord who leads
            // enough troops rides in person, but a party-less or lightly-escorted member sends an ambassador
            // rather than risking the road (Gabriel: they must not fear bandits).
            return TravelsInPerson(invitee, initiator) && HasSufficientEscort(invitee)
                ? AssemblyInviteeStatus.EnRoute
                : AssemblyInviteeStatus.Ambassador;
        }

        /// <summary>True when the invitee leads their own party and it carries enough troops to travel unafraid.</summary>
        public static bool HasSufficientEscort(Hero invitee)
        {
            MobileParty party = invitee?.PartyBelongedTo;

            return party != null && party.LeaderHero == invitee && party.MemberRoster.TotalManCount >= MinEscortTroops;
        }

        /// <summary>
        ///   True only when the invitee belongs to the summoner's OWN household and so would cross the map in
        ///   person: a companion, a family member, or a lord of the player's clan. Anyone else (an ally, an
        ///   enemy at parley) sends an ambassador instead. The reverse case, a lord summoning the PLAYER, is a
        ///   later slice and is left travelling by default.
        /// </summary>
        public static bool TravelsInPerson(Hero invitee, Hero initiator)
        {
            if (invitee == null) return false;
            if (initiator != Hero.MainHero) return true;

            Clan playerClan = Clan.PlayerClan;

            return playerClan != null && invitee.Clan == playerClan;
        }
    }
}
