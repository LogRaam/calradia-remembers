// Code written by Gabriel Mailhot, 14/11/2026.
// Women in the ranks. Gabriel, 14/11/2026: "J'aimerais une config par culture où on peut déterminer la proportion de femmes
// dans les soldats. Et pour CR de base ... Sturgia, Battanian et Khuzait ayant la plus forte proportion, Nords et South
// Empire la moins forte. Les autres cultures sont patriarcales." (25% and 10%, agreed the same day.) The game's troops are a
// count per troop type, not people, so the choice is made for each soldier as he enters a battle. The three empires share
// one culture, so a realm entry (empire_s) wins over the culture for its armies. The draw comes from the soldier's own seed,
// never from the battle's random stream, so the same soldier is drawn the same way and nothing else in the battle moves.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Troops
{
    /// <summary>The share of women per culture and per realm, in percent (women_in_ranks.json).</summary>
    public sealed class WomenInRanksTable
    {
        /// <summary>By the game's culture id (sturgia, battania...).</summary>
        public IReadOnlyDictionary<string, int> Cultures { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>By the game's kingdom id (empire_s...): wins over the culture for the soldiers of that realm's armies.</summary>
        public IReadOnlyDictionary<string, int> Realms { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>One soldier about to enter a battle.</summary>
    public sealed class RankSoldierFacts
    {
        /// <summary>The troop's culture id.</summary>
        public string CultureId { get; init; }

        /// <summary>The kingdom id of the army the soldier fights in, or null (bandits, a party of no realm).</summary>
        public string RealmId { get; init; }

        /// <summary>The troop type's id, mixed into the draw so two troop types with one seed are drawn apart.</summary>
        public string TroopId { get; init; }

        /// <summary>The soldier's own seed (the game's agent origin): the same soldier is always drawn the same way.</summary>
        public int Seed { get; init; }

        /// <summary>A hero (a lord, a companion, the player): a person with their own body, never changed.</summary>
        public bool IsHero { get; init; }

        /// <summary>The troop is already a woman (the game's or another mod's): left as it is.</summary>
        public bool IsAlreadyFemale { get; init; }

        /// <summary>The culture has a woman's face to give her; without it she would get a man's face on a woman's body.</summary>
        public bool HasFemaleFace { get; init; }
    }

    /// <summary>Whether a soldier entering a battle is a woman, and the frame she is drawn with. Pure.</summary>
    public static class WomenInRanksPolicy
    {
        // EvilPoptart1987 (Nexus, 15/11/2026): "the women in the ranks spawns coming in with ridiculously tiny heads". Her body
        // was drawn in the range of her culture's townswomen, whose weight and build reach 0.95 and 0.97 (a stout, heavy
        // peasant), so a small woman's head sat on a wide, muscled frame under a man's armour. Gabriel (15/11/2026): "il
        // faudrait que le body des femmes soient mince et beaucoup moins muscles." Both are drawn in this lean band instead,
        // still spread by her own draw so two women soldiers do not share one frame.

        /// <summary>The leanest weight and build a woman soldier is given (the game's 0..1 body sliders).</summary>
        public const float LeanFrameLowest = 0.05f;

        /// <summary>The heaviest weight and build a woman soldier is given (the game's 0..1 body sliders).</summary>
        public const float LeanFrameHighest = 0.30f;

        /// <summary>
        ///   A woman soldier's weight or build: the value her culture's woman template drew (0..1), carried into the lean band
        ///   <see cref="LeanFrameLowest" />..<see cref="LeanFrameHighest" /> so a heavier draw stays a little heavier, never stout.
        /// </summary>
        public static float LeanFrame(float drawn)
        {
            float t = float.IsNaN(drawn) ? 0f : drawn < 0f ? 0f : drawn > 1f ? 1f : drawn;

            return LeanFrameLowest + (LeanFrameHighest - LeanFrameLowest) * t;
        }

        /// <summary>The share for a soldier of <paramref name="cultureId" /> in an army of <paramref name="realmId" />: the realm's when listed, else the culture's, else 0; held to 0..100.</summary>
        public static int PercentFor(WomenInRanksTable table, string cultureId, string realmId)
        {
            if (table == null) return 0;

            int percent = 0;
            if (!string.IsNullOrEmpty(realmId) && table.Realms != null && table.Realms.TryGetValue(realmId, out int byRealm)) percent = byRealm;
            else if (!string.IsNullOrEmpty(cultureId) && table.Cultures != null && table.Cultures.TryGetValue(cultureId, out int byCulture)) percent = byCulture;

            return percent < 0 ? 0 : percent > 100 ? 100 : percent;
        }

        /// <summary>Whether this soldier is a woman: never a hero, a troop already a woman, or one whose culture has no woman's face.</summary>
        public static bool IsWoman(WomenInRanksTable table, RankSoldierFacts soldier)
        {
            if (soldier == null || soldier.IsHero || soldier.IsAlreadyFemale || !soldier.HasFemaleFace) return false;

            int percent = PercentFor(table, soldier.CultureId, soldier.RealmId);
            if (percent <= 0) return false;
            if (percent >= 100) return true;

            return Roll(soldier.TroopId, soldier.Seed) < percent;
        }

        // 0..99, from the troop id and the soldier's seed (FNV-1a, then a mix): stable across runs and machines.
        private static int Roll(string troopId, int seed)
        {
            uint h = Hashing.StableHash.Of(troopId);

            h ^= unchecked((uint) seed);
            h *= 0x9E3779B1;
            h ^= h >> 15;
            h *= 0x85EBCA6B;
            h ^= h >> 13;

            return (int) (h % 100);
        }
    }
}
