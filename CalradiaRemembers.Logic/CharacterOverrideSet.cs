// Code written by Gabriel Mailhot, 22/06/2026.
// Resolves which authored override applies to a given hero. Pure so the matching rule — id wins over
// name, name is case-insensitive, and a homonym without an id is reported rather than guessed — stays
// tested; a silent mis-resolution would hand one lord another lord's authored personality.
// LapitaPolata (Nexus, 19/10/2026): "a noble of Rhagaea's clan came to talk to me, and I noticed that she has the
// exactly same backstory as Rhagaea." The game makes a child born in the campaign FROM a parent's character (a
// daughter from her mother, a son from his father: DefaultHeroCreationModel.GetCharacterTemplateForOffspring), so
// the child's template IS the parent: an entry keyed on Rhagaea matched her daughters by the template rule.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Who an authored character entry is looked up for: the hero's id, name and template key.</summary>
    public sealed class HeroOverrideLookup
    {
        /// <summary>The hero's id.</summary>
        public string HeroId { get; init; }

        /// <summary>The hero's name, matched without case.</summary>
        public string HeroName { get; init; }

        /// <summary>The template key (CharacterOverrideSet.TemplateKey), or null.</summary>
        public string TemplateId { get; init; }
    }

    /// <summary>The authored overrides as a whole, with the rule for picking one for a hero.</summary>
    public sealed class CharacterOverrideSet
    {
        private readonly IReadOnlyList<CharacterOverride> _overrides;

        public CharacterOverrideSet(IEnumerable<CharacterOverride> overrides)
            => _overrides = (overrides ?? Enumerable.Empty<CharacterOverride>())
                            .Where(o => o != null)
                            .ToList();

        public int Count => _overrides.Count;

        /// <summary>
        ///   The template id a hero may be matched on, or null. A hero born of parents in the campaign was made from
        ///   a parent's character, so his template is that parent and never a key of his own (LapitaPolata,
        ///   19/10/2026): an entry written for a mother must not reach her daughter. A hero spawned from a template
        ///   (a mercenary captain, a wanderer) has no parents, and his template stays the key it was meant to be.
        /// </summary>
        public static string TemplateKey(string templateId, bool hasParents)
            => hasParents ? null : templateId;

        /// <summary>All authored entries, in load order. Lets a diagnostic enumerate what was authored.</summary>
        public IReadOnlyList<CharacterOverride> All => _overrides;

        /// <summary>
        ///   The override for a hero, or null when none applies. A matching <see cref="HeroOverrideLookup.HeroId" />
        ///   (StringId) always wins: the unambiguous key. Next, an entry whose id is the hero's
        ///   <see cref="HeroOverrideLookup.TemplateId" /> (tashmetu, Nexus, 26/09/2026: a mercenary captain's StringId and name are
        ///   rolled per campaign, "CharacterObject_2526", "Yahya of the Beni Zilal", but the template he is spawned
        ///   from, "spc_skolderbrotva_leader_0", is the same in every game, so a pack keyed on it works everywhere).
        ///   Otherwise the first case-insensitive match on <see cref="HeroOverrideLookup.HeroName" /> is used, UNLESS several
        ///   entries share that name and none pins an id: that homonym can't be told apart, so it returns null (the
        ///   caller should warn the author to add an <c>id</c>) rather than silently apply the wrong one.
        /// </summary>
        public CharacterOverride Resolve(HeroOverrideLookup facts) => Resolve(facts.HeroId, facts.HeroName, facts.TemplateId);

        // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
        // way round and still compile (swap-risk ratchet, 14/11/2026).
        private CharacterOverride Resolve(string heroId, string heroName, string templateId = null)
        {
            if (!string.IsNullOrWhiteSpace(heroId))
            {
                CharacterOverride byId = _overrides.FirstOrDefault(o =>
                    !string.IsNullOrWhiteSpace(o.Id) &&
                    string.Equals(o.Id, heroId, StringComparison.OrdinalIgnoreCase));
                if (byId != null) return byId;
            }

            if (!string.IsNullOrWhiteSpace(templateId))
            {
                CharacterOverride byTemplate = _overrides.FirstOrDefault(o =>
                    !string.IsNullOrWhiteSpace(o.Id) &&
                    string.Equals(o.Id, templateId, StringComparison.OrdinalIgnoreCase));
                if (byTemplate != null) return byTemplate;
            }

            if (string.IsNullOrWhiteSpace(heroName)) return null;

            List<CharacterOverride> byName = _overrides
                .Where(o => string.Equals(o.Hero, heroName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byName.Count == 1) return byName[0];
            return null; // none, or an ambiguous homonym with no id to disambiguate
        }

        /// <summary>
        ///   A new set with <paramref name="ov"/> added, replacing any existing entry that would resolve to
        ///   the same hero: one sharing its <see cref="CharacterOverride.Id"/>, or (when the injected override
        ///   carries no id) one sharing its <see cref="CharacterOverride.Hero"/> name. Lets a runtime/console
        ///   injection override a file entry for one NPC without wiping the rest. A null <paramref name="ov"/>
        ///   returns this set unchanged.
        /// </summary>
        public CharacterOverrideSet With(CharacterOverride ov)
        {
            if (ov == null) return this;

            bool SameTarget(CharacterOverride o) =>
                (!string.IsNullOrWhiteSpace(ov.Id) && string.Equals(o.Id, ov.Id, StringComparison.OrdinalIgnoreCase))
                || (string.IsNullOrWhiteSpace(ov.Id) && !string.IsNullOrWhiteSpace(ov.Hero)
                    && string.Equals(o.Hero, ov.Hero, StringComparison.OrdinalIgnoreCase));

            return new CharacterOverrideSet(_overrides.Where(o => !SameTarget(o)).Concat(new[] { ov }));
        }

        /// <summary>
        ///   This set with a campaign's own entries laid over it (Bhelogan, Discord, 14/11/2026: overrides are global, "but
        ///   runtime ids (CharacterObject_N) are per save, so characters from one playthrough leaked into the next"). Each
        ///   campaign entry replaces the shared one with the same key (its id, or its name when it has no id), as
        ///   <see cref="With"/> does; the rest are added. Null or empty leaves this set unchanged.
        /// </summary>
        public CharacterOverrideSet Overlay(IEnumerable<CharacterOverride> campaignEntries)
        {
            CharacterOverrideSet set = this;
            foreach (CharacterOverride entry in campaignEntries ?? Enumerable.Empty<CharacterOverride>()) set = set.With(entry);

            return set;
        }

        /// <summary>
        ///   The folder name of a campaign's own overrides (<c>Custom/campaigns/&lt;name&gt;/</c>): its unique game id with
        ///   anything but letters, digits, '-' and '_' made '_'. Null for a blank id (no campaign).
        /// </summary>
        public static string CampaignFolderName(string uniqueGameId)
        {
            if (string.IsNullOrWhiteSpace(uniqueGameId)) return null;

            return new string(uniqueGameId.Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        }
    }
}
