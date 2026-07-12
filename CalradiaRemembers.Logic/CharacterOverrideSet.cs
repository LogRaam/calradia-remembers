// Code written by Gabriel Mailhot, 22/06/2026.
// Resolves which authored override applies to a given hero. Pure so the matching rule — id wins over
// name, name is case-insensitive, and a homonym without an id is reported rather than guessed — stays
// tested; a silent mis-resolution would hand one lord another lord's authored personality.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
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
        ///   The override for a hero, or null when none applies. A matching <paramref name="heroId"/>
        ///   (StringId) always wins — the unambiguous key. Otherwise the first case-insensitive match on
        ///   <paramref name="heroName"/> is used, UNLESS several entries share that name and none pins an
        ///   id: that homonym can't be told apart, so it returns null (the caller should warn the author
        ///   to add an <c>id</c>) rather than silently apply the wrong one.
        /// </summary>
        public CharacterOverride Resolve(string heroId, string heroName)
        {
            if (!string.IsNullOrWhiteSpace(heroId))
            {
                CharacterOverride byId = _overrides.FirstOrDefault(o =>
                    !string.IsNullOrWhiteSpace(o.Id) &&
                    string.Equals(o.Id, heroId, StringComparison.OrdinalIgnoreCase));
                if (byId != null) return byId;
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
    }
}
