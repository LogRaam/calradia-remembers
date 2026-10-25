// Code written by Gabriel Mailhot, 25/10/2026.
// Gabriel (25/10/2026), on the night in Rhagaea's chamber: "comment le LLM dirige le beat pour amener le joueur vers le
// ending dawn? Car ca pourrait etre trop court ou trop long." Then: "On pourrait creer un orchestrateur configurable comme
// on a fait pour les captivity events? [...] les 3 premiers servent aux preliminaires, les 3 suivants le NPC prend
// l'initiative, les 3 suivants il laisse au joueur l'initiative. A partir de la, une condition: si le NPC a eu un
// orgasme, le NPC a une conversation avec le joueur de 5 beats puis on ferme le stage vers dawn. Sinon, le NPC reprend
// le beat ou elle prend l'initiative. Ce plan ou cet arc devrait diriger l'experience que le LLM fait vivre au joueur."
// A talk_to that completes as the conversation closes may carry an arc: beats played in order, each for its number of
// exchanges, each with its direction, then the close. The game counts; the model is told which beat it plays and
// whether this reply is the last.
//
// His rulings on the conditions (25/10/2026): "melange des deux, signaux observes avec tirage en filet" (the reader of
// the turn's actions reports a signal the reply shows; one never seen may be decided by the game's roll, and the model is
// then told to narrate it); "je garderais une intervention forte qui retient.. c'est un ajustement naturel au roleplay du
// joueur" (a strong turn of the player's holds the beat once); and signals work in both compositions.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Where a tale's arc stands, what the character and the reader are told of it, and what one exchange does. Pure.</summary>
   public static class TaleArc
   {
      /// <summary>The character drives the beat: acts and proposes.</summary>
      public const string NpcLeads = "npc";

      /// <summary>The player drives the beat: the character follows and answers.</summary>
      public const string PlayerLeads = "player";

      /// <summary>A transition's target that ends the conversation.</summary>
      public const string Close = "close";

      /// <summary>The game's own signal of every arc: the player turned the scene with a strong act or word of their own.</summary>
      public const string TurnedSignal = "turned";

      /// <summary>The [ACTION] type the reader reports a signal with.</summary>
      public const string SignalVerb = "tale_signal";

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
         run.ArcSignalsNow?.Clear();
         run.ArcListened?.Clear();
         run.ArcHeld = false;
         run.ArcDeclared = null;
      }

      /// <summary>
      ///   The reader of the turn's actions reported a signal in the exchange with this hero. Taken only while the arc plays,
      ///   for the game's own "turned", or for a declared signal the current beat listens to or that turns the arc at once
      ///   (on_signal); a declared signal taken sets the tale flag of its name. True when taken.
      /// </summary>
      public static bool SignalSeen(AuthoredQuestRun run, AuthoredQuest q, string heroId, string signalId)
      {
         AuthoredArc arc = Of(run, q, heroId);
         string id = (signalId ?? "").Trim();
         if (arc == null || run.ArcClosed || id.Length == 0 || !Listened(run, arc).Contains(id)) return false;

         if (!run.ArcSignalsNow.Contains(id)) run.ArcSignalsNow.Add(id);
         if (id != TurnedSignal && !run.Flags.Contains(id)) run.Flags.Add(id);

         return true;
      }

      /// <summary>
      ///   One exchange with this hero was played (their reply is in, its signals reported). A signal that turns the arc at
      ///   once jumps; a first turn of the player's holds the beat (it still counts toward max_exchanges); otherwise the beat
      ///   counts it, and a beat played out rolls the net of the signals it listened to, then goes where its "next" leads
      ///   (the following beat by default); the final beat played out closes the arc; max_exchanges reached sends the arc to
      ///   its final beat. <paramref name="roll" /> gives each net roll (0 to 1). True when this exchange closed the arc.
      /// </summary>
      public static bool Exchanged(AuthoredQuestRun run, AuthoredQuest q, string heroId, Func<double> roll)
      {
         AuthoredArc arc = Of(run, q, heroId);
         if (arc == null || run.ArcClosed)
         {
            run?.ArcSignalsNow?.Clear();

            return false;
         }

         AuthoredArcBeat beat = Beat(run, arc);
         run.ArcBeatId = beat.Id;
         List<string> seen = run.ArcSignalsNow.ToList();
         run.ArcSignalsNow.Clear();
         run.ArcDeclared = null;
         run.ArcExchanges++;
         string final = FinalId(arc);

         // The arc's very last reply is the farewell: nothing turns it or holds it, so she can be told it is the last.
         bool lastOfArc = run.ArcBeatExchanges + 1 >= Math.Max(1, beat.Exchanges) && AllPathsClose(run, arc, beat);

         // A jump to the beat already playing changes nothing; the cap holds on a jump too, or a signal repeated every
         // exchange would keep the arc open for ever.
         string jump = lastOfArc
            ? null
            : seen.Where(s => s != TurnedSignal).Select(s => Target(arc.OnSignal, s)).FirstOrDefault(t => t != null && t != beat.Id);
         if (jump != null)
         {
            run.ArcHeld = false;
            if (Capped(run, arc) && jump != Close) jump = final;

            return Go(run, jump);
         }

         if (seen.Contains(TurnedSignal) && !run.ArcHeld && !lastOfArc)
         {
            run.ArcHeld = true;
            if (Capped(run, arc) && beat.Id != final) return Go(run, final);

            return false;
         }

         run.ArcHeld = false;
         run.ArcBeatExchanges++;
         foreach (string l in Listens(beat)) run.ArcListened[l] = (run.ArcListened.TryGetValue(l, out int n) ? n : 0) + 1;

         if (run.ArcBeatExchanges < Math.Max(1, beat.Exchanges))
         {
            if (Capped(run, arc) && beat.Id != final) return Go(run, final);

            return false;
         }

         foreach (string l in Listens(beat).Where(l => !run.Flags.Contains(l)))
         {
            AuthoredArcNet net = arc.Signals?.FirstOrDefault(s => s?.Id?.Trim() == l)?.Net;
            if (net == null || run.ArcListened[l] < net.After || roll() >= net.Chance) continue;
            run.Flags.Add(l);
            run.ArcDeclared = l;
         }

         if (beat.Id == final) return Go(run, Close);
         string to = NextOf(run, arc, beat);
         if (Capped(run, arc) && to != Close) to = final;

         return Go(run, to);
      }

      /// <summary>
      ///   What the character is told of the arc for the reply about to be written: the beat, who leads it, what the game
      ///   decided (a signal of the net, to narrate now), that the player just turned the scene, its direction, and, when every
      ///   way on from here closes, that this reply ends the scene. Null when no arc plays.
      /// </summary>
      public static string Direction(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         AuthoredArc arc = Of(run, q, heroId);
         if (arc == null || run.ArcClosed) return null;

         AuthoredArcBeat beat = Beat(run, arc);
         int part = arc.Beats.IndexOf(beat) + 1;
         int reply = run.ArcBeatExchanges + 1;
         int of = Math.Max(1, beat.Exchanges);
         bool lastReply = reply >= of && AllPathsClose(run, arc, beat);

         var sb = new StringBuilder();
         sb.AppendLine($"HOW THIS CONVERSATION IS PACED (never mention it): part {part} of {arc.Beats.Count}, reply {reply} of {of} in this part.");
         sb.AppendLine(string.Equals(beat.Lead?.Trim(), PlayerLeads, StringComparison.OrdinalIgnoreCase)
            ? "The player leads this part: follow them and answer what they do and say; do not push it forward yourself."
            : "You lead this part: you act and propose. Answer what the player just said or did first, then carry the part on.");
         string declared = arc.Signals?.FirstOrDefault(s => s?.Id?.Trim() == run.ArcDeclared)?.Means;
         if (!string.IsNullOrWhiteSpace(declared))
            sb.AppendLine("The game has decided that this happens in this reply, before anything else: " + AuthoredQuestEngine.Fill(run, declared) + ". Narrate it.");
         // The reply that answered the player's turn is already written; this one only learns the part runs a reply longer.
         if (run.ArcHeld) sb.AppendLine("This part runs one reply longer than planned.");
         sb.AppendLine("This part: " + AuthoredQuestEngine.Fill(run, beat.Do));
         sb.Append(lastReply
            ? "This is your LAST reply of the scene: answer the player, then bring it to its close in this reply, as this part says; the conversation ends after it."
            : "Stay inside this part: do not hurry ahead to what comes after it, and do not end the scene.");

         return sb.ToString();
      }

      /// <summary>
      ///   For whoever reads the turn's actions: the signals the game watches for in this exchange (the player turning the
      ///   scene, and the declared ones the current beat listens to or that turn the arc), and how to report one. Null when
      ///   no arc plays.
      /// </summary>
      public static string SignalsForReader(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         AuthoredArc arc = Of(run, q, heroId);
         if (arc == null || run.ArcClosed) return null;

         var sb = new StringBuilder();
         sb.AppendLine("SIGNALS THE GAME WATCHES FOR IN THIS EXCHANGE (a written tale; only these count):");
         sb.AppendLine($"- {TurnedSignal}: the player's own message turns the scene with a strong act or word the reply must answer (a refusal, a demand, a deed of their own), not a mere answer or a move within the scene.");
         foreach (string id in Listened(run, arc).Where(i => i != TurnedSignal))
         {
            string means = arc.Signals.First(s => s?.Id?.Trim() == id).Means;
            sb.AppendLine($"- {id}: {AuthoredQuestEngine.Fill(run, means)}");
         }

         sb.AppendLine("Report one ONLY when this very exchange plainly shows it; never on a hint, a wish or a promise; never narrate "
                       + "something in order to report it. One block per signal seen:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: " + SignalVerb);
         sb.AppendLine("signal: <the signal id>");
         sb.Append("[/ACTION]");

         return sb.ToString();
      }

      #region private

      private static IEnumerable<string> Listens(AuthoredArcBeat beat) => (beat?.Listens ?? new List<string>()).Select(l => (l ?? "").Trim()).Where(l => l.Length > 0).Distinct();

      // The signals taken in this exchange: the game's own, the current beat's, and those that turn the arc at once.
      private static List<string> Listened(AuthoredQuestRun run, AuthoredArc arc)
      {
         var ids = new List<string> {TurnedSignal};
         ids.AddRange(Listens(Beat(run, arc)));
         ids.AddRange((arc.OnSignal ?? new Dictionary<string, string>()).Keys.Select(k => (k ?? "").Trim()));

         return ids.Where(i => i == TurnedSignal || arc.Signals?.Any(s => s?.Id?.Trim() == i) == true).Distinct().ToList();
      }

      private static string Target(Dictionary<string, string> onSignal, string signal)
         => (onSignal ?? new Dictionary<string, string>()).FirstOrDefault(o => (o.Key ?? "").Trim() == signal).Value?.Trim();

      private static bool Capped(AuthoredQuestRun run, AuthoredArc arc) => arc.MaxExchanges > 0 && run.ArcExchanges >= arc.MaxExchanges;

      // Where a played-out beat goes: the first "next" whose flags hold, else the following beat, else the close.
      private static string NextOf(AuthoredQuestRun run, AuthoredArc arc, AuthoredArcBeat beat)
      {
         AuthoredArcNext n = (beat.Next ?? new List<AuthoredArcNext>()).FirstOrDefault(x => x != null && AuthoredFlags.Hold(x.If, run.Flags));
         if (n != null) return string.IsNullOrWhiteSpace(n.To) ? Close : n.To.Trim();

         return Following(arc, beat)?.Id ?? Close;
      }

      // Every way on from this beat, once played out, closes: it is the final, or each of its "next" and its fallthrough closes.
      private static bool AllPathsClose(AuthoredQuestRun run, AuthoredArc arc, AuthoredArcBeat beat)
      {
         if (beat.Id == FinalId(arc)) return true;
         List<AuthoredArcNext> next = (beat.Next ?? new List<AuthoredArcNext>()).Where(x => x != null).ToList();
         bool fallsThroughToClose = Following(arc, beat) == null;
         if (next.Count == 0) return fallsThroughToClose;

         return next.All(x => string.IsNullOrWhiteSpace(x.To) || x.To.Trim() == Close)
                && (fallsThroughToClose || next.Any(x => x.If == null || x.If.All(string.IsNullOrWhiteSpace)));
      }

      private static AuthoredArcBeat Following(AuthoredArc arc, AuthoredArcBeat beat)
      {
         int i = arc.Beats.IndexOf(beat);

         return i >= 0 && i + 1 < arc.Beats.Count ? arc.Beats[i + 1] : null;
      }

      private static bool Go(AuthoredQuestRun run, string beatId)
      {
         if (beatId == null || beatId == Close)
         {
            run.ArcClosed = true;

            return true;
         }

         run.ArcBeatId = beatId;
         run.ArcBeatExchanges = 0;

         return false;
      }

      #endregion
   }
}
