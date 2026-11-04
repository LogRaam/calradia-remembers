// Code written by Gabriel Mailhot, 04/11/2026.
// The words a character is told about a pair bond (PairBondPolicy). Gabriel (04/11/2026): an unexplained start leaves "une
// mefiance vague", said without a number, and the houses' standing is "une phrase distincte" from the person's own feeling
// (tashmetu, Nexus, 04/11/2026: in the base game "a younger son, a wife or a cousin shares the leader's friends and
// enemies"). NPCs never lie: the vague feeling is told as having no known cause, so the model has no story to invent.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Relations
{
   /// <summary>Another house and how the speaker's house stands with it.</summary>
   public sealed class HouseStanding
   {
      /// <summary>The other house's name.</summary>
      public string House { get; init; }

      /// <summary>How the two heads of house stand, as believed (<see cref="PairBond.Points" />).</summary>
      public int Points { get; init; }
   }

   /// <summary>Words for a pair bond. Pure.</summary>
   public static class PairBondText
   {
      /// <summary>At most this many houses on each side of the houses' sentence.</summary>
      public const int MaxHousesEachWay = 3;

      /// <summary>
      ///   What follows a person's label when the bond says more than the number: the reason a start is believed, or the
      ///   vague feeling an unexplained one leaves. Null when there is nothing to add.
      /// </summary>
      public static string Clause(PairBond bond)
      {
         if (bond == null) return null;
         if (bond.Basis == PairBondBasis.Known && !string.IsNullOrWhiteSpace(bond.Reason)) return $"known to each other through {bond.Reason}";

         return bond.Echo switch
         {
            PairBondEcho.VagueUnease => "you have long felt a vague unease about them, though you know no cause for it",
            PairBondEcho.VagueGoodwill => "you have long felt a vague goodwill toward them, though nothing ever passed between you",
            _ => null
         };
      }

      /// <summary>
      ///   How the speaker's house stands with the named person's house, said apart from the speaker's own feeling, or
      ///   null when the houses stand nowhere in particular.
      /// </summary>
      public static string HouseClause(int housePoints, string myHeadOfHouse, string theirHeadOfHouse)
      {
         string how = HouseWord(housePoints);
         if (how == null || string.IsNullOrWhiteSpace(myHeadOfHouse) || string.IsNullOrWhiteSpace(theirHeadOfHouse)) return null;

         return $"between your houses there is {how} ({myHeadOfHouse}, head of yours, and {theirHeadOfHouse}, head of theirs): your house's standing, not necessarily your own feeling";
      }

      /// <summary>
      ///   The houses' sentence for the RELATIONSHIPS block of someone who does not lead their house, or null when the
      ///   house stands with no one in particular.
      /// </summary>
      public static string HousesLine(string headOfHouse, IReadOnlyList<HouseStanding> houses)
      {
         if (string.IsNullOrWhiteSpace(headOfHouse) || houses == null) return null;

         List<string> odds = houses.Where(h => h != null && !string.IsNullOrWhiteSpace(h.House) && new VanillaRelation(h.Points).IsEnemy)
                                   .OrderBy(h => h.Points).Take(MaxHousesEachWay).Select(h => h.House).ToList();
         List<string> good = houses.Where(h => h != null && !string.IsNullOrWhiteSpace(h.House) && new VanillaRelation(h.Points).IsFriend)
                                   .OrderByDescending(h => h.Points).Take(MaxHousesEachWay).Select(h => h.House).ToList();
         if (odds.Count == 0 && good.Count == 0) return null;

         var parts = new List<string>();
         if (odds.Count > 0) parts.Add($"at odds with {Join(odds)}");
         if (good.Count > 0) parts.Add($"on good terms with {Join(good)}");

         return $"Between houses: your house, under {headOfHouse}, is {string.Join(", and ", parts)}. That is your house's standing, not necessarily your own feeling.";
      }

      #region private

      private static string HouseWord(int points)
      {
         var r = new VanillaRelation(points);
         if (r.IsBitterEnemy) return "bitter enmity";
         if (r.IsEnemy) return "ill will";
         if (r.IsClose) return "close friendship";
         if (r.IsFriend) return "goodwill";

         return null;
      }

      private static string Join(IReadOnlyList<string> names)
         => names.Count <= 1 ? names.FirstOrDefault() : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];

      #endregion
   }
}
