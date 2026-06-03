// Code written by Gabriel Mailhot, 21/05/2026.

#region

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
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
        private static bool _timeControlModeSaved;

        public static ChatViewModel ActiveViewModel { get; private set; }

        public static bool IsOpen => _activeLayer != null;

        public static void Close()
        {
            if (!IsOpen) return;

            KillChatWindow();
            EndVanillaConversation();
            RestoreTime();
            FreeResources();

            ModLog.Info("Chat window closed — returned to campaign map.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        ///   Opens the chat window pre-loaded with a received letter and ready for
        ///   the player to compose a reply. When the player sends their message the
        ///   chat closes instead of showing an inline response — the NPC's reply is
        ///   queued as a new <see cref="NpcMemoryService.Core.Models.PendingLetter" />.
        /// </summary>
        public static void OpenForLetterReply(
            NpcMemoryService.Core.Models.NpcProfile profile,
            TaleWorlds.CampaignSystem.Hero npcHero,
            NpcMemoryService.Core.Models.PendingLetter letter)
        {
            Open(profile.Name);
            if (ActiveViewModel == null) return;

            // Point the VM at the correct hero so the relation summary resolves.
            ActiveViewModel.SetNpcHero(npcHero);
            ActiveViewModel.ActivateLetterReplyMode(letter, profile);
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
            GenerateChatWindow(npcName);

            ModLog.Info($"Chat window opened on {_hostScreen.GetType().Name} with NPC '{npcName ?? "(none)"}'.");
        }

        /// <summary>
        ///   Requests the chat to scroll to its newest message on the next frame.
        ///   Safe to call from any code path that just appended a message.
        /// </summary>
        public static void ScrollToBottom()
        {
            try
            {
                ChatScrollablePanel panel = ChatScrollablePanel.ActiveInstance;
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

        // ── Private ───────────────────────────────────────────────────────────

        private static void EndVanillaConversation()
        {
            if (Campaign.Current?.ConversationManager != null)
                Campaign.Current.ConversationManager.EndConversation();
        }

        private static void FreeResources()
        {
            _activeLayer = null;
            _activeMovieId = null;
            ActiveViewModel = null;
            _hostScreen = null;
            _timeControlModeSaved = false;
        }

        private static void GenerateChatWindow(string npcName)
        {
            ActiveViewModel = new ChatViewModel {OnCloseRequested = Close};

            Hero conversationHero = Hero.OneToOneConversationHero;
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
            _timeControlModeSaved = true;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            ModLog.Info($"Campaign time paused (was {_previousTimeControlMode}).");
        }

        private static void RestoreTime()
        {
            if (_timeControlModeSaved && Campaign.Current != null)
                Campaign.Current.TimeControlMode = _previousTimeControlMode;
        }

        #endregion
    }
}