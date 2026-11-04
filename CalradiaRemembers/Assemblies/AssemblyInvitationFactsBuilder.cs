// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1: the seam between the live campaign and the pure AssemblyRsvpPolicy. It reads one invited
// hero's situation, relative to the host settlement and the summoner, into the engine-free
// AssemblyInvitationFacts the policy judges. Every read is null-guarded and never throws: a summons must never
// crash on an odd hero or a half-loaded world. The DECISION lives in Logic (tested); this only gathers facts.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Assemblies;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Reads the campaign into the pure <see cref="AssemblyInvitationFacts" /> the RSVP policy needs.</summary>
    public static class AssemblyInvitationFactsBuilder
    {
        public static AssemblyInvitationFacts Build(Hero invitee, Settlement host, Hero initiator)
        {
            if (invitee == null || host == null || initiator == null)
                return new AssemblyInvitationFacts {IsAlive = false};

            return new AssemblyInvitationFacts {
                IsAlive = invitee.IsAlive,
                IsPrisoner = invitee.IsPrisoner,
                IsOccupied = IsOccupied(invitee),
                HostOwnedByInviteesEnemy = HostIsEnemyGround(invitee, host),
                RegardToInitiator = RegardToInitiator(invitee, initiator),
                InitiatorAtWarWithInvitee = AtWar(initiator, invitee),
                DistanceTier = AssemblyDistanceTiers.Of(DistanceToHost(invitee, host)),
                IsCompanionOfInitiator = IsCompanionOf(invitee, initiator)
            };
        }

        /// <summary>Pinned in a field battle, or besieged inside a settlement: cannot leave to travel.</summary>
        private static bool IsOccupied(Hero h)
        {
            if (h.PartyBelongedTo?.MapEvent != null) return true;
            Settlement s = h.CurrentSettlement;

            return s != null && s.IsUnderSiege;
        }

        private static bool HostIsEnemyGround(Hero invitee, Settlement host)
        {
            IFaction inviteeFaction = invitee.MapFaction;
            IFaction hostFaction = host.OwnerClan?.MapFaction;

            return inviteeFaction != null && hostFaction != null && inviteeFaction.IsAtWarWith(hostFaction);
        }

        private static bool AtWar(Hero a, Hero b)
        {
            IFaction fa = a.MapFaction;
            IFaction fb = b.MapFaction;

            return fa != null && fb != null && fa.IsAtWarWith(fb);
        }

        /// <summary>
        ///   When the player summons, this is the mod's OWN personal regard (what the player built in chat),
        ///   never the vanilla clan relation. A lord summoner (the reverse case) uses vanilla relation.
        /// </summary>
        private static int RegardToInitiator(Hero invitee, Hero initiator)
        {
            if (initiator == Hero.MainHero)
            {
                NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(invitee.StringId);

                return profile?.ReputationWithPlayer ?? 0;
            }

            return Mapping.PairBondReader.Points(invitee, initiator);
        }

        private static float DistanceToHost(Hero invitee, Settlement host)
        {
            // A party's live position first, else the settlement they sit in, else the host itself (tier 0):
            // a hero with no locatable position is not punished on distance, so the RSVP leans on regard instead.
            Vec2 from = invitee.PartyBelongedTo?.GetPosition2D
                        ?? invitee.CurrentSettlement?.GetPosition2D
                        ?? host.GetPosition2D;

            return from.Distance(host.GetPosition2D);
        }

        /// <summary>The invitee rides in the initiator's own party (a companion): present without a journey.</summary>
        private static bool IsCompanionOf(Hero invitee, Hero initiator)
        {
            MobileParty initiatorParty = initiator.PartyBelongedTo;

            return initiatorParty != null && invitee != initiator && invitee.PartyBelongedTo == initiatorParty;
        }
    }
}
