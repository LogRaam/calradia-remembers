// Code written by Gabriel Mailhot, 24/10/2026.
// Engine spike, not a feature: can a conversation open while the player is held in a castle or town dungeon? Gabriel
// (24/10/2026) wants a Hardcore tale opened by a scenario when the player is Rhagaea's prisoner where she is, at night:
// a lady fetches the player from the cell and Rhagaea waits in her chamber. Every captive scene the mod has assumes a
// roaming captor party (CaptiveEncounterService returns when the captor has no MobileParty), and nothing says what
// PlayerEncounter, the captivity menu and the conversation scene do for a player in a settlement's dungeon. This probe
// sets the case up (jail), reports the engine's state (info), tries both staging doors (open), and frees the player
// (free). Everything is written to the log under [PROBE]. Remove once the tale's engine is built.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.captive_talk_probe jail|info|open|free [tale|plain] [name]
      ///   Engine spike: a conversation with a lord while the player is held in that lord's castle or town.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("captive_talk_probe", "cr")]
      public static string CaptiveTalkProbe(List<string> args)
      {
         string mode = args?.FirstOrDefault()?.ToLowerInvariant() ?? "info";
         List<string> rest = args?.Skip(1).ToList() ?? new List<string>();
         string door = rest.FirstOrDefault()?.ToLowerInvariant();
         if (door == "tale" || door == "plain") rest = rest.Skip(1).ToList();
         else door = "tale";

         try
         {
            switch (mode)
            {
               case "jail": return Probe(Jail(ResolveHero(rest)));
               case "open": return Probe(Open(ResolveHero(rest), door));
               case "free":
                  if (!Hero.MainHero.IsPrisoner) return Probe("the player is not a prisoner");
                  EndCaptivityAction.ApplyByEscape(Hero.MainHero, null, false);
                  return Probe($"freed; still a prisoner: {Hero.MainHero.IsPrisoner}");
               default: return Probe(State(ResolveHero(rest)));
            }
         }
         catch (Exception ex)
         {
            return Probe($"{mode} threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
         }
      }

      // The case Gabriel describes: the player in a dungeon of the lord's house, the lord in the same place.
      private static string Jail(Hero lord)
      {
         if (lord == null) return "jail: name a lord (cr.captive_talk_probe jail Rhagaea)";
         if (Hero.MainHero.IsPrisoner) return "jail: the player is already a prisoner\n" + State(lord);

         Settlement seat = lord.CurrentSettlement != null && (lord.CurrentSettlement.IsTown || lord.CurrentSettlement.IsCastle)
            ? lord.CurrentSettlement
            : Settlement.All.Where(s => (s.IsTown || s.IsCastle) && s.OwnerClan == lord.Clan)
                        .OrderBy(s => s.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D))
                        .FirstOrDefault();
         if (seat == null) return $"jail: {lord.Name}'s house holds no town or castle";

         // The engine frees a captive player at its next check unless the captor's realm is at war with the player's or holds
         // a moderate or severe crime against them (PlayerCaptivityCampaignBehavior.CheckCaptivityChange, measured 24/10/2026:
         // freed at once). A realm of the player's own frees him whatever happens.
         IFaction realm = seat.MapFaction;
         if (realm == Hero.MainHero.MapFaction) return $"jail: {seat.Name} belongs to the player's own realm, which never holds him";

         string kept = "at war";
         if (!FactionManager.IsAtWarAgainstFaction(realm, Hero.MainHero.MapFaction))
         {
            ChangeCrimeRatingAction.Apply(realm, Math.Max(0f, 40f - realm.MainHeroCrimeRating), false);
            kept = $"crime rating raised to {realm.MainHeroCrimeRating:0} (moderate) so the realm keeps the player";
         }

         // A lord at the head of a party is moved with it: a lone teleport takes a party leader out of nothing.
         MobileParty party = lord.PartyBelongedTo;
         if (lord.CurrentSettlement != seat && !lord.IsPrisoner)
         {
            if (party != null && party.LeaderHero == lord && party.MapEvent == null)
            {
               if (party.Army != null) party.Army = null;
               if (party.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(party);
               party.Position = seat.GatePosition;
               EnterSettlementAction.ApplyForParty(party, seat);
            }
            else TeleportHeroAction.ApplyImmediateTeleportToSettlement(lord, seat);
         }

         TakePrisonerAction.Apply(seat.Party, Hero.MainHero);

         return $"jail: the player is held in {seat.Name} ({kept}); {lord.Name} is in {lord.CurrentSettlement?.Name?.ToString() ?? "(nowhere)"}\n" + State(lord);
      }

      private static string Open(Hero lord, string door)
      {
         if (lord == null) return "open: name a lord";

         var sb = new StringBuilder();
         sb.AppendLine($"open ({door}) with {lord.Name}; before:");
         sb.AppendLine(State(lord));
         if (door == "plain") ChatSceneStager.StagePlainConversation(lord);
         else ChatSceneStager.StageTaleMeeting(lord);
         sb.AppendLine("after the call (the scene may open a moment later; run 'info' once it is up, and again after closing it):");
         sb.Append(State(lord));

         return sb.ToString();
      }

      private static string State(Hero lord)
      {
         Hero me = Hero.MainHero;
         PartyBase held = me.PartyBelongedToAsPrisoner;
         var sb = new StringBuilder();
         sb.AppendLine($"  time: {CampaignTime.Now} night={CampaignTime.Now.IsNightTime}");
         sb.AppendLine($"  player: prisoner={me.IsPrisoner}, held by {(held == null ? "(none)" : held.Name.ToString())} "
                       + $"(settlement={held?.IsSettlement}, mobile={held?.IsMobile}, settlement={held?.Settlement?.Name}, leader={held?.LeaderHero?.Name}, owner clan={held?.Settlement?.OwnerClan?.Name})");
         sb.AppendLine($"  PlayerCaptivity.CaptorParty: {PlayerCaptivity.CaptorParty?.Name}; IsCaptive: {PlayerCaptivity.IsCaptive}");
         sb.AppendLine($"  PlayerEncounter: current={PlayerEncounter.Current != null}, insideSettlement={PlayerEncounter.InsideSettlement}, encounterSettlement={PlayerEncounter.EncounterSettlement?.Name}");
         sb.AppendLine($"  main party: active={MobileParty.MainParty?.IsActive}, currentSettlement={MobileParty.MainParty?.CurrentSettlement?.Name}, hero currentSettlement={me.CurrentSettlement?.Name}");
         sb.AppendLine($"  menu: {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "(none)"}; mission: {(Mission.Current == null ? "(none)" : Mission.Current.SceneName)}; chat open: {ChatManager.IsOpen}");
         if (lord != null)
            sb.AppendLine($"  {lord.Name}: in {lord.CurrentSettlement?.Name?.ToString() ?? "(no settlement)"}, party {lord.PartyBelongedTo?.Name?.ToString() ?? "(none)"}, prisoner={lord.IsPrisoner}, same place as the player={lord.CurrentSettlement != null && lord.CurrentSettlement == held?.Settlement}");

         return sb.ToString();
      }

      private static string Probe(string text)
      {
         ModLog.Info("[PROBE] captive_talk_probe\n" + text);

         return text;
      }
   }
}
