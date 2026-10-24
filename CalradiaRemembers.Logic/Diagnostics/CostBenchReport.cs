// Code written by Gabriel Mailhot, 24/10/2026.
// What the mod costs to play, measured, per model and per setup. fearente (Nexus, 24/10/2026): "I've already spent
// around $3.19 without particularly heavy usage. I know you've mentioned that heavy usage can cost around $1 per
// week". That figure was measured once, on Grok 4.20, before the prompt grew and before a reply became two calls; the
// rest of the page's prices were extrapolated by price ratio. Gabriel (24/10/2026): a script that tests several
// scenarios of models and MCM setups, and a table in the description naming the mod version it was measured on.
// cr.cost_bench plays one scripted conversation and one letter per scenario through the real pipeline and prices every
// call from OpenRouter's own bill; this is the part that needs no game: the scenario file, the sums and the tables.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Which kind of call a billed attempt belonged to.</summary>
   public enum CostPhase
   {
      /// <summary>The character's reply (in Standard it carries its own tags).</summary>
      Prose,

      /// <summary>Enhanced only: the interpret model reading the reply for its actions.</summary>
      Interpreter,

      /// <summary>Enhanced only: the reply redone on the fallback model after a cut or a refusal.</summary>
      Fallback,

      /// <summary>A letter the character writes unbidden (the prose model, the full prompt).</summary>
      Letter
   }

   /// <summary>One setup to measure: the models and the MCM choices a player makes.</summary>
   public sealed class CostScenario
   {
      /// <summary>The row's name in the table.</summary>
      public string Name { get; init; } = "";

      /// <summary>MCM "PROSE model": writes every reply and every letter.</summary>
      public string ProseModel { get; init; } = "";

      /// <summary>MCM "Interpret model": read only in Enhanced.</summary>
      public string InterpreterModel { get; init; } = "";

      /// <summary>MCM "Chat Composition": Enhanced (two calls a reply) or Standard (one).</summary>
      public bool Enhanced { get; init; } = true;

      /// <summary>MCM "Compact prompt for small models".</summary>
      public bool Compact { get; init; }

      /// <summary>MCM "Reasoning Effort (PROSE)" for this row; blank keeps the player's own.</summary>
      public string Reasoning { get; init; } = "";

      /// <summary>
      ///   MCM "NSFW model": in Enhanced it redoes a reply the prose model refused or cut short (the fallback), so it is
      ///   billed only when that happens. Blank keeps the player's own.
      /// </summary>
      public string NsfwModel { get; init; } = "";

      /// <summary>How the row's setup reads in the table: "Enhanced, full prompt, reasoning Low".</summary>
      public string Setup
         => $"{(Enhanced ? "Enhanced" : "Standard")}, {(Compact ? "Compact prompt" : "full prompt")}"
            + (string.IsNullOrWhiteSpace(Reasoning) ? "" : $", reasoning {Reasoning}")
            + (string.IsNullOrWhiteSpace(NsfwModel) || !Enhanced ? "" : $", fallback {NsfwModel}");

      /// <summary>The models as they read in the table: one name when both roles share it.</summary>
      public string Models
         => !Enhanced || string.Equals(ProseModel, InterpreterModel, StringComparison.OrdinalIgnoreCase)
            ? ProseModel
            : $"{ProseModel} + {InterpreterModel}";
   }

   /// <summary>One billed attempt, as OpenRouter reported it.</summary>
   public sealed class CostCall
   {
      /// <summary>The scenario's name.</summary>
      public string ScenarioName { get; init; } = "";

      /// <summary>Which call it was.</summary>
      public CostPhase Phase { get; init; }

      /// <summary>The player's line it answered, from 1; 0 for the letter.</summary>
      public int Turn { get; init; }

      /// <summary>The model the attempt went to.</summary>
      public string Model { get; init; } = "";

      /// <summary>Tokens sent.</summary>
      public int PromptTokens { get; init; }

      /// <summary>Of those, read from the provider's cache (billed far below the input rate).</summary>
      public int CachedTokens { get; init; }

      /// <summary>Tokens written, reasoning included.</summary>
      public int CompletionTokens { get; init; }

      /// <summary>Of those, spent thinking: billed, never shown.</summary>
      public int ReasoningTokens { get; init; }

      /// <summary>The bill in US dollars; null when the provider did not say (the row is then marked unpriced).</summary>
      public double? Cost { get; init; }
   }

   /// <summary>One scenario's measured costs.</summary>
   public sealed class CostScenarioResult
   {
      /// <summary>The scenario measured.</summary>
      public CostScenario Row { get; init; } = new CostScenario();

      /// <summary>The first reply of a conversation: nothing is in the provider's cache yet.</summary>
      public double FirstReply { get; init; }

      /// <summary>The average of every later reply, the price of an ordinary turn.</summary>
      public double LaterReply { get; init; }

      /// <summary>
      ///   Of a later reply, what the PROSE call cost (with the fallback when it redid the reply). Gabriel (24/10/2026):
      ///   the prose and the interpreter do different work on different prompts, so each is priced on its own.
      /// </summary>
      public double ProsePart { get; init; }

      /// <summary>Of a later reply, what the INTERPRET call cost; zero in Standard, where there is none.</summary>
      public double InterpretPart { get; init; }

      /// <summary>Of the PROSE calls' written tokens, the share spent thinking (0 to 1).</summary>
      public double ProseReasoningShare { get; init; }

      /// <summary>Of the INTERPRET calls' written tokens, the share spent thinking (0 to 1).</summary>
      public double InterpretReasoningShare { get; init; }

      /// <summary>One letter; null when none was written.</summary>
      public double? Letter { get; init; }

      /// <summary>Everything the scenario spent.</summary>
      public double Total { get; init; }

      /// <summary>Replies answered.</summary>
      public int Replies { get; init; }

      /// <summary>Of all tokens written, the share spent thinking (0 to 1).</summary>
      public double ReasoningShare { get; init; }

      /// <summary>Of all tokens sent, the share read from cache (0 to 1).</summary>
      public double CachedShare { get; init; }

      /// <summary>True when some attempt came back without a price: the row then undercounts and says so.</summary>
      public bool Unpriced { get; init; }

      /// <summary>What went wrong, if anything (a refused model id, a cap reached).</summary>
      public string Note { get; init; } = "";

      /// <summary>A hundred replies in conversations of ten: ten first replies and ninety later ones, plus nothing else.</summary>
      public double HundredReplies => 10 * FirstReply + 90 * LaterReply;
   }

   /// <summary>The scenario file: one row per line, edited by hand. Pure.</summary>
   public static class CostScenarioFile
   {
      /// <summary>The default rows: the default model three ways, then the models players asked about (fearente, 24/10/2026).</summary>
      public static IReadOnlyList<CostScenario> Defaults { get; } = new[]
      {
         Row("Grok 4.20 (default)", "x-ai/grok-4.20", "x-ai/grok-4.20", true, false),
         Row("Grok 4.20, Standard", "x-ai/grok-4.20", "x-ai/grok-4.20", false, false),
         Row("Grok 4.20, Compact", "x-ai/grok-4.20", "x-ai/grok-4.20", true, true),
         Row("Gemini 3.8 Flash", "google/gemini-3.8-flash", "google/gemini-3.8-flash", true, false),
         Row("DeepSeek V4 Flash", "deepseek/deepseek-v4-flash", "deepseek/deepseek-v4-flash", true, false),
         Row("GPT-6 Luna", "openai/gpt-6-luna", "openai/gpt-6-luna", true, false),
         Row("Gemma 4 31B", "google/gemma-4-31b-it", "google/gemma-4-31b-it", true, false),
         Row("DeepSeek prose, Grok interprets", "deepseek/deepseek-v4-flash", "x-ai/grok-4.20", true, false),
         // Gabriel (24/10/2026): GLM 4.6 writing, Grok 4.20 interpreting and standing by as the NSFW fallback.
         Row("GLM 4.6 prose, Grok interprets and NSFW", "z-ai/glm-4.6", "x-ai/grok-4.20", true, false, "x-ai/grok-4.20")
      };

      /// <summary>The file written when none exists: a header that teaches the format, then the defaults.</summary>
      public static string Template()
      {
         var sb = new StringBuilder();
         sb.AppendLine("# cr.cost_bench scenarios. One per line:");
         sb.AppendLine("#   name | prose model | interpret model | Enhanced or Standard | Full or Compact | reasoning | NSFW model");
         sb.AppendLine("# A blank reasoning or NSFW model keeps yours. The NSFW model is billed only when it redoes a refused or cut reply.");
         sb.AppendLine("# Model ids are OpenRouter's. Lines starting with # are ignored. cr.cost_bench plan shows what a run would do.");
         foreach (CostScenario s in Defaults)
            sb.AppendLine($"{s.Name} | {s.ProseModel} | {s.InterpreterModel} | {(s.Enhanced ? "Enhanced" : "Standard")} | {(s.Compact ? "Compact" : "Full")} | {s.Reasoning} | {s.NsfwModel}");

         return sb.ToString();
      }

      /// <summary>
      ///   The rows of <paramref name="text" />. A line that cannot be read is skipped and named in
      ///   <paramref name="problems" />, so one typo never silently drops a model from the table.
      /// </summary>
      public static IReadOnlyList<CostScenario> Parse(string text, out List<string> problems)
      {
         problems = new List<string>();
         var rows = new List<CostScenario>();
         if (string.IsNullOrWhiteSpace(text)) return rows;

         string[] lines = text.Replace("\r\n", "\n").Split('\n');
         for (var i = 0; i < lines.Length; i++)
         {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            string[] f = line.Split('|').Select(p => p.Trim()).ToArray();
            if (f.Length < 5 || f[0].Length == 0 || f[1].Length == 0)
            {
               problems.Add($"line {i + 1}: expected 'name | prose model | interpret model | Enhanced or Standard | Full or Compact | reasoning'");
               continue;
            }

            bool? enhanced = Word(f[3], "enhanced", "standard");
            bool? compact = Word(f[4], "compact", "full");
            if (enhanced == null || compact == null)
            {
               problems.Add($"line {i + 1}: composition must be Enhanced or Standard, prompt Full or Compact");
               continue;
            }

            rows.Add(new CostScenario {
               Name = f[0], ProseModel = f[1], InterpreterModel = f[2].Length == 0 ? f[1] : f[2], Enhanced = enhanced.Value,
               Compact = compact.Value, Reasoning = f.Length > 5 ? f[5] : "", NsfwModel = f.Length > 6 ? f[6] : ""
            });
         }

         return rows;
      }

      private static bool? Word(string value, string yes, string no)
         => string.Equals(value, yes, StringComparison.OrdinalIgnoreCase) ? true
            : string.Equals(value, no, StringComparison.OrdinalIgnoreCase) ? false
            : (bool?) null;

      private static CostScenario Row(string name, string prose, string interpreter, bool enhanced, bool compact, string nsfw = "")
         => new CostScenario {Name = name, ProseModel = prose, InterpreterModel = interpreter, Enhanced = enhanced, Compact = compact, NsfwModel = nsfw};
   }

   /// <summary>The sums and the tables. Pure.</summary>
   public static class CostBenchReport
   {
      /// <summary>Whether a run must stop: the spend has reached the cap (a cap of zero or less means none).</summary>
      public static bool CapReached(double spent, double cap) => cap > 0 && spent >= cap;

      /// <summary>One scenario's costs from its billed attempts.</summary>
      public static CostScenarioResult Summarize(CostScenario scenario, IReadOnlyList<CostCall> calls, string note = "")
      {
         List<CostCall> mine = (calls ?? new List<CostCall>()).Where(c => c.ScenarioName == scenario.Name).ToList();
         List<double> perTurn = mine.Where(c => c.Turn > 0).GroupBy(c => c.Turn).OrderBy(g => g.Key)
                                    .Select(g => g.Sum(c => c.Cost ?? 0)).ToList();
         List<CostCall> letter = mine.Where(c => c.Phase == CostPhase.Letter).ToList();
         int sent = mine.Sum(c => c.PromptTokens);
         int written = mine.Sum(c => c.CompletionTokens);
         List<CostCall> later = mine.Where(c => c.Turn > 1).ToList();
         int laterTurns = later.Select(c => c.Turn).Distinct().Count();
         List<CostCall> prose = mine.Where(c => c.Phase == CostPhase.Prose || c.Phase == CostPhase.Fallback).ToList();
         List<CostCall> interpret = mine.Where(c => c.Phase == CostPhase.Interpreter).ToList();

         return new CostScenarioResult {
            Row = scenario,
            FirstReply = perTurn.Count > 0 ? perTurn[0] : 0,
            LaterReply = perTurn.Count > 1 ? perTurn.Skip(1).Average() : perTurn.Count == 1 ? perTurn[0] : 0,
            Letter = letter.Count > 0 ? letter.Sum(c => c.Cost ?? 0) : (double?) null,
            Total = mine.Sum(c => c.Cost ?? 0),
            Replies = perTurn.Count,
            ProsePart = laterTurns > 0 ? later.Where(c => c.Phase == CostPhase.Prose || c.Phase == CostPhase.Fallback).Sum(c => c.Cost ?? 0) / laterTurns : 0,
            InterpretPart = laterTurns > 0 ? later.Where(c => c.Phase == CostPhase.Interpreter).Sum(c => c.Cost ?? 0) / laterTurns : 0,
            ProseReasoningShare = Share(prose.Sum(c => c.ReasoningTokens), prose.Sum(c => c.CompletionTokens)),
            InterpretReasoningShare = Share(interpret.Sum(c => c.ReasoningTokens), interpret.Sum(c => c.CompletionTokens)),
            ReasoningShare = written > 0 ? (double) mine.Sum(c => c.ReasoningTokens) / written : 0,
            CachedShare = sent > 0 ? (double) mine.Sum(c => c.CachedTokens) / sent : 0,
            Unpriced = mine.Any(c => c.Cost == null),
            Note = note ?? ""
         };
      }

      private static double Share(int part, int whole) => whole > 0 ? (double) part / whole : 0;

      /// <summary>What a run would do, with no call made.</summary>
      public static string Plan(IReadOnlyList<CostScenario> scenarios, int turns, bool letter, double cap)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"cr.cost_bench plan: {scenarios.Count} scenario(s), {turns} replies{(letter ? " and one letter" : "")} each, spend cap ${Money(cap)}.");
         foreach (CostScenario s in scenarios)
         {
            int calls = turns * (s.Enhanced ? 2 : 1) + (letter ? 1 : 0);
            sb.AppendLine($"  {s.Name}: {s.Models}; {s.Setup}; about {calls} calls");
         }

         return sb.ToString();
      }

      /// <summary>The table for the log and the repository (Markdown).</summary>
      public static string Markdown(IReadOnlyList<CostScenarioResult> results, string version, DateTime measuredOn)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"Measured with Calradia Remembers {version} on {measuredOn:yyyy-MM-dd}, OpenRouter's own bill for each call.");
         sb.AppendLine();
         sb.AppendLine("| Model | Setup | First reply | Each later reply | of which PROSE | of which INTERPRET | A letter | 100 replies | Thinking (prose / interpret) | From cache |");
         sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
         foreach (CostScenarioResult r in results)
            sb.AppendLine($"| {r.Row.Models} | {r.Row.Setup} | ${Money(r.FirstReply)} | ${Money(r.LaterReply)} | ${Money(r.ProsePart)} | {InterpretText(r)} | {LetterText(r)} | ${Money(r.HundredReplies)} | {Percent(r.ProseReasoningShare)} / {(r.Row.Enhanced ? Percent(r.InterpretReasoningShare) : "-")} | {Percent(r.CachedShare)} |{Flags(r)}");

         return sb.ToString();
      }

      /// <summary>
      ///   The table for the Nexus description, as a [code] block: monospace keeps the columns aligned whatever the page's
      ///   bbcode supports.
      /// </summary>
      public static string NexusCode(IReadOnlyList<CostScenarioResult> results, string version, DateTime measuredOn)
      {
         var rows = new List<string[]> {new[] {"Model", "Setup", "1st reply", "Later reply", "Prose", "Interpret", "Letter", "100 replies"}};
         rows.AddRange(results.Select(r => new[] {
            r.Row.Name, r.Row.Setup, "$" + Money(r.FirstReply), "$" + Money(r.LaterReply), "$" + Money(r.ProsePart), InterpretText(r), LetterText(r),
            "$" + Money(r.HundredReplies)
         }));
         int[] widths = Enumerable.Range(0, rows[0].Length).Select(i => rows.Max(row => row[i].Length)).ToArray();

         var sb = new StringBuilder();
         sb.AppendLine($"[b]Measured costs[/b] (Calradia Remembers {version}, {measuredOn:yyyy-MM-dd}, OpenRouter's own bill for each call)");
         sb.AppendLine("[code]");
         foreach (string[] row in rows)
            sb.AppendLine(string.Join("  ", row.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd());
         sb.AppendLine("[/code]");

         return sb.ToString();
      }

      private static string InterpretText(CostScenarioResult r) => r.Row.Enhanced ? "$" + Money(r.InterpretPart) : "-";

      private static string LetterText(CostScenarioResult r) => r.Letter == null ? "-" : "$" + Money(r.Letter.Value);

      private static string Flags(CostScenarioResult r)
         => (r.Unpriced ? " some calls unpriced, undercounts" : "") + (string.IsNullOrEmpty(r.Note) ? "" : $" {r.Note}");

      /// <summary>Dollars at a precision a reply's price needs: four decimals under a cent, two above a dollar.</summary>
      public static string Money(double dollars)
         => dollars >= 1 ? dollars.ToString("0.00", CultureInfo.InvariantCulture)
            : dollars >= 0.01 ? dollars.ToString("0.000", CultureInfo.InvariantCulture)
            : dollars.ToString("0.0000", CultureInfo.InvariantCulture);

      private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
   }
}
