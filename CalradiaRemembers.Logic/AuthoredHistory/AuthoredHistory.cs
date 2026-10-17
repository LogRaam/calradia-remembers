// Code written by Gabriel Mailhot, 17/10/2026.
// Authored history (docs/design/AUTHORED_HISTORY.md). Gabriel, 17/10/2026: a file of the world's past is interesting
// because "je pourrais mettre en scene des evenements du passe qui ne se sont pas produits dans le temps de jeu", then
// "it can also be able to set a starting vanilla. A file per save ID. File editable with an external tool.. something
// like a web page with a timeline." This is the file's shape, as the editor page writes it and the mod reads it.

#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.AuthoredHistory
{
   /// <summary>One campaign's authored history file (format 1).</summary>
   public sealed class AuthoredHistoryDocument
   {
      /// <summary>The format version; 1.</summary>
      [JsonProperty("format")]
      public int Format { get; set; } = 1;

      /// <summary>The campaign it was written for (Campaign.UniqueGameId).</summary>
      [JsonProperty("campaign")]
      public string Campaign { get; set; }

      /// <summary>The author's name for this history.</summary>
      [JsonProperty("title")]
      public string Title { get; set; }

      [JsonProperty("events")]
      public List<AuthoredHistoryEvent> Events { get; set; } = new();
   }

   /// <summary>One event of the past, before the campaign began.</summary>
   public sealed class AuthoredHistoryEvent
   {
      /// <summary>Stable id: an edit keeps it, so the event is updated rather than duplicated.</summary>
      [JsonProperty("id")]
      public string Id { get; set; }

      /// <summary>In-game calendar year.</summary>
      [JsonProperty("year")]
      public int Year { get; set; }

      /// <summary>spring, summer, autumn or winter; spring when absent.</summary>
      [JsonProperty("season")]
      public string Season { get; set; }

      /// <summary>One of <see cref="AuthoredHistoryLoader.Kinds" />.</summary>
      [JsonProperty("kind")]
      public string Kind { get; set; }

      /// <summary>The line NPCs remember.</summary>
      [JsonProperty("summary")]
      public string Summary { get; set; }

      /// <summary>Hero ids (or a name only one hero bears) of those who did it.</summary>
      [JsonProperty("actors")]
      public List<string> Actors { get; set; } = new();

      /// <summary>Hero ids of those it harmed; for an execution or a fall in battle, the dead.</summary>
      [JsonProperty("harmed")]
      public List<string> Harmed { get; set; } = new();

      /// <summary>Settlement id where it happened, or none.</summary>
      [JsonProperty("place")]
      public string Place { get; set; }

      /// <summary>Realm (kingdom or clan) ids it concerns.</summary>
      [JsonProperty("realms")]
      public List<string> Realms { get; set; } = new();

      /// <summary>How far word of it carried, 0..100; from 45 up the realms named know it.</summary>
      [JsonProperty("importance")]
      public int Importance { get; set; } = AuthoredHistoryLoader.DefaultImportance;

      /// <summary>Known only to its actors, the harmed and <see cref="KnownBy" />.</summary>
      [JsonProperty("secret")]
      public bool Secret { get; set; }

      /// <summary>Hero ids let in on a secret.</summary>
      [JsonProperty("knownBy")]
      public List<string> KnownBy { get; set; } = new();

      /// <summary>Starting vanilla relations this event leaves behind.</summary>
      [JsonProperty("relations")]
      public List<AuthoredRelation> Relations { get; set; } = new();
   }

   /// <summary>
   ///   A starting relation between two heroes. Either <see cref="Change" /> (added to whatever the game made) or
   ///   <see cref="Set" /> (the value the pair starts at); Gabriel left the choice open, so both are offered.
   /// </summary>
   public sealed class AuthoredRelation
   {
      [JsonProperty("a")]
      public string A { get; set; }

      [JsonProperty("b")]
      public string B { get; set; }

      /// <summary>Added to the pair's relation, -100..100.</summary>
      [JsonProperty("change")]
      public int? Change { get; set; }

      /// <summary>The value the pair's relation starts at, -100..100.</summary>
      [JsonProperty("set")]
      public int? Set { get; set; }
   }

   /// <summary>What the campaign knows of one hero, read by the validation.</summary>
   public sealed class HistoryHeroFacts
   {
      public string Id { get; init; }

      public string Name { get; init; }

      /// <summary>Absolute in-game day of birth; null when unknown.</summary>
      public int? BirthDay { get; init; }

      /// <summary>Dead today. Wrong, and an execution of a living lord is accepted, which the encyclopedia refutes.</summary>
      public bool IsDead { get; init; }

      /// <summary>Hero id of the spouse today, or null.</summary>
      public string SpouseId { get; init; }
   }

   /// <summary>The campaign as the validation sees it.</summary>
   public sealed class HistoryWorldFacts
   {
      /// <summary>CampaignTime.DaysInSeason (model-driven, 21 in vanilla).</summary>
      public int DaysInSeason { get; init; }

      /// <summary>CampaignTime.SeasonsInYear (model-driven, 4 in vanilla).</summary>
      public int SeasonsInYear { get; init; }

      /// <summary>Absolute in-game day the campaign began; an authored event must be dated before it.</summary>
      public int CampaignStartDay { get; init; }

      /// <summary>The player's hero id.</summary>
      public string PlayerId { get; init; }

      /// <summary>Every hero the campaign knows, dead ones included (an event about a lord dead since is still valid).</summary>
      public IReadOnlyList<HistoryHeroFacts> Heroes { get; init; } = new List<HistoryHeroFacts>();

      public ISet<string> PlaceIds { get; init; } = new HashSet<string>();

      public ISet<string> RealmIds { get; init; } = new HashSet<string>();
   }

   /// <summary>A relation entry the validation kept, resolved to hero ids and clamped.</summary>
   public sealed class AcceptedRelation
   {
      public string A { get; init; }

      public string B { get; init; }

      public int? Change { get; init; }

      public int? Set { get; init; }

      /// <summary>One side is the player's character: the mod writes its personal regard too, not only vanilla.</summary>
      public bool TouchesPlayer { get; init; }
   }

   /// <summary>An event the validation accepted, resolved and dated.</summary>
   public sealed class AcceptedHistoryEvent
   {
      public string Id { get; init; }

      /// <summary>Absolute in-game day.</summary>
      public int Day { get; init; }

      public string Kind { get; init; }

      public string Summary { get; init; }

      public List<string> ActorIds { get; init; } = new();

      public List<string> HarmedIds { get; init; } = new();

      public string PlaceId { get; init; }

      public List<string> RealmIds { get; init; } = new();

      public int Importance { get; init; }

      public bool Secret { get; init; }

      public List<string> KnownByIds { get; init; } = new();

      public List<AcceptedRelation> Relations { get; init; } = new();

      /// <summary>The player's character is an actor or harmed: part of the player's own past.</summary>
      public bool InvolvesPlayer { get; init; }
   }

   /// <summary>What a load kept, and every problem found, each naming its event.</summary>
   public sealed class AuthoredHistoryLoadResult
   {
      public List<AcceptedHistoryEvent> Events { get; } = new();

      /// <summary>Events set aside, parts dropped, and warnings, in plain words.</summary>
      public List<string> Problems { get; } = new();
   }
}
