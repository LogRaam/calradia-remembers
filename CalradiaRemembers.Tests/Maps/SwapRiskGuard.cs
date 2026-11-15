// Code written by Gabriel Mailhot, 14/11/2026.
// The swap risk, held by the suite. Tools/Find-SwapRisk.ps1 took CalradiaRemembers.Logic from 99 public static methods
// (plus 29 constructors) with a run of three same-typed parameters to zero on 10/09/2026, but only when somebody ran it. A
// silent argument swap can only hide inside a run of consecutive parameters of one type: anywhere else the compiler
// catches it. Paid for: widening CanEscort, positional, cost seven rewritten assertions; ResolutionWorldFacts took
// twenty-six booleans. Gabriel, 14/11/2026, asking for maps that put good engineering into practice: this is SOLID's
// "depend on a named shape" for a pure rule, made a wall. The fix is never "be careful": ask for the type that already
// exists, or give the policy a facts object with { get; init; } (CLAUDE.md, "The structural cure").
//
// Read from syntax: two parameters are "the same type" when their type and modifier (out, ref, in) are written the same
// (`int` and `Int32` are missed); every public method, constructor and delegate of the Logic layer is read, static or not.
// First run: the one finding was TryDecompose(string, out string, out string), which no caller can swap; hence the modifier.

#region

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   internal static class SwapRiskGuard
   {
      /// <summary>The run length from which a signature can be called with two arguments the wrong way round, unseen.</summary>
      public const int RiskyRun = 3;

      /// <summary>Every public member of the Logic layer with a run of <see cref="RiskyRun" /> or more same-typed parameters, as `Type.Member(run)`.</summary>
      public static IReadOnlyList<string> Findings(IReadOnlyList<SourceFile> files)
      {
         var found = new SortedSet<string>(StringComparer.Ordinal);

         foreach (SourceFile file in files.Where(f => f.Layer == Layer.Logic))
            foreach (SyntaxNode node in file.Root.DescendantNodes())
            {
               (string name, ParameterListSyntax list, SyntaxTokenList modifiers) = node switch {
                  MethodDeclarationSyntax m => (m.Identifier.Text, m.ParameterList, m.Modifiers),
                  ConstructorDeclarationSyntax c => (c.Identifier.Text + ".ctor", c.ParameterList, c.Modifiers),
                  DelegateDeclarationSyntax d => (d.Identifier.Text, d.ParameterList, d.Modifiers),
                  _ => (null, null, default)
               };
               if (list == null || !modifiers.Any(SyntaxKind.PublicKeyword)) continue;

               // The modifier is part of the shape: `string a, out string b` cannot be swapped, the compiler refuses `out` on an input.
               int run = LongestRun(list.Parameters.Select(p => (p.Modifiers.ToString() + " " + (p.Type?.ToString() ?? "?")).Replace(" ", "")).ToList());
               if (run < RiskyRun) continue;

               string owner = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "?";
               found.Add($"{owner}.{name} ({run} in a row)");
            }

         return found.ToList();
      }

      private static int LongestRun(IReadOnlyList<string> types)
      {
         int best = 0, current = 0;
         for (int i = 0; i < types.Count; i++)
         {
            current = i > 0 && types[i] == types[i - 1] ? current + 1 : 1;
            best = Math.Max(best, current);
         }

         return best;
      }
   }
}
