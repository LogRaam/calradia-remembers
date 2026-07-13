// Code written by Gabriel Mailhot, 05/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GUI;
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
      // The chat overlay's render order (very high, so it sits above the campaign map). It cannot be changed
      // after the layer is built, so to keep a game screen opened over the chat (the encyclopedia) visible, the
      // arbiter slides the chat's WINDOW off-screen (a VM-bound offset) instead of restacking the layer.
      private const int ChatLayerOrder = 10000;
      private const string PrefabName = "CalradiaRemembersChat";
      private const double VramTickSeconds = 10.0;

      private static GauntletLayer _activeLayer;
      private static GauntletMovieIdentifier _activeMovieId;

      // Continuous focus arbitration. The chat is a high-order modal focus layer, so ANY game screen that
      // appears over it (the alt-tab pause menu, the encyclopedia, a native popup) would otherwise be starved
      // of input while the chat holds focus. Every frame (ArbitrateFocus, pumped from the chat panel) we
      // compare the host screen's layer count to the baseline taken when the chat opened: an extra layer means
      // something modal is up, so the chat yields its focus + input grab; back to baseline, the chat reclaims.
      private static int _baselineLayerCount;
      private static bool _chatHoldsFocus;
      private static ChatScreen _chatScreen;
      private static ScreenBase _hostScreen;

      /// <summary>The screen the chat overlay currently sits over (the naval map under War Sails, or the land map). Null when closed.</summary>
      internal static ScreenBase HostScreen => _hostScreen;

      // Heartbeat VRAM diagnostic: sample at most once every VramTickSeconds while the chat is open, so an idle
      // scene (no LLM responses to trigger the per-turn read) still leaves a VRAM trend in the log.
      private static DateTime _lastVramTick = DateTime.MinValue;
      private static CampaignTimeControlMode _previousTimeControlMode;
      private static bool _previousTimeControlModeLock;
      private static bool _timeControlModeSaved;

      /// <summary>
      ///   How the chat window is hosted. <see cref="ChatHostMode.Overlay" /> is the original design: a layer on
      ///   the live host screen — keeps the map visible behind, but grabs input (so the encyclopedia / ESC menu
      ///   are starved of focus) and, on the War Sails naval map, leaks render-textures (the VRAM crash).
      ///   <see cref="ChatHostMode.PushedScreen" /> pushes a real screen on the stack: the engine suspends the map
      ///   (no VRAM leak) and hands focus to whatever opens on top of us (native, no arbitration hack). Toggled at
      ///   runtime by cr.chat_screen for A/B comparison.
      /// </summary>
      public enum ChatHostMode
      {
         Overlay,
         PushedScreen
      }

      public static ChatViewModel ActiveViewModel { get; private set; }

      /// <summary>Active hosting mode. Defaults to the original Overlay so nothing changes until opted in.</summary>
      public static ChatHostMode HostMode { get; set; } = ChatHostMode.Overlay;

      public static bool IsOpen => _activeLayer != null;

      /// <summary>
      ///   Keeps the chat from starving any game screen that opens over it of input. The chat is a high-order
      ///   modal focus layer; without this, the alt-tab pause menu, the encyclopedia, or a native popup would
      ///   appear but not respond, because the chat holds an exclusive focus + input grab. Pumped every frame
      ///   from <see cref="ChatScrollablePanel" />'s late update (the reliable per-frame main-thread hook).
      ///   When the host screen has MORE layers than when the chat opened, something modal is up, so the chat
      ///   yields (IsFocusLayer off, input restrictions reset); once the layers return to the baseline, the
      ///   chat reclaims focus. Acts only on the transition, so it never fights a screen frame after frame.
      /// </summary>
      public static void ArbitrateFocus()
      {
         if (!IsOpen || _hostScreen == null) return;

         SampleVramPeriodically(); // a heartbeat VRAM read, so an idle (no-response) scene still traces its trend

         // Pushed-screen mode needs no focus juggling — the engine hands focus to whatever is pushed on top of us.
         if (HostMode == ChatHostMode.PushedScreen) return;

         try
         {
            int layers = _hostScreen.Layers?.Count ?? _baselineLayerCount;
            bool somethingModalOnTop = layers > _baselineLayerCount;

            if (somethingModalOnTop && _chatHoldsFocus)
            {
               _activeLayer.IsFocusLayer = false;
               _activeLayer.InputRestrictions.ResetInputRestrictions();
               // Slide the chat window off the right edge so the screen above it (the encyclopedia) is not
               // covered by the chat's high-order rendering. The layer stays added and ticking (its panel
               // keeps pumping LLM turns and running this arbiter), so nothing hangs; only the visual moves.
               ActiveViewModel?.SetVisualOffscreen(true);
               _chatHoldsFocus = false;
               ModLog.Info("Chat overlay yielded focus and slid aside for a screen above it (pause menu / encyclopedia / popup).");
               LogScreenState("yield"); // exactly when the new layer exists — identifies WHAT appeared and at what order
            }
            else if (!somethingModalOnTop && !_chatHoldsFocus)
            {
               _activeLayer.IsFocusLayer = true;
               _activeLayer.InputRestrictions.SetInputRestrictions();
               ActiveViewModel?.SetVisualOffscreen(false);
               ScreenManager.TrySetFocus(_activeLayer);
               _chatHoldsFocus = true;
               ModLog.Info("Chat overlay reclaimed focus and slid back (nothing above it now).");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"ArbitrateFocus failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

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
            SafeStep(() => ActiveViewModel?.FinalizeCaptorSceneMemory(), "FinalizeCaptorSceneMemory");
            SafeStep(() => ActiveViewModel?.FinalizeCompanionAudience(), "FinalizeCompanionAudience");
            SafeStep(() => ActiveViewModel?.FinalizeConversationMemory(), "FinalizeConversationMemory");
            SafeStep(() => ActiveViewModel?.NoteCompanionHeard(), "NoteCompanionHeard");
            SafeStep(() => ActiveViewModel?.FinalizeWitnessMemories(), "FinalizeWitnessMemories");

            // Free every character tableau (GPU render-target) before releasing the movie, so no portrait
            // survives the close regardless of engine teardown timing.
            SafeStep(() => ActiveViewModel?.ReleaseAllPortraits(), "ReleaseAllPortraits");
            // Turn the host map scene's rendering back on before we tear our layer down, so the map is live
            // again the instant the chat is gone.
            SafeStep(MapSceneSuspender.Resume, "ResumeMapScene");
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
            ModLog.Info($"[VRAM] close (after teardown) | {VramProbe.Describe()}");
         }
      }

      /// <summary>
      ///   Diagnostic: dumps the screen stack top and the host screen's layer list (type + order) to the log.
      ///   Used to find out where the encyclopedia actually lands when opened from a chat hosted on the map
      ///   (it displays fine over a mission host, but never appears over NavalMapScreen). Never throws.
      /// </summary>
      public static void LogScreenState(string tag)
      {
         try
         {
            var sb = new StringBuilder();
            sb.Append($"[SCREENS] {tag} | top={ScreenManager.TopScreen?.GetType().Name ?? "null"}");

            if (_hostScreen?.Layers != null)
            {
               sb.Append($" | host={_hostScreen.GetType().Name} layers[{_hostScreen.Layers.Count}]: ");
               foreach (ScreenLayer layer in _hostScreen.Layers)
               {
                  if (layer == null) continue;
                  sb.Append(layer.GetType().Name);
                  object order = ReadOrderForDiagnostics(layer);
                  if (order != null) sb.Append($"(o={order})");
                  if (ReferenceEquals(layer, _activeLayer)) sb.Append("[CHAT]");
                  sb.Append(' ');
               }
            }

            ModLog.Info(sb.ToString());
         }
         catch (Exception ex)
         {
            ModLog.Error($"LogScreenState failed: {ex.GetType().Name}: {ex.Message}");
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

         // Overlay mode only: stop the host map scene rendering behind our overlay (the naval-map VRAM fix).
         // Pushed-screen mode suspends the map by construction (the engine suspends the screen below), so this
         // reflection hack is neither needed nor wanted there.
         if (HostMode == ChatHostMode.Overlay)
            SafeStep(() => MapSceneSuspender.Suspend(_hostScreen), "SuspendMapScene");

         ModLog.Info($"Chat window opened on {_hostScreen.GetType().Name} with NPC '{npcName ?? "(none)"}'.");
         ModLog.Info($"[VRAM] open (baseline) | {VramProbe.Describe()}");
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
         CharacterObject portraitCharacter = null,
         Hero victimCompanionHero = null,
         bool playerCoerced = false)
      {
         Open(captorHero?.Name?.ToString() ?? captorProfile?.Name);

         if (ActiveViewModel == null) return;
         if (captorHero != null) ActiveViewModel.SetNpcHero(captorHero);
         else if (captorProfile != null) ActiveViewModel.SetNpcName(captorProfile.Name);
         if (captorHero == null && portraitCharacter != null)
            ActiveViewModel.SetNpcPortraitCharacter(portraitCharacter);

         // The victim companion is a REAL participant, not just a name in the prompt: add them as a witness
         // entry carrying their HeroStringId (so the chat resolves their portrait) and IsCaptiveVictim (so the
         // host never counts them as one of the captor's men). Their voice — cries, pleas, defiance — then comes
         // through the existing [WITNESS_REACTION] channel, under their own portrait. Their gender flows to the
         // prompt so the model stops borrowing the player's.

         var withVictim = witnesses != null
            ? new List<WitnessEntry>(witnesses)
            : new List<WitnessEntry>();
         string victimName = victimCompanionHero?.Name?.ToString();
         bool victimIsFemale = victimCompanionHero?.IsFemale ?? false;

         if (victimCompanionHero != null)
         {
            withVictim.Add(new WitnessEntry {
               Name = victimName ?? "Unknown",
               RelationToNpc = "the player's captive companion, bound and at your mercy",
               HeroStringId = victimCompanionHero.StringId,
               IsPlayerCompanion = true,
               IsCaptiveVictim = true
            });
            witnesses = withVictim;
         }

         if (witnesses is {Count: > 0}) ActiveViewModel.SetWitnesses(witnesses);

         // A faceless brigand captor has no Hero — that absence is exactly the bandit signal.
         bool captorIsBandit = captorHero == null;
         ActiveViewModel.ActivateCaptiveEncounterMode(captorProfile, isCollective, intent, captorIsBandit, victimName, playerCoerced, victimIsFemale);
      }

      /// <summary>
      ///   Opens the chat window for a PLAYER-AS-CAPTOR scene: the NPC is the player's prisoner and the player
      ///   drives. A normal live conversation framed so the NPC reacts as the captive. The player speaks first.
      /// </summary>
      public static void OpenForCaptorScene(
         Hero prisonerHero,
         NpcProfile prisonerProfile,
         CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation)
      {
         Open(prisonerHero?.Name?.ToString() ?? prisonerProfile?.Name);

         if (ActiveViewModel == null) return;
         if (prisonerHero != null) ActiveViewModel.SetNpcHero(prisonerHero);
         else if (prisonerProfile != null) ActiveViewModel.SetNpcName(prisonerProfile.Name);

         ActiveViewModel.ActivateCaptorSceneMode(prisonerProfile, intent);
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
      ///   Opens a companion AUDIENCE the companion themselves requested from a map popup ("Let us speak of it"):
      ///   a grievance, or a war-weary bid to retire. A normal conversation (full memory + actions) in which the
      ///   companion speaks FIRST and voices why they came. For a retirement, granting it in the talk fires the
      ///   companion's <c>retire</c> action; persuading them keeps them on.
      /// </summary>
      public static void OpenForCompanionAudience(Hero companion, NpcProfile profile, CompanionAudienceReason reason, bool landed)
      {
         if (companion == null || profile == null) return;

         Open(companion.Name?.ToString() ?? profile.Name);

         if (ActiveViewModel == null) return;

         ActiveViewModel.SetNpcHero(companion);
         ActiveViewModel.ActivateCompanionAudience(profile, reason, landed);
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
         _chatScreen = null;
         _timeControlModeSaved = false;
         _chatHoldsFocus = false;
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
            ChatLayerOrder);
         _activeLayer.InputRestrictions.SetInputRestrictions();

         _activeMovieId = _activeLayer.LoadMovie(PrefabName, ActiveViewModel);

         IGauntletMovie movie = _activeMovieId.Movie;

         ModLog.Info($"Movie type = {movie?.GetType().FullName}");
         ModLog.Info($"Movie loaded? {movie?.IsLoaded}");
         ModLog.Info($"RootWidget null? {movie?.RootWidget == null}");

         if (HostMode == ChatHostMode.PushedScreen)
         {
            // A real screen on the stack: the engine suspends the screen below (no naval-map VRAM leak) and hands
            // focus/input to any screen pushed on top of us (the encyclopedia, the ESC menu) natively — so no
            // overlay focus-arbitration is needed. The ChatScreen adds the layer and takes focus on initialize.
            _chatScreen = new ChatScreen(_activeLayer);
            ScreenManager.PushScreen(_chatScreen);
            ModLog.Info("Chat hosted as a pushed screen (map suspended, native focus).");
         }
         else
         {
            _hostScreen.AddLayer(_activeLayer);
            _activeLayer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_activeLayer);

            // Baseline for ArbitrateFocus: any later growth in the host screen's layer count means a game
            // screen (pause menu, encyclopedia, popup) opened over the chat, and the chat should yield to it.
            _baselineLayerCount = _hostScreen.Layers?.Count ?? 0;
            _chatHoldsFocus = true;
         }
      }

      private static void KillChatWindow()
      {
         if (_activeLayer == null) return;

         _activeLayer.IsFocusLayer = false;
         _activeLayer.InputRestrictions.ResetInputRestrictions();
         _activeLayer.ReleaseMovie(_activeMovieId);

         if (_chatScreen != null)
         {
            // Pushed-screen mode: drop our layer, then pop our screen so the engine finalizes it and returns to
            // the screen below. Only pop when we are actually the top screen, to never pop someone else's.
            _chatScreen.RemoveLayer(_activeLayer);
            if (ScreenManager.TopScreen == _chatScreen) ScreenManager.PopScreen();
            else ModLog.Info("KillChatWindow: chat screen is not on top — leaving the pop to the engine.");
         }
         else
         {
            _hostScreen?.RemoveLayer(_activeLayer);
         }
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

      /// <summary>Best-effort read of a layer's render order for the diagnostics dump (property name varies by build).</summary>
      private static object ReadOrderForDiagnostics(ScreenLayer layer)
      {
         foreach (string name in new[] {"Order", "SortOrder", "LocalOrder"})
         {
            try
            {
               var p = layer.GetType().GetProperty(name);

               if (p != null) return p.GetValue(layer);
            }
            catch
            {
               // diagnostic only
            }
         }

         return null;
      }

      private static void RestoreTime()
      {
         if (!_timeControlModeSaved || Campaign.Current == null) return;

         // Restore both the mode and the lock, so a forced state we interrupted (the ambush convoy) resumes.
         Campaign.Current.TimeControlMode = _previousTimeControlMode;
         Campaign.Current.SetTimeControlModeLock(_previousTimeControlModeLock);
      }

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
      ///   A throttled VRAM read pumped from the per-frame focus arbiter, so even a scene left idle (no LLM
      ///   response to trigger the per-turn sample) leaves a periodic [VRAM] trace — the clean way to see whether
      ///   the background map screen leaks over time with no interaction. Diagnostic only; never throws.
      /// </summary>
      private static void SampleVramPeriodically()
      {
         try
         {
            DateTime now = DateTime.UtcNow;

            if ((now - _lastVramTick).TotalSeconds < VramTickSeconds) return;
            _lastVramTick = now;
            ModLog.Info($"[VRAM] tick (idle heartbeat) | {VramProbe.Describe()}");
         }
         catch
         {
            // a diagnostic must never disturb the game
         }
      }

      #endregion
   }
}