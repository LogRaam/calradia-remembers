// Code written by Gabriel Mailhot, 11/06/2026.

#region

using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Dialogue
{
   /// <summary>
   ///   Captures the vanilla dialogue exchanged between the player and an NPC
   ///   during a Bannerlord conversation, so that when the player switches to
   ///   the Calradia Remembers chat window, the LLM (and the visible message
   ///   list) start with full context of what just happened.
   ///   Lifecycle:
   ///   - Subscribe() — called once at campaign start; hooks the events.
   ///   - On ConsequenceRunned → records the consumed sentence (NPC or Player);
   ///   also detects the start of a NEW conversation (partner change, or the
   ///   deferred-clear flag armed by the previous ConversationEnd) and drops
   ///   the previous conversation's lines first. ConversationBegin is NOT
   ///   relied upon — it does not fire reliably in this engine build.
   ///   - On ConversationEnd  → arms the deferred clear; does NOT clear
   ///   immediately (the dialog flow consumes the buffer while the vanilla
   ///   conversation is still live).
   ///   - GetCapturedLines() returns the live buffer (consume then Clear()).
   ///   - Clear() empties the buffer (called after the chat window consumes them).
   ///   Note on text resolution: ConsequenceRunned fires AFTER
   ///   UpdateCurrentSentenceText, so Campaign.Current.ConversationManager.CurrentSentenceText
   ///   returns the fully rendered text (variables substituted, animations stripped)
   ///   of the sentence we are recording.
   /// </summary>
   public class VanillaDialogueRecorder
   {
      private readonly List<CapturedLine> _captured = new();
      private bool _clearBeforeNextCapture;

      // New-conversation detection (see OnConsequenceRunned). We do NOT rely on
      // ConversationBegin to clear the buffer: in this engine build it does not fire
      // reliably per conversation, so without this the lines from prior conversations
      // (the tavernkeeper, a previous NPC) accumulate and bleed into the next NPC's
      // chat intro. OnConsequenceRunned is the one event we have proof fires.
      private CharacterObject _lastCapturePartner;
      private bool _subscribed;

      public void Clear() => _captured.Clear();

      public IReadOnlyList<CapturedLine> GetCapturedLines() => _captured;

      /// <summary>
      ///   Subscribes to the ConversationManager events. Must be called once
      ///   the campaign is live (during behavior RegisterEvents).
      /// </summary>
      public void Subscribe()
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
         mgr.ConversationEnd += OnConversationEnd;

         _subscribed = true;
         ModLog.Info("VanillaDialogueRecorder subscribed to ConversationManager events.");
      }

      public readonly struct CapturedLine
      {
         public readonly string AuthorName;
         public readonly string Text;
         public readonly bool IsPlayer;

         public CapturedLine(string authorName, string text, bool isPlayer)
         {
            AuthorName = authorName;
            Text = text;
            IsPlayer = isPlayer;
         }
      }

      #region private

      private void OnConsequenceRunned(ConversationSentence sentence)
      {
         try
         {
            if (sentence == null) return;

            ConversationManager mgr = Campaign.Current?.ConversationManager;

            if (mgr == null) return;

            // Start a fresh buffer whenever a NEW vanilla conversation begins. We key
            // this off ConsequenceRunned — the one event proven to fire — rather than
            // ConversationBegin (unreliable here). A new conversation is detected by a
            // change of partner, or by the deferred-clear flag set on the previous
            // ConversationEnd (covers a second conversation with the SAME partner).
            // The clear happens at the FIRST capture of the new conversation, never on
            // end, so the greeting-line consequence that seeds the chat (while the
            // vanilla conversation is still live) can still read the buffer.
            CharacterObject partner = mgr.OneToOneConversationCharacter;
            if (_clearBeforeNextCapture || !ReferenceEquals(partner, _lastCapturePartner))
            {
               if (_captured.Count > 0)
                  ModLog.Info($"VanillaDialogueRecorder: new conversation — dropped {_captured.Count} stale line(s) from the previous one.");

               _captured.Clear();
               _clearBeforeNextCapture = false;
               _lastCapturePartner = partner;
            }

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

      // ── Event handlers ────────────────────────────────────────────────────

      private void OnConversationBegin()
      {
         // Harmless redundancy: if ConversationBegin ever does fire, clearing here is
         // consistent with the partner-change reset in OnConsequenceRunned. The
         // load-bearing reset lives there because this event is unreliable.
         _captured.Clear();
         _lastCapturePartner = null;
         _clearBeforeNextCapture = false;
      }

      private void OnConversationEnd()
      {
         // Do NOT clear here — the dialog flow consumes the buffer (via the greeting
         // line's consequence) while the vanilla conversation is still live, so an
         // end-time clear would race the seed and wipe it. Instead, arm a deferred
         // clear: the next conversation's first captured line drops these stale lines.
         // Covers the case where the player walks away without opening the chat, and
         // the case of a second conversation with the same partner (which the
         // partner-change check alone would miss).
         _clearBeforeNextCapture = true;
      }

      private string ResolveAuthorName(ConversationSentence sentence, ConversationManager mgr)
      {
         if (sentence.IsPlayer)
            return Hero.MainHero?.Name?.ToString() ?? "You";

         CharacterObject npc = mgr.OneToOneConversationCharacter;

         return npc?.Name?.ToString() ?? "the lord";
      }

      #endregion
   }
}