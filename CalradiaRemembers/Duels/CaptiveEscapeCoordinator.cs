// Code written by Gabriel Mailhot, 20/07/2026.
// Sprint note (21/07/2026): gear consequence rewired onto CaptiveGearLossPolicy + CaptiveGearBehavior (see
// their headers). WHICH outcomes strip gear is no longer hard-coded here. Both freeing outcomes also now
// grant a modest, flat skill-XP reward (GrantEscapeXp): a real feat teaches the character something, without
// becoming a skill-farming loop (see the TUNE constants' own comments for the reasoning).
// Sequences the captivity escape: the captive chat ARMS an escape (the player committed to breaking free), the
// player closes the chat with the "Leave" button, and this coordinator then stages the real 1vN encounter once
// the map is live again (Mission.Current == null, chat gone). A mission cannot be opened from inside the chat
// screen, so the launch is deferred to a tick after the close. When the encounter ends, the consequence is
// applied on the map (not mid-mission): BeatThemAll frees the player with equipment untouched (they are
// standing over the men who took it); Fled frees the player but hands their equipment to a holder
// (CaptiveGearLossPolicy.TakesGearOnResolution is the ONE seam deciding which outcomes strip gear, kept
// isolated as hygiene even though the owner has since confirmed no flip is coming); Overpowered does nothing,
// the player stays a prisoner and vanilla captivity carries on (the loss is attached to escaping, through
// CR's OWN encounter, never to being captured or to a vanilla ransom/escape, so a knocked-out player is not
// punished twice). Subscribed to TickEvent at session launch; the pending state is process-only (an escape
// never spans a save).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
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

      // TUNE: modest Roguery reward for freeing yourself through a REAL escape encounter (BeatThemAll or
      // Fled). Kept small and flat, not scaled by aggressor count, because an escape attempt is repeatable
      // across separate captivity events over a campaign: the EscapeAlreadySpent gate limits it to ONE per
      // event, but nothing stops many events over time, so this must read as a nod to a real feat, never a
      // skill-farming loop.
      private const float RogueryXpOnFreedom = 15f;

      // TUNE: additional Athletics reward for FLED specifically, since that outcome is literally outrunning
      // pursuers to the edge of the field. Same "never a farm" reasoning as RogueryXpOnFreedom above.
      private const float AthleticsXpOnFled = 15f;

      private static IReadOnlyList<CharacterObject> _pendingFoes;   // set when the chat arms an escape
      private static Hero _pendingCaptorHero;                       // the chat's captor NPC (null = a not-yet-revealed nemesis)
      private static bool _pendingCaptorIsBandit;                   // true = a bandit hold (nemesis inherits); false = a lord keeps it
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
      ///   first, so the player fights him; nulls become bandit fillers). <paramref name="captor" /> is the
      ///   chat's NPC (null for a not-yet-revealed nemesis); <paramref name="captorIsBandit" /> decides who
      ///   inherits the player's gear on a Fled outcome: a bandit hold's gear goes to the region's nemesis
      ///   (minted if needed), a lord keeps it himself. <paramref name="onOverpowered" /> reopens the captive
      ///   scene if the player loses (the scene resumes after the beating). The launch waits for the chat to close.
      /// </summary>
      public static void Arm(IReadOnlyList<CharacterObject> foes, Hero captor, bool captorIsBandit, Action onOverpowered)
      {
         _pendingFoes = foes;
         _pendingCaptorHero = captor;
         _pendingCaptorIsBandit = captorIsBandit;
         _onOverpowered = onOverpowered;
         _escapeSpent = true; // this event's one attempt is now used up
         ModLog.Info($"[ESCAPE] armed from chat: {foes?.Count ?? 0} foe(s), captor={captor?.Name?.ToString() ?? "(bandit)"}, bandit={captorIsBandit}. Waiting for the chat to close.");
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
         Hero captorHero = _pendingCaptorHero;
         bool captorIsBandit = _pendingCaptorIsBandit;
         _onOverpowered = null; // one-shot, whichever way it ended
         _pendingCaptorHero = null;
         _pendingCaptorIsBandit = false;

         if (outcome == EscapeOutcome.Overpowered)
         {
            // The loss is attached to ESCAPING, never to being captured: nothing was ever taken here, so
            // there is nothing to strip (matches vanilla, where being a prisoner alone does not strip you).
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

            CaptiveEscapeResolution resolution = outcome == EscapeOutcome.BeatThemAll
               ? CaptiveEscapeResolution.FoughtFree
               : CaptiveEscapeResolution.Fled;

            string message;
            if (CaptiveGearLossPolicy.TakesGearOnResolution(resolution))
            {
               // Resolve BEFORE ending captivity: PartyBelongedToAsPrisoner still points at the real band.
               MobileParty band = player.PartyBelongedToAsPrisoner?.MobileParty;
               var today = (int) CampaignTime.Now.ToDays;

               if (captorIsBandit)
               {
                  // Ratified 2026-07-24 (AUDIT-NEMESIS M1): a bandit escape SCATTERS the gear among that
                  // culture's bands (recovered a piece at a time by beating them), instead of minting a
                  // nemesis to hold the whole stock. Closes the resurrect-during-cooldown / counter-inflation
                  // harm: no nemesis is minted for loot at all.
                  string culture = BandCultureKey(band);
                  int scattered = ScatteredLootBehavior.Instance?.ScatterPlayerGear(culture, today) ?? 0;
                  message = scattered > 0
                     ? "You broke free of your captors, but your gear and mount are scattered among the region's bandits. " +
                       "Hunt down their bands to win it back, piece by piece."
                     : "You broke free of your captors.";
               }
               else if (captorHero != null && (CaptiveGearBehavior.Instance?.TransferPlayerGearTo(captorHero) ?? false))
                  // A LORD keeps his captive's gear himself (unchanged holder model): beat him to recover it whole.
                  message = $"You broke free of your captors. Your equipment and mount are lost, held by {captorHero.Name}.";
               else
               {
                  // Fail-safe: no lord resolved, so nothing is stripped rather than lost with nowhere to go.
                  ModLog.Error("[ESCAPE] no gear holder resolved on Fled; the player's equipment is left untouched.");
                  message = "You broke free of your captors.";
               }
            }
            else
               message = outcome == EscapeOutcome.BeatThemAll
                  ? "You beat your captors down and fought free, keeping your equipment and mount."
                  : "You broke free of your captors.";

            EndCaptivityAction.ApplyByEscape(player);
            message += GrantEscapeXp(player, outcome);
            MBInformationManager.AddQuickInformation(new TextObject(message));
            ModLog.Info($"[ESCAPE] player freed ({outcome}).");
         }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] applying freedom failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   A real escape encounter is a feat, so freeing yourself teaches something: Roguery for either
      ///   freeing outcome (getting yourself out is roguish work), plus Athletics on top for Fled
      ///   specifically (that outcome is literally outrunning pursuers to the edge of the field). Never
      ///   granted on Overpowered (the attempt failed). Returns a short suffix for the freedom notice, or
      ///   an empty string when there is nothing to grant (no HeroDeveloper, e.g. a badly-modded save).
      /// </summary>
      private static string GrantEscapeXp(Hero player, EscapeOutcome outcome)
      {
         if (outcome == EscapeOutcome.Overpowered) return string.Empty;

         HeroDeveloper developer = player.HeroDeveloper;
         if (developer == null) return string.Empty;

         developer.AddSkillXp(DefaultSkills.Roguery, RogueryXpOnFreedom, true, false);
         var note = $" (+{RogueryXpOnFreedom:0} Roguery)";

         if (outcome == EscapeOutcome.Fled)
         {
            developer.AddSkillXp(DefaultSkills.Athletics, AthleticsXpOnFled, true, false);
            note = $" (+{RogueryXpOnFreedom:0} Roguery, +{AthleticsXpOnFled:0} Athletics)";
         }

         return note;
      }

      /// <summary>The bandit-clan culture key of the capturing band (looters / forest_bandits / ...), for scattering the loot.</summary>
      private static string BandCultureKey(MobileParty band)
      {
         string clanId = band?.ActualClan?.StringId;

         return string.IsNullOrEmpty(clanId) ? "looters" : clanId;
      }
   }
}
