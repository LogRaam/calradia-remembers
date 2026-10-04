// Code written by Gabriel Mailhot, 05/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Companions;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Witnesses;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
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
      /// <summary>The default (Classic) chat skin: the original prefab.</summary>
      public const string ClassicSkin = "CalradiaRemembersChat";
      /// <summary>The ornate two-page illuminated-manuscript skin (fixed-size book over Gabriel's page art).</summary>
      public const string ManuscriptSkin = "CalradiaRemembersChat_Manuscript";
      /// <summary>The bespoke 3-column COUNCIL window (left seat strip / centre chatroom / right engagements), a stretch layout so it never needs the Manuscript's zoom.</summary>
      public const string CouncilSkin = "CalradiaRemembersCouncil";

      // Set by OpenForCouncil/OpenForAssembly immediately before Open(), and consumed (reset to false) at the
      // very start of GenerateChatWindow's skin selection, so it can never leak into the NEXT conversation.
      private static bool _forceCouncilSkin;

      // Same lifecycle as _forceCouncilSkin: forces THIS council/assembly open onto a pushed screen (real
      // keyboard focus, no walkable mission needed) even while the global HostMode stays Overlay for every
      // other conversation. Consumed at the same spot in GenerateChatWindow.
      private static bool _forceCouncilPushedScreen;

      // True while the CURRENTLY open window is actually hosted as a pushed screen, whether that came from the
      // global HostMode or from a forced council/assembly. ArbitrateFocus reads THIS instead of HostMode, so a
      // forced-pushed council still skips the overlay's focus-arbitration/slide-aside logic even though HostMode
      // itself is unchanged. Reset when the window tears down.
      private static bool _activeIsPushed;

      // The Manuscript book is a FIXED-aspect design (940 units tall); GauntletUI has no per-widget zoom, so the
      // whole chat LAYER is scaled instead (UIContext.ScaleModifier), which multiplies sizes AND margins AND
      // offsets together, so the book and every overlay stay aligned.
      // CONSTANT, NOT multiplied by ScreenManager.Scale: a fixed ScaleModifier maps the 940-unit book onto the
      // 1080-unit reference height (1080/940), times the fill fraction, so the book fills the SAME fraction of
      // the screen at every resolution. Multiplying by ScreenManager.Scale made it resolution-DEPENDENT (fine at
      // one res, hugely overflowing at 2560x1440, player report Berserk160). The trade-off is that it overrides
      // the player's UI-Scale for the chat layer, the price of a consistent fit everywhere. TUNE the fill.
      private const float ManuscriptBookHeightUnits = 940f;
      private const float GauntletReferenceHeight = 1080f;
      private const float ManuscriptFill = 0.92f; // fraction of the screen height the book fills (leaves a margin)
      private const float ManuscriptZoom = GauntletReferenceHeight / ManuscriptBookHeightUnits * ManuscriptFill;

      // Council skin: same fixed-aspect scaling as Manuscript, tuned for cr_counsel.jpg (1683x935). The art fills
      // nearly the whole screen, so the fill fraction is higher than the Manuscript book's.
      private const float CouncilArtHeightUnits = 935f;
      private const float CouncilFill = 0.98f; // fraction of screen height the council book fills (TUNE)
      private const float CouncilZoom = GauntletReferenceHeight / CouncilArtHeightUnits * CouncilFill;

      /// <summary>
      ///   Dev override for the chat skin (set by cr.skin). Null means "use the MCM menu choice". A skin is just
      ///   another prefab bound to the SAME <see cref="ChatViewModel" />, so it changes only the look. Applies on
      ///   the next conversation (the movie is chosen at open).
      /// </summary>
      public static string SkinConsoleOverride;

      /// <summary>
      ///   The skin the CURRENTLY-open window was actually loaded with (MCM choice, console override, or Classic
      ///   forced for a captive scene). Behaviour that must branch on the live look (the Manuscript left page)
      ///   reads THIS, not the chosen skin, so a captive scene forced to Classic is judged as Classic.
      /// </summary>
      public static string ActiveSkin = ClassicSkin;
      private const double VramTickSeconds = 10.0;

      private static GauntletLayer _activeLayer;
      private static GauntletMovieIdentifier _activeMovieId;

      // Continuous focus arbitration. The chat is a high-order modal focus layer, so ANY game screen that
      // appears over it (the alt-tab pause menu, the encyclopedia, a native popup) would otherwise be starved
      // of input while the chat holds focus. Every frame (ArbitrateFocus, pumped from the chat panel) we
      // compare the host screen's layer count to the baseline taken when the chat opened: an extra layer means
      // something modal is up, so the chat yields its focus + input grab; back to baseline, the chat reclaims.
      private static bool _chatHoldsFocus;

      /// <summary>
      ///   The names of every layer already claiming focus on the host screen when the chat opened. That is the
      ///   BACKDROP the chat was deliberately drawn over (the settlement menu in a tavern, the world itself, the
      ///   vanilla conversation window), and the arbiter must never mistake any of it for a screen above us.
      ///   Gabriel's play-test, 2026-09-09: a tavern chat yielded to "MapMenuOverlay" and never appeared until
      ///   the player left the menu. Names rather than references, so a layer torn down and rebuilt under the
      ///   same name is still recognised as the backdrop it is.
      /// </summary>
      private static readonly HashSet<string> _backdropFocusClaimers = new(StringComparer.Ordinal);
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
      ///   Whether the recorded layer is still attached to the screen it was opened on. False means the screen
      ///   went away without our close ever running, which is what turns a single failure into a dead feature.
      ///   Fails SAFE: an unreadable screen counts as gone, so the worst case is one extra teardown, never a
      ///   session with no conversations in it.
      /// </summary>
      private static bool LayerIsOnHostScreen
      {
         get
         {
            try { return _activeLayer != null && _hostScreen?.Layers != null && _hostScreen.Layers.Contains(_activeLayer); }
            catch { return false; }
         }
      }

      /// <summary>
      ///   Whether the screen holding our layer is itself still alive. The engine FINALIZES a screen without
      ///   emptying its layer list, so a chat stranded on a mission that has since ended still reads as attached
      ///   (tashmetu, 2026-09-09: the overlay yielded, could never be closed, and every later conversation was
      ///   refused for the rest of the session). Fails SAFE the same way: unreadable counts as dead.
      /// </summary>
      private static bool HostScreenIsAlive
      {
         get
         {
            try { return _hostScreen != null && !_hostScreen.IsFinalized; }
            catch { return false; }
         }
      }

      /// <summary>
      ///   Session-launch reset, mirroring LetterWindowManager.ResetForSession: never inherit a window from a
      ///   prior session. Drops our own references WITHOUT touching the engine objects, which belong to a screen
      ///   that session or the load already tore down.
      /// </summary>
      /// <summary>
      ///   What a granted companion audience will change (the grievance count reset, a one-shot topic spent), kept
      ///   only when the audience is really on screen (EnjoyingHydra, 03/10/2026: accepted audiences that never opened
      ///   were spent anyway, and a companion left over a word he never got to say).
      /// </summary>
      internal static readonly AudiencePromiseLedger AudiencePromises = new AudiencePromiseLedger();

      public static void ResetForSession()
      {
         AudiencePromises.Clear();
         if (IsOpen) ModLog.Compat("ChatManager: a chat was still recorded as open at session launch; clearing it.");

         _activeLayer = null;
         _activeMovieId = null;
         ActiveViewModel = null;
         _hostScreen = null;
         _chatScreen = null;
         _timeControlModeSaved = false;
         _backdropFocusClaimers.Clear();
      }

      /// <summary>
      ///   Keeps the chat from starving any game screen that opens over it of input. The chat is a high-order
      ///   modal focus layer; without this, the alt-tab pause menu, the encyclopedia, or a native popup would
      ///   appear but not respond, because the chat holds an exclusive focus + input grab. Pumped every frame
      ///   from <see cref="ChatScrollablePanel" />'s late update (the reliable per-frame main-thread hook).
      ///   The decision itself lives in <see cref="ChatFocusArbitrationPolicy" /> and is made fresh from what is
      ///   on the screen NOW: it holds no memory of what the screen looked like when the chat opened, because
      ///   the layer COUNT it used to compare against a baseline cost a player every conversation in his session
      ///   (see that file's header). Acts only on the transition, so it never fights a screen frame after frame.
      /// </summary>
      /// <summary>
      ///   Escape, the key every player tries first, did nothing in a CR conversation: the close button was the only
      ///   way out (EmperorHaterius, 03/10/2026: "I can't get out of them"). Pumped every frame with the arbiter;
      ///   ChatEscapePolicy decides, outermost layer first, and only while the chat itself holds the keyboard.
      /// </summary>
      public static void HandleEscape()
      {
         if (!IsOpen || ActiveViewModel == null) return;

         try
         {
            if (!TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.Escape)) return;

            ChatViewModel vm = ActiveViewModel;
            ChatEscapeAction action = ChatEscapePolicy.Decide(new ChatEscapeFacts {
               ChatHasFocus = ReferenceEquals(ScreenManager.FocusedLayer, _activeLayer),
               PanelOpen = vm.IsAnySidePanelOpen,
               TypingWithText = ChatInputWidget.IsTyping && !string.IsNullOrWhiteSpace(vm.InputText)
            });

            ModLog.Info($"[CHAT] Escape: {action}");
            switch (action)
            {
               case ChatEscapeAction.ClosePanel: vm.CloseTopSidePanel(); break;
               case ChatEscapeAction.StopTyping: ChatInputWidget.StopTyping(); break;
               case ChatEscapeAction.CloseChat: vm.ExecuteClose(); break;
            }
         }
         catch (Exception ex) { ModLog.Error($"[CHAT] Escape handling failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      public static void ArbitrateFocus()
      {
         if (!IsOpen || _hostScreen == null) return;

         // Manuscript skin: zoom the whole chat layer so the fixed-aspect book AND all its overlays scale
         // together (kept aligned) and fill the same fraction of the screen at ANY resolution. A CONSTANT factor
         // (see ManuscriptZoom), NOT scaled by ScreenManager.Scale, further scaled by the player's MCM "Dark
         // Leather Size" factor (ModSettings.DarkLeatherScale, 80-140%, 100% = this constant unchanged) so a
         // high-resolution player can size the book to taste. Re-applied every frame because
         // GauntletLayer.UpdateLayout resets ScaleModifier to the base UI scale each pass. Classic is untouched.
         if (ActiveSkin == ManuscriptSkin && _activeLayer?.UIContext != null)
            _activeLayer.UIContext.ScaleModifier = ManuscriptZoom * ModSettings.DarkLeatherScale;

         // Council skin (2026-08-23): the same fixed-aspect treatment as Manuscript, now that its background is
         // Gabriel's full-book leather art (cr_counsel.jpg, 1683x935) with every control laid over the painted
         // slots. Its own zoom, because the art fills more of the screen than the Manuscript book does. Scaled by
         // the SEPARATE "Council Size" setting (ModSettings.CouncilScale), NOT the chat's Dark Leather size: the
         // council book is far wider, so on a 1440p+ screen it overflowed at the same percent the chat fit at
         // (player report, 2026-08-25). The two window sizes are now dialled independently.
         if (ActiveSkin == CouncilSkin && _activeLayer?.UIContext != null)
            _activeLayer.UIContext.ScaleModifier = CouncilZoom * ModSettings.CouncilScale;

         SampleVramPeriodically(); // a heartbeat VRAM read, so an idle (no-response) scene still traces its trend

         // Pushed-screen mode needs no focus juggling — the engine hands focus to whatever is pushed on top of us.
         if (_activeIsPushed) return;

         try
         {
            bool hostIsOnTop = ReferenceEquals(ScreenManager.TopScreen, _hostScreen);
            List<OverlayNeighbour> neighbours = hostIsOnTop ? NeighbouringLayers() : null;
            bool somethingModalOnTop =
               ChatFocusArbitrationPolicy.Decide(hostIsOnTop, neighbours, _backdropFocusClaimers) == ChatFocusVerdict.Yield;

            if (somethingModalOnTop && _chatHoldsFocus)
            {
               _activeLayer.IsFocusLayer = false;
               _activeLayer.InputRestrictions.ResetInputRestrictions();
               // Slide the chat window off the right edge so the screen above it (the encyclopedia) is not
               // covered by the chat's high-order rendering. The layer stays added and ticking (its panel
               // keeps pumping LLM turns and running this arbiter), so nothing hangs; only the visual moves.
               ActiveViewModel?.SetVisualOffscreen(true);
               _chatHoldsFocus = false;
               ModLog.Info("Chat overlay yielded focus and slid aside for: "
                           + ChatFocusArbitrationPolicy.ReasonFor(hostIsOnTop, neighbours, _backdropFocusClaimers));
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

      /// <summary>
      ///   The host screen's layers as the arbiter needs to see them: a name, whether it is ours, whether it is
      ///   on its way out, and whether it means to take the input. Reading IsFocusLayer rather than counting is
      ///   the whole fix; verified against the game's binaries, no always-on mission HUD layer sets it, and the
      ///   only non-modals that do are the two vanilla conversation windows this chat is drawn over.
      /// </summary>
      private static List<OverlayNeighbour> NeighbouringLayers() => LayerFactsFor(_hostScreen, _activeLayer);

      /// <summary>
      ///   Snapshots which layers are already claiming focus on the host screen, at the moment the chat is added
      ///   to it. Everything named here is scenery for this conversation. Cleared by <see cref="FreeResources" />
      ///   and <see cref="ResetForSession" /> so one conversation's scenery never excuses the next one's modal.
      /// </summary>
      private static void RecordBackdropFocusClaimers()
      {
         _backdropFocusClaimers.Clear();

         foreach (OverlayNeighbour layer in NeighbouringLayers())
         {
            if (layer == null || layer.IsTheChat || !layer.WantsFocus) continue;
            if (string.IsNullOrEmpty(layer.Name)) continue;

            _backdropFocusClaimers.Add(layer.Name);
         }

         ModLog.Info("Chat overlay backdrop (already holding focus when it opened): "
                     + (_backdropFocusClaimers.Count == 0 ? "none" : string.Join(", ", _backdropFocusClaimers)));
      }

      /// <summary>
      ///   The same reading, against any screen, so cr.testall's chat_focus_arbiter can drive it over REAL engine
      ///   layers instead of a parallel copy. That matters for one assumption a pure test cannot check: that
      ///   ScreenLayer.Name really does hand back the name a GauntletLayer was built with, which is the whole
      ///   basis for telling vanilla's conversation window from a modal.
      /// </summary>
      internal static List<OverlayNeighbour> LayerFactsFor(ScreenBase screen, ScreenLayer chatLayer)
      {
         var facts = new List<OverlayNeighbour>();

         try
         {
            if (screen?.Layers == null) return facts;

            foreach (ScreenLayer layer in screen.Layers)
            {
               if (layer == null) continue;

               facts.Add(new OverlayNeighbour {
                  Name = layer.Name,
                  IsTheChat = chatLayer != null && ReferenceEquals(layer, chatLayer),
                  // The 3D world beneath us, which claims focus from its own constructor on every screen that
                  // has a scene. By TYPE, not by name: mistaking it for a modal costs every conversation.
                  IsTheWorldBeneath = layer is SceneLayer,
                  IsFinalized = layer.IsFinalized,
                  WantsFocus = layer.IsFocusLayer
               });
            }
         }
         catch (Exception ex)
         {
            // An unreadable screen must not decide anything: an empty list holds focus, which is the state the
            // chat is already in, rather than a yield nothing would ever undo.
            ModLog.Error($"LayerFactsFor failed: {ex.GetType().Name}: {ex.Message}");
         }

         return facts;
      }

      public static void Close()
      {
         if (!IsOpen) return;

         // Declared OUTSIDE the try because the finally reads it: a hand-off and an ordinary close say
         // different things about where the player just went.
         var handingOff = false;

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
            // Companion-briefing pillar: writes the private word into the memory of everyone who stood in
            // it. A no-op on any close that is not the briefing chat's OWN. (This comment described a
            // standing-brief STORE that was dismantled on 13/09/2026 and said the opposite of what the code
            // now does - the memory is written BY this close, not swept by it.)
            SafeStep(() => ActiveViewModel?.FinalizeCompanionBriefs(), "FinalizeCompanionBriefs");

            // A briefing closing means "take me back to the meeting I asked leave from"; anything else
            // closing means whatever return was pending is stale and must not keep encounters alive behind
            // our back.
            SafeStep(() => {
               handingOff = ActiveViewModel?.IsBriefingChat == true;

               if (handingOff) CompanionBriefingCoordinator.ArmReturn();
               else CompanionBriefingCoordinator.Forget();
            }, "ArmBriefingReturn");
            SafeStep(() => ActiveViewModel?.FinalizeConversationMemory(), "FinalizeConversationMemory");
            SafeStep(() => ActiveViewModel?.NoteCompanionHeard(), "NoteCompanionHeard");
            SafeStep(() => ActiveViewModel?.FinalizeWitnessMemories(), "FinalizeWitnessMemories");
            // Now that this conversation's memory is recorded, fold the NPC's history back to a compact form so it
            // stays fresh for their next chat and for when they appear as a witness (2026-08-23), instead of only
            // the once-a-day pass. Internally gated (threshold + MCM + not busy), so most closes are a no-op.
            SafeStep(() => ActiveViewModel?.CompressMemoryOnClose(), "CompressMemoryOnClose");
            // The council's LIFT: re-validates and executes every surviving [RESOLUTION] against the world as
            // it stands NOW. A no-op outside a council (or one with nothing agreed), see FinalizeCouncilResolutions.
            SafeStep(() => ActiveViewModel?.FinalizeCouncilResolutions(), "FinalizeCouncilResolutions");
            // Extension Surface: hand the attributed transcript to any third-party listener as the conversation
            // closes. Independent of the memory finalizers above so a listener sees even a slight conversation.
            SafeStep(() => ActiveViewModel?.RaiseConversationEndedEvent(), "RaiseConversationEndedEvent");
            // Keep the words themselves, so a conversation the player stepped out of can be stepped back into
            // (fkasad, 20/09/2026). AFTER FinalizeConversationMemory, because what is kept carries the memory
            // event that close just wrote: a resumed sitting comes back to that same event instead of leaving
            // the NPC remembering one afternoon as two.
            SafeStep(() => ActiveViewModel?.KeepConversationForResume(), "KeepConversationForResume");

            // Free every character tableau (GPU render-target) before releasing the movie, so no portrait
            // survives the close regardless of engine teardown timing.
            SafeStep(() => ActiveViewModel?.ReleaseAllPortraits(), "ReleaseAllPortraits");
            // Turn the host map scene's rendering back on before we tear our layer down, so the map is live
            // again the instant the chat is gone.
            // ── THE TWO CLOSES, walked as a plan ───────────────────────────────────────────────────────
            // Gabriel's reading, 2026-09-13, and it is the right shape: "End Conversation" has two jobs, and
            // the button is composed for the situation. An ordinary close ENDS things - it gives the world
            // back, ends the conversation, finishes the encounter and lets the clock run. A briefing's close
            // HANDS OFF: the window goes, another opens on the meeting it was nested inside, and nothing about
            // the world should move in between.
            //
            // Everything above this line runs either way - the private word is remembered whichever it is.
            // Everything below is the world being given back, and a hand-off must not give it back only to
            // take it again a frame later. Resuming the map scene, restoring the clock, ending the encounter
            // and ending the conversation are all things the very next open would immediately undo, and the
            // churn between them is where two days of loading screens and one native crash lived.
            SafeStep(KillChatWindow, "KillChatWindow");

            // The steps after the window are DATA now (ChatClosePlan), so a unit test can read which of them
            // a hand-off skips instead of somebody having to trust a comment. See BriefingReturnDecision for
            // why the shape of this sequence, rather than any step in it, is what kept going wrong.
            ChatCloseKind kind = handingOff ? ChatCloseKind.HandOff : ChatCloseKind.Ordinary;
            IReadOnlyList<ChatCloseStep> plan = ChatClosePlan.For(kind);

            // THE PLAN IS SAID OUT LOUD, and this matters as much as making it data. SafeStep logs only
            // FAILURES, so a step correctly skipped left no trace whatever - which is exactly the class of
            // fault that cost three attempts here (a missing step, an inverted guard, a step that should not
            // have existed). All three were diagnosed from logs. Now the log carries the same vocabulary the
            // unit test asserts in, so a line from a player's log can be read straight against ChatClosePlan.
            ModLog.Info($"[CLOSE] plan={kind}: {string.Join(" > ", plan)}");

            foreach (ChatCloseStep step in plan) RunCloseStep(step);

            if (handingOff)
               ModLog.Info("[BRIEFING] window closed as a HAND-OFF: the meeting stands, the world is untouched.");
         }
         finally
         {
            FreeResources();
            SafeStep(LlmServices.ClearTestGenderOverride, "ClearTestGenderOverride");
            ModLog.Info(handingOff
               ? "Chat window closed — handing off to the meeting it was nested inside."
               : "Chat window closed — returned to campaign map.");
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

      public static void Open(string npcName = null, bool forceClassicSkin = false, ChatOpening opening = ChatOpening.Staged)
      {
         // "Already open" must mean a window that is actually THERE. A layer recorded against a screen that has
         // since gone is a ghost, and refusing on its account cost a player every conversation for the rest of
         // his session (Nexus report 2026-09-08: a companion approached in a village, no window came, and from
         // then on "no CR chat windows with anyone work unless I fully quit and restart the game").
         if (ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = IsOpen, LayerStillOnHostScreen = LayerIsOnHostScreen, HostScreenAlive = HostScreenIsAlive}))
         {
            ModLog.Info("ChatManager.Open called but chat is already open.");

            return;
         }

         if (ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = IsOpen, LayerStillOnHostScreen = LayerIsOnHostScreen, HostScreenAlive = HostScreenIsAlive}))
         {
            // Always recorded, even with logging off: this is the fingerprint of the bug above, and the next
            // report of it should arrive with the evidence already in hand.
            ModLog.Compat("ChatManager.Open: a chat layer was recorded but is no longer on its host screen. "
                          + "Clearing the stale state and opening anew, rather than refusing every conversation until restart.");
            SafeStep(RestoreTime, "RestoreTime (ghost cleanup)"); // a ghosted open may have left the clock paused
            FreeResources();
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
            GenerateChatWindow(npcName, forceClassicSkin);
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

         // Overlay-hosted windows only: stop the host map scene rendering behind our overlay (the naval-map VRAM
         // fix). A pushed screen (global HostMode, or a forced council/assembly) suspends the map by
         // construction (the engine suspends the screen below), so this reflection hack is neither needed nor
         // wanted there. Reads _activeIsPushed (set inside GenerateChatWindow above), not the global HostMode,
         // so a forced-pushed council is correctly skipped even while HostMode itself stays Overlay.
         if (!_activeIsPushed)
            SafeStep(() => MapSceneSuspender.Suspend(_hostScreen), "SuspendMapScene");

         // Every open consumes what the last close kept; only an ordinary conversation puts it back on the
         // screen (fkasad, 20/09/2026: "deleted as soon as you open a new chat"). Here, not in
         // GenerateChatWindow, because the staged openers all call Open FIRST and then stage themselves on top:
         // a resumed exchange must be discarded before ActivatePostDuel or a captive scene writes its own.
         SafeStep(() => ActiveViewModel?.ResumeKeptConversation(opening), "ResumeKeptConversation");

         ModLog.Info($"Chat window opened on {_hostScreen.GetType().Name} with NPC '{npcName ?? "(none)"}'.");
         ModLog.Info($"[VRAM] open (baseline) | {VramProbe.Describe()}");
      }

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): re-reads any changed prefab XML from the DEPLOYED module folder
      ///   (WidgetFactory.CheckForUpdates compares each live prefab's file write-time and reloads it), then, if
      ///   the chat is open, rebuilds its widget tree in place by releasing and reloading the movie against the
      ///   SAME view-model, so the conversation, its history, and the panel state all survive. Lets a UI author
      ///   iterate on the chat/memories prefabs without quitting the game. Edit the file under the game's own
      ///   Modules/CalradiaRemembers/GUI/Prefabs (a dotnet build overwrites it from source), not the repo copy.
      /// </summary>
      public static void ReloadActiveMovie()
      {
         try
         {
            UIResourceManager.WidgetFactory?.CheckForUpdates();

            if (_activeLayer == null || _activeMovieId == null || ActiveViewModel == null)
            {
               ModLog.Info("[RELOAD_UI] prefabs re-read; no chat open to rebuild (reopen it to see changes).");

               return;
            }

            _activeLayer.ReleaseMovie(_activeMovieId);
            _activeMovieId = _activeLayer.LoadMovie(ActiveSkin, ActiveViewModel);
            ModLog.Info($"[RELOAD_UI] reloaded prefabs and rebuilt the '{ActiveSkin}' movie in place.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELOAD_UI] failed: {ex.GetType().Name}: {ex.Message}");
         }
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
         bool playerCoerced = false,
         string openingOverride = null)
      {
         // ONE escape attempt per captivity event. A FRESH scene (no opening override) grants the player a new
         // chance; the scene RESUMED after a lost escape carries an override and is the SAME event, so it must
         // NOT hand back another attempt (that is what let the fight loop).
         if (string.IsNullOrEmpty(openingOverride)) Duels.CaptiveEscapeCoordinator.NoteNewCaptiveScene();

         Open(captorHero?.Name?.ToString() ?? captorProfile?.Name, forceClassicSkin: true);

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
               IsCaptiveVictim = true,
               IsFemale = victimIsFemale
            });
            witnesses = withVictim;
         }

         if (witnesses is {Count: > 0}) ActiveViewModel.SetWitnesses(witnesses);

         // What the captor IS, not whether we happen to hold a Hero for them. Absence of a Hero was the
         // original signal, and it broke the moment the Nemesis pillar began MINTING a persistent Hero for a
         // bandit boss: a named brigand chief then took the lord path, so the prompt handed him the player's
         // name, house and station (Gabriel, in play 2026-07-19: a deserter chief greeted a first-time captive
         // by name). A bandit is a bandit whether or not his band's leader has a name.
         bool captorIsBandit = captorHero == null || captorHero.MapFaction?.IsBanditFaction == true;
         ActiveViewModel.ActivateCaptiveEncounterMode(captorProfile, isCollective, intent, captorIsBandit, victimName, playerCoerced, victimIsFemale, openingOverride);
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
         Open(prisonerHero?.Name?.ToString() ?? prisonerProfile?.Name, forceClassicSkin: true);

         if (ActiveViewModel == null) return;
         if (prisonerHero != null) ActiveViewModel.SetNpcHero(prisonerHero);
         else if (prisonerProfile != null) ActiveViewModel.SetNpcName(prisonerProfile.Name);

         ActiveViewModel.ActivateCaptorSceneMode(prisonerProfile, intent);
      }

      /// <summary>
      ///   Opens the conversation that RESUMES right after a fought duel. The opponent (alive, duels are
      ///   non-lethal) speaks FIRST, coloured by who prevailed (<paramref name="playerWon" />), and the chat
      ///   replays <paramref name="priorConversation" /> so the meeting reads as one continuous exchange rather
      ///   than a fresh talk. A normal conversation otherwise (full memory + actions): a player who WON may press
      ///   the loser for a favour (an oath), a player who LOST hears the victor out. <paramref name="courtedHero" />
      ///   is only meaningful for a courtship duel. Staged over a live mission by <see cref="ChatSceneStager" />,
      ///   so this leaf is reached with a scene already up.
      /// </summary>
      public static void OpenForPostDuel(
         Hero opponent, NpcProfile profile, bool playerWon, DuelReason reason, Hero courtedHero, ConversationTranscript priorConversation)
      {
         if (opponent == null) return;

         Open(opponent.Name?.ToString() ?? profile?.Name);

         if (ActiveViewModel == null) return;

         ActiveViewModel.SetNpcHero(opponent);
         ActiveViewModel.ActivatePostDuel(profile, playerWon, reason, courtedHero, priorConversation);
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
      ///   a grievance, a war-weary bid to retire, or (since the found-topic audiences) a mission report / party
      ///   conflict / practical counsel / callback / rumour. A normal conversation (full memory + actions) in
      ///   which the companion speaks FIRST and voices why they came. For a retirement, granting it in the talk
      ///   fires the companion's <c>retire</c> action; persuading them keeps them on. <paramref name="topicDetail" />
      ///   is the SPECIFIC fact the mod found (the actual rumour/memory/grudge/party condition) for the found-topic
      ///   reasons; null for the original two reasons, which need none.
      /// </summary>
      public static void OpenForCompanionAudience(
         Hero companion, NpcProfile profile, CompanionAudienceReason reason, bool landed, string topicDetail = null)
      {
         if (companion == null || profile == null)
         {
            ModLog.Error($"[AUDIENCE] cannot open: companion={companion?.Name?.ToString() ?? "null"}, "
                       + $"profile={(profile == null ? "null" : "ok")}. The popup was answered and nothing followed.");
            ModLog.Notice("a companion asked for a private word and the mod could not open it. "
                        + "They keep the grievance; they will ask again.");
            AudiencePromises.Break(companion?.StringId);

            return;
         }

         // Route through the single door, which decides staging vs direct: over the SUSPENDED map an overlay
         // sub-panel that hides itself (Memories/Compose set IsVisible=false) left ghost pixels, because nothing
         // repaints the region behind it, so the audience must open over a rendered mission with the companion
         // present. When a scene is ALREADY live, the door opens directly over it (already clean); when none is,
         // it stages one; the no-conversation-manager edge falls through to the direct open inside the door.
         ChatSceneStager.StageCompanionAudience(companion, profile, reason, landed, topicDetail);
      }

      /// <summary>
      ///   The raw companion-audience open: adds the overlay over whatever the current top screen is, with no
      ///   scene staging. <see cref="OpenForCompanionAudience" /> is the normal entry point (it stages a scene
      ///   first when none is live); this is what <see cref="ChatSceneStager" /> calls once its scene is up,
      ///   and the choke's fallback when a scene is already live.
      /// </summary>
      /// <summary>
      ///   Prisoner exchange, increment 3 (fkasad, 25/09/2026; Gabriel: an anonymous envoy announced by a popup): opens
      ///   the envoy's audience. The chat has NO hero (the envoy is nobody's profile); it shows the envoy's name and
      ///   <paramref name="portrait" />, a troop of the lord's own people, and the lord stays behind the scenes in
      ///   <paramref name="envoy" /> for the deal. The DIRECT open: the staging path calls this once the scene is up.
      /// </summary>
      public static void OpenForEnvoyAudienceDirect(string envoyName, CharacterObject portrait, EnvoyAudience envoy)
      {
         if (envoy == null || string.IsNullOrWhiteSpace(envoyName))
         {
            ModLog.Error("[ENVOY] direct open refused: no envoy.");

            return;
         }

         Open(envoyName);

         if (ActiveViewModel == null)
         {
            ModLog.Notice($"{envoyName} could not be received. If it happens again, cr.chat_reset clears a stuck window.");

            return;
         }

         ActiveViewModel.SetNpcName(envoyName);
         if (portrait != null) ActiveViewModel.SetNpcPortraitCharacter(portrait);
         ActiveViewModel.ActivateEnvoyAudience(envoy);
      }

      /// <summary>
      ///   An exploration scene (authored quests, Gabriel 03/10/2026): the chat opens on a narrator and no character.
      ///   Opened directly, like the envoy's audience; the session supplies the prompt and what the markers do.
      /// </summary>
      public static void OpenForExploration(ExplorationSession session)
      {
         if (session == null) return;

         Open(session.Title);
         if (ActiveViewModel == null)
         {
            ModLog.Notice($"the scene \"{session.Title}\" could not be opened. If it happens again, cr.chat_reset clears a stuck window.");
            session.OnClose?.Invoke();

            return;
         }

         ActiveViewModel.SetNpcName(session.Title);
         ActiveViewModel.ActivateExploration(session);
      }

      public static void OpenForCompanionAudienceDirect(
         Hero companion, NpcProfile profile, CompanionAudienceReason reason, bool landed, string topicDetail = null)
      {
         if (companion == null || profile == null)
         {
            ModLog.Error("[AUDIENCE] direct open refused: no companion or no profile.");
            AudiencePromises.Break(companion?.StringId);

            return;
         }

         Open(companion.Name?.ToString() ?? profile.Name);

         // The END of the road for a player who answered the popup and saw nothing. Open() failing leaves no
         // window and left no trace either, so the report reads "nothing happens" and the log agrees with it.
         if (ActiveViewModel == null)
         {
            ModLog.Error($"[AUDIENCE] the window did not open for {companion.Name}; their private word is lost. "
                       + "Try cr.chat_reset if later conversations also refuse to open.");
            // The end of the road, and until now it ended out of sight: Error is gated by the MCM logging
            // switch, which is off by default, so the player who answered the popup was told nothing and the
            // log they sent said nothing either. Say it where it cannot be switched off.
            ModLog.Notice($"{companion.Name}'s private word could not be opened; they will ask again. If it happens "
                        + "again, cr.chat_reset clears a stuck window.");
            AudiencePromises.Break(companion.StringId);

            return;
         }

         ActiveViewModel.SetNpcHero(companion);
         ActiveViewModel.ActivateCompanionAudience(profile, reason, landed, topicDetail);

         // On screen at last: only now does the audience count as given.
         int kept = AudiencePromises.Keep(companion.StringId);
         if (kept > 0) ModLog.Info($"[AUDIENCE] {companion.Name}'s audience is open; {kept} promise(s) kept.");
      }

      /// <summary>
      ///   Companion-briefing pillar (2026-09-07): opens the PRIVATE chat the player asked for in the vanilla
      ///   dialogue ("grant me a moment to speak with my own people"), seating only the <paramref name="chosen" />
      ///   roster, never <paramref name="target" /> (the whole design property: this is a SEPARATE conversation
      ///   the target was never part of, so there is nothing to filter and nothing to hide). Picks an anchor by
      ///   the same regard-based rule OpenForCouncil uses when more than one companion was chosen; a lone
      ///   companion is simply their own anchor. <paramref name="target" /> rides through only so the closed
      ///   chat can tell <see cref="ChatViewModel.FinalizeCompanionBriefs" /> this was the briefing chat itself,
      ///   never so it can be shown to anyone in this conversation.
      /// </summary>
      public static void OpenForCompanionBriefing(Hero target, List<Hero> chosen)
      {
         if (target == null || chosen == null || chosen.Count == 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior == null) return;

         // A companion never yet spoken with in the ordinary chat has no profile yet: ensure one exists for
         // every chosen hero before the window opens, mirroring OpenForAssembly's own SeatedByStatus guard.
         foreach (Hero h in chosen)
            if (behavior.Store.Get(h.StringId) == null)
               behavior.Store.Set(HeroProfileMapper.FromHero(h));

         Hero anchor = chosen[0];
         if (chosen.Count > 1)
         {
            var facts = chosen
               .Select(h => new CouncilSeatFacts(h.StringId, behavior.Store.Get(h.StringId)?.ReputationWithPlayer ?? 0))
               .ToList();
            int anchorIndex = CouncilAnchorPolicy.SelectAnchor(facts);
            if (anchorIndex >= 0) anchor = chosen[anchorIndex];
         }

         Open(anchor.Name?.ToString());

         // The window failed to open. The conversation this briefing interrupted is now IDLING behind
         // nothing at all, so it has to be ended here or the player is left in a dialogue with no replies.
         if (ActiveViewModel == null)
         {
            ModLog.Error("[BRIEFING] the window did not open; ending the meeting rather than leaving it hung.");
            CompanionBriefingCoordinator.AbandonBriefingFromHost();

            return;
         }

         ActiveViewModel.SetNpcHero(anchor);
         foreach (Hero h in chosen.Where(h => h != anchor))
            ActiveViewModel.AddWitness(new WitnessEntry {
               Name = h.Name?.ToString(),
               HeroStringId = h.StringId,
               IsPlayerCompanion = true,
               RelationToNpc = "a fellow companion, drawn aside for this private word",
               IsFemale = h.IsFemale
            });

         ActiveViewModel.ActivateCompanionBriefing(target);
         ModLog.Info($"[BRIEFING] opened with {chosen.Count} companion(s), anchor {anchor.Name}, for the conversation with {target.Name}.");
      }

      /// <summary>
      ///   Opens a COUNCIL of the player's own house: seats everyone the scope gathers, gives the spoken reply
      ///   to the member who regards the player best (<see cref="CouncilAnchorPolicy" />), and seats the rest as
      ///   present members who may speak or merely gesture on each turn. Returns a line describing what was
      ///   opened, or why it could not be, so a caller can report it to the player.
      ///   <paramref name="allowInScene" /> bypasses the InScene refusal: a settlement-hosted council (Phase 1
      ///   scene staging) deliberately opens AFTER its own Location mission has started, so Mission.Current is
      ///   non-null by design there, unlike the ordinary map-overlay path (cr.council), which must still refuse
      ///   inside somebody else's scene. <paramref name="parleyTargetFactionId" /> is the Parley's own extra
      ///   parameter (null for every other scope): the StringId of the specific enemy faction this parley was
      ///   convened against, carried into the roster (<see cref="CouncilAttendanceResolver.SeatedInPersonHeroes" />,
      ///   still <see cref="CouncilRosterResolver.Seated" /> itself for Parley) and the view-model
      ///   (ActivateCouncil), which in turn hands it to the save-safe session behavior.
      /// </summary>
      public static string OpenForCouncil(CouncilScope scope, bool allowInScene = false, string parleyTargetFactionId = null)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return "No campaign is running.";

         string blocked = CouncilOpenBlockedReason(allowInScene, scope == CouncilScope.Trial);
         if (blocked != null) return blocked;

         List<Hero> seated = CouncilAttendanceResolver.SeatedInPersonHeroes(scope, parleyTargetFactionId);

         if (seated.Count == 0)
            return $"Nobody is seated for a {scope} council right now (nobody available, or nobody of that house).";

         // EVERY SEAT GETS A PERSISTED PROFILE BEFORE ANYTHING READS ONE. OpenForAssembly has done this since it
         // was written (SeatedByStatus) and so has the companion briefing; the council was the one door that
         // never did, and it seats people the player may never have spoken to alone.
         //
         // fkasad (Nexus, 18/09/2026) found the visible half: "The send letter is showing the list of the
         // people you can sent a letter to... But not those you've talked to during a council." That list is
         // simply store.ListIds(), so somebody with no profile is somebody you have never met.
         //
         // The invisible half is worse, and it is why this is a fix rather than a courtesy. Both council write
         // paths read the profile and give up when it is missing: the engagement seal (ChatViewModel.Council,
         // "if (profile == null) continue") and the regard shift the sitting awards. So a councillor without a
         // profile kept NOTHING from a council he sat through: no memory of it, no agreement sealed on him, and
         // no change in how he regards the player. He would arrive at the next sitting as a stranger again.
         foreach (Hero h in seated)
            if (h != null && behavior.Store.Get(h.StringId) == null)
            {
               behavior.Store.Set(HeroProfileMapper.FromHero(h));
               ModLog.Info($"[COUNCIL] seated {h.Name} for the first time; a memory profile was created for them.");
            }

         // The anchor is chosen on the mod's OWN personal regard ledger, the one the player actually builds in
         // conversation, not the vanilla clan relation.
         var facts = seated
                    .Select(h => new CouncilSeatFacts(h.StringId, behavior.Store.Get(h.StringId)?.ReputationWithPlayer ?? 0))
                    .ToList();
         // A trial is presided by the liege, the bench's first seat; every other council picks its anchor by regard.
         int anchorIndex = scope == CouncilScope.Trial ? 0 : CouncilAnchorPolicy.SelectAnchor(facts);

         if (anchorIndex < 0) return "Could not seat an anchor for the council.";
         Hero anchor = seated[anchorIndex];

         // Overlay, not a pushed screen: staged (CouncilConversationStager) or not, the council opens exactly
         // like OpenForCaptiveEncounter (a layer over whatever the current top screen is). Staged, that top
         // screen is the anchor's own conversation MissionScreen (map suspended, real keyboard focus already
         // provided by the mission); unstaged (the rare no-conversation-partner edge), it is the plain map,
         // the same overlay-on-map tolerance every other conversation accepts.
         _forceCouncilSkin = true;
         Open(anchor.Name?.ToString());

         if (ActiveViewModel == null) return "The chat window could not be opened.";
         ActiveViewModel.SetNpcHero(anchor);

         foreach (Hero member in seated.Where(h => h != anchor))
            ActiveViewModel.AddWitness(new WitnessEntry {
               Name = member.Name?.ToString(),
               HeroStringId = member.StringId,
               IsPlayerCompanion = member.IsPlayerCompanion,
               RelationToNpc = DescribeCouncilSeat(scope),
               Persona = DescribeCouncilMemberPersona(member, behavior),
               PresenceStatus = CouncilRosterResolver.DescribePresence(member),
               IsFemale = member.IsFemale
            });

         ActiveViewModel.ActivateCouncil(scope, parleyTargetFactionId);
         ModLog.Info($"[COUNCIL] opened {scope} with {seated.Count} seated, anchor {anchor.Name}.");

         return $"Council ({scope}) opened with {seated.Count} seated. {anchor.Name} speaks for the table; "
              + "the others may speak, as the narrator gives them the floor.";
      }

      /// <summary>
      ///   Council narrator model (2026-08-02): a short persona + regard descriptor for a SEATED MEMBER other
      ///   than the anchor, so the narrator's single call can voice every member's own trait AND their own
      ///   standing with the player, not just a bare seat label. Reuses <see cref="WitnessResolver.ResolvePersona" />
      ///   (the same archetype label an ordinary witness carries) and the canonical <see cref="RegardBands" />
      ///   wording (the same one the encyclopedia's "Personal regard" row shows), so nothing here can disagree
      ///   with what the player could already look up. Never throws; a missing profile/persona degrades to the
      ///   regard clause alone.
      /// </summary>
      private static string DescribeCouncilMemberPersona(Hero member, NpcMemoryCampaignBehavior behavior)
      {
         try
         {
            string persona = WitnessResolver.ResolvePersona(member, behavior?.Store);
            int regard = behavior?.Store?.Get(member.StringId)?.ReputationWithPlayer ?? 0;
            string regardPhrase = $"regards you with {RegardBands.Describe(regard)}";

            return string.IsNullOrWhiteSpace(persona)
               ? regardPhrase
               : $"{persona}, {regardPhrase}";
         }
         catch (Exception ex)
         {
            ModLog.Error($"DescribeCouncilMemberPersona failed for {member?.Name}: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Council audit M1/M2: the player-facing reason convening is refused right now, or null when it is
      ///   safe. Maps live engine state to <see cref="CouncilAccessPolicy" /> so the rules stay unit-tested:
      ///   an already-open chat would otherwise be hijacked (the old conversation reassigned an anchor and
      ///   witnesses), and a council makes no sense from inside captivity or (for the map-overlay path) a
      ///   mission scene. <paramref name="allowInScene" /> is true only for the settlement-hosted council,
      ///   which is opened FROM inside its own Location mission on purpose.
      /// </summary>
      private static string CouncilOpenBlockedReason(bool allowInScene, bool isTrial = false)
      {
         bool inScene = !allowInScene && Mission.Current != null;

         switch (CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = IsOpen, PlayerIsPrisoner = Hero.MainHero?.IsPrisoner == true, InMissionScene = inScene, IsTrial = isTrial}))
         {
            case CouncilAccessBlock.ChatAlreadyOpen: return "A conversation is already open. Close it before convening.";
            case CouncilAccessBlock.PlayerCaptive:   return "You cannot convene while you are held captive.";
            case CouncilAccessBlock.InScene:         return "You cannot convene in the middle of a scene.";
            default:                                 return null;
         }
      }

      /// <summary>
      ///   The chat-open / captive refusal ONLY (never the scene one), for a caller staging a settlement council
      ///   BEFORE entering the Location mission: at that point Mission.Current is still null (no scene yet), so
      ///   the scene check would tell nothing useful, and checking it here would need a second, separate call
      ///   once the mission is up anyway. Null when clear to proceed.
      /// </summary>
      public static string PreCouncilBlockReason(bool isTrial = false)
      {
         switch (CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = IsOpen, PlayerIsPrisoner = Hero.MainHero?.IsPrisoner == true, InMissionScene = false, IsTrial = isTrial}))
         {
            case CouncilAccessBlock.ChatAlreadyOpen: return "A conversation is already open. Close it before convening.";
            case CouncilAccessBlock.PlayerCaptive:   return "You cannot convene while you are held captive.";
            default:                                 return null;
         }
      }

      // Internal (not private): ChatViewModel.PopulateCouncilParticipants reuses this for the anchor seat too, so
      // the staging anchor reads with the SAME scope label as every other seat (no privileged/leader seat).
      internal static string DescribeCouncilSeat(CouncilScope scope)
         => scope switch {
            CouncilScope.Family => "kin of the player's house, seated at this council",
            CouncilScope.ClanLords => "a lord of the player's clan, seated at this council",
            CouncilScope.WarCouncil => "an allied lord seated at this war council",
            CouncilScope.Parley => "the enemy leader this parley was sought with",
            CouncilScope.Party => "one riding with the player, seated at this council",
            CouncilScope.Trial => "a lord of the realm, sitting in judgement of the player",
            _ => "a companion in the player's service, seated at this council"
         };

      /// <summary>
      ///   Opens a convened ASSEMBLY as a round-table council over whoever actually reached the hall: the Present
      ///   participants sit in person, and each Ambassador seat speaks as an envoy for a lord who could not come.
      ///   The anchor who opens the floor is drawn from those present, on the mod's own regard ledger. A profile
      ///   is created for any seated hero the player has not met, so the anchor and every reaction resolve.
      /// </summary>
      public static string OpenForAssembly()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return "No campaign is running.";

         string blocked = CouncilOpenBlockedReason(allowInScene: false);
         if (blocked != null) return blocked;

         AssemblyRecord pending = AssemblyBehavior.Instance?.Pending;

         if (pending == null) return "No assembly is convened.";

         List<Hero> present = SeatedByStatus(pending, AssemblyInviteeStatus.Present, behavior);
         List<Hero> envoys = SeatedByStatus(pending, AssemblyInviteeStatus.Ambassador, behavior);

         if (present.Count == 0 && envoys.Count == 0)
            return "No one has reached the assembly yet, and no envoys have come. Wait for them, or stand it down.";

         List<Hero> anchorPool = present.Count > 0
            ? present
            : envoys;
         var facts = anchorPool
                    .Select(h => new CouncilSeatFacts(h.StringId, behavior.Store.Get(h.StringId)?.ReputationWithPlayer ?? 0))
                    .ToList();
         int anchorIndex = CouncilAnchorPolicy.SelectAnchor(facts);
         Hero anchor = anchorIndex >= 0
            ? anchorPool[anchorIndex]
            : anchorPool[0];

         _forceCouncilSkin = true;
         _forceCouncilPushedScreen = true;
         Open(anchor.Name?.ToString());

         if (ActiveViewModel == null) return "The chat window could not be opened.";
         ActiveViewModel.SetNpcHero(anchor);

         foreach (Hero member in present.Where(h => h != anchor))
            ActiveViewModel.AddWitness(new WitnessEntry {
               Name = member.Name?.ToString(),
               HeroStringId = member.StringId,
               IsPlayerCompanion = member.IsPlayerCompanion,
               RelationToNpc = "seated at the player's assembly, present in person",
               Persona = DescribeCouncilMemberPersona(member, behavior),
               IsFemale = member.IsFemale
            });

         foreach (Hero lord in envoys.Where(h => h != anchor))
            ActiveViewModel.AddWitness(new WitnessEntry {
               Name = lord.Name?.ToString(),
               HeroStringId = lord.StringId,
               IsPlayerCompanion = false,
               RelationToNpc = $"an envoy speaking in the name of {lord.Name}, who could not attend in person",
               Persona = DescribeCouncilMemberPersona(lord, behavior),
               IsFemale = lord.IsFemale
            });

         ActiveViewModel.ActivateCouncil(CouncilScope.Assembly);
         ModLog.Info($"[ASSEMBLY] opened as a council: {present.Count} present, {envoys.Count} by envoy, anchor {anchor.Name}.");

         return $"The assembly opens: {present.Count} present, {envoys.Count} by envoy. {anchor.Name} takes the floor.";
      }

      /// <summary>The living heroes on the roster with this status, each guaranteed a memory profile.</summary>
      private static List<Hero> SeatedByStatus(AssemblyRecord pending, AssemblyInviteeStatus status, NpcMemoryCampaignBehavior behavior)
      {
         var list = new List<Hero>();

         foreach (AssemblyInviteeRecord inv in pending.Invitees.Where(i => i.Status == status))
         {
            Hero h = Hero.FindFirst(x => x.StringId == inv.HeroId);

            if (h == null || !h.IsAlive) continue;
            if (behavior.Store.Get(h.StringId) == null) behavior.Store.Set(HeroProfileMapper.FromHero(h));
            list.Add(h);
         }

         return list;
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
      /// <summary>
      ///   Gives the world back, for a hand-off that nobody caught.
      ///   <para>
      ///     A briefing's close skips these steps deliberately: the next window opens immediately and would
      ///     undo every one of them. But "immediately" is a promise, and an unkept one would leave the map
      ///     scene suspended, the clock stopped and the encounter standing, with no window on screen - a
      ///     frozen game, which is worse than the crash this whole change is undoing. So the promise has a
      ///     catch: whoever armed the hand-off calls this if it does not happen.
      ///   </para>
      ///   <para>
      ///     Every step is the same one Close runs and each is individually guarded, so calling it when the
      ///     world was never taken is a no-op rather than a fault.
      ///   </para>
      /// </summary>
      public static void FinishDeferredClose()
      {
         ModLog.Info("[BRIEFING] the hand-off was not caught; giving the world back as an ordinary close would.");

         // Derived from the two plans, never listed again: the debt IS whatever the hand-off skipped, so it
         // cannot fall out of step with what a hand-off actually skips.
         IReadOnlyList<ChatCloseStep> owed = ChatClosePlan.Deferred();
         ModLog.Info($"[CLOSE] plan=Deferred: {string.Join(" > ", owed)}");

         foreach (ChatCloseStep step in owed) RunCloseStep(step);
      }

      /// <summary>
      ///   Performs one planned step. The plan says WHICH and in what order; this says how, and nothing here
      ///   decides anything - that separation is the whole point (see BriefingReturnDecision's header).
      /// </summary>
      private static void RunCloseStep(ChatCloseStep step)
      {
         switch (step)
         {
            // Recording and the window itself are run directly by Close, above the plan walk: they happen on
            // every path including the finally-guarded one, and must not depend on a plan being well formed.
            case ChatCloseStep.RecordWhatHappened:
            case ChatCloseStep.ReleasePortraits:
            case ChatCloseStep.KillWindow:
               return;

            case ChatCloseStep.ResumeMapScene:
               SafeStep(MapSceneSuspender.Resume, "ResumeMapScene");

               return;

            case ChatCloseStep.EndVanillaConversation:
               SafeStep(EndVanillaConversation, "EndVanillaConversation");

               return;

            case ChatCloseStep.EndStagedMission:
               SafeStep(EndOrphanedStagedMission, "EndOrphanedStagedMission");

               return;

            case ChatCloseStep.EndMapEncounter:
               SafeStep(EndMapEncounterIfNeeded, "EndMapEncounterIfNeeded");

               return;

            case ChatCloseStep.RestoreTime:
               SafeStep(RestoreTime, "RestoreTime");

               return;

            default:
               // A step was added to the plan and never given an action here. Silence would mean the world
               // quietly stops being given back in one of its parts - the exact shape of every fault in this
               // sequence so far - so it is loud instead.
               ModLog.Error($"[CLOSE] planned step {step} has no action wired; it did NOTHING.");

               return;
         }
      }

      private static void EndMapEncounterIfNeeded()
      {
         try
         {
            if (Mission.Current != null) return; // inside a scene/mission — not a map talk
            if (PlayerEncounter.Current == null) return; // no encounter to finish
            if (PlayerEncounter.InsideSettlement) return; // a town/castle encounter — leave it intact

            // A briefing is a step INSIDE a meeting, never the end of one. Gabriel asked Ira for a moment to
            // brief his companion (2026-09-12) and closing that briefing left him on the map with the meeting
            // gone. The cause proper is the briefing dialogue line, which exits on the engine's own
            // close_window token and so ENDS the conversation rather than suspending it; his own encounter was
            // inside a settlement, so this particular call was NOT what finished it that night. It would be on
            // the open map, where all three guards above pass while the encounter is still perfectly alive -
            // so the guard belongs here too, before somebody meets Ira in the field.
            if (CompanionBriefingCoordinator.HasPendingReturn)
            {
               ModLog.Info("[BRIEFING] encounter kept: this close is a briefing, not the end of the meeting.");

               return;
            }

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
         // ONLY end a conversation that is actually in progress. A DIRECT (unstaged) overlay over a live
         // settlement menu + encounter (the at-sea / no-location fallback, the console harness) leaves
         // Hero.OneToOneConversationHero null; calling EndConversation on that phantom conversation while the
         // real settlement encounter is active crashes NATIVELY (0xE0434352, uncaught by SafeStep, so nothing is
         // logged) on leave (player report 2026-08-25). An ordinary chat and every STAGED overlay (captive,
         // audience, council) ride a real conversation here, so ending it is what tears their mission down
         // cleanly; a direct overlay has none, and skipping is a safe no-op for it.
         // A briefing is nested INSIDE a meeting: its close must leave that meeting standing, or the way
         // back has to rebuild a scene from nothing, which is what crashed the game on 2026-09-13.
         if (CompanionBriefingCoordinator.HasPendingReturn)
         {
            ModLog.Info("[BRIEFING] the vanilla conversation is left idling: the meeting is not over.");

            return;
         }

         if (Hero.OneToOneConversationHero == null) return;

         if (Campaign.Current?.ConversationManager != null)
            Campaign.Current.ConversationManager.EndConversation();
      }

      /// <summary>
      ///   Safety net for a STAGED chat (captive/audience/council scene) whose conversation never started: the
      ///   intercept line lost the "start" token to another mod, so no conversation exists for
      ///   <see cref="EndVanillaConversation" /> to end, and it is the conversation end that makes the engine tear
      ///   a conversation mission down. Without this the player would be stranded in a silent scene after the
      ///   chat closes. A no-op for every ordinary conversation (the engine's own conversation-end already ended
      ///   its mission, so the flag the stagers set at open time reads false).
      /// </summary>
      private static void EndOrphanedStagedMission()
      {
         try
         {
            if (!ChatSceneStager.StagedMissionNeedsEnd) return;
            ChatSceneStager.StagedMissionNeedsEnd = false;

            if (Mission.Current == null) return;

            Mission.Current.EndMission();
            ModLog.Info("Orphaned staged mission ended on chat close (its conversation never started).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"EndOrphanedStagedMission failed: {ex.GetType().Name}: {ex.Message}");
         }
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
         // One conversation's scenery must never excuse the next one's modal.
         _backdropFocusClaimers.Clear();
      }

      private static void GenerateChatWindow(string npcName, bool forceClassicSkin = false)
      {
         ActiveViewModel = new ChatViewModel {OnCloseRequested = Close};
         ConversationMood.Reset(); // a new conversation starts again from each character's day

         var conversationHero = Hero.OneToOneConversationHero;
         if (conversationHero != null)
            ActiveViewModel.SetNpcHero(conversationHero);
         else if (!string.IsNullOrWhiteSpace(npcName))
            ActiveViewModel.SetNpcName(npcName);


         // GauntletLayer ctor: (string name, int localOrder, bool shouldClear), the same shape vanilla uses
         // ("MissionEscapeMenu", "MissionConversation"). shouldClear defaults false, which keeps the host
         // screen's rendering visible behind us. The NAME is not decoration: ArbitrateFocus reads every
         // neighbouring layer's name to tell a real modal from the vanilla conversation window.
         _activeLayer = new GauntletLayer(
            "CalradiaRemembers",
            ChatLayerOrder);
         _activeLayer.InputRestrictions.SetInputRestrictions();

         // Consumed here (reset to false) so a flag set for THIS open can never leak into a later conversation
         // that opens through a different path (forceClassicSkin, console override, or the MCM choice).
         bool council = _forceCouncilSkin;
         _forceCouncilSkin = false;

         // Skin precedence: the bespoke council window wins outright (OpenForCouncil/OpenForAssembly); else a
         // captive scene forces Classic for now (the Manuscript skin is not yet fitted to those scenes); else a
         // dev console override (cr.skin); else the MCM menu choice (default Dark Leather). Fall back to Classic
         // if a name is somehow empty so a bad skin can never leave the player with no chat.
         string skin = council
            ? CouncilSkin
            : forceClassicSkin
               ? ClassicSkin
               : !string.IsNullOrWhiteSpace(SkinConsoleOverride)
                  ? SkinConsoleOverride
                  : ModSettings.ChatSkinPrefab;
         if (string.IsNullOrWhiteSpace(skin)) skin = ClassicSkin;
         ActiveSkin = skin;
         _activeMovieId = _activeLayer.LoadMovie(skin, ActiveViewModel);

         IGauntletMovie movie = _activeMovieId.Movie;

         ModLog.Info($"Movie type = {movie?.GetType().FullName}");
         ModLog.Info($"Movie loaded? {movie?.IsLoaded}");
         ModLog.Info($"RootWidget null? {movie?.RootWidget == null}");

         // Consumed here (reset to false) so a flag set for THIS council/assembly open can never leak into a
         // later conversation that opens through a different path. A forced council/assembly always gets a
         // pushed screen (real keyboard focus, no walkable mission), even while HostMode itself stays Overlay
         // for every other conversation.
         bool pushed = HostMode == ChatHostMode.PushedScreen || _forceCouncilPushedScreen;
         _forceCouncilPushedScreen = false;
         _activeIsPushed = pushed;

         if (pushed)
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

            // The backdrop, captured ONCE: whatever already holds focus here is what the chat is being drawn
            // OVER, never something above it. This is not the layer COUNT baseline that caused tashmetu's bug
            // (a late arrival changed the count and the chat fled the screen); it is the set of names, so a
            // layer arriving later is judged on WHAT it is rather than on the fact that it arrived.
            RecordBackdropFocusClaimers();
            _chatHoldsFocus = true;
         }
      }

      private static void KillChatWindow()
      {
         if (_activeLayer == null) return;

         _activeLayer.IsFocusLayer = false;
         _activeLayer.InputRestrictions.ResetInputRestrictions();
         // Bespoke council window: free every seat tile's tableau before the movie goes away, so none survives
         // the close regardless of engine teardown timing (mirrors ReleaseAllPortraits for the message list).
         ActiveViewModel?.ReleaseCouncilPortraits();
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

         _activeIsPushed = false;
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