// Code written by Gabriel Mailhot, 28/06/2026.
// "Stance with teeth", increment 3.2 — the gate ambush menu. The AmbushSettlementAccessModel denies town entry
// while an attempt on the player's life is armed, which drops the player into the vanilla "you are not allowed
// inside" menu at the gates. That menu is not what we want: there is no walking away from killers who have come
// for you. This behavior redirects that moment into a dedicated one-option menu — face the ambush, period. The
// single option opens the fight FROM the gate context (no town interior loaded), which is the piece still under
// in-game iteration; everything here is guarded so a failure can never crash the menu, only fail to open a scene.

#region

using System;
using CalradiaRemembers.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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

      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         starter.AddGameMenu(MenuId,
            "Hooded figures peel from the shadows as you near the gate, steel already drawn. " +
            "This is no robbery — they have come for your life, and there is no slipping past them.",
            OnMenuInit, GameMenu.MenuOverlayType.None, GameMenu.MenuFlags.None, null);

         // A single option — the ambush. No "Leave": the cutthroats stand between the player and the open road.
         starter.AddGameMenuOption(MenuId, "cr_gate_ambush_fight",
            "Draw your weapon and meet them",
            OnFightCondition, OnFightConsequence, false, -1, false);
      }

      private void OnMenuInit(MenuCallbackArgs args)
      {
         // The fight may return control here (e.g. a clean win disarmed the threat). Never trap the player in a
         // menu whose only purpose is spent — send them back to the map so they can enter the town normally.
         Settlement s = GateSettlement();
         if (s == null || !TownAmbushBehavior.IsEntryBlocked(s))
         {
            try { GameMenu.ExitToLast(); } catch { /* returning to the map is best-effort */ }
         }
      }

      private static bool OnFightCondition(MenuCallbackArgs args)
      {
         args.optionLeaveType = GameMenuOption.LeaveType.DefendAction;

         return true;
      }

      private static void OnFightConsequence(MenuCallbackArgs args)
      {
         Settlement s = GateSettlement();
         if (s == null) { ModLog.Error("[AMBUSH] gate menu: no settlement to spring against."); return; }

         if (!TownAmbushBehavior.SpringAtGate(s))
         {
            ModLog.Error("[AMBUSH] gate menu: SpringAtGate found nothing armed — releasing the player.");
            try { GameMenu.ExitToLast(); } catch { /* best-effort */ }
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

      /// <summary>The town the player is being held out of — readable from the open-gate encounter when outside.</summary>
      private static Settlement GateSettlement()
      {
         Settlement s = Settlement.CurrentSettlement;
         if (s is {IsTown: true}) return s;

         try
         {
            Settlement encountered = PlayerEncounter.EncounterSettlement ?? PlayerEncounter.LocationEncounter?.Settlement;
            if (encountered is {IsTown: true}) return encountered;
         }
         catch { /* encounter state not readable — fall through */ }

         return null;
      }

      #endregion
   }
}
