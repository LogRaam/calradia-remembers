// Code written by Gabriel Mailhot, 07/09/2026.
// Companion-briefing pillar: the engine glue between the vanilla dialogue option ("grant me a moment to speak
// with my own people") and the private briefing chat. Offers a picker of the player's own companions (the same
// candidate pool and cap the ordinary witness picker uses, WitnessResolver.GetPlayerCompanions/MaxCompanions,
// so a briefing can never gather a roster the follow-up conversation could not itself seat), then hands the
// chosen roster to ChatManager.OpenForCompanionBriefing. Mirrors WitnessResolver.ShowCompanionPickerIfNeeded's
// own shape (async MultiSelectionInquiry, fail-soft try/catch), kept a separate small file rather than folded
// into WitnessResolver: the picker's WORDING and MINIMUM selection differ (this one requires at least one
// companion, since the player just asked, in the vanilla dialogue, for a word with his own people).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Witnesses;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Companions
{
   /// <summary>Offers the companion-briefing picker and opens the resulting private chat.</summary>
   public static class CompanionBriefingCoordinator
   {
      /// <summary>
      ///   Ruling #4: the vanilla dialogue option must appear ONLY when the player actually has companions to
      ///   brief, never as dead noise in an already-crowded menu. Reuses the SAME candidate pool the ordinary
      ///   witness picker offers, so "has someone to brief" and "has someone the follow-up conversation could
      ///   seat" never disagree.
      /// </summary>
      public static bool HasBriefableCompanions(Hero target)
         => WitnessResolver.GetPlayerCompanions(target).Count > 0;

      /// <summary>
      ///   Shows the companion picker for <paramref name="target" />'s conversation. On confirmation, opens the
      ///   private briefing chat over the chosen roster; cancelling (or picking none) leaves the vanilla
      ///   dialogue's own close (already handled by its own "close_window" output) to return the player to the
      ///   map, with nothing further to do here. Fail-soft: any exception is logged and swallowed, mirroring
      ///   every other picker in this codebase.
      /// </summary>
      public static void OfferPicker(Hero target)
      {
         try
         {
            // A prisoner has no private counsel to hold, the same guarantee ShowCompanionPickerIfNeeded gives
            // the ordinary witness picker.
            if (Hero.MainHero?.IsPrisoner == true) return;

            List<Hero> companions = WitnessResolver.GetPlayerCompanions(target);
            if (companions.Count == 0)
            {
               ModLog.Info("[BRIEFING] picker skipped, no companions available to brief.");

               return;
            }

            var elements = companions.Select(c => new InquiryElement(c.StringId, c.Name.ToString(), null)).ToList();

            ModLog.Info($"[BRIEFING] picker opening ({companions.Count} option(s)).");

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Speak With Your Own People",
                  "Whom would you draw aside for a private word before this meeting continues?",
                  elements,
                  true, // isExitShown (Exit = never mind, say nothing)
                  1, WitnessResolver.MaxCompanions, // at least one: the player just asked for this word
                  "Speak With Them", "Never Mind",
                  selected => OnCompanionsChosen(target, selected),
                  null),
               false, // pauseGameActiveState (nothing else is running that needs pausing)
               true); // prioritize
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] OfferPicker failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static void OnCompanionsChosen(Hero target, List<InquiryElement> selected)
      {
         try
         {
            if (selected == null || selected.Count == 0) return;

            var chosen = new List<Hero>();
            foreach (InquiryElement element in selected)
            {
               var id = element.Identifier as string;
               Hero hero = string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

               if (hero != null) chosen.Add(hero);
            }

            if (chosen.Count == 0) return;

               // Remember the meeting this briefing is FOR. Until 2026-09-13 the target survived only as a
            // StringId and a display name on the view-model, for the sole purpose of telling this chat's
            // close apart from an ordinary one - the Hero itself was dropped, so there was nothing to go
            // back to. Gabriel played it and the whole encounter ended instead (see ArmReturn).
            RememberReturn(target, chosen);

            ChatManager.OpenForCompanionBriefing(target, chosen);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] OnCompanionsChosen failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
      #region the return

      /// <summary>
      ///   Records the meeting a briefing is FOR, apart from opening anything. Split out so the live sweep can
      ///   walk the whole briefing-to-meeting vector without an inquiry popping up mid-test: the picker is a
      ///   window, and what has to be RIGHT is what it decides.
      /// </summary>
      public static void RememberReturn(Hero target, IEnumerable<Hero> chosen)
      {
         _returnTo = target;
         _returnWith = chosen == null ? new List<Hero>() : new List<Hero>(chosen);
      }

      /// <summary>The companions we owe a seat at that meeting, for the sweep to check against.</summary>
      public static IReadOnlyList<Hero> PendingReturnCompanions => _returnWith ?? new List<Hero>();

      private static Hero _returnTo;
      private static List<Hero> _returnWith;
      private static bool _returnArmed;

      /// <summary>True while a briefing is open for a meeting we still owe the player a way back into.</summary>
      public static bool HasPendingReturn => _returnTo != null;

      /// <summary>
      ///   Arms the return, called as the briefing chat closes. Deferred rather than immediate for the reason
      ///   DuelCoordinator defers its own restart: the chat is still tearing down on this very call, and a
      ///   conversation cannot be staged over a window that has not finished closing.
      /// </summary>
      public static void ArmReturn()
      {
         if (_returnTo == null) return;

         _returnArmed = true;
         ModLog.Info($"[BRIEFING] return to {_returnTo.Name} armed; waiting for the chat to come down.");
      }

      /// <summary>
      ///   Drives the armed return once the chat is actually down. Called from the campaign tick.
      ///   <para>
      ///     Waits on the same two facts DuelCoordinator waits on - no chat, no mission - because the vanilla
      ///     conversation was ENDED by the engine's own close_window token back at the briefing dialogue line,
      ///     so there is nothing to resume and a scene must be staged fresh.
      ///   </para>
      /// </summary>
      public static void TickReturn()
      {
         if (!_returnArmed) return;

         try
         {
            if (ChatManager.IsOpen || Mission.Current != null) return;

            Hero target = _returnTo;
            List<Hero> briefed = _returnWith;
            Forget();

            if (target == null || !target.IsAlive) return;

            ChatSceneStager.StageBriefedReturn(target, briefed);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] the return failed: {ex.GetType().Name}: {ex.Message}");
            Forget();
         }
      }

      /// <summary>Drops the pending return: the briefing was abandoned, or the return has been handed over.</summary>
      public static void Forget()
      {
         _returnTo = null;
         _returnWith = null;
         _returnArmed = false;
      }

      /// <summary>The name of the meeting we owe a way back into, for the close button. Null when there is none.</summary>
      public static string PendingReturnName => _returnTo?.Name?.ToString();

      #endregion
   }
}