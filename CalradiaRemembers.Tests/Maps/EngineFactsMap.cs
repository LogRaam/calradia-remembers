// Code written by Gabriel Mailhot, 28/09/2026.
// docs/maps/engine-facts.md: which fact of the game each pure rule reads, and which facts are read by more than one.
//
// Gabriel, 28/09/2026, after a rule of rank was added: "le quasi-doublon du rang, c'est les cartes qui t'ont fait voir ce
// detail?" Only partly. constants.md catalogues NAMES, and a duplicated CONCEPT hides under other names: rank was being
// compared as a "prestige score" (bastards), a "stature tier" (peace mediation), a "station" (duels), plain clan tiers
// (arranged matches, lord recruitment) and now a "rank" (correspondence), and a search found it only by guessing the
// word "tier". What these rules share is not a word but what they READ: Clan.Tier, every one of them.
//
// So this map follows the value, not the name. For every rule the mod feeds (an object initializer of a type declared in
// CalradiaRemembers.Logic, or a call into a static class declared there), it reads the expressions that fill it and
// lists the member chains they end on, keyed by owner and member ("Clan.Tier", "Hero.Spouse", "ModSettings.RelationWarmth").
// A value passed through a local, or through a helper method of the same type, is followed up to three steps.
//
// It reads syntax, not types, so a key is a best guess and says how it was made: a parameter or local with a declared
// type is keyed by that type ("Hero lord" gives "Hero.Spouse"), a property by its own name ("h.Clan.Tier" gives
// "Clan.Tier"), the player's own objects by their type ("Clan.PlayerClan.Tier" gives "Clan.Tier"), a capitalised name
// as the static type it is ("Kingdom.All"). A lambda's "h" or a var of unknown type is dropped rather than guessed. Tools
// (randomness, time, text, the names of traits), the mod's own services and singletons, and the model's reply are not
// facts and are left out; ModSettings stays, since a rule reads a setting the way it reads the world. A fact read under
// two spellings shows as two keys: the map's known blind spot.

#region

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   internal static class EngineFactsMap
   {
      public const string Path = "docs/maps/engine-facts.md";

      private const int MaxHops = 3;

      /// <summary>The player's own objects, keyed by their type so they meet the same fact read off anyone else.</summary>
      private static readonly Dictionary<string, string> PlayerAliases = new(StringComparer.Ordinal) {
         ["PlayerClan"] = "Clan", ["MainHero"] = "Hero", ["MainParty"] = "MobileParty", ["PlayerCharacter"] = "CharacterObject"
      };

      /// <summary>Members that are plumbing rather than facts: identity, text, and the collection verbs.</summary>
      private static readonly HashSet<string> Plumbing = new(StringComparer.Ordinal) {
         "ToString", "StringId", "Id", "Count", "Any", "All", "Where", "Select", "SelectMany", "FirstOrDefault", "First",
         "LastOrDefault", "Sum", "Max", "Min", "Contains", "Value", "HasValue", "Length", "Trim", "ToLowerInvariant",
         "ToUpperInvariant", "ToList", "ToArray", "OrderBy", "OrderByDescending", "Distinct", "GetValueOrDefault", "Equals",
         "TryGetValue", "Get", "Instance", "Current", "Now", "IsNullOrWhiteSpace", "IsNullOrEmpty", "Format", "Join",
         "MainHero", "PlayerClan", "MainParty", "PlayerCharacter", "FindFirst", "GetCampaignBehavior", "ToDays", "ToHours"
      };

      /// <summary>Owners that are tools, not facts: randomness, text, time, maths, and the names of traits and skills.</summary>
      private static readonly HashSet<string> Tools = new(StringComparer.Ordinal) {
         "MBRandom", "StringComparison", "Math", "MathF", "MBMath", "CampaignTime", "Now", "TextObject", "Convert", "Enum",
         "DefaultTraits", "DefaultSkills", "HeroField", "BattleSideEnum", "InformationManager", "Environment", "Path", "File",
         "Instance", "Response", "StringSplitOptions"
      };

      public static string Generate(IReadOnlyList<SourceFile> files)
      {
         HashSet<string> logicTypes = TypesDeclaredIn(files, Layer.Logic, staticOnly: false);
         // The SDK's own types (its enums, its models) are the mod's vocabulary, not facts of the game.
         // The mod's own services are not facts of the game either; its settings are, since a rule reads them the same way.
         HashSet<string> notEngine = new HashSet<string>(logicTypes.Concat(TypesDeclaredIn(files, Layer.Sdk, staticOnly: false))
                                                                  .Concat(TypesDeclaredIn(files, Layer.Mod, staticOnly: false))
                                                                  .Concat(EnumsDeclaredIn(files))
                                                                  .Concat(Tools), StringComparer.Ordinal);
         notEngine.Remove("ModSettings");
         HashSet<string> logicStatics = TypesDeclaredIn(files, Layer.Logic, staticOnly: true);
         // The console commands and cr.test all feed rules too, but to exercise them, not to play: they are left out.
         List<Read> reads = ReadsOf(files.Where(f => f.Layer == Layer.Mod && !IsDiagnostics(f)).ToList(), logicTypes, logicStatics, notEngine);

         var byFact = reads.GroupBy(r => r.Fact)
                           .Select(g => (Fact: g.Key, Rules: g.Select(r => r.Rule).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList()))
                           .ToList();
         var sb = new StringBuilder();

         sb.AppendLine("# Engine facts: which fact of the game each rule reads");
         sb.AppendLine();
         sb.AppendLine("GENERATED by the Maps fixtures in `CalradiaRemembers.Tests/Maps`. Do not edit. A test fails when this file");
         sb.AppendLine("no longer matches the code; see `docs/maps/README.md`.");
         sb.AppendLine();
         sb.AppendLine("A duplicated concept hides under other names (rank was a prestige score, a stature, a station and a tier), but");
         sb.AppendLine("it reads the same fact. This map follows the value from the game into every pure rule the mod feeds, and");
         sb.AppendLine("groups the rules by what they read. **Look here before adding a rule: if the fact it reads is already read by");
         sb.AppendLine("others, read theirs first.** Keys are read from syntax, not types (see the header of `EngineFactsMap.cs`).");
         sb.AppendLine();
         sb.AppendLine($"{reads.Select(r => r.Rule).Distinct().Count()} rules read {byFact.Count} facts.");
         sb.AppendLine();

         sb.AppendLine("## Facts read by more than one rule");
         sb.AppendLine();
         sb.AppendLine("| Fact | Read by |");
         sb.AppendLine("|---|---|");
         foreach ((string fact, List<string> rules) in byFact.Where(f => f.Rules.Count > 1)
                                                             .OrderByDescending(f => f.Rules.Count)
                                                             .ThenBy(f => f.Fact, StringComparer.Ordinal))
            sb.AppendLine($"| `{fact}` ({rules.Count}) | {string.Join(", ", rules.Select(r => $"`{r}`"))} |");

         sb.AppendLine();
         sb.AppendLine("## Every rule, and what it reads");
         sb.AppendLine();
         foreach (IGrouping<string, Read> rule in reads.GroupBy(r => r.Rule).OrderBy(g => g.Key, StringComparer.Ordinal))
            sb.AppendLine($"- `{rule.Key}`: {string.Join(", ", rule.Select(r => r.Fact).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(f => $"`{f}`"))}");

         sb.AppendLine();

         return sb.ToString();
      }

      #region private

      private sealed record Read(string Rule, string Fact);

      private static HashSet<string> TypesDeclaredIn(IReadOnlyList<SourceFile> files, Layer layer, bool staticOnly)
         => files.Where(f => f.Layer == layer)
                 .SelectMany(f => f.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                 .Where(t => !staticOnly || t.Modifiers.Any(m => m.Text == "static"))
                 .Select(t => t.Identifier.Text)
                 .ToHashSet(StringComparer.Ordinal);

      private static bool IsDiagnostics(SourceFile f)
         => ("/" + f.RelativePath).Contains("/Diagnostics/") || System.IO.Path.GetFileName(f.RelativePath).StartsWith("ModCheats", StringComparison.Ordinal);

      /// <summary>A helper this short is read whole (its conditions decide its answer); a longer one only for what it returns.</summary>
      private const int WholeHelperStatements = 12;

      private static HashSet<string> EnumsDeclaredIn(IReadOnlyList<SourceFile> files)
         => files.SelectMany(f => f.Root.DescendantNodes().OfType<EnumDeclarationSyntax>()).Select(e => e.Identifier.Text).ToHashSet(StringComparer.Ordinal);

      // Every part of every mod type, by name, so a helper in another file of a partial class is still found.
      private static Dictionary<string, List<TypeDeclarationSyntax>> _typeParts = new();

      private static List<Read> ReadsOf(List<SourceFile> modFiles, HashSet<string> logicTypes, HashSet<string> logicStatics, HashSet<string> notEngine)
      {
         var reads = new List<Read>();
         _typeParts = modFiles.SelectMany(f => f.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                              .GroupBy(t => t.Identifier.Text)
                              .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

         foreach (SourceFile file in modFiles)
         {
            // A rule fed through an object initializer: new SomeFacts { A = ..., B = ... }.
            foreach (BaseObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
               string type = creation is ObjectCreationExpressionSyntax o ? LastName(o.Type) : null;
               if (type == null || !logicTypes.Contains(type) || creation.Initializer == null) continue;

               foreach (AssignmentExpressionSyntax assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                  foreach (string fact in FactsIn(assign.Right, notEngine, 0))
                     reads.Add(new Read(type, fact));
            }

            // A rule fed through a call: SomePolicy.Decide(a, b).
            foreach (InvocationExpressionSyntax call in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
               if (call.Expression is not MemberAccessExpressionSyntax {Expression: IdentifierNameSyntax owner} member) continue;
               if (!logicStatics.Contains(owner.Identifier.Text)) continue;

               string rule = $"{owner.Identifier.Text}.{member.Name.Identifier.Text}";
               foreach (ArgumentSyntax arg in call.ArgumentList.Arguments)
               {
                  if (arg.Expression is BaseObjectCreationExpressionSyntax) continue; // counted as an initializer above
                  foreach (string fact in FactsIn(arg.Expression, notEngine, 0))
                     reads.Add(new Read(rule, fact));
               }
            }
         }

         return reads.Distinct().ToList();
      }

      /// <summary>The facts an expression ends on, following locals and same-type helpers up to <see cref="MaxHops" /> steps.</summary>
      private static IEnumerable<string> FactsIn(ExpressionSyntax expression, HashSet<string> logicTypes, int hop)
      {
         if (expression == null) yield break;

         // A mod READER is where a fact is read from the game (StatureReader.Of, StatureReader.Player): the chain is keyed
         // by the reader and what it was asked, whatever is taken from the answer, so every rule reading rank meets under
         // one key.
         foreach (MemberAccessExpressionSyntax access in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
         {
            string reader = LastIdentifier(access.Expression);
            if (reader != null && IsReader(reader)) yield return $"{reader}.{access.Name.Identifier.Text}";
         }

         // The LAST link of each chain only: a.B.C and a?.B?.C both give B.C, never also a.B.
         foreach (MemberAccessExpressionSyntax access in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
         {
            if (IsInnerLink(access) || ChainHasReader(access)) continue;

            string fact = Key(access.Expression, access.Name.Identifier.Text, access, logicTypes);
            if (fact != null) yield return fact;
         }

         foreach (MemberBindingExpressionSyntax binding in expression.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>())
         {
            if (IsInnerLink(binding)) continue;

            // "x?.Y": the receiver is the conditional access's own expression; "x?.Y()" wraps the binding in a call.
            SyntaxNode link = binding.Parent is InvocationExpressionSyntax call && call.Expression == binding ? call : binding;
            ExpressionSyntax receiver = link.Parent is ConditionalAccessExpressionSyntax ca && ca.WhenNotNull == link ? ca.Expression : null;
            string fact = Key(receiver, binding.Name.Identifier.Text, binding, logicTypes);
            if (fact != null) yield return fact;
         }

         if (hop >= MaxHops) yield break;

         // Follow a local to its initializer, and a helper method of the same type to what it returns.
         foreach (IdentifierNameSyntax id in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
         {
            if (id.Parent is MemberAccessExpressionSyntax m && m.Name == id) continue;

            VariableDeclaratorSyntax local = id.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault()?
                                               .DescendantNodes().OfType<VariableDeclaratorSyntax>()
                                               .FirstOrDefault(v => v.Identifier.Text == id.Identifier.Text && v.Initializer != null);
            if (local != null && !local.Span.Contains(id.Span))
               foreach (string f in FactsIn(local.Initializer!.Value, logicTypes, hop + 1)) yield return f;
         }

         foreach (InvocationExpressionSyntax call in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
         {
            if (call.Expression is not IdentifierNameSyntax name) continue;

            string typeName = call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
            MethodDeclarationSyntax helper = typeName != null && _typeParts.TryGetValue(typeName, out List<TypeDeclarationSyntax> parts)
               ? parts.SelectMany(t => t.Members.OfType<MethodDeclarationSyntax>()).FirstOrDefault(md => md.Identifier.Text == name.Identifier.Text)
               : null;
            if (helper == null) continue;

            // The whole body, not only what it returns: "if (tier >= 4) return 2;" reads the tier in its condition.
            // A long helper does many things, and reading it whole would credit the rule with all of them.
            int statements = helper.Body?.DescendantNodes().OfType<StatementSyntax>().Count() ?? 0;
            IEnumerable<ExpressionSyntax> body = helper.ExpressionBody != null
               ? new[] {helper.ExpressionBody.Expression}
               : statements <= WholeHelperStatements
                  ? helper.Body?.DescendantNodes().OfType<ExpressionSyntax>().Where(e => e.Parent is not ExpressionSyntax) ?? Enumerable.Empty<ExpressionSyntax>()
                  : helper.DescendantNodes().OfType<ReturnStatementSyntax>().Select(r => r.Expression).Where(e => e != null)!;
            foreach (ExpressionSyntax part in body)
               foreach (string f in FactsIn(part, logicTypes, hop + 1)) yield return f;
         }
      }

      private static bool IsReader(string name) => name.Length > "Reader".Length && name.EndsWith("Reader", StringComparison.Ordinal) && char.IsUpper(name[0]);

      private static string LastIdentifier(ExpressionSyntax e)
         => e switch {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            _ => null
         };

      /// <summary>Whether the chain this link ends passes through a reader (it is then keyed by the reader instead).</summary>
      private static bool ChainHasReader(ExpressionSyntax link)
         => link.DescendantNodes().OfType<SimpleNameSyntax>().Any(n => IsReader(n.Identifier.Text));

      /// <summary>A link that another link of the same chain continues from (the "B" of "a.B.C" or of "a?.B?.C").</summary>
      private static bool IsInnerLink(ExpressionSyntax link)
      {
         SyntaxNode node = link.Parent is InvocationExpressionSyntax call && call.Expression == link ? call : link;

         return node.Parent switch
         {
            MemberAccessExpressionSyntax outer => outer.Expression == node,
            ConditionalAccessExpressionSyntax ca => ca.Expression == node,
            _ => false
         };
      }

      /// <summary>"Owner.Member" for one link of a chain, or null when it is plumbing or one of the rules' own types.</summary>
      private static string Key(ExpressionSyntax receiver, string member, SyntaxNode at, HashSet<string> logicTypes)
      {
         if (Plumbing.Contains(member)) return null;

         string owner = OwnerOf(receiver, at);
         if (owner == null || logicTypes.Contains(owner) || owner is "Math" or "MathF" or "string" or "String" or "Enumerable") return null;
         if (PlayerAliases.TryGetValue(owner, out string alias)) owner = alias;

         return $"{owner}.{member}";
      }

      private static string OwnerOf(ExpressionSyntax receiver, SyntaxNode at)
      {
         switch (receiver)
         {
            case null:
               return null;
            case MemberAccessExpressionSyntax m:
               return m.Name.Identifier.Text;
            case MemberBindingExpressionSyntax b:
               return b.Name.Identifier.Text;
            case ConditionalAccessExpressionSyntax c:
               return OwnerOf(c.WhenNotNull, at);
            case InvocationExpressionSyntax:
            case ThisExpressionSyntax:
               return null;
            case IdentifierNameSyntax id:
               string name = id.Identifier.Text;
               string declared = DeclaredTypeOf(name, at);
               if (declared != null) return declared;

               // A capitalised name is a type read statically (Hero, Kingdom, ModSettings). A lowercase one whose type
               // the syntax does not give (a lambda's "h", a var) is dropped rather than guessed.
               return name.Length > 0 && char.IsUpper(name[0]) ? name : null;
            default:
               return null;
         }
      }

      /// <summary>The declared type of a parameter or explicitly typed local named <paramref name="name" /> in the enclosing member.</summary>
      private static string DeclaredTypeOf(string name, SyntaxNode at)
      {
         MemberDeclarationSyntax member = at.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(m => m is BaseMethodDeclarationSyntax or PropertyDeclarationSyntax);
         if (member == null) return null;

         ParameterSyntax parameter = member.DescendantNodes().OfType<ParameterSyntax>().FirstOrDefault(p => p.Identifier.Text == name && p.Type != null);
         if (parameter != null) return LastName(parameter.Type);

         VariableDeclarationSyntax local = member.DescendantNodes().OfType<VariableDeclarationSyntax>()
                                                 .FirstOrDefault(d => !d.Type.IsVar && d.Variables.Any(v => v.Identifier.Text == name));

         return local == null ? null : LastName(local.Type);
      }

      private static string LastName(TypeSyntax type)
      {
         switch (type)
         {
            case NullableTypeSyntax n:
               return LastName(n.ElementType);
            case QualifiedNameSyntax q:
               return q.Right.Identifier.Text;
            case SimpleNameSyntax s:
               return s.Identifier.Text;
            case PredefinedTypeSyntax p:
               return p.Keyword.Text;
            default:
               return type?.ToString();
         }
      }

      #endregion
   }
}
