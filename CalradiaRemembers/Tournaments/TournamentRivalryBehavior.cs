// Code written by Gabriel Mailhot, 06/09/2026.
// Tournament memory pillar Inc 4 (rivalries). Keeps a per-lord tally of head-to-head arena meetings with the player
// and, once they have faced each other enough (TournamentRivalryPolicy), crystallizes a standing SPORTING RIVALRY
// exactly once: a worthy-opponent APPRECIATION for an honourable competitor, or a LIGHT grudge for a low-honour one
// (Gabriel: both flavors, by character). The recent-matter memories (Inc 2) already make a rival seek the player
// out; this is what gives the repeated contest a lasting warmth or edge. The feeder records each head-to-head; this
// behavior owns the tally, its persistence, and the one-time aftermath.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Tournaments;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Tournaments
{
   /// <summary>A running tally of a lord's arena head-to-heads with the player (persisted).</summary>
   public sealed class RivalryTally
   {
      public int PlayerWins { get; set; }
      public int HeroWins { get; set; }

      /// <summary>True once the rivalry has crystallized (its one-time appreciation/grudge has been planted).</summary>
      public bool Formed { get; set; }
   }

   /// <summary>Tallies arena head-to-heads and crystallizes a standing rivalry (worthy or bitter) once enough have been fought.</summary>
   public sealed class TournamentRivalryBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_TournamentRivalry_v1bytes";

      /// <summary>Where a rivalry was forged, when the caller does not say: the arena, which is where this ledger began.</summary>
      public const string DefaultVenue = "the tournament grounds";

      public static TournamentRivalryBehavior Instance;

      private Dictionary<string, RivalryTally> _tallies = new();

      public TournamentRivalryBehavior() => Instance = this;

      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_tallies, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[TOURNEY] rivalry serialize failed, persisting none. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[TOURNEY] rivalry save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves, start empty */
            }

            _tallies = Deserialize(bytes);
         }
      }

      private static Dictionary<string, RivalryTally> Deserialize(byte[] bytes)
      {
         if (bytes is not {Length: > 0}) return new Dictionary<string, RivalryTally>();

         try
         {
            return JsonConvert.DeserializeObject<Dictionary<string, RivalryTally>>(Encoding.UTF8.GetString(bytes))
                   ?? new Dictionary<string, RivalryTally>();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TOURNEY] rivalry load failed, dropped for this load. {ex.GetType().Name}: {ex.Message}");

            return new Dictionary<string, RivalryTally>();
         }
      }

      /// <summary>Records one arena head-to-head with the player and, on crossing the threshold, crystallizes the rivalry once.</summary>
      public void RecordEncounter(Hero hero, bool playerWon, string venue = DefaultVenue)
      {
         if (hero == null || string.IsNullOrEmpty(hero.StringId) || Hero.MainHero == null) return;

         if (!_tallies.TryGetValue(hero.StringId, out RivalryTally tally))
         {
            tally = new RivalryTally();
            _tallies[hero.StringId] = tally;
         }

         if (playerWon) tally.PlayerWins++;
         else tally.HeroWins++;

         if (tally.Formed) return;

         TournamentRivalryFlavor flavor = TournamentRivalryPolicy.Evaluate(tally.PlayerWins, tally.HeroWins, TraitsOf(hero));
         if (flavor == TournamentRivalryFlavor.None) return;

         CrystallizeRivalry(hero, flavor, venue);
         tally.Formed = true;
      }

      private static void CrystallizeRivalry(Hero hero, TournamentRivalryFlavor flavor, string venue)
      {
         var today = (int) CampaignTime.Now.ToDays;

         if (flavor == TournamentRivalryFlavor.Bitter)
         {
            GrudgeBehavior.Instance?.TryPlant(hero, Hero.MainHero, GrudgeSource.External,
               $"A bitter rivalry forged in {venue}.", TournamentRivalryPolicy.BitterMagnitude, knownToPlayer: true, today);
            ModLog.Info($"[TOURNEY] {hero.Name} nurses a bitter arena rivalry with the player.");
         }
         else
         {
            AppreciationBehavior.Instance?.TryPlant(hero, Hero.MainHero, AppreciationSource.External,
               $"A worthy rivalry forged in {venue}.", TournamentRivalryPolicy.FriendlyMagnitude, knownToPlayer: true, today);
            ModLog.Info($"[TOURNEY] {hero.Name} and the player are worthy arena rivals now.");
         }
      }

      private static TournamentTraitFacts TraitsOf(Hero hero)
         => new TournamentTraitFacts(
            hero.GetTraitLevel(DefaultTraits.Valor),
            hero.GetTraitLevel(DefaultTraits.Honor),
            hero.GetTraitLevel(DefaultTraits.Mercy));

      /// <summary>The running head-to-head tally for a lord, or null when none is recorded (cr.testall / diagnostics).</summary>
      public RivalryTally Tally(string heroId)
         => !string.IsNullOrEmpty(heroId) && _tallies.TryGetValue(heroId, out RivalryTally tally) ? tally : null;

      /// <summary>Clears a lord's tally (cr.testall cleanup).</summary>
      public void ForgetTally(string heroId)
      {
         if (!string.IsNullOrEmpty(heroId)) _tallies.Remove(heroId);
      }
   }
}
