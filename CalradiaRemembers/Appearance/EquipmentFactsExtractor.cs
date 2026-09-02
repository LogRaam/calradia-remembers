// Code written by Gabriel Mailhot, 29/08/2026.
// Equipment-awareness pillar, engine extraction: reads a live Hero's equipped items (both loadouts, battle AND
// civilian, since a companion's mastercraft blade lives in BattleEquipment while a lady's fine gown lives in
// CivilianEquipment) into the engine-agnostic EquipmentPieceFacts that EquipmentNotabilityPolicy scores and
// phrases. Feeding BOTH sets for every kind is deliberate and safe: the policy already keeps only the single
// BEST piece per kind (GearKind), so a mediocre battle helm sitting alongside a fine civilian cap never doubles
// up in the rendered clause, the finer one simply wins. Never throws (guarded per set): called for every hero
// in a scene, every turn.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>Maps a live <see cref="Hero" />'s equipped items to the pure <see cref="EquipmentPieceFacts" /> list. Never throws.</summary>
    internal static class EquipmentFactsExtractor
    {
        /// <summary>Ammo classes never read as a wielded, quality-worth-voicing weapon (a "masterwork arrow bag" clause is noise).</summary>
        private static readonly HashSet<WeaponClass> AmmoClasses = new HashSet<WeaponClass>
        {
            WeaponClass.Arrow, WeaponClass.Bolt, WeaponClass.SlingStone, WeaponClass.Cartridge,
            WeaponClass.Stone, WeaponClass.Boulder, WeaponClass.BallistaBoulder, WeaponClass.BallistaStone
        };

        public static List<EquipmentPieceFacts> Extract(Hero hero)
        {
            var result = new List<EquipmentPieceFacts>();
            if (hero == null) return result;

            try { ExtractInto(hero.BattleEquipment, result); }
            catch { /* never throw: called per hero per turn */ }

            try { ExtractInto(hero.CivilianEquipment, result); }
            catch { /* never throw: called per hero per turn */ }

            return result;
        }

        /// <summary>
        ///   Reads only the SCENE-APPROPRIATE loadout (player report: a battle sword read in a tavern where the
        ///   player had narrated no armour). In a settlement the game shows the civilian outfit; in the field, the
        ///   battle harness; EquipmentLoadoutPolicy picks which from the scene and this reads that set. Falls back
        ///   to the other set ONLY when the chosen one is unset (a hero with no civilian outfit assigned), so a
        ///   real civilian outfit never lets the battle plate leak into a town scene.
        /// </summary>
        public static List<EquipmentPieceFacts> Extract(Hero hero, bool preferCivilian)
        {
            var result = new List<EquipmentPieceFacts>();
            if (hero == null) return result;

            Equipment primary = preferCivilian ? hero.CivilianEquipment : hero.BattleEquipment;
            Equipment fallback = preferCivilian ? hero.BattleEquipment : hero.CivilianEquipment;

            try { ExtractInto(primary, result); }
            catch { /* never throw: called per hero per turn */ }

            if (result.Count == 0)
                try { ExtractInto(fallback, result); }
                catch { /* never throw */ }

            return result;
        }

        #region private

        private static void ExtractInto(Equipment eq, List<EquipmentPieceFacts> into)
        {
            if (eq == null) return;

            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i <= EquipmentIndex.Horse; i++)
            {
                EquipmentElement element = eq[i];
                ItemObject item = element.Item;
                if (item == null) continue;

                GearKind? kind = MapKind(item, i);
                if (kind == null) continue;

                string noun = MapNoun(item, kind.Value);
                GearQuality quality = ResolveQuality(element.ItemModifier);

                into.Add(new EquipmentPieceFacts(kind.Value, noun, quality, (int) item.Tier, element.ItemValue));
            }
        }

        /// <summary>The horse slot is always a Mount; every other slot is read off the item's own type (a weapon
        /// component means Weapon, unless it is pure ammo, which carries no wielded-quality story worth voicing).</summary>
        private static GearKind? MapKind(ItemObject item, EquipmentIndex slot)
        {
            if (slot == EquipmentIndex.Horse) return GearKind.Mount;

            if (item.HasWeaponComponent)
            {
                WeaponClass wc = item.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;

                return AmmoClasses.Contains(wc) ? (GearKind?) null : GearKind.Weapon;
            }

            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return GearKind.Armor;
                default:
                    return null;
            }
        }

        private static string MapNoun(ItemObject item, GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapon:
                    return WeaponNoun(item.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined);
                case GearKind.Mount:
                    return "horse";
                default:
                    // A civilian garment (a dress, a robe) reads as "attire", not "armor" (the player's own
                    // cited case in the report). Every armor slot folds to one of these two mass nouns.
                    return item.IsCivilian ? "attire" : "armor";
            }
        }

        private static string WeaponNoun(WeaponClass wc)
        {
            switch (wc)
            {
                case WeaponClass.Dagger: return "dagger";
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword: return "sword";
                case WeaponClass.OneHandedAxe:
                case WeaponClass.TwoHandedAxe:
                case WeaponClass.ThrowingAxe: return "axe";
                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                case WeaponClass.Pick: return "mace";
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm: return "spear";
                case WeaponClass.Bow: return "bow";
                case WeaponClass.Crossbow: return "crossbow";
                case WeaponClass.Javelin: return "javelin";
                case WeaponClass.ThrowingKnife: return "knife";
                case WeaponClass.Sling: return "sling";
                case WeaponClass.SmallShield:
                case WeaponClass.LargeShield: return "shield";
                default: return "weapon";
            }
        }

        /// <summary>
        ///   The native ItemQuality enum name is the game's own authoritative band; passed through as a plain
        ///   string so the pure EquipmentModifierQualityPolicy never touches a TaleWorlds type. An unmodified
        ///   slot (null modifier) reads Common outright, matching the game's own default.
        /// </summary>
        private static GearQuality ResolveQuality(ItemModifier modifier)
            => modifier == null
                ? GearQuality.Common
                : EquipmentModifierQualityPolicy.FromModifier(modifier.ItemQuality.ToString(), modifier.PriceMultiplier);

        #endregion
    }
}
