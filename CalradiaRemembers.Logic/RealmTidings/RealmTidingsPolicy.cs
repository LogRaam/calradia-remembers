// Code written by Gabriel Mailhot, 08/08/2026.
// Realm-tidings feature, Increment B: choosing WHICH known world event a friendly writer should relay in an
// unbidden "news of the realm" letter. The LLM only ever RELAYS a real event, never invents one, so the whole
// honesty of the feature rests on this pure pick: it may return only a genuine, worth-telling event the writer
// has not already relayed, or nothing at all. Three floors live here, all deterministic and test-covered: the
// per-NPC non-repeat (an NPC never relays the same event twice), the no-trivial-spam floor (gossip below a
// salience worth a friend's ink is silence), and the honest-silence default (no fresh news = write no letter).
// The mod owns the weekly tick, the salience computation, and the per-NPC already-sent ledger; this class only
// reasons over plain facts so the selection stays a one-line, test-covered decision.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.RealmTidings
{
   /// <summary>
   ///   Pure subject-selection for a realm-tidings letter: given the world events a writer could relay and the
   ///   ones they have already relayed, picks the single most worth-telling fresh event, or nothing. Deterministic
   ///   given its inputs so the behaviour is stable and testable; never throws on null or empty input.
   /// </summary>
   public static class RealmTidingsPolicy
   {
      /// <summary>
      ///   The floor a candidate's salience must reach before a friend would bother writing about it. Below this,
      ///   the news is gossip too trivial for a friend to spend a letter on, so it is left unsaid rather than
      ///   spamming the player with realm minutiae. A small positive floor, deliberately owned here as a tuning knob.
      /// </summary>
      public const int MinSalience = 10;

      /// <summary>
      ///   One world event a writer could relay: its id, and a caller-computed salience folding the event's
      ///   magnitude and how confidently the writer knows it. The policy treats <see cref="Salience" /> as an
      ///   opaque higher-is-more-worth-relaying score; all the engine-coupled magnitude/confidence math lives in
      ///   the mod so this pick stays pure and testable.
      /// </summary>
      public readonly struct RealmTidingsCandidate
      {
         public string EventId { get; }
         public int Salience { get; }

         public RealmTidingsCandidate(string eventId, int salience)
         {
            EventId = eventId;
            Salience = salience;
         }
      }

      /// <summary>
      ///   Picks the <see cref="RealmTidingsCandidate.EventId" /> of the most salient candidate that is worth
      ///   telling and that this writer has not already relayed: salience at or above <see cref="MinSalience" />
      ///   (the no-trivial-spam floor) and an id not present in <paramref name="alreadySentEventIds" /> (the
      ///   per-NPC non-repeat floor). Returns null when none qualify, which the caller reads as "write no letter
      ///   this time" (the honest silence: no fresh, worth-telling news beats inventing something to say). The
      ///   tie-break is deterministic: highest salience first, then the ordinal-lowest EventId, so the choice is
      ///   stable and reproducible. Null or empty inputs return null rather than throwing.
      /// </summary>
      public static string SelectSubject(IReadOnlyList<RealmTidingsCandidate> candidates, IReadOnlyCollection<string> alreadySentEventIds)
      {
         if (candidates == null || candidates.Count == 0) return null;

         HashSet<string> alreadySent = alreadySentEventIds == null
                                          ? new HashSet<string>()
                                          : new HashSet<string>(alreadySentEventIds);

         RealmTidingsCandidate? best = candidates
                                       .Where(c => c.EventId != null
                                                   && c.Salience >= MinSalience
                                                   && !alreadySent.Contains(c.EventId))
                                       .OrderByDescending(c => c.Salience)
                                       .ThenBy(c => c.EventId, System.StringComparer.Ordinal)
                                       .Cast<RealmTidingsCandidate?>()
                                       .FirstOrDefault();

         return best?.EventId;
      }
   }
}
