// Code written by Gabriel Mailhot, 23/10/2026.
// The news a siege makes when it begins and when it is lifted. chmidt (Nexus, 23/10/2026): characters "refuse to
// acknowledge a lot of stuff that you say, like if I say a person besieged their city, only knowing who owned the city
// afterwards." Only a siege that ended in capture was recorded (WorldEventFeeders.RecordSiegeCapture); a siege under
// way or lifted left nothing, and the besieger was never named. Now each start and each lifting is news, carried by the
// rumour road to the two realms and the country around it (RumorAwareness: magnitude, plus the realm, plus nearness).

namespace CalradiaRemembers.Logic.Battles
{
   /// <summary>The facts of one siege as the news tells it.</summary>
   public sealed class SiegeNews
   {
      /// <summary>Who laid the siege, already labelled ("Derthert of the dey Meroc"), or a host when no lord led it.</summary>
      public string Besieger { get; init; }

      /// <summary>The besieged place.</summary>
      public string Place { get; init; }

      /// <summary>The realm the place belongs to, or empty.</summary>
      public string PlaceRealm { get; init; }

      /// <summary>The realm the besieger fights for, or empty.</summary>
      public string BesiegerRealm { get; init; }
   }

   /// <summary>Pure: the summary lines and the weight of a siege's news.</summary>
   public static class SiegeRecordText
   {
      /// <summary>
      ///   How loudly a siege begun travels: with the realm's +25, everyone of the two realms knows it; with nearness, the
      ///   country around it does (the awareness threshold is 50).
      /// </summary>
      public const int StartedMagnitude = 40;

      /// <summary>A siege lifted is the same news turned, and travels as far.</summary>
      public const int LiftedMagnitude = 40;

      /// <summary>"Derthert of the dey Meroc (Vlandia) laid siege to Pravend of the Northern Empire."</summary>
      public static string Started(SiegeNews n)
      {
         string who = Who(n);
         string place = Place(n);

         return $"{char.ToUpperInvariant(who[0])}{who.Substring(1)} laid siege to {place}.";
      }

      /// <summary>"The siege of Pravend of the Northern Empire by Derthert of the dey Meroc (Vlandia) was lifted; the walls held."</summary>
      public static string Lifted(SiegeNews n) => $"The siege of {Place(n)} by {Who(n)} was lifted; the walls held.";

      private static string Who(SiegeNews n)
      {
         string who = string.IsNullOrWhiteSpace(n?.Besieger) ? "a host" : n.Besieger.Trim();

         return string.IsNullOrWhiteSpace(n?.BesiegerRealm) ? who : $"{who} ({n.BesiegerRealm.Trim()})";
      }

      private static string Place(SiegeNews n)
      {
         string place = string.IsNullOrWhiteSpace(n?.Place) ? "a stronghold" : n.Place.Trim();

         return string.IsNullOrWhiteSpace(n?.PlaceRealm) ? place : $"{place} of {n.PlaceRealm.Trim()}";
      }
   }
}
