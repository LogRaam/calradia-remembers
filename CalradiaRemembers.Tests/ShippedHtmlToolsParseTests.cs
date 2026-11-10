// Code written by Gabriel Mailhot, 11/10/2026.
// The Tale Editor and the Scenario Composer ship in Docs/ as single HTML pages a player opens in a browser. Gabriel
// (11/10/2026): "lorsque je demarre TaleEditor.html, ca ne fonctionne pas". One unescaped apostrophe in a label
// ('Travelling in the player's party', added with spouse tales) was a syntax error that stopped the whole page, and
// nothing caught it: the parity tests run only the editor's validator under Node, never its forms. A browser drops a
// script with a syntax error silently, so every code script in each shipped page is parsed here.

#region

using System.Diagnostics;
using System.Text.RegularExpressions;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ShippedHtmlToolsParseTests
   {
      // STAKE: a page that does not run gives an author nothing: no forms, no checks, no download. Gabriel opened the Tale
      // Editor and it did nothing at all; every player who opens it from Docs/ would have seen the same.
      [TestCase("TaleEditor.html")]
      [TestCase("ScenarioComposer.html")]
      [TestCase("HistoryEditor.html")]
      [TestCase("CrvidPlayer.html")]
      public void GIVEN_a_page_shipped_in_Docs_WHEN_a_browser_reads_its_scripts_THEN_none_has_a_syntax_error(string page)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string html = File.ReadAllText(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "Extension", page));
         var errors = new List<string>();
         int index = 0;
         foreach (Match m in Regex.Matches(html, @"<script([^>]*)>(.*?)</script>", RegexOptions.Singleline))
         {
            index++;
            if (m.Groups[1].Value.Contains("json", StringComparison.OrdinalIgnoreCase)) continue;

            string file = Path.Combine(Path.GetTempPath(), $"cr_parse_{Guid.NewGuid():N}.js");
            File.WriteAllText(file, m.Groups[2].Value);
            try
            {
               string error = NodeCheck(file);
               if (error != null) errors.Add($"script {index}{m.Groups[1].Value}: {error}");
            }
            finally
            {
               File.Delete(file);
            }
         }

         errors.Should().BeEmpty($"every script of {page} must parse, or the browser drops it and the page does nothing");
      }

      // node --check: null when the script parses, else the first lines of what Node says.
      private static string NodeCheck(string file)
      {
         var start = new ProcessStartInfo("node") {RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false};
         start.ArgumentList.Add("--check");
         start.ArgumentList.Add(file);

         Process process;
         try { process = Process.Start(start)!; }
         catch (System.ComponentModel.Win32Exception)
         {
            Assert.Fail("Node is not installed or not on PATH; the shipped pages cannot be checked without it.");

            return null;
         }

         string error = process.StandardError.ReadToEnd();
         process.WaitForExit();

         return process.ExitCode == 0 ? null : string.Join(" ", error.Split('\n').Take(4)).Trim();
      }
   }
}
