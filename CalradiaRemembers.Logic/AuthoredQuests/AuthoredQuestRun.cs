// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: the stage machine (docs/design/AUTHORED_QUESTS.md). A run is one quest being played,
// saved as JSON. Every transition is a fact the engine reports (a conversation opened, a night spent near a place, a
// finding revealed, a party beaten, a day passed); the model never moves a stage (Gabriel's rule: the game advances
// the quest, never the character's word). The first stage is the request itself, made in the conversation where the
// quest is offered, so accepting it completes that stage.
// 04/10/2026, branching tales (docs/design/quest-editor/goal.md): a stage may be left by several ways, its own
// objective and its open choices, and the first one met wins. A choice in a scene counts only by the narrator's
// marker, a choice by words only by the interpreter's report, and a closed choice (its flags not holding) never at
// all. A path may end on one of several named endings. Every field added to the run is additive: a run saved before
// reads with no flags, no choice taken and no ending, which is exactly what it had.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   public enum AuthoredQuestStatus
   {
      Active,
      Completed,
      Failed
   }

   /// <summary>One authored quest being played: who and where it was bound to, which stage, and the stage's progress.</summary>
   public sealed class AuthoredQuestRun
   {
      public string QuestId { get; set; }

      /// <summary>Role to the bound hero or settlement StringId ("giver" to "lord_1_7").</summary>
      public Dictionary<string, string> BoundIds { get; set; } = new();

      /// <summary>Role to the name said aloud ("village" to "Ortongard").</summary>
      public Dictionary<string, string> BoundNames { get; set; } = new();

      public string StageId { get; set; }
      public int StartDay { get; set; }
      public int StageStartDay { get; set; }
      public AuthoredQuestStatus Status { get; set; } = AuthoredQuestStatus.Active;

      /// <summary>talk_to_locals: the locals spoken to in this stage.</summary>
      public List<string> TalkedThisStage { get; set; } = new();

      /// <summary>keep_watch: nights spent near the place in this stage, and the last day counted.</summary>
      public int NightsWatched { get; set; }
      public int LastNightDay { get; set; } = -1;

      /// <summary>explore: findings revealed and hazards sprung, each once.</summary>
      public List<string> Findings { get; set; } = new();
      public List<string> HazardsSprung { get; set; } = new();

      /// <summary>A hazard warned the quest's band (alert): it will be harder to catch when it is hunted.</summary>
      public bool BandAlerted { get; set; }

      /// <summary>explore: the people of the scene met so far (the narrator voices them in increment 1).</summary>
      public List<string> Met { get; set; } = new();

      /// <summary>defeat_party: the StringId of the party spawned for this stage.</summary>
      public string SpawnedPartyId { get; set; }

      /// <summary>
      ///   The stage a conversation completed on opening, and with whom: while that conversation lasts, that person speaks
      ///   of THAT stage (the lord hearing the report), not of the next one. Cleared when the conversation closes.
      /// </summary>
      public string SpokenStageId { get; set; }
      public string SpokenHeroId { get; set; }

      /// <summary>The flags set so far in this run (branching tales, 04/10/2026; absent from a run saved before).</summary>
      public List<string> Flags { get; set; } = new();

      /// <summary>
      ///   The giver's regard for the player when the tale began (Gabriel, 24/10/2026: a night went well when the regard rose);
      ///   null on a run saved before it was kept. See TaleRegardFlags.
      /// </summary>
      public int? GiverRegardAtStart { get; set; }

      /// <summary>The picture or video chosen from each stage's image_pool for this playing of the tale, by stage id.</summary>
      public Dictionary<string, string> ImagePicks { get; set; } = new();

      /// <summary>Every choice taken, as "stage/choice", in the order taken: the path the player walked.</summary>
      public List<string> ChoicesTaken { get; set; } = new();

      /// <summary>The named ending the tale reached ("end:silence" gives "silence"); null for the reward, or still under way.</summary>
      public string EndingId { get; set; }

      /// <summary>
      ///   talk_to_locals for a choice: the locals heard in this stage, as "place|hero", so two ways out that question
      ///   two different places each count their own people. The stage's own objective still counts TalkedThisStage.
      /// </summary>
      public List<string> LocalsHeard { get; set; } = new();

      /// <summary>defeat_party for a choice: the band spawned for each such choice of this stage, by choice id.</summary>
      public Dictionary<string, string> ChoiceBands { get; set; } = new();

      /// <summary>
      ///   The defeats already told (stage, way and the day the stage began), so a band beating the player twice is told
      ///   once (Gabriel, 10/10/2026). Added 10/10/2026: absent in older saves, where none was told.
      /// </summary>
      public List<string> DefeatsTold { get; set; } = new();

      /// <summary>
      ///   war_ends: the roles whose realm was seen at war with the player's during this stage (Gabriel, 18/10/2026): the
      ///   stage's own war is the one whose end it waits for, so a stage begun before its war is declared never reads the
      ///   peace of its first day as the war's end. Added 18/10/2026: absent in older saves, where none was seen.
      /// </summary>
      public List<string> WarsSeen { get; set; } = new();

      /// <summary>explore: the choice the narration took in the scene, carried out when the scene closes.</summary>
      public string SceneChoiceId { get; set; }

      /// <summary>
      ///   The choice the conversation now open took (with <see cref="SpokenHeroId" />): that person speaks from the
      ///   choice's own lines while it lasts. Cleared with the other two.
      /// </summary>
      public string SpokenChoiceId { get; set; }

      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      public static AuthoredQuestRun Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return null;
         try { return JsonConvert.DeserializeObject<AuthoredQuestRun>(json); }
         catch (JsonException) { return null; }
      }
   }

   /// <summary>What a reported fact did to the run.</summary>
   public enum AuthoredStepKind
   {
      None,
      Advanced,
      Completed,
      Failed,

      /// <summary>keep_watch: tonight the scene opens.</summary>
      OpenScene
   }

   public sealed class AuthoredStep
   {
      public AuthoredStepKind Kind { get; init; }
      public string FromStage { get; init; }
      public string ToStage { get; init; }

      /// <summary>The choice that was taken, or null when the stage's own objective was met.</summary>
      public string ChoiceId { get; init; }

      /// <summary>Completed: the named ending reached, or null for the reward.</summary>
      public string EndingId { get; init; }

      public static readonly AuthoredStep Nothing = new() {Kind = AuthoredStepKind.None};
   }

   /// <summary>What a lost fight against a tale's band makes of the tale (AuthoredQuestEngine.PlayerLostTo).</summary>
   public sealed class AuthoredDefeatResult
   {
      /// <summary>The band's on_defeat; null when the band is not the tale's, or says nothing of a defeat.</summary>
      public AuthoredDefeat OnDefeat { get; init; }

      /// <summary>The defeat is told now (memories, news, journal): the first time on this stage.</summary>
      public bool Tell { get; init; }

      /// <summary>The ending reached, when on_defeat names one; Nothing when the tale goes on.</summary>
      public AuthoredStep Step { get; init; } = AuthoredStep.Nothing;

      public static readonly AuthoredDefeatResult None = new();
   }

   /// <summary>One way out of the current stage: its own objective (no choice), or an open choice.</summary>
   public sealed class AuthoredWay
   {
      public AuthoredObjective Objective { get; init; }
      public string Next { get; init; }

      /// <summary>Null for the stage's own objective.</summary>
      public AuthoredChoice Choice { get; init; }
   }

   /// <summary>What the game reads of one of the tale's people, for the ways out the world takes (war_ends, dies, weds_player).</summary>
   public sealed class TaleRoleState
   {
      /// <summary>No longer in the world: dead, or gone from it (a hero the game could not find counts as gone).</summary>
      public bool Gone { get; init; }

      /// <summary>Married to the player.</summary>
      public bool WedToPlayer { get; init; }

      /// <summary>This person's realm (or house, when it has none) is at war with the player's.</summary>
      public bool AtWarWithPlayer { get; init; }
   }

   /// <summary>The stage machine. Pure: the engine reports facts, this says what they do.</summary>
   public static class AuthoredQuestEngine
   {
      /// <summary>Starts a run bound to the given roles, at the first stage.</summary>
      public static AuthoredQuestRun Start(AuthoredQuest q, IDictionary<string, string> ids, IDictionary<string, string> names, int today)
         => new() {
            QuestId = q.Id, BoundIds = new Dictionary<string, string>(ids ?? new Dictionary<string, string>()),
            BoundNames = new Dictionary<string, string>(names ?? new Dictionary<string, string>()),
            StageId = q.Stages[0].Id, StartDay = today, StageStartDay = today
         };

      /// <summary>The player accepted in the offering conversation: the request (the first stage) is done.</summary>
      public static AuthoredStep Accept(AuthoredQuestRun run, AuthoredQuest q, int today)
         => run?.StageId == q.Stages[0].Id ? GoTo(run, q, q.Stages[0], null, q.Stages[0].OnComplete?.Next, today) : AuthoredStep.Nothing;

      public static AuthoredStage Current(AuthoredQuestRun run, AuthoredQuest q) => q?.Stages.FirstOrDefault(s => s.Id == run?.StageId);

      /// <summary>
      ///   The ways out of the current stage the player may take now, in the file's order: the stage's own objective
      ///   first, then every choice that is open (its flags hold). A closed choice is never among them.
      /// </summary>
      public static IReadOnlyList<AuthoredWay> Ways(AuthoredQuestRun run, AuthoredQuest q)
      {
         AuthoredStage s = Active(run, q);
         if (s == null) return Array.Empty<AuthoredWay>();

         var ways = new List<AuthoredWay>();
         if (s.Objective != null && !string.IsNullOrWhiteSpace(s.Objective.Type)) ways.Add(new AuthoredWay {Objective = s.Objective, Next = s.OnComplete?.Next});
         ways.AddRange(OpenChoices(run, q).Where(c => c.Objective != null).Select(c => new AuthoredWay {Objective = c.Objective, Next = c.Next, Choice = c}));

         return ways;
      }

      /// <summary>The choices of the current stage whose flags hold, in the file's order.</summary>
      public static IReadOnlyList<AuthoredChoice> OpenChoices(AuthoredQuestRun run, AuthoredQuest q)
         => (Active(run, q)?.Choices ?? new List<AuthoredChoice>())
            .Where(c => c != null && AuthoredFlags.Hold(c.If, run.Flags) && CanPay(CostOf(c, q), PlayerGold?.Invoke())).ToList();

      /// <summary>
      ///   The player's gold, read when the choices are listed (set by the mod at load; null in tests and before, when every
      ///   choice counts as affordable). tashmetu (20/10/2026): an ending the player pays for (a fine, a bribe, a child's keep).
      /// </summary>
      public static Func<int> PlayerGold { get; set; }

      /// <summary>
      ///   What taking this choice costs the player: the gold of the ending it leads straight to, when that ending makes the
      ///   player pay; 0 otherwise (a choice that leads to a stage costs nothing yet). Gating before declaration: a choice the
      ///   player cannot pay for is never offered, rather than taken and then refuted by an empty purse.
      /// </summary>
      public static int CostOf(AuthoredChoice c, AuthoredQuest q)
      {
         if (q == null || !AuthoredQuestLoader.IsEnding(c?.Next)) return 0;
         string id = AuthoredQuestLoader.EndingId(c.Next);
         AuthoredOutcome o = id == null ? q.Reward : q.Endings != null && q.Endings.TryGetValue(id, out AuthoredOutcome e) ? e : null;

         return o != null && o.Gold < 0 ? -o.Gold : 0;
      }

      /// <summary>Whether the player can pay <paramref name="cost" />; an unknown purse can (tests, before the mod sets it).</summary>
      public static bool CanPay(int cost, int? playerGold) => cost <= 0 || playerGold == null || playerGold.Value >= cost;

      /// <summary>The open choices made in the scene (no act, no words): what the narrator is told and the player is shown.</summary>
      public static IReadOnlyList<AuthoredChoice> SceneChoices(AuthoredQuestRun run, AuthoredQuest q)
         => Current(run, q)?.Objective?.Type == "explore" ? OpenChoices(run, q).Where(IsSceneChoice).ToList() : new List<AuthoredChoice>();

      /// <summary>
      ///   The scene has nothing left the tale needs: every required finding found, and no way out to choose (tashmetu,
      ///   07/10/2026: the narrator kept asking "What do you do?" until the player closed the chat). The player may end it.
      /// </summary>
      public static bool SceneMayEnd(AuthoredQuestRun run, AuthoredQuest q)
      {
         AuthoredScene scene = Current(run, q)?.Objective?.Type == "explore" ? Current(run, q).Objective.Scene : null;
         if (scene == null || SceneChoices(run, q).Count > 0) return false;
         List<AuthoredFinding> required = scene.Findings.Where(f => f != null && f.Required).ToList();

         return required.Count > 0 && required.All(f => run.Findings.Contains(f.Id));
      }

      /// <summary>
      ///   The journal for the current stage: its own line, then the line of each open choice that has one (Gabriel's
      ///   goal: the journal shows the ways out the author chose to name). Placeholders unfilled.
      /// </summary>
      public static IReadOnlyList<string> JournalLines(AuthoredQuestRun run, AuthoredQuest q)
      {
         AuthoredStage s = Current(run, q);
         if (s == null) return Array.Empty<string>();

         var lines = new List<string>();
         if (!string.IsNullOrWhiteSpace(s.Journal)) lines.Add(s.Journal);
         lines.AddRange((s.Choices ?? new List<AuthoredChoice>()).Where(c => c != null && AuthoredFlags.Hold(c.If, run.Flags) && !string.IsNullOrWhiteSpace(c.Journal))
                                                                 .Select(c => c.Journal));

         return lines;
      }

      /// <summary>
      ///   The player said no to a scenario tale's popup whose no is an ending (on_scenario.decline_ending): the tale goes
      ///   there at once, from its first stage. Nothing when the tale names no such ending.
      /// </summary>
      public static AuthoredStep Declined(AuthoredQuestRun run, AuthoredQuest q, int today)
      {
         string ending = q?.OnScenario?.DeclineEnding?.Trim();
         if (run == null || string.IsNullOrEmpty(ending) || q.Endings == null || !q.Endings.ContainsKey(ending)) return AuthoredStep.Nothing;

         return GoTo(run, q, Current(run, q), null, "end:" + ending, today);
      }

      /// <summary>A CR conversation opened with this hero, who belongs to <paramref name="localOfPlaceId" /> (or null).</summary>
      public static AuthoredStep ConversationOpened(AuthoredQuestRun run, AuthoredQuest q, string heroId, string localOfPlaceId, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || string.IsNullOrEmpty(heroId)) return AuthoredStep.Nothing;

         IReadOnlyList<AuthoredWay> ways = Ways(run, q);
         if (localOfPlaceId != null) Heard(run, ways, heroId, localOfPlaceId);

         foreach (AuthoredWay way in ways)
         {
            bool met = way.Objective.Type switch {
               "talk_to" => heroId == Bound(run, way.Objective.Hero) && !EndsOnClose(way.Objective),
               "return" => heroId == Bound(run, "{giver}"),

               // The one whose conversation completes the questioning still speaks from it (Gabriel's play test, 04/10/2026:
               // the second villager, with the stage already moved on, believed nothing and said "bandits").
               "talk_to_locals" => localOfPlaceId != null && localOfPlaceId == Bound(run, way.Objective.Place)
                                   && LocalsCounted(run, way) >= Math.Max(1, way.Objective.Count),
               _ => false
            };
            if (met) return Spoken(run, q, s, way, heroId, today);
         }

         return AuthoredStep.Nothing;
      }

      /// <summary>
      ///   The `bring` open now in a conversation with this hero: the stage's own, else the first open choice's (tashmetu,
      ///   20/10/2026, items in tales). Null when nothing is to be handed to them. The bag offers the hand-over from it.
      /// </summary>
      public static AuthoredWay DeliveryFor(AuthoredQuestRun run, AuthoredQuest q, string heroId)
         => string.IsNullOrEmpty(heroId)
            ? null
            : Ways(run, q).FirstOrDefault(w => w.Objective.Type == "bring" && heroId == Bound(run, TaleItemPolicy.Receiver(w.Objective)));

      /// <summary>
      ///   The goods of the open `bring` were handed to this hero, the whole count (the hand-over took them, or nothing):
      ///   the way is taken as a conversation takes one, so they speak from where it leads.
      /// </summary>
      public static AuthoredStep Delivered(AuthoredQuestRun run, AuthoredQuest q, string heroId, int today)
      {
         AuthoredStage s = Active(run, q);
         AuthoredWay way = DeliveryFor(run, q, heroId);

         return s == null || way == null ? AuthoredStep.Nothing : Spoken(run, q, s, way, heroId, today);
      }

      /// <summary>
      ///   The conversation with this hero ended: a talk_to that completes on the close ("when": "closed") is met now, so
      ///   everything said in it already counts (Gabriel, 24/10/2026: a night judged by what it left).
      /// </summary>
      public static AuthoredStep ConversationEnded(AuthoredQuestRun run, AuthoredQuest q, string heroId, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || string.IsNullOrEmpty(heroId)) return AuthoredStep.Nothing;

         AuthoredWay way = Ways(run, q).FirstOrDefault(w => w.Objective.Type == "talk_to" && EndsOnClose(w.Objective) && heroId == Bound(run, w.Objective.Hero));

         return way == null ? AuthoredStep.Nothing : GoTo(run, q, s, way.Choice, way.Next, today);
      }

      /// <summary>A talk_to that completes its stage when the conversation ends rather than when it opens.</summary>
      public static bool EndsOnClose(AuthoredObjective o) => string.Equals(o?.When?.Trim(), "closed", StringComparison.OrdinalIgnoreCase);

      /// <summary>The conversation with this hero closed: they stop speaking of the stage it completed.</summary>
      public static void ConversationClosed(AuthoredQuestRun run, string heroId)
      {
         if (run == null || run.SpokenHeroId != heroId) return;
         run.SpokenHeroId = null;
         run.SpokenStageId = null;
         run.SpokenChoiceId = null;
      }

      /// <summary>
      ///   A choice taken by words: the action interpreter reported, in a conversation with this hero, that the player
      ///   said what the choice means. It counts only for a choice the current stage declares, open, said to that hero.
      /// </summary>
      public static AuthoredStep Said(AuthoredQuestRun run, AuthoredQuest q, string heroId, string choiceId, int today)
      {
         AuthoredStage s = Active(run, q);
         AuthoredChoice c = OpenChoices(run, q).FirstOrDefault(x => x.Id == choiceId && x.Said != null && x.Objective == null);
         if (s == null || c == null || string.IsNullOrEmpty(heroId) || heroId != Bound(run, c.Said.To)) return AuthoredStep.Nothing;

         return Spoken(run, q, s, new AuthoredWay {Next = c.Next, Choice = c}, heroId, today);
      }

      /// <summary>The choices by words open now in a conversation with this hero: what the interpreter may be taught.</summary>
      public static IReadOnlyList<AuthoredChoice> SaidChoicesFor(AuthoredQuestRun run, AuthoredQuest q, string heroId)
         => string.IsNullOrEmpty(heroId)
            ? new List<AuthoredChoice>()
            : OpenChoices(run, q).Where(c => c.Said != null && c.Objective == null && Bound(run, c.Said.To) == heroId).ToList();

      private static AuthoredStep Spoken(AuthoredQuestRun run, AuthoredQuest q, AuthoredStage s, AuthoredWay way, string heroId, int today)
      {
         run.SpokenStageId = s.Id;
         run.SpokenHeroId = heroId;
         run.SpokenChoiceId = way.Choice?.Id;

         AuthoredStep step = GoTo(run, q, s, way.Choice, way.Next, today);

         // Gabriel (10/10/2026): stages that are states of one conversation. When the stage reached is itself decided by
         // words with the same character, and the choice says nothing of its own (no canvas, facts or knowers, which
         // the character would speak from), the conversation goes on in it: they speak from the new stage, not the old.
         bool choiceSpeaks = way.Choice != null && (way.Choice.Canvas?.Count > 0 || way.Choice.Facts?.Count > 0 || way.Choice.Knowers?.Count > 0);
         if (!choiceSpeaks && run.Status == AuthoredQuestStatus.Active && run.StageId != s.Id && SaidChoicesFor(run, q, heroId).Count > 0)
         {
            run.SpokenStageId = null;
            run.SpokenHeroId = null;
            run.SpokenChoiceId = null;
         }

         return step;
      }

      /// <summary>
      ///   be_at: the player's party is within the radius of this place. The first way out that asks to be there is
      ///   taken (the engine side reads the radii from <see cref="PlacesToReach" />).
      /// </summary>
      public static AuthoredStep Arrived(AuthoredQuestRun run, AuthoredQuest q, string placeId, int today)
      {
         AuthoredStage s = Active(run, q);
         AuthoredWay way = Ways(run, q).FirstOrDefault(w => w.Objective.Type == "be_at" && placeId != null && placeId == Bound(run, w.Objective.Place));

         return s != null && way != null ? GoTo(run, q, s, way.Choice, way.Next, today) : AuthoredStep.Nothing;
      }

      /// <summary>be_at: the places the player could arrive at now, each with its radius, in the order they are checked.</summary>
      public static IReadOnlyList<AuthoredObjective> PlacesToReach(AuthoredQuestRun run, AuthoredQuest q)
         => Ways(run, q).Where(w => w.Objective.Type == "be_at").Select(w => w.Objective).ToList();

      /// <summary>
      ///   keep_watch: a night passed with the player near (or not near) the place. Counted once a day; from the first
      ///   eligible night a seeded roll may open the scene, and on the last it opens for certain (Gabriel, 03/10/2026:
      ///   nights spent waiting, no moon). Seeded by quest and day, so a reload does not reroll it. Only a stage's own
      ///   objective keeps watch: a choice cannot (the loader refuses it).
      /// </summary>
      public static AuthoredStep NightPassed(AuthoredQuestRun run, AuthoredQuest q, bool nearPlace, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s?.Objective?.Type != "keep_watch" || !nearPlace || run.LastNightDay == today) return AuthoredStep.Nothing;

         run.LastNightDay = today;
         run.NightsWatched++;

         int first = s.Objective.Nights[0], last = s.Objective.Nights[1];
         if (run.NightsWatched < first) return AuthoredStep.Nothing;
         bool opens = run.NightsWatched >= last || Roll(run.QuestId, today) < s.Objective.Chance;
         if (!opens) return AuthoredStep.Nothing;

         return GoTo(run, q, s, null, s.OnComplete?.Next, today) is {Kind: AuthoredStepKind.Advanced} step
            ? new AuthoredStep {Kind = AuthoredStepKind.OpenScene, FromStage = step.FromStage, ToStage = step.ToStage}
            : AuthoredStep.Nothing;
      }

      /// <summary>explore: the narration revealed a finding. True only for a finding the scene declares, once; its flags are set.</summary>
      public static bool Found(AuthoredQuestRun run, AuthoredQuest q, string findingId)
      {
         AuthoredScene scene = Active(run, q)?.Objective?.Scene;
         AuthoredFinding f = scene?.Findings.FirstOrDefault(x => x.Id == findingId);
         if (f == null || run.Findings.Contains(findingId)) return false;
         run.Findings.Add(findingId);
         SetFlags(run, f.Sets);

         return true;
      }

      /// <summary>explore: the narration sprang a hazard. Its effect for the game to carry out, once; null otherwise.</summary>
      public static AuthoredHazardEffect Sprung(AuthoredQuestRun run, AuthoredQuest q, string hazardId)
      {
         AuthoredHazard h = Active(run, q)?.Objective?.Scene?.Hazards.FirstOrDefault(x => x.Id == hazardId);
         if (h == null || run.HazardsSprung.Contains(hazardId)) return null;
         run.HazardsSprung.Add(hazardId);

         return h.Effect;
      }

      /// <summary>explore: the narration brought someone in. True only for an encounter the scene declares, once; its flags are set.</summary>
      public static bool Meet(AuthoredQuestRun run, AuthoredQuest q, string encounterId)
      {
         AuthoredScene scene = Active(run, q)?.Objective?.Scene;
         AuthoredEncounter e = scene?.Encounters.FirstOrDefault(x => x.Id == encounterId);
         if (e == null || run.Met.Contains(encounterId)) return false;
         run.Met.Add(encounterId);
         SetFlags(run, e.Sets);

         return true;
      }

      /// <summary>
      ///   explore: the narration took a choice of the scene (its marker). Accepted only for a choice the scene declares,
      ///   made in the scene, open now, and only the first: the scene then ends and the story follows it on closing.
      /// </summary>
      public static bool ChooseInScene(AuthoredQuestRun run, AuthoredQuest q, string choiceId)
      {
         if (run == null || run.SceneChoiceId != null || SceneChoices(run, q).All(c => c.Id != choiceId)) return false;
         run.SceneChoiceId = choiceId;

         return true;
      }

      /// <summary>
      ///   explore: the scene closed. The required findings the narration never reached, to be stated at closing so a
      ///   clue is missed only in its manner, never in substance (their flags are set too); then the stage is left by
      ///   the choice the scene took, or else by its on_complete.next, or else by its first default choice.
      /// </summary>
      public static IReadOnlyList<AuthoredFinding> SceneClosed(AuthoredQuestRun run, AuthoredQuest q, int today, out AuthoredStep step)
      {
         AuthoredStage s = Active(run, q);
         AuthoredScene scene = s?.Objective?.Scene;
         if (scene == null || s.Objective.Type != "explore")
         {
            step = AuthoredStep.Nothing;

            return Array.Empty<AuthoredFinding>();
         }

         List<AuthoredFinding> missed = scene.Findings.Where(f => f.Required && !run.Findings.Contains(f.Id)).ToList();
         run.Findings.AddRange(missed.Select(f => f.Id));
         foreach (AuthoredFinding f in missed) SetFlags(run, f.Sets);

         IReadOnlyList<AuthoredChoice> open = SceneChoices(run, q);
         AuthoredChoice chosen = open.FirstOrDefault(c => c.Id == run.SceneChoiceId);
         if (chosen != null) step = GoTo(run, q, s, chosen, chosen.Next, today);
         else if (!string.IsNullOrWhiteSpace(s.OnComplete?.Next)) step = GoTo(run, q, s, null, s.OnComplete.Next, today);
         else if (open.FirstOrDefault(c => c.Default) is { } fallback) step = GoTo(run, q, s, fallback, fallback.Next, today);
         else step = AuthoredStep.Nothing;

         return missed;
      }

      /// <summary>defeat_party: a party was beaten by the player; the way out it belongs to is taken, if it was spawned for one.</summary>
      public static AuthoredStep PartyDefeated(AuthoredQuestRun run, AuthoredQuest q, string partyId, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || partyId == null) return AuthoredStep.Nothing;

         AuthoredWay way = Ways(run, q).FirstOrDefault(w => w.Objective.Type == "defeat_party"
                                                            && (w.Choice == null ? partyId == run.SpawnedPartyId
                                                                   : run.ChoiceBands.TryGetValue(w.Choice.Id, out string band) && band == partyId));

         return way != null ? GoTo(run, q, s, way.Choice, way.Next, today) : AuthoredStep.Nothing;
      }

      /// <summary>
      ///   The player lost a fight to a band (Gabriel, 10/10/2026). The tale's own band's on_defeat is told once per stage
      ///   and, when it names an ending, the tale ends there; otherwise it goes on. Another party's victory is no concern
      ///   of the tale's.
      /// </summary>
      public static AuthoredDefeatResult PlayerLostTo(AuthoredQuestRun run, AuthoredQuest q, string partyId, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || partyId == null) return AuthoredDefeatResult.None;

         AuthoredWay way = Ways(run, q).FirstOrDefault(w => w.Objective.Type == "defeat_party"
                                                            && (w.Choice == null ? partyId == run.SpawnedPartyId
                                                                   : run.ChoiceBands.TryGetValue(w.Choice.Id, out string band) && band == partyId));
         AuthoredDefeat onDefeat = way?.Objective.Spawn?.OnDefeat;
         if (onDefeat == null) return AuthoredDefeatResult.None;

         run.DefeatsTold ??= new List<string>();
         string key = $"{s.Id}/{way.Choice?.Id}@{run.StageStartDay}";
         bool tell = !run.DefeatsTold.Contains(key);
         if (tell) run.DefeatsTold.Add(key);

         AuthoredStep step = AuthoredQuestLoader.EndingId(onDefeat.Next) != null
            ? GoTo(run, q, s, null, onDefeat.Next, today)
            : AuthoredStep.Nothing;

         return new AuthoredDefeatResult {OnDefeat = onDefeat, Tell = tell, Step = step};
      }

      /// <summary>defeat_party: the bands the current stage hunts now, the stage's own (null choice) and each open choice's.</summary>
      public static IReadOnlyList<AuthoredWay> BandsToHunt(AuthoredQuestRun run, AuthoredQuest q)
         => Ways(run, q).Where(w => w.Objective.Type == "defeat_party" && w.Objective.Spawn != null).ToList();

      /// <summary>
      ///   A day passed: the quest fails past its time limit, or when the stage's spawned band has had time to flee;
      ///   otherwise a way out that waits (wait) is taken once its days have passed with nothing else taken.
      /// </summary>
      public static AuthoredStep Day(AuthoredQuestRun run, AuthoredQuest q, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null) return AuthoredStep.Nothing;

         bool timedOut = q.Failure?.Days > 0 && today - run.StartDay > q.Failure.Days;
         bool fled = s.Objective?.Type == "defeat_party" && s.Objective.Spawn?.FleeAfterDays > 0 && today - run.StageStartDay > s.Objective.Spawn.FleeAfterDays;
         if (timedOut || fled)
         {
            run.Status = AuthoredQuestStatus.Failed;

            return new AuthoredStep {Kind = AuthoredStepKind.Failed, FromStage = s.Id};
         }

         AuthoredWay waited = Ways(run, q).FirstOrDefault(w => w.Objective.Type == "wait" && today - run.StageStartDay >= Math.Max(1, w.Objective.Days));

         return waited != null ? GoTo(run, q, s, waited.Choice, waited.Next, today) : AuthoredStep.Nothing;
      }

      /// <summary>The ways out the world takes rather than the player (Gabriel, 18/10/2026, the Troy-inspired tale's war).</summary>
      public static readonly IReadOnlyList<string> WorldTypes = new[] {"war_ends", "dies", "weds_player"};

      /// <summary>The roles the current stage's world exits name, as written ("{giver}"): the ones the game must read.</summary>
      public static IReadOnlyList<string> RolesWatched(AuthoredQuestRun run, AuthoredQuest q)
         => Ways(run, q).Where(w => WorldTypes.Contains(w.Objective.Type) && !string.IsNullOrWhiteSpace(w.Objective.Hero))
                        .Select(w => w.Objective.Hero).Distinct().ToList();

      /// <summary>
      ///   The world as the game reads it now, by role: the first world exit of the current stage that holds is taken, in
      ///   the file's order. dies: the person is gone from the world. weds_player: married to the player. war_ends: their
      ///   realm was at war with the player's during this stage and is no longer. A role the game did not read moves nothing.
      /// </summary>
      public static AuthoredStep WorldNow(AuthoredQuestRun run, AuthoredQuest q, IReadOnlyDictionary<string, TaleRoleState> roles, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || roles == null) return AuthoredStep.Nothing;

         List<AuthoredWay> ways = Ways(run, q).Where(w => WorldTypes.Contains(w.Objective.Type) && !string.IsNullOrWhiteSpace(w.Objective.Hero)).ToList();
         foreach (AuthoredWay w in ways.Where(w => w.Objective.Type == "war_ends"))
            if (roles.TryGetValue(w.Objective.Hero, out TaleRoleState st) && st != null && st.AtWarWithPlayer && !run.WarsSeen.Contains(w.Objective.Hero))
               run.WarsSeen.Add(w.Objective.Hero);

         foreach (AuthoredWay w in ways)
         {
            if (!roles.TryGetValue(w.Objective.Hero, out TaleRoleState st) || st == null) continue;
            bool holds = w.Objective.Type switch
            {
               "dies" => st.Gone,
               "weds_player" => st.WedToPlayer,
               "war_ends" => !st.AtWarWithPlayer && run.WarsSeen.Contains(w.Objective.Hero),
               _ => false
            };
            if (holds) return GoTo(run, q, s, w.Choice, w.Next, today);
         }

         return AuthoredStep.Nothing;
      }

      /// <summary>
      ///   A test lever only (cr.story_step): completes the active stage as though its objective were met; a stage
      ///   with no objective is left by its first open choice.
      /// </summary>
      public static AuthoredStep ForceAdvance(AuthoredQuestRun run, AuthoredQuest q, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null) return AuthoredStep.Nothing;
         if (s.Objective != null && !string.IsNullOrWhiteSpace(s.OnComplete?.Next)) return GoTo(run, q, s, null, s.OnComplete.Next, today);

         AuthoredChoice first = OpenChoices(run, q).FirstOrDefault();

         return first != null ? GoTo(run, q, s, first, first.Next, today) : AuthoredStep.Nothing;
      }

      /// <summary>A test lever only (cr.story_choose): takes this choice of the active stage, if it is open, however it is taken.</summary>
      public static AuthoredStep ForceChoice(AuthoredQuestRun run, AuthoredQuest q, string choiceId, int today)
      {
         AuthoredStage s = Active(run, q);
         AuthoredChoice c = OpenChoices(run, q).FirstOrDefault(x => x.Id == choiceId);

         return s != null && c != null ? GoTo(run, q, s, c, c.Next, today) : AuthoredStep.Nothing;
      }

      /// <summary>What the tale left when it ended: the named ending it reached, the reward, or the failure.</summary>
      public static AuthoredOutcome Outcome(AuthoredQuestRun run, AuthoredQuest q)
      {
         if (run == null || q == null) return null;
         if (run.Status == AuthoredQuestStatus.Failed) return q.Failure;
         if (run.EndingId != null) return q.Endings != null && q.Endings.TryGetValue(run.EndingId, out AuthoredOutcome e) ? e : null;

         return q.Reward;
      }

      /// <summary>An outcome's consequences that hold for the path this run walked (a consequence under a flag only when it holds).</summary>
      public static IReadOnlyList<AuthoredConsequence> Consequences(AuthoredQuestRun run, AuthoredOutcome outcome)
         => Consequences(run, outcome?.Consequences);

      /// <summary>Those of these consequences that hold for the path this run walked.</summary>
      public static IReadOnlyList<AuthoredConsequence> Consequences(AuthoredQuestRun run, IEnumerable<AuthoredConsequence> consequences)
         => (consequences ?? new List<AuthoredConsequence>()).Where(c => c != null && AuthoredFlags.Hold(c.If, run?.Flags)).ToList();

      /// <summary>
      ///   tashmetu (20/10/2026): "Could locals_of leave out anyone the same ending already gives a memory to? Then a tale can
      ///   speak to the giver and to the village without the giver hearing it twice." The heroes these consequences (and the
      ///   outcome's own memory, which goes to the giver) already address by name: a "locals_of:" memory leaves them out. Two
      ///   memories an author aims at one hero by name both stand; only the crowd yields to the named.
      /// </summary>
      public static ISet<string> NamedMemoryHolders(AuthoredQuestRun run, AuthoredOutcome outcome, IEnumerable<AuthoredConsequence> consequences)
      {
         var named = new HashSet<string>();
         if (outcome != null && !string.IsNullOrWhiteSpace(outcome.Memory) && Bound(run, "{giver}") is string giver) named.Add(giver);
         foreach (AuthoredConsequence c in consequences ?? Enumerable.Empty<AuthoredConsequence>())
         {
            if (c?.Type != "memory" || string.IsNullOrEmpty(c.Who) || c.Who.StartsWith(LocalsOf)) continue;
            if (Bound(run, c.Who) is string id) named.Add(id);
         }

         return named;
      }

      /// <summary>The prefix of a memory aimed at the notables of a bound place: "locals_of:{village}".</summary>
      public const string LocalsOf = "locals_of:";

      /// <summary>
      ///   tashmetu (15/10/2026): what the current stage carries out as the tale enters it, under the flags set so far (the
      ///   choice that led here included). The first stage is the request and enters nothing.
      /// </summary>
      public static IReadOnlyList<AuthoredConsequence> OnEnter(AuthoredQuestRun run, AuthoredQuest q)
      {
         AuthoredStage s = Current(run, q);

         return s == null || s == q.Stages[0] ? new List<AuthoredConsequence>() : Consequences(run, s.OnEnter);
      }

      /// <summary>The bound StringId for a role written "{role}"; the text itself when it is no placeholder.</summary>
      public static string Bound(AuthoredQuestRun run, string role)
      {
         if (string.IsNullOrEmpty(role)) return null;
         string key = role.Trim().TrimStart('{').TrimEnd('}');

         return run?.BoundIds != null && run.BoundIds.TryGetValue(key, out string id) ? id : null;
      }

      /// <summary>TUNE: a tale's grudge whose file gives no magnitude: a real quarrel, well short of a vendetta.</summary>
      public const int DefaultTaleGrudgeMagnitude = 30;

      /// <summary>
      ///   The grudge a "grudge" consequence plants: who holds it against whom, the cause in their names, and how deep,
      ///   kept within 1..100. Null when either role is not a bound hero of the tale, or both are the same.
      /// </summary>
      public static TaleGrudge GrudgeOf(AuthoredQuestRun run, AuthoredConsequence c)
      {
         if (c == null || run == null) return null;
         string holder = Bound(run, c.Who), target = Bound(run, c.Against);
         if (string.IsNullOrEmpty(holder) || string.IsNullOrEmpty(target) || holder == target) return null;

         int magnitude = c.Magnitude <= 0 ? DefaultTaleGrudgeMagnitude : c.Magnitude > 100 ? 100 : c.Magnitude;

         return new TaleGrudge {HolderId = holder, TargetId = target, Cause = Fill(run, c.Text), Magnitude = magnitude};
      }

      /// <summary>Fills "{role}" placeholders with the names said aloud.</summary>
      public static string Fill(AuthoredQuestRun run, string text)
      {
         if (string.IsNullOrEmpty(text) || run?.BoundNames == null) return text;
         foreach (KeyValuePair<string, string> kv in run.BoundNames) text = text.Replace("{" + kv.Key + "}", kv.Value);

         return text;
      }

      private static AuthoredStage Active(AuthoredQuestRun run, AuthoredQuest q)
         => run?.Status == AuthoredQuestStatus.Active ? Current(run, q) : null;

      private static bool IsSceneChoice(AuthoredChoice c) => c.Objective == null && c.Said == null;

      /// <summary>A local of this place spoke with the player: counted for every way out that questions this place.</summary>
      private static void Heard(AuthoredQuestRun run, IReadOnlyList<AuthoredWay> ways, string heroId, string placeId)
      {
         foreach (AuthoredWay w in ways.Where(w => w.Objective.Type == "talk_to_locals" && Bound(run, w.Objective.Place) == placeId))
         {
            if (w.Choice == null && !run.TalkedThisStage.Contains(heroId)) run.TalkedThisStage.Add(heroId);
            string key = placeId + "|" + heroId;
            if (!run.LocalsHeard.Contains(key)) run.LocalsHeard.Add(key);
         }
      }

      private static int LocalsCounted(AuthoredQuestRun run, AuthoredWay way)
      {
         string prefix = Bound(run, way.Objective.Place) + "|";
         IEnumerable<string> heard = run.LocalsHeard.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k.Substring(prefix.Length));
         if (way.Choice == null) heard = heard.Union(run.TalkedThisStage);

         return heard.Distinct().Count();
      }

      private static void SetFlags(AuthoredQuestRun run, IEnumerable<string> flags)
      {
         foreach (string f in (flags ?? Enumerable.Empty<string>()).Select(f => (f ?? "").Trim()).Where(f => f.Length > 0))
            if (!run.Flags.Contains(f)) run.Flags.Add(f);
      }

      /// <summary>
      ///   Leaves <paramref name="from" /> by a way out: the choice's flags are set and its taking recorded, the stage's
      ///   progress is reset, and the story moves to <paramref name="next" />, a stage or an ending.
      /// </summary>
      private static AuthoredStep GoTo(AuthoredQuestRun run, AuthoredQuest q, AuthoredStage from, AuthoredChoice choice, string next, int today)
      {
         if (choice != null)
         {
            SetFlags(run, choice.Sets);
            run.ChoicesTaken.Add(from?.Id + "/" + choice.Id);
         }

         run.TalkedThisStage.Clear();
         run.LocalsHeard.Clear();
         run.ChoiceBands.Clear();
         run.WarsSeen.Clear();
         run.SceneChoiceId = null;
         run.NightsWatched = 0;
         run.LastNightDay = -1;
         run.StageStartDay = today;

         if (next == null || AuthoredQuestLoader.IsEnding(next))
         {
            run.Status = AuthoredQuestStatus.Completed;
            run.EndingId = AuthoredQuestLoader.EndingId(next);

            return new AuthoredStep {Kind = AuthoredStepKind.Completed, FromStage = from?.Id, ChoiceId = choice?.Id, EndingId = run.EndingId};
         }

         run.StageId = next;

         return new AuthoredStep {Kind = AuthoredStepKind.Advanced, FromStage = from?.Id, ToStage = next, ChoiceId = choice?.Id};
      }

      /// <summary>A stable roll in [0,1) from the quest and the day (FNV-1a: string.GetHashCode is randomised per process).</summary>
      private static double Roll(string questId, int day)
      {
         unchecked
         {
            uint h = 2166136261;
            foreach (char c in (questId ?? "") + ":" + day) h = (h ^ c) * 16777619;

            return (h % 10000) / 10000.0;
         }
      }
   }

   /// <summary>A grudge a tale's ending plants between two of its heroes.</summary>
   public sealed class TaleGrudge
   {
      public string HolderId { get; init; }
      public string TargetId { get; init; }
      public string Cause { get; init; }
      public int Magnitude { get; init; }
   }
}
