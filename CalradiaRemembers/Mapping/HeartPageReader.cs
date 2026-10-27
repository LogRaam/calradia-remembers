// Code written by Gabriel Mailhot, 20/10/2026.
// What the encyclopedia's Heart section reads of the game (Gabriel, 20/10/2026: love stories were kept and shown nowhere):
// the vanilla marriage, which outranks the romantic state since a divorce does not reset it; the wedding day CR recorded;
// the children the two have, never a bastard still hidden; her own lover only once the player knows of him. The words are
// HeartPagePolicy's. Shared by the page and cr.testall heart_page, so the test reads exactly what the player reads.
// At Hardcore the nights (HeartIntimacyPolicy, Gabriel 23/10/2026): the character's own Intimacy memories, which
// compression always keeps. What the player took by force is not love: it is told in Misdeeds (MisdeedsPageReader).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>Reads a hero's bond with the player for the Heart section.</summary>
    internal static class HeartPageReader
    {
        /// <summary>The Heart lines of a hero's page, as shown (cr.testall heart_page reads this seam). Empty when none.</summary>
        internal static IReadOnlyList<string> LinesFor(Hero hero)
        {
            if (hero == null || Hero.MainHero == null) return Array.Empty<string>();

            bool intimate = HeartIntimacyPolicy.Shows(ModSettings.AdultContentLevel);
            if (hero != Hero.MainHero)
            {
                var lines = HeartPagePolicy.Lines(FactsFor(hero), When).ToList();
                string nights = intimate ? HeartIntimacyPolicy.NightsLine(NightsOf(hero), When) : null;
                if (nights != null) lines.Add(nights);
                string tastes = intimate ? TastesLine(hero, NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)) : null;
                if (tastes != null) lines.Add(tastes);

                return lines;
            }

            List<string> recap = Partners().Select(h => HeartPagePolicy.RecapLine(FactsFor(h), When)).Where(l => l != null).ToList();
            if (!intimate) return recap;

            recap.AddRange(Lovers().Select(h => HeartIntimacyPolicy.NightsRecapLine(NightsOf(h), When)).Where(l => l != null));

            return recap;
        }

        /// <summary>The tastes in bed the player has learned of this hero, as the page names them (cr.testall intimate_acts reads this seam).</summary>
        internal static string TastesLine(Hero hero, NpcProfile profile)
        {
            RomanticProfile r = profile?.Romantic;
            if (hero == null || r == null) return null;

            return HeartIntimacyPolicy.TastesLine(new HeartTastesFacts
            {
                IsFemale = hero.IsFemale, Loved = r.LovedActs ?? new List<IntimateAct>(), Disliked = r.DislikedActs ?? new List<IntimateAct>(),
                LessFond = r.LessFondActs ?? new List<IntimateAct>(), Known = r.KnownActs ?? new List<IntimateAct>()
            });
        }

        /// <summary>The hero's bond with the player as another mod reads it (CrRomance.GetStatus), from the page's own facts.</summary>
        internal static RomanceBondReading BondOf(Hero hero)
            => hero == null || Hero.MainHero == null || hero == Hero.MainHero ? new RomanceBondReading() : RomanceBondPolicy.Read(FactsFor(hero));

        /// <summary>Every character with a bond with the player now, by hero id (CrRomance's change check).</summary>
        internal static IReadOnlyDictionary<string, RomanceBondReading> BondsNow()
        {
            var bonds = new Dictionary<string, RomanceBondReading>();
            if (Hero.MainHero == null) return bonds;

            foreach (Hero h in Partners())
            {
                RomanceBondReading r = BondOf(h);
                if (r.Bond != RomanceBondPolicy.None || r.OpenTerms) bonds[h.StringId] = r;
            }

            return bonds;
        }

        /// <summary>A game day as the page names it: "spring 1091". The days are counted from the calendar's start.</summary>
        internal static string When(int day)
        {
            CampaignTime t = CampaignTime.Days(day);

            return $"{t.GetSeasonOfYear.ToString().ToLowerInvariant()} {t.GetYear}";
        }

        // Everyone with a story with the player: the profiles in an engaged state, the spouse, the former spouses.
        private static IEnumerable<Hero> Partners()
        {
            Hero main = Hero.MainHero;
            var seen = new HashSet<Hero>();
            if (main.Spouse != null && seen.Add(main.Spouse)) yield return main.Spouse;
            foreach (Hero ex in main.ExSpouses ?? Enumerable.Empty<Hero>())
                if (ex != null && seen.Add(ex)) yield return ex;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) yield break;

            foreach (string id in store.ListIds())
            {
                NpcProfile p = store.Get(id);
                if (p?.Romantic == null || p.Romantic.Status == RomanticStatus.None && !p.Romantic.IsConsort && !p.Romantic.IsSecretLover) continue;
                Hero h = Hero.FindFirst(x => x.StringId == id);
                if (h != null && h != main && seen.Add(h)) yield return h;
            }
        }

        // Everyone who shared a night with the player, by the first night.
        private static IEnumerable<Hero> Lovers()
        {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return Enumerable.Empty<Hero>();

            var lovers = new List<(Hero Hero, int First)>();
            foreach (string id in store.ListIds())
            {
                int[] days = store.Get(id)?.Events?.Where(e => e != null && e.type == NotableEventType.Intimacy).Select(e => e.gameDay).ToArray();
                if (days == null || days.Length == 0) continue;
                Hero h = Hero.FindFirst(x => x.StringId == id);
                if (h != null && h != Hero.MainHero) lovers.Add((h, days.Min()));
            }

            return lovers.OrderBy(l => l.First).Select(l => l.Hero);
        }

        private static HeartNightsFacts NightsOf(Hero hero)
            => new HeartNightsFacts
            {
                Name = hero.Name?.ToString(),
                NightDays = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.Events?
                                                     .Where(e => e != null && e.type == NotableEventType.Intimacy).Select(e => e.gameDay).ToList()
                            ?? new List<int>()
            };

        private static HeartFacts FactsFor(Hero hero)
        {
            Hero main = Hero.MainHero;
            RomanticProfile r = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.Romantic;
            bool spouse = hero.Spouse == main;
            bool ex = !spouse && main.ExSpouses?.Contains(hero) == true;
            Hero lover = r?.LoverKnownToPlayer == true && !string.IsNullOrEmpty(r.TakenLoverId) ? Hero.FindFirst(x => x.StringId == r.TakenLoverId) : null;

            return new HeartFacts
            {
                Name = hero.Name?.ToString(), IsFemale = hero.IsFemale,
                Status = r?.Status ?? RomanticStatus.None, StatusSinceDay = r?.StatusSinceDay, LoversSinceDay = r?.LoversSinceDay,
                IsSpouse = spouse, IsExSpouse = ex,
                WeddedOnDay = spouse ? WeddingDayBehavior.WeddedOn(hero) : ex ? WeddedThePlayerOn(hero) : null,
                IsConsort = r?.IsConsort == true, IsSecretLover = r?.IsSecretLover == true, SecretLoverSinceDay = r?.SecretLoverSinceDay,
                IsOpenArrangement = r?.IsOpenArrangement == true, AcceptsPlayersOtherPartners = r?.AcceptsPlayersOtherPartners == true,
                KnownLoverName = lover?.Name?.ToString(), AffairDiscovered = r?.AffairDiscovered == true,
                Children = (main.Children ?? new List<Hero>())
                           .Where(c => c != null && (c.Mother == hero || c.Father == hero) && !IsHiddenBastard(c))
                           .OrderBy(c => c.BirthDay.ToDays)
                           .Select(c => new HeartChild {Name = c.Name?.ToString(), BornYear = c.BirthDay.GetYear}).ToList()
            };
        }

        // A former spouse's wedding: WeddingDayBehavior keeps only each hero's LAST wedding, which a remarriage overwrites,
        // so the player's own wedding is read from the chronicle (WorldEventFeeders.OnHeroesMarried). Null when unrecorded.
        private static int? WeddedThePlayerOn(Hero hero)
            => WorldEventStore.Instance?.GetPlayerChronicle()
                              .Where(e => e.Type == WorldEventType.PlayerFeat && e.ActorIds != null && e.ActorIds.Contains(hero.StringId)
                                          && e.ChronicleLine?.Contains("was joined in marriage to") == true)
                              .Select(e => (int?) e.Day).LastOrDefault();

        private static bool IsHiddenBastard(Hero child)
            => BastardBehavior.Instance is { } b && b.IsTracked(child.StringId) && !b.IsDiscovered(child.StringId);

    }
}
