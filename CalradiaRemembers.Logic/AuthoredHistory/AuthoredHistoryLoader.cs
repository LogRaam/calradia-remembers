// Code written by Gabriel Mailhot, 17/10/2026.
// Reads an authored history file and says, in plain words, everything wrong with it (docs/design/AUTHORED_HISTORY.md
// section 5; the lesson of the kinks parser: parsers stay forgiving, the fix is REPORTING). A load never fails as a
// whole: each event is kept, kept with a warning, or set aside. What is set aside is what would put the game in the
// wrong: an unknown hero, a date inside the campaign (the game owns that time), a hero dated before his birth, or a
// death the player can see is false in the encyclopedia ("never fake what the player can verify").

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.AuthoredHistory
{
   /// <summary>Parses and validates authored history. Pure.</summary>
   public static class AuthoredHistoryLoader
   {
      /// <summary>The kinds an authored event may be: the long-term memory's own, then those the game never records.</summary>
      public static readonly IReadOnlyList<string> Kinds = new[] {
         "battle", "siege", "fief_taken", "execution", "fell_in_battle", "marriage", "divorce", "war", "peace", "rebellion",
         "scheme", "capture", "betrayal", "feud", "kindness", "debt", "rescue", "duel", "other"
      };

      /// <summary>The kinds that kill their harmed: their dead must be dead today.</summary>
      public static readonly IReadOnlyList<string> KillingKinds = new[] {"execution", "fell_in_battle"};

      /// <summary>The seasons, in the game's order (CampaignTime.Seasons).</summary>
      public static readonly IReadOnlyList<string> Seasons = new[] {"spring", "summer", "autumn", "winter"};

      /// <summary>An event's importance when the author gave none: known to its ties, under the realm-wide line.</summary>
      public const int DefaultImportance = 40;


      /// <summary>Parses the file's text. Null and a problem when it is not readable JSON.</summary>
      public static AuthoredHistoryDocument Parse(string json, List<string> problems)
      {
         if (string.IsNullOrWhiteSpace(json)) return new AuthoredHistoryDocument();

         try
         {
            return JsonConvert.DeserializeObject<AuthoredHistoryDocument>(json) ?? new AuthoredHistoryDocument();
         }
         catch (JsonException ex)
         {
            problems?.Add($"The history file is not valid JSON, nothing was loaded: {ex.Message}");

            return null;
         }
      }

      /// <summary>The absolute in-game day an event of this year and season falls on (its first day).</summary>
      public static int DayOf(int year, string season, int daysInSeason, int seasonsInYear)
      {
         int s = SeasonIndex(season);

         return year * daysInSeason * seasonsInYear + Math.Max(0, s) * daysInSeason;
      }

      /// <summary>Validates every event against the campaign.</summary>
      public static AuthoredHistoryLoadResult Validate(AuthoredHistoryDocument doc, HistoryWorldFacts world)
      {
         var result = new AuthoredHistoryLoadResult();

         if (doc?.Events == null || world == null) return result;

         Dictionary<string, HistoryHeroFacts> byId = world.Heroes.Where(h => h?.Id != null).GroupBy(h => h.Id).ToDictionary(g => g.Key, g => g.First());
         var seen = new HashSet<string>();

         for (var i = 0; i < doc.Events.Count; i++)
         {
            AuthoredHistoryEvent e = doc.Events[i];
            if (e == null) continue;

            string label = string.IsNullOrWhiteSpace(e.Id) ? $"event #{i + 1}" : $"event '{e.Id}'";
            var reasons = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(e.Id)) reasons.Add("it has no id");
            else if (!seen.Add(e.Id)) reasons.Add("another event already has this id");

            string kind = e.Kind?.Trim().ToLowerInvariant();
            if (kind == null || !Kinds.Contains(kind)) reasons.Add($"its kind '{e.Kind}' is not one of: {string.Join(", ", Kinds)}");

            if (string.IsNullOrWhiteSpace(e.Summary)) reasons.Add("it has no summary");

            if (e.Season != null && SeasonIndex(e.Season) < 0) warnings.Add($"season '{e.Season}' is unknown, read as spring");

            int day = DayOf(e.Year, e.Season, world.DaysInSeason, world.SeasonsInYear);
            if (day >= world.CampaignStartDay) reasons.Add($"it is dated {e.Year}, not before the campaign began (the game owns that time)");

            List<string> actors = ResolveAll(e.Actors, byId, world.Heroes, "actor", reasons);
            List<string> harmed = ResolveAll(e.Harmed, byId, world.Heroes, "harmed hero", reasons);
            List<string> knownBy = ResolveAll(e.KnownBy, byId, world.Heroes, "confidant", reasons);

            if (actors.Count == 0 && harmed.Count == 0) reasons.Add("it names no actor and no harmed hero");

            foreach (string id in actors.Concat(harmed).Distinct())
            {
               int? born = byId[id].BirthDay;
               if (born != null && born.Value > day) reasons.Add($"{byId[id].Name} was not yet born in {e.Year}");
            }

            if (kind != null && KillingKinds.Contains(kind))
            {
               if (harmed.Count == 0) reasons.Add($"a {kind} must name its dead among the harmed");
               foreach (string id in harmed.Where(id => !byId[id].IsDead))
                  reasons.Add($"{byId[id].Name} is alive today, so the {kind} would be false in the encyclopedia");
            }

            if (kind == "marriage")
            {
               List<string> pair = actors.Concat(harmed).Distinct().ToList();
               if (pair.Count == 2 && byId[pair[0]].SpouseId != pair[1])
                  warnings.Add($"{byId[pair[0]].Name} and {byId[pair[1]].Name} are not married today (kept: they may have divorced or been widowed)");
            }

            string place = string.IsNullOrWhiteSpace(e.Place) ? null : e.Place.Trim();
            if (place != null && !world.PlaceIds.Contains(place)) reasons.Add($"its place '{place}' is not a settlement of this campaign");

            List<string> realms = (e.Realms ?? new List<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
            foreach (string r in realms.Where(r => !world.RealmIds.Contains(r)))
               reasons.Add($"its realm '{r}' is not a realm or house of this campaign");

            if (reasons.Count > 0)
            {
               result.Problems.Add($"{label} set aside: {string.Join("; ", reasons)}.");

               continue;
            }

            List<AcceptedRelation> relations = Relations(e.Relations, byId, world, warnings);

            foreach (string w in warnings) result.Problems.Add($"{label}: {w}.");

            result.Events.Add(new AcceptedHistoryEvent {
               Id = e.Id.Trim(), Day = day, Kind = kind, Summary = e.Summary.Trim(), ActorIds = actors, HarmedIds = harmed, PlaceId = place,
               RealmIds = realms, Importance = Clamp(e.Importance, 0, 100), Secret = e.Secret, KnownByIds = knownBy, Relations = relations,
               InvolvesPlayer = world.PlayerId != null && (actors.Contains(world.PlayerId) || harmed.Contains(world.PlayerId))
            });
         }

         return result;
      }

      #region private

      private static int SeasonIndex(string season)
      {
         if (string.IsNullOrWhiteSpace(season)) return 0;

         string s = season.Trim().ToLowerInvariant();
         if (s == "fall") s = "autumn";

         for (var i = 0; i < Seasons.Count; i++)
            if (Seasons[i] == s) return i;

         return -1;
      }

      private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

      // An id first; a hand-written name only when exactly one hero bears it (namesakes are different people).
      private static string Resolve(string token, Dictionary<string, HistoryHeroFacts> byId, IReadOnlyList<HistoryHeroFacts> heroes, out string why)
      {
         why = null;
         if (string.IsNullOrWhiteSpace(token)) return null;

         string t = token.Trim();
         if (byId.ContainsKey(t)) return t;

         List<HistoryHeroFacts> named = heroes.Where(h => h?.Name != null && string.Equals(h.Name, t, StringComparison.OrdinalIgnoreCase)).ToList();
         if (named.Count == 1) return named[0].Id;

         why = named.Count == 0 ? $"'{t}' is no hero of this campaign" : $"'{t}' names {named.Count} heroes, use the id";

         return null;
      }

      private static List<string> ResolveAll(List<string> tokens, Dictionary<string, HistoryHeroFacts> byId, IReadOnlyList<HistoryHeroFacts> heroes, string role,
                                             List<string> reasons)
      {
         var ids = new List<string>();

         foreach (string token in tokens ?? new List<string>())
         {
            string id = Resolve(token, byId, heroes, out string why);
            if (id == null)
            {
               if (why != null) reasons.Add($"{role} {why}");

               continue;
            }

            if (!ids.Contains(id)) ids.Add(id);
         }

         return ids;
      }

      private static List<AcceptedRelation> Relations(List<AuthoredRelation> entries, Dictionary<string, HistoryHeroFacts> byId, HistoryWorldFacts world,
                                                      List<string> warnings)
      {
         var kept = new List<AcceptedRelation>();

         foreach (AuthoredRelation r in entries ?? new List<AuthoredRelation>())
         {
            if (r == null) continue;

            string a = Resolve(r.A, byId, world.Heroes, out string whyA);
            string b = Resolve(r.B, byId, world.Heroes, out string whyB);

            if (a == null || b == null)
            {
               warnings.Add($"a relation was dropped: {whyA ?? whyB ?? "it names no hero"}");

               continue;
            }

            if (a == b)
            {
               warnings.Add($"a relation was dropped: it names {byId[a].Name} twice");

               continue;
            }

            if ((r.Change == null) == (r.Set == null))
            {
               warnings.Add($"the relation between {byId[a].Name} and {byId[b].Name} was dropped: give either 'change' or 'set'");

               continue;
            }

            kept.Add(new AcceptedRelation {
               A = a, B = b,
               Change = r.Change == null ? null : Clamp(r.Change.Value, -VanillaRelation.Bound, VanillaRelation.Bound),
               Set = r.Set == null ? null : Clamp(r.Set.Value, -VanillaRelation.Bound, VanillaRelation.Bound),
               TouchesPlayer = world.PlayerId != null && (a == world.PlayerId || b == world.PlayerId)
            });
         }

         return kept;
      }

      #endregion
   }
}
