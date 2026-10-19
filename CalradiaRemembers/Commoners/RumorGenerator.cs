// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Commoners
{
   /// <summary>
   ///   Derives street-level rumor fragments from live campaign state for a given
   ///   settlement. Sources: captured lords, recent lord deaths, player presence,
   ///   prosperity mood, and active wars. Each call returns a randomised selection
   ///   of up to four fragments — never the same list twice.
   /// </summary>
   public static class RumorGenerator
   {
      // Recent-death window: one Calradian season.
      private static int DeathWindowDays => WorldCalendar.DaysPerSeason;

      // Maximum entries scanned in DeadOrDisabledHeroes to avoid frame spikes in
      // long campaigns where the collection can be very large.
      private const int MaxDeathScan = 300;

      private static readonly Random _rng = new();

      /// <summary>
      ///   Generates up to four rumor fragments for <paramref name="settlement" />
      ///   as of <paramref name="today" /> (absolute game day from
      ///   <c>CampaignTime.Now.ToDays</c>). Returns null when no relevant events
      ///   are found, so the caller can skip injecting the rumor block entirely.
      /// </summary>
      public static string Generate(Settlement settlement, int today)
      {
         if (settlement == null) return null;

         var pool = new List<string>();

         TryWorldEvents(pool, settlement, today);
         TryCapturedLords(pool, settlement);
         TryRecentDeaths(pool, today);
         TryPlayerPresence(pool, settlement);
         TryProsperityRumor(pool, settlement);
         TryWarRumor(pool, settlement);

         if (pool.Count == 0) return null;

         Shuffle(pool);
         int take = Math.Min(4, pool.Count);

         return string.Join("\n", pool.Take(take).Select(r => $"- {r}"));
      }

      #region private

      private static string DeathCause(Hero dead)
      {
         switch (dead.DeathMark.ToString())
         {
            case "DiedInBattle": return "fallen in battle";
            case "DiedOfOldAge": return "died of old age";
            case "Executed": return "been executed";
            case "Murdered": return "been murdered";
            default: return "died";
         }
      }

      private static string DeathRecency(int daysAgo)
      {
         if (daysAgo == 0) return "just this very day";
         if (daysAgo == 1) return "just yesterday";

         return $"{daysAgo} days past";
      }

      private static void Shuffle(List<string> items)
      {
         for (int i = items.Count - 1; i > 0; i--)
         {
            int j = _rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
         }
      }

      private static void TryCapturedLords(List<string> pool, Settlement settlement)
      {
         Kingdom localKingdom = settlement.OwnerClan?.Kingdom;
         var added = 0;

         foreach (Hero hero in Hero.AllAliveHeroes)
         {
            if (!hero.IsLord || !hero.IsPrisoner || hero == Hero.MainHero) continue;

            // Prefer lords the locals would know — same kingdom or any lord if no kingdom.
            bool isLocal = localKingdom != null && hero.Clan?.Kingdom == localKingdom;

            if (!isLocal && localKingdom != null && _rng.NextDouble() > 0.35) continue;

            pool.Add($"They say {hero.Name} has been taken prisoner.");

            if (++added >= 2) break;
         }
      }

      private static void TryPlayerPresence(List<string> pool, Settlement settlement)
      {
         var player = Hero.MainHero;

         if (player == null || player.CurrentSettlement != settlement) return;

         string title = player.IsFemale
            ? "a lady of some standing"
            : "a lord of some renown";
         string town = settlement.Name?.ToString() ?? "town";
         pool.Add($"There is talk of {title} who arrived in {town} not long ago.");
      }

      private static void TryProsperityRumor(List<string> pool, Settlement settlement)
      {
         try
         {
            float p = settlement.IsTown ? settlement.Town?.Prosperity ?? 0f
               : settlement.IsVillage ? settlement.Village?.Hearth ?? 0f
               : 0f;

            bool isTown = settlement.IsTown;
            string rumor;

            if (isTown)
            {
               if (p >= 6000f) rumor = "The market's been lively of late — trade's good and spirits are high.";
               else if (p >= 4000f) rumor = "Things are ordinary enough around here. No great troubles, no great fortune.";
               else if (p >= 2000f) rumor = "Times are hard. Prices keep rising and fewer merchants are passing through.";
               else rumor = "It's grim here. Families are hungry and folk are leaving if they can.";
            }
            else
            {
               if (p >= 600f) rumor = "The harvest was good this year — the village is doing well enough.";
               else if (p >= 300f) rumor = "We manage. Nothing to complain about, nothing to celebrate.";
               else if (p >= 100f) rumor = "The hearths here are cold more often than not. It's been a lean season.";
               else rumor = "People are struggling badly. Even seed grain is hard to come by.";
            }

            pool.Add(rumor);
         }
         catch
         {
            /* never crash over flavor */
         }
      }

      private static void TryRecentDeaths(List<string> pool, int today)
      {
         var scanned = 0;
         var added = 0;

         foreach (Hero dead in Hero.DeadOrDisabledHeroes)
         {
            if (++scanned > MaxDeathScan) break;

            if (!dead.IsLord || dead.DeathDay.ToDays <= 0) continue;

            int daysAgo = today - (int) dead.DeathDay.ToDays;

            if (daysAgo < 0 || daysAgo > DeathWindowDays) continue;

            string when = DeathRecency(daysAgo);
            string cause = DeathCause(dead);
            pool.Add($"Word came {when} that {dead.Name} has {cause}.");

            if (++added >= 2) break;
         }
      }

      private static void TryWarRumor(List<string> pool, Settlement settlement)
      {
         try
         {
            Kingdom kingdom = settlement.OwnerClan?.Kingdom;

            if (kingdom == null) return;

            var enemies = new List<string>();
            foreach (Kingdom other in Kingdom.All)
            {
               if (other == kingdom) continue;
               if (FactionManager.IsAtWarAgainstFaction(kingdom, other))
                  enemies.Add(other.Name?.ToString());
            }

            if (enemies.Count == 0) return;

            string rumor = enemies.Count == 1
               ? $"There's talk of the war with {enemies[0]} — young men are being called up."
               : $"The kingdom's fighting on {enemies.Count} fronts now. Fewer merchants, more soldiers on the roads.";

            pool.Add(rumor);
         }
         catch { ModLog.Error("[RUMOR] Failed to generate war rumor."); }
      }

      /// <summary>Pulls real dated happenings the locals would plausibly have heard of, phrased by how surely word reached them.</summary>
      private static void TryWorldEvents(List<string> pool, Settlement settlement, int today)
      {
         IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated = WorldEventStore.Instance?.GetKnownEventsRatedAt(settlement, today, 3);

         if (rated == null) return;

         foreach ((WorldEvent e, RumorConfidence conf) in rated)
         {
            if (string.IsNullOrWhiteSpace(e.Summary)) continue;
            string s = e.Summary.TrimEnd('.', ' ');
            pool.Add(conf switch {
               RumorConfidence.WellKnown => $"{s}.",
               RumorConfidence.Secondhand => $"Word came that {s}.",
               _ => $"There's a faint rumour, though none here can vouch for it, that {s}."
            });
         }
      }

      #endregion
   }
}