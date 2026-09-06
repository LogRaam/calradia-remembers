// Code written by Gabriel Mailhot, 06/09/2026.
// The pure core of the "tournament memory" pillar (player suggestion 2026-09-06): a tournament should not vanish the
// moment the prize is handed out. Who fought whom, who beat the player and who the player beat, and who took the
// crown, become first-hand memories the lords carry, so one may seek the player out afterward to brag, to
// congratulate, or (in time) as a sporting rival. This file is engine-agnostic: the mod reads the real arena bracket
// (SandBox TournamentBehavior: Rounds -> matches -> participants/winners) into these plain structures, and the
// policy beside it decides, per hero, how they would carry the result. Nothing here touches the game.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Tournaments
{
   /// <summary>One match the PLAYER fought in a tournament: which round, the heroes they faced, and whether they advanced.</summary>
   public sealed class TournamentBout
   {
      public TournamentBout(int round, IReadOnlyList<string> opponentHeroIds, bool playerAdvanced)
      {
         Round = round;
         OpponentHeroIds = opponentHeroIds ?? new List<string>();
         PlayerAdvanced = playerAdvanced;
      }

      /// <summary>The round this match belonged to (1-based; a 4-round bracket ends in round 4, the final).</summary>
      public int Round { get; }

      /// <summary>The HERO participants the player shared this match with (commoner arena fighters are excluded upstream).</summary>
      public IReadOnlyList<string> OpponentHeroIds { get; }

      /// <summary>True when the player won this match and moved on; false when this is the match that put the player out.</summary>
      public bool PlayerAdvanced { get; }
   }

   /// <summary>A whole tournament read from the player's point of view: their bouts, who took the crown, and where they fell.</summary>
   public sealed class TournamentResult
   {
      public TournamentResult(string townName, int day, bool playerWonTournament, int playerEliminatedRound, string championHeroId, IReadOnlyList<TournamentBout> bouts)
      {
         TownName = townName ?? "";
         Day = day;
         PlayerWonTournament = playerWonTournament;
         PlayerEliminatedRound = playerEliminatedRound;
         ChampionHeroId = championHeroId;
         Bouts = bouts ?? new List<TournamentBout>();
      }

      /// <summary>The town whose tournament this was, for naming the matter ("at Poros").</summary>
      public string TownName { get; }

      /// <summary>The in-game day the tournament finished (freshest wins in the recent-matter selector).</summary>
      public int Day { get; }

      /// <summary>True when the PLAYER took the crown.</summary>
      public bool PlayerWonTournament { get; }

      /// <summary>The round the player was eliminated in, or 0 when they won it (or did not compete).</summary>
      public int PlayerEliminatedRound { get; }

      /// <summary>The champion's hero StringId, or null when the player won it or the champion is not a hero.</summary>
      public string ChampionHeroId { get; }

      /// <summary>The player's own matches, in order.</summary>
      public IReadOnlyList<TournamentBout> Bouts { get; }
   }

   /// <summary>How a given hero stands relative to the PLAYER in a tournament, which decides what they would carry away.</summary>
   public enum TournamentRelation
   {
      /// <summary>No meaningful shared result with the player.</summary>
      None,

      /// <summary>They competed alongside the player but never faced them decisively (the champion nuance is carried separately).</summary>
      CoCompetitor,

      /// <summary>The PLAYER beat them: they were in a match the player won and did not advance from it.</summary>
      LostToThePlayer,

      /// <summary>They beat the PLAYER: they were in the match that eliminated the player and advanced from it.</summary>
      BeatThePlayer
   }

   /// <summary>A hero's standing in one tournament relative to the player: the decisive relation, whether they took the crown, and the round it turned on.</summary>
   public sealed class TournamentStanding
   {
      public TournamentStanding(TournamentRelation relation, bool isChampion, int round)
      {
         Relation = relation;
         IsChampion = isChampion;
         Round = round;
      }

      public TournamentRelation Relation { get; }

      /// <summary>True when this hero won the whole tournament (a crown they may crow about, especially over a player who also competed).</summary>
      public bool IsChampion { get; }

      /// <summary>The round the decisive bout against the player turned on (0 when there was none, e.g. a champion who never faced the player).</summary>
      public int Round { get; }
   }
}
