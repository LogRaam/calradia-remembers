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
      /// <summary>
      ///   The tag a secret event carries in a prompt line (Gabriel, 17/10/2026: "secret event devrait etre tagged
      ///   secret et ces secrets ne devraient etres connus que de ceux qui le connaissent"). Without it, one of the few
      ///   who know a secret reads it like common news and spreads it. It asks for discretion, never for denial: an
      ///   NPC may keep silent or stay vague, but does not lie.
      /// </summary>
      public const string SecretTag = "(a secret few share: speak of it only in confidence) ";

      /// <summary>The tag to put before a known event's line: <see cref="SecretTag" /> for a secret, empty otherwise.</summary>
      public static string TagFor(bool knownOnlyToThoseInvolved) => knownOnlyToThoseInvolved ? SecretTag : "";

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
