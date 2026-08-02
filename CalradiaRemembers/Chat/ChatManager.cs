// Code written by Gabriel Mailhot, 05/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Mapping;
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

         // Manuscript skin: zoom the whole chat layer so the fixed-aspect book AND all its overlays scale
         // together (kept aligned) and fill the same fraction of the screen at ANY resolution. A CONSTANT factor
         // (see ManuscriptZoom), NOT scaled by ScreenManager.Scale, further scaled by the player's MCM "Dark
         // Leather Size" factor (ModSettings.DarkLeatherScale, 80-140%, 100% = this constant unchanged) so a
         // high-resolution player can size the book to taste. Re-applied every frame because
         // GauntletLayer.UpdateLayout resets ScaleModifier to the base UI scale each pass. Classic is untouched.
         if (ActiveSkin == ManuscriptSkin && _activeLayer?.UIContext != null)
            _activeLayer.UIContext.ScaleModifier = ManuscriptZoom * ModSettings.DarkLeatherScale;

         SampleVramPeriodically(); // a heartbeat VRAM read, so an idle (no-response) scene still traces its trend

         // Pushed-screen mode needs no focus juggling — the engine hands focus to whatever is pushed on top of us.
         if (_activeIsPushed) return;

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
            // The council's LIFT: re-validates and executes every surviving [RESOLUTION] against the world as
            // it stands NOW. A no-op outside a council (or one with nothing agreed), see FinalizeCouncilResolutions.
            SafeStep(() => ActiveViewModel?.FinalizeCouncilResolutions(), "FinalizeCouncilResolutions");
            // Extension Surface: hand the attributed transcript to any third-party listener as the conversation
            // closes. Independent of the memory finalizers above so a listener sees even a slight conversation.
            SafeStep(() => ActiveViewModel?.RaiseConversationEndedEvent(), "RaiseConversationEndedEvent");

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

      public static void Open(string npcName = null, bool forceClassicSkin = false)
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
               IsCaptiveVictim = true
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
         if (companion == null || profile == null) return;

         Open(companion.Name?.ToString() ?? profile.Name);

         if (ActiveViewModel == null) return;

         ActiveViewModel.SetNpcHero(companion);
         ActiveViewModel.ActivateCompanionAudience(profile, reason, landed, topicDetail);
      }

      /// <summary>
      ///   Opens a COUNCIL of the player's own house: seats everyone the scope gathers, gives the spoken reply
      ///   to the member who regards the player best (<see cref="CouncilAnchorPolicy" />), and seats the rest as
      ///   present members who may speak or merely gesture on each turn. Returns a line describing what was
      ///   opened, or why it could not be, so a caller can report it to the player.
      ///   <paramref name="allowInScene" /> bypasses the InScene refusal: a settlement-hosted council (Phase 1
      ///   scene staging) deliberately opens AFTER its own Location mission has started, so Mission.Current is
      ///   non-null by design there, unlike the ordinary map-overlay path (cr.council), which must still refuse
      ///   inside somebody else's scene.
      /// </summary>
      public static string OpenForCouncil(CouncilScope scope, bool allowInScene = false)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return "No campaign is running.";

         string blocked = CouncilOpenBlockedReason(allowInScene);
         if (blocked != null) return blocked;

         List<Hero> seated = CouncilRosterResolver.Seated(scope);

         if (seated.Count == 0)
            return $"Nobody is seated for a {scope} council right now (nobody available, or nobody of that house).";

         // The anchor is chosen on the mod's OWN personal regard ledger, the one the player actually builds in
         // conversation, not the vanilla clan relation.
         var facts = seated
                    .Select(h => new CouncilSeatFacts(h.StringId, behavior.Store.Get(h.StringId)?.ReputationWithPlayer ?? 0))
                    .ToList();
         int anchorIndex = CouncilAnchorPolicy.SelectAnchor(facts);

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
               RelationToNpc = DescribeCouncilSeat(scope)
            });

         ActiveViewModel.ActivateCouncil(scope);
         ModLog.Info($"[COUNCIL] opened {scope} with {seated.Count} seated, anchor {anchor.Name}.");

         return $"Council ({scope}) opened with {seated.Count} seated. {anchor.Name} speaks for the table; "
              + "the others react each turn.";
      }

      /// <summary>
      ///   Council audit M1/M2: the player-facing reason convening is refused right now, or null when it is
      ///   safe. Maps live engine state to <see cref="CouncilAccessPolicy" /> so the rules stay unit-tested:
      ///   an already-open chat would otherwise be hijacked (the old conversation reassigned an anchor and
      ///   witnesses), and a council makes no sense from inside captivity or (for the map-overlay path) a
      ///   mission scene. <paramref name="allowInScene" /> is true only for the settlement-hosted council,
      ///   which is opened FROM inside its own Location mission on purpose.
      /// </summary>
      private static string CouncilOpenBlockedReason(bool allowInScene)
      {
         bool inScene = !allowInScene && Mission.Current != null;

         switch (CouncilAccessPolicy.Evaluate(IsOpen, Hero.MainHero?.IsPrisoner == true, inScene))
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
      public static string PreCouncilBlockReason()
      {
         switch (CouncilAccessPolicy.Evaluate(IsOpen, Hero.MainHero?.IsPrisoner == true, false))
         {
            case CouncilAccessBlock.ChatAlreadyOpen: return "A conversation is already open. Close it before convening.";
            case CouncilAccessBlock.PlayerCaptive:   return "You cannot convene while you are held captive.";
            default:                                 return null;
         }
      }

      private static string DescribeCouncilSeat(CouncilScope scope)
         => scope switch {
            CouncilScope.Family => "kin of the player's house, seated at this council",
            CouncilScope.ClanLords => "a lord of the player's clan, seated at this council",
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
               RelationToNpc = "seated at the player's assembly, present in person"
            });

         foreach (Hero lord in envoys.Where(h => h != anchor))
            ActiveViewModel.AddWitness(new WitnessEntry {
               Name = lord.Name?.ToString(),
               HeroStringId = lord.StringId,
               IsPlayerCompanion = false,
               RelationToNpc = $"an envoy speaking in the name of {lord.Name}, who could not attend in person"
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

      private static void GenerateChatWindow(string npcName, bool forceClassicSkin = false)
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