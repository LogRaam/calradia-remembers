// Code written by Gabriel Mailhot, 21/09/2026.
// A conversation the player stepped out of, kept whole so he can step back into it.
//
// fkasad, Nexus, 20/09/2026: "I quit the chat, save and close the game and live my life. Then come back and
// reload... He remembers talking to me but most of what we said is gone. I need to restart the negociation from
// the begining." Roleplay takes longer than a battle, so the player leaves in the middle of one, and CR was
// built on the assumption that leaving means ending: the transcript is a process object, and what survives is
// the summary written when the chat closed.
//
// That assumption is right for a conversation that ended and wrong for one that was interrupted, and the
// difference costs the player the whole negotiation. This type is the exception, drawn as narrowly as the report
// itself draws it: the LAST conversation only, for the lord it was had with, on the day it was had.
//
// WHY THE RAW LINES AND NOT A RECAP. The transcript is not just what the player reads, it is what the model
// reads: every participant's session is recomposed from it before every call (ConversationTranscript
// .ComposeSessionFor). Keeping the lines therefore restores the NPC's continuity and the player's screen from
// ONE stored fact. A "summary of the previous episode" injected into the prompt, which is what the report
// imagines, would be a second telling that has to be kept in step with the first, and CR already has one of
// those: the persisted memory event.

#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>
   ///   Whose conversation this is and when it was had. Separate from the words so a capture can never be taken
   ///   without saying who it belongs to: an unattributed transcript would match the next chat that asked for
   ///   one, and put a lord's words in another lord's mouth.
   /// </summary>
   public sealed class ConversationResumeIdentity
   {
      /// <summary>The hero the player was talking to. A capture with no hero named is refused.</summary>
      public string HeroId { get; set; } = "";

      /// <summary>The campaign day it was had on. A conversation does not survive the turn of the day.</summary>
      public int GameDay { get; set; }

      /// <summary>
      ///   The memory event this conversation already wrote when it first closed
      ///   (<c>ChatViewModel.FinalizeConversationMemory</c>'s base event). Carried so the resumed sitting comes
      ///   back to that same event rather than writing a second one, which would leave the NPC remembering one
      ///   afternoon as two conversations. Empty when the exchange was too slight to have written one.
      /// </summary>
      public string AnchorId { get; set; } = "";
   }

   /// <summary>
   ///   The words of one conversation, kept across a save. Serializes to a plain JSON string so the mod's
   ///   persistence layer can store it verbatim; anything unreadable degrades to "there is nothing to resume"
   ///   rather than throwing, since this rides in the campaign file (save-migration rule).
   /// </summary>
   public sealed class ConversationResumeSnapshot
   {
      /// <summary>
      ///   How many lines are kept. A conversation longer than this keeps its most recent ones, because the
      ///   player comes back to answer the last thing said. Sixty is roughly an hour of play at the pace the
      ///   report describes, and it bounds what rides in every save file.
      /// </summary>
      public const int MaxLinesKept = 60;

      /// <summary>
      ///   And a ceiling in characters, because a line has no bounded length: a single long narration beat can be
      ///   several thousand characters, so a line count alone does not bound the blob.
      /// </summary>
      public const int MaxCharactersKept = 24000;

      public string HeroId { get; set; } = "";
      public int GameDay { get; set; }
      public string AnchorId { get; set; } = "";

      /// <summary>The conversation's own compressed recap, if it had grown long enough to be folded.</summary>
      public string RunningSummary { get; set; } = "";

      /// <summary>Index into <see cref="Lines" />: everything before it is covered by <see cref="RunningSummary" />.</summary>
      public int SummarizedThrough { get; set; }

      /// <summary>The kept lines, oldest first.</summary>
      public List<TranscriptLine> Lines { get; set; } = new List<TranscriptLine>();

      /// <summary>
      ///   Takes the conversation as it stands, trimmed to what is worth carrying. Returns null when there is
      ///   nothing to keep (no words, or no hero to keep them for), so a caller never stores an empty capture
      ///   that a later open would "resume" into a blank exchange.
      /// </summary>
      public static ConversationResumeSnapshot Capture(ConversationTranscript transcript, ConversationResumeIdentity identity)
      {
         if (transcript == null || transcript.Count == 0) return null;
         if (identity == null || string.IsNullOrWhiteSpace(identity.HeroId)) return null;

         int from = FirstLineToKeep(transcript.Lines);
         var kept = new List<TranscriptLine>();
         for (int i = from; i < transcript.Lines.Count; i++) kept.Add(transcript.Lines[i]);

         if (kept.Count == 0) return null;

         // The fold marker is an INDEX, so dropping lines from the front moves it by the same amount. Left where
         // it was, it would mark still-raw lines as already recapped and those words would reach the model
         // neither raw nor folded.
         int folded = transcript.SummarizedThrough - from;
         if (folded < 0) folded = 0;
         if (folded > kept.Count) folded = kept.Count;

         return new ConversationResumeSnapshot {
            HeroId = identity.HeroId,
            GameDay = identity.GameDay,
            AnchorId = identity.AnchorId ?? "",
            RunningSummary = transcript.RunningSummary ?? "",
            SummarizedThrough = folded,
            Lines = kept
         };
      }

      /// <summary>
      ///   The kept conversation as a transcript again, ready to be seeded into a fresh chat
      ///   (<see cref="ConversationTranscript.SeedFrom" />) and replayed into the bubbles.
      /// </summary>
      public ConversationTranscript ToTranscript()
      {
         var transcript = new ConversationTranscript();

         foreach (TranscriptLine line in Lines) transcript.Record(line);

         if (!string.IsNullOrEmpty(RunningSummary)) transcript.ApplySummary(RunningSummary, SummarizedThrough);

         return transcript;
      }

      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>
      ///   Reads back what <see cref="Serialize" /> wrote. Null, empty, corrupt, or simply the wrong shape all
      ///   give null: a conversation that fails to resume is a small disappointment, a load that throws is the
      ///   player's campaign.
      /// </summary>
      public static ConversationResumeSnapshot Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return null;

         try
         {
            ConversationResumeSnapshot read = JsonConvert.DeserializeObject<ConversationResumeSnapshot>(json);
            if (read == null) return null;

            read.Lines = read.Lines ?? new List<TranscriptLine>();
            read.HeroId = read.HeroId ?? "";
            read.AnchorId = read.AnchorId ?? "";
            read.RunningSummary = read.RunningSummary ?? "";

            return read;
         }
         catch (Exception)
         {
            return null;
         }
      }

      #region private

      /// <summary>
      ///   The index of the oldest line worth carrying: whichever of the two ceilings bites first. Walked from
      ///   the END backwards, because the recent words are the ones the player is coming back to answer.
      /// </summary>
      private static int FirstLineToKeep(IReadOnlyList<TranscriptLine> lines)
      {
         int byCount = lines.Count > MaxLinesKept ? lines.Count - MaxLinesKept : 0;

         var spent = 0;
         int byLength = 0;
         for (int i = lines.Count - 1; i >= byCount; i--)
         {
            spent += lines[i].Text?.Length ?? 0;
            if (spent <= MaxCharactersKept) continue;

            byLength = i + 1;
            break;
         }

         // One enormous line (a long narration beat) must not swallow the whole capture: the last thing said is
         // the one thing the player is certain to be coming back to, so it is kept whatever it costs.
         if (byLength >= lines.Count) byLength = lines.Count - 1;

         return byLength > byCount ? byLength : byCount;
      }

      #endregion
   }
}
