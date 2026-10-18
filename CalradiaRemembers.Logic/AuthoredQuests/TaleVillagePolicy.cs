// Code written by Gabriel Mailhot, 18/10/2026.
// Which village a written tale may be about, and when the tale is lost with it. fkasad (Nexus, 18/10/2026), on the
// Drowned Bridge: "The village is a burned enemy village. It is bound to a city that was sieged and has fallen just
// before or after the quest was calculated or given... the process should select a village bound to a town that is NOT
// under siege. Otherwise, the quest either takes place in an illogical context... or becomes impossible to complete."
// A tale took the giver's first village whatever its state. Gabriel (18/10/2026): pick a quiet village, and end the
// tale without penalty when the giver loses the village.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The state of one village a tale could be about.</summary>
   public sealed class TaleVillageFacts
   {
      /// <summary>The village's name, for the reason a tale is not offered.</summary>
      public string Name { get; init; }

      /// <summary>Raiders are in it now.</summary>
      public bool BeingRaided { get; init; }

      /// <summary>It was looted and has not yet recovered.</summary>
      public bool Looted { get; init; }

      /// <summary>The town or castle it is bound to is under siege.</summary>
      public bool FortressUnderSiege { get; init; }
   }

   /// <summary>Which village a tale may be about, and when the giver has lost it. Pure.</summary>
   public static class TaleVillagePolicy
   {
      /// <summary>
      ///   A village the tale's people can be found in and its lord can still speak for: not raided, not looted, its
      ///   fortress not besieged.
      /// </summary>
      public static bool IsQuiet(TaleVillageFacts f) => f != null && !f.BeingRaided && !f.Looted && !f.FortressUnderSiege;

      /// <summary>The first quiet village, in the order given, or null.</summary>
      public static TaleVillageFacts Pick(IEnumerable<TaleVillageFacts> candidates) => candidates?.FirstOrDefault(IsQuiet);

      /// <summary>Why none of these villages will do now, for the log ("Ortongard is looted, Ismenia's fortress is under siege").</summary>
      public static string WhyNoneQuiet(IEnumerable<TaleVillageFacts> candidates)
         => string.Join(", ", (candidates ?? Enumerable.Empty<TaleVillageFacts>()).Where(f => f != null).Select(f =>
            f.BeingRaided ? $"{f.Name} is being raided"
            : f.Looted ? $"{f.Name} is looted"
            : f.FortressUnderSiege ? $"{f.Name}'s fortress is under siege"
            : $"{f.Name} is quiet"));

      /// <summary>
      ///   The giver no longer holds the village the tale is about (its fortress fell): his house is known and the
      ///   village's house is another, or none.
      /// </summary>
      public static bool GiverLostIt(string giverClanId, string villageClanId)
         => !string.IsNullOrEmpty(giverClanId) && villageClanId != giverClanId;

      /// <summary>The journal's last line when the tale is lost with its village.</summary>
      public static string LostLine(string village, string newHolder)
         => string.IsNullOrEmpty(newHolder)
            ? $"{village} is no longer in the hands of the one who asked. The matter is lost with it."
            : $"{village} has passed to {newHolder}. The matter is lost with it.";
   }
}
