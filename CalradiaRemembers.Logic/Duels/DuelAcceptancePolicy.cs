// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0 (pure): once a challenge is admissible (DuelChallengePolicy said Eligible), does the
// NPC actually take the field? This is the heart of whether the feature feels alive. The ratified character
// of it (ROADMAP): Valour and Honour lean IN, a fearful Calculating slips OUT, and an affronted grudge-holder
// DEMANDS it. A duel is never casual, so the default, absent any real push, is to refuse: people do not fight
// for nothing. Two forces overpower ordinary caution: a live grudge a duel would answer (the honourable
// outlet for it), and being called out BEFORE WITNESSES (an honourable lord cannot refuse steel in front of a
// crowd without disgrace). We work in plain "leaning points" and expose the score so the curve is testable
// directly, the way GrudgeResolutionPolicy exposes its gift bar.

using CalradiaRemembers.Logic.Grudges;

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>The NPC's answer to an admissible challenge.</summary>
   public enum DuelResponse
   {
      /// <summary>They take the field.</summary>
      Accept,

      /// <summary>They decline. Before witnesses this carries a forfeit cost (see <see cref="DuelStakesPolicy" />).</summary>
      Refuse
   }

   /// <summary>Everything <see cref="DuelAcceptancePolicy.Decide" /> weighs. The host fills it from the NPC's traits, stance, and the situation.</summary>
   public sealed class DuelAcceptanceContext
   {
      /// <summary>The NPC's personality: Valour and Honour lean in, Calculating sharpens fear into an exit.</summary>
      public TraitFacts Traits { get; set; }

      /// <summary>The NPC's posture toward the player. Fear (of the player's prowess) is the axis that leans them out.</summary>
      public NpcStance Stance { get; set; }

      /// <summary>A live grudge against the player that winning (or simply fighting) this duel would answer.</summary>
      public bool AGrudgeDrivesIt { get; set; }

      /// <summary>The challenge was issued before at least one witness, so refusing it would be a public disgrace.</summary>
      public bool ChallengedBeforeWitnesses { get; set; }
   }

   /// <summary>
   ///   Decides whether an NPC accepts an admissible duel. Every constant is a tuning knob. The rule is a sum
   ///   of leanings: a grudge and an honourable public challenge push hard toward the field, bravery and
   ///   honour lean in, fear of the player leans out, and a calculating nature turns real fear into a
   ///   deliberate exit. When nothing pushes past the threshold, the answer is no: a duel is a grave thing,
   ///   not a default.
   /// </summary>
   public static class DuelAcceptancePolicy
   {
      /// <summary>Tuning: a live grudge a duel would answer. The single strongest pull toward the field ("let us settle this with steel").</summary>
      public const int GrudgeDrivesLean = 40;

      /// <summary>Tuning: leaning per Honour level (-2..+2). An honourable lord inclines to the field; a dishonourable one shirks it.</summary>
      public const int HonorLeanPerLevel = 8;

      /// <summary>Tuning: EXTRA leaning per Honour level when challenged before witnesses. Honour plus a crowd is nearly a compulsion; dishonour before a crowd still ducks.</summary>
      public const int WitnessHonorLeanPerLevel = 6;

      /// <summary>Tuning: leaning per Valour level (-2..+2). The brave do not refuse steel; the timid avoid it.</summary>
      public const int ValorLeanPerLevel = 7;

      /// <summary>Tuning: leaning OUT per point of Fear (0..100) of the player. At full terror this alone nearly forbids the fight.</summary>
      public const double FearLeanOutPerPoint = 0.4;

      /// <summary>Tuning: the Fear level at/above which a Calculating nature starts converting that fear into a calculated exit.</summary>
      public const int FearCowardiceThreshold = 40;

      /// <summary>Tuning: additional leaning OUT per positive Calculating level, but ONLY once fear clears the cowardice threshold. A calculating man who fears you finds the door; a fearless one does not need it.</summary>
      public const int CalculatingSlipPerLevel = 10;

      /// <summary>Tuning: the score at or above which the NPC accepts. Above zero on purpose, so a real reason is needed to fight at all.</summary>
      public const int AcceptThreshold = 10;

      /// <summary>The verdict. Accepts when the leaning score clears <see cref="AcceptThreshold" />, else refuses.</summary>
      public static DuelResponse Decide(DuelAcceptanceContext ctx)
         => Score(ctx) >= AcceptThreshold ? DuelResponse.Accept : DuelResponse.Refuse;

      /// <summary>
      ///   The raw leaning score behind <see cref="Decide" />, exposed so tests (and any future "will he fight
      ///   me?" preview) can assert the curve directly instead of reverse-engineering it from the verdict. A
      ///   null context leans nowhere and scores zero (a refusal, below the threshold).
      /// </summary>
      public static int Score(DuelAcceptanceContext ctx)
      {
         if (ctx == null) return 0;

         int honor = ctx.Traits?.Honor ?? 0;
         int valor = ctx.Traits?.Valor ?? 0;
         int calculating = ctx.Traits?.Calculating ?? 0;
         int fear = ctx.Stance?.Fear ?? 0;
         if (fear < 0) fear = 0;

         double score = 0;

         if (ctx.AGrudgeDrivesIt) score += GrudgeDrivesLean;

         score += honor * HonorLeanPerLevel;
         score += valor * ValorLeanPerLevel;

         // Being called out in public turns honour into near-compulsion, and turns a dishonourable nature into
         // an even more visible duck. It only bites when witnesses are present.
         if (ctx.ChallengedBeforeWitnesses) score += honor * WitnessHonorLeanPerLevel;

         score -= fear * FearLeanOutPerPoint;

         // Cool calculation only becomes cowardice when there is something real to fear. A calculating man who
         // does not fear the player has no reason to slip out on that account.
         if (fear >= FearCowardiceThreshold && calculating > 0)
            score -= calculating * CalculatingSlipPerLevel;

         return (int) System.Math.Round(score);
      }
   }
}
