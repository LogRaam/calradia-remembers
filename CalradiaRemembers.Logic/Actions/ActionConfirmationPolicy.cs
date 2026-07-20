// Code written by Gabriel Mailhot, 12/07/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>
   ///   How the player answered a pending in-chat confirmation prompt.
   /// </summary>
   public enum ConfirmationAnswer
   {
      /// <summary>The player agreed (explicit "y"/"yes", or just pressed Enter, since Y is the default).</summary>
      Confirm,

      /// <summary>The player refused ("n"/"no").</summary>
      Decline,

      /// <summary>
      ///   The player typed something other than a yes/no answer, which reads as moving on to a fresh
      ///   message instead of settling the prompt. The pending action is declined so it never applies
      ///   unconfirmed.
      /// </summary>
      Recompose
   }

   /// <summary>
   ///   Pure policy for the terminal-style "confirm this action?" prompt shown in chat before a
   ///   consequential LLM action (moving coin or committing to a service) is applied. Decides which
   ///   action types need confirming, reads the player's typed answer, and builds the plain-language
   ///   question shown to them.
   /// </summary>
   public static class ActionConfirmationPolicy
   {
      /// <summary>
      ///   The plain-language confirmation question shown in chat for the pending action.
      ///   <paramref name="amount" /> is the coin count (ignored for actions that carry no amount, such as
      ///   "free_prisoner"); <paramref name="npcName" /> is the NPC's display name. Case-insensitive on
      ///   <paramref name="actionType" />; anything unrecognised falls back to a generic confirmation line.
      /// </summary>
      public static string BuildQuestion(string actionType, string npcName, int amount)
      {
         if (string.Equals(actionType, "give_gold", StringComparison.OrdinalIgnoreCase)) return $"Accept {amount} coins from {npcName}?";

         if (string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase)) return $"Give {npcName} {amount} coins?";

         if (string.Equals(actionType, "join_party", StringComparison.OrdinalIgnoreCase)) return $"Let {npcName} take service in your party for {amount} coins?";

         if (string.Equals(actionType, "free_prisoner", StringComparison.OrdinalIgnoreCase)) return $"Set {npcName} free?";

         return $"Confirm this with {npcName}?";
      }

      /// <summary>
      ///   Interprets the player's answer while a confirmation is pending. Trimmed, case-insensitive:
      ///   null or blank (just pressed Enter) and "y"/"yes" both count as <see cref="ConfirmationAnswer.Confirm" />
      ///   (Y is the default); "n"/"no" is <see cref="ConfirmationAnswer.Decline" />; anything else is
      ///   <see cref="ConfirmationAnswer.Recompose" />, since the player is typing a new message instead.
      /// </summary>
      public static ConfirmationAnswer ParseAnswer(string input)
      {
         string trimmed = input == null
            ? ""
            : input.Trim();

         if (trimmed.Length == 0 || string.Equals(trimmed, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)) return ConfirmationAnswer.Confirm;

         if (string.Equals(trimmed, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)) return ConfirmationAnswer.Decline;

         return ConfirmationAnswer.Recompose;
      }

      /// <summary>
      ///   True only for the action types the PLAYER spends or commits on, so those alone are confirmed:
      ///   "take_gold" (the player pays), "join_party" (the player hires and pays), and "free_prisoner"
      ///   (the player gives up a prisoner they hold, an irreversible commitment). Case-insensitive.
      ///   False for everything else, including "give_gold" (the NPC gives the player coin, which is the
      ///   NPC's initiative, not the player's, so it is never gated), null, and empty. Consequences the
      ///   player must not be able to veto (relation changes, grudges, and so on) are never routed here.
      /// </summary>
      public static bool RequiresConfirmation(string actionType)
      {
         if (string.IsNullOrEmpty(actionType)) return false;

         return string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase) || string.Equals(actionType, "join_party", StringComparison.OrdinalIgnoreCase) || string.Equals(actionType, "free_prisoner", StringComparison.OrdinalIgnoreCase);
      }
   }
}