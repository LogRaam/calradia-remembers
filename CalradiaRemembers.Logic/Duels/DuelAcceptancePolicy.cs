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

      /// <summary>
      ///   How many steps of station the CHALLENGER stands BENEATH this NPC (0 = a peer or their better). A great
      ///   gulf is the honourable reason to decline: a king does not cross swords with a nobody who calls him out
      ///   (ratified 2026-08-26). The host derives it from rank (ruler / clan tier / renown); 0 when the challenger
      ///   is an equal or above, so an ordinary duel between peers is unaffected.
      /// </summary>
      public int ChallengerStationBelowNpc { get; set; }
   }

   /// <summary>
   ///   Decides whether an NPC accepts an admissible duel. Every constant is a tuning knob. A duel of honour is
   ///   ANSWERED by default (ratified 2026-08-26: a challenge should generally lead to a fight, the more so for an
   ///   honourable NPC), so the rule starts leaning IN. What overpowers that: real fear turned to a calculated
   ///   exit, a dishonourable and timid nature, or the challenger standing far beneath the NPC's station (a king
   ///   does not duel a nobody). A grudge and a public challenge push harder still toward the field.
   /// </summary>
   public static class DuelAcceptancePolicy
   {
      /// <summary>Tuning: the standing lean IN, so an ordinary duel of honour between peers is ACCEPTED by default; a real reason (fear, cowardice, a great station gap) is needed to refuse.</summary>
      public const int BaseAcceptLean = 20;

      /// <summary>Tuning: leaning OUT per step the challenger stands beneath the NPC's station. A great gulf (a nobody calling out a king) refuses outright, even for an honourable lord.</summary>
      public const int StationGapLeanPerStep = 15;

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

      /// <summary>Tuning: the score at or above which the NPC accepts. The <see cref="BaseAcceptLean" /> clears it on its own, so a peer duel of honour is accepted unless something real (fear, cowardice, a station gulf) drags the score under it.</summary>
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

         // A duel of honour is answered by default: start leaning IN, so a real reason is needed to refuse.
         double score = BaseAcceptLean;

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

         // The honourable refusal: a challenger far beneath the NPC's station is dismissed rather than fought
         // (a nobody who calls out a king). A great enough gulf overrides even honour's pull toward the field.
         int stationGap = ctx.ChallengerStationBelowNpc;
         if (stationGap > 0) score -= stationGap * StationGapLeanPerStep;

         return (int) System.Math.Round(score);
      }
   }
}
