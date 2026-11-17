// Code written by Gabriel Mailhot, 17/11/2026.
// Gabriel (17/11/2026): "Nous pourrions remplacer le Story log par ce nouveau systeme de memoire a long terme. Chaque episode
// (session chat) aurait une entete qui decrit la scene et qui est present ... Ensuite on enregistre la conversation avec si
// necessaire des tags qui servent pour de la recherche ... Chaque action devrait etre recomposee en texte comme si le recit
// etait un roman." And: written "toujours, car le joueur peut activer, desactiver, reactiver"; the switch governs only what
// goes into a prompt. Goeth (Discord, 17/11/2026) asked for the long memory this archive will feed.
//
// One record is one thing that happened in the story: an episode opening, a line, a narration, a deed, a letter. Records are
// chained (each names the one before it) so a reloaded save reads only its own past: an abandoned branch stays in the file
// but cannot be reached from the head the save holds (StoryArchiveBranch). Who heard a line is written on the line itself,
// because presence changes inside a scene (a private audience clears the room, a companion steps out).
//
// If this is wrong: a character later remembers words said where they were not, a reloaded save hands back words from a
// future that never happened, or the archive cannot be read back and the player's story is lost.

#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#endregion

namespace CalradiaRemembers.Logic.StoryArchive
{
   /// <summary>What kind of thing one archive record holds.</summary>
   public enum StoryArchiveKind
   {
      /// <summary>An episode opens: the scene, the place, who is present.</summary>
      EpisodeStart = 0,

      /// <summary>A spoken line, word for word.</summary>
      Line,

      /// <summary>Scene narration.</summary>
      Narration,

      /// <summary>An out-of-character note the conversation carried (a refused deed, a declined confirmation).</summary>
      Note,

      /// <summary>A deed done in the scene, told as a sentence.</summary>
      Action,

      /// <summary>A letter sent or received.</summary>
      Letter,

      /// <summary>Something the mod itself told the player (a marriage settled, a house left).</summary>
      Notice
   }

   /// <summary>One person present when an episode opened.</summary>
   public sealed class StoryArchiveParticipant
   {
      /// <summary>The hero's StringId; null for someone without one (a bandit, a commoner).</summary>
      public string HeroId { get; init; }

      /// <summary>The name shown at the time.</summary>
      public string Name { get; init; }

      /// <summary>Why they are there: "host", "witness", "companion", "captive".</summary>
      public string Role { get; init; }
   }

   /// <summary>One thing that happened in the player's story, as the archive keeps it.</summary>
   public sealed class StoryArchiveRecord
   {
      /// <summary>This record's own id.</summary>
      public string Id { get; init; }

      /// <summary>The id of the record before it on the same branch; null for the first.</summary>
      public string ParentId { get; init; }

      /// <summary>What kind of record this is.</summary>
      [JsonConverter(typeof(StringEnumConverter))]
      public StoryArchiveKind Kind { get; init; }

      /// <summary>The episode it belongs to (the conversation's session id); null outside a conversation.</summary>
      public string EpisodeId { get; init; }

      /// <summary>The scene's kind: "conversation", "captive", "commoner", "council", "envoy", "exploration", "letter", "world".</summary>
      public string SceneKind { get; init; }

      /// <summary>Campaign time in days, fractional.</summary>
      public double Time { get; init; }

      /// <summary>The place's StringId, when there is one.</summary>
      public string PlaceId { get; init; }

      /// <summary>The place as told ("the castle of Poros").</summary>
      public string PlaceName { get; init; }

      /// <summary>Who spoke or acted: a hero StringId (the player's own for the player), null for narration.</summary>
      public string SpeakerId { get; init; }

      /// <summary>The name shown for the speaker at the time.</summary>
      public string SpeakerName { get; init; }

      /// <summary>True when the player spoke or acted.</summary>
      public bool IsPlayer { get; init; }

      /// <summary>The words, the narration, or the deed told as a sentence.</summary>
      public string Text { get; init; }

      /// <summary>The heroes (StringIds) who heard or saw it, the player excepted. Written per record: presence changes mid-scene.</summary>
      public List<string> Audience { get; init; }

      /// <summary>Who was present when the episode opened (EpisodeStart only).</summary>
      public List<StoryArchiveParticipant> Participants { get; init; }

      /// <summary>The verb of a deed (Action only), e.g. "give_gold".</summary>
      public string ActionType { get; init; }

      /// <summary>True when the game refused the deed: it did not happen.</summary>
      public bool Refused { get; init; }

      /// <summary>Search tags (hero ids, the place, the faction...).</summary>
      public List<string> Tags { get; init; }

      /// <summary>This record placed on a branch: the same content under <paramref name="id" />, after <paramref name="parentId" />.</summary>
      public StoryArchiveRecord Chained(string id, string parentId) => new StoryArchiveRecord {
         Id = id, ParentId = parentId, Kind = Kind, EpisodeId = EpisodeId, SceneKind = SceneKind, Time = Time, PlaceId = PlaceId,
         PlaceName = PlaceName, SpeakerId = SpeakerId, SpeakerName = SpeakerName, IsPlayer = IsPlayer, Text = Text, Audience = Audience,
         Participants = Participants, ActionType = ActionType, Refused = Refused, Tags = Tags
      };

      /// <summary>One line of the archive file.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Settings);

      /// <summary>Reads one line back; null for a blank, corrupt or id-less line (a torn last write after a crash).</summary>
      public static StoryArchiveRecord Deserialize(string line)
      {
         if (string.IsNullOrWhiteSpace(line)) return null;

         try
         {
            StoryArchiveRecord read = JsonConvert.DeserializeObject<StoryArchiveRecord>(line, Settings);

            return string.IsNullOrEmpty(read?.Id) ? null : read;
         }
         catch (Exception)
         {
            return null;
         }
      }

      private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
         NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore, Formatting = Formatting.None
      };
   }
}
