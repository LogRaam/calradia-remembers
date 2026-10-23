// Code written by Gabriel Mailhot, 22/10/2026.
// Which edict a lord or a ruler of the world decrees on his own (Gabriel, 21/10/2026: "Les PNJ peuvent aussi"). It reads
// the town as the game keeps it and answers its sorest need, so an edict the player hears of makes sense to him (a
// hungry town requisitions grain, a lawless one doubles its watch). Rare by design: CR produces what the player will
// meet, so a lord considers it seldom and a realm carries few at once, and every one is told as realm news. Decided
// overnight on Gabriel's leave; every number is a TUNE.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Edicts
{
   /// <summary>A town as its lord sees it when weighing an edict.</summary>
   public sealed class EdictTownFacts
   {
      /// <summary>The town's settlement id.</summary>
      public string TownId { get; init; }

      /// <summary>Days of food left at today's rate; a large number when stocks are not falling.</summary>
      public float FoodDaysLeft { get; init; }

      /// <summary>Loyalty (0 to 100).</summary>
      public float Loyalty { get; init; }

      /// <summary>Security (0 to 100).</summary>
      public float Security { get; init; }

      /// <summary>Prosperity.</summary>
      public float Prosperity { get; init; }

      /// <summary>Militia.</summary>
      public float Militia { get; init; }

      /// <summary>The realm is at war.</summary>
      public bool AtWar { get; init; }

      /// <summary>The coin of the head of the house that holds the town.</summary>
      public int HolderGold { get; init; }
   }

   /// <summary>The counsel a lord takes on his own towns, and a ruler on his realm.</summary>
   public static class EdictCounselPolicy
   {
      /// <summary>TUNE: below these the town's need is sore.</summary>
      public const float HungryBelowDays = 15f, RestlessBelow = 30f, LawlessBelow = 30f, FewSpearsBelow = 100f, PoorBelow = 2500f;

      /// <summary>TUNE: a house head with less coin than this is short of it, and may lay a levy on a settled town.</summary>
      public const int ShortOfCoinBelow = 5000;

      /// <summary>TUNE: a town this content and safe can bear a charter or a levy.</summary>
      public const float SettledAbove = 60f;

      /// <summary>TUNE: the chance, each week, that a lord with a sore town decrees for it.</summary>
      public const float WeeklyChance = 0.1f;

      /// <summary>TUNE: at most this many edicts of the world's lords in force in one realm at once (the player's never count).</summary>
      public const int MostInARealm = 2;

      /// <summary>TUNE: share of a realm's towns that must share a need before its ruler decrees for all of them.</summary>
      public const float RealmShare = 0.5f;

      /// <summary>The edict that answers this town's sorest need; null when it has none worth an edict.</summary>
      public static EdictKind? Answer(EdictTownFacts t)
      {
         if (t == null) return null;
         if (t.FoodDaysLeft < HungryBelowDays) return EdictKind.GrainRequisition;
         if (t.Loyalty < RestlessBelow) return EdictKind.TaxRelief;
         if (t.Security < LawlessBelow) return EdictKind.DoubledWatch;
         if (t.AtWar && t.Militia < FewSpearsBelow) return EdictKind.MilitiaMuster;
         if (t.Loyalty >= SettledAbove && t.Security >= SettledAbove)
         {
            if (t.HolderGold < ShortOfCoinBelow) return EdictKind.HeavyLevy;
            if (t.Prosperity < PoorBelow) return EdictKind.MarketCharter;
         }

         return null;
      }

      /// <summary>
      ///   The edict a ruler decrees for his whole realm: the need shared by at least <see cref="RealmShare" /> of its
      ///   towns, the commonest first; null when the towns' needs differ.
      /// </summary>
      public static EdictKind? AnswerForRealm(IReadOnlyList<EdictTownFacts> towns)
      {
         if (towns == null || towns.Count == 0) return null;
         var needs = towns.Select(Answer).Where(k => k.HasValue).Select(k => k.Value).ToList();
         if (needs.Count == 0) return null;
         IGrouping<EdictKind, EdictKind> top = needs.GroupBy(k => k).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();

         return top.Count() >= towns.Count * RealmShare ? top.Key : (EdictKind?) null;
      }

      /// <summary>A lord decrees this week: the week's roll fell under the chance and his realm has room.</summary>
      public static bool DecreesThisWeek(float roll, int worldEdictsInRealm) => roll < WeeklyChance && worldEdictsInRealm < MostInARealm;
   }
}
