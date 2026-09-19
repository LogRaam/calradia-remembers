// Code written by Gabriel Mailhot, 19/09/2026.
// Who in the player's house is FAMILY, and so can never be handed to another lord.
//
// reminensce (Nexus, 19/09/2026), building a bridge submod: "the new take_into_service action/feature appears
// to be using a broader candidate pool than intended... This means it's possible for extended family members
// or married-in nobles to be given to and/or taken by other lords. Is this the intended behavior or an actual
// semantic error?"
//
// Both halves of his reading were right, and they need separating.
//
// THE POOL IS DELIBERATELY WIDE. It walks Clan.Heroes and not Clan.Companions, and the bridge says why: the
// mod's own turn_nemesis and recruit_prisoner swear a hero in by setting Hero.Clan directly, which never sets
// CompanionOf, so IsPlayerCompanion stays false for exactly the retainers a player is proudest of. Narrowing
// the pool to companions would put that bug back.
//
// THE EXCLUSIONS WERE TOO NARROW, and that is the defect. The 2.5.9 changelog promised "your spouse cannot be
// given, nor your own blood". "Own blood" was implemented as parents, children and siblings, and nothing else,
// while a clan the player has run for a generation holds grandchildren, nephews and nieces, and the wives and
// husbands of all of them. A daughter-in-law is in the clan, is not the player's blood, and is not the
// player's spouse, so she passed every test and could be offered to a lord as a retainer.
//
// So family here means what a player would mean by it: anyone who shares a forebear with the player within
// three generations, and anyone married to one of them.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One person as the family tree sees them: who they are, their parents, and whom they married.</summary>
   public sealed class KinRecord
   {
      public string Id { get; init; } = "";

      public string FatherId { get; init; }

      public string MotherId { get; init; }

      public string SpouseId { get; init; }
   }

   /// <summary>Decides who in the player's house is family, and therefore never a retainer to be given away.</summary>
   public static class HouseholdKinPolicy
   {
      /// <summary>
      ///   How far up the tree kinship is looked for. Three generations reaches great-grandparents, which is what
      ///   makes a cousin a cousin (a shared grandparent) and a great-nephew a relative (a shared great-grandparent
      ///   of his, a grandparent of yours). Deeper than this and the question stops being one a player would ask.
      /// </summary>
      public const int GenerationsUp = 3;

      /// <summary>
      ///   Everyone who is the player's family: blood kin within <see cref="GenerationsUp" /> generations, and
      ///   anyone married to one of them, including the player's own spouse. The player is in the set too.
      ///   <paramref name="people" /> must carry the ancestors needed to find a shared forebear, living or dead;
      ///   an ancestor who is simply absent from it is treated as unknown, never as a relative.
      /// </summary>
      public static ISet<string> FamilyOf(string playerId, IEnumerable<KinRecord> people)
      {
         var family = new HashSet<string>();

         if (string.IsNullOrEmpty(playerId) || people == null) return family;

         Dictionary<string, KinRecord> byId = people
            .Where(p => p != null && !string.IsNullOrEmpty(p.Id))
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

         HashSet<string> playerLine = LineOf(playerId, byId);

         // Blood: anyone whose own line meets the player's somewhere in the reach of the tree.
         foreach (KinRecord person in byId.Values)
            if (LineOf(person.Id, byId).Overlaps(playerLine))
               family.Add(person.Id);

         family.Add(playerId);

         // Marriage: the husband or wife of any blood relative, in either direction of the link, since a
         // marriage may be recorded on only one of the two people.
         var married = new HashSet<string>();

         foreach (KinRecord person in byId.Values)
         {
            if (string.IsNullOrEmpty(person.SpouseId)) continue;

            if (family.Contains(person.Id)) married.Add(person.SpouseId);
            if (family.Contains(person.SpouseId)) married.Add(person.Id);
         }

         family.UnionWith(married);

         return family;
      }

      #region private

      /// <summary>A person and every forebear of theirs within the reach, the set two relatives will share.</summary>
      private static HashSet<string> LineOf(string id, IReadOnlyDictionary<string, KinRecord> byId)
      {
         var line = new HashSet<string> {id};
         var frontier = new List<string> {id};

         for (var generation = 0; generation < GenerationsUp; generation++)
         {
            var next = new List<string>();

            foreach (string current in frontier)
            {
               if (!byId.TryGetValue(current, out KinRecord record)) continue;

               foreach (string parent in new[] {record.FatherId, record.MotherId})
                  if (!string.IsNullOrEmpty(parent) && line.Add(parent))
                     next.Add(parent);
            }

            frontier = next;
         }

         return line;
      }

      #endregion
   }
}
