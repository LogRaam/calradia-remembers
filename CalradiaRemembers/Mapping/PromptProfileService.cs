// Code written by Gabriel Mailhot, 12/10/2026.
// The Prompt Composer, increment 2 (docs/design/prompt-composer/DESIGN.md). Gabriel (11/10/2026): "Un param MCM pourrait
// designer l'ordonnanceur a utiliser, car il pourrait y avoir plusieurs mods qui proposent un ordonnanceur". Finds the
// prompt profiles (Logic/PromptProfiles: stock, the player's Custom/Prompts, each mod's ModuleData/cr_prompts), fills the
// MCM "Prompt Profile" list with them, keeps the player's pick by id when the list changes (MCM saves only the place in
// the list), reads the chosen profile and its parents, and hands each character the layout for their kind. Read once per
// session and again when the pick changes, never per reply. Stock is always one MCM switch away.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The prompt profile in use, and the layout each kind of character gets from it.</summary>
   internal static class PromptProfileService
   {
      private const string SelectionFile = "selection.json";

      private static List<PromptProfileSource> _sources = new() {new PromptProfileSource {Id = PromptProfileFolders.StockId, Source = ScenarioPackFolders.OwnSource}};
      private static readonly List<string> FindProblems = new();
      private static IReadOnlyList<PromptProfile> _chain = new List<PromptProfile>();
      private static IReadOnlyDictionary<string, PromptSectionText> _texts;
      private static readonly List<string> ChainProblems = new();
      private static string _appliedId = PromptProfileFolders.StockId;
      private static int _appliedIndex;

      /// <summary>What MCM shows for stock.</summary>
      internal const string StockLabel = "Stock (Calradia Remembers)";

      /// <summary>The profile in use ("stock" for CR's own order).</summary>
      internal static string AppliedId => _appliedId;

      /// <summary>
      ///   Finds the profiles, puts them in the MCM list, settles the pick (kept by id when the list changed) and reads the
      ///   chosen profile. At the main menu and at each session launch; again from cr.prompt_check.
      /// </summary>
      internal static void Refresh()
      {
         try
         {
            FindProblems.Clear();
            string custom = NarrativeLoader.CustomBaseFolder();
            _sources = PromptProfileFolders.List(custom, EnabledModules(), ScenarioPackFolders.OwnSource,
               d => Directory.Exists(d) ? Directory.GetDirectories(d) : Array.Empty<string>(), FindProblems).ToList();

            var ids = _sources.Select(s => s.Id).ToList();
            (List<string> lastList, string lastChosen) = ReadSelection();
            MCM.Common.Dropdown<string> dropdown = ModSettings.PromptProfile;
            PromptSelection pick = PromptProfileSelection.Resolve(new PromptSelectionFacts {
               ListNow = ids, SelectedIndexNow = dropdown?.SelectedIndex ?? 0, ListLastTime = lastList, ChosenLastTime = lastChosen
            });
            if (pick.Notice != null) ModLog.Info("[PROMPT] " + pick.Notice);

            Dictionary<string, PromptProfile> read = ReadProfiles();
            if (dropdown != null)
            {
               dropdown.Clear();
               dropdown.AddRange(_sources.Select(s => Label(s, read)));
               dropdown.SelectedIndex = pick.SelectedIndex;
            }

            Apply(pick.ChosenId, pick.SelectedIndex, read);
         }
         catch (Exception ex)
         {
            ModLog.Fault("reading the prompt profiles (the stock prompt is used)", ex);
            UseStock();
         }
      }

      /// <summary>
      ///   The layout for a character of this kind under the profile in use, or null for the stock order. Notices a pick
      ///   changed in MCM since the last build and applies it first.
      /// </summary>
      internal static PromptLayout LayoutFor(string kind)
      {
         try
         {
            int now = ModSettings.PromptProfile?.SelectedIndex ?? 0;
            if (now != _appliedIndex && now >= 0 && now < _sources.Count) Apply(_sources[now].Id, now, ReadProfiles());

            return PromptLayoutChoice.For(kind, _chain);
         }
         catch (Exception ex)
         {
            ModLog.Fault("choosing a prompt layout (the stock order is used)", ex);
            return null;
         }
      }

      /// <summary>The sections the profile in use rewords (increment 3), or null; read once with the profile, never per reply.</summary>
      internal static IReadOnlyDictionary<string, PromptSectionText> Texts => _texts;

      /// <summary>cr.prompt_check: every profile found, the one in use and its parents, and each kind's layout with what the locks changed.</summary>
      internal static string Check()
      {
         Refresh();
         var sb = new StringBuilder();
         sb.AppendLine(ActiveLine());
         sb.AppendLine("Profiles found:");
         Dictionary<string, PromptProfile> read = ReadProfiles();
         foreach (PromptProfileSource s in _sources)
         {
            sb.AppendLine($"  {(s.Id == _appliedId ? "*" : "-")} {s.Id} ({s.Source}){(s.Folder == null ? "" : ": " + s.Folder)}");
            if (read.TryGetValue(s.Id, out PromptProfile p))
               foreach (string problem in p.Problems) sb.AppendLine("      ! " + problem);
         }

         foreach (string problem in FindProblems.Concat(ChainProblems)) sb.AppendLine("  ! " + problem);
         if (_chain.Count == 0) return sb.ToString();

         if (_texts != null)
         {
            sb.AppendLine("Sections reworded:");
            foreach (KeyValuePair<string, PromptSectionText> t in _texts.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
               PromptSectionSpec spec = NpcMemoryService.Core.Prompts.PromptBuilder.CharacterSections.FirstOrDefault(s => s.Key == t.Key);
               string how = t.Value.Append ? "added to" : t.Value.Text == null ? "lines removed from" : "replaces";
               string refused = spec?.Lock == PromptSectionLock.Sealed ? "  ! sealed: ignored, the stock text stays"
                  : spec?.Lock == PromptSectionLock.Required && string.IsNullOrWhiteSpace(t.Value.Text) && !t.Value.Append && t.Value.RemoveLines.Count == 0
                     ? "  ! required: an empty text is ignored" : "";
               sb.AppendLine($"  {t.Key}: {how} the stock text ({t.Value.Source}){refused}");
            }
         }

         sb.AppendLine("Layouts in use, by kind of character:");
         foreach (string kind in PromptKinds.All)
         {
            PromptLayout layout = PromptLayoutChoice.For(kind, _chain);
            sb.AppendLine($"  {kind}: {layout?.Source ?? "stock order"}");
            if (layout == null) continue;

            var problems = new List<string>();
            PromptLayoutResolver.Order(NpcMemoryService.Core.Prompts.PromptBuilder.CharacterSections, layout, problems);
            foreach (string problem in problems) sb.AppendLine("      ! " + problem);
         }

         return sb.ToString();
      }

      /// <summary>The one log line that says whether a custom prompt is in use (DESIGN.md: "Overclocking is visible").</summary>
      internal static string ActiveLine()
      {
         if (_chain.Count == 0) return "[PROMPT] stock prompt";

         PromptProfile p = _chain[0];
         string kinds = string.Join(", ", _chain.SelectMany(c => c.Layouts.Keys).Distinct());
         int reworded = _texts?.Count ?? 0;
         return $"[PROMPT] custom prompt active: profile '{p.Id}' ({p.Source}){(_chain.Count > 1 ? ", parents " + string.Join(" > ", _chain.Skip(1).Select(c => c.Id)) : "")}; " +
                $"layouts: {(kinds.Length == 0 ? "none (stock order)" : kinds)}; {reworded} section(s) reworded{(reworded == 0 ? "" : " (" + string.Join(", ", _texts.Keys) + ")")}; {_chain.Sum(c => c.Problems.Count) + ChainProblems.Count} problem(s), see cr.prompt_check";
      }

      /// <summary>Self-test seam: uses <paramref name="profile" /> as the chosen one (no MCM, no files); <see cref="Refresh" /> puts the real pick back.</summary>
      internal static void DebugUse(PromptProfile profile)
      {
         _chain = profile == null ? new List<PromptProfile>() : new List<PromptProfile> {profile};
         _texts = PromptSectionTexts.Of(_chain);
         _appliedId = profile?.Id ?? PromptProfileFolders.StockId;
         _appliedIndex = ModSettings.PromptProfile?.SelectedIndex ?? 0;
      }

      #region private

      private static void Apply(string id, int index, Dictionary<string, PromptProfile> read)
      {
         ChainProblems.Clear();
         _chain = PromptProfileChain.Of(id, read, ChainProblems);
         _texts = PromptSectionTexts.Of(_chain);
         _appliedId = _chain.Count == 0 ? PromptProfileFolders.StockId : id;
         _appliedIndex = index;
         // Stock is always first, so its place never shifts: only another pick needs remembering by id (and a stale record
         // must not bring back a profile the player has since left for stock).
         if (string.Equals(_appliedId, PromptProfileFolders.StockId, StringComparison.OrdinalIgnoreCase)) ForgetSelection();
         else WriteSelection(_sources.Select(s => s.Id).ToList(), id);
         ModLog.Info(ActiveLine());
         foreach (string problem in ChainProblems.Concat(_chain.SelectMany(c => c.Problems))) ModLog.Info("[PROMPT] ! " + problem);
      }

      private static void UseStock()
      {
         _chain = new List<PromptProfile>();
         _texts = null;
         _appliedId = PromptProfileFolders.StockId;
         _appliedIndex = 0;
      }

      private static Dictionary<string, PromptProfile> ReadProfiles()
         => _sources.Where(s => s.Folder != null)
                    .Select(s => PromptProfileReader.Read(s, path => File.Exists(path) ? File.ReadAllText(path) : null,
                       dir => Directory.Exists(dir) ? Directory.GetFiles(dir) : Array.Empty<string>()))
                    .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

      private static string Label(PromptProfileSource s, Dictionary<string, PromptProfile> read)
      {
         if (s.Folder == null) return StockLabel;

         string name = read.TryGetValue(s.Id, out PromptProfile p) && !string.IsNullOrWhiteSpace(p.Name) ? p.Name : s.Id;
         return $"{name} ({(s.Source == ScenarioPackFolders.CustomSource ? "your own" : s.Source)})";
      }

      private static string SelectionPath() => Path.Combine(NarrativeLoader.CustomBaseFolder(), PromptProfileFolders.CustomFolder, SelectionFile);

      private sealed class SelectionState
      {
         public List<string> List { get; set; }
         public string Chosen { get; set; }
      }

      private static (List<string> List, string Chosen) ReadSelection()
      {
         try
         {
            string path = SelectionPath();
            if (!File.Exists(path)) return (null, null);

            var state = JsonConvert.DeserializeObject<SelectionState>(File.ReadAllText(path));
            return (state?.List, state?.Chosen);
         }
         catch { return (null, null); }
      }

      private static void ForgetSelection()
      {
         try
         {
            string path = SelectionPath();
            if (File.Exists(path)) File.Delete(path);
         }
         catch (Exception ex) { ModLog.Error($"[PROMPT] could not forget the chosen profile: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static void WriteSelection(List<string> list, string chosen)
      {
         try
         {
            string path = SelectionPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(new SelectionState {List = list, Chosen = chosen}, Formatting.Indented));
         }
         catch (Exception ex) { ModLog.Error($"[PROMPT] could not remember the chosen profile: {ex.GetType().Name}: {ex.Message}"); }
      }

      // The other enabled mods with their roots, in launcher order (the same list cr_patch, tales and scenarios read).
      private static IEnumerable<(string Id, string Root)> EnabledModules()
      {
         var modules = new List<(string, string)>();
         foreach (string id in NarrativePatchScanner.LoadedModuleIds())
         {
            string root;
            try { root = TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath(id); }
            catch { continue; }
            modules.Add((id, root));
         }

         return modules;
      }

      #endregion
   }
}
