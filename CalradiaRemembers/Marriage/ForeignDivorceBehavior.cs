// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026, Bridges to Remember on 2.6.10): "CR records marriages as world news, but not divorces
// made by other mods (Hero.Spouse set to null). Could CR notice them?" The game raises no event when a spouse is
// cleared, so this looks once a day: the marriages seen the day before are compared with today's
// (ForeignDivorceDetection), and a couple parted while both still live becomes world news and a memory for each.
// CR's own divorces (MarriageDissolutionService) are left to the code that made them. Nothing is saved: the first look
// after a load starts from the marriages as they stand.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>Notices a marriage another mod ended, and has the world and the former spouses know it.</summary>
   public sealed class ForeignDivorceBehavior : CampaignBehaviorBase
   {
      private static Dictionary<string, string> _lastSeen;
      private static HashSet<string> _dissolvedByCr = new HashSet<string>();

      /// <summary>Created for every campaign started or loaded: nothing seen in the last one carries over.</summary>
      public ForeignDivorceBehavior()
      {
         _lastSeen = null;
         _dissolvedByCr = new HashSet<string>();
      }

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _lastSeen = MarriagesNow());
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("ForeignDivorceBehavior.Daily", () => Look()));
      }

      public override void SyncData(IDataStore dataStore) { }

      /// <summary>MarriageDissolutionService's note that CR itself parted this couple, so it is not recorded twice.</summary>
      internal static void NoteDissolvedByCr(Hero a, Hero b)
      {
         if (a?.StringId != null && b?.StringId != null) _dissolvedByCr.Add(ForeignDivorceDetection.PairKey(a.StringId, b.StringId));
      }

      /// <summary>
      ///   cr.testall seam, records nothing: the couples the daily look would find if this hero's marriage had been
      ///   cleared by another mod (crByUs: as though CR had parted them itself).
      /// </summary>
      internal static IReadOnlyList<SpousePair> DebugFindIfParted(Hero hero, bool crByUs)
      {
         Dictionary<string, string> before = MarriagesNow();
         var after = new Dictionary<string, string>(before);
         after.Remove(hero.StringId);
         if (hero.Spouse != null) after.Remove(hero.Spouse.StringId);

         return ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = before, Now = after,
            InTheWorld = new HashSet<string>(Hero.AllAliveHeroes.Where(HeroPresence.IsInTheWorld).Select(h => h.StringId)),
            DissolvedByCr = crByUs ? new HashSet<string> {ForeignDivorceDetection.PairKey(hero.StringId, hero.Spouse?.StringId)} : new HashSet<string>()
         });
      }

      private static IReadOnlyList<SpousePair> Look()
      {
         Dictionary<string, string> now = MarriagesNow();
         if (_lastSeen == null)
         {
            _lastSeen = now;

            return Array.Empty<SpousePair>();
         }

         IReadOnlyList<SpousePair> ended = ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = _lastSeen, Now = now, DissolvedByCr = _dissolvedByCr,
            InTheWorld = new HashSet<string>(Hero.AllAliveHeroes.Where(HeroPresence.IsInTheWorld).Select(h => h.StringId))
         });
         _lastSeen = now;
         _dissolvedByCr.Clear();

         foreach (SpousePair pair in ended) Record(pair);

         return ended;
      }

      private static Dictionary<string, string> MarriagesNow()
      {
         var map = new Dictionary<string, string>();
         foreach (Hero h in Hero.AllAliveHeroes)
            if (h?.StringId != null && h.Spouse?.StringId != null)
               map[h.StringId] = h.Spouse.StringId;

         return map;
      }

      private static void Record(SpousePair pair)
      {
         try
         {
            Hero a = Hero.FindFirst(h => h.StringId == pair.A);
            Hero b = Hero.FindFirst(h => h.StringId == pair.B);
            if (a == null || b == null) return;

            bool playerIn = a == Hero.MainHero || b == Hero.MainHero;
            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Divorce, Day = (int) CampaignTime.Now.ToDays, Magnitude = playerIn ? 40 : 30,
               InvolvesPlayer = playerIn, Summary = ForeignDivorceDetection.Summary(Named(a), Named(b)),
               ActorIds = new List<string> {a.StringId, b.StringId},
               FactionIds = new[] {a.Clan?.StringId, b.Clan?.StringId}.Where(s => s != null).Distinct().ToList()
            });

            Remember(a, b);
            Remember(b, a);
            ModLog.Info($"[MARRIAGE] {a.Name} and {b.Name} are no longer wed (ended outside CR); recorded and remembered.");
         }
         catch (Exception ex) { ModLog.Error($"[MARRIAGE] divorce not recorded for {pair.Key}: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static void Remember(Hero who, Hero former)
      {
         if (who == Hero.MainHero) return;
         CrNpcMemory.AddMemory(new CrMemoryEntry {
            NpcId = who.StringId, Summary = ForeignDivorceDetection.MemoryLine(Named(former)), Kind = "Other", DaysAgo = 0, SourceMod = "CalradiaRemembers"
         });
      }

      private static string Named(Hero h)
         => HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h?.Name?.ToString(), ClanName = h?.Clan?.Name?.ToString()});
   }
}
