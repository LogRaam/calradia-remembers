// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design [[project_npc_recent_matter]] phase 3b): a lord NEVER deduces who a secret lover is,
// he only ever learns it because the PLAYER tells him. This pure policy decides, given who the player is secretly
// a lover of and who the player is speaking with, whether admitting the bond to this NPC would actually reveal
// anything (so the admit_secret_lover verb is taught/executable only when there is a genuine secret to confess),
// and to WHICH subjects the admission applies. The engine (verb + knowledge ledger) does the recording; nothing
// here mutates state.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Decides whether the player admitting a secret-lover bond to a given NPC reveals anything, and about whom.</summary>
   public static class SecretLoverAdmissionPolicy
   {
      /// <summary>
      ///   True when the player has at least one secret-lover bond this NPC does not already embody: the player is
      ///   the hidden lover of someone OTHER than the NPC being spoken to. Confessing your bond to the very person
      ///   you secretly love tells them nothing they do not already know, so it is never taught there.
      /// </summary>
      public static bool CanAdmitTo(IReadOnlyCollection<string> secretLoverSubjectIds, string npcId)
         => SubjectsRevealedTo(secretLoverSubjectIds, npcId).Count > 0;

      /// <summary>
      ///   The subset of the player's secret-lover subjects whose identity this NPC would newly learn if the player
      ///   admitted the bond now: every subject that is not the NPC themselves. The knowledge ledger records the NPC
      ///   as knowing each of these.
      /// </summary>
      public static List<string> SubjectsRevealedTo(IReadOnlyCollection<string> secretLoverSubjectIds, string npcId)
      {
         var revealed = new List<string>();
         if (secretLoverSubjectIds == null) return revealed;

         foreach (string id in secretLoverSubjectIds)
            if (!string.IsNullOrEmpty(id) && id != npcId) revealed.Add(id);

         return revealed;
      }
   }
}
