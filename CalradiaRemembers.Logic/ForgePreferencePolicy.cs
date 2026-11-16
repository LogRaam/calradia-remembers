// Code written by Gabriel Mailhot, 16/11/2026.
// Which weapons a lord would ask the player to forge for him.
//
// Schmidt (Discord, 16/11/2026): "If you offer to forge weapons for lords or other NPC's, that they don't always ask what
// you have to offer, but with personality and traits have their own preferences of weapon types and stats. I can't see
// a Nord lord having much use for Khuzait short bows, or Monchug using a 2 handed axe in battle for example." The
// craft_weapon task (CraftWeaponPolicy) let the model name any of the smithy's kinds, with nothing about the man asking.
// Gabriel (16/11/2026): "fais la forge maintenant".
//
// The game gives three honest facts: the weapons he carries into battle, the weapon skills he has trained, and his
// people. A lord asks first for a better copy of what he already fights with, then for what his people forge and his
// training suits. If this is wrong, a Khuzait khan asks for a great axe he has never lifted.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The weapon families of the smithy, by the skill that wields them.</summary>
    public enum WeaponSkillFamily
    {
        /// <summary>One hand: sword, axe, mace, dagger.</summary>
        OneHanded,

        /// <summary>Two hands: two-handed sword, axe, mace.</summary>
        TwoHanded,

        /// <summary>Spear, lance, glaive.</summary>
        Polearm,

        /// <summary>Javelins, throwing axes and knives.</summary>
        Throwing
    }

    /// <summary>What the game says about the man who would ask for a forged weapon.</summary>
    public sealed class ForgeAskerFacts
    {
        /// <summary>His culture's id ("sturgia", "khuzait", "nord"...), or null.</summary>
        public string CultureId { get; init; }

        /// <summary>The smithy kinds (CraftWeaponPolicy.Kinds) among the weapons he carries into battle.</summary>
        public IReadOnlyList<string> KindsHeWields { get; init; }

        /// <summary>His weapon skill families, best trained first.</summary>
        public IReadOnlyList<WeaponSkillFamily> SkillsBestFirst { get; init; }
    }

    /// <summary>The smithy kinds a lord would ask for, best suited first (at most <see cref="MaxSuggested" />).</summary>
    public static class ForgePreferencePolicy
    {
        /// <summary>How many kinds the cue names: enough for a choice, few enough to be his.</summary>
        public const int MaxSuggested = 3;

        /// <summary>The kinds a lord would ask to be forged, best suited first; never empty.</summary>
        public static IReadOnlyList<string> Suited(ForgeAskerFacts f)
        {
            IReadOnlyList<string> people = PeopleForge(f?.CultureId);
            IReadOnlyList<WeaponSkillFamily> skills = f?.SkillsBestFirst ?? new List<WeaponSkillFamily>();
            List<string> wields = (f?.KindsHeWields ?? new List<string>()).Where(k => CraftWeaponPolicy.Kinds.Contains(k)).Distinct().ToList();

            var picked = new List<string>();
            void Add(IEnumerable<string> kinds)
            {
                foreach (string k in kinds)
                    if (!picked.Contains(k)) picked.Add(k);
            }

            // What he already fights with, his people's make first: a better copy of his own weapon.
            Add(wields.Where(people.Contains));
            Add(wields);
            // Then what his people forge, in the order of his training: his best skill's kinds first.
            foreach (WeaponSkillFamily skill in skills.Take(2)) Add(KindsOf(skill).Where(people.Contains));
            if (picked.Count == 0) Add(people);

            return picked.Take(MaxSuggested).ToList();
        }

        /// <summary>The smithy kinds a skill family wields.</summary>
        public static IReadOnlyList<string> KindsOf(WeaponSkillFamily family)
            => family switch {
                WeaponSkillFamily.OneHanded => new[] {"sword", "axe", "mace", "dagger"},
                WeaponSkillFamily.TwoHanded => new[] {"two_handed_sword", "two_handed_axe", "two_handed_mace"},
                WeaponSkillFamily.Polearm => new[] {"polearm"},
                _ => new[] {"javelin", "throwing_axe", "throwing_knife"}
            };

        /// <summary>
        ///   What a people's smiths make and its warriors carry, from the game's own troop trees: Vlandian swords and
        ///   lances, Sturgian and Nord axes, Battanian long blades and javelins, Khuzait sabres, maces and lances, Aserai
        ///   swords, maces and javelins, imperial swords, maces and spears. An unknown people: the common blades.
        /// </summary>
        public static IReadOnlyList<string> PeopleForge(string cultureId)
            => (cultureId ?? "").ToLowerInvariant() switch {
                "vlandia" => new[] {"sword", "two_handed_sword", "polearm", "mace", "two_handed_mace"},
                "empire" => new[] {"sword", "mace", "polearm", "javelin", "two_handed_mace"},
                "sturgia" => new[] {"axe", "two_handed_axe", "sword", "polearm", "throwing_axe"},
                "nord" => new[] {"axe", "two_handed_axe", "sword", "polearm", "throwing_axe"},
                "battania" => new[] {"two_handed_sword", "sword", "javelin", "axe", "polearm"},
                "khuzait" => new[] {"sword", "mace", "polearm", "javelin"},
                "aserai" => new[] {"sword", "mace", "polearm", "javelin", "two_handed_mace"},
                _ => new[] {"sword", "axe", "mace", "polearm"}
            };
    }
}
