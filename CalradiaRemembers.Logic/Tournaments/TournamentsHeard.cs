// Code written by Gabriel Mailhot, 05/11/2026.
// Which tournaments under way a character knows of. 3chmidt (Nexus, 05/11/2026): "another idea would be that lords know in
// what town currently tournaments happen, at least in the same kingdom or a neighbouring one would make sense for them to
// know." Nothing in the prompt named a tournament being held. A character now knows those in towns of his own realm, and
// those near enough to be talked of on the roads (a neighbouring realm's, as the map has it), his own realm's first and
// then the nearest, a few at most. Pure; the host reads the game's tournaments.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Tournaments
{
   /// <summary>A tournament being held, as a character might hear of it.</summary>
   public sealed class HeldTournament
   {
      /// <summary>The town holding it.</summary>
      public string Town { get; init; } = "";

      /// <summary>The realm the town belongs to; blank for none.</summary>
      public string Realm { get; init; } = "";

      /// <summary>The town is in the character's own realm.</summary>
      public bool InOwnRealm { get; init; }

      /// <summary>How far the town is from where the character speaks, in map units.</summary>
      public float Distance { get; init; }

      /// <summary>The prize offered, as the game names it; blank when unknown.</summary>
      public string Prize { get; init; } = "";
   }

   /// <summary>The tournaments a character knows are being held, and the words for them.</summary>
   public static class TournamentsHeard
   {
      /// <summary>The most tournaments named in one turn.</summary>
      public const int MaxNamed = 4;

      /// <summary>TUNE: a town of another realm closer than this (map units) is near enough for its tournament to be talked of.</summary>
      public const float HeardWithin = 150f;

      /// <summary>The lines to tell: own realm first, then the nearest; only those the character would know. Empty when none.</summary>
      public static List<string> Lines(IEnumerable<HeldTournament> held)
      {
         return (held ?? Enumerable.Empty<HeldTournament>())
               .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Town) && (t.InOwnRealm || t.Distance <= HeardWithin))
               .OrderByDescending(t => t.InOwnRealm)
               .ThenBy(t => t.Distance)
               .Take(MaxNamed)
               .Select(Describe)
               .ToList();
      }

      /// <summary>"at Pravend, in your own realm, the prize a Noble Bow" / "at Sargot, in Vlandia".</summary>
      public static string Describe(HeldTournament t)
      {
         string where = t.InOwnRealm ? ", in your own realm" : string.IsNullOrWhiteSpace(t.Realm) ? "" : $", in {t.Realm.Trim()}";
         string prize = string.IsNullOrWhiteSpace(t.Prize) ? "" : $", the prize {Article(t.Prize.Trim())}";

         return $"at {t.Town.Trim()}{where}{prize}";
      }

      private static string Article(string noun)
         => "aeiouAEIOU".IndexOf(noun[0]) >= 0 ? $"an {noun}" : $"a {noun}";
   }
}
