// Code written by Gabriel Mailhot, 16/11/2026.
// A warmth is dropped as "already paid by the quest" only when a quest reward is really paid.
//
// M-R3 (regard audit): a quest completed in a turn pays its own fixed, verified relation reward, so a positive
// change_relation the model also writes for "completing the task" would count the same deed twice; it was dropped on
// every turn whose reply CLAIMED a completion. Aporo (Discord, 16/11/2026, Queen Rhagaea's garrison task, log
// 07:40:28 and 07:47:30): the claim was refused ("no verified quest to match"), no reward was paid, and the warmth was
// dropped all the same: the player lost both. Gabriel (16/11/2026): "go".
//
// So the warmth is dropped only when the claim will be paid (a task of this character's awaits its reward and the
// claim names it, or names no type) or when a hand-over in the same reply settles a task and pays its reward itself.

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>The facts about a positive change_relation written on a turn that claims a task is done.</summary>
   public sealed class QuestClaimRegardFacts
   {
      /// <summary>The reply claims one of this character's tasks is complete ([QUEST_COMPLETE]).</summary>
      public bool CompletionClaimed { get; init; }

      /// <summary>The game will pay that claim: a task of theirs is verified done and awaits its reward, and the claim matches it.</summary>
      public bool ClaimWillBePaid { get; init; }

      /// <summary>A hand-over in the same reply (reinforce_garrison, deliver_recruits) settles a task and pays its reward itself.</summary>
      public bool HandOverSettlesATask { get; init; }

      /// <summary>The change_relation's delta.</summary>
      public int Delta { get; init; }
   }

   /// <summary>Whether a warmth is the same goodwill a quest reward of the same turn already pays.</summary>
   public static class QuestClaimRegardPolicy
   {
      /// <summary>True when the warmth should be dropped: a claimed completion whose reward the game really pays this turn.</summary>
      public static bool IsPaidByTheQuestReward(QuestClaimRegardFacts f)
         => f != null && f.Delta > 0 && f.CompletionClaimed && (f.ClaimWillBePaid || f.HandOverSettlesATask);
   }
}
