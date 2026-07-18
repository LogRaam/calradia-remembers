// Code written by Gabriel Mailhot, 30/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using FaceGen = TaleWorlds.Core.FaceGen;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>On entering a town, an armed assassination may spring into a street fight in the town centre.</summary>
   public sealed class TownAmbushBehavior : CampaignBehaviorBase
   {
      // The companion AGENTS spawned at the player's side, captured the same way — the player's side of the brawl.
      private static readonly List<Agent> _allyAgents = new();

      // The actual ambusher AGENTS as they spawn (captured via the LocationCharacter afterAgentCreated hook),
      // so the mission logic can hand them to MissionFightHandler.StartCustomFight as the opponent side.

      // The player's companions who stand with them this ambush (hero ids), spawned as allies in the brawl so the
      // 3-to-1 force ratio means something. Re-derived at BeginAmbush, consumed at injection.
      private static readonly List<string> _pendingAllyIds = new();

      // Set the instant the player chooses to face the ambush, consumed by the spawn-event injector one beat
      // later when the town centre populates. Static: there is one player, one ambush at a time.
      private static string _pendingAssassinId;
      private static int _pendingBanditCount; // the disguised cutthroats under the chief (the chief is always one)
      private static int _pendingCount; // total attackers (chief + bandits), the wait-loop's expected count

      // The spawn-point tag chosen this injection (the most-available townsfolk tag in THIS scene), read by
      // the character factories. Picked from the event's usable-point counts so we never spawn on a tag with no points.
      private static string _spawnTag;

      /// <summary>The companion agents spawned at the player's side — the ally side of the brawl.</summary>
      public static List<Agent> AllyAgents => _allyAgents;

      /// <summary>The chief + cutthroat agents spawned for the current ambush — the opponent side of the brawl.</summary>
      public static List<Agent> AmbusherAgents { get; } = new();

      /// <summary>
      ///   Called from the Harmony postfix on <c>Mission.AfterStart</c> — the only reliable "the mission has
      ///   FULLY started" hook: after AfterStart returns, the player and the injected cutthroats are spawned, and
      ///   (being a postfix, not mid-iteration) it is safe to add a mission behavior. Attaches the brawl driver,
      ///   which then waits a beat for everyone to settle and starts the fight. Consumes the pending ambush.
      /// </summary>
      public static void AttachBrawlIfPending(Mission mission)
      {
         try
         {
            if (string.IsNullOrEmpty(_pendingAssassinId)) return; // ordinary mission, not our ambush

            string assassinId = _pendingAssassinId;
            int count = _pendingCount;
            _pendingAssassinId = null; // consume
            _pendingCount = 0;
            _pendingBanditCount = 0;
            _pendingAllyIds.Clear();

            mission?.AddMissionBehavior(new AmbushFightMissionLogic(assassinId, count));
            ModLog.Info($"[AMBUSH] brawl driver attached after AfterStart (paymaster id {assassinId}, {count} attacker(s)).");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] attach brawl driver failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Console seam: spring the ambush now (player must be inside a town). Returns false if it can't.</summary>
      public static bool DebugSpring(Hero assassin)
      {
         var here = Settlement.CurrentSettlement;

         if (assassin == null || here is not {IsTown: true}) return false;

         BeginAmbush(assassin, here);

         return true;
      }

      /// <summary>Console/test seam: kept for the cr.ambush_gate command — routes to the same centre ambush.</summary>
      public static bool DebugSpringAt(Hero assassin, Settlement town)
      {
         if (assassin == null || town is not {IsTown: true}) return false;

         BeginAmbush(assassin, town);

         return true;
      }

      /// <summary>
      ///   Console/test seam (cr.ambush_scene): the fight always plays in the town centre now, so the location
      ///   id is ignored — kept so the existing command keeps compiling and still springs the ambush.
      /// </summary>
      public static bool DebugSpringScene(Hero assassin, string locationId)
      {
         var here = Settlement.CurrentSettlement;

         if (assassin == null || here is not {IsTown: true}) return false;

         ModLog.Info($"[AMBUSH] cr.ambush_scene: location '{locationId}' ignored — the street ambush always uses the town centre.");
         BeginAmbush(assassin, here);

         return true;
      }

      /// <summary>True when an ambush is armed against the player, so the access model can hold a town's gates.</summary>
      public static bool IsEntryBlocked(Settlement settlement)
      {
         if (settlement is not {IsTown: true}) return false;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         return store != null && FindArmedAssassin(store) != null;
      }

      /// <summary>
      ///   The gate menu chose to face the ambush (post-loss re-entry path): find the armed assassin, spend the
      ///   arming, and spring. Returns false if there is nothing armed (the menu should then release the player).
      /// </summary>
      public static bool SpringAtGate(Settlement town)
      {
         if (town is not {IsTown: true}) return false;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null) return false;

         Hero assassin = FindArmedAssassin(store);

         if (assassin == null) return false;

         Disarm(store, assassin); // one chance per arming, gate or street alike
         BeginAmbush(assassin, town);

         return true;
      }

      public override void RegisterEvents()
      {
         CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
         CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, OnLocationCharactersReadyToSpawn);
         // The brawl is started from a Harmony postfix on Mission.AfterStart (AmbushMissionStartPatch): the
         // AfterMissionStarted campaign event fires mid-AfterStart, BEFORE the player and cutthroats spawn.
      }

      public override void SyncData(IDataStore dataStore) { } // armed state lives on each NpcProfile

      #region private

      /// <summary>
      ///   Gives a spawned ambusher the behaviour groups a town brawl needs. An (empty) DailyBehaviorGroup plus
      ///   an AlarmedBehaviorGroup is the minimum — MissionFightHandler.StartCustomFight then adds the scripted
      ///   FightBehavior itself. (Patrolling-thug AI was the wrong fit — its PatrolAgentBehavior NREs on a town
      ///   spawn with no patrol path.)
      /// </summary>
      private static void AddAmbusherBehaviors(IAgent agent)
      {
         if (agent is not Agent a) return;

         AgentNavigator nav = a.GetComponent<CampaignAgentComponent>()?.AgentNavigator;

         if (nav == null) return;

         nav.AddBehaviorGroup<DailyBehaviorGroup>();
         nav.AddBehaviorGroup<AlarmedBehaviorGroup>();
      }

      /// <summary>
      ///   Arms the injection, then drops the player into the walkable town-centre scene (exactly as the
      ///   "Take a walk around the town center" menu does). Opening the centre fires the character-spawn event,
      ///   where <see cref="OnLocationCharactersReadyToSpawn" /> injects the cutthroats among the crowd.
      /// </summary>
      private static void BeginAmbush(Hero assassin, Settlement settlement)
      {
         try
         {
            // The companions standing with the player set the scale: ~3 cutthroats per defender, plus the chief.
            List<Hero> allies = CompanionsAtPlayerSide();
            AmbushForce force = AmbushPolicy.ComputeForce(1 + allies.Count, MBRandom.RandomInt(100));

            _pendingAssassinId = assassin?.StringId;
            _pendingBanditCount = force.BanditCount;
            _pendingCount = force.TotalAttackers;
            _pendingAllyIds.Clear();
            _pendingAllyIds.AddRange(allies.Select(h => h.StringId));

            ModLog.Info($"[AMBUSH] facing the ambush in {settlement?.Name}: opening the town centre — 1 chief + " +
                        $"{force.BanditCount} cutthroat(s) vs the player and {allies.Count} companion(s) " +
                        $"(paymaster {assassin?.Name}).");

            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(
               LocationComplex.Current.GetLocationWithId("center"));
            // NB: Mission.Current is still null here (the mission loads next tick). The brawl driver is attached
            // by AttachBrawlIfPending, called from the Harmony postfix on Mission.AfterStart once the mission
            // exists and its AfterStart has finished spawning the player and the injected cutthroats.
         }
         catch (Exception ex)
         {
            _pendingAssassinId = null;
            _pendingCount = 0;
            _pendingBanditCount = 0;
            _pendingAllyIds.Clear();
            ModLog.Error($"[AMBUSH] failed to open the town centre: {ex.GetType().Name}: {ex.Message}");
            InformationManager.DisplayMessage(new InformationMessage("You fend off the attackers, and they flee into the night."));
         }
      }

      /// <summary>
      ///   The Blackened "stealth" set over a troop's battle weapons, or null when the set is unavailable (so the
      ///   caller falls back to civilian equipment). Cloning the battle loadout keeps the troop's own arms.
      /// </summary>
      private static Equipment BuildDisguiseEquipment(CharacterObject baseTroop)
      {
         ItemObject body = FindItem("assassin_armor"); // "Blackened Armor" — the anchor; if it is gone, so is the set

         if (body == null) return null;

         Equipment eq = baseTroop?.FirstBattleEquipment?.Clone() ?? new Equipment();
         SetSlot(eq, EquipmentIndex.Body, body);
         SetSlot(eq, EquipmentIndex.Cape, FindItem("assassin_shoulder")); // "Blackened Cape"
         SetSlot(eq, EquipmentIndex.Leg, FindItem("assassin_boot")); // "Blackened Boots"
         SetSlot(eq, EquipmentIndex.Head, FindItem("assassin_hood")); // "Blackened Hood"

         return eq;
      }

      /// <summary>The hero companions in the player's party who stand with them in a street ambush, capped.</summary>
      private static List<Hero> CompanionsAtPlayerSide()
      {
         var heroes = new List<Hero>();
         TroopRoster roster = MobileParty.MainParty?.MemberRoster;

         if (roster == null) return heroes;

         foreach (TroopRosterElement e in roster.GetTroopRoster())
         {
            Hero h = e.Character?.HeroObject;
            if (h != null && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner) heroes.Add(h);
         }

         int cap = AmbushPolicy.CompanionsAtSide(heroes.Count);
         if (heroes.Count > cap) heroes.RemoveRange(cap, heroes.Count - cap);

         return heroes;
      }

      /// <summary>
      ///   Builds one of the player's companions as an ALLY in the brawl — their own hero appearance and battle
      ///   gear, on foot, captured into <see cref="_allyAgents" /> so the fight handler puts them on the player's
      ///   side. Spawned Neutral (not hostile); the custom fight assigns the team.
      /// </summary>
      private static LocationCharacter CreateAlly(Hero companion)
      {
         CharacterObject troop = companion.CharacterObject;
         Monster monster = FaceGen.GetMonsterWithSuffix(troop.Race, "_settlement");
         AgentData data = new AgentData(new SimpleAgentOrigin(troop)).Monster(monster).NoHorses(true);

         return new LocationCharacter(
            data,
            AddAmbusherBehaviors,
            _spawnTag,
            false,
            LocationCharacter.CharacterRelations.Neutral,
            null,
            false,
            false,
            null,
            false,
            false,
            true,
            OnAllySpawned);
      }

      /// <summary>
      ///   Builds one disguised cutthroat: a thug troop wearing the Blackened "stealth" set (soldiers out of
      ///   uniform on a lord's errand), keeping the troop's own weapons. If the set is missing from the game's
      ///   items, falls back to the troop's CIVILIAN equipment (still armed, still stronger than a looter).
      /// </summary>
      private static LocationCharacter CreateBandit(CultureObject culture, LocationCharacter.CharacterRelations relation)
      {
         CharacterObject troop = FindThug() ?? FindThrowingThug();
         Monster monster = FaceGen.GetMonsterWithSuffix(troop.Race, "_settlement");

         Equipment disguise = BuildDisguiseEquipment(troop);
         AgentData data = new AgentData(new SimpleAgentOrigin(troop)).Monster(monster).NoHorses(true);
         if (disguise != null) data.Equipment(disguise);

         return new LocationCharacter(
            data,
            AddAmbusherBehaviors,
            _spawnTag,
            false,
            relation,
            null,
            disguise == null, // fallback only: civilian clothes when the Blackened set is absent
            false,
            null,
            false,
            false,
            true,
            OnAmbusherSpawned);
      }

      /// <summary>
      ///   Builds the ambush CHIEF: an elite soldier of the paymaster's faction, on foot, in his own military
      ///   gear — the visible tell that a lord, not a gang, is behind the attempt. Enemy relation puts him on the
      ///   player's enemy team; <see cref="AddAmbusherBehaviors" /> drives him straight into the fight.
      /// </summary>
      private static LocationCharacter CreateChief(CultureObject culture, LocationCharacter.CharacterRelations relation)
      {
         int targetTier = AmbushPolicy.EliteChiefTier(Clan.PlayerClan?.Tier ?? 0);
         CharacterObject troop = ResolveEliteChief(culture, targetTier) ?? FindThug() ?? FindThrowingThug();
         Monster monster = FaceGen.GetMonsterWithSuffix(troop.Race, "_settlement");
         AgentData data = new AgentData(new SimpleAgentOrigin(troop)).Monster(monster).NoHorses(true);

         return new LocationCharacter(
            data,
            AddAmbusherBehaviors,
            _spawnTag,
            false,
            relation,
            null,
            false, // his soldier's panoply IS the point — keep the battle gear
            false,
            null,
            false,
            false,
            true,
            OnAmbusherSpawned);
      }

      private static void Disarm(BannerlordNpcMemoryStore store, Hero assassin)
      {
         NpcProfile p = store.Get(assassin.StringId);

         if (p == null) return;

         p.StanceAmbushArmedDay = 0;
         store.Set(p);
      }

      private static Hero FindArmedAssassin(BannerlordNpcMemoryStore store)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile p = store.Get(id);

            if (p == null || p.StanceAmbushArmedDay <= 0) continue;

            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);

            if (h is not {IsAlive: true}) continue;

            // C4: an armed attempt is not a one-way latch. A captor cannot send killers from his own cell, and a
            // lord the player has since won back must not have his months-old knife spring at the next town visit
            // while the prompt now says he is "genuinely glad to see you". Re-run the arming policy against his
            // CURRENT stance; if murder is no longer his verdict (or he is now the player's prisoner), disarm.
            if (h.IsPrisoner || !StanceConsequenceBehavior.IsMurderousNow(h, p))
            {
               Disarm(store, h);
               ModLog.Info($"[STANCE] {h.Name}'s armed attempt on the player lapses ({(h.IsPrisoner ? "now a prisoner" : "cooled off / reconciled")}).");

               continue;
            }

            return h;
         }

         return null;
      }

      private static CharacterObject FindCharacter(params string[] ids)
      {
         foreach (string id in ids)
         {
            var c = MBObjectManager.Instance?.GetObject<CharacterObject>(id);

            if (c != null) return c;
         }

         return null;
      }

      private static ItemObject FindItem(string id) => MBObjectManager.Instance?.GetObject<ItemObject>(id);

      // Bandit templates that carry thrown weapons (throwing axes / knives). The exact best id is one to confirm
      // in-game — sea raiders throw axes, forest/steppe bandits throw javelins; looter (stones) is the fallback.
      private static CharacterObject FindThrowingThug()
         => FindCharacter("sea_raiders_raider", "forest_bandits_raider", "steppe_bandits_raider", "looter");

      private static CharacterObject FindThug()
         => FindCharacter("looter", "mountain_bandits_raider", "vlandia_bandit", "gangster_tier_2");

      /// <summary>Captures each companion ally agent as it spawns, for the brawl's player side.</summary>
      private static void OnAllySpawned(IAgent agent)
      {
         if (agent is not Agent a) return;

         _allyAgents.Add(a);
         ModLog.Info($"[AMBUSH] companion ally agent spawned — {_allyAgents.Count} captured so far (active={a.IsActive()}).");
      }

      /// <summary>Captures each chief/cutthroat agent as it spawns, for the brawl's opponent side.</summary>
      private static void OnAmbusherSpawned(IAgent agent)
      {
         if (agent is not Agent a) return;

         AmbusherAgents.Add(a);
         ModLog.Info($"[AMBUSH] attacker agent spawned — {AmbusherAgents.Count} captured so far (active={a.IsActive()}).");
      }

      private static void OnFlee()
         => InformationManager.DisplayMessage(new InformationMessage(
            "You break and run, and lose them in the crowded streets — this time."));

      /// <summary>The usable spawn-point tag with the most free points (so the cutthroats actually appear), or null.</summary>
      private static string PickSpawnTag(Dictionary<string, int> usablePointCount)
      {
         if (usablePointCount == null || usablePointCount.Count == 0) return null;

         string best = null;
         var bestCount = 0;
         foreach (KeyValuePair<string, int> kv in usablePointCount)
            if (kv.Value > bestCount)
            {
               bestCount = kv.Value;
               best = kv.Key;
            }

         return best;
      }

      private static void Present(Hero assassin, Settlement settlement)
         => InformationManager.ShowInquiry(new InquiryData(
            "Blades in the dark",
            "As you make your way through the streets, hooded figures peel from the shadows, steel drawn. " +
            "This is no robbery — they have come for your life.",
            true, true,
            "Draw your weapon", "Try to slip away",
            () => BeginAmbush(assassin, settlement),
            OnFlee), true);

      /// <summary>
      ///   The chief's troop: the paymaster culture's noble line climbed only until it reaches the target tier
      ///   (≈ the player's clan tier), so the chief scales to the player rather than always fielding a top-tier
      ///   veteran. Null only if the culture has no elite line.
      /// </summary>
      private static CharacterObject ResolveEliteChief(CultureObject culture, int targetTier)
      {
         CharacterObject t = culture?.EliteBasicTroop;

         if (t == null) return null;

         for (var guard = 0; guard < 8 && t.Tier < targetTier; guard++) // climb until the target tier (guard vs cycles)
         {
            CharacterObject[] ups = t.UpgradeTargets;

            if (ups == null || ups.Length == 0) break;
            t = ups[0] ?? t;
         }

         return t;
      }

      private static void SetSlot(Equipment eq, EquipmentIndex slot, ItemObject item)
      {
         if (item != null) eq[slot] = new EquipmentElement(item);
      }

      /// <summary>
      ///   Fires while a location's characters are being populated. When an ambush is pending and the centre is
      ///   the location being filled, injects the hostile cutthroats at the townsfolk spawn points. The pending
      ///   state is deliberately LEFT SET here.. it is consumed later by <see cref="AttachBrawlIfPending" /> (the
      ///   Harmony postfix on Mission.AfterStart), which is what attaches the brawl driver.
      /// </summary>
      private void OnLocationCharactersReadyToSpawn(Dictionary<string, int> unusedUsablePointCount)
      {
         try
         {
            string tags = unusedUsablePointCount == null
               ? "(null)"
               : string.Join(", ", unusedUsablePointCount.Select(kv => $"{kv.Key}:{kv.Value}"));
            ModLog.Info($"[AMBUSH] spawn event: pending={_pendingAssassinId ?? "(none)"}, " +
                        $"missionLoc={CampaignMission.Current?.Location?.StringId ?? "(null)"}; usable tags: {tags}");

            if (string.IsNullOrEmpty(_pendingAssassinId)) return; // an ordinary town visit, not our ambush

            Location center = LocationComplex.Current?.GetLocationWithId("center");
            if (center == null)
            {
               ModLog.Error("[AMBUSH] no 'center' location to inject into.");

               return;
            }

            int bandits = _pendingBanditCount; // NB: pending state is consumed in AttachBrawlIfPending, not here

            // The chief is a real soldier of the PAYMASTER's faction (the tell a lord is behind it); the disguised
            // cutthroats and the spawn tag follow the SETTLEMENT's culture, where the scene actually is.
            Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _pendingAssassinId);
            CultureObject chiefCulture = paymaster?.Culture ?? Settlement.CurrentSettlement?.Culture;
            CultureObject culture = Settlement.CurrentSettlement?.Culture ?? chiefCulture;
            if (culture == null)
            {
               ModLog.Error("[AMBUSH] no settlement culture.");

               return;
            }

            _spawnTag = PickSpawnTag(unusedUsablePointCount); // a tag that actually has free points in THIS scene
            AmbusherAgents.Clear();
            _allyAgents.Clear();

            // The chief, then the disguised cutthroats — both hostile, both captured into _ambusherAgents.
            center.AddLocationCharacters(CreateChief, chiefCulture, LocationCharacter.CharacterRelations.Enemy, 1);
            center.AddLocationCharacters(CreateBandit, culture, LocationCharacter.CharacterRelations.Enemy, bandits);

            // The player's companions, on the player's side — one spawn per id, captured into _allyAgents.
            var alliesSpawned = 0;
            foreach (string id in _pendingAllyIds)
            {
               Hero ally = Hero.FindFirst(h => h != null && h.StringId == id);

               if (ally?.CharacterObject == null) continue;

               Hero captured = ally; // capture for the closure
               center.AddLocationCharacters((c, r) => CreateAlly(captured), culture, LocationCharacter.CharacterRelations.Neutral, 1);
               alliesSpawned++;
            }

            ModLog.Info($"[AMBUSH] injected 1 chief ({chiefCulture?.Name}) + {bandits} cutthroat(s) + {alliesSpawned} " +
                        $"companion ally(ies) at tag '{_spawnTag ?? "(null)"}'.");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] inject failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         try
         {
            // Firehose filter (kept first and silent): SettlementEntered fires for EVERY party entering EVERY
            // settlement. Only the player walking into a town can ever spring an ambush.
            if (party != MobileParty.MainParty || settlement is not {IsTown: true}) return;

            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

            if (store == null) return;

            // From here we are on a player-enters-town event (infrequent), so it is cheap to scan for an armed
            // assassin and log the decision — the gating used to be silent, which is why "no ambush ever fires"
            // could not be diagnosed from a log. The pure AmbushPolicy.DecidePlayerTownEntry is unit-tested.
            Hero assassin = FindArmedAssassin(store);
            bool armed = assassin != null;
            int roll = MBRandom.RandomInt(100);
            // The experimental toggle was retired in v1.18.0 — the feature ships always-on, so the gate is always
            // enabled. The pure policy keeps its featureEnabled parameter (a tested abstraction) for clarity.
            AmbushEntryOutcome outcome = AmbushPolicy.DecidePlayerTownEntry(true, armed, roll);

            // Stay silent in the ordinary case (no one is gunning for the player); otherwise log WHY, so a tester
            // (or a player who armed one via console) can see the roll outcome at a glance.
            if (armed)
               ModLog.Info($"[AMBUSH] {settlement.Name}: armed by {assassin.Name}, roll={roll} → {outcome}.");

            if (outcome != AmbushEntryOutcome.Spring) return;

            Disarm(store, assassin); // spent only when it actually springs; a miss re-rolls next visit
            Present(assassin, settlement);
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] trigger failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}