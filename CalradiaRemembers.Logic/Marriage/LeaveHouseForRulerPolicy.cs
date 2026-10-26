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
   /// <summary>
   ///   How the player leaves their house for her (Gabriel, 26/10/2026). Through the heir: the heir takes the house and
   ///   becomes the played character. Giving her all: the player keeps their character and their family, and brings
   ///   her their fiefs, their gold and, if they rule one, their realm; the road when there is no heir.
   /// </summary>
   public enum LeaveHouseRoad
   {
      /// <summary>The heir takes up the house and the player plays on as the heir; the old character weds her.</summary>
      ThroughTheHeir,

      /// <summary>The player keeps their character and family, gives her fiefs, gold and realm, and weds her.</summary>
      GiveHerAll
   }

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
      /// <summary>The road asked about: an heir is needed only to go through the heir.</summary>
      public LeaveHouseRoad Road { get; init; } = LeaveHouseRoad.ThroughTheHeir;

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

   /// <summary>One engine step of giving her all, in the order it must run.</summary>
   public enum GiveHerAllStep
   {
      /// <summary>An army the player leads is disbanded.</summary>
      DisbandTheArmy,

      /// <summary>A realm the player rules: its other houses join hers, its armies disband.</summary>
      AbsorbTheRealm,

      /// <summary>Every town and castle of the player's house is given to her (villages follow).</summary>
      GiveTheFiefs,

      /// <summary>The player's gold, which is the house's, is given to her.</summary>
      GiveTheGold,

      /// <summary>The player's house joins her realm.</summary>
      JoinHerRealm,

      /// <summary>The influence the game wipes on a change of realm is given back.</summary>
      RestoreTheInfluence,

      /// <summary>The player weds her, each keeping their own house.</summary>
      Wed,

      /// <summary>She gives him a season of his house's wages.</summary>
      SeasonOfWages,

      /// <summary>The rival realms of her people may declare war on the union.</summary>
      TheRivalsAnswer
   }

   /// <summary>Who a newborn's parents are, for the house the child belongs to.</summary>
   public sealed class UnionChildFacts
   {
      /// <summary>The mother is the player's wife.</summary>
      public bool MotherIsThePlayersWife { get; init; }

      /// <summary>She belongs to a house other than the player's (a ruler wed by giving her all keeps her own).</summary>
      public bool MotherKeepsHerOwnHouse { get; init; }
   }

   /// <summary>What the player brings her, which decides whether her people's rivals answer with war.</summary>
   public sealed class UnionFacts
   {
      /// <summary>The player rules a realm, which is absorbed into hers.</summary>
      public bool BringsARealm { get; init; }

      /// <summary>The player's house leaves a realm of her own people, a rival claimant to her throne.</summary>
      public bool DefectsFromARival { get; init; }

      /// <summary>The towns and castles the player's house gives her.</summary>
      public int FiefsBrought { get; init; }
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
         if (f.Road == LeaveHouseRoad.ThroughTheHeir && !f.HasAnHeir) return LeaveHouseBar.NoHeir;
         if (f.Road == LeaveHouseRoad.ThroughTheHeir && !f.HeirRidesWithThePlayer) return LeaveHouseBar.HeirIsNotAtTheirSide;
         if (f.ArmyOrBattle) return LeaveHouseBar.ArmyOrBattle;
         if (!f.TheGameWouldWedThemAfter) return LeaveHouseBar.TheGameWouldRefuse;

         return LeaveHouseBar.None;
      }

      /// <summary>
      ///   Giving her all, in order. The fiefs pass to her and the realm's other houses to her realm BEFORE the player's
      ///   house joins it: a house that leaves its realm first hands every fief to that realm's ruler. The wedding comes
      ///   once the house is hers, so a king who gave her his crown no longer reads as one. The influence the move wipes
      ///   is given back (Gabriel, 26/10/2026), and her season of wages is paid once all is hers to give.
      /// </summary>
      public static IReadOnlyList<GiveHerAllStep> GiveHerAllSteps { get; } = new[] {
         GiveHerAllStep.DisbandTheArmy,
         GiveHerAllStep.AbsorbTheRealm,
         GiveHerAllStep.GiveTheFiefs,
         GiveHerAllStep.GiveTheGold,
         GiveHerAllStep.JoinHerRealm,
         GiveHerAllStep.RestoreTheInfluence,
         GiveHerAllStep.Wed,
         GiveHerAllStep.SeasonOfWages,
         GiveHerAllStep.TheRivalsAnswer
      };

      /// <summary>The days her season of wages covers (Gabriel, 26/10/2026: "une saison de solde").</summary>
      public const int SeasonDays = 60;

      /// <summary>Her gift to keep his house's troops paid a season: every party of the house, not his alone.</summary>
      public static int SeasonOfWages(int houseDailyWages) => houseDailyWages <= 0 ? 0 : houseDailyWages * SeasonDays;

      /// <summary>
      ///   The chance, out of 100, that the rival realms of her people declare war on the union (Gabriel, 26/10/2026: the
      ///   two other empires against the Southern Empire; it "depends" on what the player brings). A realm handed to
      ///   her, or a house that defects from a rival of her people, is near certain; otherwise each fief raises it; a
      ///   landless house alarms nobody.
      /// </summary>
      public static int WarChance(UnionFacts f)
      {
         if (f == null) return 0;
         if (f.BringsARealm || f.DefectsFromARival) return NearCertainWar;

         return f.FiefsBrought <= 0 ? 0 : System.Math.Min(MostLikelyWarFromFiefs, f.FiefsBrought * WarChancePerFief);
      }

      /// <summary>
      ///   A child of the player and a wife who kept her own house belongs to HER line (Gabriel, 26/10/2026: "enfants dans sa
      ///   lignee"), though the game puts every child of the player in the player's house.
      /// </summary>
      public static bool ChildBelongsToHerHouse(UnionChildFacts f) => f != null && f.MotherIsThePlayersWife && f.MotherKeepsHerOwnHouse;

      private const int NearCertainWar = 90;
      private const int WarChancePerFief = 20;
      private const int MostLikelyWarFromFiefs = 80;

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
