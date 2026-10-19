// Code written by Gabriel Mailhot, 03/10/2026.
// Who travels with the player, in one place, for the two chat buttons Gabriel designed after nerethi's request
// (03/10/2026): "Invite someone" offers a companion, a prisoner or a lord travelling with the player; "Request
// Private" is granted at once when every witness is one of them. Until now each group lived in its own behaviour
// (the party roster, the prison roster, FollowMeBehavior's escorts, RetainerBehavior's riders) and nothing put them
// together.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

#endregion

namespace CalradiaRemembers.Witnesses
{
   /// <summary>The player's company: who may be invited into a conversation, and whose presence the player commands.</summary>
   internal static class PlayerCompany
   {
      /// <summary>Everyone travelling with the player who could stand in a conversation, with how they travel.</summary>
      internal static List<(Hero Hero, CompanyRole Role)> Members()
      {
         var members = new List<(Hero, CompanyRole)>();
         try
         {
            var retainers = new HashSet<string>(Retainer.RetainerBehavior.Instance?.RetainerIds ?? Enumerable.Empty<string>());

            foreach (TroopRosterElement e in MobileParty.MainParty?.MemberRoster?.GetTroopRoster() ?? Enumerable.Empty<TroopRosterElement>())
            {
               Hero h = e.Character?.HeroObject;
               if (h == null || h == Hero.MainHero || !Mapping.ActionGate.Allows(h, ActionKind.Witness)) continue;

               bool lordOfAnotherHouse = retainers.Contains(h.StringId) || (h.IsLord && h.Clan != null && h.Clan != Clan.PlayerClan);
               members.Add((h, lordOfAnotherHouse ? CompanyRole.TravellingLord : CompanyRole.Companion));
            }

            foreach (string id in Escort.FollowMeBehavior.Instance?.EscortingLordIds ?? Enumerable.Empty<string>())
            {
               Hero h = Hero.FindFirst(x => x.StringId == id);
               if (h != null && Mapping.ActionGate.Allows(h, ActionKind.Witness)) members.Add((h, CompanyRole.TravellingLord));
            }

            foreach (TroopRosterElement e in MobileParty.MainParty?.PrisonRoster?.GetTroopRoster() ?? Enumerable.Empty<TroopRosterElement>())
            {
               Hero h = e.Character?.HeroObject;
               if (h != null && h != Hero.MainHero && Mapping.ActionGate.Allows(h, ActionKind.Witness)) members.Add((h, CompanyRole.Prisoner));
            }
         }
         catch (Exception ex) { ModLog.Error($"[WITNESSES] the player's company could not be read: {ex.GetType().Name}: {ex.Message}"); }

         return members;
      }

      /// <summary>The hero ids of the player's company.</summary>
      internal static HashSet<string> Ids() => new HashSet<string>(Members().Select(m => m.Hero.StringId));

      /// <summary>As the policy sees them.</summary>
      internal static List<CompanyMember> AsCandidates()
         => Members().Select(m => new CompanyMember {Id = m.Hero.StringId, Name = m.Hero.Name?.ToString() ?? "", Role = m.Role}).ToList();

      /// <summary>The witness entry for an invited member, framed by how they travel with the player.</summary>
      internal static WitnessEntry EntryFor(Hero h, CompanyRole role)
      {
         if (h == null) return null;
         if (role == CompanyRole.Companion) return WitnessResolver.EntryForCompanion(h);

         return new WitnessEntry {
            Name = h.Name?.ToString(),
            RelationToNpc = role == CompanyRole.Prisoner ? "the player's prisoner, brought in by the player" : "a lord travelling with the player",
            IsBroughtCaptive = role == CompanyRole.Prisoner,
            HeroStringId = h.StringId,
            IsFemale = h.IsFemale,
            Gear = WitnessResolver.ComposeGear(h)
         };
      }
   }
}
