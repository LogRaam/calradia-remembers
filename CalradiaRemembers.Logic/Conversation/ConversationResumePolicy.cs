// Code written by Gabriel Mailhot, 21/09/2026.
// Whether the conversation kept from last time may be picked up again, or whether putting it back would be a lie.
//
// fkasad, Nexus, 20/09/2026, drew the bound himself in the same breath as the request: "That chat memory being
// deleted as soon as you open a new chat (after being reused if it's the same npc/same day)."
//
// The bound is the important half. CR's standing rule is that an NPC's continuity comes from what he REMEMBERS,
// never from what the player claims was said, and the report accepts that rule explicitly ("'We said this or
// that and you agreed to it' doesn't work and it is normal and has to be like this"). Resuming a transcript is
// not an exception to it: the words are real, kept verbatim, for the one case where the conversation did not
// actually end. Every case below where that is not true is a refusal, because a mod that recites an afternoon
// the world has moved past reads as the model losing track, which is the one impression Gabriel refuses to
// create (2026-09-20: a character must be vague, ignorant, or silent, never wrong).

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>Why a kept conversation was or was not picked up again. Named rather than a bool, because the refusal is what a diagnostic log needs.</summary>
   public enum ConversationResumeVerdict
   {
      /// <summary>Same person, same day: the player stepped out and stepped back in.</summary>
      Resume,

      /// <summary>Nothing was kept, or what was kept has no words in it. Every first conversation of a campaign.</summary>
      NothingKept,

      /// <summary>The player is opening a chat with a different hero. The kept words are not his.</summary>
      SomebodyElse,

      /// <summary>The campaign clock has turned. The conversation genuinely ended and lives in the NPC's memory now.</summary>
      ANewDay,

      /// <summary>A captive scene, a companion briefing, a post-duel meeting: staged, with its own opening.</summary>
      NotAnOrdinaryConversation
   }

   /// <summary>What is known when a chat is about to open, against what was kept when the last one closed.</summary>
   public sealed class ConversationResumeFacts
   {
      /// <summary>The hero the kept conversation was had with. Empty when nothing was kept.</summary>
      public string KeptForHeroId { get; set; } = "";

      /// <summary>The campaign day the kept conversation was had on.</summary>
      public int KeptOnDay { get; set; }

      /// <summary>How many lines were kept. Zero is the same as nothing kept.</summary>
      public int KeptLineCount { get; set; }

      /// <summary>The hero whose chat is opening now.</summary>
      public string OpeningWithHeroId { get; set; } = "";

      /// <summary>The campaign day now.</summary>
      public int Today { get; set; }

      /// <summary>
      ///   False for anything staged: a captive scene, the companion briefing, the post-duel meeting. Each opens
      ///   with its own cue that a resumed exchange would contradict, and the briefing must never put its words
      ///   where the person it is about could read them.
      /// </summary>
      public bool IsOrdinaryConversation { get; set; } = true;
   }

   /// <summary>Decides whether the kept conversation belongs in the chat that is opening.</summary>
   public static class ConversationResumePolicy
   {
      /// <summary>
      ///   The verdict, most fundamental refusal first: what was kept, then what kind of chat is opening, then
      ///   who with, then when. The order matters only for the log line, and it matters there because "somebody
      ///   else" would be a misleading thing to say when nothing was kept for anybody.
      /// </summary>
      public static ConversationResumeVerdict Decide(ConversationResumeFacts facts)
      {
         if (facts == null) return ConversationResumeVerdict.NothingKept;
         if (string.IsNullOrWhiteSpace(facts.KeptForHeroId) || facts.KeptLineCount <= 0) return ConversationResumeVerdict.NothingKept;
         if (!facts.IsOrdinaryConversation) return ConversationResumeVerdict.NotAnOrdinaryConversation;

         if (string.IsNullOrWhiteSpace(facts.OpeningWithHeroId) || facts.OpeningWithHeroId != facts.KeptForHeroId)
            return ConversationResumeVerdict.SomebodyElse;

         // Not "later than", because a loaded save can hand back an earlier day. The conversation belongs to ONE
         // day and any other day is a day it is not being had on.
         if (facts.Today != facts.KeptOnDay) return ConversationResumeVerdict.ANewDay;

         return ConversationResumeVerdict.Resume;
      }
   }
}
