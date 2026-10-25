// Code written by Gabriel Mailhot, 25/10/2026.
// Gabriel (25/10/2026), on the night in Rhagaea's chamber: "comment le LLM dirige le beat pour amener le joueur vers le
// ending dawn? Car ca pourrait etre trop court ou trop long." Then: "On pourrait creer un orchestrateur configurable comme
// on a fait pour les captivity events? [...] les 3 premiers servent aux preliminaires, les 3 suivants le NPC prend
// l'initiative, les 3 suivants il laisse au joueur l'initiative [...] Ce plan ou cet arc devrait diriger l'experience que
// le LLM fait vivre au joueur." A talk_to that completes as the conversation closes may carry an arc: beats played in
// order, each for its number of exchanges, each with its direction, then the close. The game counts; the model is told
// which beat it plays and whether this reply is the last. The straight arc (ROADMAP, increment 1): signals and
// transitions come next.

#region

using System;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Where a tale's arc stands, what the character is told of it, and what one exchange does to it. Pure.</summary>
   public static class TaleArc
   {
      /// <summary>The character drives the beat: acts and proposes.</summary>
      public const string NpcLeads = "npc";

      /// <summary>The player drives the beat: the character follows and answers.</summary>
      public const string PlayerLeads = "player";

      /// <summary>
      ///   The arc of the conversation with this hero, when it is the current stage's own talk_to (one that completes as the
      ///   conversation closes) and the tale is under way; null otherwise.
      /// </summary>
      public static AuthoredArc Of(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         if (run is not {Status: AuthoredQuestStatus.Active} || string.IsNullOrEmpty(heroId)) return null;
         AuthoredObjective o = AuthoredQuestEngine.Current(run, q)?.Objective;

         return o?.Type == "talk_to" && AuthoredQuestEngine.EndsOnClose(o) && o.Arc?.Beats?.Count > 0
                && heroId == AuthoredQuestEngine.Bound(run, o.Hero)
            ? o.Arc
            : null;
      }

      /// <summary>The beat playing now: the one recorded, else the first.</summary>
      public static AuthoredArcBeat Beat(AuthoredQuestRun run, AuthoredArc arc)
         => arc?.Beats?.FirstOrDefault(b => b != null && b.Id == run?.ArcBeatId) ?? arc?.Beats?.FirstOrDefault();

      /// <summary>The beat the arc ends on: its "final", else the last.</summary>
      public static string FinalId(AuthoredArc arc)
         => !string.IsNullOrWhiteSpace(arc?.Final) ? arc.Final.Trim() : arc?.Beats?.LastOrDefault()?.Id;

      /// <summary>A new stage starts with no arc played.</summary>
      public static void Reset(AuthoredQuestRun run)
      {
         if (run == null) return;
         run.ArcBeatId = null;
         run.ArcBeatExchanges = 0;
         run.ArcExchanges = 0;
         run.ArcClosed = false;
      }

      /// <summary>
      ///   One exchange with this hero was played (their reply to the player's message is in). The beat counts it; a beat
      ///   played out gives way to the next; the final beat played out closes the arc; max_exchanges reached sends the
      ///   arc to its final beat. True when this exchange closed the arc: the conversation is to end now.
      /// </summary>
      public static bool Exchanged(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         AuthoredArc arc = Of(run, q, heroId);
         if (arc == null || run.ArcClosed) return false;

         AuthoredArcBeat beat = Beat(run, arc);
         run.ArcBeatId = beat.Id;
         run.ArcBeatExchanges++;
         run.ArcExchanges++;

         string final = FinalId(arc);
         bool beatDone = run.ArcBeatExchanges >= Math.Max(1, beat.Exchanges);
         if (beatDone && (beat.Id == final || Following(arc, beat) == null))
         {
            run.ArcClosed = true;

            return true;
         }

         if (arc.MaxExchanges > 0 && run.ArcExchanges >= arc.MaxExchanges && beat.Id != final)
         {
            MoveTo(run, final);

            return false;
         }

         if (beatDone) MoveTo(run, Following(arc, beat).Id);

         return false;
      }

      /// <summary>
      ///   What the character is told of the arc for the reply about to be written: the beat, who leads it, its direction,
      ///   and, on the beat's last reply before the close, that this reply ends the scene. Null when no arc plays.
      /// </summary>
      public static string Direction(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         AuthoredArc arc = Of(run, q, heroId);
         if (arc == null || run.ArcClosed) return null;

         AuthoredArcBeat beat = Beat(run, arc);
         int part = arc.Beats.IndexOf(beat) + 1;
         int reply = run.ArcBeatExchanges + 1;
         int of = Math.Max(1, beat.Exchanges);
         bool lastReply = reply >= of && (beat.Id == FinalId(arc) || Following(arc, beat) == null);

         var sb = new StringBuilder();
         sb.AppendLine($"HOW THIS CONVERSATION IS PACED (never mention it): part {part} of {arc.Beats.Count}, reply {reply} of {of} in this part.");
         sb.AppendLine(string.Equals(beat.Lead?.Trim(), PlayerLeads, StringComparison.OrdinalIgnoreCase)
            ? "The player leads this part: follow them and answer what they do and say; do not push it forward yourself."
            : "You lead this part: you act and propose. Answer what the player just said or did first, then carry the part on.");
         sb.AppendLine("This part: " + AuthoredQuestEngine.Fill(run, beat.Do));
         sb.Append(lastReply
            ? "This is your LAST reply of the scene: bring it to its close in this reply, as this part says; the conversation ends after it."
            : "Stay inside this part: do not hurry ahead to what comes after it, and do not end the scene.");

         return sb.ToString();
      }

      #region private

      private static AuthoredArcBeat Following(AuthoredArc arc, AuthoredArcBeat beat)
      {
         int i = arc.Beats.IndexOf(beat);

         return i >= 0 && i + 1 < arc.Beats.Count ? arc.Beats[i + 1] : null;
      }

      private static void MoveTo(AuthoredQuestRun run, string beatId)
      {
         run.ArcBeatId = beatId;
         run.ArcBeatExchanges = 0;
      }

      #endregion
   }
}
