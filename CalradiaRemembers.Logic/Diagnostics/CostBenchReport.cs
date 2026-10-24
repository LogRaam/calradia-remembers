// Code written by Gabriel Mailhot, 24/10/2026.
// What the mod costs to play, measured, per model and per role. fearente (Nexus, 24/10/2026): "I've already spent
// around $3.19 without particularly heavy usage. I know you've mentioned that heavy usage can cost around $1 per
// week". That figure was measured once, on Grok 4.20, before the prompt grew and before a reply became two calls; the
// rest of the page's prices were extrapolated by price ratio. Gabriel (24/10/2026): a script that tests several models
// and MCM setups, a table in the description naming the version it was measured on; then: measure the PROSE and the
// INTERPRET roles apart, "car la tache est differente pour les deux modes", and report each by the length of the
// conversation (start, middle, long) with its quality and its cost.
// cr.cost_bench runs four passes: PROSE (each model plays the opening of a conversation for real, then answers at the
// middle and late in a long one over a synthetic history of the right size, folded as the chat folds it; Gabriel,
// 24/10/2026: three points instead of twenty replies), INTERPRET (each model reads the same scripted replies, by difficulty, and
// is scored against what each reply really did), STANDARD (one integrated call), FOLD (one fold of the same recap per
// Background model). The prose x interpret grid is the sum of the two parts: the two calls share nothing in Enhanced.
// This is the part that needs no game: the scenario file, the windows, the signals, the sums and the tables.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NpcMemoryService.Core.Actions;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Which kind of call a billed attempt belonged to.</summary>
   public enum CostPhase
   {
      /// <summary>The character's reply (in a Standard row it carries its own tags).</summary>
      Prose,

      /// <summary>
      ///   The character setting the scene before the player's first word (MCM "Characters set the scene", on by default):
      ///   one PROSE call and no interpreter, the first of a conversation, so the one that pays the cold prompt.
      /// </summary>
      Opener,

      /// <summary>The interpret model reading a reply for its actions.</summary>
      Interpreter,

      /// <summary>The reply redone on the NSFW model after a cut or a refusal: still the prose's work.</summary>
      Fallback,

      /// <summary>A letter the character writes unbidden (the prose model, the full prompt).</summary>
      Letter,

      /// <summary>The older part of a long conversation folded into a recap (the Background model).</summary>
      Fold
   }

   /// <summary>Which pass a row of the scenario file belongs to.</summary>
   public enum CostPass
   {
      /// <summary>A model writing replies in Enhanced, with no interpreter.</summary>
      Prose,

      /// <summary>A model reading scripted replies for their actions.</summary>
      Interpret,

      /// <summary>A model writing and tagging in one call (Chat Composition: Standard).</summary>
      Standard,

      /// <summary>A Background model folding a long conversation into its recap.</summary>
      Fold
   }

   /// <summary>How hard a scripted reply is for the interpreter.</summary>
   public enum InterpretDifficulty
   {
      /// <summary>An ordinary reply in which nothing is done: nothing must be emitted.</summary>
      NoAction,

      /// <summary>A deed done plainly, one action.</summary>
      Simple,

      /// <summary>A deed done in flowery or oblique words, or several deeds at once.</summary>
      Oblique,

      /// <summary>A deed named but not done (offered, refused, threatened, put off): it must be withheld.</summary>
      FalseLead
   }

   /// <summary>One row of the scenario file.</summary>
   public sealed class CostScenario
   {
      /// <summary>The row's name in the tables.</summary>
      public string Name { get; init; } = "";

      /// <summary>The pass it belongs to.</summary>
      public CostPass Pass { get; init; }

      /// <summary>The model measured.</summary>
      public string Model { get; init; } = "";

      /// <summary>MCM "Compact prompt for small models" (prose and standard rows).</summary>
      public bool Compact { get; init; }

      /// <summary>MCM "Reasoning Effort (PROSE)" (prose and standard rows); blank keeps the player's own.</summary>
      public string Reasoning { get; init; } = "";

      /// <summary>MCM "NSFW model", the fallback that redoes a refused or cut reply (prose rows); blank keeps the player's own.</summary>
      public string FallbackModel { get; init; } = "";

      /// <summary>
      ///   Interpret rows: readings per difficulty, or 0 for the whole catalog (the default). Gabriel (24/10/2026): the sample
      ///   an interpreter's score needs, inside this test; six per difficulty told 24/24 from 23/24 and nothing more.
      /// </summary>
      public int CasesPerDifficulty { get; init; }

      /// <summary>How the row's setup reads in a table.</summary>
      public string Setup
         => (Compact ? "Compact prompt" : "full prompt")
            + (string.IsNullOrWhiteSpace(Reasoning) ? "" : $", reasoning {Reasoning}")
            + (string.IsNullOrWhiteSpace(FallbackModel) ? "" : $", fallback {FallbackModel}");
   }

   /// <summary>One billed attempt, as OpenRouter reported it.</summary>
   public sealed class CostCall
   {
      /// <summary>The row's name.</summary>
      public string ScenarioName { get; init; } = "";

      /// <summary>The row's pass: a name may stand in several sections (Grok 4.20 writes, reads and folds).</summary>
      public CostPass Pass { get; init; }

      /// <summary>Which call it was.</summary>
      public CostPhase Phase { get; init; }

      /// <summary>The player's line it answered, from 1; 0 for a letter, a fold or an interpreter case.</summary>
      public int Turn { get; init; }

      /// <summary>An interpreter case's difficulty (interpret rows only).</summary>
      public InterpretDifficulty Difficulty { get; init; }

      /// <summary>The model the attempt went to.</summary>
      public string Model { get; init; } = "";

      /// <summary>Tokens sent.</summary>
      public int PromptTokens { get; init; }

      /// <summary>Of those, read from the provider's cache.</summary>
      public int CachedTokens { get; init; }

      /// <summary>Tokens written, reasoning included.</summary>
      public int CompletionTokens { get; init; }

      /// <summary>Of those, spent thinking: billed, never shown.</summary>
      public int ReasoningTokens { get; init; }

      /// <summary>The bill in US dollars; null when the provider did not say (the row then says it undercounts).</summary>
      public double? Cost { get; init; }

      /// <summary>How long the player waited for this attempt, in seconds (Gabriel, 24/10/2026: response time beside cost).</summary>
      public double Seconds { get; init; }
   }

   /// <summary>How an interpreter read one scripted reply.</summary>
   public sealed class InterpretOutcome
   {
      /// <summary>The row's name.</summary>
      public string ScenarioName { get; init; } = "";

      /// <summary>The case's difficulty.</summary>
      public InterpretDifficulty Difficulty { get; init; }

      /// <summary>It emitted exactly what the reply did (or nothing, when nothing was done).</summary>
      public bool Right { get; init; }

      /// <summary>It emitted a deed the reply did not do: the game would act on something that never happened.</summary>
      public bool Invented { get; init; }
   }

   /// <summary>A prose (or standard) row's costs and signals.</summary>
   public sealed class ProseRowResult
   {
      /// <summary>The row measured.</summary>
      public CostScenario Row { get; init; } = new CostScenario();

      /// <summary>The scene the character sets before the player speaks; null when the scene opener is off.</summary>
      public double? Opener { get; init; }

      /// <summary>Seconds for the scene opener.</summary>
      public double OpenerSeconds { get; init; }

      /// <summary>The first reply to the player: cold when there was no scene, warmed by it when there was.</summary>
      public double Start { get; init; }

      /// <summary>An ordinary reply in the middle of a conversation (turn 8), warm and before anything is folded.</summary>
      public double Middle { get; init; }

      /// <summary>A reply late in a long conversation (turn 18), its older part folded into a recap.</summary>
      public double Long { get; init; }

      /// <summary>One letter; null when none was written.</summary>
      public double? Letter { get; init; }

      /// <summary>Seconds the player waits for the first reply, a middle one and a late one (retries and fallback included).</summary>
      public double StartSeconds { get; init; }

      /// <summary>Seconds for the middle reply.</summary>
      public double MiddleSeconds { get; init; }

      /// <summary>Seconds for the late reply.</summary>
      public double LongSeconds { get; init; }

      /// <summary>Of the tokens written, the share spent thinking (0 to 1).</summary>
      public double ThinkingShare { get; init; }

      /// <summary>Of the tokens sent, the share read from cache (0 to 1).</summary>
      public double CachedShare { get; init; }

      /// <summary>Replies answered.</summary>
      public int Replies { get; init; }

      /// <summary>Advisory signals: replies repeated word for word, refusals, sermons after reassurance, Earth references.</summary>
      public ProseSignals Signals { get; init; } = new ProseSignals();

      /// <summary>True when some attempt came back without a price.</summary>
      public bool Unpriced { get; init; }

      /// <summary>What went wrong, if anything.</summary>
      public string Note { get; init; } = "";
   }

   /// <summary>Advisory signals over a prose row's replies: they point the eye, the reader judges.</summary>
   public sealed class ProseSignals
   {
      /// <summary>Replies repeated word for word.</summary>
      public int Repeats { get; init; }

      /// <summary>Replies that read as a refusal or a broken character.</summary>
      public int Refusals { get; init; }

      /// <summary>Replies after the reassurance that still warn or lecture (fearente's complaint).</summary>
      public int Sermons { get; init; }

      /// <summary>Replies naming Earth's faiths or figures.</summary>
      public int EarthReferences { get; init; }

      /// <summary>Whether the late reply found, in the folded recap, what was agreed early in the conversation.</summary>
      public bool RecapRecalled { get; init; }

      /// <summary>The signals as one cell.</summary>
      public override string ToString()
         => $"repeats {Repeats}, refusals {Refusals}, sermons {Sermons}, earth {EarthReferences}, recap {(RecapRecalled ? "recalled" : "missed")}";
   }

   /// <summary>An interpret row's costs and score, by difficulty.</summary>
   public sealed class InterpretRowResult
   {
      /// <summary>The row measured.</summary>
      public CostScenario Row { get; init; } = new CostScenario();

      /// <summary>Per difficulty: the average cost of one reading, right readings, cases, deeds invented.</summary>
      public IReadOnlyDictionary<InterpretDifficulty, (double Cost, int Right, int Cases, int Invented)> ByDifficulty { get; init; }
         = new Dictionary<InterpretDifficulty, (double, int, int, int)>();

      /// <summary>The average cost of one reading over every case: the INTERPRET part of an ordinary reply.</summary>
      public double PerReading { get; init; }

      /// <summary>The average seconds of one reading: what the INTERPRET call adds to the player's wait.</summary>
      public double SecondsPerReading { get; init; }

      /// <summary>Right readings over all cases, with its 95% interval (Wilson), so a small sample says how little it knows.</summary>
      public (int Right, int Cases, double Low, double High) Accuracy { get; init; }

      /// <summary>True when some attempt came back without a price.</summary>
      public bool Unpriced { get; init; }

      /// <summary>What went wrong, if anything.</summary>
      public string Note { get; init; } = "";
   }

   /// <summary>A fold row: what one fold of the reference recap cost.</summary>
   public sealed class FoldRowResult
   {
      /// <summary>The row measured.</summary>
      public CostScenario Row { get; init; } = new CostScenario();

      /// <summary>One fold; null when it failed.</summary>
      public double? Cost { get; init; }

      /// <summary>What went wrong, if anything.</summary>
      public string Note { get; init; } = "";
   }

   /// <summary>The scenario file: sections of rows, edited by hand. Pure.</summary>
   public static class CostScenarioFile
   {
      /// <summary>The default rows: the models players asked about (fearente, Gabriel, 24/10/2026).</summary>
      public static IReadOnlyList<CostScenario> Defaults { get; } = new[]
      {
         Prose("Grok 4.20", "x-ai/grok-4.20"),
         new CostScenario {Name = "Grok 4.20, Compact", Pass = CostPass.Prose, Model = "x-ai/grok-4.20", Compact = true},
         Prose("Gemini 3.8 Flash", "google/gemini-3.8-flash"),
         Prose("DeepSeek V4 Flash", "deepseek/deepseek-v4-flash"),
         Prose("GPT-6 Luna", "openai/gpt-6-luna"),
         Prose("Gemma 4 31B", "google/gemma-4-31b-it"),
         // Gabriel (24/10/2026): GLM 4.6 writing, Grok 4.20 standing by as the NSFW fallback.
         new CostScenario {Name = "GLM 4.6", Pass = CostPass.Prose, Model = "z-ai/glm-4.6", FallbackModel = "x-ai/grok-4.20"},
         Interpret("Grok 4.20", "x-ai/grok-4.20"),
         Interpret("Gemini 3.8 Flash", "google/gemini-3.8-flash"),
         Interpret("DeepSeek V4 Flash", "deepseek/deepseek-v4-flash"),
         new CostScenario {Name = "Grok 4.20, Standard", Pass = CostPass.Standard, Model = "x-ai/grok-4.20"},
         new CostScenario {Name = "Grok 4.20", Pass = CostPass.Fold, Model = "x-ai/grok-4.20"},
         new CostScenario {Name = "DeepSeek V4 Flash", Pass = CostPass.Fold, Model = "deepseek/deepseek-v4-flash"}
      };

      /// <summary>The file written when none exists: a header that teaches the format, then the defaults.</summary>
      public static string Template()
      {
         var sb = new StringBuilder();
         sb.AppendLine("# cr.cost_bench scenarios. Model ids are OpenRouter's. Lines starting with # are ignored.");
         sb.AppendLine("# [prose]     name | model | Full or Compact | reasoning | NSFW fallback model   (blank = yours)");
         sb.AppendLine("# [interpret] name | model | readings per difficulty (blank = the whole catalog, about 300)");
         sb.AppendLine("# [standard]  name | model | Full or Compact | reasoning");
         sb.AppendLine("# [fold]      name | model   (the Background model that folds a long conversation)");
         sb.AppendLine("# cr.cost_bench plan shows what a run would do.");
         foreach (CostPass pass in new[] {CostPass.Prose, CostPass.Interpret, CostPass.Standard, CostPass.Fold})
         {
            sb.AppendLine($"[{pass.ToString().ToLowerInvariant()}]");
            foreach (CostScenario s in Defaults.Where(d => d.Pass == pass))
               sb.AppendLine(pass == CostPass.Interpret || pass == CostPass.Fold
                  ? $"{s.Name} | {s.Model}"
                  : $"{s.Name} | {s.Model} | {(s.Compact ? "Compact" : "Full")} | {s.Reasoning}{(pass == CostPass.Prose ? $" | {s.FallbackModel}" : "")}");
         }

         return sb.ToString();
      }

      /// <summary>
      ///   The rows of <paramref name="text" />. A line that cannot be read is skipped and named in
      ///   <paramref name="problems" />, so one typo never silently drops a model from the tables.
      /// </summary>
      public static IReadOnlyList<CostScenario> Parse(string text, out List<string> problems)
      {
         problems = new List<string>();
         var rows = new List<CostScenario>();
         if (string.IsNullOrWhiteSpace(text)) return rows;

         CostPass? section = null;
         string[] lines = text.Replace("\r\n", "\n").Split('\n');
         for (var i = 0; i < lines.Length; i++)
         {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
               section = Enum.TryParse(line.Substring(1, line.Length - 2), true, out CostPass p) ? p : (CostPass?) null;
               if (section == null) problems.Add($"line {i + 1}: unknown section {line} (prose, interpret, standard, fold)");
               continue;
            }

            string[] f = line.Split('|').Select(x => x.Trim()).ToArray();
            if (section == null || f.Length < 2 || f[0].Length == 0 || f[1].Length == 0)
            {
               problems.Add(section == null ? $"line {i + 1}: a row before any [section]" : $"line {i + 1}: expected at least 'name | model'");
               continue;
            }

            if (section == CostPass.Interpret)
            {
               int perClass = 0;
               if (f.Length > 2 && f[2].Length > 0 && (!int.TryParse(f[2], out perClass) || perClass < 1))
               {
                  problems.Add($"line {i + 1}: readings per difficulty must be a number, or blank for the whole catalog");
                  continue;
               }

               rows.Add(new CostScenario {Name = f[0], Pass = CostPass.Interpret, Model = f[1], CasesPerDifficulty = perClass});
               continue;
            }

            bool compact = f.Length > 2 && string.Equals(f[2], "compact", StringComparison.OrdinalIgnoreCase);
            if (f.Length > 2 && f[2].Length > 0 && !compact && !string.Equals(f[2], "full", StringComparison.OrdinalIgnoreCase))
            {
               problems.Add($"line {i + 1}: the prompt must be Full or Compact");
               continue;
            }

            rows.Add(new CostScenario {
               Name = f[0], Pass = section.Value, Model = f[1], Compact = compact, Reasoning = f.Length > 3 ? f[3] : "",
               FallbackModel = section == CostPass.Prose && f.Length > 4 ? f[4] : ""
            });
         }

         return rows;
      }

      private static CostScenario Prose(string name, string model) => new CostScenario {Name = name, Pass = CostPass.Prose, Model = model};

      private static CostScenario Interpret(string name, string model) => new CostScenario {Name = name, Pass = CostPass.Interpret, Model = model};
   }

   /// <summary>
   ///   The points a prose row is measured at. Gabriel (24/10/2026): play the opening for real, then build the middle and the
   ///   long conversation from a synthetic history of the right size, instead of twenty replies. Each synthetic point is
   ///   answered twice: a warm-up reply (billed, not counted) so the provider's cache is as warm as in play, then the
   ///   measured one. Pure.
   /// </summary>
   public static class CostWindows
   {
      /// <summary>The first reply: the cache is cold.</summary>
      public const int StartTurn = 1;

      /// <summary>Replies played for real at the opening: the greeting, fearente's ships and his reassurance.</summary>
      public const int OpeningTurns = 3;

      /// <summary>
      ///   The middle: turn 8, after six synthetic exchanges and a warm-up at turn 7. Fourteen lines then stand unfolded,
      ///   the most the chat leaves before it folds (ConversationCompressionPolicy.UnfoldedLineThreshold).
      /// </summary>
      public static readonly IReadOnlyList<int> MiddleTurns = new[] {8};

      /// <summary>The middle's warm-up reply.</summary>
      public const int MiddleWarmUp = 7;

      /// <summary>
      ///   A long conversation: turn 18, after ten exchanges folded into a recap, six kept verbatim and a warm-up at turn
      ///   17, the shape the chat leaves after its folds.
      /// </summary>
      public static readonly IReadOnlyList<int> LongTurns = new[] {18};

      /// <summary>The long conversation's warm-up reply.</summary>
      public const int LongWarmUp = 17;

      /// <summary>
      ///   The player's reassurance line (fearente's ships): a warning in the reply to it, or after, is a sermon. fearente:
      ///   "Even after I reassured her that everything would be fine, she launched into another speech about pride".
      /// </summary>
      public const int ReassuranceTurn = 3;
   }

   /// <summary>The fearente signal: a reply that still warns or lectures after the player has answered the concern. Pure.</summary>
   public static class SermonSignal
   {
      private static readonly string[] Markers = {
         "pride", "prideful", "arrogan", "hubris", "humble", "humbled", "downfall", "be mindful", "mind you", "beware", "heed my",
         "reckless", "foolhard", "a fool", "lesson"
      };

      /// <summary>True when <paramref name="reply" /> carries a lecturing marker. Advisory: the transcript is the judge.</summary>
      public static bool Lectures(string reply)
      {
         if (string.IsNullOrWhiteSpace(reply)) return false;

         string lower = reply.ToLowerInvariant();

         return Markers.Any(m => lower.Contains(m));
      }
   }

   /// <summary>Whether a late reply found what the folded recap holds: the levies agreed early in the conversation. Pure.</summary>
   public static class RecapSignal
   {
      /// <summary>The agreement the synthetic recap carries: forty spearmen for the levies, grain for her villages in return.</summary>
      public const string Agreement = "forty spearmen";

      /// <summary>True when <paramref name="reply" /> names the agreement. Advisory: the transcript is the judge.</summary>
      public static bool Recalled(string reply)
      {
         if (string.IsNullOrWhiteSpace(reply)) return false;

         string lower = reply.ToLowerInvariant();

         return lower.Contains("forty") || lower.Contains("40");
      }
   }

   /// <summary>Picks and scores the interpreter's cases. Pure.</summary>
   public static class InterpretCases
   {
      /// <summary>
      ///   The difficulty of a catalog case: a withheld deed is a false lead; a plain first phrasing (no _v suffix, one
      ///   action) is simple; a flowery or oblique rephrasing (_v2, _v3) or several deeds at once is oblique.
      /// </summary>
      public static InterpretDifficulty Classify(ActionBenchCase c)
      {
         if (c.IsNegative) return InterpretDifficulty.FalseLead;
         if (c.Expected.Count > 1) return InterpretDifficulty.Oblique;

         int v = c.Id.LastIndexOf("_v", StringComparison.Ordinal);

         return v > 0 && v + 2 < c.Id.Length && char.IsDigit(c.Id[v + 2]) ? InterpretDifficulty.Oblique : InterpretDifficulty.Simple;
      }

      /// <summary>
      ///   <paramref name="perClass" /> cases of each difficulty, spread evenly over the catalog so they cover different
      ///   verbs, the same on every run (an interpreter is compared with another on identical work).
      /// </summary>
      public static IReadOnlyList<ActionBenchCase> Pick(IReadOnlyList<ActionBenchCase> catalog, InterpretDifficulty difficulty, int perClass)
      {
         List<ActionBenchCase> mine = catalog.Where(c => Classify(c) == difficulty).ToList();
         if (mine.Count <= perClass) return mine;

         double step = (double) mine.Count / perClass;

         return Enumerable.Range(0, perClass).Select(i => mine[(int) (i * step)]).ToList();
      }

      /// <summary>
      ///   An ordinary reply in which nothing is done reads right when nothing but a change of regard was emitted (warm
      ///   talk may move regard); anything else is a deed invented.
      /// </summary>
      public static bool NoActionReadRight(IEnumerable<string> emittedTypes)
         => (emittedTypes ?? Enumerable.Empty<string>()).All(t => string.Equals(t, "change_relation", StringComparison.OrdinalIgnoreCase));

      /// <summary>Whether a catalog verdict is a right reading, and whether it invented a deed.</summary>
      public static (bool Right, bool Invented) Read(ActionBenchVerdict verdict)
         => (verdict == ActionBenchVerdict.Hit || verdict == ActionBenchVerdict.CorrectWithhold, verdict == ActionBenchVerdict.FalsePositive);
   }

   /// <summary>The sums and the tables. Pure.</summary>
   public static class CostBenchReport
   {
      /// <summary>Whether a run must stop: the spend has reached the cap (a cap of zero or less means none).</summary>
      public static bool CapReached(double spent, double cap) => cap > 0 && spent >= cap;

      /// <summary>A prose or standard row's costs from its billed attempts and its replies (by turn, from 1).</summary>
      public static ProseRowResult SummarizeProse(CostScenario row, IReadOnlyList<CostCall> calls, IReadOnlyList<string> replies, string note = "")
      {
         List<CostCall> mine = Mine(calls, row);
         Dictionary<int, double> perTurn = mine.Where(c => c.Turn > 0 && c.Phase != CostPhase.Fold).GroupBy(c => c.Turn)
                                               .ToDictionary(g => g.Key, g => g.Sum(c => c.Cost ?? 0));
         List<CostCall> letter = mine.Where(c => c.Phase == CostPhase.Letter).ToList();
         List<CostCall> opener = mine.Where(c => c.Phase == CostPhase.Opener).ToList();
         List<CostCall> writing = mine.Where(c => c.Phase != CostPhase.Fold).ToList();
         List<string> said = (replies ?? new List<string>()).ToList();

         Dictionary<int, double> secondsPerTurn = mine.Where(c => c.Turn > 0).GroupBy(c => c.Turn).ToDictionary(g => g.Key, g => g.Sum(c => c.Seconds));

         return new ProseRowResult {
            Row = row,
            Opener = opener.Count > 0 ? opener.Sum(c => c.Cost ?? 0) : (double?) null,
            OpenerSeconds = opener.Sum(c => c.Seconds),
            StartSeconds = secondsPerTurn.TryGetValue(CostWindows.StartTurn, out double ss) ? ss : 0,
            MiddleSeconds = Average(secondsPerTurn, CostWindows.MiddleTurns),
            LongSeconds = Average(secondsPerTurn, CostWindows.LongTurns),
            Start = perTurn.TryGetValue(CostWindows.StartTurn, out double s) ? s : 0,
            Middle = Average(perTurn, CostWindows.MiddleTurns),
            Long = Average(perTurn, CostWindows.LongTurns),
            Letter = letter.Count > 0 ? letter.Sum(c => c.Cost ?? 0) : (double?) null,
            ThinkingShare = Share(writing.Sum(c => c.ReasoningTokens), writing.Sum(c => c.CompletionTokens)),
            CachedShare = Share(writing.Sum(c => c.CachedTokens), writing.Sum(c => c.PromptTokens)),
            Replies = perTurn.Count,
            Signals = new ProseSignals {
               Repeats = RoleplayBenchChecks.HasVerbatimRepeat(said),
               Refusals = said.Count(RoleplayBenchChecks.LooksLikeRefusal),
               Sermons = said.Skip(CostWindows.ReassuranceTurn - 1).Count(SermonSignal.Lectures),
               EarthReferences = said.Count(RoleplayBenchChecks.MentionsEarthReference),
               RecapRecalled = said.Count > 0 && RecapSignal.Recalled(said[said.Count - 1])
            },
            Unpriced = mine.Any(c => c.Cost == null),
            Note = note ?? ""
         };
      }

      /// <summary>An interpret row's costs and score, by difficulty.</summary>
      public static InterpretRowResult SummarizeInterpret(CostScenario row, IReadOnlyList<CostCall> calls, IReadOnlyList<InterpretOutcome> outcomes, string note = "")
      {
         List<CostCall> mine = Mine(calls, row);
         List<InterpretOutcome> read = (outcomes ?? new List<InterpretOutcome>()).Where(o => o.ScenarioName == row.Name).ToList();
         var byDifficulty = new Dictionary<InterpretDifficulty, (double, int, int, int)>();
         foreach (InterpretDifficulty d in Enum.GetValues(typeof(InterpretDifficulty)))
         {
            List<InterpretOutcome> o = read.Where(x => x.Difficulty == d).ToList();
            if (o.Count == 0) continue;

            double cost = mine.Where(c => c.Difficulty == d).Sum(c => c.Cost ?? 0) / o.Count;
            byDifficulty[d] = (cost, o.Count(x => x.Right), o.Count, o.Count(x => x.Invented));
         }

         return new InterpretRowResult {
            Row = row,
            ByDifficulty = byDifficulty,
            PerReading = read.Count > 0 ? mine.Sum(c => c.Cost ?? 0) / read.Count : 0,
            SecondsPerReading = read.Count > 0 ? mine.Sum(c => c.Seconds) / read.Count : 0,
            Accuracy = Wilson(read.Count(o => o.Right), read.Count),
            Unpriced = mine.Any(c => c.Cost == null),
            Note = note ?? ""
         };
      }

      /// <summary>What a run would do, with no call made.</summary>
      public static string Plan(IReadOnlyList<CostScenario> rows, int turns, int casesPerDifficulty, double cap)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"cr.cost_bench plan: {rows.Count} row(s), spend cap ${Money(cap)}.");
         foreach (CostScenario r in rows)
         {
            string work = r.Pass switch
            {
               CostPass.Prose => $"the scene opener, {CostWindows.OpeningTurns} replies played, then the middle and a long conversation (a warm-up and a measured reply each), a letter; about {turns} calls",
               CostPass.Standard => $"the same in one call each; about {turns} calls",
               CostPass.Interpret => r.CasesPerDifficulty > 0
                  ? $"{r.CasesPerDifficulty} readings of each of 4 difficulties; {r.CasesPerDifficulty * 4} calls"
                  : $"the whole catalog and {casesPerDifficulty} ordinary replies; about {casesPerDifficulty + 300} calls",
               _ => "one fold of the reference recap; 1 call"
            };
            sb.AppendLine($"  [{r.Pass.ToString().ToLowerInvariant()}] {r.Name}: {r.Model}; {(r.Pass == CostPass.Interpret || r.Pass == CostPass.Fold ? "" : r.Setup + "; ")}{work}");
         }

         return sb.ToString();
      }

      /// <summary>The full report for the log and the repository (Markdown).</summary>
      public static string Markdown(IReadOnlyList<ProseRowResult> prose, IReadOnlyList<InterpretRowResult> interpret,
                                    IReadOnlyList<FoldRowResult> folds, string version, DateTime measuredOn)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"Measured with Calradia Remembers {version} on {measuredOn:yyyy-MM-dd}, OpenRouter's own bill for each call.");
         sb.AppendLine();

         sb.AppendLine("PROSE (Enhanced: the reply alone) and STANDARD (one call writes and tags), per reply:");
         sb.AppendLine("| Model | Role | Setup | Scene opener | First reply (turn 1) | Middle (turn 8) | Long (turn 18, folded) | Letter | Thinking | From cache | Signals |");
         sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
         foreach (ProseRowResult r in prose)
            sb.AppendLine($"| {r.Row.Name} ({r.Row.Model}) | {(r.Row.Pass == CostPass.Standard ? "STANDARD" : "PROSE")} | {r.Row.Setup} | {(r.Opener == null ? "-" : $"${Money(r.Opener.Value)}, {Secs(r.OpenerSeconds)}")} | ${Money(r.Start)}, {Secs(r.StartSeconds)} | ${Money(r.Middle)}, {Secs(r.MiddleSeconds)} | ${Money(r.Long)}, {Secs(r.LongSeconds)} | {(r.Letter == null ? "-" : "$" + Money(r.Letter.Value))} | {Percent(r.ThinkingShare)} | {Percent(r.CachedShare)} | {r.Signals} |{Flags(r.Unpriced, r.Note)}");
         sb.AppendLine("The middle and the long conversation answer over a synthetic history of the same size for every model (a warm-up reply first, billed, not counted): models are compared on the same input; a wordier model would weigh more on its own history in play.");
         sb.AppendLine();

         sb.AppendLine("INTERPRET, per reading, with right readings out of cases (and deeds invented):");
         sb.AppendLine("| Model | No action | Simple | Oblique | False lead | Read right (95%) | Per reading |");
         sb.AppendLine("|---|---|---|---|---|---|---|");
         foreach (InterpretRowResult r in interpret)
            sb.AppendLine($"| {r.Row.Name} ({r.Row.Model}) | {Cell(r, InterpretDifficulty.NoAction)} | {Cell(r, InterpretDifficulty.Simple)} | {Cell(r, InterpretDifficulty.Oblique)} | {Cell(r, InterpretDifficulty.FalseLead)} | {AccuracyText(r)} | ${Money(r.PerReading)}, {Secs(r.SecondsPerReading)} |{Flags(r.Unpriced, r.Note)}");
         sb.AppendLine();

         sb.AppendLine("FOLD, one fold of the same recap, per Background model:");
         sb.AppendLine("| Model | One fold |");
         sb.AppendLine("|---|---|");
         foreach (FoldRowResult f in folds)
            sb.AppendLine($"| {f.Row.Name} ({f.Row.Model}) | {(f.Cost == null ? "failed" : "$" + Money(f.Cost.Value))} |{Flags(false, f.Note)}");
         sb.AppendLine();

         sb.Append(Grid(prose, interpret, nexus: false));

         return sb.ToString();
      }

      /// <summary>
      ///   The tables for the Nexus description, as [code] blocks: monospace keeps the columns aligned whatever the page's
      ///   bbcode supports.
      /// </summary>
      public static string NexusCode(IReadOnlyList<ProseRowResult> prose, IReadOnlyList<InterpretRowResult> interpret, string version, DateTime measuredOn)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"[b]Measured costs[/b] (Calradia Remembers {version}, {measuredOn:yyyy-MM-dd}, OpenRouter's own bill for each call)");
         sb.Append(Code(new[] {new[] {"Prose model", "Setup", "Scene", "1st reply", "Middle", "Long talk", "Letter"}}
                       .Concat(prose.Where(p => p.Row.Pass == CostPass.Prose).Select(p => new[] {
                          p.Row.Name, p.Row.Setup, p.Opener == null ? "-" : $"${Money(p.Opener.Value)} {Secs(p.OpenerSeconds)}",
                          $"${Money(p.Start)} {Secs(p.StartSeconds)}", $"${Money(p.Middle)} {Secs(p.MiddleSeconds)}",
                          $"${Money(p.Long)} {Secs(p.LongSeconds)}", p.Letter == null ? "-" : "$" + Money(p.Letter.Value)
                       }))));
         sb.Append(Code(new[] {new[] {"Interpret model", "Per reply", "Read right"}}
                       .Concat(interpret.Select(i => new[] {i.Row.Name, $"${Money(i.PerReading)} {Secs(i.SecondsPerReading)}", AccuracyText(i)}))));
         sb.Append(Grid(prose, interpret, nexus: true));

         return sb.ToString();
      }

      /// <summary>The prose x interpret grid for an ordinary reply in the middle of a conversation: the sum of the two parts.</summary>
      private static string Grid(IReadOnlyList<ProseRowResult> prose, IReadOnlyList<InterpretRowResult> interpret, bool nexus)
      {
         List<ProseRowResult> writers = prose.Where(p => p.Row.Pass == CostPass.Prose).ToList();
         if (writers.Count == 0 || interpret.Count == 0) return "";

         var rows = new List<string[]> {new[] {"Prose \\ Interpret"}.Concat(interpret.Select(i => i.Row.Name)).ToArray()};
         rows.AddRange(writers.Select(p => new[] {p.Row.Name}
                                 .Concat(interpret.Select(i => $"${Money(p.Middle + i.PerReading)} {Secs(p.MiddleSeconds + i.SecondsPerReading)}")).ToArray()));
         const string caption = "Enhanced, one ordinary reply (middle of a conversation) = its PROSE part + its INTERPRET part, in dollars and in seconds the player waits (the two calls run one after the other). Computed: the two calls share nothing; a wordier prose model gives the interpreter a little more to read.";

         if (nexus) return caption + "\n" + Code(rows);

         var sb = new StringBuilder();
         sb.AppendLine(caption);
         sb.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
         sb.AppendLine("|" + string.Concat(Enumerable.Repeat("---|", rows[0].Length)));
         foreach (string[] r in rows.Skip(1)) sb.AppendLine("| " + string.Join(" | ", r) + " |");

         return sb.ToString();
      }

      private static string Code(IEnumerable<string[]> table)
      {
         List<string[]> rows = table.ToList();
         int[] widths = Enumerable.Range(0, rows[0].Length).Select(i => rows.Max(row => row[i].Length)).ToArray();
         var sb = new StringBuilder();
         sb.AppendLine("[code]");
         foreach (string[] row in rows)
            sb.AppendLine(string.Join("  ", row.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd());
         sb.AppendLine("[/code]");

         return sb.ToString();
      }

      private static string Secs(double seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

      private static string AccuracyText(InterpretRowResult r)
         => r.Accuracy.Cases == 0
            ? "-"
            : $"{r.Accuracy.Right}/{r.Accuracy.Cases} ({Percent(r.Accuracy.Low)}-{Percent(r.Accuracy.High)})";

      private static string Cell(InterpretRowResult r, InterpretDifficulty d)
         => r.ByDifficulty.TryGetValue(d, out (double Cost, int Right, int Cases, int Invented) v)
            ? $"${Money(v.Cost)}, {v.Right}/{v.Cases}{(v.Invented > 0 ? $" ({v.Invented} invented)" : "")}"
            : "-";

      private static List<CostCall> Mine(IReadOnlyList<CostCall> calls, CostScenario row)
         => (calls ?? new List<CostCall>()).Where(c => c.ScenarioName == row.Name && c.Pass == row.Pass).ToList();

      private static double Average(Dictionary<int, double> perTurn, IReadOnlyList<int> turns)
      {
         List<double> seen = turns.Where(perTurn.ContainsKey).Select(t => perTurn[t]).ToList();

         return seen.Count > 0 ? seen.Average() : 0;
      }

      private static double Share(int part, int whole) => whole > 0 ? (double) part / whole : 0;

      /// <summary>
      ///   <paramref name="right" /> out of <paramref name="cases" /> with its 95% Wilson interval: honest at small samples and
      ///   near 100%, where the plain interval claims a certainty it does not have.
      /// </summary>
      public static (int Right, int Cases, double Low, double High) Wilson(int right, int cases)
      {
         if (cases <= 0) return (0, 0, 0, 0);

         const double z = 1.96;
         double p = (double) right / cases, n = cases, z2 = z * z;
         double centre = (p + z2 / (2 * n)) / (1 + z2 / n);
         double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4 * n * n)) / (1 + z2 / n);

         return (right, cases, Math.Max(0, centre - half), Math.Min(1, centre + half));
      }

      /// <summary>
      ///   Where two prompts' cacheable prefixes first differ, or -1 when one is the start of the other. A provider caches a
      ///   prompt's longest unchanged beginning: a change near the top makes every later token a fresh, full-price token.
      /// </summary>
      public static int FirstDivergence(string before, string now)
      {
         if (before == null || now == null) return -1;

         int n = Math.Min(before.Length, now.Length);
         for (var i = 0; i < n; i++)
            if (before[i] != now[i]) return i;

         return before.Length == now.Length ? -1 : n;
      }

      private static string Flags(bool unpriced, string note)
         => (unpriced ? " some calls unpriced, undercounts" : "") + (string.IsNullOrEmpty(note) ? "" : $" {note}");

      /// <summary>Dollars at a precision a reply's price needs: four decimals under a cent, two above a dollar.</summary>
      public static string Money(double dollars)
         => dollars >= 1 ? dollars.ToString("0.00", CultureInfo.InvariantCulture)
            : dollars >= 0.01 ? dollars.ToString("0.000", CultureInfo.InvariantCulture)
            : dollars.ToString("0.0000", CultureInfo.InvariantCulture);

      private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
   }
}
