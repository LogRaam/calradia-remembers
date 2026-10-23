// Code written by Gabriel Mailhot, 20/10/2026.
// What the encyclopedia's Heart section reads of the game (Gabriel, 20/10/2026: love stories were kept and shown nowhere):
// the vanilla marriage, which outranks the romantic state since a divorce does not reset it; the wedding day CR recorded;
// the children the two have, never a bastard still hidden; her own lover only once the player knows of him. The words are
// HeartPagePolicy's. Shared by the page and cr.testall heart_page, so the test reads exactly what the player reads.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
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

            if (hero != Hero.MainHero) return HeartPagePolicy.Lines(FactsFor(hero), When);

            return Partners().Select(h => HeartPagePolicy.RecapLine(FactsFor(h), When)).Where(l => l != null).ToList();
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
