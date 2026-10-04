// Code written by Gabriel Mailhot, 03/10/2026.
// Reads authored quest files and says, in plain words, everything wrong with each (the lesson of the kinks parser:
// parsers stay forgiving, the fix is REPORTING). A quest with a problem that could strand the player (a stage that
// leads nowhere, an objective the engine cannot check, a scene with nothing to find) is refused whole; a cosmetic one
// (an unbound placeholder) is reported and the quest still loads.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The quests that loaded, and every problem found, each naming its file and quest.</summary>
   public sealed class AuthoredQuestLoadResult
   {
      public List<AuthoredQuest> Quests { get; } = new();
      public List<string> Problems { get; } = new();
   }

   /// <summary>Parses and validates authored quest files. Pure.</summary>
   public static class AuthoredQuestLoader
   {
      /// <summary>The objectives increment 1 can verify.</summary>
      public static readonly IReadOnlyList<string> ObjectiveTypes = new[] {"talk_to", "talk_to_locals", "be_at", "keep_watch", "explore", "defeat_party", "return"};

      /// <summary>The hazard effects increment 1 carries out.</summary>
      public static readonly IReadOnlyList<string> HazardEffects = new[] {"injure", "alert", "lose_time"};

      /// <summary>The consequences increment 1 carries out.</summary>
      public static readonly IReadOnlyList<string> ConsequenceTypes = new[] {"memory", "world_event"};

      private const string Reward = "reward";
      private static readonly Regex Placeholder = new(@"\{([a-z_]+)\}", RegexOptions.Compiled);

      /// <summary>Several files at once: ids are unique across all of them; a repeat is refused and named.</summary>
      public static AuthoredQuestLoadResult ParseAll(IEnumerable<(string Source, string Json)> files)
      {
         var all = new AuthoredQuestLoadResult();
         var seen = new HashSet<string>(StringComparer.Ordinal);

         foreach ((string source, string json) in files ?? Enumerable.Empty<(string, string)>())
         {
            AuthoredQuestLoadResult one = Parse(json, source);
            all.Problems.AddRange(one.Problems);

            foreach (AuthoredQuest q in one.Quests)
            {
               if (seen.Add(q.Id)) all.Quests.Add(q);
               else all.Problems.Add($"{source}: quest '{q.Id}' is already defined by another file; this one is ignored.");
            }
         }

         return all;
      }

      /// <summary>One file: a quest object, or an array of them.</summary>
      public static AuthoredQuestLoadResult Parse(string json, string source)
      {
         var result = new AuthoredQuestLoadResult();
         List<AuthoredQuest> read;

         try
         {
            JToken root = JToken.Parse(json ?? "");
            read = root is JArray arr ? arr.ToObject<List<AuthoredQuest>>() : new List<AuthoredQuest> {root.ToObject<AuthoredQuest>()};
         }
         catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidCastException)
         {
            result.Problems.Add($"{source}: not valid JSON ({ex.Message}).");

            return result;
         }

         foreach (AuthoredQuest q in read.Where(q => q != null))
         {
            q.Source = source;
            var blocking = new List<string>();
            var cosmetic = new List<string>();
            Validate(q, JsonConvert.SerializeObject(q), blocking, cosmetic);

            string name = $"{source}: quest '{q.Id ?? "(no id)"}'";
            result.Problems.AddRange(blocking.Select(p => $"{name}: {p} The quest is not loaded."));
            result.Problems.AddRange(cosmetic.Select(p => $"{name}: {p}"));
            if (blocking.Count == 0) result.Quests.Add(q);
         }

         return result;
      }

      private static void Validate(AuthoredQuest q, string text, List<string> blocking, List<string> cosmetic)
      {
         if (string.IsNullOrWhiteSpace(q.Id)) blocking.Add("it has no id.");
         if (q.Stages == null || q.Stages.Count == 0)
         {
            blocking.Add("it has no stages.");

            return;
         }

         var ids = new HashSet<string>(StringComparer.Ordinal);
         foreach (AuthoredStage s in q.Stages)
         {
            if (string.IsNullOrWhiteSpace(s?.Id)) { blocking.Add("a stage has no id."); continue; }
            if (!ids.Add(s.Id)) blocking.Add($"two stages are called '{s.Id}'.");
         }

         foreach (AuthoredStage s in q.Stages.Where(s => s != null))
         {
            string next = s.OnComplete?.Next;
            if (string.IsNullOrWhiteSpace(next)) blocking.Add($"stage '{s.Id}' has no on_complete.next (a stage id, or \"reward\").");
            else if (next != Reward && !ids.Contains(next)) blocking.Add($"stage '{s.Id}' leads to '{next}', which is no stage of this quest.");

            ValidateObjective(s, blocking);
         }

         // Walk the chain from the first stage: it must reach the reward without going round in a circle.
         var walked = new HashSet<string>(StringComparer.Ordinal);
         AuthoredStage at = q.Stages[0];
         while (at != null)
         {
            if (!walked.Add(at.Id ?? "")) { blocking.Add($"the stages loop back to '{at.Id}' and never reach the reward."); break; }
            string next = at.OnComplete?.Next;
            if (next == Reward || next == null) break;
            at = q.Stages.FirstOrDefault(x => x?.Id == next);
         }

         foreach (AuthoredConsequence c in (q.Reward?.Consequences ?? new List<AuthoredConsequence>()).Concat(q.Failure?.Consequences ?? new List<AuthoredConsequence>()))
            if (c != null && !ConsequenceTypes.Contains(c.Type))
               cosmetic.Add($"consequence '{c.Type}' is not carried out yet (known: {string.Join(", ", ConsequenceTypes)}); it is skipped.");

         var binds = new HashSet<string>(q.Binds?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
         foreach (string unbound in Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Where(p => !binds.Contains(p)))
            cosmetic.Add($"{{{unbound}}} is used but no role binds it (add it to \"binds\"), so it would reach the player as written.");
      }

      private static void ValidateObjective(AuthoredStage s, List<string> blocking)
      {
         AuthoredObjective o = s.Objective;
         if (o == null || string.IsNullOrWhiteSpace(o.Type))
         {
            blocking.Add($"stage '{s.Id}' has no objective.");

            return;
         }

         if (!ObjectiveTypes.Contains(o.Type))
         {
            blocking.Add($"stage '{s.Id}' asks for '{o.Type}', which the game cannot check yet (known: {string.Join(", ", ObjectiveTypes)}).");

            return;
         }

         switch (o.Type)
         {
            case "talk_to":
               if (string.IsNullOrWhiteSpace(o.Hero)) blocking.Add($"stage '{s.Id}': talk_to needs \"hero\".");
               break;
            case "talk_to_locals":
            case "be_at":
               if (string.IsNullOrWhiteSpace(o.Place)) blocking.Add($"stage '{s.Id}': {o.Type} needs \"place\".");
               if (o.Type == "talk_to_locals" && o.Count < 1) blocking.Add($"stage '{s.Id}': talk_to_locals needs a \"count\" of at least 1.");
               break;
            case "keep_watch":
               if (string.IsNullOrWhiteSpace(o.Place)) blocking.Add($"stage '{s.Id}': keep_watch needs \"place\".");
               if (o.Nights == null || o.Nights.Length != 2 || o.Nights[0] < 1 || o.Nights[1] < o.Nights[0])
                  blocking.Add($"stage '{s.Id}': keep_watch needs \"nights\": [first night it may happen, night it surely happens], with 1 <= first <= last.");
               if (o.Chance < 0 || o.Chance > 1) blocking.Add($"stage '{s.Id}': keep_watch \"chance\" must be between 0 and 1.");
               break;
            case "explore":
               ValidateScene(s, o.Scene, blocking);
               break;
            case "defeat_party":
               if (o.Spawn == null || string.IsNullOrWhiteSpace(o.Spawn.Id) || string.IsNullOrWhiteSpace(o.Spawn.Near))
                  blocking.Add($"stage '{s.Id}': defeat_party needs \"spawn\" with an \"id\" and a \"near\".");
               else if (o.Spawn.Size == null || o.Spawn.Size.Length != 2 || o.Spawn.Size[0] < 1 || o.Spawn.Size[1] < o.Spawn.Size[0])
                  blocking.Add($"stage '{s.Id}': spawn \"size\" must be [fewest, most] with 1 <= fewest <= most.");
               break;
         }
      }

      private static void ValidateScene(AuthoredStage s, AuthoredScene scene, List<string> blocking)
      {
         if (scene?.Opening == null || string.IsNullOrWhiteSpace(scene.Opening.Canvas))
         {
            blocking.Add($"stage '{s.Id}': an explore scene needs an \"opening\" with a \"canvas\".");

            return;
         }

         List<AuthoredFinding> findings = scene.Findings ?? new List<AuthoredFinding>();
         if (!findings.Any(f => f != null && f.Required))
            blocking.Add($"stage '{s.Id}': an explore scene needs at least one required finding, or it could never be completed.");
         if (findings.Where(f => f != null).GroupBy(f => f.Id).Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
            blocking.Add($"stage '{s.Id}': every finding needs its own id.");

         foreach (AuthoredHazard h in scene.Hazards ?? new List<AuthoredHazard>())
         {
            if (h == null) continue;
            if (string.IsNullOrWhiteSpace(h.Id) || string.IsNullOrWhiteSpace(h.Trigger))
               blocking.Add($"stage '{s.Id}': every hazard needs an \"id\" and a \"trigger\" (what the player must do to spring it).");
            if (h.Effect == null || !HazardEffects.Contains(h.Effect.Type))
               blocking.Add($"stage '{s.Id}': hazard '{h.Id}' has effect '{h.Effect?.Type}', which the game cannot carry out yet (known: {string.Join(", ", HazardEffects)}).");
         }
      }
   }
}
