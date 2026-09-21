// Code written by Gabriel Mailhot, 21/09/2026.
// The one sentence that says what a thing IS, lifted from its own XML doc comment. This is why the maps read source
// rather than assemblies: reflection discards these comments, and a list of type names with nothing beside them is
// not a map, it is a directory listing.

#region

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   internal static class DocText
   {
      public const string NoSummary = "(no summary)";

      private const int MaxLength = 220;

      /// <summary>The first sentence of the member's summary, flattened to plain text, or <see cref="NoSummary" />.</summary>
      public static string FirstSentenceOf(SyntaxNode member)
      {
         XmlElementSyntax? summary = member.GetLeadingTrivia()
                                           .Select(t => t.GetStructure())
                                           .OfType<DocumentationCommentTriviaSyntax>()
                                           .SelectMany(d => d.Content.OfType<XmlElementSyntax>())
                                           .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

         if (summary == null) return NoSummary;

         string text = Regex.Replace(Flatten(summary.Content), @"\s+", " ").Trim();
         if (text.Length == 0) return NoSummary;

         return Shorten(FirstSentence(text));
      }

      /// <summary>Text safe to put in a Markdown table cell.</summary>
      public static string Cell(string text) => text.Replace("|", "\\|");

      #region private

      private static string Flatten(SyntaxList<XmlNodeSyntax> nodes)
      {
         var sb = new StringBuilder();

         foreach (XmlNodeSyntax node in nodes)
            switch (node)
            {
               case XmlTextSyntax text:
                  foreach (SyntaxToken token in text.TextTokens) sb.Append(token.ValueText);

                  break;
               case XmlEmptyElementSyntax empty:
                  sb.Append(NamedBy(empty.Attributes));

                  break;
               case XmlElementSyntax element:
                  // A <para> starts the detail; the sentence that says what the thing is always comes before it.
                  if (element.StartTag.Name.ToString() == "para") return sb.ToString();

                  sb.Append(Flatten(element.Content));

                  break;
            }

         return sb.ToString();
      }

      /// <summary>What a see/paramref/typeparamref points at, as the reader would say it.</summary>
      private static string NamedBy(SyntaxList<XmlAttributeSyntax> attributes)
      {
         foreach (XmlAttributeSyntax attribute in attributes)
            switch (attribute)
            {
               case XmlCrefAttributeSyntax cref: return cref.Cref.ToString();
               case XmlNameAttributeSyntax name: return name.Identifier.ToString();
               case XmlTextAttributeSyntax text: return string.Concat(text.TextTokens.Select(t => t.ValueText));
            }

         return "";
      }

      /// <summary>Up to the first full stop that really ends a sentence: followed by a capital, or by nothing.</summary>
      private static string FirstSentence(string text)
      {
         Match end = Regex.Match(text, @"(?<=[a-z0-9\)\]""'])[.!?](?=\s+[A-Z(]|\s*$)");

         return end.Success ? text.Substring(0, end.Index + 1) : text;
      }

      private static string Shorten(string text)
         => text.Length <= MaxLength ? text : text.Substring(0, MaxLength).TrimEnd() + "...";

      #endregion
   }
}
