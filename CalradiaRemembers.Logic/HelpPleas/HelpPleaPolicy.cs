// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): "Friends, relatives, and allies could write to the player when they need help: military
// (joining a war, sending troops), financial, or economic, depending on their situation, what the player has, and the
// relationship." Only the ruler's war duty asked anything of the player; a brother under siege had no way to. This decides
// who may write, for which crisis, what they ask for and what the player's answer is worth when the crisis closes.
// Gabriel, 01/11/2026 ("oui vas-y"), on Claude's two defaults: a letter thrown away unread counts as no answer, because the
// abandonment happened whether or not it was read; refusing costs nothing when the game can see the player truly cannot.

#region

using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.HelpPleas
{
   /// <summary>What has gone wrong for the writer. Only what the game can prove: nobody invents a distress.</summary>
   public enum HelpCrisis
   {
      /// <summary>A town or castle of the writer's house is under siege.</summary>
      Besieged,

      /// <summary>The writer's house has run out of money.</summary>
      Ruined,

      /// <summary>The writer was just beaten in the field and got away.</summary>
      BattleLost,

      /// <summary>The writer's house just lost a fief to an enemy.</summary>
      FiefLost,

      /// <summary>A town or castle of the writer's house is running out of food.</summary>
      Starving
   }

   /// <summary>What the writer asks of the player.</summary>
   public enum HelpAsk
   {
      /// <summary>Ride to the besieged place.</summary>
      Relief,

      /// <summary>Send a sum of money.</summary>
      Coin,

      /// <summary>Bring food, by hand.</summary>
      Food
   }

   /// <summary>Why the player truly cannot do what is asked, as the game sees it.</summary>
   public enum HelpIncapacity
   {
      /// <summary>Nothing stops them.</summary>
      None,

      /// <summary>The player is a prisoner.</summary>
      Captive,

      /// <summary>A fief of the player's own is under siege.</summary>
      OwnFiefBesieged,

      /// <summary>The player has too few men to ride to anyone's aid.</summary>
      TooFewMen,

      /// <summary>The player does not have the sum asked.</summary>
      TooPoor
   }

   /// <summary>The player's answer, as they gave it on the letter.</summary>
   public enum HelpAnswer
   {
      /// <summary>No answer: the letter was left unanswered or thrown away.</summary>
      None,

      /// <summary>They said they would come.</summary>
      Promised,

      /// <summary>They said no.</summary>
      Refused
   }

   /// <summary>How a would-be writer stands to the player.</summary>
   public sealed class HelpWriterFacts
   {
      /// <summary>The player has met them.</summary>
      public bool HasMet { get; init; }

      /// <summary>They belong to the player's own clan: their trouble is the player's own, no letter needed.</summary>
      public bool InThePlayersClan { get; init; }

      /// <summary>Their realm is at war with the player's: they cannot ask, and the player could not come.</summary>
      public bool AtWarWithThePlayer { get; init; }

      /// <summary>They are of the player's blood.</summary>
      public bool IsKin { get; init; }

      /// <summary>They serve the player's realm, at war together.</summary>
      public bool IsWarAlly { get; init; }

      /// <summary>Their regard for the player (the mod's own ledger).</summary>
      public int Regard { get; init; }
   }

   /// <summary>The state of a plea about to be opened.</summary>
   public sealed class HelpOpeningFacts
   {
      /// <summary>Today, in campaign days.</summary>
      public int Today { get; init; }

      /// <summary>The day this writer last asked, or null if never.</summary>
      public int? LastPleaDay { get; init; }

      /// <summary>This crisis has already had its plea.</summary>
      public bool CrisisAlreadyPleaded { get; init; }

      /// <summary>This writer has a plea still open.</summary>
      public bool WriterHasOpenPlea { get; init; }

      /// <summary>How many pleas are open across the world.</summary>
      public int OpenPleas { get; init; }
   }

   /// <summary>What the player can do today, read from the game.</summary>
   public sealed class HelpMeansFacts
   {
      /// <summary>What is asked.</summary>
      public HelpAsk Ask { get; init; }

      /// <summary>The player is a prisoner.</summary>
      public bool IsCaptive { get; init; }

      /// <summary>A fief of the player's is under siege.</summary>
      public bool OwnFiefBesieged { get; init; }

      /// <summary>The men in the player's party.</summary>
      public int PartyMen { get; init; }

      /// <summary>The player's gold.</summary>
      public int Gold { get; init; }

      /// <summary>The sum asked, for a plea for coin.</summary>
      public int Amount { get; init; }
   }

   /// <summary>The whole of a plea at the moment it closes.</summary>
   public sealed class HelpCloseFacts
   {
      /// <summary>The letter reached the player. A letter still on the road when the crisis ended asked nothing of them.</summary>
      public bool LetterReached { get; init; }

      /// <summary>The help came: the player was at the place, or the money was sent. The deed counts, whatever was answered.</summary>
      public bool HelpCame { get; init; }

      /// <summary>What the player answered.</summary>
      public HelpAnswer Answer { get; init; }

      /// <summary>What stood in their way, as the game saw it when they answered or when the plea closed.</summary>
      public HelpIncapacity Incapacity { get; init; }

      /// <summary>The worst happened: the place fell.</summary>
      public bool Disaster { get; init; }

      /// <summary>The player who promised had closed the distance when the crisis ended: they came too late, their word was not broken.</summary>
      public bool WasOnTheWay { get; init; }
   }

   /// <summary>What a closed plea did to the writer's regard and memory.</summary>
   public sealed class HelpVerdict
   {
      /// <summary>What the writer now thinks: none, thankful, disappointed, hurt or betrayed.</summary>
      public HelpFeeling Feeling { get; init; }

      /// <summary>The change in the writer's regard.</summary>
      public int RegardDelta { get; init; }

      /// <summary>The magnitude of the grievance they keep, 0 for none.</summary>
      public int GrudgeMagnitude { get; init; }

      /// <summary>The magnitude of the kindness they keep, 0 for none.</summary>
      public int KindnessMagnitude { get; init; }
   }

   /// <summary>What a closed plea leaves in the writer.</summary>
   public enum HelpFeeling
   {
      /// <summary>Nothing: the letter never arrived, or the player truly could not.</summary>
      Nothing,

      /// <summary>The player came too late, having tried.</summary>
      TooLate,

      /// <summary>The help came.</summary>
      Thankful,

      /// <summary>The player said no, and could have said yes.</summary>
      Disappointed,

      /// <summary>The player left the letter without an answer.</summary>
      Abandoned,

      /// <summary>The player said they would come and did nothing.</summary>
      Betrayed
   }

   /// <summary>Who may ask the player for help, for what, and what the answer is worth.</summary>
   public static class HelpPleaPolicy
   {
      /// <summary>A writer waits this long between two pleas: "never a flood of pleas".</summary>
      public const int DaysBetweenPleas = 60;

      /// <summary>No more than this many pleas stand open at once, however many friends are in trouble.</summary>
      public const int MaxOpenPleas = 2;

      /// <summary>A siege younger than this is not yet a crisis: most break off within a day.</summary>
      public const int DaysBeforeASiegeCounts = 1;

      /// <summary>The player has this many days from the letter's arrival to reach a besieged place.</summary>
      public const int DaysToReachTheSiege = 14;

      /// <summary>The player has this many days from the letter's arrival to answer a plea for coin or food.</summary>
      public const int DaysToAnswer = 15;

      /// <summary>The days allowed to bring the food once promised.</summary>
      public const int DaysToBringFood = 30;

      /// <summary>The fewest men with which anyone can answer a call to arms.</summary>
      public const int MinMenToRide = 20;

      /// <summary>A house with less than this in its coffers is ruined.</summary>
      public const int RuinedGold = 3000;

      /// <summary>A house with more than this does not beg after a lost fight.</summary>
      public const int NeedyGold = 15000;

      /// <summary>The smallest sum worth a plea.</summary>
      public const int MinCoin = 500;

      /// <summary>The largest sum anyone asks.</summary>
      public const int MaxCoin = 5000;

      /// <summary>A plea for coin asks one part in this many of what the player has.</summary>
      public const int ShareOfThePlayersGold = 5;

      /// <summary>How close to the place the player must come, in map units, to have come.</summary>
      public const float ArrivalDistance = 8f;

      /// <summary>The food a plea asks for.</summary>
      public const int FoodAsked = 20;

      /// <summary>A fortified place with this many days of food or fewer is starving.</summary>
      public const int StarvingFoodStocks = 5;

      /// <summary>The regard kin must keep to ask at all: a kinsman who has soured on the player does not write.</summary>
      public const int KinFloor = Regard.CoolTier;

      /// <summary>The regard a war ally needs to ask.</summary>
      public const int AllyFloor = Regard.FriendlyTier;

      /// <summary>The regard anyone else needs: a true friend.</summary>
      public const int FriendFloor = Regard.WarmTier;

      /// <summary>True when this person may ask the player for help at all.</summary>
      public static bool MayWrite(HelpWriterFacts f)
      {
         if (f == null || !f.HasMet || f.InThePlayersClan || f.AtWarWithThePlayer) return false;
         if (f.IsKin) return f.Regard >= KinFloor;
         if (f.IsWarAlly) return f.Regard >= AllyFloor;

         return f.Regard >= FriendFloor;
      }

      /// <summary>Who writes when several could: kin first, then the warmest.</summary>
      public static int WriterRank(HelpWriterFacts f) => f == null ? int.MinValue : f.Regard + (f.IsKin ? 1000 : 0);

      /// <summary>True when a plea may open today: one per crisis, one at a time per writer, a long wait between two, few at once.</summary>
      public static bool MayOpen(HelpOpeningFacts f)
      {
         if (f == null || f.CrisisAlreadyPleaded || f.WriterHasOpenPlea || f.OpenPleas >= MaxOpenPleas) return false;

         return !f.LastPleaDay.HasValue || f.Today - f.LastPleaDay.Value >= DaysBetweenPleas;
      }

      /// <summary>What a crisis asks of the player.</summary>
      public static HelpAsk AskFor(HelpCrisis crisis)
      {
         switch (crisis)
         {
            case HelpCrisis.Besieged: return HelpAsk.Relief;
            case HelpCrisis.Starving: return HelpAsk.Food;
            default: return HelpAsk.Coin;
         }
      }

      /// <summary>True when a house this rich has need of the player's money after this crisis.</summary>
      public static bool HouseIsInNeed(HelpCrisis crisis, int houseGold)
      {
         switch (crisis)
         {
            case HelpCrisis.Ruined: return houseGold < RuinedGold;
            case HelpCrisis.BattleLost:
            case HelpCrisis.FiefLost: return houseGold < NeedyGold;
            default: return true;
         }
      }

      /// <summary>The sum to ask of a player with this much, in round hundreds, or 0 when they plainly have too little to be asked.</summary>
      public static int CoinAsked(int playerGold)
      {
         int share = playerGold / ShareOfThePlayersGold;
         if (share < MinCoin) return 0;

         return System.Math.Min(share / 100 * 100, MaxCoin);
      }

      /// <summary>What truly stops the player from doing what is asked.</summary>
      public static HelpIncapacity IncapacityOf(HelpMeansFacts f)
      {
         if (f == null || f.IsCaptive) return HelpIncapacity.Captive;

         switch (f.Ask)
         {
            case HelpAsk.Relief:
               if (f.OwnFiefBesieged) return HelpIncapacity.OwnFiefBesieged;

               return f.PartyMen < MinMenToRide ? HelpIncapacity.TooFewMen : HelpIncapacity.None;
            case HelpAsk.Coin:
               return f.Gold < f.Amount ? HelpIncapacity.TooPoor : HelpIncapacity.None;
            default:
               return HelpIncapacity.None;
         }
      }

      /// <summary>
      ///   Judges a plea as it closes. The deed is what counts, not the letter: help that came is thanked whatever was
      ///   answered, and a letter thrown away is simply no answer (Gabriel, 01/11/2026). Nothing is held against a player who
      ///   never received the letter or who truly could not act, and a promise is broken only if the player did not try.
      /// </summary>
      public static HelpVerdict Judge(HelpCloseFacts f)
      {
         if (f == null || !f.LetterReached) return new HelpVerdict {Feeling = HelpFeeling.Nothing};
         if (f.HelpCame) return new HelpVerdict {Feeling = HelpFeeling.Thankful, RegardDelta = 6, KindnessMagnitude = 40};

         switch (f.Answer)
         {
            case HelpAnswer.Promised:
               if (f.WasOnTheWay || f.Incapacity == HelpIncapacity.Captive) return new HelpVerdict {Feeling = HelpFeeling.TooLate};

               return new HelpVerdict {Feeling = HelpFeeling.Betrayed, RegardDelta = f.Disaster ? -8 : -5, GrudgeMagnitude = f.Disaster ? 55 : 30};
            case HelpAnswer.Refused:
               if (f.Incapacity != HelpIncapacity.None) return new HelpVerdict {Feeling = HelpFeeling.Nothing};

               return new HelpVerdict {Feeling = HelpFeeling.Disappointed, RegardDelta = f.Disaster ? -4 : -2};
            default:
               if (f.Incapacity != HelpIncapacity.None) return new HelpVerdict {Feeling = HelpFeeling.Nothing};

               return new HelpVerdict {Feeling = HelpFeeling.Abandoned, RegardDelta = f.Disaster ? -6 : -3, GrudgeMagnitude = f.Disaster ? 40 : 15};
         }
      }
   }
}
