// Code written by Gabriel Mailhot, 08/10/2026.
// What a character's reply to the player's letter is about (IdentityCrysys, Nexus, 08/10/2026: once the player answered
// her "why no reply?" follow-up, every reply after it was another "why no reply?"). A reply carries on the occasion of
// the letter it answers, except the follow-up itself: it is only a nudge, and the thread it nudged is a romantic one.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The occasion of a reply. Pure.</summary>
   public static class LetterReplyReason
   {
      /// <summary>The reason a reply to a letter of <paramref name="answered" /> carries.</summary>
      public static LetterReason Of(LetterReason answered)
         // The awaiting-reply follow-up is only ever sent for a romantic letter left unanswered (LetterTriggerEvaluator).
         => answered == LetterReason.AwaitingReply ? LetterReason.RomanticCorrespondence : answered;
   }
}
