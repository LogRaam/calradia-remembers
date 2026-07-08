// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 4 (SPARE + TURN / recruit): DESIGN PROPOSAL, the pure gate ONLY. The wiring (the
// captor-scene offer, the bandit-to-clan conversion, the stance turn, the companion recruit) is deliberately
// NOT built: the roadmap requires the turn gate to be designed and ratified before building, and this is that
// gate, test-first, so Gabriel can approve or retune the thresholds before any engine work.
//
// The fiction: you have beaten the nemesis and hold him at your mercy. Rather than kill him, you offer to
// spare him and take him into your service. He turns ONLY when all of the following hold, else he refuses
// (and would rather die than grovel or serve the one he hates):
//   1. He FEARS you enough that death is a real, present thing (a captor he does not fear has no reason to bend).
//   2. He RESPECTS you enough that serving you is not shameful (you beat him as a worthy foe, not by luck).
//   3. His NATURE is a pragmatic survivor's, not a fanatic's: a brave or honor-bound man dies free, UNLESS he
//      is also a cold calculator who will simply switch sides when the arithmetic says so.
//   4. His HATRED of you is not an unbreakable vendetta, UNLESS your sheer dominance (fear and respect well
//      above their floors) overcomes even a deep grudge.
//
// All thresholds below are TUNING and flagged for ratification. Trait levels are the vanilla -2..+2 scale;
// Fear/Respect/GrudgeMagnitude are 0..100. A typical minted nemesis (low Honor, some Calculating, e.g. the
// "Corrupted Patron" archetype) is turnable once cowed and respectful; a rare high-Valor or honor-bound one
// never bends to a captor.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a nemesis refuses to turn, or <see cref="None" /> when he accepts.</summary>
   public enum NemesisTurnRefusal
   {
      None,
      FearsYouTooLittle,
      RespectsYouTooLittle,
      TooDefiant,
      HatesYouTooMuch
   }

   /// <summary>The facts the turn gate reasons over: the nemesis's stance toward the player, his grudge, his traits.</summary>
   public readonly struct NemesisTurnFacts
   {
      public NemesisTurnFacts(int fear, int respect, int grudgeMagnitude, int honor, int valor, int calculating)
      {
         Fear = fear;
         Respect = respect;
         GrudgeMagnitude = grudgeMagnitude;
         Honor = honor;
         Valor = valor;
         Calculating = calculating;
      }

      /// <summary>0..100 stance: how much the nemesis fears the player (death feels real).</summary>
      public int Fear { get; }

      /// <summary>0..100 stance: how much the nemesis respects the player (serving is not shameful).</summary>
      public int Respect { get; }

      /// <summary>0..100: the nemesis's own grudge against the player (his hatred).</summary>
      public int GrudgeMagnitude { get; }

      /// <summary>Vanilla trait level (-2..+2). High Honor: he dies free rather than serve his captor.</summary>
      public int Honor { get; }

      /// <summary>Vanilla trait level (-2..+2). High Valor: defiant, would rather die than bend.</summary>
      public int Valor { get; }

      /// <summary>Vanilla trait level (-2..+2). High Calculating: a survivor who switches sides on the arithmetic.</summary>
      public int Calculating { get; }
   }

   /// <summary>The gate's verdict: whether the nemesis turns, and if not, why.</summary>
   public readonly struct NemesisTurnVerdict
   {
      public NemesisTurnVerdict(bool canTurn, NemesisTurnRefusal refusal)
      {
         CanTurn = canTurn;
         Refusal = refusal;
      }

      public bool CanTurn { get; }
      public NemesisTurnRefusal Refusal { get; }
   }

   /// <summary>
   ///   The pure gate deciding whether a beaten, held nemesis will accept being spared and taken into the
   ///   player's service. Pure and unit-tested; the host sources the facts (stance, grudge, traits) and, on a
   ///   turn, performs the recruit + bandit-to-clan conversion. See the file header for the design.
   /// </summary>
   public static class NemesisTurnPolicy
   {
      /// <summary>TUNE: the nemesis must fear the player at least this much (death must feel real to him).</summary>
      public const int FearFloor = 40;

      /// <summary>TUNE: the nemesis must respect the player at least this much (serving must not be shameful).</summary>
      public const int RespectFloor = 25;

      /// <summary>TUNE: a Valor at or above this leans the nemesis toward dying free rather than serving.</summary>
      public const int DefiantValorThreshold = 1;

      /// <summary>TUNE: an Honor at or above this also leans him toward defiance (an honor-bound man does not grovel).</summary>
      public const int DefiantHonorThreshold = 1;

      /// <summary>TUNE: a Calculating at or above this makes him a pragmatic side-switcher, overriding defiance.</summary>
      public const int PragmatistCalculatingThreshold = 1;

      /// <summary>TUNE: a grudge at or above this normally blocks the turn (his hatred runs too deep).</summary>
      public const int GrudgeCeiling = 60;

      /// <summary>
      ///   TUNE: the combined margin, above the two stance floors, at which sheer dominance overcomes even a
      ///   grudge past <see cref="GrudgeCeiling" />: (Fear - FearFloor) + (Respect - RespectFloor) at or above this.
      /// </summary>
      public const int DominanceOvercomesHatredMargin = 55;

      /// <summary>Resolves whether the held nemesis turns to the player's service, or refuses (and why).</summary>
      public static NemesisTurnVerdict Resolve(NemesisTurnFacts f)
      {
         if (f.Fear < FearFloor) return Refuse(NemesisTurnRefusal.FearsYouTooLittle);
         if (f.Respect < RespectFloor) return Refuse(NemesisTurnRefusal.RespectsYouTooLittle);

         // A brave or honor-bound nemesis dies free rather than serve his captor, UNLESS he is also a cold
         // calculator who will simply switch sides when the arithmetic favours it.
         bool principled = f.Valor >= DefiantValorThreshold || f.Honor >= DefiantHonorThreshold;
         bool pragmatist = f.Calculating >= PragmatistCalculatingThreshold;
         if (principled && !pragmatist) return Refuse(NemesisTurnRefusal.TooDefiant);

         // A deep grudge blocks the turn, unless your dominance (fear and respect well past their floors)
         // overcomes even his hatred.
         int dominanceMargin = (f.Fear - FearFloor) + (f.Respect - RespectFloor);
         bool dominanceOvercomes = dominanceMargin >= DominanceOvercomesHatredMargin;
         if (f.GrudgeMagnitude >= GrudgeCeiling && !dominanceOvercomes) return Refuse(NemesisTurnRefusal.HatesYouTooMuch);

         return new NemesisTurnVerdict(true, NemesisTurnRefusal.None);
      }

      private static NemesisTurnVerdict Refuse(NemesisTurnRefusal reason) => new NemesisTurnVerdict(false, reason);
   }
}
