// Code written by Gabriel Mailhot, 07/08/2026.
// Bounds a contact's retained correspondence so the byte[] JSON profile store (native-string size limit)
// cannot grow without limit in a very long, chatty relationship. Called at each delivery, once a letter
// has been marked delivered: the pure CalradiaRemembers.Logic.LetterArchivePolicy chooses the oldest
// DELIVERED letters to evict beyond the cap, and only those are removed here. In-flight, waylaid, and
// blank-placeholder letters are never eligible, so the delivery pipeline that owns them is untouched.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Enforces the per-contact retention cap on a profile's archived letters.</summary>
   public static class LetterArchiveRetention
   {
      /// <summary>
      ///   Drops the oldest delivered letters beyond <see cref="LetterArchivePolicy.PerContactCap" /> from the
      ///   profile's lists, in place. The caller persists the profile (this runs right before an existing
      ///   Store.Set at each delivery site). No-op when the contact is under the cap.
      /// </summary>
      public static void EnforceCap(NpcProfile profile)
      {
         if (profile == null) return;

         (IReadOnlyList<PendingLetter> incoming, IReadOnlyList<PlayerLetter> outgoing) =
            LetterArchivePolicy.SelectPrunable(profile.SentLetters, profile.ReceivedPlayerLetters,
               LetterArchivePolicy.PerContactCap);

         if (incoming.Count == 0 && outgoing.Count == 0) return;

         foreach (PendingLetter l in incoming) profile.SentLetters.Remove(l);
         foreach (PlayerLetter l in outgoing) profile.ReceivedPlayerLetters.Remove(l);

         ModLog.Info($"[LETTER ARCHIVE] pruned {incoming.Count + outgoing.Count} oldest letter(s) for {profile.Name} (cap {LetterArchivePolicy.PerContactCap}).");
      }
   }
}
