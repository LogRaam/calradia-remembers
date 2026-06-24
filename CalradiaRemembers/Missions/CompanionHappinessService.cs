// Code written by Gabriel Mailhot, 23/06/2026.
// Engine glue around the pure CompanionHappiness model: reads/writes a companion's happiness on their
// stored NpcProfile and applies the named drivers. The bands, magnitudes, and drift are pure and tested
// in CalradiaRemembers.Logic; this only moves the score between the game and that logic.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Missions
{
    /// <summary>The single place a companion's happiness is read from / written to their profile.</summary>
    public static class CompanionHappinessService
    {
        /// <summary>The companion's current happiness, or the neutral baseline when they have no profile yet.</summary>
        public static int Get(Hero hero)
            => ProfileOf(hero)?.Happiness ?? CompanionHappiness.Default;

        /// <summary>Applies a driver to the companion's stored happiness (no-op when they have no profile).</summary>
        public static void ApplyDriver(Hero hero, HappinessDriver driver)
        {
            NpcProfile p = ProfileOf(hero);
            if (p == null) return;

            int before = p.Happiness;
            p.Happiness = CompanionHappiness.ApplyDriver(p.Happiness, driver);
            if (p.Happiness == before) return;

            Store?.Set(p);
            ModLog.Info($"[HAPPINESS] {hero.Name}: {before}→{p.Happiness} ({driver}).");
        }

        /// <summary>Eases the companion's happiness toward the baseline by one step (idle/relaxation tick).</summary>
        public static void Drift(Hero hero, int step)
        {
            NpcProfile p = ProfileOf(hero);
            if (p == null) return;

            int after = CompanionHappiness.DriftToBaseline(p.Happiness, step);
            if (after == p.Happiness) return;
            p.Happiness = after;
            Store?.Set(p);
        }

        /// <summary>Sets the score directly (console test path), clamped.</summary>
        public static void Set(Hero hero, int value)
        {
            NpcProfile p = ProfileOf(hero);
            if (p == null) return;
            p.Happiness = CompanionHappiness.Clamp(value);
            Store?.Set(p);
        }

        private static BannerlordNpcMemoryStore Store => NpcMemoryCampaignBehavior.Instance?.Store;

        private static NpcProfile ProfileOf(Hero hero)
            => hero == null ? null : Store?.Get(hero.StringId);
    }
}
