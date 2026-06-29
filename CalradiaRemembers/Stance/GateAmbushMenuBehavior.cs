// Code written by Gabriel Mailhot, 28/06/2026.

#region

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Redirects the "not allowed inside" gate menu into a single-option ambush when one is armed.</summary>
   public sealed class GateAmbushMenuBehavior : CampaignBehaviorBase
   {
      private const string MenuId = "cr_gate_ambush";

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
         CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
      }

      public override void SyncData(IDataStore dataStore) { } // armed state lives on each NpcProfile

      #region private

      /// <summary>The town the player is being held out of — readable from the open-gate encounter when outside.</summary>
      private static Settlement GateSettlement()
      {
         var s = Settlement.CurrentSettlement;

         if (s is {IsTown: true}) return s;

         try
         {
            Settlement encountered = PlayerEncounter.EncounterSettlement ?? PlayerEncounter.LocationEncounter?.Settlement;

            if (encountered is {IsTown: true}) return encountered;
         }
         catch
         {
            /* encounter state not readable — fall through */
         }

         return null;
      }

      private static bool OnFightCondition(MenuCallbackArgs args)
      {
         args.optionLeaveType = GameMenuOption.LeaveType.DefendAction;

         return true;
      }

      private static void OnFightConsequence(MenuCallbackArgs args)
      {
         Settlement s = GateSettlement();
         if (s == null)
         {
            ModLog.Error("[AMBUSH] gate menu: no settlement to spring against.");

            return;
         }

         if (TownAmbushBehavior.SpringAtGate(s)) return;
         ModLog.Error("[AMBUSH] gate menu: SpringAtGate found nothing armed — releasing the player.");
         try { GameMenu.ExitToLast(); }
         catch
         {
            /* best-effort */
         }
      }

      private void OnGameMenuOpened(MenuCallbackArgs args)
      {
         try
         {
            string current = args.MenuContext?.GameMenu?.StringId;

            if (current == MenuId) return; // already showing our menu — do not recurse

            Settlement s = GateSettlement();

            if (s == null || !TownAmbushBehavior.IsEntryBlocked(s)) return;

            ModLog.Info($"[AMBUSH] gate menu: redirecting '{current}' for {s.Name} into the ambush.");
            GameMenu.SwitchToMenu(MenuId);
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] gate menu redirect failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnMenuInit(MenuCallbackArgs args)
      {
         // The fight may return control here (e.g. a clean win disarmed the threat). Never trap the player in a
         // menu whose only purpose is spent — send them back to the map so they can enter the town normally.
         Settlement s = GateSettlement();

         if (s != null && TownAmbushBehavior.IsEntryBlocked(s)) return;
         try { GameMenu.ExitToLast(); }
         catch
         {
            /* returning to the map is best-effort */
         }
      }

      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         starter.AddGameMenu(MenuId,
            "Hooded figures peel from the shadows as you near the gate, steel already drawn. " +
            "This is no robbery — they have come for your life, and there is no slipping past them.",
            OnMenuInit);

         // A single option — the ambush. No "Leave": the cutthroats stand between the player and the open road.
         starter.AddGameMenuOption(MenuId, "cr_gate_ambush_fight",
            "Draw your weapon and meet them",
            OnFightCondition, OnFightConsequence);
      }

      #endregion
   }
}