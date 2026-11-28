// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 3 of the group-scene plan, the composed intro (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "au début de chaque CHAT on ajoute l'intro ... ça met en scène", with his example: "The governor's hall in the
// keep of Poros, the morning after the battle, late spring. The hall smells of smoke and vinegar; ... when Arwa enters she
// rises at once, smoothing her gown with a hand that still trembles from yesterday." His rule: the composer "chooses among
// true facts and stages them; it invents nothing the game tracks".
//
// It IS the scene opener (Lordfadooboo, 21/10/2026; SceneOpenerPolicy), not a second one: with the MCM switch "Composed
// opening scene" on, the opener's call becomes the composer's (the simulator's ComposeScene, GroupConversationSimulation),
// followed by one cheap check (the interpreter model) that reads the paragraph and the concern against the facts given. A
// composition that invents a battle, a raid, a death, a capture, a marriage, a war, an alliance, a fief, a sum or a troop
// count is dropped, and the opening is today's. 1Enfant's "Scene Narration" switch (Nexus, 16/11/2026) keeps governing the
// purple line: with it off there is nothing to show, so nothing is composed.
//
// This file holds the pure half: whether to compose, the facts sheet the composer and the checker read, the two prompts,
// and the reading of both replies. The engine glue is ChatViewModel.ComposedOpening.cs.
//
// Stage 7 (28/11/2026), the Dramaturge mode only (ComposedOpeningSheet.ProposeEvents): the composer also proposes up to three
// events the world may bring later in the scene, as the simulator's ComposeScene did, and a separate events check passes each
// one or drops it. Off, every prompt here is the stage 3 one.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>What an opening knows when it decides whether the composer opens it. The scene opener has already said yes.</summary>
   public sealed class ComposedOpeningFacts
   {
      /// <summary>The MCM switch "Composed opening scene" is on.</summary>
      public bool SwitchOn { get; init; }

      /// <summary>The MCM switch "Scene Narration" is on: the composed paragraph has somewhere to be shown.</summary>
      public bool SceneNarrationOn { get; init; }

      /// <summary>The conversation is an envoy's audience (a companion speaking for the player to a lord).</summary>
      public bool EnvoyAudience { get; init; }

      /// <summary>The conversation is a captive scene, either side of the bars.</summary>
      public bool CaptiveScene { get; init; }

      /// <summary>The conversation is a private briefing of a companion.</summary>
      public bool CompanionBriefing { get; init; }

      /// <summary>The conversation is a council or an assembly.</summary>
      public bool Council { get; init; }

      /// <summary>The character already met the player in a talk today (a memory of today's talk is on their record).</summary>
      public bool SpokeToday { get; init; }
   }

   /// <summary>Decides whether the composer opens a conversation, and its limits.</summary>
   public static class ComposedOpeningPolicy
   {
      /// <summary>The longest the check may take before the opening falls back to today's, in seconds.</summary>
      public const int CheckTimeoutSeconds = 30;

      /// <summary>The longest setting paragraph kept, in characters.</summary>
      public const int MaxSettingChars = 1400;

      /// <summary>The longest concern or tension kept, in characters (it rides in every turn's prompt).</summary>
      public const int MaxHiddenChars = 400;

      /// <summary>
      ///   True when this opening is composed: the switch is on, the scene narration has somewhere to show it, and the
      ///   conversation is an ordinary one met for the first time today. Every other opening keeps today's behaviour.
      /// </summary>
      public static bool ShouldCompose(ComposedOpeningFacts facts)
         => facts != null
            && facts.SwitchOn
            && facts.SceneNarrationOn
            && !facts.EnvoyAudience
            && !facts.CaptiveScene
            && !facts.CompanionBriefing
            && !facts.Council
            && !facts.SpokeToday;

      /// <summary>The composer's time limit: the player's own reply timeout, never under 15 seconds.</summary>
      public static int ComposerTimeoutSeconds(int replyTimeoutSeconds) => Math.Max(15, replyTimeoutSeconds);

      /// <summary>The words of the hour the composer is given, from the engine's clock and its own night reading.</summary>
      public static string TimeOfDay(int hour, bool isNight)
      {
         if (isNight) return hour >= 4 && hour < 7 ? "before dawn" : hour >= 18 && hour < 22 ? "nightfall" : "the dead of night";
         if (hour < 8) return "early morning";
         if (hour < 11) return "morning";
         if (hour < 14) return "midday";
         if (hour < 17) return "afternoon";

         return "evening";
      }
   }

   /// <summary>What the game knows, as the composer and the checker read it. Every text may be empty; nothing here is invented.</summary>
   public sealed class ComposedOpeningSheet
   {
      /// <summary>The character's name, as the prompt names them.</summary>
      public string NpcName { get; init; }

      /// <summary>The player's name.</summary>
      public string PlayerName { get; init; }

      /// <summary>Who the character is: the identity, personality and authored lines of their own prompt.</summary>
      public string Character { get; init; }

      /// <summary>Their history with the player and their memories, as their own prompt holds them.</summary>
      public string History { get; init; }

      /// <summary>What they lived in the last days, each already dated in words ("today: ...", "yesterday: ..."); a battle fought beside the player is here.</summary>
      public IReadOnlyList<string> LivedLately { get; init; } = new string[0];

      /// <summary>The talk of the realm that reached them.</summary>
      public IReadOnlyList<string> RealmNews { get; init; } = new string[0];

      /// <summary>Where they are (the WHERE YOU ARE note, or the nearest place and whose lands these are).</summary>
      public string Place { get; init; }

      /// <summary>The season, as the calendar names it, or empty.</summary>
      public string Season { get; init; }

      /// <summary>The hour in words (<see cref="ComposedOpeningPolicy.TimeOfDay" />), or empty.</summary>
      public string TimeOfDay { get; init; }

      /// <summary>What the sky is doing, or empty.</summary>
      public string Weather { get; init; }

      /// <summary>What the game says the character is about, or empty.</summary>
      public string Activity { get; init; }

      /// <summary>How the character is at the player's side right now (riding in the party, sharing a town), or empty.</summary>
      public string Presence { get; init; }

      /// <summary>What the player wears and carries, as the game describes it, or empty.</summary>
      public string PlayerGear { get; init; }

      /// <summary>The player's companions seated in this conversation; empty means the player comes alone.</summary>
      public IReadOnlyList<string> Present { get; init; } = new string[0];

      /// <summary>The words the character will say first (the greeting that stands after the paragraph), or empty.</summary>
      public string Greeting { get; init; }

      /// <summary>The language the paragraph is written in; empty for English.</summary>
      public string Language { get; init; }

      /// <summary>
      ///   Stage 7 (28/11/2026), the Dramaturge mode only: the composer also proposes up to three events the world may bring
      ///   later in the scene, each checked against the facts. Off, the prompts are exactly the stage 3 ones.
      /// </summary>
      public bool ProposeEvents { get; init; }
   }

   /// <summary>The composer's reply, read.</summary>
   public sealed class ComposedOpeningReply
   {
      /// <summary>What the character is doing as the player arrives.</summary>
      public string Doing { get; init; } = "";

      /// <summary>The paragraph that opens the scene, shown in the narrator's purple.</summary>
      public string Setting { get; init; } = "";

      /// <summary>What weighs on the character today (hidden, in their prompt).</summary>
      public string Concern { get; init; } = "";

      /// <summary>What could rub with the player (hidden, in their prompt).</summary>
      public string Tension { get; init; } = "";

      /// <summary>
      ///   The events the world may bring later in the scene (Stage 7, Dramaturge mode), at most
      ///   <see cref="ComposedOpeningPrompt.MaxEvents" />, plain text. Once checked, only those the check passed remain.
      /// </summary>
      public IReadOnlyList<string> Events { get; init; } = new string[0];

      /// <summary>True when there is a paragraph to show.</summary>
      public bool IsUsable => !string.IsNullOrWhiteSpace(Setting);
   }

   /// <summary>The checker's verdict on a composition.</summary>
   public sealed class ComposedOpeningVerdict
   {
      /// <summary>True only when the checker said plainly that nothing was invented.</summary>
      public bool Passes { get; init; }

      /// <summary>Why it did not pass: what it invents, or that no verdict could be read. Empty when it passes.</summary>
      public string Reason { get; init; } = "";
   }

   /// <summary>The composer's and the checker's prompts, and the reading of their replies. Pure.</summary>
   public static class ComposedOpeningPrompt
   {
      /// <summary>The most characters of each long block (the character, the history) the composer is given.</summary>
      public const int MaxBlockChars = 3500;

      /// <summary>The most lines of the realm's talk and of the last days given.</summary>
      public const int MaxListLines = 8;

      private static readonly string[] Labels = {"doing", "setting", "concern", "tension"};

      /// <summary>The composer's instructions: the simulator's ComposeScene, with the opening paragraph and no events.</summary>
      public static string ComposerSystem(ComposedOpeningSheet sheet)
      {
         string npc = Name(sheet?.NpcName, "the character");
         string player = Name(sheet?.PlayerName, "the visitor");
         string language = (sheet?.Language ?? "").Trim();
         bool events = sheet?.ProposeEvents == true;

         return "You compose the opening of a scene in a medieval world, for a novelist, from what the game knows. You never write dialogue."
                + (events
                      ? " Answer these labelled lines, the labels in English: the four below, then up to three event lines:"
                      : " Answer exactly these four labelled lines, the labels in English:")
                + $"\ndoing: <what {npc} is doing as {player} arrives, one sentence>"
                + $"\nsetting: <one paragraph of three to five sentences, third person, present tense, that opens the scene: the place, the hour and the"
                + $" season, what the air holds, how {player} comes in (what they wear, who comes with them), and what {npc} is doing as they enter."
                + $" No dialogue and no quotation marks: {npc}'s first words follow it. Never say what {player} thinks, feels, says or does beyond arriving.>"
                + $"\nconcern: <addressed to {npc} as 'you': what weighs on you today and what you want from this meeting without saying it, drawn from the"
                + " facts and your memories; one or two sentences>"
                + $"\ntension: <addressed to {npc} as 'you': what in your true history with {player}, or in how {player} arrives, could rub; one sentence>"
                + (events
                      ? "\nevent: <something the world may bring later in the scene, as one sentence of narration, present tense, third person, no dialogue>"
                        + "\nevent: <another>\nevent: <another, optional>"
                      : "")
                + "\nRULES: choose among the facts given and stage them. Never invent a battle, raid, death, capture, marriage, war, alliance, fief, sum of money"
                + " or troop count that is not in the facts. Bring in no named person the facts do not place here. The weather, the light, smells, sounds,"
                + " servants, guards and the ordinary happenings of the place are yours to choose, as long as they fit the facts. When the facts hold"
                + $" something {npc} and {player} have just lived together, above all a battle, it colours everything: let the setting show it. The concern"
                + " may be private (a feeling, a fear) as long as it touches no game fact that is not given."
                + $" The doing and the setting are what {player} sees and hears: what {npc} keeps from {player} (a secret, a private memory)"
                + " never shows in them; it may go only in the concern."
                // The simulator's ComposeScene (27/11/2026): events drawn from the facts, and a dilemma rather than a solution.
                + (events
                      ? $" An event may only bring news of a fact given, a person or thing the facts place nearby, or an ordinary happening of"
                        + $" the place; it never reveals what {npc} keeps from {player}. Prefer an event that forces a choice on both of them over"
                        + " one that only informs."
                      : "")
                + (language.Length == 0 ? "" : events
                      ? $"\nWrite the setting, the concern, the tension and the events in {language}; keep the labels in English."
                      : $"\nWrite the setting, the concern and the tension in {language}; keep the four labels in English.");
      }

      /// <summary>The facts the composer and the checker both read.</summary>
      public static string FactsText(ComposedOpeningSheet sheet)
      {
         if (sheet == null) return "";

         string npc = Name(sheet.NpcName, "the character");
         string player = Name(sheet.PlayerName, "the visitor");
         var sb = new StringBuilder();
         Block(sb, $"{npc.ToUpperInvariant()}, AS THE GAME KNOWS THEM:", Cap(sheet.Character, false));
         Block(sb, $"{npc.ToUpperInvariant()}'S HISTORY WITH {player.ToUpperInvariant()} AND MEMORIES (true):", Cap(sheet.History, true));
         Block(sb, $"WHAT {npc.ToUpperInvariant()} HAS LIVED IN THE LAST DAYS (true, and it colours everything between them now):", Lines(sheet.LivedLately));
         Block(sb, "THE TALK OF THE REALM (true):", Lines(sheet.RealmNews));

         var here = new StringBuilder();
         if (!string.IsNullOrWhiteSpace(sheet.Place)) here.AppendLine("Where: " + sheet.Place.Trim());
         string when = string.Join(", ", new[] {sheet.TimeOfDay, sheet.Season, sheet.Weather}.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
         if (when.Length > 0) here.AppendLine("When: " + when);
         if (!string.IsNullOrWhiteSpace(sheet.Activity)) here.AppendLine($"What {npc} is about: {sheet.Activity.Trim()}");
         if (!string.IsNullOrWhiteSpace(sheet.Presence)) here.AppendLine(sheet.Presence.Trim());
         if (!string.IsNullOrWhiteSpace(sheet.PlayerGear)) here.AppendLine($"How {player} comes: {sheet.PlayerGear.Trim()}");
         List<string> present = (sheet.Present ?? new string[0]).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
         here.AppendLine(present.Count == 0
                            ? $"With {player}: no companion; {player} comes alone into the scene."
                            : $"With {player}: {string.Join(", ", present)} (in the scene with them).");
         if (!string.IsNullOrWhiteSpace(sheet.Greeting)) here.AppendLine($"{npc}'s first words to {player}, right after the setting: \"{sheet.Greeting.Trim()}\"");
         Block(sb, "THE ENCOUNTER AS IT STANDS (true):", here.ToString().TrimEnd());

         return sb.ToString().TrimEnd();
      }

      /// <summary>The checker's instructions (the simulator's event check, turned on the opening itself).</summary>
      public const string CheckSystem =
         "You check the opening of a scene against the facts the game gave. The opening may choose and stage those facts, and add the weather,"
         + " the light, smells, sounds, servants, guards, clerks, food, gestures, feelings and the ordinary happenings of the place. It INVENTS when"
         + " it states as true a battle, raid, death, capture, marriage, war, alliance, fief, sum of money or troop count that is not in the facts."
         + " The setting also FAILS when it shows what the facts say the character keeps from the visitor (a secret, a private memory)."
         + " Answer one line only: 'ok', or 'invents <what>' (for a secret shown: 'invents a shown secret').";

      /// <summary>What the checker reads: the facts, then the composed paragraph and the hidden lines.</summary>
      public static string CheckUser(ComposedOpeningSheet sheet, ComposedOpeningReply reply)
         => FactsText(sheet)
            + "\n\nTHE OPENING TO CHECK:"
            + $"\nsetting: {reply?.Setting}"
            + $"\nconcern: {reply?.Concern}"
            + $"\ntension: {reply?.Tension}";

      /// <summary>
      ///   Reads the composer's labelled lines. The setting may run over several lines: a field runs to the next label. Labels
      ///   are matched in English whatever the language of the text. Each field is trimmed of quotes and asterisks and capped.
      /// </summary>
      public static ComposedOpeningReply Parse(string content)
      {
         var fields = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
         // Stage 7: "event:" may repeat; each is its own entry, and a line after one runs into that event, never into the tension.
         var events = new List<StringBuilder>();
         string current = null;
         foreach (string raw in (content ?? "").Replace("\r\n", "\n").Split('\n'))
         {
            string line = raw.Trim().TrimStart('-', '*', '#', ' ').TrimStart();
            if (IsLabelled(line, EventLabel))
            {
               current = EventLabel;
               events.Add(new StringBuilder(AfterLabel(line, EventLabel)));
               continue;
            }

            string label = Labels.FirstOrDefault(l => IsLabelled(line, l));
            if (label != null)
            {
               current = label;
               fields[current] = new StringBuilder(AfterLabel(line, label));
               continue;
            }

            if (current == EventLabel && line.Length > 0) events[events.Count - 1].Append(' ').Append(line);
            else if (current != null && line.Length > 0) fields[current].Append(' ').Append(line);
         }

         string Field(string name, int cap) => fields.TryGetValue(name, out StringBuilder b) ? Clean(b.ToString(), cap) : "";

         return new ComposedOpeningReply {
            Doing = Field("doing", ComposedOpeningPolicy.MaxHiddenChars),
            Setting = Field("setting", ComposedOpeningPolicy.MaxSettingChars),
            Concern = Field("concern", ComposedOpeningPolicy.MaxHiddenChars),
            Tension = Field("tension", ComposedOpeningPolicy.MaxHiddenChars),
            Events = events.Select(e => Clean(e.ToString().Replace("*", ""), ComposedOpeningPolicy.MaxHiddenChars)).Where(e => e.Length > 0).Take(MaxEvents).ToList()
         };
      }

      /// <summary>The most events a composition proposes (the simulator's three).</summary>
      public const int MaxEvents = 3;

      /// <summary>
      ///   The events' check (Stage 7): the simulator's event check of ComposeScene, run apart from the opening's own one-line
      ///   check so one inventing event never sinks the opening.
      /// </summary>
      public const string EventCheckSystem =
         "You check the events proposed for a scene against the facts the game gave. For each event answer one line, 'N: ok' or 'N: invents <what>'."
         + " An event INVENTS when it brings a battle, raid, death, capture, marriage, war, alliance, fief, sum of money or troop count that is"
         + " not in the facts. Bringing news of a fact given, a person or thing the facts place nearby, or an ordinary happening of the place"
         + " (a servant, a clerk, a messenger, the weather) is ok. An event also INVENTS when it shows what the facts say the character keeps"
         + " from the visitor (a secret, a private memory): answer 'N: invents a shown secret'.";

      /// <summary>What the events' check reads: the facts, then the events numbered from 1.</summary>
      public static string EventCheckUser(ComposedOpeningSheet sheet, IReadOnlyList<string> events)
         => FactsText(sheet)
            + "\n\nTHE EVENTS PROPOSED:\n"
            + string.Join("\n", (events ?? new string[0]).Select((e, i) => $"{i + 1}. {e}"));

      /// <summary>
      ///   Reads the events' check: for each of <paramref name="count" /> events, true only when its line says plainly "ok"
      ///   ("N: ok"); "invents", no line, or anything else drops it (a check that did not speak for an event is no check).
      /// </summary>
      public static bool[] ReadEventVerdicts(string content, int count)
      {
         var kept = new bool[Math.Max(0, count)];
         foreach (string raw in (content ?? "").Replace("\r\n", "\n").Split('\n'))
         {
            string line = raw.Trim().TrimStart('-', '*', '#', ' ').Trim();
            if (line.StartsWith("event", StringComparison.OrdinalIgnoreCase)) line = line.Substring(5).TrimStart();
            int digits = 0;
            while (digits < line.Length && char.IsDigit(line[digits])) digits++;
            if (digits == 0 || !int.TryParse(line.Substring(0, digits), out int n) || n < 1 || n > kept.Length) continue;

            string verdict = line.Substring(digits).TrimStart(':', '.', ')', ' ').Trim().Trim('*', '"', '\'', '`', '.', ' ');
            bool ok = verdict.Equals("ok", StringComparison.OrdinalIgnoreCase)
                      || (verdict.Length > 2 && verdict.StartsWith("ok", StringComparison.OrdinalIgnoreCase) && !char.IsLetter(verdict[2])
                          && verdict.IndexOf("invents", StringComparison.OrdinalIgnoreCase) < 0);
            kept[n - 1] = ok;
         }

         return kept;
      }

      private const string EventLabel = "event";

      // An event may come numbered ("event 2:"); the four fields never are.
      private static char[] LabelTail(string label)
         => label == EventLabel ? new[] {'*', ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'} : new[] {'*', ' '};

      private static bool IsLabelled(string line, string label)
         => line.StartsWith(label, StringComparison.OrdinalIgnoreCase)
            && line.Length > label.Length
            && line.Substring(label.Length).TrimStart(LabelTail(label)).StartsWith(":", StringComparison.Ordinal);

      private static string AfterLabel(string line, string label) => line.Substring(label.Length).TrimStart(LabelTail(label)).Substring(1).Trim();

      /// <summary>
      ///   Reads the checker's verdict. Only a plain "ok" passes: "invents ..." fails with what it invents, and an empty or
      ///   unreadable reply fails too (a check that did not run is no check).
      /// </summary>
      public static ComposedOpeningVerdict ReadVerdict(string content)
      {
         string line = (content ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim().Trim('*', '"', '\'', '`', '.', ' ')).FirstOrDefault(l => l.Length > 0) ?? "";
         bool startsInvents = line.StartsWith("invent", StringComparison.OrdinalIgnoreCase);
         // "invents" anywhere fails ("ok, but it invents a raid"), unless it invents nothing; "invented" ("ok: nothing is
         // invented") is not the verdict word.
         int invents = line.IndexOf("invents", StringComparison.OrdinalIgnoreCase);
         bool inventsNothing = invents >= 0 && line.Substring(invents + "invents".Length).TrimStart().StartsWith("nothing", StringComparison.OrdinalIgnoreCase);
         if (invents >= 0 && inventsNothing) invents = -1;
         // "ok", "ok.", "ok: nothing invented": the verdict is its first word.
         if (!startsInvents && invents < 0 && (line.Equals("ok", StringComparison.OrdinalIgnoreCase) || (line.Length > 2 && line.StartsWith("ok", StringComparison.OrdinalIgnoreCase) && !char.IsLetter(line[2]))))
            return new ComposedOpeningVerdict {Passes = true};

         if ((startsInvents && !inventsNothing) || invents >= 0)
         {
            string what = line.Substring(Math.Max(0, invents)).Split(new[] {' '}, 2).Skip(1).FirstOrDefault()?.Trim() ?? "";

            return new ComposedOpeningVerdict {Passes = false, Reason = what.Length == 0 ? "it invents a fact" : "it invents " + what};
         }

         return new ComposedOpeningVerdict {Passes = false, Reason = line.Length == 0 ? "the check gave no verdict" : "the check gave no readable verdict"};
      }

      private static string Clean(string text, int cap)
      {
         string t = (text ?? "").Trim().Trim('*', '"', '“', '”', ' ').Trim();
         if (t.StartsWith("<", StringComparison.Ordinal) && t.EndsWith(">", StringComparison.Ordinal)) t = t.Substring(1, t.Length - 2).Trim();
         if (t.Equals("none", StringComparison.OrdinalIgnoreCase)) return "";

         return t.Length <= cap ? t : t.Substring(0, cap).TrimEnd();
      }

      private static string Name(string name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();

      // The history keeps its latest lines (the tail), every other block its first ones.
      private static string Cap(string text, bool keepTail)
      {
         string t = (text ?? "").Trim();
         if (t.Length <= MaxBlockChars) return t;

         return keepTail ? "..." + t.Substring(t.Length - MaxBlockChars) : t.Substring(0, MaxBlockChars) + "...";
      }

      private static string Lines(IReadOnlyList<string> lines)
         => string.Join("\n", (lines ?? new string[0]).Where(l => !string.IsNullOrWhiteSpace(l)).Take(MaxListLines).Select(l => "- " + l.Trim()));

      private static void Block(StringBuilder sb, string heading, string body)
      {
         if (string.IsNullOrWhiteSpace(body)) return;

         sb.AppendLine(heading);
         sb.AppendLine(body);
         sb.AppendLine();
      }
   }
}
