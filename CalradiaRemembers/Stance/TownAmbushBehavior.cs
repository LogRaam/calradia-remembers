// Code written by Gabriel Mailhot, 27/06/2026.
// "Stance with teeth", increment 3 — the town ambush (a Warband-style street fight). A lord whose stance hit
// Assassinate has armed an attempt on the player's life (StanceConsequenceBehavior set StanceAmbushArmedDay);
// when the player walks a town's streets, this may spring it: a narrated warning, then a real alley fight
// against hired cutthroats. The odds + headcount are the pure AmbushPolicy; everything here is engine glue and
// is fully guarded — a failure to open the scene must never crash the game, only let the knives miss this time.
//
// NOTE: the alley-fight scene open (CampaignMission.OpenAlleyFightMission) is the piece that needs in-game
// iteration — scene id, upgrade level, and roster shape are resolved at runtime with fallbacks and logged.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>On entering a town, an armed assassination may spring into a street fight.</summary>
   public sealed class TownAmbushBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
         => CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);

      public override void SyncData(IDataStore dataStore) { } // armed state lives on each NpcProfile

      /// <summary>Console seam: spring the ambush now (player must be inside a town). Returns false if it can't.</summary>
      public static bool DebugSpring(Hero assassin)
      {
         Settlement here = Settlement.CurrentSettlement;
         if (assassin == null || here is not {IsTown: true}) return false;

         Spring(assassin, here);

         return true;
      }

      #region private

      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         try
         {
            if (party != MobileParty.MainParty || settlement is not {IsTown: true}) return;

            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            Hero assassin = FindArmedAssassin(store);
            if (assassin == null) return;
            if (!AmbushPolicy.ShouldSpring(MBRandom.RandomInt(100))) return; // the knives stay sheathed this visit

            Disarm(store, assassin); // it gets one chance per arming — spring or fizzle, it's spent
            Present(assassin, settlement);
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] trigger failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static Hero FindArmedAssassin(BannerlordNpcMemoryStore store)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile p = store.Get(id);
            if (p == null || p.StanceAmbushArmedDay <= 0) continue;

            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);
            if (h is {IsAlive: true}) return h;
         }

         return null;
      }

      private static void Disarm(BannerlordNpcMemoryStore store, Hero assassin)
      {
         NpcProfile p = store.Get(assassin.StringId);
         if (p == null) return;

         p.StanceAmbushArmedDay = 0;
         store.Set(p);
      }

      private static void Present(Hero assassin, Settlement settlement)
         => InformationManager.ShowInquiry(new InquiryData(
            "Blades in the dark",
            "As you make your way through the streets, hooded figures peel from the shadows, steel drawn. " +
            "This is no robbery — they have come for your life.",
            true, true,
            "Draw your weapon", "Try to slip away",
            () => Spring(assassin, settlement),
            OnFlee), true);

      private static void OnFlee()
         => InformationManager.DisplayMessage(new InformationMessage(
            "You break and run, and lose them in the crowded streets — this time."));

      private static void Spring(Hero assassin, Settlement settlement)
      {
         try
         {
            (Location alley, string scene, int usedLevel) = ResolveScene(settlement);
            if (alley == null || string.IsNullOrEmpty(scene)) { ModLog.Error("[AMBUSH] no usable alley scene resolved (see candidates above)."); return; }

            TroopRoster playerSide = BuildPlayerSide();
            TroopRoster rivalSide = BuildRivalSide(assassin);

            ModLog.Info($"[AMBUSH] springing in {settlement.Name} (scene {scene}, lvl {usedLevel}, " +
                        $"{rivalSide.TotalManCount} attacker(s), paymaster {assassin?.Name}).");

            CampaignMission.OpenAlleyFightMission(scene, usedLevel, alley, playerSide, rivalSide);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AMBUSH] fight failed to open: {ex.GetType().Name}: {ex.Message}");
            InformationManager.DisplayMessage(new InformationMessage("You fend off the attackers, and they flee into the night."));
         }
      }

      /// <summary>
      ///   Finds a usable fight location + scene by scanning every location in the settlement across all four
      ///   scene levels (the scene-name array is size 4, indices 0..3), preferring an alley, then the town
      ///   centre, then anything with a scene. Logs every candidate so the real inventory is visible in-game
      ///   — this is the piece that needs tuning per how a given town's locations are populated.
      /// </summary>
      private static (Location loc, string scene, int level) ResolveScene(Settlement settlement)
      {
         LocationComplex lc = settlement?.LocationComplex;
         if (lc == null) return (null, null, 0);

         (Location loc, string scene, int level) alley = default, center = default, any = default;

         foreach (Location loc in lc.GetListOfLocations() ?? Enumerable.Empty<Location>())
         {
            for (var l = 0; l <= 3; l++)
            {
               string s = loc.GetSceneName(l);
               if (string.IsNullOrEmpty(s)) continue;

               ModLog.Info($"[AMBUSH] candidate location '{loc.StringId}' lvl {l} -> {s}");

               if (loc.StringId?.IndexOf("alley", StringComparison.OrdinalIgnoreCase) >= 0)
               {
                  if (alley.loc == null) alley = (loc, s, l);
               }
               else if (loc.StringId == "center")
               {
                  if (center.loc == null) center = (loc, s, l);
               }
               else if (any.loc == null)
               {
                  any = (loc, s, l);
               }
            }
         }

         return alley.loc != null
            ? alley
            : center.loc != null
               ? center
               : any;
      }

      private static TroopRoster BuildPlayerSide()
      {
         TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
         roster.AddToCounts(Hero.MainHero.CharacterObject, 1);

         // A coin-flip whether one companion happens to be at your side when the knives come out.
         if (MBRandom.RandomFloat < 0.5f)
         {
            CharacterObject companion = MobileParty.MainParty.MemberRoster.GetTroopRoster()
                                                   .Select(e => e.Character)
                                                   .FirstOrDefault(c => c is {IsHero: true} && c != Hero.MainHero.CharacterObject);
            if (companion != null) roster.AddToCounts(companion, 1);
         }

         return roster;
      }

      private static TroopRoster BuildRivalSide(Hero assassin)
      {
         TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
         int count = AmbushPolicy.AttackerCount(assassin?.Clan?.Tier ?? 0);

         CharacterObject melee = FindThug();
         CharacterObject thrower = FindThrowingThug();

         // One cutthroat hangs back and throws knives/axes; the rest close with blades.
         if (thrower != null && count > 1)
         {
            roster.AddToCounts(thrower, 1);
            count--;
         }

         CharacterObject rest = melee ?? thrower;
         if (rest != null) roster.AddToCounts(rest, count);

         return roster;
      }

      private static CharacterObject FindThug()
         => FindCharacter("looter", "mountain_bandits_raider", "vlandia_bandit", "gangster_tier_2");

      // Bandit templates that carry thrown weapons (throwing axes / knives). The exact best id is one to confirm
      // in-game — sea raiders throw axes, forest/steppe bandits throw javelins; looter (stones) is the fallback.
      private static CharacterObject FindThrowingThug()
         => FindCharacter("sea_raiders_raider", "forest_bandits_raider", "steppe_bandits_raider", "looter");

      private static CharacterObject FindCharacter(params string[] ids)
      {
         foreach (string id in ids)
         {
            CharacterObject c = MBObjectManager.Instance?.GetObject<CharacterObject>(id);
            if (c != null) return c;
         }

         return null;
      }

      #endregion
   }
}
