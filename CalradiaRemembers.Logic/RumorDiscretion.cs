// Code written by Gabriel Mailhot, 03/10/2026.
// IdentityCrysys (Nexus, 03/10/2026): a way to keep a world event from a character, or from everyone but those it
// involves. Hearing is by score (RumorAwareness), never by a stored list; this is the one veto on top of the score,
// set by the player through cr.world_event_secret / cr.world_event_hide.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What a discretion check is judged on.</summary>
   public sealed class DiscretionFacts
   {
      /// <summary>The hero asking; null for a settlement's common talk.</summary>
      public string ObserverId { get; init; }

      /// <summary>The event is kept to those it names.</summary>
      public bool KnownOnlyToThoseInvolved { get; init; }

      /// <summary>The heroes the event names (its actors and those it harmed).</summary>
      public IEnumerable<string> InvolvedIds { get; init; }

      /// <summary>Heroes the player chose to keep unaware.</summary>
      public IEnumerable<string> HiddenFromIds { get; init; }
   }

   /// <summary>Whether a world event is kept from someone whatever their score. Pure.</summary>
   public static class RumorDiscretion
   {
      /// <summary>Why the event is withheld from this observer, or null when it is not.</summary>
      public static string WithheldReason(DiscretionFacts f)
      {
         if (f == null) return null;

         if (f.ObserverId != null && f.HiddenFromIds != null && f.HiddenFromIds.Contains(f.ObserverId))
            return "Hidden from this character by the player.";

         if (f.KnownOnlyToThoseInvolved && (f.ObserverId == null || f.InvolvedIds == null || !f.InvolvedIds.Contains(f.ObserverId)))
            return "Kept secret: known only to those it names.";

         return null;
      }
   }
}
