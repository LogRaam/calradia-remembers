// Code written by Gabriel Mailhot, 20/07/2026.
// Sequences the captivity escape: the captive chat ARMS an escape (the player committed to breaking free), the
// player closes the chat with the "Leave" button, and this coordinator then stages the real 1vN encounter once
// the map is live again (Mission.Current == null, chat gone). A mission cannot be opened from inside the chat
// screen, so the launch is deferred to a tick after the close. When the encounter ends, the consequence is
// applied on the map (not mid-mission): fought free or fled -> escape + lost weapons and mount; overpowered ->
// nothing, the player stays a prisoner and vanilla captivity carries on. Subscribed to TickEvent at session
// launch; the pending state is process-only (an escape never spans a save).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Chat;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Bridges the in-chat escape decision to the real escape mission and its map-side consequence.</summary>
   public static class CaptiveEscapeCoordinator
   {
      /// <summary>
      ///   The synthetic stage cue that reopens the captive scene when the player LOSES the escape fight: the
      ///   captor opens on the recapture, so the scene picks up as the logical aftermath of the beating.
      /// </summary>
      public const string ResumeAfterLostEscapeCue =
         "[The prisoner's escape attempt is OVER and it FAILED: they made a violent break for freedom, were beaten " +
         "down by your people and hauled back to you, subdued. This is the AFTERMATH, not a new attempt: do NOT emit " +
         "an escape_attempt action. React to having them in your hands again this very instant: your anger at the " +
         "attempt, the cost of it to them, your grip reasserted. Carry the scene on from here.]";

      private static IReadOnlyList<CharacterObject> _pendingFoes;   // set when the chat arms an escape
      private static Action _onOverpowered;                         // reopens the captive scene if the player loses
      private static EscapeOutcome? _pendingConsequence;            // set when the mission ends, applied next map tick
      private static bool _escapeSpent;                             // an escape was already tried in THIS captivity event

      /// <summary>
      ///   True once an escape has been attempted in the current captivity event. ONE attempt per event: after a
      ///   failed break the captor is watching ("slip me twice? not a chance"), so the scene cannot loop back into
      ///   the fight. A genuinely NEW captive scene clears it; the scene RESUMED after a loss does not.
      /// </summary>
      public static bool EscapeAlreadySpent => _escapeSpent;

      /// <summary>
      ///   Called when a FRESH captive scene opens (never on the post-defeat resume, which is the same event):
      ///   the player earns a new chance to break out.
      /// </summary>
      public static void NoteNewCaptiveScene() => _escapeSpent = false;

      /// <summary>
      ///   Arms an escape from the captive chat: <paramref name="foes" /> is the roster to face (the real captor
      ///   first, so the player fights him; nulls become bandit fillers). <paramref name="onOverpowered" /> reopens
      ///   the captive scene if the player loses (the scene resumes after the beating). The launch waits for the
      ///   chat to close.
      /// </summary>
      public static void Arm(IReadOnlyList<CharacterObject> foes, Hero captor, Action onOverpowered)
      {
         _pendingFoes = foes;
         _onOverpowered = onOverpowered;
         _escapeSpent = true; // this event's one attempt is now used up
         ModLog.Info($"[ESCAPE] armed from chat: {foes?.Count ?? 0} foe(s), captor={captor?.Name?.ToString() ?? "(bandit)"}. Waiting for the chat to close.");
      }

      /// <summary>TickEvent handler (subscribed at session launch): launches the armed encounter and, later, applies its outcome.</summary>
      public static void OnTick(float dt)
      {
         try
         {
            if (Campaign.Current == null) return;

            // Phase 2: the escape mission has ended -> apply the consequence on the live map.
            if (_pendingConsequence.HasValue && Mission.Current == null)
            {
               EscapeOutcome outcome = _pendingConsequence.Value;
               _pendingConsequence = null;
               ApplyOutcome(outcome);

               return;
            }

            // Phase 1: the chat has closed and no mission is running -> stage the encounter exactly once.
            if (_pendingFoes != null && Mission.Current == null && !ChatManager.IsOpen)
            {
               IReadOnlyList<CharacterObject> foes = _pendingFoes;
               _pendingFoes = null;
               CaptiveEscapeMissionLauncher.StartEscape(foes, o => _pendingConsequence = o);
            }
         }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] coordinator tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static void ApplyOutcome(EscapeOutcome outcome)
      {
         Action onOverpowered = _onOverpowered;
         _onOverpowered = null; // one-shot, whichever way it ended

         if (outcome == EscapeOutcome.Overpowered)
         {
            // The scene RESUMES: reopen the captive chat so the captor reacts to the beaten-back prisoner, the
            // logical continuation of the fight. Falls back to a bare notice if there is no scene to resume.
            if (onOverpowered != null)
            {
               try
               {
                  onOverpowered();
                  ModLog.Info("[ESCAPE] player overpowered; the captive scene resumes on the recapture.");

                  return;
               }
               catch (Exception ex) { ModLog.Error($"[ESCAPE] reopen-on-loss failed: {ex.GetType().Name}: {ex.Message}"); }
            }

            MBInformationManager.AddQuickInformation(new TextObject("You were overpowered and remain a prisoner."));
            ModLog.Info("[ESCAPE] player overpowered, stays a prisoner, vanilla captivity carries on.");

            return;
         }

         try
         {
            Hero player = Hero.MainHero;
            if (player is not {IsPrisoner: true}) return; // freed elsewhere in the meantime; nothing to do

            StripWeaponsAndMount(player); // the captors kept your gear; you leave with your skin and your armour
            EndCaptivityAction.ApplyByEscape(player);
            MBInformationManager.AddQuickInformation(new TextObject("You broke free of your captors. Your weapons and mount are lost."));
            ModLog.Info($"[ESCAPE] player freed ({outcome}); weapons and mount stripped.");
         }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] applying freedom failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Clears the player's carried weapons and mount (kept by the captors); armour is left intact.</summary>
      private static void StripWeaponsAndMount(Hero player)
      {
         Equipment eq = player.BattleEquipment;
         if (eq == null) return;

         for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
            eq[i] = default;
         eq[EquipmentIndex.Horse] = default;
         eq[EquipmentIndex.HorseHarness] = default;
      }
   }
}
