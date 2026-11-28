// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 7 of the group-scene plan, the dramaturge (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel, 28/11/2026:
// "on va aussi ajouter un modele dramaturge j'imagine?", and the mode "DRAMA+PROSE+INTERPRET+NSFW". Ported from the simulator's
// Dramaturge (GroupConversationSimulation), the prose-model version: a cheaper one was graded 5.40 against 6.61 for this one
// (27/11/2026: no event ever came, the scenes went flat). His rule for every event: it "chooses among true facts", it invents
// nothing the game tracks; so the events come only from the stage 3 composer, checked against the facts, and with the composed
// opening off the dramaturge brings none and only says when the scene has said what it had to say.
//
// NOT A/B-validated: it runs only in the Dramaturge mode. The gates are the simulator's: an event never before two beats are
// played (a scene needs room to set its question), at most one a beat, and "end" not before three ("EndFrom"). An event the
// player cannot see brings nothing: with Scene Narration off (1Enfant, Nexus, 16/11/2026) none is brought.
//
// This file holds the pure half: when the dramaturge reads, its prompt, the reading of its reply, the decision, the events
// waiting in a chat, and the notes the next reply is given. The engine glue is ChatViewModel.DramaturgeScene.cs.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>The facts the dramaturge's reading is decided on, after a beat.</summary>
   public sealed class DramaturgeReadFacts
   {
      /// <summary>The beat just played ran the Dramaturge machinery (a real player line, the main character, the mode on).</summary>
      public bool DramaturgeTurn { get; init; }

      /// <summary>Beats played so far in this chat (one player line and the reply it drew), the one just ended included.</summary>
      public int BeatsPlayed { get; init; }

      /// <summary>The dramaturge already said the scene has run its course.</summary>
      public bool SceneEnded { get; init; }

      /// <summary>A reading is still out.</summary>
      public bool ReadInFlight { get; init; }
   }

   /// <summary>What the dramaturge answered, read.</summary>
   public sealed class DramaturgeReading
   {
      /// <summary>What changed in the situation since the beat before, or "nothing".</summary>
      public string Progress { get; init; } = "";

      /// <summary>Who repeats what no longer works, or "none".</summary>
      public string Repeating { get; init; } = "";

      /// <summary>The strongest conflict or secret not yet in play.</summary>
      public string Untapped { get; init; } = "";

      /// <summary>The move asked for.</summary>
      public DramaturgeMove Move { get; init; }

      /// <summary>For <see cref="DramaturgeMove.Event" />: the number of the event asked for, as listed (1-based).</summary>
      public int EventNumber { get; init; }

      /// <summary>True when a move line could be read at all.</summary>
      public bool HadMove { get; init; }
   }

   /// <summary>The dramaturge's move.</summary>
   public enum DramaturgeMove
   {
      /// <summary>Let the characters turn the scene.</summary>
      None,

      /// <summary>The world brings one of the events waiting.</summary>
      Event,

      /// <summary>The scene has said what it had to say.</summary>
      End
   }

   /// <summary>The facts a reading is acted on.</summary>
   public sealed class DramaturgeDecisionFacts
   {
      /// <summary>Beats played so far, the one the reading followed included.</summary>
      public int BeatsPlayed { get; init; }

      /// <summary>The beat on which the last event was brought, or -1.</summary>
      public int LastEventBeat { get; init; } = -1;

      /// <summary>How many events the composer left in this chat, used or not (the listed numbers run 1 to this).</summary>
      public int EventCount { get; init; }

      /// <summary>The numbers (1-based) of the events already brought.</summary>
      public IReadOnlyCollection<int> UsedEvents { get; init; } = new int[0];

      /// <summary>The MCM switch "Scene Narration" is on: an event can be shown.</summary>
      public bool SceneNarrationOn { get; init; }
   }

   /// <summary>What the chat does with a reading.</summary>
   public sealed class DramaturgeDecision
   {
      /// <summary>The number (1-based) of the event to bring now, or 0 for none.</summary>
      public int EventNumber { get; init; }

      /// <summary>The scene has run its course: the next reply is told it may close it.</summary>
      public bool End { get; init; }

      /// <summary>One line for the conversation log.</summary>
      public string Why { get; init; } = "";
   }

   /// <summary>When the dramaturge reads, and what its reading is allowed to do. Pure.</summary>
   public static class DramaturgeScenePolicy
   {
      /// <summary>
      ///   Beats played before the dramaturge first reads. The simulator read from the second beat on and could bring nothing
      ///   before the third: reading earlier would pay a prose call that can change nothing.
      /// </summary>
      public const int FirstReadAfterBeats = 2;

      /// <summary>Beats played before an event may come (the simulator's "never before the third beat starts").</summary>
      public const int EventsFromBeats = 2;

      /// <summary>Beats played before the scene may end (the simulator's EndFrom = 3).</summary>
      public const int EndFromBeats = 3;

      /// <summary>True when the dramaturge reads after the beat just played.</summary>
      public static bool ShouldRead(DramaturgeReadFacts facts)
         => facts != null && facts.DramaturgeTurn && !facts.SceneEnded && !facts.ReadInFlight && facts.BeatsPlayed >= FirstReadAfterBeats;

      /// <summary>
      ///   What a reading is allowed to do: "end" from <see cref="EndFromBeats" /> on (and it wins over an event, as in the
      ///   simulator); an event only when it is listed, not yet brought, from <see cref="EventsFromBeats" /> on, not twice in
      ///   one beat, and only when it can be shown.
      /// </summary>
      public static DramaturgeDecision Decide(DramaturgeReading reading, DramaturgeDecisionFacts facts)
      {
         if (reading == null || facts == null) return new DramaturgeDecision {Why = "no reading"};
         if (!reading.HadMove) return new DramaturgeDecision {Why = "no move could be read; nothing changes"};

         if (reading.Move == DramaturgeMove.End)
            return facts.BeatsPlayed >= EndFromBeats
               ? new DramaturgeDecision {End = true, Why = "the scene has said what it had to say; the next reply may close it"}
               : new DramaturgeDecision {Why = $"asked to end after {facts.BeatsPlayed} beats; too soon, the scene goes on"};

         if (reading.Move != DramaturgeMove.Event) return new DramaturgeDecision {Why = "no move: the characters turn the scene"};

         int n = reading.EventNumber;
         if (n < 1 || n > facts.EventCount) return new DramaturgeDecision {Why = $"asked for event {n}, which is not listed"};
         if (facts.UsedEvents != null && facts.UsedEvents.Contains(n)) return new DramaturgeDecision {Why = $"asked for event {n}, already brought"};
         if (facts.BeatsPlayed < EventsFromBeats) return new DramaturgeDecision {Why = $"asked for event {n} after {facts.BeatsPlayed} beats; too soon"};
         if (facts.LastEventBeat == facts.BeatsPlayed) return new DramaturgeDecision {Why = $"asked for event {n}; one was already brought this beat"};
         if (!facts.SceneNarrationOn) return new DramaturgeDecision {Why = $"asked for event {n}; Scene Narration is off, so it cannot be shown and is not brought"};

         return new DramaturgeDecision {EventNumber = n, Why = $"event {n} is brought"};
      }
   }

   /// <summary>The events the composer left in one chat, in their listed order, and which were brought. Thread-safe.</summary>
   public sealed class DramaturgeEvents
   {
      private readonly List<string> _all = new();
      private readonly HashSet<int> _used = new();

      /// <summary>Starts the chat's list from the checked events (replacing any). Blank ones are skipped.</summary>
      public void Reset(IEnumerable<string> events)
      {
         lock (_all)
         {
            _all.Clear();
            _used.Clear();
            if (events != null) _all.AddRange(events.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()));
         }
      }

      /// <summary>How many events the chat holds, used or not.</summary>
      public int Count
      {
         get
         {
            lock (_all) return _all.Count;
         }
      }

      /// <summary>The numbers (1-based) already brought.</summary>
      public IReadOnlyCollection<int> Used
      {
         get
         {
            lock (_all) return _used.ToList();
         }
      }

      /// <summary>The events still waiting, with their listed numbers (1-based).</summary>
      public IReadOnlyList<KeyValuePair<int, string>> Waiting()
      {
         lock (_all) return _all.Select((e, i) => new KeyValuePair<int, string>(i + 1, e)).Where(p => !_used.Contains(p.Key)).ToList();
      }

      /// <summary>Marks event <paramref name="number" /> as brought and returns its text; null when it is not listed or was brought.</summary>
      public string Take(int number)
      {
         lock (_all)
         {
            if (number < 1 || number > _all.Count || !_used.Add(number)) return null;

            return _all[number - 1];
         }
      }
   }

   /// <summary>The dramaturge's prompt, the reading of its reply, and the notes the next reply is given. Pure.</summary>
   public static class DramaturgeDirector
   {
      /// <summary>
      ///   The dramaturge's instructions: the simulator's, word for word in its rules, with the visitor's freedom added (the player
      ///   decides when to leave; the scene's end is a cue to the character, never a lock).
      /// </summary>
      public const string System =
         "You are the dramaturge of a scene in progress. You never write dialogue and never speak for anyone. You read the scene and the hidden inner life"
         + " of the character, and decide whether the circumstances must change. A scene is alive when what the characters know, want or risk is shifting."
         + " Emotion repeated is not tension; a calm line that changes who holds power is. Do nothing when the characters are already turning the scene."
         + "\nAnswer exactly these four lines:"
         + "\nprogress: <what changed in the situation (knowledge, power, aims) since the beat before, or nothing>"
         + "\nrepeating: <Name: what they keep doing that no longer works, for each one who repeats, separated by ' | '; or none>"
         + "\nuntapped: <the strongest conflict or secret not yet in play, one phrase>"
         + "\nmove: <none | event N | end>"
         + "\nChoose 'event N' only when progress is nothing or someone repeats, and pick the event that forces someone to DECIDE something."
         + " Prefer an event that opens more than one choice, that could help or hurt more than one person, over one that points to a single answer."
         + " Choose 'end' only when the scene's question has been answered and any further line would only echo it."
         + " The visitor is played by a person who leaves when they choose; 'end' only tells the character the scene has run its course.";

      /// <summary>The longest scene text the dramaturge is given, in characters (the latest lines are kept).</summary>
      public const int MaxSceneChars = 9000;

      /// <summary>The dramaturge's message: who the character is, their inner life, the scene, the record and the events waiting.</summary>
      public static string User(DramaturgeSheet sheet, DramaturgeInnerState state, string sceneText, string recordText,
                                IReadOnlyList<KeyValuePair<int, string>> waiting, int beatsPlayed)
      {
         string npc = string.IsNullOrWhiteSpace(sheet?.NpcName) ? "the character" : sheet.NpcName.Trim();
         string player = string.IsNullOrWhiteSpace(sheet?.PlayerName) ? "the visitor" : sheet.PlayerName.Trim();
         DramaturgeInnerState s = state ?? new DramaturgeInnerState();
         string scene = (sceneText ?? "").Trim();
         if (scene.Length > MaxSceneChars) scene = "..." + scene.Substring(scene.Length - MaxSceneChars);

         var sb = new StringBuilder();
         sb.Append($"THE SCENE: {npc} and {player} (the protagonist, played by a person).");
         if (!string.IsNullOrWhiteSpace(sheet?.Knows)) sb.Append($"\n\nWHO {npc.ToUpperInvariant()} IS AND WHAT THEY KNOW:\n{Cap(sheet.Knows.Trim(), 3500)}");
         sb.Append($"\n\n{(scene.Length == 0 ? "(nothing said yet)" : scene)}");
         sb.Append($"\n\nINNER LIFE (hidden from {player}):\n- {npc}: feels {Or(s.Feeling)}; wants {Or(s.Aim)}; shows {Or(s.Showing)}; hides {Or(s.Hiding)}");
         if (!string.IsNullOrWhiteSpace(s.Bond)) sb.Append($"; bond: {s.Bond.Trim()}");
         sb.Append($"\n\nTHE RECORD (established in this scene):\n{(string.IsNullOrWhiteSpace(recordText) ? "(nothing yet)" : recordText.Trim())}");
         sb.Append("\n\nEVENTS THE WORLD CAN BRING (already true, waiting to surface):\n");
         sb.Append(waiting == null || waiting.Count == 0 ? "(none left)" : string.Join("\n", waiting.Select(w => $"{w.Key}. {w.Value}")));
         sb.Append($"\n\nThis is the end of beat {Math.Max(0, beatsPlayed)}.");

         return sb.ToString();
      }

      /// <summary>Reads the four lines; lenient on case and stray text. A reply with no move line reads as no move.</summary>
      public static DramaturgeReading Parse(string content)
      {
         string reply = content ?? "";
         string Line(string name) => DramaturgeInnerLife.Clean(Regex.Match(reply, $@"(?im)^\s*[-*]*\s*{name}\s*[*]*\s*:\s*(.+)$").Groups[1].Value);

         string move = Line("move").ToLowerInvariant().Trim('<', '>', ' ', '.');
         Match pick = Regex.Match(move, @"^event\s*(\d+)");
         DramaturgeMove kind = pick.Success ? DramaturgeMove.Event
            : move.StartsWith("end", StringComparison.Ordinal) ? DramaturgeMove.End
            : DramaturgeMove.None;

         return new DramaturgeReading {
            Progress = Line("progress"),
            Repeating = Line("repeating"),
            Untapped = Line("untapped"),
            Move = kind,
            EventNumber = pick.Success && int.TryParse(pick.Groups[1].Value, out int n) ? n : 0,
            HadMove = move.Length > 0
         };
      }

      /// <summary>The note the reply after an event is given: it happened, it is true, react as you would.</summary>
      public static string EventNote(string eventText)
         => $"This has just happened in the scene, and it is true: {(eventText ?? "").Trim()}"
            + "\nTake it in as you would: it may change what you say, what you want, or nothing at all.";

      /// <summary>
      ///   The note the reply after "end" is given. A cue, never an order: the character may close the scene in character (and
      ///   whether they may end the conversation stays the game's rule), and the player keeps the floor.
      /// </summary>
      public const string EndNote =
         "What this scene had to say has now been said. You may bring it to a close in character, as you would: a parting word, a decision, a"
         + " dismissal or a door left open. Do not stop short or repeat yourself; if the visitor goes on, answer them as you would.";

      /// <summary>One line for the conversation log: the reading in brief.</summary>
      public static string Describe(DramaturgeReading reading)
         => reading == null
            ? "no reading"
            : $"progress: {Or(reading.Progress)}; repeating: {Or(reading.Repeating)}; untapped: {Or(reading.Untapped)}; move: "
              + (reading.Move == DramaturgeMove.Event ? $"event {reading.EventNumber}" : reading.Move.ToString().ToLowerInvariant());

      private static string Or(string v) => string.IsNullOrWhiteSpace(v) ? "(not known)" : v.Trim();

      private static string Cap(string text, int max) => text.Length <= max ? text : text.Substring(0, max) + "...";
   }
}
