// Code written by Gabriel Mailhot, 19/10/2026.
// The MCM page, translatable. kkxz3 (Nexus, 18/10/2026, "written using a translator"): "Could you make it possible to
// translate the MCM settings?" Gabriel said yes on 19/10/2026. MCM shows every setting's name, hint, group and button
// through the game's TextObject (MCM 1.4.1, SettingsPropertyVM), so a "{=id}text" literal is looked up in the game's
// language files and falls back to its own English. CR ships the English file as the translators' template, generated
// here from CrSettings.cs so it cannot fall behind the page.
//
// If these fail: a new setting shows only in English in every language (no id), one id carries two texts and a
// translator can render only one of them, a hint runs off the screen once its id is set aside, or the shipped template
// no longer matches the page (regenerate with Tools/Update-Maps.ps1 and commit it with the change).
//
// sekiz5 (Nexus, 06/11/2026): a Turkish copy of the template stayed English. The game reads a strings file as
// ChildNodes[1] of the document (LocalizedTextManager.LoadLanguage, 1.4.8), so it must be <base>; the template
// carried two comments before <base>, and every copy of it loaded nothing, silently. Its notes now sit inside <base>.

#region

using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class McmStringsTests
   {
      /// <summary>Where the English template ships, relative to the mod repository.</summary>
      public const string TemplatePath = "CalradiaRemembers/ModuleData/Languages/EN/std_CalradiaRemembers_mcm.xml";

      // The width a hint has before MCM clips it (v2.5.6 repaired nineteen that ran off; cr.test setting_hints_fit).
      private const int MaxHintChars = 250;

      // STAKE (kkxz3): a setting without an id cannot be translated; the page reads half in one language, half in English.
      [Test]
      public void GIVEN_the_mcm_page_WHEN_read_THEN_every_name_hint_group_and_button_carries_a_translation_id()
      {
         string source = Settings();

         Regex.Matches(source, @"\[SettingProperty(?:Bool|Dropdown|Integer|FloatingInteger|Text|Button|Group)\(""(?!\{=)").Count
              .Should().Be(0, "every setting name and group opens on a {=id}");
         Regex.Matches(source, @"(?:HintText|Content)\s*=\s*""(?!\{=)").Count.Should().Be(0, "every hint and button opens on a {=id}");
      }

      // STAKE: a translator writes one text per id; an id that means two things on the page is translated wrong once.
      [Test]
      public void GIVEN_the_mcm_page_WHEN_read_THEN_one_id_carries_one_text()
      {
         var clashes = Strings().GroupBy(s => s.Id).Where(g => g.Select(s => s.Text).Distinct().Count() > 1).Select(g => g.Key).ToList();

         clashes.Should().BeEmpty();
      }

      // STAKE: the hint is what the player reads to choose; the id is not shown, the text must still fit the screen.
      [Test]
      public void GIVEN_the_mcm_page_WHEN_read_THEN_every_hint_fits_on_the_screen()
      {
         Strings().Where(s => s.Id.EndsWith("_Hint", StringComparison.Ordinal) && s.Text.Length > MaxHintChars)
                  .Select(s => $"{s.Id}={s.Text.Length}").Should().BeEmpty();
      }

      // STAKE: the template a translator copies is the page as it stands, every id once.
      [Test]
      public void GIVEN_the_mcm_page_as_it_stands_WHEN_the_english_template_is_regenerated_THEN_it_matches_the_shipped_one()
         => ApprovedMap.Verify(TemplatePath, Template());

      // STAKE (sekiz5): a translator's copy of the template loads nothing unless <base> is the node right after the
      // declaration, which is the only place the game looks.
      [Test]
      public void GIVEN_the_english_template_WHEN_read_as_the_game_reads_it_THEN_its_second_node_holds_the_strings()
      {
         var doc = new System.Xml.XmlDocument();
         doc.LoadXml(Template());

         doc.ChildNodes[1].Name.Should().Be("base");
         doc.ChildNodes[1].ChildNodes.Cast<System.Xml.XmlNode>().Where(n => n.Name == "strings").Single().ChildNodes
            .Cast<System.Xml.XmlNode>().Count(n => n.Name == "string").Should().BeGreaterThan(0);
      }

      #region private

      private static string Settings()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}.");

         return SourceTree.Production.Single(f => f.RelativePath.EndsWith("Foundation/CrSettings.cs", StringComparison.Ordinal)).Text;
      }

      // Every "{=id}text" literal on the page, its concatenated pieces joined, in the order the page declares them.
      private static List<(string Id, string Text)> Strings()
      {
         string source = Settings();
         var found = new List<(string, string)>();

         foreach (Match m in Regex.Matches(source, @"""\{=(CR_MCM_\w+)\}"))
         {
            var text = new StringBuilder();
            int i = m.Index + m.Length;
            while (true)
            {
               i = ReadLiteralBody(source, i, text);
               int next = SkipSpace(source, i);
               if (next < source.Length && source[next] == '+')
               {
                  int open = SkipSpace(source, next + 1);
                  if (open < source.Length && source[open] == '"')
                  {
                     i = open + 1;
                     continue;
                  }
               }

               break;
            }

            found.Add((m.Groups[1].Value, text.ToString()));
         }

         return found;
      }

      // Reads a regular C# string literal's body from just after its opening quote; returns the index after its closing quote.
      private static int ReadLiteralBody(string s, int i, StringBuilder into)
      {
         while (i < s.Length && s[i] != '"')
         {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
               char e = s[i + 1];
               into.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
               i += 2;
               continue;
            }

            into.Append(s[i]);
            i++;
         }

         return i + 1;
      }

      private static int SkipSpace(string s, int i)
      {
         while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

         return i;
      }

      private static string Template()
      {
         var sb = new StringBuilder();
         sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
         // The notes go INSIDE <base>: the game reads the document's second node and nothing else (sekiz5, 06/11/2026).
         sb.Append("<base xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" type=\"string\">\n");
         sb.Append("  <!-- GENERATED from CalradiaRemembers/Foundation/CrSettings.cs by McmStringsTests: do not edit by hand. -->\n");
         sb.Append("  <!-- To translate: see ../README.txt. Keep every id; translate only the text. -->\n");
         sb.Append("  <tags>\n    <tag language=\"English\" />\n  </tags>\n  <strings>\n");
         foreach ((string id, string text) in Strings().GroupBy(s => s.Id).Select(g => g.First()))
            sb.Append($"    <string id=\"{id}\" text=\"{Escape(text)}\" />\n");
         sb.Append("  </strings>\n</base>\n");

         return sb.ToString();
      }

      private static string Escape(string text)
         => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("\n", "&#10;");

      #endregion
   }
}
