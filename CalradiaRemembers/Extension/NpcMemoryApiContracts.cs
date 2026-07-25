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

   /// <summary>The identity of a conversation as it OPENS, delivered by <see cref="CrConversation.Started" />.</summary>
   public sealed class CrConversationInfo
   {
      public CrConversationInfo(string npcId, string saveId, int gameDay)
      {
         NpcId = npcId;
         SaveId = saveId;
         GameDay = gameDay;
      }

      /// <summary>The NPC's hero StringId.</summary>
      public string NpcId { get; }

      /// <summary>The loaded campaign's stable unique id, so a listener can isolate memory per save.</summary>
      public string SaveId { get; }

      /// <summary>The in-game day the conversation takes place.</summary>
      public int GameDay { get; }
   }

   /// <summary>A whole conversation as it CLOSES, delivered by <see cref="CrConversation.Ended" />.</summary>
   public sealed class CrConversationLog
   {
      public CrConversationLog(string npcId, string saveId, int gameDay, IReadOnlyList<CrTranscriptLine> lines)
      {
         NpcId = npcId;
         SaveId = saveId;
         GameDay = gameDay;
         Lines = lines;
      }

      /// <summary>The main NPC's hero StringId.</summary>
      public string NpcId { get; }

      /// <summary>The loaded campaign's stable unique id.</summary>
      public string SaveId { get; }

      /// <summary>The in-game day the conversation took place.</summary>
      public int GameDay { get; }

      /// <summary>Every attributed line, oldest first, including witness (third-party) lines and narration.</summary>
      public IReadOnlyList<CrTranscriptLine> Lines { get; }
   }
}
