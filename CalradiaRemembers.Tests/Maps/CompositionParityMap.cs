// Code written by Gabriel Mailhot, 25/10/2026.
// docs/maps/composition-parity.md: what each of the two readers of a conversation's actions is taught, side by side.
//
// Gabriel (25/10/2026): "il ne devrait pas avoir de differences entre [Enhanced et le mode regulier], car ce sont juste
// deux facons differentes d'interpreter les actions dans une conversation", and "tout devrait etre un seul chemin et la
// seule jonction devrait etre a quel service je demande l'interpretation". In Standard composition the prose model writes
// its own [ACTION] tags, taught by PromptBuilder; in Enhanced a second call, the interpreter, writes them, taught by
// ActionInterpreterPromptBuilder and ActionInterpreterContextBuilder. Nothing compared the two, and a trace found a tale's
// choices by words taught to the interpreter alone, so a player in Standard composition could never take one.
//
// THE CELLS ARE RENDERED, NOT READ. Both prompts are built here by the real builders, from one context
// (NpcMemoryServiceTests.PermissiveEncounter, compiled by link so the SDK's own test and this map share the subject), with
// the prose vocabulary the game builds (ProseActionVocabulary) and a tale's choices composed by the real
// AuthoredQuestPrompt. A verb counts as taught when the rendered prompt holds its `type: verb` block line or its
// `- verb:` list line, matched only against the names known from the catalogue, the prose vocabulary, the verb classes
// and the story-choice constant, so a tale's choice ids or a worked example never become rows.
//
// Every gate cannot be open in one context (the player held captive and the player holding a captive are two branches of
// the prompt), so both readers are rendered for each of PermissiveEncounter.Scenarios, and a verb counts as taught to a
// reader when any of them teaches it; the prose column names which.
//
// One column is READ FROM THE SOURCE instead, and says so: teaching text the engine-bound mod composes itself (the verb
// classes, and blocks such as the divorce notes, passed through EncounterContext strings) cannot be built here, so a verb
// the mod writes a `type:` line for is shown but not compared. The step after this one (ROADMAP, one path for actions)
// moves that teaching where it can be rendered here.

#region

using System.Text;
using System.Text.RegularExpressions;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.AuthoredQuests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryServiceTests;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   internal static class CompositionParityMap
   {
      public const string Path = "docs/maps/composition-parity.md";

      /// <summary>
      ///   The verbs taught to one reader and not the other, each with why it is allowed for now. This list may only
      ///   shrink: CompositionParityTests refuses a new asymmetry, and refuses an entry that is no longer one.
      /// </summary>
      public static readonly IReadOnlyDictionary<string, string> Debts = new SortedDictionary<string, string>(StringComparer.Ordinal) {
         ["story_choice"] = "A tale's choices by words are composed for the interpreter only (StoryChoicesForInterpreter), so in Standard "
                          + "composition the prose model is never told it may report one. Step 3 of 'one path for actions' teaches it to both.",
         ["gather_news"] = "A deprecated alias of dispatch_mission (GameActionCatalog), which the prose model is taught under that name; "
                         + "the interpreter's catalogue still lists the old name too.",
      };

      /// <summary>One verb, and where each reader is taught it.</summary>
      internal sealed class Row
      {
         public string Verb { get; init; } = "";
         public bool InterpreterFull { get; init; }
         public bool InterpreterCompact { get; init; }
         public bool ProseFull { get; init; }
         public bool ProseCompact { get; init; }
         public bool ProseNothingOpen { get; init; }
         public bool ModText { get; init; }

         /// <summary>The scenarios in which the prose model is taught it (Full).</summary>
         public string ProseScenarios { get; init; } = "";
         public bool Handled { get; init; }

         /// <summary>Compared only where both sides were rendered here (a mod-registry verb is taught by text this test cannot build).</summary>
         public bool Compared => !ModText;

         public bool Asymmetric => Compared && InterpreterFull != ProseFull;
      }

      public static string Generate(IReadOnlyList<SourceFile> files)
      {
         IReadOnlyList<Row> rows = Rows(files);

         var sb = new StringBuilder();
         sb.AppendLine("# Composition parity: what each reader of the actions is taught");
         sb.AppendLine();
         sb.AppendLine("GENERATED by the Maps fixtures in `CalradiaRemembers.Tests/Maps`. Do not edit. A test fails when this file");
         sb.AppendLine("no longer matches the code; see `docs/maps/README.md`.");
         sb.AppendLine();
         sb.AppendLine("Gabriel (25/10/2026): Enhanced and Standard are two ways of reading the actions in a conversation, and must not");
         sb.AppendLine("differ. In **Standard** the prose model writes its own `[ACTION]` tags (taught by `PromptBuilder`); in **Enhanced** a");
         sb.AppendLine("second call, the interpreter, writes them (taught by `ActionInterpreterPromptBuilder` and");
         sb.AppendLine("`ActionInterpreterContextBuilder`). Every cell below is RENDERED by those builders from one context, except the");
         sb.AppendLine("`Mod text` column, which is READ FROM THE SOURCE: a `type:` line the engine-bound mod writes itself (its verb classes,");
         sb.AppendLine("the divorce notes...) reaches the prose prompt through an `EncounterContext` string only the game can compose, so");
         sb.AppendLine("those rows are not compared.");
         sb.AppendLine();
         sb.AppendLine("- Both readers are rendered for each scenario of `PermissiveEncounter.Scenarios` (everything open for a free player;");
         sb.AppendLine("  the player held captive; the player's captor scene; a hostile lord holding the player's secret), at the Hardcore");
         sb.AppendLine("  adult level, with a tale holding a choice by");
         sb.AppendLine("  words open with this character; a verb is taught to a reader when any scenario teaches it.");
         sb.AppendLine("- `Interpreter`: its cached prefix and its per-turn facts, Full and Compact.");
         sb.AppendLine("- `Prose`: the prose model's prompt with the game's own vocabulary (`ProseActionVocabulary`), Full and Compact, the");
         sb.AppendLine("  scenarios that teach it, and a context where nothing is open. A verb taught in a scenario and not when nothing is");
         sb.AppendLine("  open is taught on condition; the interpreter is taught every verb every turn and the bridge refuses after.");
         sb.AppendLine("- `Bridge`: the bridge's handled list or a dispatch `case` names it.");
         sb.AppendLine("- `Parity`: `same` when both readers are taught it (or neither) in Full; otherwise which one, and the reason it is");
         sb.AppendLine("  allowed for now, from the list `CompositionParityTests` holds (it may only shrink).");
         sb.AppendLine();

         int asymmetric = rows.Count(r => r.Asymmetric), notCompared = rows.Count(r => !r.Compared);
         sb.AppendLine($"{rows.Count} verbs: {rows.Count - asymmetric - notCompared} the same for both readers, {asymmetric} taught to one reader only, "
                     + $"{notCompared} not compared (taught by the mod's own text).");
         sb.AppendLine();
         sb.AppendLine("| Verb | Interpreter | Interpreter, Compact | Prose | Prose, Compact | Prose taught in | Prose, nothing open | Mod text | Bridge | Parity |");
         sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
         foreach (Row r in rows)
            sb.AppendLine($"| `{r.Verb}` | {Mark(r.InterpreterFull)} | {Mark(r.InterpreterCompact)} | {Mark(r.ProseFull)} | {Mark(r.ProseCompact)} | "
                        + $"{r.ProseScenarios} | {Mark(r.ProseNothingOpen)} | {Mark(r.ModText)} | {Mark(r.Handled)} | {Parity(r)} |");
         sb.AppendLine();

         AppendAllowed(sb, rows);
         AppendContextFields(sb, files);
         AppendWhoReads(sb, files);

         return sb.ToString();
      }

      /// <summary>Every verb, rendered both ways. Also the guard's input.</summary>
      internal static IReadOnlyList<Row> Rows(IReadOnlyList<SourceFile> files)
      {
         SourceFile? bridge = VerbsMap.File(files, Layer.Mod, "GameState/BannerlordGameStateBridge.cs");
         HashSet<string> catalogue = VerbsMap.SpecNames(VerbsMap.File(files, Layer.Sdk, "Actions/GameActionCatalog.cs"));
         Dictionary<string, string> verbClasses = VerbsMap.VerbClasses(files);
         HashSet<string> modText = ModTypeLines(files);
         HashSet<string> handled = VerbsMap.LiteralsInField(bridge, "HandledActionTypes");
         handled.UnionWith(VerbsMap.CaseLabels(bridge));
         if (bridge?.Root.DescendantNodes().OfType<CaseSwitchLabelSyntax>().Any(l => l.Value.ToString().EndsWith(nameof(AuthoredQuestPrompt.StoryChoiceVerb), StringComparison.Ordinal)) == true)
            handled.Add(AuthoredQuestPrompt.StoryChoiceVerb);

         List<string> vocabulary = ProseActionVocabulary.Build(RelationWarmth.Normal).Select(d => d.Type).ToList();
         List<string> names = catalogue.Union(vocabulary).Union(verbClasses.Keys).Append(AuthoredQuestPrompt.StoryChoiceVerb)
                                       .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

         string choices = TaleChoices();
         var full = PermissiveEncounter.Scenarios(LeanPromptLevel.Full, choices);
         var compact = PermissiveEncounter.Scenarios(LeanPromptLevel.Lean, choices);
         List<string> interpreterFull = full.Select(s => Interpreter(s.Context, LeanPromptLevel.Full)).ToList();
         List<string> interpreterCompact = compact.Select(s => Interpreter(s.Context, LeanPromptLevel.Lean)).ToList();
         List<(string Name, string Prompt)> proseFull = full.Select(s => (s.Name, Prose(s.Context))).ToList();
         List<string> proseCompact = compact.Select(s => Prose(s.Context)).ToList();
         string proseNothing = Prose(new EncounterContext());

         return names.Select(n => new Row {
            Verb = n,
            InterpreterFull = interpreterFull.Any(p => Taught(p, n)),
            InterpreterCompact = interpreterCompact.Any(p => Taught(p, n)),
            ProseFull = proseFull.Any(p => Taught(p.Prompt, n)),
            ProseCompact = proseCompact.Any(p => Taught(p, n)),
            ProseScenarios = string.Join("; ", proseFull.Where(p => Taught(p.Prompt, n)).Select(p => p.Name)),
            ProseNothingOpen = Taught(proseNothing, n),
            ModText = modText.Contains(n),
            Handled = handled.Contains(n)
         }).ToList();
      }

      #region private

      private static string Mark(bool present) => present ? "yes" : "";

      private static string Parity(Row r)
      {
         if (!r.Compared) return "not compared";
         if (!r.Asymmetric) return "same";
         string side = r.InterpreterFull ? "interpreter only" : "prose only";
         return Debts.ContainsKey(r.Verb) ? side + " (allowed, below)" : "**" + side + "**";
      }

      // A teaching may be indented under its section (grant_blessing is), so leading blanks are allowed on both shapes.
      private static bool Taught(string prompt, string verb)
         => Regex.IsMatch(prompt, @"(?m)^[ \t]*type:[ \t]*" + Regex.Escape(verb) + @"[ \t]*\r?$")
            || Regex.IsMatch(prompt, @"(?m)^[ \t]*- " + Regex.Escape(verb) + ":");

      /// <summary>
      ///   The verbs the engine-bound mod writes a teaching line for itself (`"type: x"` as a whole string literal), outside
      ///   its diagnostics and cheats, which quote actions without teaching them.
      /// </summary>
      private static HashSet<string> ModTypeLines(IReadOnlyList<SourceFile> files)
         => files.Where(f => f.Layer == Layer.Mod && !f.RelativePath.StartsWith("Diagnostics/", StringComparison.Ordinal)
                             && !f.RelativePath.StartsWith("ModCheats", StringComparison.Ordinal))
                 .SelectMany(f => f.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
                 .Select(l => Regex.Match(l.Token.ValueText, @"^type:[ \t]*([a-z_]+)$"))
                 .Where(m => m.Success)
                 .Select(m => m.Groups[1].Value)
                 .ToHashSet();

      private static NpcProfile Subject() => new() {Id = "n", Name = "Test Lord", Faction = "Vlandia", Clan = "dey Meroc"};

      private static string Interpreter(EncounterContext context, LeanPromptLevel lean)
         => ActionInterpreterPromptBuilder.StablePrefixFor(lean) + "\n" + ActionInterpreterContextBuilder.Project(context, Subject(), "the player", true);

      private static string Prose(EncounterContext context)
         => new PromptBuilder {ActionVocabulary = ProseActionVocabulary.Build(RelationWarmth.Normal).ToList(), AdultLevel = AdultContentLevel.Hardcore, PlayerIsFemale = true}
            .BuildSystemPrompt(Subject(), new WorldState {CurrentDay = 10}, context);

      /// <summary>A tale held at a stage with a choice by words open with this character, composed by the real code.</summary>
      private static string TaleChoices()
      {
         AuthoredQuest q = AuthoredTales.Load(AuthoredTales.Fork.Replace("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says the boy is not worth the trouble\" }"));

         return AuthoredQuestPrompt.ChoicesForInterpreter(AuthoredTales.At(q, "the_road"), q, AuthoredTales.Giver);
      }

      private static void AppendAllowed(StringBuilder sb, IReadOnlyList<Row> rows)
      {
         sb.AppendLine("## Taught to one reader only, and allowed for now");
         sb.AppendLine();
         if (Debts.Count == 0) sb.AppendLine("Nothing: both readers are taught the same verbs.");
         foreach (KeyValuePair<string, string> debt in Debts)
            sb.AppendLine($"- `{debt.Key}`: {debt.Value}");
         sb.AppendLine();
      }

      /// <summary>
      ///   The context fields each side reads, by syntax (`context.X`). Read from the source: which fields TEACH an action
      ///   is not visible here, only which side can see them.
      /// </summary>
      private static void AppendContextFields(StringBuilder sb, IReadOnlyList<SourceFile> files)
      {
         HashSet<string> interpreter = ContextFields(files.Where(f => f.Layer == Layer.Sdk
                                                                       && (f.RelativePath == "Prompts/ActionInterpreterContextBuilder.cs"
                                                                           || f.RelativePath == "Prompts/ActionInterpreterPromptBuilder.cs")));
         HashSet<string> prose = ContextFields(files.Where(f => f.Layer == Layer.Sdk
                                                                 && (f.RelativePath == "Prompts/PromptBuilder.cs" || f.RelativePath == "Prompts/PromptBuilder.Sections.cs")));

         sb.AppendLine("## The context each reader can see (read from the source)");
         sb.AppendLine();
         sb.AppendLine("The interpreter's fields, and whether the prose prompt reads them too:");
         sb.AppendLine();
         sb.AppendLine("| Field | Prose reads it |");
         sb.AppendLine("|---|---|");
         foreach (string f in interpreter.OrderBy(f => f, StringComparer.Ordinal)) sb.AppendLine($"| `{f}` | {Mark(prose.Contains(f))} |");
         sb.AppendLine();

         List<string> gates = prose.Where(f => !interpreter.Contains(f) && IsTeachingGate(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
         sb.AppendLine($"Fields that open or close an action in the prose prompt and that the interpreter never sees ({gates.Count}; by name: `Can`,");
         sb.AppendLine("`Eligible`, `ExtraActionTeachings`, `UnavailableDeeds`): " + (gates.Count == 0 ? "none." : string.Join(", ", gates.Select(g => $"`{g}`")) + "."));
         sb.AppendLine();
      }

      private static bool IsTeachingGate(string field)
         => field.Contains("Can", StringComparison.Ordinal) && Regex.IsMatch(field, @"(^|[a-z])Can[A-Z]")
            || field.EndsWith("Eligible", StringComparison.Ordinal)
            || field == "ExtraActionTeachings" || field == "UnavailableDeeds";

      private static HashSet<string> ContextFields(IEnumerable<SourceFile> files)
         => files.SelectMany(f => f.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                 .Where(m => Unwrapped(m.Expression) == "context")
                 .Select(m => m.Name.Identifier.Text)
                 .Concat(files.SelectMany(f => f.Root.DescendantNodes().OfType<MemberBindingExpressionSyntax>())
                              .Where(b => b.Parent?.Parent is ConditionalAccessExpressionSyntax c && Unwrapped(c.Expression) == "context")
                              .Select(b => b.Name.Identifier.Text))
                 .Where(n => n.Length > 0 && char.IsUpper(n[0]))
                 .ToHashSet();

      private static string Unwrapped(ExpressionSyntax e)
         => e is PostfixUnaryExpressionSyntax p ? p.Operand.ToString() : e.ToString();

      /// <summary>The one condition that decides, today, which reader reads a reply's actions.</summary>
      private static void AppendWhoReads(StringBuilder sb, IReadOnlyList<SourceFile> files)
      {
         string? condition = files.Where(f => f.Layer == Layer.Mod && f.RelativePath == "Chat/ChatViewModel.Exchanges.cs")
                                  .SelectMany(f => f.Root.DescendantNodes())
                                  .Select(n => n switch {
                                     VariableDeclaratorSyntax v when v.Identifier.Text == "proseInterpretEligible" => v.Initializer?.Value.ToString(),
                                     AssignmentExpressionSyntax a when a.Left.ToString() == "proseInterpretEligible" => a.Right.ToString(),
                                     _ => null
                                  })
                                  .FirstOrDefault(s => s != null);

         sb.AppendLine("## Who reads a reply's actions (read from the source)");
         sb.AppendLine();
         sb.AppendLine("The interpreter reads them only when this holds (`ChatViewModel.Exchanges`, `proseInterpretEligible`); every other");
         sb.AppendLine("turn, even in Enhanced, is read from the prose model's own tags, by the same code as Standard:");
         sb.AppendLine();
         sb.AppendLine("```");
         sb.AppendLine(condition == null ? "(not found)" : Regex.Replace(condition, @"\s+", " "));
         sb.AppendLine("```");
         sb.AppendLine();
      }

      #endregion
   }
}
