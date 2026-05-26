// Code written by Gabriel Mailhot, 24/05/2026.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Builds a deterministic <see cref="RomanticProfile"/> for a Bannerlord
    ///   <see cref="Hero"/>. Seeded by <see cref="Hero.StringId"/> so the same
    ///   NPC always rolls the same orientation, preferences, and kinks even
    ///   if the player encounters them fresh after a reload before saving.
    ///
    ///   Sprint 8.2: infrastructure only. The textual sketches (RelationalSketch,
    ///   IntimateSketch) are left empty here and will be populated by Sprint 8.3,
    ///   which will pair them with the archetype derived from traits.
    /// </summary>
    public static class RomanticProfileBuilder
    {
        public static RomanticProfile Build(Hero hero)
        {
            // Deterministic RNG: same StringId always produces the same rolls.
            int seed = hero.StringId.GetHashCode();
            Random rng = new Random(seed);

            // Build Traits view from Hero — used both for kink correlation
            // and (later, in Sprint 8.3) for archetype selection.
            Traits traits = new()
            {
                Calculating = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Calculating)),
                Generosity  = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Generosity)),
                Mercy       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Mercy)),
                Honor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Honor)),
                Valor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Valor))
            };

            return new RomanticProfile
            {
                ArchetypeName = "",            // Sprint 8.3
                RelationalSketch = "",         // Sprint 8.3
                IntimateSketch = "",           // Sprint 8.3
                Orientation = DetermineOrientation(rng),
                IsFemale = hero.IsFemale,
                Preferences = DeterminePreferences(rng, traits),
                Kinks = DetermineKinks(rng, traits)
            };
        }

        #region private

        // ── Orientation ───────────────────────────────────────────────────────

        /// <summary>
        ///   Approximate distribution (tweakable):
        ///     70% Hetero, 15% Bi, 10% Homo, 3% Pan, 2% Ace
        /// </summary>
        private static SexualOrientation DetermineOrientation(Random rng)
        {
            int roll = rng.Next(100);
            if (roll <  70) return SexualOrientation.Heterosexual;
            if (roll <  85) return SexualOrientation.Bisexual;
            if (roll <  95) return SexualOrientation.Homosexual;
            if (roll <  98) return SexualOrientation.Pansexual;
            return SexualOrientation.Asexual;
        }

        // ── Preferences ───────────────────────────────────────────────────────

        /// <summary>
        ///   Most NPCs receive 2 preferences chosen from a trait-influenced pool.
        ///   Preferences are stable patterns of attachment, so we always assign some
        ///   (unlike Kinks, where 'None' is the most common outcome).
        /// </summary>
        private static List<RomanticPreference> DeterminePreferences(Random rng, Traits traits)
        {
            HashSet<RomanticPreference> pool = BuildPreferencePool(traits);
            int count = rng.Next(1, 3); // 1 or 2 preferences
            return PickDistinct(pool, count, rng);
        }

        private static HashSet<RomanticPreference> BuildPreferencePool(Traits t)
        {
            HashSet<RomanticPreference> pool = new()
            {
                // Power dynamics — always available
                RomanticPreference.Switch
            };

            // Honor influences fidelity stance
            if (t.Honor >=  1) pool.Add(RomanticPreference.MonogamousStrict);
            if (t.Honor >=  1) pool.Add(RomanticPreference.Devoted);
            if (t.Honor <= -1) pool.Add(RomanticPreference.MonogamousFlexible);
            if (t.Honor <= -1) pool.Add(RomanticPreference.Polyamorous);
            if (t.Honor <= -1) pool.Add(RomanticPreference.Casual);
            if (t.Honor ==  0) pool.Add(RomanticPreference.MonogamousFlexible);

            // Mercy influences dominance leaning
            if (t.Mercy <= -1) pool.Add(RomanticPreference.Dominant);
            if (t.Mercy >=  1) pool.Add(RomanticPreference.Submissive);
            if (t.Mercy >=  1) pool.Add(RomanticPreference.Devoted);

            // Calculating influences pace and emotional reserve
            if (t.Calculating >=  1) pool.Add(RomanticPreference.SlowBurn);
            if (t.Calculating >=  1) pool.Add(RomanticPreference.Reserved);
            if (t.Calculating <= -1) pool.Add(RomanticPreference.Intense);

            // Valor influences intensity
            if (t.Valor >=  1) pool.Add(RomanticPreference.Intense);
            if (t.Valor <= -1) pool.Add(RomanticPreference.Reserved);

            // Generosity influences possessiveness vs independence
            if (t.Generosity >=  1) pool.Add(RomanticPreference.Devoted);
            if (t.Generosity <= -1) pool.Add(RomanticPreference.Independent);
            if (t.Generosity <= -1) pool.Add(RomanticPreference.Possessive);

            // Cross-trait combinations
            if (t.Mercy <= -1 && t.Calculating >= 1) pool.Add(RomanticPreference.Possessive);
            if (t.Valor >=  1 && t.Honor <= -1)     pool.Add(RomanticPreference.Casual);

            return pool;
        }

        // ── Kinks ─────────────────────────────────────────────────────────────

        /// <summary>
        ///   Distribution: 60% of NPCs have 0 kinks, 25% have 1, 12% have 2, 3% have 3.
        ///   Pool is trait-influenced so kinks correlate plausibly with personality.
        /// </summary>
        private static List<Kink> DetermineKinks(Random rng, Traits traits)
        {
            int roll = rng.Next(100);
            int count;
            if      (roll <  60) count = 0;
            else if (roll <  85) count = 1;
            else if (roll <  97) count = 2;
            else                 count = 3;

            if (count == 0) return new List<Kink>();

            HashSet<Kink> pool = BuildKinkPool(traits);
            return PickDistinctKinks(pool, count, rng);
        }

        private static HashSet<Kink> BuildKinkPool(Traits t)
        {
            HashSet<Kink> pool = new()
            {
                // Always available — these don't strongly correlate with any single trait
                Kink.Roleplay,
                Kink.PublicAffection,
                Kink.Voyeurism,
                Kink.Exhibitionism
            };

            // Mercy: cruel NPCs lean toward dominance/sadism; merciful toward submission/masochism
            if (t.Mercy <= -1)
            {
                pool.Add(Kink.Dominance);
                pool.Add(Kink.Sadism);
                pool.Add(Kink.BondageGiving);
                pool.Add(Kink.PowerImbalance);
            }
            if (t.Mercy >=  1)
            {
                pool.Add(Kink.Submission);
                pool.Add(Kink.BondageReceiving);
                pool.Add(Kink.Masochism);
            }

            // Honor: dishonorable NPCs more open to power imbalance dynamics
            if (t.Honor <= -1)
            {
                pool.Add(Kink.PowerImbalance);
                pool.Add(Kink.SwitchTendencies);
            }

            // Calculating: dominance and possessiveness
            if (t.Calculating >= 1)
            {
                pool.Add(Kink.Dominance);
                pool.Add(Kink.Possessiveness);
                pool.Add(Kink.BondageGiving);
            }

            // Valor: bolder kinks (exhibitionism, intensity)
            if (t.Valor >=  1)
            {
                pool.Add(Kink.Exhibitionism);
                pool.Add(Kink.Masochism);
            }

            // Generosity affects possessiveness vs sharing
            if (t.Generosity <= -1) pool.Add(Kink.Possessiveness);

            return pool;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<RomanticPreference> PickDistinct(
            HashSet<RomanticPreference> pool, int count, Random rng)
        {
            List<RomanticPreference> source = new(pool);
            Shuffle(source, rng);
            int take = Math.Min(count, source.Count);
            return source.GetRange(0, take);
        }

        private static List<Kink> PickDistinctKinks(HashSet<Kink> pool, int count, Random rng)
        {
            List<Kink> source = new(pool);
            Shuffle(source, rng);
            int take = Math.Min(count, source.Count);
            return source.GetRange(0, take);
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static int ConvertLevel(int score)
        {
            if (score <= -4000) return -2;
            if (score <= -1000) return -1;
            if (score >=  4000) return  2;
            if (score >=  1000) return  1;
            return 0;
        }

        #endregion
    }
}
