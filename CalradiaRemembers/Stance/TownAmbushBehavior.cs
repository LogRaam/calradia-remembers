// Code written by Gabriel Mailhot, 29/06/2026.
// "Stance with teeth", increment 3 — the town ambush (a Warband-style street fight). A lord whose stance hit
// Assassinate has armed an attempt on the player's life (StanceConsequenceBehavior set StanceAmbushArmedDay);
// when the player walks into a town, this may spring it: a narrated warning, then a real street fight.
//
// REARCHITECTED 2026-06-29 (Warlord-style): the OLD approach opened an alley-fight mission with roster sides,
// which the engine framed as a SIEGE with the attackers spawning ~300m off at the walls. The NEW approach drops
// the player into the real town-CENTRE walkable scene and injects the cutthroats as hostile LocationCharacters
// at the townsfolk spawn points (CampaignEvents.LocationCharactersAreReadyToSpawnEvent + Location.AddLocationCharacters
// with CharacterRelations.Enemy). They appear in the street, among the crowd, right next to the player. No siege,
// no teleport. Everything here is guarded — a failure to spring must never crash, only let the knives miss.

#region

using System;
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
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>On entering a town, an armed assassination may spring into a street fight in the town centre.</summary>
   public sealed class TownAmbushBehavior : CampaignBehaviorBase
   {
      // Set the instant the player chooses to face the ambush, consumed by the spawn-event injector one beat
      // later when the town centre populates. Static: there is one player, one ambush at a time.
      private static string _pendingAssassinId;
      private static int _pendingCount;

      // The spawn-point tag chosen this injection (the most-available townsfolk tag in THIS scene), read by
      // CreateAmbusher. Picked from the event's usable-point counts so we never spawn on a tag with no points.
      private static string _spawnTag;

      // The actual ambusher AGENTS as they spawn (captured via the LocationCharacter afterAgentCreated hook),
      // so the mission logic can hand them to MissionFightHandler.StartCustomFight as the opponent side.
      private static readonly System.Collections.Generic.List<Agent> _ambusherAgents = new System.Collections.Generic.List<Agent>();

      /// <summary>The cutthroat agents spawned for the current ambush — the opponent side of the brawl.</summary>
      public static System.Collections.Generic.List<Agent> AmbusherAgents => _ambusherAgents;

      public override void RegisterEvents()
      {
         CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
         CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, OnLocationCharactersReadyToSpawn);
         // The brawl is started from a Harmony postfix on Mission.AfterStart (AmbushMissionStartPatch): the
         // AfterMissionStarted campaign event fires mid-AfterStart, BEFORE the player and cutthroats spawn.
      }

      public override void SyncData(IDataStore dataStore) { } // armed state lives on each NpcProfile

      /// <summary>Console seam: spring the ambush now (player must be inside a town). Returns false if it can't.</summary>
      public static bool DebugSpring(Hero assassin)
      {
         Settlement here = Settlement.CurrentSettlement;
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
         Settlement here = Settlement.CurrentSettlement;
         if (assassin == null || here is not {IsTown: true}) return false;

         ModLog.Info($"[AMBUSH] cr.ambush_scene: location '{locationId}' ignored — the street ambush always uses the town centre.");
         BeginAmbush(assassin, here);

         return true;
      }

      /// <summary>True when an ambush is armed against the player, so the access model can hold a town's gates.</summary>
      public static bool IsEntryBlocked(Settlement settlement)
      {
         if (!ModSettings.TownAmbushEnabled) return false; // feature gated off — never hold a town's gates
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

      #region private

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
            AmbushEntryOutcome outcome = AmbushPolicy.DecidePlayerTownEntry(ModSettings.TownAmbushEnabled, armed, roll);

            // Stay silent in the ordinary case (no one is gunning for the player); otherwise log WHY, so a tester
            // (or a player who armed one via console) can see the toggle state and the roll outcome at a glance.
            if (armed)
               ModLog.Info($"[AMBUSH] {settlement.Name}: armed by {assassin.Name}, toggle={ModSettings.TownAmbushEnabled}, " +
                           $"roll={roll} → {outcome}.");

            if (outcome != AmbushEntryOutcome.Spring) return;

            Disarm(store, assassin); // spent only when it actually springs; a miss re-rolls next visit
            Present(assassin, settlement);
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] trigger failed: {ex.GetType().Name}: {ex.Message}"); }
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

      private static void OnFlee()
         => InformationManager.DisplayMessage(new InformationMessage(
            "You break and run, and lose them in the crowded streets — this time."));

      /// <summary>
      ///   Arms the injection, then drops the player into the walkable town-centre scene (exactly as the
      ///   "Take a walk around the town center" menu does). Opening the centre fires the character-spawn event,
      ///   where <see cref="OnLocationCharactersReadyToSpawn" /> injects the cutthroats among the crowd.
      /// </summary>
      private static void BeginAmbush(Hero assassin, Settlement settlement)
      {
         try
         {
            _pendingAssassinId = assassin?.StringId;
            _pendingCount = AmbushPolicy.AttackerCount(assassin?.Clan?.Tier ?? 0);
            ModLog.Info($"[AMBUSH] facing the ambush in {settlement?.Name}: opening the town centre, " +
                        $"{_pendingCount} cutthroat(s) incoming (paymaster {assassin?.Name}).");

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
            ModLog.Error($"[AMBUSH] failed to open the town centre: {ex.GetType().Name}: {ex.Message}");
            InformationManager.DisplayMessage(new InformationMessage("You fend off the attackers, and they flee into the night."));
         }
      }

      /// <summary>
      ///   Fires while a location's characters are being populated. When an ambush is pending and the centre is
      ///   the location being filled, injects the hostile cutthroats at the townsfolk spawn points. The pending
      ///   state is deliberately LEFT SET here.. it is consumed later by <see cref="AttachBrawlIfPending" /> (the
      ///   Harmony postfix on Mission.AfterStart), which is what attaches the brawl driver.
      /// </summary>
      private void OnLocationCharactersReadyToSpawn(System.Collections.Generic.Dictionary<string, int> unusedUsablePointCount)
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
            if (center == null) { ModLog.Error("[AMBUSH] no 'center' location to inject into."); return; }

            int count = _pendingCount; // NB: pending state is consumed in AttachBrawlIfPending, not here

            CultureObject culture = Settlement.CurrentSettlement?.Culture;
            if (culture == null) { ModLog.Error("[AMBUSH] no settlement culture."); return; }

            _spawnTag = PickSpawnTag(unusedUsablePointCount); // a tag that actually has free points in THIS scene
            _ambusherAgents.Clear();
            center.AddLocationCharacters(CreateAmbusher, culture, LocationCharacter.CharacterRelations.Enemy, count);
            ModLog.Info($"[AMBUSH] injected {count} hostile cutthroat(s) at tag '{_spawnTag ?? "(null)"}'.");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] inject failed: {ex.GetType().Name}: {ex.Message}"); }
      }

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

            mission?.AddMissionBehavior(new AmbushFightMissionLogic(assassinId, count));
            ModLog.Info($"[AMBUSH] brawl driver attached after AfterStart (paymaster id {assassinId}, {count} cutthroat(s)).");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] attach brawl driver failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The usable spawn-point tag with the most free points (so the cutthroats actually appear), or null.</summary>
      private static string PickSpawnTag(System.Collections.Generic.Dictionary<string, int> usablePointCount)
      {
         if (usablePointCount == null || usablePointCount.Count == 0) return null;

         string best = null;
         var bestCount = 0;
         foreach (System.Collections.Generic.KeyValuePair<string, int> kv in usablePointCount)
            if (kv.Value > bestCount) { bestCount = kv.Value; best = kv.Key; }

         return best;
      }

      /// <summary>
      ///   Builds one hostile cutthroat as a <see cref="LocationCharacter" /> — a bandit troop, armed (NOT
      ///   civilian equipment), spawned at a common townsfolk point. The Enemy relation puts them on the
      ///   player's enemy team; <see cref="AddAmbusherBehaviors" /> drives them straight into the fight.
      /// </summary>
      private static LocationCharacter CreateAmbusher(CultureObject culture, LocationCharacter.CharacterRelations relation)
      {
         CharacterObject troop = FindThug() ?? FindThrowingThug();
         Monster monster = TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(troop.Race, "_settlement");
         var data = new AgentData(new SimpleAgentOrigin(troop)).Monster(monster).NoHorses(true);

         return new LocationCharacter(
            data,
            AddAmbusherBehaviors,
            _spawnTag, // a tag with free points in this scene (picked from the spawn event), null = any point
            fixedLocation: false,
            relation,
            null, // actionSetCode — default for the monster
            useCivilianEquipment: false, // false = armed with their battle weapons
            isFixedCharacter: false,
            specialItem: null,
            isHidden: false,
            isVisualTracked: false,
            overrideBodyProperties: true,
            afterAgentCreated: OnAmbusherSpawned);
      }

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

      /// <summary>Captures each cutthroat agent as it spawns, for the brawl's opponent side.</summary>
      private static void OnAmbusherSpawned(IAgent agent)
      {
         if (agent is not Agent a) return;

         _ambusherAgents.Add(a);
         ModLog.Info($"[AMBUSH] cutthroat agent spawned — {_ambusherAgents.Count} captured so far (active={a.IsActive()}).");
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
