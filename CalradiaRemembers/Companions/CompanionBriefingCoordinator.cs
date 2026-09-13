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
using CalradiaRemembers.Logic.Conversation;
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
               AbandonBriefing("no companions available to brief");

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
                  // "Never Mind". Previously null, and harmless while the dialogue line exited on
                  // close_window: the conversation was already over by the time the picker appeared. It now
                  // IDLES instead, so somebody has to end it or the player is left staring at a dialogue with
                  // no replies (see BriefCompanionsHoldToken).
                  _ => AbandonBriefing("the player said never mind")),
               false, // pauseGameActiveState (nothing else is running that needs pausing)
               true); // prioritize
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] OfferPicker failed: {ex.GetType().Name}: {ex.Message}");
            AbandonBriefing("the picker could not be shown");
         }
      }

      private static void OnCompanionsChosen(Hero target, List<InquiryElement> selected)
      {
         try
         {
            if (selected == null || selected.Count == 0)
            {
               AbandonBriefing("nobody was chosen");

               return;
            }

            var chosen = new List<Hero>();
            foreach (InquiryElement element in selected)
            {
               var id = element.Identifier as string;
               Hero hero = string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

               if (hero != null) chosen.Add(hero);
            }

            if (chosen.Count == 0)
            {
               AbandonBriefing("none of the chosen could be resolved");

               return;
            }

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
            AbandonBriefing("the briefing could not be opened");
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
      ///   Drives the armed return once the briefing window is actually down. Called from the application tick.
      ///   <para>
      ///     IT WAITS FOR THE CHAT, AND ONLY THE CHAT. The first cut also waited for Mission.Current to go
      ///     null, copied from DuelCoordinator - where that is right for the opposite reason: a duel needs its
      ///     OWN arena, so it must let the current mission end first. A briefing return wants the scene the
      ///     player is ALREADY standing in. Gabriel briefed his companion inside Ira's hall, ended it, and got
      ///     a load screen that went nowhere (2026-09-13): the guard held until something made the mission
      ///     momentarily null, then staged a second scene on top of the one he had never left.
      ///   </para>
      ///   <para>
      ///     ChatSceneStager.Open already makes exactly this decision - a live scene is opened over, only an
      ///     empty one is staged - so the right move is to hand it the request and stop second-guessing it.
      ///   </para>
      /// </summary>
      public static void TickReturn()
      {
         if (!_returnArmed) return;

         try
         {
            bool chatOpen = ChatManager.IsOpen;
            bool sceneLive = Mission.Current != null;
            ReturnMove move = BriefingReturnDecision.Decide(_returnArmed, chatOpen, sceneLive, ++_ticksWaited);

            // Logged on CHANGE only: this runs on every application tick, and Wait is the answer for most of
            // them. What a reader needs is the inputs at the moment the answer turned - because every fault
            // in this sequence so far has been an answer that was wrong for the inputs it had, and no log
            // ever showed the inputs.
            if (move != _lastLoggedMove)
            {
               _lastLoggedMove = move;
               ModLog.Info($"[BRIEFING] decide(armed=true chat={chatOpen} scene={sceneLive} " +
                           $"waited={_ticksWaited}) -> {move}");
            }

            switch (move)
            {
               case ReturnMove.Idle:
               case ReturnMove.Wait:
                  return;

               case ReturnMove.GiveTheWorldBack:
                  // The hand-off cannot be caught: no scene to open over, or the wait ran long. Settle what
                  // the close deferred rather than leave the map suspended behind an empty screen.
                  ModLog.Info("[BRIEFING] the return cannot happen; settling what the hand-off deferred.");
                  Forget();
                  ChatManager.FinishDeferredClose();

                  return;

               case ReturnMove.OpenOverLiveScene:
                  Hero target = _returnTo;
                  List<Hero> briefed = _returnWith;
                  Forget();

                  if (target == null || !target.IsAlive) return;

                  ModLog.Info($"[BRIEFING] returning to {target.Name} over the scene already loaded.");
                  ChatSceneStager.Open(ChatSceneStager.BuildBriefedReturn(target, briefed));

                  return;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] the return failed: {ex.GetType().Name}: {ex.Message}");
            Forget();
            ChatManager.FinishDeferredClose();
         }
      }

      private static int _ticksWaited;
      private static ReturnMove _lastLoggedMove = ReturnMove.Idle;

      /// <summary>
      ///   Nothing came of the briefing, so end the meeting it was nested inside.
      ///   <para>
      ///     THE DEBT THE HOLD TOKEN TOOK ON. While the dialogue line exited on close_window, every way of
      ///     abandoning a briefing was already handled: the conversation was over before the picker even
      ///     appeared. Leaving it IDLING is what makes the way back possible without building a scene - and it
      ///     means an abandoned briefing now leaves a live conversation with no reply and no chat over it,
      ///     which is a player stuck in a window. One exit, called from every abandonment, so a new one cannot
      ///     be added without meeting this.
      ///   </para>
      /// </summary>
      /// <summary>The same exit, for the one abandonment that happens outside this class (the window failing to open).</summary>
      public static void AbandonBriefingFromHost() => AbandonBriefing("the briefing window did not open");

      private static void AbandonBriefing(string why)
      {
         Forget();

         try
         {
            ModLog.Info($"[BRIEFING] abandoned ({why}); the meeting ends as it would have before.");

            if (Hero.OneToOneConversationHero != null && Campaign.Current?.ConversationManager != null)
               Campaign.Current.ConversationManager.EndConversation();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] could not end the conversation after abandoning: " +
                         $"{ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Drops the pending return: the briefing was abandoned, or the return has been handed over.</summary>
      public static void Forget()
      {
         _returnTo = null;
         _returnWith = null;
         _returnArmed = false;
         _ticksWaited = 0;
         _lastLoggedMove = ReturnMove.Idle;
      }

      /// <summary>The name of the meeting we owe a way back into, for the close button. Null when there is none.</summary>
      public static string PendingReturnName => _returnTo?.Name?.ToString();

      #endregion
   }
}