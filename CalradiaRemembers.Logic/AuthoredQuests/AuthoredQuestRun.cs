// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: the stage machine (docs/design/AUTHORED_QUESTS.md). A run is one quest being played,
// saved as JSON. Every transition is a fact the engine reports (a conversation opened, a night spent near a place, a
// finding revealed, a party beaten, a day passed); the model never moves a stage (Gabriel's rule: the game advances
// the quest, never the character's word). The first stage is the request itself, made in the conversation where the
// quest is offered, so accepting it completes that stage.

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

      /// <summary>defeat_party: the StringId of the party spawned for this stage.</summary>
      public string SpawnedPartyId { get; set; }

      /// <summary>
      ///   The stage a conversation completed on opening, and with whom: while that conversation lasts, that person speaks
      ///   of THAT stage (the lord hearing the report), not of the next one. Cleared when the conversation closes.
      /// </summary>
      public string SpokenStageId { get; set; }
      public string SpokenHeroId { get; set; }

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

      public static readonly AuthoredStep Nothing = new() {Kind = AuthoredStepKind.None};
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
         => run?.StageId == q.Stages[0].Id ? Advance(run, q, today) : AuthoredStep.Nothing;

      public static AuthoredStage Current(AuthoredQuestRun run, AuthoredQuest q) => q?.Stages.FirstOrDefault(s => s.Id == run?.StageId);

      /// <summary>A CR conversation opened with this hero, who belongs to <paramref name="localOfPlaceId" /> (or null).</summary>
      public static AuthoredStep ConversationOpened(AuthoredQuestRun run, AuthoredQuest q, string heroId, string localOfPlaceId, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null || string.IsNullOrEmpty(heroId)) return AuthoredStep.Nothing;

         switch (s.Objective.Type)
         {
            case "talk_to":
               return heroId == Bound(run, s.Objective.Hero) ? Spoken(run, q, s, heroId, today) : AuthoredStep.Nothing;
            case "return":
               return heroId == Bound(run, "{giver}") ? Spoken(run, q, s, heroId, today) : AuthoredStep.Nothing;
            case "talk_to_locals":
               if (localOfPlaceId == null || localOfPlaceId != Bound(run, s.Objective.Place)) return AuthoredStep.Nothing;
               if (!run.TalkedThisStage.Contains(heroId)) run.TalkedThisStage.Add(heroId);

               return run.TalkedThisStage.Count >= Math.Max(1, s.Objective.Count) ? Advance(run, q, today) : AuthoredStep.Nothing;
            default:
               return AuthoredStep.Nothing;
         }
      }

      /// <summary>The conversation with this hero closed: they stop speaking of the stage it completed.</summary>
      public static void ConversationClosed(AuthoredQuestRun run, string heroId)
      {
         if (run == null || run.SpokenHeroId != heroId) return;
         run.SpokenHeroId = null;
         run.SpokenStageId = null;
      }

      private static AuthoredStep Spoken(AuthoredQuestRun run, AuthoredQuest q, AuthoredStage s, string heroId, int today)
      {
         run.SpokenStageId = s.Id;
         run.SpokenHeroId = heroId;

         return Advance(run, q, today);
      }

      /// <summary>be_at: the player's party is within the stage's radius of its place.</summary>
      public static AuthoredStep Arrived(AuthoredQuestRun run, AuthoredQuest q, string placeId, int today)
      {
         AuthoredStage s = Active(run, q);

         return s?.Objective.Type == "be_at" && placeId == Bound(run, s.Objective.Place) ? Advance(run, q, today) : AuthoredStep.Nothing;
      }

      /// <summary>
      ///   keep_watch: a night passed with the player near (or not near) the place. Counted once a day; from the first
      ///   eligible night a seeded roll may open the scene, and on the last it opens for certain (Gabriel, 03/10/2026:
      ///   nights spent waiting, no moon). Seeded by quest and day, so a reload does not reroll it.
      /// </summary>
      public static AuthoredStep NightPassed(AuthoredQuestRun run, AuthoredQuest q, bool nearPlace, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s?.Objective.Type != "keep_watch" || !nearPlace || run.LastNightDay == today) return AuthoredStep.Nothing;

         run.LastNightDay = today;
         run.NightsWatched++;

         int first = s.Objective.Nights[0], last = s.Objective.Nights[1];
         if (run.NightsWatched < first) return AuthoredStep.Nothing;
         bool opens = run.NightsWatched >= last || Roll(run.QuestId, today) < s.Objective.Chance;
         if (!opens) return AuthoredStep.Nothing;

         return Advance(run, q, today) is {Kind: AuthoredStepKind.Advanced} step
            ? new AuthoredStep {Kind = AuthoredStepKind.OpenScene, FromStage = step.FromStage, ToStage = step.ToStage}
            : AuthoredStep.Nothing;
      }

      /// <summary>explore: the narration revealed a finding. True only for a finding the scene declares, once.</summary>
      public static bool Found(AuthoredQuestRun run, AuthoredQuest q, string findingId)
      {
         AuthoredScene scene = Active(run, q)?.Objective.Scene;
         if (scene == null || run.Findings.Contains(findingId) || scene.Findings.All(f => f.Id != findingId)) return false;
         run.Findings.Add(findingId);

         return true;
      }

      /// <summary>explore: the narration sprang a hazard. Its effect for the game to carry out, once; null otherwise.</summary>
      public static AuthoredHazardEffect Sprung(AuthoredQuestRun run, AuthoredQuest q, string hazardId)
      {
         AuthoredHazard h = Active(run, q)?.Objective.Scene?.Hazards.FirstOrDefault(x => x.Id == hazardId);
         if (h == null || run.HazardsSprung.Contains(hazardId)) return null;
         run.HazardsSprung.Add(hazardId);

         return h.Effect;
      }

      /// <summary>
      ///   explore: the scene closed. The required findings the narration never reached, to be stated at closing so a
      ///   clue is missed only in its manner, never in substance; then the stage completes.
      /// </summary>
      public static IReadOnlyList<AuthoredFinding> SceneClosed(AuthoredQuestRun run, AuthoredQuest q, int today, out AuthoredStep step)
      {
         AuthoredScene scene = Active(run, q)?.Objective.Scene;
         if (scene == null || Current(run, q)?.Objective.Type != "explore")
         {
            step = AuthoredStep.Nothing;

            return Array.Empty<AuthoredFinding>();
         }

         List<AuthoredFinding> missed = scene.Findings.Where(f => f.Required && !run.Findings.Contains(f.Id)).ToList();
         run.Findings.AddRange(missed.Select(f => f.Id));
         step = Advance(run, q, today);

         return missed;
      }

      /// <summary>defeat_party: a party was beaten by the player; the stage completes if it was the one spawned for it.</summary>
      public static AuthoredStep PartyDefeated(AuthoredQuestRun run, AuthoredQuest q, string partyId, int today)
      {
         AuthoredStage s = Active(run, q);

         return s?.Objective.Type == "defeat_party" && partyId != null && partyId == run.SpawnedPartyId ? Advance(run, q, today) : AuthoredStep.Nothing;
      }

      /// <summary>A day passed: the quest fails past its time limit, or when a spawned band has had time to flee.</summary>
      public static AuthoredStep Day(AuthoredQuestRun run, AuthoredQuest q, int today)
      {
         AuthoredStage s = Active(run, q);
         if (s == null) return AuthoredStep.Nothing;

         bool timedOut = q.Failure?.Days > 0 && today - run.StartDay > q.Failure.Days;
         bool fled = s.Objective.Type == "defeat_party" && s.Objective.Spawn?.FleeAfterDays > 0 && today - run.StageStartDay > s.Objective.Spawn.FleeAfterDays;
         if (!timedOut && !fled) return AuthoredStep.Nothing;

         run.Status = AuthoredQuestStatus.Failed;

         return new AuthoredStep {Kind = AuthoredStepKind.Failed, FromStage = s.Id};
      }

      /// <summary>A test lever only (cr.story_step): completes the active stage as though its objective were met.</summary>
      public static AuthoredStep ForceAdvance(AuthoredQuestRun run, AuthoredQuest q, int today)
         => Active(run, q) != null ? Advance(run, q, today) : AuthoredStep.Nothing;

      /// <summary>The bound StringId for a role written "{role}"; the text itself when it is no placeholder.</summary>
      public static string Bound(AuthoredQuestRun run, string role)
      {
         if (string.IsNullOrEmpty(role)) return null;
         string key = role.Trim().TrimStart('{').TrimEnd('}');

         return run?.BoundIds != null && run.BoundIds.TryGetValue(key, out string id) ? id : null;
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

      private static AuthoredStep Advance(AuthoredQuestRun run, AuthoredQuest q, int today)
      {
         AuthoredStage s = Current(run, q);
         string next = s?.OnComplete?.Next;
         run.TalkedThisStage.Clear();
         run.NightsWatched = 0;
         run.LastNightDay = -1;
         run.StageStartDay = today;

         if (next == null || next == "reward")
         {
            run.Status = AuthoredQuestStatus.Completed;

            return new AuthoredStep {Kind = AuthoredStepKind.Completed, FromStage = s?.Id};
         }

         run.StageId = next;

         return new AuthoredStep {Kind = AuthoredStepKind.Advanced, FromStage = s?.Id, ToStage = next};
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
}
