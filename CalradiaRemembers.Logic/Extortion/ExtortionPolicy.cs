// Code written by Gabriel Mailhot, 01/11/2026.
// Gabriel, 23/10/2026, for the Misdeeds page: "on pourrait aussi ajouter des trahisons, extortions, et tout autres gestes qui
// pourraient etres classes comme misdeeds". Extortion could not be listed because the player could not do it. His "go" of
// 01/11/2026 took this shape: a threat with a blade is already rob_npc, so extortion is a hold the GAME can verify, a secret
// the player really knows or lands they could really burn, and the model is told it and never invents one. The answer is
// drawn before the character speaks (gating before declaration), so they do not agree in words to what the game then
// refuses. The figures are mine, to tune in play.

#region

using System;
using CalradiaRemembers.Logic.Violence;

#endregion

namespace CalradiaRemembers.Logic.Extortion
{
   /// <summary>What the player holds over someone. Saved by name, so the order may change.</summary>
   public enum ExtortionLever
   {
      /// <summary>Nothing the game can verify.</summary>
      None,

      /// <summary>A hidden affair the player really knows of.</summary>
      Secret,

      /// <summary>A village of theirs within reach of men they cannot stop.</summary>
      Lands
   }

   /// <summary>What decides whether the player has a hold over someone.</summary>
   public sealed class ExtortionLeverFacts
   {
      /// <summary>The player really knows of a hidden affair of theirs.</summary>
      public bool SecretIsKnown { get; init; }

      /// <summary>A village of their house lies within reach of the player's party.</summary>
      public bool VillageInReach { get; init; }

      /// <summary>The healthy men in the player's party.</summary>
      public int PlayerMen { get; init; }

      /// <summary>The healthy men at their back.</summary>
      public int TheirMen { get; init; }
   }

   /// <summary>What decides whether someone gives in.</summary>
   public sealed class ExtortionResistanceFacts
   {
      /// <summary>The hold in play.</summary>
      public ExtortionLever Lever { get; init; }

      /// <summary>The player's Roguery.</summary>
      public int PlayerRoguery { get; init; }

      /// <summary>The healthy men in the player's party.</summary>
      public int PlayerMen { get; init; }

      /// <summary>The healthy men at their back.</summary>
      public int TheirMen { get; init; }

      /// <summary>Their Honor trait, -2 to 2.</summary>
      public int Honor { get; init; }

      /// <summary>How many tiers their house stands above the player's; negative below.</summary>
      public int TiersAbove { get; init; }

      /// <summary>They rule a realm.</summary>
      public bool IsRuler { get; init; }
   }

   /// <summary>What one attempt costs the player.</summary>
   public sealed class ExtortionCost
   {
      /// <summary>Crime charged by their realm.</summary>
      public int CrimeRating { get; init; }

      /// <summary>How far their regard for the player falls.</summary>
      public int RelationHit { get; init; }

      /// <summary>The grudge they keep.</summary>
      public int GrudgeMagnitude { get; init; }
   }

   /// <summary>Decides who can be extorted, whether they give in, how much they pay, and what it costs.</summary>
   public static class ExtortionPolicy
   {
      /// <summary>Men in the party before a threat to lands means anything.</summary>
      public const int FewestMen = 30;

      /// <summary>How many of the player's men to one of theirs make a threat to their lands real.</summary>
      public const int MenToOne = 2;

      /// <summary>The share of a purse one extortion takes, in percent.</summary>
      public const int PurseSharePercent = 20;

      /// <summary>The smallest sum worth a threat: a poorer purse is not asked.</summary>
      public const int FewestDenars = 100;

      /// <summary>How long one victim stays out of reach after an attempt: no milking a purse day after day.</summary>
      public const int DaysBetweenAttempts = 30;

      /// <summary>How long coin paid for lands binds the player not to raid them.</summary>
      public const int DaysOfTheBargain = 30;

      /// <summary>The chance at equal footing, in percent.</summary>
      public const int EvenChance = 50;

      /// <summary>What a known secret adds to it.</summary>
      public const int SecretBonus = 10;

      /// <summary>The most a gap in men past the bar adds to a threat to lands.</summary>
      public const int MenBonusCap = 20;

      /// <summary>What each level of Honor takes off: an honest house refuses to be bled.</summary>
      public const int HonorPenalty = 10;

      /// <summary>What each tier of their house above the player's takes off.</summary>
      public const int TierPenalty = 5;

      /// <summary>What a crowned head takes off.</summary>
      public const int RulerPenalty = 15;

      /// <summary>The grudge of someone who paid.</summary>
      public const int PaidGrudge = 30;

      /// <summary>What each earlier attempt on the same person adds to the regard lost, in percent.</summary>
      public const int RepeatPercent = 50;

      /// <summary>What each earlier attempt adds to the grudge.</summary>
      public const int RepeatGrudge = 10;

      /// <summary>The most an extortion's grudge can grow.</summary>
      public const int MostGrudge = 60;

      /// <summary>Crime charged for a threat to lands; a secret's victim cannot complain without telling it.</summary>
      public const int LandsCrime = 5;

      /// <summary>The grudge left by that.</summary>
      public const int BrokenBargainGrudge = 50;

      /// <summary>The hold the player has, a secret first since it needs no army.</summary>
      public static ExtortionLever Lever(ExtortionLeverFacts facts)
      {
         if (facts == null) return ExtortionLever.None;
         if (facts.SecretIsKnown) return ExtortionLever.Secret;

         bool outmatched = facts.PlayerMen >= FewestMen && facts.PlayerMen >= facts.TheirMen * MenToOne;

         return facts.VillageInReach && outmatched ? ExtortionLever.Lands : ExtortionLever.None;
      }

      /// <summary>The most one extortion takes from <paramref name="purse" />.</summary>
      public static int MostTaken(int purse)
      {
         if (purse <= 0) return 0;

         return Math.Min((int) (purse * (long) PurseSharePercent / 100), GoldTransferLimit.MaxSingleTransfer);
      }

      /// <summary>True when this purse is worth a threat.</summary>
      public static bool IsWorthAsking(int purse) => MostTaken(purse) >= FewestDenars;

      /// <summary>What is really paid: the sum asked within the most taken, the whole of it when none was named.</summary>
      public static int Take(int? asked, int purse)
      {
         int most = MostTaken(purse);

         return asked is > 0 ? Math.Min(asked.Value, most) : most;
      }

      /// <summary>True when this person can be pressed again.</summary>
      public static bool MayPressAgain(int? lastDay, int today) => lastDay == null || today - lastDay.Value >= DaysBetweenAttempts;

      /// <summary>The chance they give in, in percent.</summary>
      public static int ChanceToYield(ExtortionResistanceFacts facts)
      {
         if (facts == null) return ViolencePolicy.MinChancePercent;

         int chance = EvenChance + Math.Min(Math.Max(facts.PlayerRoguery, 0) / ViolencePolicy.RogueryDivisor, ViolencePolicy.RogueryBonusCap);
         chance += facts.Lever == ExtortionLever.Secret ? SecretBonus : MenBonus(facts);
         chance -= facts.Honor * HonorPenalty;
         chance -= Math.Max(facts.TiersAbove, 0) * TierPenalty;
         if (facts.IsRuler) chance -= RulerPenalty;

         return Math.Max(ViolencePolicy.MinChancePercent, Math.Min(ViolencePolicy.MaxChancePercent, chance));
      }

      /// <summary>True when a roll in [0, 100) gives in at this chance.</summary>
      public static bool Yields(int chance, int roll) => roll < chance;

      /// <summary>What an attempt costs. Refused, it still costs a share: they know what the player tried.</summary>
      public static ExtortionCost Cost(ExtortionLever lever, bool paid, int earlierAttempts)
      {
         int earlier = Math.Max(earlierAttempts, 0);
         double share = paid ? 1.0 : ViolenceConsequencePolicy.FailedShare;
         int hit = DeedRegardPolicy.ExtortedVictim * (100 + RepeatPercent * earlier) / 100;
         int grudge = Math.Min(PaidGrudge + RepeatGrudge * earlier, MostGrudge);

         return new ExtortionCost {
            CrimeRating = lever == ExtortionLever.Lands ? (int) Math.Round(LandsCrime * share) : 0,
            RelationHit = (int) Math.Round(hit * share),
            GrudgeMagnitude = (int) Math.Round(grudge * share)
         };
      }

      // A threat past the bar weighs more the further past it: 2 to 1 adds nothing, 4 to 1 and up adds the whole bonus.
      private static int MenBonus(ExtortionResistanceFacts facts)
      {
         int theirs = Math.Max(facts.TheirMen, 1);
         int past = facts.PlayerMen * 10 / theirs - MenToOne * 10;

         return Math.Max(0, Math.Min(past, MenBonusCap));
      }
   }
}
