// Code written by Gabriel Mailhot, 11/10/2026.
// Gabriel (11/10/2026): "est-ce que tu peux faire en sorte que les scenarios soient aussi supportes par des mods?"
// Scenario packs were read from CR's own ModuleData/scenarios only, so a player's pack lived inside CR's folder (an
// update could wipe it) and a mod could not ship one. Now, as tales do (cr_quests): CR's own folder, then the player's
// Custom/scenarios (kept across updates, the MCM path when set), then each enabled mod's ModuleData/cr_scenarios in the
// launcher's order. A fired one-shot is sealed in the save by its rule id alone, so two packs sharing an id would seal
// each other: the first one read keeps the id, a later one is dropped and reported with both sources.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>A folder of scenario packs and who it belongs to.</summary>
    public sealed class ScenarioPackFolder
    {
        /// <summary>Who the packs belong to: "CalradiaRemembers", "Custom", or another mod's id.</summary>
        public string Source { get; init; }

        /// <summary>The folder holding the pack files.</summary>
        public string Folder { get; init; }
    }

    /// <summary>Where scenario packs are read from, in order. Pure.</summary>
    public static class ScenarioPackFolders
    {
        /// <summary>The folder another mod ships its scenario packs in, inside its own ModuleData.</summary>
        public const string ModuleFolder = "cr_scenarios";

        /// <summary>The source name of CR's own packs.</summary>
        public const string OwnSource = "CalradiaRemembers";

        /// <summary>The source name of the player's own packs (the Custom folder).</summary>
        public const string CustomSource = "Custom";

        /// <summary>
        ///   CR's shipped folder, then scenarios under the player's Custom folder, then each other enabled mod's
        ///   ModuleData/cr_scenarios in launcher order (CR itself, blanks and repeats left out). Each folder once.
        /// </summary>
        public static IReadOnlyList<ScenarioPackFolder> List(string shippedFolder, string playerCustomFolder,
                                                             IEnumerable<(string Id, string Root)> enabledModules, string ownModuleId)
        {
            var folders = new List<ScenarioPackFolder>();
            void Add(string source, string folder)
            {
                if (string.IsNullOrWhiteSpace(folder)) return;
                string norm = folder.TrimEnd('\\', '/');
                foreach (ScenarioPackFolder known in folders)
                    if (string.Equals(known.Folder.TrimEnd('\\', '/'), norm, System.StringComparison.OrdinalIgnoreCase)) return;
                folders.Add(new ScenarioPackFolder {Source = source, Folder = folder});
            }

            Add(OwnSource, shippedFolder);
            if (!string.IsNullOrWhiteSpace(playerCustomFolder)) Add(CustomSource, System.IO.Path.Combine(playerCustomFolder, "scenarios"));
            // The same walk as tales (AuthoredQuestFolders.FromModules), kept here so Logic does not come to depend on its
            // AuthoredQuests namespace, which already depends on it.
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach ((string id, string root) in enabledModules ?? new List<(string, string)>())
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(root)) continue;
                if (string.Equals(id.Trim(), ownModuleId, System.StringComparison.OrdinalIgnoreCase) || !seen.Add(id.Trim())) continue;
                Add(id.Trim(), System.IO.Path.Combine(root, "ModuleData", ModuleFolder));
            }

            return folders;
        }
    }

    /// <summary>A loaded rule and the source it came from.</summary>
    public sealed class SourcedScenarioRule
    {
        /// <summary>Who the pack belongs to (see <see cref="ScenarioPackFolder.Source" />).</summary>
        public string Source { get; init; }

        /// <summary>The rule as parsed.</summary>
        public ScenarioRule Rule { get; init; }
    }

    /// <summary>The rules kept, where each came from, and what was dropped.</summary>
    public sealed class ScenarioMergeResult
    {
        /// <summary>The rules kept, in reading order.</summary>
        public IReadOnlyList<ScenarioRule> Rules { get; init; }

        /// <summary>The source of each kept rule, by rule id.</summary>
        public IReadOnlyDictionary<string, string> SourceOf { get; init; }

        /// <summary>One line per rule dropped (a repeated id), naming both sources.</summary>
        public IReadOnlyList<string> Problems { get; init; }
    }

    /// <summary>Keeps one rule per id, the first read. Pure.</summary>
    public static class ScenarioRuleMerge
    {
        /// <summary>
        ///   The rules in reading order, each id once: the first keeps it (CR's own before the player's before a mod's),
        ///   a later rule with the same id (case-insensitive, as an author reads it) is dropped and reported.
        /// </summary>
        public static ScenarioMergeResult Merge(IEnumerable<SourcedScenarioRule> rules)
        {
            var kept = new List<ScenarioRule>();
            var sources = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var problems = new List<string>();

            foreach (SourcedScenarioRule s in rules ?? new List<SourcedScenarioRule>())
            {
                if (s?.Rule == null || string.IsNullOrWhiteSpace(s.Rule.Id)) continue;
                if (sources.TryGetValue(s.Rule.Id, out string first))
                {
                    problems.Add($"rule '{s.Rule.Id}' from {s.Source} skipped: {first} already defines that id (give each rule an id of its own, e.g. prefixed with your mod's id).");
                    continue;
                }

                sources[s.Rule.Id] = s.Source;
                kept.Add(s.Rule);
            }

            return new ScenarioMergeResult {Rules = kept, SourceOf = sources, Problems = problems};
        }
    }
}
