// Code written by Gabriel Mailhot, 03/10/2026.
// Reads authored quest files and says, in plain words, everything wrong with each (the lesson of the kinks parser:
// parsers stay forgiving, the fix is REPORTING). A quest with a problem that could strand the player (a stage that
// leads nowhere, an objective the engine cannot check, a scene with nothing to find) is refused whole; a cosmetic one
// (an unbound placeholder) is reported and the quest still loads.
// 04/10/2026, branching tales: choices, named endings and flags are checked the same way. A path that can never end
// is refused; an ending or a stage nobody can reach, or a flag tested but never set, is reported. The tale editor
// (Docs/TaleEditor.html) runs a JavaScript copy of these checks; the fixtures in docs/design/quest-editor/fixtures
// are run through both, so the two cannot disagree silently. Change a message here, change it there.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
      /// <summary>The objectives the game can verify.</summary>
      public static readonly IReadOnlyList<string> ObjectiveTypes = new[] {"talk_to", "talk_to_locals", "be_at", "keep_watch", "explore", "defeat_party", "return", "wait", "war_ends", "dies", "weds_player", "bring"};

      /// <summary>
      ///   The objectives a CHOICE may carry. keep_watch and explore open a scene of the stage that follows, so they are
      ///   that stage's own objective, never one way out among several.
      /// </summary>
      public static readonly IReadOnlyList<string> ChoiceObjectiveTypes = new[] {"talk_to", "return", "talk_to_locals", "be_at", "defeat_party", "wait", "war_ends", "dies", "weds_player", "bring"};

      /// <summary>The hazard effects the game carries out.</summary>
      public static readonly IReadOnlyList<string> HazardEffects = new[] {"injure", "alert", "lose_time"};

      /// <summary>The consequences the game carries out.</summary>
      public static readonly IReadOnlyList<string> ConsequenceTypes = new[] {"memory", "world_event", "grudge", "conception", "elope", "return_spouse", "item", "regard"};

      /// <summary>
      ///   Flags the game sets itself as the tale ends, which no choice sets (Gabriel, 24/10/2026: a night went well when the
      ///   giver's regard rose); see TaleRegardFlags.
      /// </summary>
      public static readonly IReadOnlyList<string> BuiltInFlags = new[] {TaleRegardFlags.Rose, TaleRegardFlags.Fell};

      /// <summary>When a talk_to completes its stage: as the conversation opens (the default), or as it ends.</summary>
      public static readonly IReadOnlyList<string> TalkMoments = new[] {"opened", "closed"};

      /// <summary>Who may give a tale (anything else is read as "any").</summary>
      public static readonly IReadOnlyList<string> GiverRoles = new[] {"lord", "notable", "gang_leader", "spouse", "companion", "any"};

      /// <summary>The sexes a tale may ask its giver to be (empty for either).</summary>
      public static readonly IReadOnlyList<string> GiverGenders = new[] {"female", "male"};

      /// <summary>The trades a notable giver may be asked to ply (the same keys as the trade guidelines files).</summary>
      public static readonly IReadOnlyList<string> GiverTrades = StationGuidelinesPolicy.TradeKeys;

      /// <summary>The kinds of place a tale may bind (as {village}).</summary>
      public static readonly IReadOnlyList<string> PlaceTypes = new[] {"village", "town"};

      /// <summary>The content levels a tale may ask for (AuthoredContentPolicy).</summary>
      public static readonly IReadOnlyList<string> ContentLevels = AuthoredContentPolicy.Levels;

      /// <summary>What a {rival} may be.</summary>
      public static readonly IReadOnlyList<string> RivalRoles = new[] {"clan_leader"};

      /// <summary>The roles a "said" choice may be said to: the characters the game binds.</summary>
      public static readonly IReadOnlyList<string> SpeakingRoles = new[] {"{giver}", "{rival}", "{husband}"};

      /// <summary>What a {husband} may be (the Troy-inspired tale, Gabriel 12/10/2026).</summary>
      public static readonly IReadOnlyList<string> HusbandRoles = new[] {"spouse_of_giver"};

      private const string Reward = "reward";
      private const string EndPrefix = "end:";
      private static readonly Regex Placeholder = new(@"\{([a-z_]+)\}", RegexOptions.Compiled);

      /// <summary>
      ///   Roles the game binds for every tale, with no "binds" entry (tashmetu, 11/10/2026): {player}, the player's name, so
      ///   a tale's memories and journal can name the player instead of saying "the player".
      /// </summary>
      public static readonly IReadOnlyCollection<string> GameBoundRoles = new[] {"player", "giver_realm"};
      private static readonly Regex MarkerName = new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

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
            // Dates stay words: a line that happens to look like a date reaches the player as written. A comment before
            // the quest is skipped like any other comment (it used to be read as the whole file, which then held nothing).
            using var reader = new JsonTextReader(new StringReader(json ?? "")) {DateParseHandling = DateParseHandling.None};
            JToken root = JToken.ReadFrom(reader, new JsonLoadSettings {CommentHandling = CommentHandling.Ignore});
            while (reader.Read()) { } // as JToken.Parse does: anything but a comment after the root is refused by the reader
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

      /// <summary>
      ///   Where a "next" leads: an ending ("reward", or "end:id" when the file defines it), a stage, or nowhere
      ///   (null). The engine follows the same reading.
      /// </summary>
      public static bool IsEnding(string next) => next == Reward || next != null && next.StartsWith(EndPrefix, StringComparison.Ordinal);

      /// <summary>The named ending a "next" leads to ("end:silence" gives "silence"); null for "reward" or a stage.</summary>
      public static string EndingId(string next) => next != null && next.StartsWith(EndPrefix, StringComparison.Ordinal) ? next.Substring(EndPrefix.Length) : null;

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

         Dictionary<string, AuthoredOutcome> endings = q.Endings ?? new Dictionary<string, AuthoredOutcome>();
         if (endings.Keys.Any(string.IsNullOrWhiteSpace)) blocking.Add("an ending has no name.");

         AuthoredStage first = q.Stages[0];
         foreach (AuthoredStage s in q.Stages.Where(s => s != null))
         {
            if (s.Choices == null || s.Choices.Count == 0)
            {
               string next = s.OnComplete?.Next;
               if (string.IsNullOrWhiteSpace(next)) blocking.Add($"stage '{s.Id}' has no on_complete.next (a stage id, \"reward\", or \"end:<id>\").");
               else CheckNext($"stage '{s.Id}' leads to", next, ids, endings, blocking);

               ValidateObjective($"stage '{s.Id}'", s.Objective, false, false, blocking, cosmetic);

               continue;
            }

            ValidateChoiceStage(q, s, s == first, ids, endings, blocking, cosmetic);
         }

         ValidatePaths(q, ids, endings, blocking, cosmetic);
         ValidateDefeats(q, endings, blocking);

         IEnumerable<AuthoredOutcome> outcomes = new[] {q.Reward, q.Failure}.Concat(endings.Values)
                                                                           .Concat(Defeats(q).Select(d => d.Defeat.AsOutcome()));
         // A stage's on_enter (tashmetu, 15/10/2026) is checked as an ending's consequences are.
         IEnumerable<AuthoredConsequence> entered = q.Stages.Where(s => s != null).SelectMany(s => s.OnEnter ?? new List<AuthoredConsequence>());
         if (first?.OnEnter is {Count: > 0})
            cosmetic.Add($"stage '{first.Id}' is the request, entered before the tale is accepted: its on_enter is never carried out.");
         foreach (AuthoredConsequence c in outcomes.Where(o => o != null).SelectMany(o => o.Consequences ?? new List<AuthoredConsequence>()).Concat(entered))
         {
            if (c != null && !ConsequenceTypes.Contains(c.Type))
               cosmetic.Add($"consequence '{c.Type}' is not carried out yet (known: {string.Join(", ", ConsequenceTypes)}); it is skipped.");
            // The Troy-inspired tale (Gabriel, 12/10/2026): an elopement or a return names a husband the game must bind.
            if (c != null && (c.Type == "elope" || c.Type == "return_spouse") && (string.IsNullOrWhiteSpace(c.Spouse) || c.Spouse.Trim() == "{husband}")
                && q.Requires?.Husband == null)
               blocking.Add($"consequence '{c.Type}' names the giver's husband, but no husband is bound: add \"husband\" to \"requires\".");
            // Items in tales (tashmetu, 20/10/2026): what is given is named, in a count the game can carry.
            if (c?.Type == "item") blocking.AddRange(TaleItemPolicy.ShapeProblems($"consequence '{c.Type}'", "item", c.Item, c.Count));
            if (c?.Type == "regard" && (c.Amount == 0 || c.Amount < -100 || c.Amount > 100))
               blocking.Add($"consequence 'regard' moves regard by {c.Amount}, which must be from -100 to 100 and not 0.");
            if (c != null && !TaleConceptionPolicy.IsAChance(c.Chance))
               blocking.Add($"consequence '{c.Type}' has a chance of {c.Chance.ToString(CultureInfo.InvariantCulture)}, which must be from 0 to 1.");
         }

         ValidateFlags(q, cosmetic);

         var binds = new HashSet<string>(q.Binds?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
         binds.UnionWith(GameBoundRoles);
         foreach (string unbound in Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Where(p => !binds.Contains(p)))
            cosmetic.Add($"{{{unbound}}} is used but no role binds it (add it to \"binds\"), so it would reach the player as written.");

         if (AuthoredContentPolicy.Required(q.Requires?.Content) == null)
            blocking.Add($"requires.content '{q.Requires.Content}' is no content level ({string.Join(", ", ContentLevels)}).");

         // A tale that begins on the map (11/10/2026): someone travelling with the player gives it, by a popup.
         AuthoredMapTrigger onMap = q.OnMap;
         if (onMap != null)
         {
            if (q.Requires?.Giver?.WithPlayer != true)
               blocking.Add("on_map: the tale begins on the map, so its giver must travel with the player: set requires.giver.with_player to true.");
            if (onMap.ChancePerDay <= 0 || onMap.ChancePerDay > 1)
               blocking.Add($"on_map.chance_per_day {onMap.ChancePerDay.ToString(CultureInfo.InvariantCulture)} must be above 0 and at most 1.");
            if (onMap.StillHours < 0) blocking.Add($"on_map.still_hours {onMap.StillHours} cannot be below 0.");
            if (string.IsNullOrWhiteSpace(onMap.Invitation)) blocking.Add("on_map.invitation is empty: the popup would ask the player nothing.");
         }

         // A tale only a scenario opens (Gabriel, 24/10/2026): one way in, and a popup that says something.
         if (q.OnScenario != null)
         {
            if (onMap != null) blocking.Add("on_scenario and on_map both say how the tale begins: keep one.");
            if (string.IsNullOrWhiteSpace(q.OnScenario.Invitation)) blocking.Add("on_scenario.invitation is empty: the popup would ask the player nothing.");
         }

         string gender = q.Requires?.Giver?.Gender;
         if (!string.IsNullOrWhiteSpace(gender) && !GiverGenders.Contains(gender.Trim().ToLowerInvariant()))
            blocking.Add($"requires.giver.gender '{gender}' is neither {string.Join(" nor ", GiverGenders)}.");
         string playerGender = q.Requires?.Player?.Gender;
         if (!string.IsNullOrWhiteSpace(playerGender) && !GiverGenders.Contains(playerGender.Trim().ToLowerInvariant()))
            blocking.Add($"requires.player.gender '{playerGender}' is neither {string.Join(" nor ", GiverGenders)}.");
         foreach (string trade in q.Requires?.Giver?.Trade ?? new List<string>())
            if (!GiverTrades.Contains((trade ?? "").Trim().ToLowerInvariant()))
               blocking.Add($"requires.giver.trade '{trade}' is no trade ({string.Join(", ", GiverTrades)}).");
         if ((q.Requires?.Giver?.Trade?.Count ?? 0) > 0 && q.Requires.Giver.Role != "notable" && q.Requires.Giver.Role != "any")
            blocking.Add($"requires.giver.trade names a trade, which only a notable plies, but the role is '{q.Requires.Giver.Role}': set role to \"notable\".");
         string placeType = q.Requires?.Village?.Type;
         if (q.Requires?.Village != null && !PlaceTypes.Contains((placeType ?? "").Trim().ToLowerInvariant()))
            blocking.Add($"requires.village.type '{placeType}' is neither {string.Join(" nor ", PlaceTypes)}.");

         RivalRequirement rival = q.Requires?.Rival;
         if (rival != null && !RivalRoles.Contains(rival.Role))
            blocking.Add($"requires.rival asks for a '{rival.Role}', but the game binds a rival only as a \"clan_leader\" of the giver's realm.");
         HusbandRequirement husband = q.Requires?.Husband;
         if (husband != null && !HusbandRoles.Contains(husband.Role))
            blocking.Add($"requires.husband asks for a '{husband.Role}', but the game binds a husband only as \"spouse_of_giver\".");
         if (husband != null && q.Requires?.Giver?.Married != true)
            blocking.Add("requires.husband binds the giver's husband, so requires.giver.married must be true.");
         if (rival != null && rival.SameRealmAs != "{giver}")
            blocking.Add($"requires.rival is of the same realm as '{rival.SameRealmAs}', but the game binds a rival only in the giver's realm (\"{{giver}}\").");
      }

      /// <summary>Every band's on_defeat in the tale, with where it stands (a stage's own band, or a choice's).</summary>
      private static IEnumerable<(string Where, AuthoredDefeat Defeat)> Defeats(AuthoredQuest q)
      {
         foreach (AuthoredStage s in q.Stages.Where(s => s != null))
         {
            if (s.Objective?.Spawn?.OnDefeat is AuthoredDefeat own) yield return ($"stage '{s.Id}'", own);
            foreach (AuthoredChoice c in (s.Choices ?? new List<AuthoredChoice>()).Where(c => c?.Objective?.Spawn?.OnDefeat != null))
               yield return ($"stage '{s.Id}', choice '{c.Id}'", c.Objective.Spawn.OnDefeat);
         }
      }

      /// <summary>A defeat leads to one of the tale's endings, or nowhere (the tale goes on); never to a stage.</summary>
      private static void ValidateDefeats(AuthoredQuest q, Dictionary<string, AuthoredOutcome> endings, List<string> blocking)
      {
         foreach ((string where, AuthoredDefeat d) in Defeats(q))
         {
            if (string.IsNullOrWhiteSpace(d.Next)) continue;
            string ending = EndingId(d.Next);
            if (ending == null)
               blocking.Add($"{where}: on_defeat.next '{d.Next}' must be an ending (\"end:<id>\"); leave it out and the tale goes on after a defeat.");
            else if (!endings.ContainsKey(ending))
               blocking.Add($"{where}: on_defeat leads to ending '{ending}', which \"endings\" does not define.");
         }
      }

      private static void CheckNext(string what, string next, HashSet<string> ids, Dictionary<string, AuthoredOutcome> endings, List<string> blocking)
      {
         string ending = EndingId(next);
         if (ending != null)
         {
            if (!endings.ContainsKey(ending)) blocking.Add($"{what} ending '{ending}', which \"endings\" does not define.");
         }
         else if (next != Reward && !ids.Contains(next)) blocking.Add($"{what} '{next}', which is no stage of this quest.");
      }

      /// <summary>A stage that offers choices: each must have a name, a way to be taken, and somewhere to lead.</summary>
      private static void ValidateChoiceStage(AuthoredQuest q, AuthoredStage s, bool isFirst, HashSet<string> ids, Dictionary<string, AuthoredOutcome> endings,
                                              List<string> blocking, List<string> cosmetic)
      {
         string where = $"stage '{s.Id}'";
         if (isFirst)
            blocking.Add($"{where} is the request, accepted in the conversation where the tale is offered, so it cannot offer choices (move them to the next stage).");

         bool isScene = s.Objective?.Type == "explore";
         string next = s.OnComplete?.Next;
         List<AuthoredChoice> choices = s.Choices.Where(c => c != null).ToList();
         List<AuthoredChoice> sceneChoices = choices.Where(c => c.Objective == null && c.Said == null).ToList();

         if (s.Objective != null)
         {
            bool defaultLeadsOn = isScene && sceneChoices.Any(c => c.Default);
            if (string.IsNullOrWhiteSpace(next))
            {
               if (!isScene) blocking.Add($"{where} has no on_complete.next (a stage id, \"reward\", or \"end:<id>\").");
               else if (!defaultLeadsOn)
                  blocking.Add($"{where}: a scene with choices needs a way out when it closes with none taken: an on_complete.next, or one choice marked \"default\": true.");
            }
            else CheckNext($"{where} leads to", next, ids, endings, blocking);

            ValidateObjective(where, s.Objective, false, isScene && sceneChoices.Count > 0, blocking, cosmetic);
         }
         else if (!string.IsNullOrWhiteSpace(next))
            cosmetic.Add($"{where} has no objective, so its on_complete.next is never used; its choices lead on.");

         var choiceIds = new HashSet<string>(StringComparer.Ordinal);
         foreach (AuthoredChoice c in choices)
         {
            if (string.IsNullOrWhiteSpace(c.Id)) { blocking.Add($"{where}: a choice has no id."); continue; }
            if (!choiceIds.Add(c.Id)) blocking.Add($"{where}: two choices are called '{c.Id}'.");
         }

         foreach (AuthoredChoice c in choices.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
         {
            string choice = $"{where}, choice '{c.Id}'";
            if (string.IsNullOrWhiteSpace(c.Next)) blocking.Add($"{choice} has no next (a stage id, \"reward\", or \"end:<id>\").");
            else CheckNext($"{choice} leads to", c.Next, ids, endings, blocking);

            if (c.Objective != null && c.Said != null)
               blocking.Add($"{choice} has both an objective and a \"said\"; give it one way to be taken.");
            else if (c.Objective != null)
               ValidateObjective(choice, c.Objective, true, false, blocking, cosmetic);
            else if (c.Said != null)
            {
               if (string.IsNullOrWhiteSpace(c.Said.To) || string.IsNullOrWhiteSpace(c.Said.Meaning))
                  blocking.Add($"{choice}: \"said\" needs \"to\" (a role, \"{{giver}}\") and \"meaning\" (what the player's words must mean).");
               else if (!SpeakingRoles.Contains(c.Said.To.Trim()))
                  blocking.Add($"{choice} is said to '{c.Said.To}', but a choice can be said only to {string.Join(" or ", SpeakingRoles)}.");
               else if (c.Said.To.Trim() == "{husband}" && q.Requires?.Husband == null)
                  blocking.Add($"{choice} is said to {{husband}}, but no husband is bound: add \"husband\" to \"requires\".");
               else if (c.Said.To.Trim() == "{rival}" && q.Requires?.Rival == null)
                  blocking.Add($"{choice} is said to {{rival}}, but no rival is bound: add \"rival\" to \"requires\".");
            }
            else if (!isScene)
               blocking.Add($"{choice} has no way to be taken: give it an objective or a \"said\" (only a choice in an explore stage is taken in the scene).");
            else
            {
               if (string.IsNullOrWhiteSpace(c.Label)) blocking.Add($"{choice} is made in a scene, so it needs a \"label\": the words the player picks it by.");
               if (!MarkerName.IsMatch(c.Id)) blocking.Add($"{choice} is made in a scene, so its id may hold only letters, digits and _ (the narrator writes it in a marker).");
            }

            if (c.Default && !sceneChoices.Contains(c))
               cosmetic.Add($"{choice} is marked default, which only matters for a choice made in a scene.");
         }

         List<AuthoredChoice> defaults = sceneChoices.Where(c => c.Default && !string.IsNullOrWhiteSpace(c.Id)).ToList();
         if (isScene && defaults.Count > 0)
         {
            if (!string.IsNullOrWhiteSpace(next))
               cosmetic.Add($"{where}: choice '{defaults[0].Id}' is marked default, but a scene closed with no choice follows on_complete.next; the mark is unused.");
            else
            {
               if (defaults[0].If != null && defaults[0].If.Count > 0)
                  blocking.Add($"{where}: the default choice '{defaults[0].Id}' cannot depend on flags, or the scene could close with nowhere to go.");
               if (defaults.Count > 1) cosmetic.Add($"{where}: several choices are marked default; only the first, '{defaults[0].Id}', is used.");
            }
         }

         if (s.Objective == null && choices.Count > 0 && choices.All(c => c.If != null && c.If.Count > 0))
            cosmetic.Add(q.Failure?.Days > 0
               ? $"{where}: every way out depends on flags; if none holds, the tale can only end when its time runs out."
               : $"{where}: every way out depends on flags; if none holds, the tale can never end.");
      }

      /// <summary>
      ///   Every stage the player can reach must be able to reach an ending (loops are fine when they have an exit). A
      ///   stage, or an ending, that no path reaches is reported: it is never played. A way out that leads nowhere (no
      ///   next, or one naming no stage or ending) is already refused above, so it is read as "could lead anywhere": it
      ///   neither makes its stage a dead end nor leaves anything "never reached", and the author sees the one real
      ///   problem rather than its echoes.
      /// </summary>
      private static void ValidatePaths(AuthoredQuest q, HashSet<string> ids, Dictionary<string, AuthoredOutcome> endings, List<string> blocking, List<string> cosmetic)
      {
         List<AuthoredStage> stages = q.Stages.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Id)).GroupBy(s => s.Id).Select(g => g.First()).ToList();
         if (stages.Count == 0) return;

         Dictionary<string, List<string>> leads = stages.ToDictionary(s => s.Id, Exits);
         bool Resolves(string next) => next == Reward || EndingId(next) is { } e && endings.ContainsKey(e);
         var unsure = new HashSet<string>(stages.Where(s => MissesANext(s) || leads[s.Id].Any(n => !Resolves(n) && !leads.ContainsKey(n))).Select(s => s.Id),
            StringComparer.Ordinal);

         var reachable = new HashSet<string>(StringComparer.Ordinal);
         var queue = new Queue<string>();
         if (q.Stages[0] != null && leads.ContainsKey(q.Stages[0].Id ?? "")) { reachable.Add(q.Stages[0].Id); queue.Enqueue(q.Stages[0].Id); }
         while (queue.Count > 0)
            foreach (string n in leads[queue.Dequeue()].Where(n => leads.ContainsKey(n) && reachable.Add(n)))
               queue.Enqueue(n);

         var canEnd = new HashSet<string>(StringComparer.Ordinal);
         for (var grew = true; grew;)
         {
            grew = false;
            foreach (AuthoredStage s in stages.Where(s => !canEnd.Contains(s.Id)))
               if (unsure.Contains(s.Id) || leads[s.Id].Any(n => Resolves(n) || canEnd.Contains(n))) grew = canEnd.Add(s.Id) | grew;
         }

         bool sure = !stages.Any(s => reachable.Contains(s.Id) && unsure.Contains(s.Id));
         foreach (AuthoredStage s in stages)
         {
            if (!reachable.Contains(s.Id))
            {
               if (sure) cosmetic.Add($"stage '{s.Id}' is never reached from the first stage, so it is never played.");
            }
            else if (!canEnd.Contains(s.Id)) blocking.Add($"stage '{s.Id}' can never reach an ending: every way out of it leads back without an exit.");
         }

         if (!sure) return;
         List<string> reached = stages.Where(s => reachable.Contains(s.Id)).SelectMany(s => leads[s.Id]).ToList();
         foreach (string e in endings.Keys.Where(k => !string.IsNullOrWhiteSpace(k) && !reached.Contains(EndPrefix + k)))
            cosmetic.Add($"ending '{e}' is never reached: no path leads to \"end:{e}\".");
         if (!reached.Contains(Reward) && HasContent(q.Reward))
            cosmetic.Add("the reward is never reached: no path leads to \"reward\" (name it \"end:<id>\" in \"endings\", or lead a path to it).");
      }

      /// <summary>A stage, or one of its choices, that needs a next and has none (already refused, with its own message).</summary>
      private static bool MissesANext(AuthoredStage s)
      {
         bool hasChoices = s.Choices != null && s.Choices.Count > 0;
         if (!hasChoices) return string.IsNullOrWhiteSpace(s.OnComplete?.Next);
         if (s.Choices.Any(c => c != null && string.IsNullOrWhiteSpace(c.Next))) return true;
         bool defaultLeadsOn = s.Objective?.Type == "explore" && s.Choices.Any(c => c != null && c.Objective == null && c.Said == null && c.Default);

         return s.Objective != null && string.IsNullOrWhiteSpace(s.OnComplete?.Next) && !defaultLeadsOn;
      }

      /// <summary>Where a stage's ways out lead: its own objective's next, then each choice's.</summary>
      private static List<string> Exits(AuthoredStage s)
      {
         var exits = new List<string>();
         bool hasChoices = s.Choices != null && s.Choices.Count > 0;
         if ((!hasChoices || s.Objective != null) && !string.IsNullOrWhiteSpace(s.OnComplete?.Next)) exits.Add(s.OnComplete.Next);
         if (hasChoices) exits.AddRange(s.Choices.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Next)).Select(c => c.Next));
         // A defeat that ends the tale is a way out too (Gabriel, 10/10/2026): its ending is reached.
         if (EndingId(s.Objective?.Spawn?.OnDefeat?.Next) != null) exits.Add(s.Objective.Spawn.OnDefeat.Next);
         if (hasChoices)
            exits.AddRange(s.Choices.Where(c => EndingId(c?.Objective?.Spawn?.OnDefeat?.Next) != null).Select(c => c.Objective.Spawn.OnDefeat.Next));

         return exits;
      }

      /// <summary>An outcome that says anything at all: it differs from a blank one (its days aside, which only failure reads).</summary>
      private static bool HasContent(AuthoredOutcome o)
         => o != null && !JToken.DeepEquals(JToken.FromObject(o), JToken.FromObject(new AuthoredOutcome {Days = o.Days}));

      /// <summary>A flag tested but set nowhere never holds: the author is told, since a choice or a line under it is dead.</summary>
      private static void ValidateFlags(AuthoredQuest q, List<string> cosmetic)
      {
         var set = new HashSet<string>(BuiltInFlags, StringComparer.Ordinal);
         var tested = new List<string>();

         void Tests(IEnumerable<string> conditions)
         {
            foreach (string c in conditions ?? Enumerable.Empty<string>())
            {
               string name = (c ?? "").Trim().TrimStart('!').Trim();
               if (!tested.Contains(name)) tested.Add(name);
            }
         }

         void Lines(IEnumerable<AuthoredLine> lines)
         {
            foreach (AuthoredLine l in lines ?? Enumerable.Empty<AuthoredLine>()) Tests(l?.If);
         }

         void Knowers(Dictionary<string, List<AuthoredLine>> knowers)
         {
            foreach (List<AuthoredLine> lines in (knowers ?? new Dictionary<string, List<AuthoredLine>>()).Values) Lines(lines);
         }

         foreach (AuthoredStage s in q.Stages.Where(s => s != null))
         {
            Lines(s.Canvas);
            Lines(s.Facts);
            Knowers(s.Knowers);
            AuthoredScene scene = s.Objective?.Scene;
            foreach (AuthoredFinding f in scene?.Findings ?? new List<AuthoredFinding>()) set.UnionWith(f?.Sets ?? new List<string>());
            foreach (AuthoredEncounter e in scene?.Encounters ?? new List<AuthoredEncounter>()) set.UnionWith(e?.Sets ?? new List<string>());

            foreach (AuthoredConsequence c in (s.OnEnter ?? new List<AuthoredConsequence>()).Where(c => c != null)) Tests(c.If);

            foreach (AuthoredChoice c in (s.Choices ?? new List<AuthoredChoice>()).Where(c => c != null))
            {
               Tests(c.If);
               set.UnionWith(c.Sets ?? new List<string>());
               Lines(c.Canvas);
               Lines(c.Facts);
               Knowers(c.Knowers);
            }
         }

         foreach (AuthoredOutcome o in new[] {q.Reward, q.Failure}.Concat((q.Endings ?? new Dictionary<string, AuthoredOutcome>()).Values).Where(o => o != null))
            foreach (AuthoredConsequence c in (o.Consequences ?? new List<AuthoredConsequence>()).Where(c => c != null))
               Tests(c.If);

         foreach (string name in tested)
         {
            if (name.Length == 0) cosmetic.Add("a flag test is empty; write a flag's name, or \"!name\" for not set.");
            else if (!set.Contains(name)) cosmetic.Add($"flag '{name}' is tested but never set anywhere, so \"{name}\" never holds and \"!{name}\" always does.");
         }
      }

      /// <summary>
      ///   One objective, of a stage or of a choice (<paramref name="where" /> names which). A choice may not keep watch
      ///   or open a scene: those belong to a stage of their own.
      /// </summary>
      private static void ValidateObjective(string where, AuthoredObjective o, bool ofChoice, bool sceneHasChoices, List<string> blocking, List<string> cosmetic)
      {
         if (o == null || string.IsNullOrWhiteSpace(o.Type))
         {
            blocking.Add($"{where} has no objective.");

            return;
         }

         if (!ObjectiveTypes.Contains(o.Type))
         {
            blocking.Add($"{where} asks for '{o.Type}', which the game cannot check yet (known: {string.Join(", ", ObjectiveTypes)}).");

            return;
         }

         if (ofChoice && !ChoiceObjectiveTypes.Contains(o.Type))
         {
            blocking.Add($"{where} asks for '{o.Type}', which a choice cannot do (a choice may: {string.Join(", ", ChoiceObjectiveTypes)}); lead it to a stage that does.");

            return;
         }

         switch (o.Type)
         {
            case "talk_to":
               if (string.IsNullOrWhiteSpace(o.Hero)) blocking.Add($"{where}: talk_to needs \"hero\".");
               if (!string.IsNullOrWhiteSpace(o.When) && !TalkMoments.Contains(o.When.Trim().ToLowerInvariant()))
                  blocking.Add($"{where}: talk_to \"when\" '{o.When}' is neither {string.Join(" nor ", TalkMoments)}.");
               break;
            case "talk_to_locals":
            case "be_at":
               if (string.IsNullOrWhiteSpace(o.Place)) blocking.Add($"{where}: {o.Type} needs \"place\".");
               if (o.Type == "talk_to_locals" && o.Count < 1) blocking.Add($"{where}: talk_to_locals needs a \"count\" of at least 1.");
               break;
            case "keep_watch":
               if (string.IsNullOrWhiteSpace(o.Place)) blocking.Add($"{where}: keep_watch needs \"place\".");
               if (o.Nights == null || o.Nights.Length != 2 || o.Nights[0] < 1 || o.Nights[1] < o.Nights[0])
                  blocking.Add($"{where}: keep_watch needs \"nights\": [first night it may happen, night it surely happens], with 1 <= first <= last.");
               if (o.Chance < 0 || o.Chance > 1) blocking.Add($"{where}: keep_watch \"chance\" must be between 0 and 1.");
               break;
            case "explore":
               ValidateScene(where, o.Scene, sceneHasChoices, blocking, cosmetic);
               break;
            case "defeat_party":
               if (o.Spawn == null || string.IsNullOrWhiteSpace(o.Spawn.Id) || string.IsNullOrWhiteSpace(o.Spawn.Near))
                  blocking.Add($"{where}: defeat_party needs \"spawn\" with an \"id\" and a \"near\".");
               else if (o.Spawn.Size == null || o.Spawn.Size.Length != 2 || o.Spawn.Size[0] < 1 || o.Spawn.Size[1] < o.Spawn.Size[0])
                  blocking.Add($"{where}: spawn \"size\" must be [fewest, most] with 1 <= fewest <= most.");
               else if (ofChoice && o.Spawn.FleeAfterDays > 0)
                  cosmetic.Add($"{where}: flee_after_days is not played on a choice's band; it stays until the stage ends.");
               break;
            case "wait":
               if (o.Days < 1) blocking.Add($"{where}: wait needs \"days\" of at least 1.");
               break;
            case "war_ends":
            case "dies":
            case "weds_player":
               if (string.IsNullOrWhiteSpace(o.Hero)) blocking.Add($"{where}: {o.Type} needs \"hero\".");
               break;
            case "bring":
               blocking.AddRange(TaleItemPolicy.ShapeProblems(where, "bring", o.Item, o.Count));
               break;
         }
      }

      private static void ValidateScene(string where, AuthoredScene scene, bool hasChoices, List<string> blocking, List<string> cosmetic)
      {
         if (scene?.Opening == null || string.IsNullOrWhiteSpace(scene.Opening.Canvas))
         {
            blocking.Add($"{where}: an explore scene needs an \"opening\" with a \"canvas\".");

            return;
         }

         List<AuthoredFinding> findings = scene.Findings ?? new List<AuthoredFinding>();
         if (!hasChoices && !findings.Any(f => f != null && f.Required))
            blocking.Add($"{where}: an explore scene needs at least one required finding, or it could never be completed.");
         if (findings.Where(f => f != null).GroupBy(f => f.Id).Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
            blocking.Add($"{where}: every finding needs its own id.");

         foreach (AuthoredHazard h in scene.Hazards ?? new List<AuthoredHazard>())
         {
            if (h == null) continue;
            if (string.IsNullOrWhiteSpace(h.Id) || string.IsNullOrWhiteSpace(h.Trigger))
               blocking.Add($"{where}: every hazard needs an \"id\" and a \"trigger\" (what the player must do to spring it).");
            if (h.Effect == null || !HazardEffects.Contains(h.Effect.Type))
               blocking.Add($"{where}: hazard '{h.Id}' has effect '{h.Effect?.Type}', which the game cannot carry out yet (known: {string.Join(", ", HazardEffects)}).");
         }

         IEnumerable<(string kind, string id)> named = findings.Where(f => f != null).Select(f => ("finding", f.Id))
                                                               .Concat((scene.Hazards ?? new List<AuthoredHazard>()).Where(h => h != null).Select(h => ("hazard", h.Id)))
                                                               .Concat((scene.Encounters ?? new List<AuthoredEncounter>()).Where(e => e != null).Select(e => ("encounter", e.Id)));
         foreach ((string kind, string id) in named.Where(n => !string.IsNullOrWhiteSpace(n.id) && !MarkerName.IsMatch(n.id)))
            cosmetic.Add($"{where}: {kind} '{id}' has a name its marker cannot carry (letters, digits and _ only), so the narration can never report it.");
      }
   }
}
