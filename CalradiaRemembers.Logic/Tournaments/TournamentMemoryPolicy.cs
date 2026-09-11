// Code written by Gabriel Mailhot, 06/09/2026.
// Tournament memory pillar (player suggestion 2026-09-06): once the arena bracket is read into a TournamentResult,
// this pure policy decides, for one hero, HOW they would carry their result against the player. Two rulings, both
// engine-agnostic and testable. Classify: from the bracket, what is this hero's decisive relation to the player
// (they beat the player, the player beat them, they merely co-competed) and did they take the crown. Tone: from that
// standing and the hero's own character (Valor/Honor/Mercy), do they brag, congratulate graciously, respect a hard
// fight, or sulk. And Aftermath: whether the encounter warms into an appreciation or curdles into a light grudge,
// which is what makes a sore loser and a worthy rival both possible from the same match (Gabriel: both, by traits).

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Tournaments
{
   /// <summary>How a hero would speak of their tournament result against the player.</summary>
   public enum TournamentMemoryTone
   {
      /// <summary>They crow about besting the player (a proud, low-honour victor).</summary>
      Brag,

      /// <summary>They are magnanimous: they praise the player's showing, whether they won or lost to them.</summary>
      Gracious,

      /// <summary>A measured acknowledgement of a hard-fought bout, without crowing or sulking.</summary>
      Respect,

      /// <summary>They nurse the humiliation of losing to the player (a proud, low-honour loser).</summary>
      Sore
   }

   /// <summary>What the encounter leaves behind between the hero and the player.</summary>
   public enum TournamentAftermath
   {
      /// <summary>Nothing lasting (a brag is competitive banter; a rivalry may still build over repeated meetings).</summary>
      None,

      /// <summary>A warm regard: a worthy opponent, respected or graciously met.</summary>
      Appreciation,

      /// <summary>A wound: a sore loser resents the player for the defeat.</summary>
      Grudge
   }

   /// <summary>The hero's own character traits that colour how they carry a tournament result (Bannerlord trait levels, roughly -2..2).</summary>
   public sealed class TournamentTraitFacts
   {
      /// <summary>Pride in martial prowess: high Valor leans toward crowing over a win and stewing over a loss.</summary>
      public int Valor { get; init; }

      /// <summary>Fair-mindedness: high Honor leans toward grace in victory and defeat alike; low Honor toward gloating and sulking.</summary>
      public int Honor { get; init; }

      /// <summary>Warmth: high Mercy softens the register toward grace.</summary>
      public int Mercy { get; init; }
   }

   /// <summary>Decides a hero's standing in a tournament relative to the player, and how they would carry it.</summary>
   public static class TournamentMemoryPolicy
   {
      /// <summary>
      ///   The hero's decisive standing against the player, read from the bracket: the match they shared with the
      ///   player (if any) decides whether they beat the player or the player beat them; a hero who never faced the
      ///   player but took the crown is a champion co-competitor. When a hero both faced the player and won the
      ///   whole thing, the face-to-face relation is kept AND the crown is flagged, so a victor who put the player
      ///   out can also crow about the title.
      /// </summary>
      public static TournamentStanding Classify(TournamentResult result, string heroId)
      {
         if (result == null || string.IsNullOrEmpty(heroId))
            return new TournamentStanding(TournamentRelation.None, false, 0);

         bool isChampion = heroId == result.ChampionHeroId;

         foreach (TournamentBout bout in result.Bouts)
         {
            if (bout?.OpponentHeroIds == null || !Contains(bout.OpponentHeroIds, heroId)) continue;

            // The shared match is decisive: if the player advanced from it, the player beat this hero; if not, this
            // is the match that put the player out, so this hero beat the player.
            TournamentRelation relation = bout.PlayerAdvanced ? TournamentRelation.LostToThePlayer : TournamentRelation.BeatThePlayer;

            return new TournamentStanding(relation, isChampion, bout.Round);
         }

         // Never faced the player directly: only a crown makes it worth carrying (a champion over a player who also
         // competed); otherwise a mere co-competitor with nothing decisive between them.
         return new TournamentStanding(isChampion ? TournamentRelation.CoCompetitor : TournamentRelation.None, isChampion, 0);
      }

      /// <summary>
      ///   How the hero would speak of it. A victor over the player (beat them, or took the crown over them) either
      ///   crows (proud, low honour), is gracious (honourable or merciful), or measured. A hero the PLAYER beat is
      ///   sore (proud, low honour), gracious, or measured. Standing with no result yields a plain measured note.
      /// </summary>
      public static TournamentMemoryTone Tone(TournamentStanding standing, TournamentTraitFacts traits)
      {
         if (traits == null) traits = new TournamentTraitFacts {Valor = 0, Honor = 0, Mercy = 0};

         bool wonOverPlayer = standing != null
            && (standing.Relation == TournamentRelation.BeatThePlayer
                || (standing.Relation == TournamentRelation.CoCompetitor && standing.IsChampion));
         bool lostToPlayer = standing != null && standing.Relation == TournamentRelation.LostToThePlayer;

         if (wonOverPlayer)
         {
            if (traits.Honor >= 1 || traits.Mercy >= 1) return TournamentMemoryTone.Gracious;
            if (traits.Valor >= 1 && traits.Honor <= 0) return TournamentMemoryTone.Brag;

            return TournamentMemoryTone.Respect;
         }

         if (lostToPlayer)
         {
            if (traits.Honor <= -1) return TournamentMemoryTone.Sore;
            if (traits.Honor >= 1 || traits.Mercy >= 1) return TournamentMemoryTone.Gracious;

            return TournamentMemoryTone.Respect;
         }

         return TournamentMemoryTone.Respect;
      }

      /// <summary>
      ///   What the result leaves between them. Grace or respect warms into an appreciation (a worthy opponent);
      ///   sulking curdles into a light grudge; a brag leaves nothing lasting on its own (a rivalry may still build
      ///   over repeated meetings, handled elsewhere).
      /// </summary>
      public static TournamentAftermath Aftermath(TournamentMemoryTone tone)
      {
         switch (tone)
         {
            case TournamentMemoryTone.Sore: return TournamentAftermath.Grudge;
            case TournamentMemoryTone.Gracious:
            case TournamentMemoryTone.Respect: return TournamentAftermath.Appreciation;
            default: return TournamentAftermath.None;
         }
      }

      private static bool Contains(IReadOnlyList<string> ids, string id)
      {
         for (var i = 0; i < ids.Count; i++)
            if (ids[i] == id) return true;

         return false;
      }
   }
}
