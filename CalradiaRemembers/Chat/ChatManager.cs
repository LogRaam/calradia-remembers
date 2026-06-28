// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Manages the lifecycle of the Calradia Remembers chat window as a
   ///   non-blocking overlay. Attaches a GauntletLayer to the current top screen
   ///   (MapScreen, MissionScreen, etc.) so the host screen keeps rendering and
   ///   ticking normally behind our overlay.
   ///   Sprint 7.6.G — Auto-scroll via custom ChatScrollablePanel:
   ///   - ScrollToBottom() sets the panel's ScrollToBottomRequested flag.
   ///   - The custom panel acts on the flag in its next OnLateUpdate, after
   ///   MaxValue has been recomputed for the new content.
   ///   - We reach the panel via ChatScrollablePanel.ActiveInstance, which
   ///   the widget sets in its constructor.
   /// </summary>
   public static class ChatManager
   {
      private const string PrefabName = "CalradiaRemembersChat";

      private static GauntletLayer _activeLayer;
      private static GauntletMovieIdentifier _activeMovieId;
      private static ScreenBase _hostScreen;
      private static CampaignTimeControlMode _previousTimeControlMode;
      private static bool _previousTimeControlModeLock;
      private static bool _timeControlModeSaved;

      public static ChatViewModel ActiveViewModel { get; private set; }

      public static bool IsOpen => _activeLayer != null;

      public static void Close()
      {
         if (!IsOpen) return;

         // CRITICAL: FreeResources() (which nulls _activeLayer) MUST run no matter what.
         // If any teardown step threw before it, _activeLayer would stay non-null, IsOpen
         // would stay true forever, and every future Open() would no-op — silently killing
         // all conversations for the rest of the session. The finally block guarantees the
         // reset; each Finalize* is individually guarded so one failing does not skip the rest.
         try
         {
            // Record the session in the NPC's memory before tearing down. Each call is
            // a no-op outside its own mode and idempotent. The async summary either may
            // launch captures the data it needs, so it survives the teardown below.
            SafeStep(() => ActiveViewModel?.FinalizeStoryLog(), "FinalizeStoryLog");
            SafeStep(() => ActiveViewModel?.FinalizeCaptiveSceneMemory(), "FinalizeCaptiveSceneMemory");
            SafeStep(() => ActiveViewModel?.FinalizeConversationMemory(), "FinalizeConversationMemory");

            SafeStep(KillChatWindow, "KillChatWindow");
            SafeStep(EndVanillaConversation, "EndVanillaConversation");
            SafeStep(EndMapEncounterIfNeeded, "EndMapEncounterIfNeeded");
            SafeStep(RestoreTime, "RestoreTime");
         }
         finally
         {
            FreeResources();
            SafeStep(LlmServices.ClearTestGenderOverride, "ClearTestGenderOverride");
            ModLog.Info("Chat window closed — returned to campaign map.");
         }
      }

      public static void Open(string npcName = null)
      {
         if (IsOpen)
         {
            ModLog.Info("ChatManager.Open called but chat is already open.");

            return;
         }

         _hostScreen = ScreenManager.TopScreen;
         if (_hostScreen == null)
         {
            ModLog.Error("ChatManager.Open: no top screen available.");

            return;
         }

         if (Campaign.Current != null) PauseCampaignMap();

         try
         {
            GenerateChatWindow(npcName);
         }
         catch (Exception ex)
         {
            // A half-built window would leave _activeLayer set and trap every future Open()
            // behind the IsOpen guard. Tear down what little we made and reset so the next
            // attempt has a clean slate.
            ModLog.Error($"ChatManager.Open: GenerateChatWindow failed: {ex.GetType().Name}: {ex.Message}");
            SafeStep(KillChatWindow, "KillChatWindow (open-failure cleanup)");
            SafeStep(RestoreTime, "RestoreTime (open-failure cleanup)");
            FreeResources();

            return;
         }

         ModLog.Info($"Chat window opened on {_hostScreen.GetType().Name} with NPC '{npcName ?? "(none)"}'.");
      }

      // ── Public API ────────────────────────────────────────────────────────

      /// <summary>
      ///   Opens the chat window for a captive encounter initiated by the captor NPC.
      ///   Unlike a vanilla dialogue, this is triggered programmatically from the daily
      ///   tick — the player has no choice but to enter. Witnesses are pre-resolved by
      ///   <see cref="CalradiaRemembers.Letters.CaptiveEncounterService" />.
      /// </summary>
      public static void OpenForCaptiveEncounter(
         Hero captorHero,
         NpcProfile captorProfile,
         List<WitnessEntry> witnesses,
         bool isCollective,
         CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation,
         CharacterObject portraitCharacter = null)
      {
         Open(captorHero?.Name?.ToString() ?? captorProfile?.Name);

         if (ActiveViewModel == null) return;
         if (captorHero != null) ActiveViewModel.SetNpcHero(captorHero);
         else if (captorProfile != null) ActiveViewModel.SetNpcName(captorProfile.Name);
         if (captorHero == null && portraitCharacter != null)
            ActiveViewModel.SetNpcPortraitCharacter(portraitCharacter);
         if (witnesses is {Count: > 0}) ActiveViewModel.SetWitnesses(witnesses);

         // A faceless brigand captor has no Hero — that absence is exactly the bandit signal.
         bool captorIsBandit = captorHero == null;
         ActiveViewModel.ActivateCaptiveEncounterMode(captorProfile, isCollective, intent, captorIsBandit);
      }

      /// <summary>
      ///   Opens the chat window for a transient commoner conversation.
      ///   The synthetic profile and settlement knowledge are passed directly to
      ///   <see cref="ChatViewModel.ActivateCommonerMode" />; nothing is loaded
      ///   from or saved to the profile store.
      /// </summary>
      public static void OpenForCommoner(
         NpcProfile profile,
         CommonsKnowledge knowledge)
      {
         Open(profile?.Name);

         ActiveViewModel?.ActivateCommonerMode(profile, knowledge);
      }

      /// <summary>
      ///   Requests the chat to scroll to its newest message on the next frame.
      ///   Safe to call from any code path that just appended a message.
      /// </summary>
      public static void ScrollToBottom()
      {
         try
         {
            var panel = ChatScrollablePanel.ActiveInstance;
            if (panel == null)
            {
               ModLog.Info("ScrollToBottom: ChatScrollablePanel.ActiveInstance is null");

               return;
            }

            panel.RequestScrollToBottom();
            ModLog.Info("ScrollToBottom: requested on ActiveInstance.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"ScrollToBottom failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>
      ///   Runs a teardown/open step, swallowing and logging any exception so that one
      ///   failing step never aborts the sequence. Keeping the window lifecycle resilient
      ///   is what guarantees a single bad teardown does not freeze all future conversations.
      /// </summary>
      private static void SafeStep(Action step, string label)
      {
         try { step(); }
         catch (Exception ex)
         {
            ModLog.Error($"ChatManager.{label} failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Finishes the lingering map encounter after a map conversation. Talking to a lord on the
      ///   campaign map opens a PlayerEncounter that EndConversation alone does not close — so the
      ///   vanilla encounter menu stays up. For a lord LEADING AN ARMY that menu is the hostile army
      ///   menu (Attack / Send troops / Try to get away / Surrender) with NO "Leave", trapping the
      ///   player. Finishing the encounter returns them cleanly to the map. Strictly map-only: never
      ///   tear down a settlement encounter (the player's presence in a town) or a mission scene.
      /// </summary>
      private static void EndMapEncounterIfNeeded()
      {
         try
         {
            if (Mission.Current != null) return; // inside a scene/mission — not a map talk
            if (PlayerEncounter.Current == null) return; // no encounter to finish
            if (PlayerEncounter.InsideSettlement) return; // a town/castle encounter — leave it intact

            PlayerEncounter.Finish();
            ModLog.Info("Map encounter finished on chat close (clean return — no lingering army menu).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"EndMapEncounterIfNeeded failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // ── Private ───────────────────────────────────────────────────────────

      private static void EndVanillaConversation()
      {
         if (Campaign.Current?.ConversationManager != null)
            Campaign.Current.ConversationManager.EndConversation();
      }

      private static void FreeResources()
      {
         // Drop any LLM-turn continuations still queued — they belong to the conversation
         // we just closed and must never run against the next one.
         MainThreadDispatcher.Clear();

         _activeLayer = null;
         _activeMovieId = null;
         ActiveViewModel = null;
         _hostScreen = null;
         _timeControlModeSaved = false;
      }

      private static void GenerateChatWindow(string npcName)
      {
         ActiveViewModel = new ChatViewModel {OnCloseRequested = Close};

         var conversationHero = Hero.OneToOneConversationHero;
         if (conversationHero != null)
            ActiveViewModel.SetNpcHero(conversationHero);
         else if (!string.IsNullOrWhiteSpace(npcName))
            ActiveViewModel.SetNpcName(npcName);


         // GauntletLayer ctor: (int localOrder, string categoryId, bool shouldClear)
         // shouldClear=false keeps the host screen's rendering visible behind us.
         _activeLayer = new GauntletLayer(
            "CalradiaRemembers",
            10000);
         _activeLayer.InputRestrictions.SetInputRestrictions();

         _activeMovieId = _activeLayer.LoadMovie(PrefabName, ActiveViewModel);

         IGauntletMovie movie = _activeMovieId.Movie;

         ModLog.Info($"Movie type = {movie?.GetType().FullName}");
         ModLog.Info($"Movie loaded? {movie?.IsLoaded}");
         ModLog.Info($"RootWidget null? {movie?.RootWidget == null}");

         _hostScreen.AddLayer(_activeLayer);
         _activeLayer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_activeLayer);
      }

      private static void KillChatWindow()
      {
         if (_activeLayer == null) return;

         _activeLayer.IsFocusLayer = false;
         _activeLayer.InputRestrictions.ResetInputRestrictions();
         _activeLayer.ReleaseMovie(_activeMovieId);
         _hostScreen?.RemoveLayer(_activeLayer);
      }

      private static void PauseCampaignMap()
      {
         _previousTimeControlMode = Campaign.Current.TimeControlMode;
         _previousTimeControlModeLock = Campaign.Current.TimeControlModeLock;
         _timeControlModeSaved = true;
         // A LOCKED clock (e.g. the ambush convoy's forced fast-forward) ignores a mode change, so release the
         // lock first — otherwise the conversation runs while the map keeps ticking and events pile up.
         Campaign.Current.SetTimeControlModeLock(false);
         Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
         ModLog.Info($"Campaign time paused (was {_previousTimeControlMode}, lock {_previousTimeControlModeLock}).");
      }

      private static void RestoreTime()
      {
         if (!_timeControlModeSaved || Campaign.Current == null) return;

         // Restore both the mode and the lock, so a forced state we interrupted (the ambush convoy) resumes.
         Campaign.Current.TimeControlMode = _previousTimeControlMode;
         Campaign.Current.SetTimeControlModeLock(_previousTimeControlModeLock);
      }

      #endregion
   }
}