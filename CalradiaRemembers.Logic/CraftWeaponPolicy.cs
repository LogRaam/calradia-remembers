// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "could even pull inspiration from the vanilla quests and add some of those, like a
// blacksmithing quest." A lord asks the player to forge him a weapon of a kind he names; Gabriel ruled the same day
// that the KIND is part of the deal ("fais la forge avec le type d'arme"). The game marks every weapon the player
// forges (ItemObject.IsCraftedByPlayer), and each weapon carries its class (WeaponClass), so the hand-over is checked
// against both: forged by the player, and the kind asked. A bought sword, or a forged axe for a sword, does not count.
// The weapon then goes to the lord, who wields it.

namespace CalradiaRemembers.Logic
{
    /// <summary>The rules of a craft_weapon task. Pure.</summary>
    public static class CraftWeaponPolicy
    {
        /// <summary>The prefix of the supply key for a forged weapon ("forged:sword"), distinct from every other supply key.</summary>
        public const string KeyPrefix = "forged:";

        /// <summary>The weapon kinds a lord may ask to be forged: the smithy's own families.</summary>
        public static readonly string[] Kinds =
        {
            "sword", "two_handed_sword", "axe", "two_handed_axe", "mace", "two_handed_mace", "polearm", "dagger", "javelin",
            "throwing_axe", "throwing_knife"
        };

        /// <summary>The kind a lord's word names, or null when it names no weapon the smithy makes.</summary>
        public static string Normalize(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return null;

            switch (word.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' '))
            {
                case "sword": case "blade": case "one handed sword": case "arming sword": case "sabre": case "saber": case "falchion":
                    return "sword";
                case "two handed sword": case "greatsword": case "great sword": case "longsword": case "long sword": case "claymore":
                    return "two_handed_sword";
                case "axe": case "hatchet": case "one handed axe":
                    return "axe";
                case "two handed axe": case "great axe": case "greataxe": case "battle axe": case "battleaxe":
                    return "two_handed_axe";
                case "mace": case "club": case "one handed mace":
                    return "mace";
                case "two handed mace": case "maul": case "warhammer": case "war hammer": case "hammer":
                    return "two_handed_mace";
                case "polearm": case "spear": case "pike": case "lance": case "glaive": case "halberd": case "bardiche": case "voulge":
                    return "polearm";
                case "dagger": case "knife": case "dirk":
                    return "dagger";
                case "javelin": case "javelins":
                    return "javelin";
                case "throwing axe": case "throwing axes":
                    return "throwing_axe";
                case "throwing knife": case "throwing knives":
                    return "throwing_knife";
                default:
                    return null;
            }
        }

        /// <summary>The kind of a weapon, from the name of its game WeaponClass; null for anything the smithy does not make.</summary>
        public static string KindOfClass(string weaponClass)
            => weaponClass switch {
                "OneHandedSword" => "sword",
                "TwoHandedSword" => "two_handed_sword",
                "OneHandedAxe" => "axe",
                "TwoHandedAxe" => "two_handed_axe",
                "Mace" => "mace",
                "TwoHandedMace" => "two_handed_mace",
                "OneHandedPolearm" or "TwoHandedPolearm" or "LowGripPolearm" => "polearm",
                "Dagger" => "dagger",
                "Javelin" => "javelin",
                "ThrowingAxe" => "throwing_axe",
                "ThrowingKnife" => "throwing_knife",
                _ => null
            };

        /// <summary>The supply key for this kind.</summary>
        public static string Key(string kind) => string.IsNullOrEmpty(kind) ? null : KeyPrefix + kind;

        /// <summary>The kind in a forge key, or null when the key is another supply.</summary>
        public static string KindOfKey(string key)
            => key != null && key.StartsWith(KeyPrefix, System.StringComparison.Ordinal) && key.Length > KeyPrefix.Length
                ? key.Substring(KeyPrefix.Length)
                : null;

        /// <summary>Whether this weapon answers the task: forged by the player, and of the kind asked.</summary>
        public static bool Answers(bool forgedByThePlayer, string weaponClass, string key)
        {
            string asked = KindOfKey(key);

            return forgedByThePlayer && asked != null && KindOfClass(weaponClass) == asked;
        }

        /// <summary>The kind in words, for the player and the prompt ("two-handed sword").</summary>
        public static string Label(string kind) => string.IsNullOrEmpty(kind) ? "weapon" : kind.Replace("two_handed_", "two-handed ").Replace('_', ' ');
    }
}
