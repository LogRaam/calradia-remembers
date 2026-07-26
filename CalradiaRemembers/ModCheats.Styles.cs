// Code written by Gabriel Mailhot, 26/07/2026.
// Console surface for the injectable NARRATIVE STYLE feature (Inc 0, before any UI exists): list the styles on
// disk, pick one, and print exactly what is being injected. This is the whole feature without the chat picker,
// so a style can be authored and iterated on from the console alone, and the fragile chat prefab is only
// touched later (Inc 1: a button opening a selection popup).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.Styles;
using CalradiaRemembers.Styles;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        private const int StylePreviewChars = 600;

        /// <summary>cr.styles: lists every style file found, marks the active one, and prints the folder to author in.</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("styles", "cr")]
        public static string Styles(List<string> args)
        {
            try
            {
                IReadOnlyList<string> names = StyleCatalog.AvailableNames();
                string active = StyleService.ActiveName;

                var sb = new StringBuilder();
                sb.AppendLine($"Narrative styles ({names.Count} found) in {StyleCatalog.FolderPath}:");

                if (names.Count == 0)
                    sb.AppendLine("  (none: drop a .txt file in that folder, its name becomes the style's name)");
                else
                    foreach (string n in names)
                        sb.AppendLine(string.Equals(n, active, StringComparison.OrdinalIgnoreCase)
                            ? $"  * {n}   <- active"
                            : $"    {n}");

                sb.Append("Pick one with cr.style <name>; cr.style_show prints what is injected. " +
                          "Editing a style's .txt applies on your next message, no restart.");

                return sb.ToString();
            }
            catch (Exception ex) { return $"cr.styles failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>cr.style &lt;name&gt;: makes that style the one injected into every conversation from the next message on.</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("style", "cr")]
        public static string Style(List<string> args)
        {
            try
            {
                if (args == null || args.Count == 0)
                    return $"Active style: {StyleService.ActiveName}. Usage: cr.style <name>  (cr.styles lists them).";

                string requested = string.Join(" ", args).Trim();
                string applied = StyleService.Select(requested);

                return string.Equals(applied, requested, StringComparison.OrdinalIgnoreCase)
                    ? $"Style set to '{applied}'. It applies to every conversation from your next message."
                    : $"No style named '{requested}'; fell back to '{applied}'. Run cr.styles to see what is available.";
            }
            catch (Exception ex) { return $"cr.style failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.style_show: prints the beginning of the rules currently injected, straight from the live cache,
        ///   so you can confirm an edit to the .txt actually took (the lazy reload fired) before spending a call.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("style_show", "cr")]
        public static string StyleShow(List<string> args)
        {
            try
            {
                string rules = StyleService.CurrentRules();

                if (string.IsNullOrWhiteSpace(rules))
                    return $"Style '{StyleService.ActiveName}' injects nothing (empty or missing file).";

                string preview = rules.Length > StylePreviewChars
                    ? rules.Substring(0, StylePreviewChars) + "..."
                    : rules;

                return $"Style '{StyleService.ActiveName}' ({rules.Length} chars) currently injects:\n{preview}";
            }
            catch (Exception ex) { return $"cr.style_show failed: {ex.GetType().Name}: {ex.Message}"; }
        }
    }
}
