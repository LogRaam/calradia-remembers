// Code written by Gabriel Mailhot, 05/10/2026.
// The contract between what the interpreter is TAUGHT an action carries and what the game READS from it.
//
// fkasad (Nexus, 05/10/2026): "Take those ten ingots of Gromril", thanked for ten, one moved. The bridge read a
// "count" parameter and its policy (GiftQuantityPolicy) was right and tested; the SDK catalogue that teaches the model
// declared only "item". Every piece was correct on its own, so no test of a piece could see it: the bug lived between
// the two. Gabriel asked (05/10/2026) for the test that would have caught it. This is that test, for every action:
//
//   - a parameter the game reads must be declared in the catalogue, or the model is never told to send it and the
//     game silently falls back (one ingot instead of ten);
//   - a parameter the catalogue declares must be read by the game, or the model is taught to fill a field that does
//     nothing, and the player is promised what never happens.
//
// What is read is found the way the game reads it: action.Parameters.TryGetValue("x", ...) and helpers called as
// Helper(action.Parameters, "x", ...), inside the executor the bridge's switch dispatches to (following the bridge's
// own methods it calls) or inside the IConversationVerb class whose Name is the action. An action handled elsewhere
// (the chat reads challenge_duel's "spirit") is listed below with the reason, never silently skipped.

#region

using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class ActionParameterContractTests
   {
      // A parameter read outside the executors this test can follow, by action and name, with where it is read.
      // Each entry is a decision someone made; a new entry needs its reason.
      private static readonly Dictionary<(string Action, string Param), string> ReadElsewhere = new()
      {
      };

      // A parameter the game reads on purpose without teaching it (an internal or host-built action), with why.
      private static readonly Dictionary<(string Action, string Param), string> ReadButNotTaught = new()
      {
         // Set only by EnvoyMandatePolicy.Bind when the player agreed to give the captive away (fkasad, 26/09/2026).
         // Taught to the model, it could turn any sale into a free one.
         [("buy_prisoner", "gift")] = "host-built by the envoy's binder, never the model's to send",
      };

      // fkasad's bug, for every action: the game reads a field the model is never told about.
      [Test]
      public void GIVEN_every_action_WHEN_the_game_reads_a_parameter_THEN_the_interpreter_is_taught_it()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         Contract c = Contract.From(SourceTree.Production);
         var missing = new List<string>();
         foreach ((string action, HashSet<string> read) in c.Read)
         {
            if (!c.Declared.TryGetValue(action, out HashSet<string>? declared)) continue; // not taught at all: the verbs map shows it
            foreach (string p in read.Where(p => !declared.Contains(p) && !ReadButNotTaught.ContainsKey((action, p))))
               missing.Add($"{action}.{p} (read in {c.ReadIn[action]})");
         }

         missing.Should().BeEmpty("the model is never told to send these, so the game falls back on its default (one ingot instead of ten)");
      }

      // The other half: the model is taught a field the game never reads, so what it promises never happens.
      [Test]
      public void GIVEN_every_action_WHEN_the_catalogue_declares_a_parameter_THEN_the_game_reads_it()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         Contract c = Contract.From(SourceTree.Production);
         var unread = new List<string>();
         foreach ((string action, HashSet<string> declared) in c.Declared)
         {
            if (!c.Read.TryGetValue(action, out HashSet<string>? read)) continue; // no executor this test can follow: the verbs map shows it
            foreach (string p in declared.Where(p => !read.Contains(p) && !ReadElsewhere.ContainsKey((action, p))))
               unread.Add($"{action}.{p} (executor {c.ReadIn[action]})");
         }

         unread.Should().BeEmpty("the model is taught to fill these and nothing in the game reads them");
      }

      // The guard must not go blind: if the parser stops recognising the code, both tests above pass vacuously.
      [Test]
      public void GIVEN_the_source_WHEN_the_contract_is_read_THEN_the_known_cases_are_found()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         Contract c = Contract.From(SourceTree.Production);

         c.Declared.Should().ContainKey("give_item");
         c.Declared["give_item"].Should().Contain(new[] {"item", "count"});
         c.Read.Should().ContainKey("give_item", "the bridge's switch dispatches give_item to ExecuteGiveItem");
         c.Read["give_item"].Should().Contain(new[] {"item", "count"});
         c.Read.Should().ContainKey("sell_prisoner", "a verb class (SellPrisonerVerb) is followed too");
         c.Read["sell_prisoner"].Should().Contain("target");
         c.Read["take_into_service"].Should().Contain("target", "read as action?.Parameters?.TryGetValue(\"target\", ...)");
         c.Read.Count.Should().BeGreaterThan(20, "most actions have an executor this test can follow");
      }

      #region reading the contract

      private sealed class Contract
      {
         public Dictionary<string, HashSet<string>> Declared { get; } = new(StringComparer.Ordinal);
         public Dictionary<string, HashSet<string>> Read { get; } = new(StringComparer.Ordinal);
         public Dictionary<string, string> ReadIn { get; } = new(StringComparer.Ordinal);

         public static Contract From(IReadOnlyList<SourceFile> files)
         {
            var c = new Contract();

            SourceFile? catalogue = files.FirstOrDefault(f => f.Layer == Layer.Sdk && f.RelativePath == "Actions/GameActionCatalog.cs");
            foreach (InvocationExpressionSyntax spec in catalogue?.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                                                        .Where(i => i.Expression is IdentifierNameSyntax {Identifier.Text: "Spec"})
                                                        ?? Enumerable.Empty<InvocationExpressionSyntax>())
            {
               string? type = Literal(spec.ArgumentList.Arguments.FirstOrDefault()?.Expression);
               if (type == null) continue;
               c.Declared[type] = spec.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                                      .Where(o => o.Type.ToString() == "GameActionParam")
                                      .Select(o => Literal(o.ArgumentList?.Arguments.FirstOrDefault()?.Expression))
                                      .Where(n => n != null).Select(n => n!).ToHashSet(StringComparer.Ordinal);
            }

            // The bridge: case "x": return ExecuteX(...); followed through the bridge's own methods it calls.
            SourceFile? bridge = files.FirstOrDefault(f => f.Layer == Layer.Mod && f.RelativePath == "GameState/BannerlordGameStateBridge.cs");
            if (bridge != null)
            {
               Dictionary<string, List<MethodDeclarationSyntax>> methods = bridge.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                                                                                .GroupBy(m => m.Identifier.Text)
                                                                                .ToDictionary(g => g.Key, g => g.ToList());
               foreach (SwitchSectionSyntax section in bridge.Root.DescendantNodes().OfType<SwitchSectionSyntax>())
               {
                  string? called = section.Statements.SelectMany(s => s.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                                          .Select(i => i.Expression as IdentifierNameSyntax)
                                          .FirstOrDefault(n => n != null && methods.ContainsKey(n.Identifier.Text))?.Identifier.Text;
                  if (called == null) continue;

                  HashSet<string> keys = KeysReachable(called, methods);
                  foreach (string action in section.Labels.OfType<CaseSwitchLabelSyntax>().Select(l => Literal(l.Value)).Where(a => a != null).Select(a => a!))
                  {
                     if (!c.Declared.ContainsKey(action)) continue; // the bridge switches on other strings too (a wound's severity)
                     c.Read[action] = keys;
                     c.ReadIn[action] = $"BannerlordGameStateBridge.{called}";
                  }
               }
            }

            // Verb classes: IConversationVerb with `public string Name => "x";`.
            foreach (ClassDeclarationSyntax type in files.Where(f => f.Layer == Layer.Mod).SelectMany(f => f.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()))
            {
               if (type.BaseList?.Types.Any(t => t.Type.ToString() == "IConversationVerb") != true) continue;
               string? name = type.Members.OfType<PropertyDeclarationSyntax>()
                                  .Where(p => p.Identifier.Text == "Name")
                                  .Select(p => Literal(p.ExpressionBody?.Expression))
                                  .FirstOrDefault(n => n != null);
               if (name == null) continue;

               HashSet<string> keys = KeysIn(type);
               if (c.Read.TryGetValue(name, out HashSet<string>? already)) already.UnionWith(keys);
               else
               {
                  c.Read[name] = keys;
                  c.ReadIn[name] = type.Identifier.Text;
               }
            }

            return c;
         }

         private static HashSet<string> KeysReachable(string start, Dictionary<string, List<MethodDeclarationSyntax>> methods)
         {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var todo = new Stack<string>();
            todo.Push(start);
            while (todo.Count > 0)
            {
               string name = todo.Pop();
               if (!seen.Add(name) || !methods.TryGetValue(name, out List<MethodDeclarationSyntax>? overloads)) continue;
               foreach (MethodDeclarationSyntax m in overloads)
               {
                  keys.UnionWith(KeysIn(m));
                  foreach (IdentifierNameSyntax callee in m.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(i => i.Expression).OfType<IdentifierNameSyntax>())
                     todo.Push(callee.Identifier.Text);
               }
            }

            return keys;
         }

         /// <summary>Parameters.TryGetValue("x", ...) and Helper(...Parameters, "x", ...), anywhere under <paramref name="node" />.</summary>
         private static HashSet<string> KeysIn(SyntaxNode node)
         {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (InvocationExpressionSyntax call in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
               SeparatedSyntaxList<ArgumentSyntax> args = call.ArgumentList.Arguments;
               if (call.Expression is MemberAccessExpressionSyntax {Name.Identifier.Text: "TryGetValue"} access
                   && access.Expression.ToString().Contains("Parameters")
                   && Literal(args.FirstOrDefault()?.Expression) is string key)
                  keys.Add(key);
               // action?.Parameters?.TryGetValue("x", ...): a binding inside a conditional access, not a member access.
               else if (call.Expression is MemberBindingExpressionSyntax {Name.Identifier.Text: "TryGetValue"}
                        && call.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression.ToString().Contains("Parameters") == true
                        && Literal(args.FirstOrDefault()?.Expression) is string boundKey)
                  keys.Add(boundKey);
               else if (args.Count >= 2 && args[0].Expression.ToString().Contains("Parameters") && Literal(args[1].Expression) is string helperKey)
                  keys.Add(helperKey);
            }

            return keys;
         }
      }

      private static string? Literal(ExpressionSyntax? expression)
         => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : null;

      #endregion
   }
}
