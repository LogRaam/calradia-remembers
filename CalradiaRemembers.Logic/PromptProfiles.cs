// Code written by Gabriel Mailhot, 12/10/2026.
// The Prompt Composer, increment 2 (docs/design/prompt-composer/DESIGN.md). Gabriel (11/10/2026): "Un param MCM pourrait
// designer l'ordonnanceur a utiliser, car il pourrait y avoir plusieurs mods qui proposent un ordonnanceur", "Mise en
// page par type", "fait les choix logiques pour laisser le plus de liberte". A prompt PROFILE is a folder (profile.json,
// layouts/<kind>.json) found in the player's Custom/Prompts, or in a mod's ModuleData/cr_prompts; stock is CR's own order
// and always offered. The player picks one in MCM; each character gets the profile's layout for their kind, else its
// default, else its parent's, else stock. MCM saves only the PLACE of the pick in its list, so the pick is also kept by
// id and found again when a mod added or removed a profile. The SDK keeps the locks (PromptLayoutResolver).

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NpcMemoryService.Core.Prompts;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The kinds of character a profile may give its own layout (DESIGN.md, most specific first).</summary>
    public static class PromptKinds
    {
        public const string Spouse = "spouse";
        public const string Companion = "companion";
        public const string CaptiveScene = "captive_scene";
        public const string GangLeader = "gang_leader";
        public const string Notable = "notable";
        public const string Wanderer = "wanderer";
        public const string Lord = "lord";
        public const string Default = "default";

        /// <summary>Every kind, most specific first.</summary>
        public static IReadOnlyList<string> All { get; } = new[] {Spouse, Companion, CaptiveScene, GangLeader, Notable, Wanderer, Lord, Default};
    }

    /// <summary>What decides the kind of the character speaking.</summary>
    public sealed class PromptKindFacts
    {
        /// <summary>The character is the player's spouse.</summary>
        public bool IsPlayerSpouse { get; init; }

        /// <summary>The character is one of the player's companions.</summary>
        public bool IsPlayerCompanion { get; init; }

        /// <summary>The conversation is a captive or captor scene.</summary>
        public bool InCaptiveScene { get; init; }

        /// <summary>The station the behaviour guidelines use (StationGuidelinesPolicy keys), or null for the lordly register.</summary>
        public string StationKey { get; init; }

        /// <summary>The character is a lord (the game's occupation).</summary>
        public bool IsLord { get; init; }
    }

    /// <summary>Which kind a character is, for the layout. Pure.</summary>
    public static class PromptKindPolicy
    {
        /// <summary>The first kind that fits, most specific first; <see cref="PromptKinds.Default" /> when none does.</summary>
        public static string Of(PromptKindFacts f)
        {
            if (f == null) return PromptKinds.Default;
            if (f.IsPlayerSpouse) return PromptKinds.Spouse;
            if (f.IsPlayerCompanion || f.StationKey == StationGuidelinesPolicy.CompanionKey) return PromptKinds.Companion;
            if (f.InCaptiveScene) return PromptKinds.CaptiveScene;
            if (f.StationKey == StationGuidelinesPolicy.GangLeaderKey) return PromptKinds.GangLeader;
            if (f.StationKey == StationGuidelinesPolicy.NotableKey) return PromptKinds.Notable;
            if (f.StationKey == StationGuidelinesPolicy.WandererKey) return PromptKinds.Wanderer;

            return f.IsLord ? PromptKinds.Lord : PromptKinds.Default;
        }
    }

    /// <summary>A profile found on disk (or stock, which has no folder).</summary>
    public sealed class PromptProfileSource
    {
        /// <summary>The profile's id: its folder name ("stock" for CR's own order).</summary>
        public string Id { get; init; }

        /// <summary>Who it belongs to: "CalradiaRemembers", "Custom", or a mod's id.</summary>
        public string Source { get; init; }

        /// <summary>Its folder; null for stock.</summary>
        public string Folder { get; init; }
    }

    /// <summary>Where prompt profiles are found. Pure: the folder listing is handed in.</summary>
    public static class PromptProfileFolders
    {
        /// <summary>The id of CR's own order, always offered first.</summary>
        public const string StockId = "stock";

        /// <summary>The folder a mod ships its profiles in, inside its own ModuleData.</summary>
        public const string ModuleFolder = "cr_prompts";

        /// <summary>The folder, under the player's Custom folder, of the player's own profiles.</summary>
        public const string CustomFolder = "Prompts";

        /// <summary>
        ///   Stock, then each sub-folder of the player's Custom/Prompts, then each enabled mod's ModuleData/cr_prompts
        ///   sub-folders in launcher order (CR itself left out). An id already found keeps its first owner; the later one is
        ///   reported.
        /// </summary>
        public static IReadOnlyList<PromptProfileSource> List(string playerCustomFolder, IEnumerable<(string Id, string Root)> enabledModules,
                                                              string ownModuleId, Func<string, IEnumerable<string>> subFolders, List<string> problems)
        {
            var found = new List<PromptProfileSource> {new() {Id = StockId, Source = ScenarioPackFolders.OwnSource}};

            void AddAll(string source, string folder)
            {
                IEnumerable<string> dirs;
                try { dirs = subFolders(folder) ?? Enumerable.Empty<string>(); }
                catch { return; }

                foreach (string dir in dirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    string id = Path.GetFileName(dir.TrimEnd('\\', '/'));
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    PromptProfileSource owner = found.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (owner != null)
                    {
                        problems?.Add($"profile '{id}' of {source} has the id of {owner.Source}'s: left out.");
                        continue;
                    }

                    found.Add(new PromptProfileSource {Id = id, Source = source, Folder = dir});
                }
            }

            if (!string.IsNullOrWhiteSpace(playerCustomFolder)) AddAll(ScenarioPackFolders.CustomSource, Path.Combine(playerCustomFolder, CustomFolder));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, string root) in enabledModules ?? Enumerable.Empty<(string, string)>())
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(root)) continue;
                if (string.Equals(id.Trim(), ownModuleId, StringComparison.OrdinalIgnoreCase) || !seen.Add(id.Trim())) continue;
                AddAll(id.Trim(), Path.Combine(root, "ModuleData", ModuleFolder));
            }

            return found;
        }
    }

    /// <summary>A profile as read: its layouts by kind, and what was wrong in its files.</summary>
    public sealed class PromptProfile
    {
        public string Id { get; init; }

        /// <summary>Shown in MCM; the id when profile.json names none.</summary>
        public string Name { get; set; }

        public string Source { get; init; }

        /// <summary>The profile it falls back to before stock, or null.</summary>
        public string Parent { get; set; }

        /// <summary>The CR version it was made from (informational).</summary>
        public string BasedOn { get; set; }

        /// <summary>Its layouts, by kind.</summary>
        public Dictionary<string, PromptLayout> Layouts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Its rewordings, by section key (sections/&lt;key&gt;.txt; increment 3).</summary>
        public Dictionary<string, PromptSectionText> Sections { get; init; } = new(StringComparer.Ordinal);

        /// <summary>The "!# based_on: key@hash" a rewording carries, by key, for the drift check.</summary>
        public Dictionary<string, string> SectionBasedOn { get; init; } = new(StringComparer.Ordinal);

        /// <summary>What was wrong in its files, for the log and cr.prompt_check.</summary>
        public List<string> Problems { get; init; } = new();
    }

    /// <summary>The rewordings a profile chain gives the prompt. Pure.</summary>
    public static class PromptSectionTexts
    {
        /// <summary>Nearest profile first: its rewording of a section wins over a parent's. Null when nothing is reworded.</summary>
        public static IReadOnlyDictionary<string, PromptSectionText> Of(IEnumerable<PromptProfile> chain)
        {
            var texts = new Dictionary<string, PromptSectionText>(StringComparer.Ordinal);
            foreach (PromptProfile p in chain ?? Enumerable.Empty<PromptProfile>())
            foreach (KeyValuePair<string, PromptSectionText> s in p.Sections)
                if (!texts.ContainsKey(s.Key)) texts[s.Key] = s.Value;

            return texts.Count == 0 ? null : texts;
        }
    }

    /// <summary>Reads a profile's files. Pure: file reading is handed in.</summary>
    public static class PromptProfileReader
    {
        /// <summary>
        ///   profile.json (name, author, description, based_on, parent) and every layouts/&lt;kind&gt;.json. A file that is
        ///   not valid JSON, or a layout named after no kind, is left out and reported; nothing here throws.
        /// </summary>
        public static PromptProfile Read(PromptProfileSource source, Func<string, string> readFile, Func<string, IEnumerable<string>> filesIn)
        {
            var p = new PromptProfile {Id = source?.Id, Name = source?.Id, Source = source?.Source};
            if (string.IsNullOrWhiteSpace(source?.Folder)) return p;

            string profileFile = Path.Combine(source.Folder, "profile.json");
            JObject head = Parse(SafeRead(readFile, profileFile), "profile.json", p);
            if (head != null)
            {
                p.Name = Text(head, "name") ?? p.Name;
                p.Parent = Text(head, "parent");
                p.BasedOn = Text(head, "based_on");
            }

            string layouts = Path.Combine(source.Folder, "layouts");
            IEnumerable<string> files;
            try { files = filesIn(layouts) ?? Enumerable.Empty<string>(); }
            catch { files = Enumerable.Empty<string>(); }

            foreach (string file in files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string kind = Path.GetFileNameWithoutExtension(file);
                if (!PromptKinds.All.Contains(kind, StringComparer.OrdinalIgnoreCase))
                {
                    p.Problems.Add($"layouts/{Path.GetFileName(file)}: '{kind}' is no kind of character ({string.Join(", ", PromptKinds.All)}): left out.");
                    continue;
                }

                JObject layout = Parse(SafeRead(readFile, file), "layouts/" + Path.GetFileName(file), p);
                if (layout == null) continue;

                p.Layouts[kind.ToLowerInvariant()] = new PromptLayout {
                    Prefix = Keys(layout, "prefix"), Tail = Keys(layout, "tail"), Off = Keys(layout, "off"),
                    Source = $"profile '{p.Id}' ({p.Source}), layout '{kind.ToLowerInvariant()}'"
                };
            }

            ReadSections(source, readFile, filesIn, p);

            return p;
        }

        // sections/<key>.txt: the words replace the stock text; "!append" adds them; "!remove <text>" drops stock lines;
        // "!# ..." is a note ("!# based_on: key@hash" kept for the drift check).
        private static void ReadSections(PromptProfileSource source, Func<string, string> readFile, Func<string, IEnumerable<string>> filesIn, PromptProfile p)
        {
            string folder = Path.Combine(source.Folder, "sections");
            IEnumerable<string> files;
            try { files = filesIn(folder) ?? Enumerable.Empty<string>(); }
            catch { files = Enumerable.Empty<string>(); }

            var known = new HashSet<string>(PromptBuilder.CharacterSections.Select(s => s.Key), StringComparer.Ordinal);
            foreach (string file in files.Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string key = Path.GetFileNameWithoutExtension(file);
                if (!known.Contains(key))
                {
                    p.Problems.Add($"sections/{Path.GetFileName(file)}: '{key}' is no section of the prompt: left out (cr.prompt_dump <name> lists them).");
                    continue;
                }

                string raw = SafeRead(readFile, file);
                if (raw == null) continue;

                var words = new List<string>();
                var remove = new List<string>();
                var append = false;
                foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("!#", StringComparison.Ordinal))
                    {
                        string note = trimmed.Substring(2).Trim();
                        if (note.StartsWith("based_on:", StringComparison.OrdinalIgnoreCase)) p.SectionBasedOn[key] = note.Substring("based_on:".Length).Trim();
                        continue;
                    }

                    if (trimmed.Equals("!append", StringComparison.OrdinalIgnoreCase)) append = true;
                    else if (trimmed.StartsWith("!remove ", StringComparison.OrdinalIgnoreCase)) remove.Add(trimmed.Substring("!remove ".Length).Trim());
                    else words.Add(line.TrimEnd());
                }

                string text = string.Join("\n", words).Trim('\n');
                p.Sections[key] = new PromptSectionText {
                    Text = text.Length == 0 && (append || remove.Count > 0) ? null : text, Append = append, RemoveLines = remove,
                    Source = $"profile '{p.Id}' ({p.Source}), sections/{key}.txt"
                };
            }
        }

        private static string SafeRead(Func<string, string> readFile, string path)
        {
            try { return readFile(path); }
            catch { return null; }
        }

        private static JObject Parse(string text, string name, PromptProfile p)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            try { return JObject.Parse(text); }
            catch (Exception ex)
            {
                p.Problems.Add($"{name} is not valid JSON ({ex.Message}): left out.");
                return null;
            }
        }

        private static string Text(JObject o, string name)
        {
            string s = o[name]?.Type == JTokenType.String ? (string) o[name] : null;
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static IReadOnlyList<string> Keys(JObject o, string name)
            => o[name] is JArray a ? a.Where(t => t.Type == JTokenType.String).Select(t => ((string) t).Trim()).ToList() : new List<string>();
    }

    /// <summary>The chosen profile and its parents, nearest first. Pure.</summary>
    public static class PromptProfileChain
    {
        /// <summary>The chosen profile, then its parent, and so on; stops at a missing parent or a loop, and says so. Stock is no link.</summary>
        public static IReadOnlyList<PromptProfile> Of(string chosenId, IReadOnlyDictionary<string, PromptProfile> profiles, List<string> problems)
        {
            var chain = new List<PromptProfile>();
            string id = chosenId;
            while (!string.IsNullOrWhiteSpace(id) && !string.Equals(id, PromptProfileFolders.StockId, StringComparison.OrdinalIgnoreCase))
            {
                if (profiles == null || !profiles.TryGetValue(id, out PromptProfile p))
                {
                    problems?.Add(chain.Count == 0 ? $"profile '{id}' was not found: the stock prompt is used." : $"parent profile '{id}' was not found: stock comes next.");
                    break;
                }

                if (chain.Contains(p))
                {
                    problems?.Add($"profile '{id}' is its own ancestor: the chain stops there.");
                    break;
                }

                chain.Add(p);
                id = p.Parent;
            }

            return chain;
        }
    }

    /// <summary>Which layout a character gets. Pure.</summary>
    public static class PromptLayoutChoice
    {
        /// <summary>
        ///   Along the chain, nearest first: the profile's layout for the kind, else its default; then its parent's the same
        ///   way. Null: the stock order.
        /// </summary>
        public static PromptLayout For(string kind, IReadOnlyList<PromptProfile> chain)
        {
            foreach (PromptProfile p in chain ?? new List<PromptProfile>())
            {
                if (!string.IsNullOrWhiteSpace(kind) && p.Layouts.TryGetValue(kind, out PromptLayout own)) return own;
                if (p.Layouts.TryGetValue(PromptKinds.Default, out PromptLayout fallback)) return fallback;
            }

            return null;
        }
    }

    /// <summary>What the MCM pick is judged on.</summary>
    public sealed class PromptSelectionFacts
    {
        /// <summary>The profile ids in the MCM list now, stock first.</summary>
        public IReadOnlyList<string> ListNow { get; init; }

        /// <summary>The place MCM has selected (all it saves).</summary>
        public int SelectedIndexNow { get; init; }

        /// <summary>The list as it stood when a profile was last applied; null the first time.</summary>
        public IReadOnlyList<string> ListLastTime { get; init; }

        /// <summary>The id applied last time; null the first time.</summary>
        public string ChosenLastTime { get; init; }
    }

    /// <summary>The profile to use and the place MCM should show.</summary>
    public sealed class PromptSelection
    {
        public string ChosenId { get; init; }

        public int SelectedIndex { get; init; }

        /// <summary>Said in the log when the pick had to be found again or fell back to stock; null otherwise.</summary>
        public string Notice { get; init; }
    }

    /// <summary>Keeps the player's pick when the list of profiles changes under MCM's saved place. Pure.</summary>
    public static class PromptProfileSelection
    {
        /// <summary>
        ///   Same list as last time (or no last time): the place MCM shows is the player's pick. A different list: MCM's
        ///   place may now name another profile, so the id applied last time is found again; gone, stock.
        /// </summary>
        public static PromptSelection Resolve(PromptSelectionFacts f)
        {
            IReadOnlyList<string> now = f?.ListNow ?? new[] {PromptProfileFolders.StockId};
            int index = f != null && f.SelectedIndexNow >= 0 && f.SelectedIndexNow < now.Count ? f.SelectedIndexNow : 0;
            bool sameList = f?.ListLastTime == null || f.ListLastTime.SequenceEqual(now, StringComparer.OrdinalIgnoreCase);
            if (sameList || string.IsNullOrWhiteSpace(f.ChosenLastTime)) return new PromptSelection {ChosenId = now[index], SelectedIndex = index};

            for (var i = 0; i < now.Count; i++)
                if (string.Equals(now[i], f.ChosenLastTime, StringComparison.OrdinalIgnoreCase))
                    return new PromptSelection {
                        ChosenId = now[i], SelectedIndex = i,
                        Notice = i == index ? null : $"the list of prompt profiles changed: '{now[i]}' stays chosen."
                    };

            return new PromptSelection {
                ChosenId = now[0], SelectedIndex = 0,
                Notice = $"the prompt profile '{f.ChosenLastTime}' is no longer found (its mod removed?): the stock prompt is used."
            };
        }
    }
}
