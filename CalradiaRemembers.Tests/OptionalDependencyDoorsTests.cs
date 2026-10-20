// Code written by Gabriel Mailhot, 20/10/2026.
// The doors a mod that keeps Calradia Remembers optional can use. tashmetu (20/10/2026): "without Calradia Remembers the
// game refused R&S altogether ('could not be loaded correctly due to a dependency conflict'). The game reads every type of
// a DLL when it loads it, so a class that implements your interface cannot load without your DLL." A door whose signature
// names only the game's and .NET's types can be called from a method that runs only when CR is there, and the mod still
// loads without CR. A public member documented "Names no Calradia Remembers type" makes that promise; this guard holds it
// to it. If it is wrong, a mod built on such a door is refused by the game for every player who does not run CR.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class OptionalDependencyDoorsTests
   {
      private const string Promise = "Names no Calradia Remembers type";

      private static IEnumerable<MemberDeclarationSyntax> Doors()
         => SourceTree.Production
                      .Where(f => ("/" + f.RelativePath.Replace('\\', '/')).Contains("/Extension/"))
                      .SelectMany(f => f.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
                      .Where(m => m is MethodDeclarationSyntax or EventFieldDeclarationSyntax or EventDeclarationSyntax)
                      .Where(m => m.GetLeadingTrivia().ToFullString().Replace("///", " ").Split(default(char[]), System.StringSplitOptions.RemoveEmptyEntries)
                                   is var words && string.Join(" ", words).Contains(Promise));

      private static IEnumerable<TypeSyntax> SignatureTypes(MemberDeclarationSyntax m) => m switch
      {
         MethodDeclarationSyntax method => method.ParameterList.Parameters.Select(p => p.Type).Append(method.ReturnType),
         EventFieldDeclarationSyntax ev => new[] {ev.Declaration.Type},
         EventDeclarationSyntax ev => new[] {ev.Type},
         _ => Enumerable.Empty<TypeSyntax>()
      };

      private static string Name(MemberDeclarationSyntax m) => m switch
      {
         MethodDeclarationSyntax method => method.Identifier.Text,
         EventFieldDeclarationSyntax ev => ev.Declaration.Variables.First().Identifier.Text,
         EventDeclarationSyntax ev => ev.Identifier.Text,
         _ => "?"
      };

      // STAKE: tashmetu's report. A door that promises to name no CR type and names one (a trace class, a contract, an enum)
      // brings back the load failure for every player of that mod who does not run CR.
      [Test]
      public void GIVEN_a_door_promised_to_a_mod_that_keeps_CR_optional_WHEN_reading_its_signature_THEN_it_names_no_CR_type()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         var ours = new HashSet<string>(SourceTree.Production
            .SelectMany(f => f.Root.DescendantNodes())
            .Select(n => n switch
            {
               BaseTypeDeclarationSyntax t => t.Identifier.Text,
               DelegateDeclarationSyntax d => d.Identifier.Text,
               _ => null
            })
            .Where(n => n != null)!);

         List<MemberDeclarationSyntax> doors = Doors().ToList();
         doors.Should().NotBeEmpty("the regard source door makes the promise since 2.7.19");

         var offenders = doors.SelectMany(d => SignatureTypes(d)
                                 .SelectMany(t => t.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
                                 .Select(n => n.Identifier.Text)
                                 .Where(ours.Contains)
                                 .Select(n => $"{Name(d)} names {n}"))
                              .ToList();

         offenders.Should().BeEmpty();
      }
   }
}
