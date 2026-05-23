using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;

namespace CalradiaRemembers.Dialogue
{
    /// <summary>
    /// Captures the vanilla dialogue exchanged between the player and an NPC
    /// during a Bannerlord conversation, so that when the player switches to
    /// the Calradia Remembers chat window, the LLM (and the visible message
    /// list) start with full context of what just happened.
    ///
    /// Lifecycle:
    ///   - Subscribe() — called once at campaign start; hooks the events.
    ///   - On ConversationBegin → clears the buffer.
    ///   - On ConsequenceRunned → records the consumed sentence (NPC or Player).
    ///   - On ConversationEnd  → does NOT clear the buffer; the dialog flow
    ///     decides when to consume it.
    ///   - GetCapturedLines() returns a snapshot.
    ///   - Clear() empties the buffer (called after the chat window consumes them).
    ///
    /// Note on text resolution: ConsequenceRunned fires AFTER
    /// UpdateCurrentSentenceText, so Campaign.Current.ConversationManager.CurrentSentenceText
    /// returns the fully rendered text (variables substituted, animations stripped)
    /// of the sentence we are recording.
    /// </summary>
    public static class VanillaDialogueRecorder
    {
        private static readonly List<CapturedLine> _captured = new List<CapturedLine>();
        private static bool _subscribed;

        public readonly struct CapturedLine
        {
            public readonly string AuthorName;
            public readonly string Text;
            public readonly bool   IsPlayer;

            public CapturedLine(string authorName, string text, bool isPlayer)
            {
                AuthorName = authorName;
                Text       = text;
                IsPlayer   = isPlayer;
            }
        }

        /// <summary>
        /// Subscribes to the ConversationManager events. Must be called once
        /// the campaign is live (during behavior RegisterEvents).
        /// </summary>
        public static void Subscribe()
        {
            if (_subscribed)
            {
                ModLog.Info("VanillaDialogueRecorder.Subscribe called twice — ignored.");
                return;
            }

            ConversationManager mgr = Campaign.Current?.ConversationManager;
            if (mgr == null)
            {
                ModLog.Error("VanillaDialogueRecorder.Subscribe: ConversationManager not available.");
                return;
            }

            mgr.ConversationBegin += OnConversationBegin;
            mgr.ConsequenceRunned += OnConsequenceRunned;
            mgr.ConversationEnd   += OnConversationEnd;

            _subscribed = true;
            ModLog.Info("VanillaDialogueRecorder subscribed to ConversationManager events.");
        }

        public static IReadOnlyList<CapturedLine> GetCapturedLines() => _captured;

        public static void Clear() => _captured.Clear();

        // ── Event handlers ────────────────────────────────────────────────────

        private static void OnConversationBegin()
        {
            _captured.Clear();
        }

        private static void OnConversationEnd()
        {
            // Intentionally NOT clearing — the dialog flow consumes the buffer
            // when the player opens the chat window. If the player just walks
            // away from a vanilla dialogue without opening the chat, the buffer
            // remains until the next ConversationBegin overwrites it.
        }

        private static void OnConsequenceRunned(ConversationSentence sentence)
        {
            try
            {
                if (sentence == null) return;

                ConversationManager mgr = Campaign.Current?.ConversationManager;
                if (mgr == null) return;

                // The rendered text — variables substituted, animation tags stripped.
                string text = mgr.CurrentSentenceText;
                if (string.IsNullOrWhiteSpace(text)) return;

                string author = ResolveAuthorName(sentence, mgr);
                _captured.Add(new CapturedLine(author, text, sentence.IsPlayer));
            }
            catch (Exception ex)
            {
                ModLog.Error($"VanillaDialogueRecorder.OnConsequenceRunned threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string ResolveAuthorName(ConversationSentence sentence, ConversationManager mgr)
        {
            if (sentence.IsPlayer)
                return Hero.MainHero?.Name?.ToString() ?? "You";

            CharacterObject npc = mgr.OneToOneConversationCharacter;
            return npc?.Name?.ToString() ?? "the lord";
        }
    }
}
