// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests (docs/design/AUTHORED_QUESTS.md; Gabriel, 03/10/2026): multi-stage quests written in JSON, which
// players and modders extend. These types are the file's shape, read as written; the loader validates them and the
// stage machine plays them. Field names follow the file (snake_case).

#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>One authored quest, as its file describes it.</summary>
   public sealed class AuthoredQuest
   {
      [JsonProperty("id")] public string Id { get; set; }
      [JsonProperty("title")] public string Title { get; set; }
      [JsonProperty("author")] public string Author { get; set; }

      /// <summary>Played at most once per campaign (Gabriel: unique is better; a repeated quest bores).</summary>
      [JsonProperty("unique")] public bool Unique { get; set; } = true;

      [JsonProperty("repeat_cooldown_days")] public int RepeatCooldownDays { get; set; }
      [JsonProperty("weight")] public int Weight { get; set; } = 10;
      [JsonProperty("requires")] public AuthoredRequires Requires { get; set; } = new();

      /// <summary>The roles the story needs ({giver}, {village}...), with a word on each for the author.</summary>
      [JsonProperty("binds")] public Dictionary<string, string> Binds { get; set; } = new();

      [JsonProperty("stages")] public List<AuthoredStage> Stages { get; set; } = new();
      [JsonProperty("reward")] public AuthoredOutcome Reward { get; set; } = new();
      [JsonProperty("failure")] public AuthoredOutcome Failure { get; set; } = new();

      /// <summary>The file it came from, for every report about it.</summary>
      [JsonIgnore] public string Source { get; set; }
   }

   /// <summary>When the quest may be offered, and by whom (increment 1: the shape the werewolf needs).</summary>
   public sealed class AuthoredRequires
   {
      [JsonProperty("giver")] public GiverRequirement Giver { get; set; } = new();
      [JsonProperty("village")] public PlaceRequirement Village { get; set; }
      [JsonProperty("player")] public PlayerRequirement Player { get; set; } = new();
   }

   public sealed class GiverRequirement
   {
      /// <summary>"lord", "notable", "gang_leader", "any" (Gabriel: any character may offer a quest).</summary>
      [JsonProperty("role")] public string Role { get; set; } = "lord";

      /// <summary>A role the giver must hold (a village of theirs), e.g. "{village}".</summary>
      [JsonProperty("holds")] public string Holds { get; set; }

      [JsonProperty("min_regard")] public int MinRegard { get; set; }
      [JsonProperty("not_at_war_with_player")] public bool NotAtWarWithPlayer { get; set; } = true;
   }

   public sealed class PlaceRequirement
   {
      [JsonProperty("type")] public string Type { get; set; } = "village";

      /// <summary>"forest", or null for anywhere.</summary>
      [JsonProperty("near")] public string Near { get; set; }
   }

   public sealed class PlayerRequirement
   {
      [JsonProperty("min_clan_tier")] public int MinClanTier { get; set; }
   }

   /// <summary>One stage: a journal line, one verified objective, what the people involved know, and what follows.</summary>
   public sealed class AuthoredStage
   {
      [JsonProperty("id")] public string Id { get; set; }
      [JsonProperty("journal")] public string Journal { get; set; }
      [JsonProperty("objective")] public AuthoredObjective Objective { get; set; }

      /// <summary>The beat, for the model to say in its own words (Gabriel: a canvas, rephrased).</summary>
      [JsonProperty("canvas")] public string Canvas { get; set; }

      /// <summary>What must stay true in anything said: fixed in substance, free in wording.</summary>
      [JsonProperty("facts")] public List<string> Facts { get; set; } = new();

      /// <summary>Who knows what at this stage ("{giver}", "locals_of:{village}"): injected into their prompt only.</summary>
      [JsonProperty("knowers")] public Dictionary<string, List<string>> Knowers { get; set; } = new();

      [JsonProperty("on_complete")] public AuthoredOnComplete OnComplete { get; set; } = new();
   }

   public sealed class AuthoredOnComplete
   {
      /// <summary>The next stage id, or "reward".</summary>
      [JsonProperty("next")] public string Next { get; set; }
   }

   /// <summary>One condition the engine verifies; which fields matter depends on <see cref="Type" />.</summary>
   public sealed class AuthoredObjective
   {
      [JsonProperty("type")] public string Type { get; set; }
      [JsonProperty("hero")] public string Hero { get; set; }
      [JsonProperty("place")] public string Place { get; set; }
      [JsonProperty("count")] public int Count { get; set; } = 1;
      [JsonProperty("radius")] public float Radius { get; set; } = 5f;

      /// <summary>keep_watch: [first night the scene may open, night it opens for certain].</summary>
      [JsonProperty("nights")] public int[] Nights { get; set; }

      /// <summary>keep_watch: the chance each eligible night opens the scene.</summary>
      [JsonProperty("chance")] public double Chance { get; set; }

      [JsonProperty("scene")] public AuthoredScene Scene { get; set; }
      [JsonProperty("spawn")] public AuthoredSpawn Spawn { get; set; }
      [JsonProperty("item")] public string Item { get; set; }
      [JsonProperty("to")] public string To { get; set; }
   }

   /// <summary>An exploration scene: the chat with a narrator and no character, until someone is met.</summary>
   public sealed class AuthoredScene
   {
      [JsonProperty("opening")] public AuthoredBeat Opening { get; set; }
      [JsonProperty("findings")] public List<AuthoredFinding> Findings { get; set; } = new();
      [JsonProperty("hazards")] public List<AuthoredHazard> Hazards { get; set; } = new();
      [JsonProperty("encounters")] public List<AuthoredEncounter> Encounters { get; set; } = new();

      /// <summary>What the player leaves knowing, whatever they did: stated at closing for any required finding missed.</summary>
      [JsonProperty("closing")] public string Closing { get; set; }
   }

   public sealed class AuthoredBeat
   {
      [JsonProperty("canvas")] public string Canvas { get; set; }
      [JsonProperty("facts")] public List<string> Facts { get; set; } = new();
   }

   public sealed class AuthoredFinding
   {
      [JsonProperty("id")] public string Id { get; set; }
      [JsonProperty("required")] public bool Required { get; set; }
      [JsonProperty("text")] public string Text { get; set; }
   }

   public sealed class AuthoredHazard
   {
      [JsonProperty("id")] public string Id { get; set; }

      /// <summary>What the player must do to spring it, written into the narrator's prompt.</summary>
      [JsonProperty("trigger")] public string Trigger { get; set; }

      [JsonProperty("effect")] public AuthoredHazardEffect Effect { get; set; }
   }

   public sealed class AuthoredHazardEffect
   {
      /// <summary>injure, alert, lose_time (increment 1); ambush and the rest later.</summary>
      [JsonProperty("type")] public string Type { get; set; }

      [JsonProperty("hit_points")] public int[] HitPoints { get; set; }
      [JsonProperty("party")] public string Party { get; set; }
      [JsonProperty("hours")] public int Hours { get; set; }
   }

   public sealed class AuthoredEncounter
   {
      [JsonProperty("id")] public string Id { get; set; }
      [JsonProperty("name")] public string Name { get; set; }
      [JsonProperty("persona")] public string Persona { get; set; }
      [JsonProperty("knows")] public List<string> Knows { get; set; } = new();
      [JsonProperty("may_materialize")] public bool MayMaterialize { get; set; }
      [JsonProperty("materialize_when")] public string MaterializeWhen { get; set; }
      [JsonProperty("escort_to")] public string EscortTo { get; set; }
   }

   /// <summary>A party spawned for the quest: real, on the map, beaten in a real battle.</summary>
   public sealed class AuthoredSpawn
   {
      [JsonProperty("id")] public string Id { get; set; }
      [JsonProperty("template")] public string Template { get; set; } = "looters";
      [JsonProperty("size")] public int[] Size { get; set; }
      [JsonProperty("name")] public string Name { get; set; }
      [JsonProperty("near")] public string Near { get; set; }
      [JsonProperty("behavior")] public string Behavior { get; set; } = "hide_near";
      [JsonProperty("flee_after_days")] public int FleeAfterDays { get; set; }
   }

   /// <summary>What success or failure leaves behind.</summary>
   public sealed class AuthoredOutcome
   {
      [JsonProperty("days")] public int Days { get; set; }
      [JsonProperty("regard")] public int Regard { get; set; }
      [JsonProperty("gold")] public int Gold { get; set; }
      [JsonProperty("memory")] public string Memory { get; set; }
      [JsonProperty("consequences")] public List<AuthoredConsequence> Consequences { get; set; } = new();
   }

   public sealed class AuthoredConsequence
   {
      /// <summary>memory, world_event (increment 1); the rest of the closed list later.</summary>
      [JsonProperty("type")] public string Type { get; set; }

      [JsonProperty("who")] public string Who { get; set; }
      [JsonProperty("text")] public string Text { get; set; }
      [JsonProperty("summary")] public string Summary { get; set; }
      [JsonProperty("magnitude")] public int Magnitude { get; set; }
      [JsonProperty("place")] public string Place { get; set; }
   }
}
