// Code written by Gabriel Mailhot, 21/09/2026.
// Keeps the LAST conversation across a save, so the player can step back into the one he stepped out of.
//
// fkasad, Nexus 20/09/2026: "Roleplaying takes time... I quit the chat, save and close the game and live my
// life. Then come back and reload. For the game, no time elapsed... He remembers talking to me but most of what
// we said is gone. I need to restart the negociation from the begining."
//
// ONE conversation is kept, never a collection keyed by hero, and that is the design rather than a shortcut. The
// report asked for exactly that ("That chat memory being deleted as soon as you open a new chat"), and it is
// what makes the rule enforce itself: opening any chat consumes what is kept, so "the player has not talked to
// anybody else since" needs no bookkeeping to be true. A per-hero cabinet of transcripts would grow in every
// save file and would sooner or later hand somebody words from a conversation the world had long moved past.
//
// No lock, unlike ConversationSummaryQueueRunner: everything here runs on the main thread (a chat opens, a chat
// closes, the engine saves), and there is no background worker draining anything.

#region

using System;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Holds at most one <see cref="ConversationResumeSnapshot" /> and persists it with the campaign. Additive
   ///   key, tolerant load: a save written before this feature simply has nothing kept.
   /// </summary>
   public sealed class ConversationResumeStore
   {
      // A byte[] blob, distinct from every other key, mirroring the profile store and the summary queue. A
      // transcript can run past the ~65535-char limit that bites native string keys.
      private const string SaveKey = "CR_ConversationResume";

      private ConversationResumeSnapshot _kept;

      /// <summary>Whether anything is currently kept, for the diagnostic command and the self-test.</summary>
      public bool HasKeptConversation => _kept != null;

      /// <summary>
      ///   Keeps this conversation, replacing whatever was kept before. A capture with no words or no hero named
      ///   is refused by <see cref="ConversationResumeSnapshot.Capture" />, and refusing it here means FORGETTING
      ///   the previous one: a chat that ends with nothing said is still a chat the player has moved on to, so
      ///   leaving the older transcript in place would let it resume out of its own conversation.
      /// </summary>
      public void Keep(ConversationTranscript transcript, ConversationResumeIdentity identity)
      {
         try
         {
            _kept = ConversationResumeSnapshot.Capture(transcript, identity);

            if (_kept != null)
               ModLog.Info($"[CONV RESUME] kept {_kept.Lines.Count} line(s) with {_kept.HeroId} on day {_kept.GameDay}.");
         }
         catch (Exception ex)
         {
            // Nothing here is worth a broken close. The worst case is the player retyping his last exchange,
            // which is exactly what happened before this feature existed.
            ModLog.Error($"[CONV RESUME] capture failed, nothing kept: {ex.GetType().Name}: {ex.Message}");
            _kept = null;
         }
      }

      /// <summary>
      ///   Hands back the kept conversation if it belongs in the chat now opening, and CLEARS it either way.
      ///   Clearing on a refusal is the whole "deleted as soon as you open a new chat" rule: a transcript that
      ///   did not match this chat must not be waiting for the next one.
      /// </summary>
      public ConversationResumeSnapshot Consume(string openingWithHeroId, int today, bool isOrdinaryConversation)
      {
         ConversationResumeSnapshot kept = _kept;
         _kept = null;

         var facts = new ConversationResumeFacts {
            KeptForHeroId = kept?.HeroId ?? "",
            KeptOnDay = kept?.GameDay ?? 0,
            KeptLineCount = kept?.Lines?.Count ?? 0,
            OpeningWithHeroId = openingWithHeroId ?? "",
            Today = today,
            IsOrdinaryConversation = isOrdinaryConversation
         };

         ConversationResumeVerdict verdict = ConversationResumePolicy.Decide(facts);

         if (verdict == ConversationResumeVerdict.Resume)
         {
            ModLog.Info($"[CONV RESUME] resuming {kept.Lines.Count} line(s) with {kept.HeroId}.");

            return kept;
         }

         // NothingKept is the ordinary case (every first conversation), so it is not worth a line. The others
         // are worth one: they are what somebody reads when a resume they expected did not happen.
         if (verdict != ConversationResumeVerdict.NothingKept)
            ModLog.Info($"[CONV RESUME] not resumed ({verdict}); the kept conversation is discarded.");

         return null;
      }

      /// <summary>Drops whatever is kept. For a campaign ending, and for the self-test's cleanup.</summary>
      public void Forget() => _kept = null;

      /// <summary>What is kept, in one line, for the diagnostic command.</summary>
      public string Describe()
         => _kept == null
            ? "nothing kept"
            : $"{_kept.Lines.Count} line(s) with {_kept.HeroId} on day {_kept.GameDay}"
            + (string.IsNullOrEmpty(_kept.AnchorId) ? ", no memory anchor" : $", anchored on {_kept.AnchorId}");

      /// <summary>
      ///   Persists the kept conversation as a UTF-8 byte[] blob. A bad blob must never brick a save or a load,
      ///   so every step is isolated and any failure degrades to nothing kept.
      /// </summary>
      public void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving) Save(dataStore);
         else Load(dataStore);
      }

      #region private

      private void Save(IDataStore dataStore)
      {
         string json;
         try { json = _kept?.Serialize() ?? ""; }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV RESUME] serialize failed, keeping nothing this save. {ex.GetType().Name}: {ex.Message}");
            json = "";
         }

         byte[] bytes = Encoding.UTF8.GetBytes(json);
         try { dataStore.SyncData(SaveKey, ref bytes); }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV RESUME] blob save failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void Load(IDataStore dataStore)
      {
         byte[] bytes = null;
         try { dataStore.SyncData(SaveKey, ref bytes); }
         catch
         {
            /* key absent in a save written before this feature: nothing kept, which is the old behaviour */
         }

         string json = bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null;
         _kept = ConversationResumeSnapshot.Deserialize(json);

         if (_kept != null) ModLog.Info($"[CONV RESUME] loaded {_kept.Lines.Count} line(s) with {_kept.HeroId} from save.");
      }

      #endregion
   }
}
