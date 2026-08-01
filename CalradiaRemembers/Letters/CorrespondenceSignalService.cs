// Code written by Gabriel Mailhot, 01/08/2026.
// Extracts the single seam that applies a stated CorrespondencePreference to a profile's running interest
// dampers. Before this, the parse-then-apply logic lived inline inside GeneratePlayerLetterReplyAsync's
// mutation closure, reachable only by actually sending a reply through the LLM. Pulling it out means the
// "player asks to stop writing -> weight drops" chain can be exercised by the SAME code in game and in the
// mechanical self-test (cr.testall's letters_mailbox), rather than the test re-implementing (and risking
// drifting from) what the reply path does.

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Applies a stated <see cref="CorrespondencePreference" /> to an NPC's running correspondence-interest
   ///   dampers, in memory only. The single seam both <see cref="LetterGenerationService.GeneratePlayerLetterReplyAsync" />
   ///   and the <c>letters_mailbox</c> self-test go through, so the effect of a player asking to hear more or
   ///   less from a correspondent is proven identical in a live reply and in a mechanical test.
   /// </summary>
   public static class CorrespondenceSignalService
   {
      /// <summary>
      ///   Applies <paramref name="pref" />'s <see cref="CorrespondenceInterestPolicy.DeltasFor" /> deltas onto
      ///   <paramref name="profile" />'s sender interest and, when <paramref name="evaluator" /> is given, onto
      ///   the subject (keyed by <paramref name="subjectReason" />) and global dampers too. Returns <c>false</c>
      ///   and mutates nothing when <paramref name="pref" />.Scope is <see cref="PreferenceScope.None" /> (no
      ///   preference was actually stated). Mutates <paramref name="profile" /> and <paramref name="evaluator" />
      ///   in memory only; the caller owns persisting the profile (<c>store.Set</c>) and the threading context
      ///   this runs on.
      /// </summary>
      public static bool ApplyPreference(NpcProfile profile, LetterTriggerEvaluator evaluator, CorrespondencePreference pref, LetterReason subjectReason)
      {
         if (pref.Scope == PreferenceScope.None) return false;

         InterestDeltas d = CorrespondenceInterestPolicy.DeltasFor(pref);

         profile.CorrespondenceInterest = CorrespondenceInterestPolicy.ClampSender(profile.CorrespondenceInterest + d.SenderDelta);

         if (evaluator != null)
         {
            evaluator.NudgeSubject(subjectReason, d.SubjectDelta);
            evaluator.NudgeGlobal(d.GlobalDelta);
         }

         return true;
      }
   }
}
