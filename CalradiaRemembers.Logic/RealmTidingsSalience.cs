// Code written by Gabriel Mailhot, 08/08/2026.
// Realm-tidings feature, Increment D: folding a world event's raw MAGNITUDE and how SURELY the writer knows it
// (RumorConfidence) into the single "worth relaying" score RealmTidingsPolicy.SelectSubject ranks on. Kept here,
// pure and beside RumorConfidence (which also lives in .Logic), so the whole realm-tidings pick stays test-covered
// and free of engine coupling. The rule this pins: magnitude carries the news, but a faint rumour is worth less
// ink than a confirmed report of the same size, and a faint whisper of a trivial thing sinks below the policy's
// no-trivial-spam floor entirely (silence beats relaying gossip a friend is not even sure of).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Turns a world event's magnitude (how far the news carries, 0..100) and the writer's
   ///   <see cref="RumorConfidence" /> in it into the opaque salience score
   ///   <see cref="RealmTidingsPolicy.RealmTidingsCandidate" /> competes on. Higher is more worth relaying.
   ///   Deterministic and total: never throws, never returns negative, so a weekly tick over live data is safe.
   /// </summary>
   public static class RealmTidingsSalience
   {
      /// <summary>
      ///   Weight (percent of magnitude) kept for a barely-reached, doubtful rumour. Dampened on purpose, so a
      ///   faint whisper of a small event drops under <see cref="RealmTidingsPolicy.MinSalience" /> and is left
      ///   unsaid rather than relayed as gossip the writer is not even sure of.
      /// </summary>
      public const int DistantWeightPercent = 55;

      /// <summary>Weight kept for news that reached the writer at one remove ("word came that...").</summary>
      public const int SecondhandWeightPercent = 80;

      /// <summary>Weight kept for near / own-faction / major news the writer holds as good as confirmed: the full magnitude counts.</summary>
      public const int WellKnownWeightPercent = 100;

      /// <summary>
      ///   The salience of one relayable event: its <paramref name="magnitude" /> scaled by how surely the writer
      ///   knows it (<paramref name="confidence" />). A confirmed report counts for its full reach; a second-hand
      ///   one somewhat less; a distant, doubtful whisper least of all, so confidence both orders ties and can gate
      ///   a faint rumour of a trivial event out entirely. Magnitude is floored at zero so a malformed event never
      ///   yields a negative score.
      /// </summary>
      public static int Of(int magnitude, RumorConfidence confidence)
      {
         int mag = Math.Max(0, magnitude);

         return mag * WeightPercentFor(confidence) / 100;
      }

      private static int WeightPercentFor(RumorConfidence confidence)
      {
         switch (confidence)
         {
            case RumorConfidence.WellKnown:  return WellKnownWeightPercent;
            case RumorConfidence.Secondhand: return SecondhandWeightPercent;
            default:                         return DistantWeightPercent; // Distant, and any unknown value: treat as the faintest
         }
      }
   }
}
