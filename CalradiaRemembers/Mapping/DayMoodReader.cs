// Code written by Gabriel Mailhot, 30/09/2026.
// THE MOOD OF THE DAY, increment 2: the host side of the draw. Reads a character's day off the live game into
// DayMoodReadings, lets DayMoodFactsPolicy (Logic, tested) turn them into the facts the draw weighs, and draws with a
// seed of hero and day, so the same man all day and another man tomorrow (ROADMAP, Rulings 2 and 3).

#region

using System;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Mood;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Draws a character's mood of the day from the live game.</summary>
   public static class DayMoodReader
   {
      /// <summary>The day's mood and whether wit may surface, or (null, false) when none can be drawn.</summary>
      public static (DayMood? Mood, bool Wit) Draw(Hero hero)
      {
         (DayMood? mood, bool wit, DayCause _) = DrawDay(hero);

         return (mood, wit);
      }

      /// <summary>
      ///   The day's mood, whether wit may surface, and why the day is what it is (Lordfadooboo, 18/10/2026): the fact
      ///   that leaned it, else a trifle (MoodCause). (null, false, null) when none can be drawn.
      /// </summary>
      public static (DayMood? Mood, bool Wit, DayCause Cause) DrawDay(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero) return (null, false, null);

         try
         {
            MoodFacts facts = DayMoodFactsPolicy.From(Read(hero));
            int seed = MoodOfTheDay.SeedFor(hero.StringId, (int) CampaignTime.Now.ToDays);
            DayMood mood = MoodOfTheDay.Draw(facts, seed);

            return (mood, MoodOfTheDay.WitIsPermitted(mood, facts, seed), MoodCause.Of(mood, facts, seed));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MOOD] could not draw {hero.Name}'s day: {ex.GetType().Name}: {ex.Message}");

            return (null, false, null);
         }
      }

      /// <summary>What the game says about this character's day.</summary>
      internal static DayMoodReadings Read(Hero hero)
      {
         (bool atWar, float own, float enemies) = RealmWarFooting.Of(hero.Clan?.Kingdom);
         Settlement here = hero.CurrentSettlement;
         NpcMemoryService.Core.Models.NpcProfile profile = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);

         return new DayMoodReadings {
            Hurt = BodyHurt(hero),
            Treasury = TreasuryOf(hero.Clan),
            RealmAtWar = atWar,
            RealmStrength = own,
            EnemiesStrength = enemies,
            InTownOrCastle = here != null && (here.IsTown || here.IsCastle),
            UnderSiege = here?.IsUnderSiege ?? false,
            HourOfDay = (int) CampaignTime.Now.CurrentHourInDay,
            IsPrisoner = hero.IsPrisoner,
            RecentBlow = Standing.SelfEsteemService.HasRecentBlow(profile),
            RecentGladness = Standing.SelfEsteemService.HasRecentGladness(profile),
            RecentBlowKind = KindOf(profile?.LastPersonalBlowKind),
            RecentBlowSubject = profile?.LastPersonalBlowSubject,
            RecentGladnessKind = KindOf(profile?.LastPersonalGladnessKind),
            RecentGladnessSubject = profile?.LastPersonalGladnessSubject,
            SelfEsteem = Standing.SelfEsteemService.BandFor(hero, profile)
         };
      }

      private static SelfEsteemEvent? KindOf(string name) => Enum.TryParse(name, out SelfEsteemEvent e) ? e : (SelfEsteemEvent?) null;

      // The same bands the own_body and house_means packs speak, read through their own policies.
      private static HurtBand BodyHurt(Hero hero) => EncounterContextBuilder.ReadHurtFor(hero);

      private static TreasuryBand TreasuryOf(Clan clan) => EncounterContextBuilder.ReadTreasuryFor(clan);
   }
}
