// Code written by Gabriel Mailhot, 26/09/2026.
// Brigandage (arrests Inc 5; Gabriel's rulings, 26/09/2026): a player who attacks a merchant (a caravan by land, a
// merchant ship by sea) or a fishing boat while at peace becomes an outlaw. The merchant's sender and receiver both put
// a price on their head; a fishing boat, only its owner. Never a kingdom's ruler, never a realm already at war.
//
// The game's own hostile-act hook is BeHostileAction's private ApplyInternal, which every hostile act against a party
// passes through (an attack, a coercion), for caravans and fishing boats alike, even though the game itself then only
// punishes caravans and only in the sender's realm. The PREFIX reads the facts before the game changes them (whether
// the realm was at peace: the game may declare war inside the call); the POSTFIX places the bounties once the game's own
// consequences have run. Resolved by name with Prepare, so a renamed target skips this patch rather than breaking every
// other one PatchAll applies.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CalradiaRemembers.Logic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Arrests
{
   /// <summary>Makes a brigand of a player who attacks merchants or fishers in peace.</summary>
   [HarmonyPatch]
   public static class BrigandagePatch
   {
      /// <summary>The engine's own hostile-act core, or null when this build does not have it.</summary>
      private static MethodBase TargetMethod() => AccessTools.Method(typeof(BeHostileAction), "ApplyInternal");

      /// <summary>Skips the patch cleanly when the target does not exist in this game build.</summary>
      private static bool Prepare()
      {
         bool found = TargetMethod() != null;
         if (!found) ModLog.Error("[ARREST] BeHostileAction.ApplyInternal not found; brigandage will not be detected on this game version.");

         return found;
      }

      /// <summary>Reads, before the game acts, which realms the attack wrongs and whether each was at peace.</summary>
      private static void Prefix(PartyBase attackerParty, PartyBase defenderParty, out List<string> __state)
      {
         __state = null;
         try
         {
            if (attackerParty != PartyBase.MainParty || defenderParty?.MobileParty == null) return;

            MobileParty target = defenderParty.MobileParty;
            bool isFishing = target.PartyComponent?.GetType().Name == "FishingPartyComponent";
            if (!target.IsCaravan && !isFishing) return;

            Clan playerClan = Clan.PlayerClan;
            bool rulesAKingdom = playerClan?.Kingdom != null && playerClan.Kingdom.RulingClan == playerClan;
            IFaction mine = Hero.MainHero?.MapFaction;

            IReadOnlyList<string> realms = OutlawPolicy.BrigandageRealms(new BrigandageFacts {
               IsFishing = isFishing,
               OwnerRealmId = target.MapFaction?.StringId,
               DestinationRealmId = isFishing ? null : target.TargetSettlement?.MapFaction?.StringId
            });

            __state = realms.Where(id =>
            {
               IFaction realm = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == id);
               bool atWar = realm != null && mine != null && realm.IsAtWarWith(mine);

               return realm != null && OutlawPolicy.IsBrigandage(rulesAKingdom, atWar);
            }).ToList();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] reading a brigandage failed: {ex.GetType().Name}: {ex.Message}");
            __state = null;
         }
      }

      /// <summary>Once the game's own consequences have run, each wronged realm puts a price on the player's head.</summary>
      private static void Postfix(List<string> __state)
      {
         if (__state == null || __state.Count == 0) return;

         try
         {
            foreach (string id in __state)
            {
               IFaction realm = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == id);
               if (realm == null) continue;

               ModLog.Info($"[ARREST] brigandage: {realm.Name} puts a price on the player's head.");
               ArrestBehavior.Instance?.BecomeOutlaw(realm, BountyReason.Brigandage);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] placing a brigandage bounty failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
