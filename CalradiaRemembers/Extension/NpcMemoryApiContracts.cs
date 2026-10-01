// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the PUBLIC contract types for the NPC-memory and conversation-lifecycle API (CrNpcMemory,
// CrConversation). Requested by an addon author who runs an external memory proxy and needs, per conversation:
// which NPC (a STABLE id, never a display-name string), which save/campaign is loaded, the in-game date, the
// attributed transcript when it ends, plus read and write access to what CR stores for that NPC. Kept free of
// Bannerlord types (ids are plain strings) so the contract stays stable while CR's internals change behind it.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The outcome of injecting a memory through <see cref="CrNpcMemory.AddMemory" />.</summary>
   public enum MemoryResult
   {
      /// <summary>The memory was stored on the NPC's profile.</summary>
      Added,

      /// <summary>The entry was malformed (no NPC id, or an empty summary).</summary>
      RejectedInvalid,

      /// <summary>The NPC id did not resolve to a real hero in this campaign.</summary>
      RejectedUnknownHero,

      /// <summary>The memory store is not available (no campaign is loaded).</summary>
      RejectedNoStore
   }

   /// <summary>
   ///   One memory a third-party mod wants an NPC to hold. CR normalises it (bounds the summary, maps the kind,
   ///   dates it) and stores it on the NPC's profile, from where it surfaces in later conversations as the NPC's
   ///   own recollection. The NPC is named by <see cref="NpcId" /> (a hero StringId), never a display name.
   /// </summary>
   public sealed class CrMemoryEntry
   {
      /// <summary>The NPC's hero StringId. REQUIRED. A display name is not accepted.</summary>
      public string NpcId { get; set; }

      /// <summary>The ready-to-remember natural-language line ("We agreed to meet at the ford at dawn.").</summary>
      public string Summary { get; set; }

      /// <summary>
      ///   Optional, since 2.6.10 (tashmetu): true marks the memory as said in confidence. The character speaks of it
      ///   only in private, never in front of others, the audience rule CR's own private memories follow. False
      ///   (the default) keeps it public, as before.
      /// </summary>
      public bool Private { get; set; }

      /// <summary>
      ///   Optional category. Accepts the narrative kinds (Conflict, Collaboration, Agreement, Flirt, Intimacy,
      ///   Betrayal, Confrontation, Other); anything else, or an engine-owned kind, is filed as Other.
      /// </summary>
      public string Kind { get; set; }

      /// <summary>How many in-game days ago the memory happened (0 = today). Dated back from the current day.</summary>
      public int DaysAgo { get; set; }

      /// <summary>The injecting mod's id, for provenance in the logs. Optional but encouraged.</summary>
      public string SourceMod { get; set; }
   }

   /// <summary>One memory CR already holds for an NPC, as read back through <see cref="CrNpcMemory.GetMemories" />.</summary>
   public sealed class CrMemoryRecord
   {
      public CrMemoryRecord(int day, string kind, string summary)
      {
         Day = day;
         Kind = kind;
         Summary = summary;
      }

      /// <summary>The in-game day the memory is dated to.</summary>
      public int Day { get; }

      /// <summary>The event kind label (e.g. "Agreement", "Conflict", "Other").</summary>
      public string Kind { get; }

      /// <summary>The remembered line.</summary>
      public string Summary { get; }
   }

   /// <summary>One attributed line of a conversation, as delivered in <see cref="CrConversationLog" />.</summary>
   public sealed class CrTranscriptLine
   {
      public CrTranscriptLine(string speakerId, string speakerName, bool isPlayer, bool isNarration, string text)
      {
         SpeakerId = speakerId;
         SpeakerName = speakerName;
         IsPlayer = isPlayer;
         IsNarration = isNarration;
         Text = text;
      }

      /// <summary>The speaker's hero StringId, or null for narration and non-hero (synthetic) speakers.</summary>
      public string SpeakerId { get; }

      /// <summary>The speaker's display name (for logging; prefer <see cref="SpeakerId" /> for identity).</summary>
      public string SpeakerName { get; }

      /// <summary>True when this line is the player's own.</summary>
      public bool IsPlayer { get; }

      /// <summary>True when this line is scene narration rather than a spoken line.</summary>
      public bool IsNarration { get; }

      /// <summary>What was said (or narrated).</summary>
      public string Text { get; }
   }

   /// <summary>
   ///   One line as it is DISPLAYED in a conversation, delivered in real time by <see cref="CrConversation.LineSpoken" />.
   ///   This is the surface a text-to-speech (or subtitle, or lip-sync) addon reads: the text is already the clean,
   ///   player-visible words (CR's control tags stripped), attributed to a speaker with enough to pick a voice
   ///   (<see cref="IsFemale" />, <see cref="CultureId" />, and the stable <see cref="SpeakerId" /> as a per-hero
   ///   voice key). Player-authored lines and out-of-character notes arrive with <see cref="IsPlayer" /> true, so a
   ///   voice-over addon can skip them with a single check.
   /// </summary>
   public sealed class CrSpeechLine
   {
      public CrSpeechLine(string conversationId, string speakerId, string speakerName, bool isPlayer,
                          bool isNarration, bool isWitness, bool isFemale, string cultureId, string text)
      {
         ConversationId = conversationId;
         SpeakerId = speakerId;
         SpeakerName = speakerName;
         IsPlayer = isPlayer;
         IsNarration = isNarration;
         IsWitness = isWitness;
         IsFemale = isFemale;
         CultureId = cultureId;
         Text = text;
      }

      /// <summary>The main NPC's hero StringId, identifying which conversation this line belongs to (null if unknown).</summary>
      public string ConversationId { get; }

      /// <summary>The speaker's hero StringId, or null for narration and non-hero (synthetic) speakers. A stable per-voice key.</summary>
      public string SpeakerId { get; }

      /// <summary>The speaker's display name (for logging/subtitles; prefer <see cref="SpeakerId" /> for identity).</summary>
      public string SpeakerName { get; }

      /// <summary>True when this line is the player's own, or an out-of-character note; a voice-over addon typically skips these.</summary>
      public bool IsPlayer { get; }

      /// <summary>True when this line is scene narration (no single speaker) rather than a spoken line.</summary>
      public bool IsNarration { get; }

      /// <summary>True when the speaker is a witness or companion in the scene, not the main conversation NPC.</summary>
      public bool IsWitness { get; }

      /// <summary>Best-effort: true when the speaker hero is female, to pick a voice. False for narration/unknown.</summary>
      public bool IsFemale { get; }

      /// <summary>The speaker hero's culture StringId (for accent/voice selection), or null for narration/unknown.</summary>
      public string CultureId { get; }

      /// <summary>The clean, player-visible words to voice (CR's control tags already stripped).</summary>
      public string Text { get; }

      /// <summary>
      ///   A unique id for THIS conversation: a fresh GUID minted each time a conversation opens. Unlike
      ///   <see cref="ConversationId" /> (the main NPC's hero id, which repeats across every meeting with that
      ///   NPC), this value is shared only by the <see cref="CrConversation.Started" />, LineSpoken, and
      ///   <see cref="CrConversation.Ended" /> of the SAME exchange, so a listener can group lines that belong
      ///   together even across the multi-participant conversations introduced in 2.3.1. Stamped by CR after
      ///   construction; never set by a caller.
      /// </summary>
      public string SessionId { get; internal set; }

      /// <summary>
      ///   The 0-based position of this line within its <see cref="SessionId" />, in display order (0, 1, 2, ...).
      ///   Lets a listener order and de-duplicate lines of one session without relying on arrival timing.
      ///   Stamped by CR after construction; never set by a caller.
      /// </summary>
      public int LineIndex { get; internal set; }
   }

   /// <summary>The identity of a conversation as it OPENS, delivered by <see cref="CrConversation.Started" />.</summary>
   public sealed class CrConversationInfo
   {
      public CrConversationInfo(string npcId, string saveId, int gameDay, string sessionId)
      {
         NpcId = npcId;
         SaveId = saveId;
         GameDay = gameDay;
         SessionId = sessionId;
      }

      /// <summary>The NPC's hero StringId.</summary>
      public string NpcId { get; }

      /// <summary>The loaded campaign's stable unique id, so a listener can isolate memory per save.</summary>
      public string SaveId { get; }

      /// <summary>The in-game day the conversation takes place.</summary>
      public int GameDay { get; }

      /// <summary>
      ///   A unique id for this conversation, matching the <see cref="CrSpeechLine.SessionId" /> of every
      ///   line spoken in it and the <see cref="CrConversationLog.SessionId" /> it closes with.
      /// </summary>
      public string SessionId { get; }
   }

   /// <summary>A whole conversation as it CLOSES, delivered by <see cref="CrConversation.Ended" />.</summary>
   public sealed class CrConversationLog
   {
      public CrConversationLog(string npcId, string saveId, int gameDay, IReadOnlyList<CrTranscriptLine> lines, string sessionId)
      {
         NpcId = npcId;
         SaveId = saveId;
         GameDay = gameDay;
         Lines = lines;
         SessionId = sessionId;
      }

      /// <summary>The main NPC's hero StringId.</summary>
      public string NpcId { get; }

      /// <summary>The loaded campaign's stable unique id.</summary>
      public string SaveId { get; }

      /// <summary>The in-game day the conversation took place.</summary>
      public int GameDay { get; }

      /// <summary>Every attributed line, oldest first, including witness (third-party) lines and narration.</summary>
      public IReadOnlyList<CrTranscriptLine> Lines { get; }

      /// <summary>
      ///   The same unique id carried by <see cref="CrConversationInfo.SessionId" /> and every
      ///   <see cref="CrSpeechLine.SessionId" /> of this conversation.
      /// </summary>
      public string SessionId { get; }
   }
}
