// Code written by Gabriel Mailhot, 26/10/2026.
// A ruler who heads her house never gives it up to wed the player (Gabriel, 26/10/2026: "Jamais elle laissera son
// trone"), and the game weds no two heads of houses. So the player may leave THEIR house for her: "le joueur aura le
// choix soit d'abandonner son personnage et continuer avec l'heritier [...]". This is that first road: the heir takes
// up the house and becomes the played character, and the old character, now one of the world's lords, weds her and
// joins her house. RaveN017Mod (Nexus, 26/10/2026) asked to marry Rhagaea and hit the silent refusal.
// It cannot be undone, so every bar the game would raise AFTER the hand-over is asked BEFORE it, and the order of the
// engine steps is stated here, where a test can hold it: wedding him while he is still the player would pull her into
// his clan and leave her empire without a ruler (DefaultMarriageModel.GetClanAfterMarriage favours the player).

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>What stands between the player and leaving their house for a ruler who heads hers.</summary>
   public enum LeaveHouseBar
   {
      /// <summary>Nothing: the road is open.</summary>
      None,

      /// <summary>The player is wed. Not this road (yet): she would never be a second wife.</summary>
      PlayerIsWed,

      /// <summary>Not two heads of houses: an ordinary marriage, or none, is the question here.</summary>
      NotTwoHeadsOfHouses,

      /// <summary>A war between their peoples, or a captive on either side.</summary>
      WarOrCaptivity,

      /// <summary>She does not hold the player dear enough to take them as a spouse.</summary>
      HerHeartIsNotWon,

      /// <summary>The house has no heir of age to take it up.</summary>
      NoHeir,

      /// <summary>The heir does not ride in the player's own party, where the hand-over must happen.</summary>
      HeirIsNotAtTheirSide,

      /// <summary>One of them is in an army or a battle, which the game's own wedding refuses.</summary>
      ArmyOrBattle,

      /// <summary>Another of the game's own marriage rules would refuse them even once the house is handed over.</summary>
      TheGameWouldRefuse
   }

   /// <summary>The live facts behind leaving one's house for a ruler.</summary>
   public sealed class LeaveHouseFacts
   {
      /// <summary>The player has a spouse.</summary>
      public bool PlayerIsWed { get; init; }

      /// <summary>Both she and the player head their houses (<see cref="HouseHeadMarriagePolicy" />).</summary>
      public bool TwoHeadsOfHouses { get; init; }

      /// <summary>Their peoples are at war, or either of them is a captive.</summary>
      public bool WarOrCaptivity { get; init; }

      /// <summary>Her regard for the player meets the marriage bar.</summary>
      public bool HerHeartIsWon { get; init; }

      /// <summary>The player's house has an heir of age (the game's own heir apparents).</summary>
      public bool HasAnHeir { get; init; }

      /// <summary>That heir rides in the player's own party.</summary>
      public bool HeirRidesWithThePlayer { get; init; }

      /// <summary>Either of them is in an army or a battle.</summary>
      public bool ArmyOrBattle { get; init; }

      /// <summary>Every other rule of the game's marriage model would let them wed once the player heads no house.</summary>
      public bool TheGameWouldWedThemAfter { get; init; }
   }

   /// <summary>One engine step of the hand-over, in the order it must run.</summary>
   public enum LeaveHouseStep
   {
      /// <summary>The player's own workshops pass to the heir.</summary>
      HandOverWorkshops,

      /// <summary>The house's caravans pass to the heir.</summary>
      HandOverCaravans,

      /// <summary>An army the player leads is disbanded.</summary>
      DisbandTheArmy,

      /// <summary>The heir becomes head of the house.</summary>
      HandOverTheHouse,

      /// <summary>The heir becomes the played character.</summary>
      HandOverThePlayer,

      /// <summary>The old character, now a lord of the world, weds her and joins her house.</summary>
      Wed,

      /// <summary>He rides to her side, so the game does not leave him wandering as a fugitive.</summary>
      JoinHer
   }

   /// <summary>Whether the player may leave their house for a ruler, and the order the hand-over runs in.</summary>
   public static class LeaveHouseForRulerPolicy
   {
      /// <summary>The first bar in the way, the most lasting first, or <see cref="LeaveHouseBar.None" />.</summary>
      public static LeaveHouseBar WhyNot(LeaveHouseFacts f)
      {
         if (f == null) return LeaveHouseBar.NotTwoHeadsOfHouses;
         if (f.PlayerIsWed) return LeaveHouseBar.PlayerIsWed;
         if (!f.TwoHeadsOfHouses) return LeaveHouseBar.NotTwoHeadsOfHouses;
         if (f.WarOrCaptivity) return LeaveHouseBar.WarOrCaptivity;
         if (!f.HerHeartIsWon) return LeaveHouseBar.HerHeartIsNotWon;
         if (!f.HasAnHeir) return LeaveHouseBar.NoHeir;
         if (!f.HeirRidesWithThePlayer) return LeaveHouseBar.HeirIsNotAtTheirSide;
         if (f.ArmyOrBattle) return LeaveHouseBar.ArmyOrBattle;
         if (!f.TheGameWouldWedThemAfter) return LeaveHouseBar.TheGameWouldRefuse;

         return LeaveHouseBar.None;
      }

      /// <summary>
      ///   The hand-over, in order. The house and the player pass to the heir BEFORE the wedding: wed first, and the game
      ///   would pull her into the player's clan, leaving her own without its head.
      /// </summary>
      public static IReadOnlyList<LeaveHouseStep> Steps { get; } = new[] {
         LeaveHouseStep.HandOverWorkshops,
         LeaveHouseStep.HandOverCaravans,
         LeaveHouseStep.DisbandTheArmy,
         LeaveHouseStep.HandOverTheHouse,
         LeaveHouseStep.HandOverThePlayer,
         LeaveHouseStep.Wed,
         LeaveHouseStep.JoinHer
      };
   }
}
