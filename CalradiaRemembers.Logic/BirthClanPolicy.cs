// Code written by Gabriel Mailhot, 22/11/2026.
// Which house a character was born into. EvilPoptart1987 (Nexus, 21/11/2026): "Married people remember what house they
// are born into i.e. they married in from. Silly to hear marriage transplants talking about growing up in a house not
// their own." The birth-house line (BirthHouseText, Maifailun 07/10/2026) read the engine's OriginClan first, but the
// engine stamps OriginClan with the FIRST clan a hero is ever given (Hero.Clan's setter, "if (_clan == null)
// _originClan = value;", 1.4.8 decompile). A wife already married when the campaign starts is loaded straight into her
// husband's clan, so her OriginClan IS her husband's clan: the line said nothing and the model gave her his childhood.
// Gabriel (22/11/2026): "Fait OriginClan."

namespace CalradiaRemembers.Logic
{
   /// <summary>The clans around a character, by id, from which the house they were born into is read.</summary>
   public sealed class BirthClanFacts
   {
      /// <summary>The clan the character belongs to now.</summary>
      public string CurrentClan { get; init; }

      /// <summary>The engine's OriginClan: the first clan the character was ever given, which for a wife married before the campaign began is her husband's.</summary>
      public string OriginClan { get; init; }

      /// <summary>The father's clan, when the game knows the father.</summary>
      public string FatherClan { get; init; }

      /// <summary>The mother's clan, when the game knows the mother.</summary>
      public string MotherClan { get; init; }

      /// <summary>The character has a spouse, living or dead.</summary>
      public bool IsMarried { get; init; }

      /// <summary>The character leads their current clan.</summary>
      public bool LeadsTheClan { get; init; }

      /// <summary>That spouse belongs to the current clan by blood: a parent in it, or its leader.</summary>
      public bool SpouseIsOfTheClanByBlood { get; init; }
   }

   /// <summary>Where the birth house comes from.</summary>
   public enum BirthClanSource
   {
      /// <summary>Born into the clan they belong to now, or nothing is known: nothing to say.</summary>
      None,

      /// <summary>The engine's OriginClan, which differs from the current clan.</summary>
      Origin,

      /// <summary>The father's clan.</summary>
      Father,

      /// <summary>The mother's clan (no father known).</summary>
      Mother,

      /// <summary>Married in from a house the game does not name: no parent is known.</summary>
      MarriedInUnknownHouse
   }

   /// <summary>Reads which house a character was born into. Pure.</summary>
   public static class BirthClanPolicy
   {
      /// <summary>The source of the birth house, or None when they were born into the house they belong to.</summary>
      public static BirthClanSource Resolve(BirthClanFacts f)
      {
         if (f == null || string.IsNullOrEmpty(f.CurrentClan)) return BirthClanSource.None;

         // A move the engine saw (a wedding during the campaign) is the surest fact.
         if (!string.IsNullOrEmpty(f.OriginClan) && f.OriginClan != f.CurrentClan) return BirthClanSource.Origin;

         // OriginClan equals the current clan: true for a child of the house, and for a wife loaded into it. The parents
         // tell them apart. A father in the house means born there.
         if (!string.IsNullOrEmpty(f.FatherClan)) return f.FatherClan != f.CurrentClan ? BirthClanSource.Father : BirthClanSource.None;

         if (!string.IsNullOrEmpty(f.MotherClan)) return f.MotherClan != f.CurrentClan ? BirthClanSource.Mother : BirthClanSource.None;

         // No parent at all. A married character who does not lead the house, wed to someone who is of it by blood, came
         // in by marriage; which house they left is not known.
         return f.IsMarried && !f.LeadsTheClan && f.SpouseIsOfTheClanByBlood ? BirthClanSource.MarriedInUnknownHouse : BirthClanSource.None;
      }
   }
}
