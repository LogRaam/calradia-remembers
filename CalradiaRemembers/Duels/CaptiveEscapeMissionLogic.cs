// Code written by Gabriel Mailhot, 20/07/2026.
// Captivity escape encounter (W1 spike), built on the wilderness-duel recipe (DuelMissionLogic). A captive who
// fights or runs for it is put in a REAL scene instead of the LLM narrating the outcome: the player, UNARMED
// and stripped of armour (civilian clothes), faces up to three aggressors in civilian gear each swinging a
// Knobbed Club. Two ways out are freedom: knock every aggressor down, OR reach the edge of the scene (break
// away and run). Knocked out yourself, you stay a prisoner. Non-lethal like the duel (IAgentStateDecider ->
// Unconscious), so a captive scene can never delete a hero, and a downed player is only recaptured, never
// killed. The mission is pure glue; who is freed / what is lost / the nemesis memory ride the onEnd callback.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>How a captive-escape encounter ended.</summary>
   public enum EscapeOutcome
   {
      /// <summary>Every aggressor was knocked out: the player fought free.</summary>
      BeatThemAll,

      /// <summary>The player reached the edge of the scene and broke away.</summary>
      Fled,

      /// <summary>The player was knocked out: still a prisoner.</summary>
      Overpowered
   }

   /// <summary>Stages and adjudicates the non-lethal 1-vs-N captive escape, reporting the outcome to the caller.</summary>
   public sealed class CaptiveEscapeMissionLogic : MissionLogic, IAgentStateDecider
   {
      /// <summary>The Knobbed Club item id (SandBoxCore weapons.xml), the aggressors' blunt weapon when armed.</summary>
      private const string ClubItemId = "battania_mace_1_t2";

      /// <summary>Whether the aggressors carry the Knobbed Club (true) or brawl bare-handed (false). TUNE.
      /// static readonly, not const, so flipping it does not fold the other branch into unreachable code.</summary>
      private static readonly bool AggressorsArmed = false;

      // Kept SHORT on purpose: the aggressors start right on top of the player, so turning to run for it means
      // baring your back to clubs already in reach, which is what makes a straight-line dash costly. TUNE.
      private const float Separation = 5f;        // metres between the player and the aggressor line
      private const float FoeSpread = 3f;         // metres between aggressors along that line

      // The player CAN break for it, but tires: the run-speed cap starts at parity with the aggressors and decays
      // one step every interval, so a chase catches a fleeing captive unless they are athletic enough to keep the
      // lead. Endurance (the Athletics skill, under the Endurance attribute) governs how fast they tire: a low
      // score bleeds speed quickly, a high score barely at all. Values are speed-limit multipliers. TUNE.
      private const float StartSpeedFactor = 1.0f;      // start even with the aggressors: a real break is possible
      private const float DecayInterval = 10f;          // seconds between each speed drop
      private const float MaxDecayPerInterval = 0.15f;  // worst-case drop per interval, at zero endurance
      private const int PlayerEnduranceCap = 200;       // Athletics at/above which the player barely tires
      private const float SpeedFloor = 0.5f;            // never set any speed cap below this

      // The pursuers are strung OUT, not a wall: the lead runs at parity with the player, each further one
      // slower, so a fleeing player is caught by ONE first and must deal with them before the rest arrive. TUNE.
      private const float FoeLeadSpeed = 1.0f;          // the fastest pursuer, even with the player's start speed
      private const float FoeSpeedStagger = 0.25f;      // each further pursuer is this much slower (1.0 / 0.75 / 0.50)
      private const float EndLingerSeconds = 4f;  // seconds to let the ending be seen before the return to the map
      private const float FleeMargin = 0.8f;      // fraction of the smaller half-extent the player must cross to have fled
      private const float FleeCap = 90f;          // metres to the edge: long enough that the speed decay bites mid-run

      private readonly Action<EscapeOutcome> _onEnd;
      private readonly int _foeCount;
      private readonly IReadOnlyList<CharacterObject> _foes; // requested aggressors; a null entry falls back to a bandit
      private readonly List<Agent> _foeAgents = new List<Agent>();

      private Agent _playerAgent;
      private float _centreX, _centreY, _centreZ;
      private float _fleeDistanceSq;
      private bool _ended;
      private bool _callbackFired;
      private float _endElapsed;
      private EscapeOutcome _outcome;

      private float _decayPerInterval; // computed once from the player's Athletics at scene start
      private float _speedTimer;       // seconds since the last speed drop
      private int _intervalsElapsed;   // how many drops have been applied

      public CaptiveEscapeMissionLogic(IReadOnlyList<CharacterObject> foes, Action<EscapeOutcome> onEnd)
      {
         _foes = foes;
         _foeCount = Math.Max(1, Math.Min(foes?.Count ?? 1, 3));
         _onEnd = onEnd;
      }

      /// <summary>Everyone who falls falls UNCONSCIOUS: a captive scene wounds and recaptures, it never kills.</summary>
      public AgentState GetAgentState(Agent affectedAgent, float deathProbability, out bool usedSurgery)
      {
         usedSurgery = false;

         return AgentState.Unconscious;
      }

      public override void AfterStart()
      {
         try
         {
            SpawnAll();
            Mission.SetMissionMode(MissionMode.Battle, false); // frees walking + weapon pickup (duel-proven)
            ModLog.Info($"[ESCAPE] player (unarmed) vs {_foeAgents.Count} club-armed aggressor(s). Fight free or reach the edge.");
         }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] AfterStart failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
      {
         if (_ended) return;

         if (affectedAgent == _playerAgent)
         {
            Latch(EscapeOutcome.Overpowered);

            return;
         }

         if (_foeAgents.Remove(affectedAgent) && _foeAgents.Count == 0)
            Latch(EscapeOutcome.BeatThemAll);
      }

      public override void OnMissionTick(float dt)
      {
         if (!_ended)
         {
            if (_playerAgent != null && _playerAgent.IsActive())
            {
               // The player tires: every interval, drop the speed cap another step, so a straight run buys only
               // a head start and the chase closes unless their endurance holds the lead.
               _speedTimer += dt;
               if (_speedTimer >= DecayInterval)
               {
                  _speedTimer -= DecayInterval;
                  _intervalsElapsed++;
                  float cap = Math.Max(SpeedFloor, StartSpeedFactor - _intervalsElapsed * _decayPerInterval);
                  _playerAgent.SetMaximumSpeedLimit(cap, true);
               }

               // Reaching the scene's edge is escape: the player broke away and ran for it.
               Vec3 p = _playerAgent.Position;
               float dx = p.x - _centreX, dy = p.y - _centreY;
               if (dx * dx + dy * dy >= _fleeDistanceSq) Latch(EscapeOutcome.Fled);
            }

            return;
         }

         _endElapsed += dt;
         if (_endElapsed < EndLingerSeconds || _callbackFired) return;

         _callbackFired = true;
         if (_onEnd != null)
         {
            try { _onEnd(_outcome); }
            catch (Exception ex) { ModLog.Error($"[ESCAPE] onEnd callback threw: {ex.GetType().Name}: {ex.Message}"); }
         }

         try { Mission.EndMission(); }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] EndMission threw: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The player cannot simply leave the scene: the encounter must resolve (fight, flee, or fall).</summary>
      public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
      {
         canPlayerLeave = _ended;

         return null;
      }

      #region private

      private void Latch(EscapeOutcome outcome)
      {
         if (_ended) return;
         _outcome = outcome;
         _ended = true;
         _endElapsed = 0f;
         ModLog.Info($"[ESCAPE] resolved: {outcome}. Closing shortly.");
      }

      /// <summary>
      ///   How much run-speed the player loses each interval, from their Athletics (endurance): none at the cap,
      ///   the full <see cref="MaxDecayPerInterval" /> at zero. Computed once so the whole chase is consistent.
      /// </summary>
      private static float ComputeDecayPerInterval()
      {
         int athletics = Hero.MainHero?.GetSkillValue(DefaultSkills.Athletics) ?? 0;
         float endured = Math.Min(1f, Math.Max(0f, athletics / (float) PlayerEnduranceCap));

         return MaxDecayPerInterval * (1f - endured);
      }

      private void SpawnAll()
      {
         Team playerTeam = Mission.Teams.Add(BattleSideEnum.Defender, uint.MaxValue, uint.MaxValue, null, true, false, true);
         Team enemyTeam = Mission.Teams.Add(BattleSideEnum.Attacker, uint.MaxValue, uint.MaxValue, null, false, false, true);
         Mission.PlayerTeam = playerTeam;
         playerTeam.SetIsEnemyOf(enemyTeam, true);

         Vec3 centre = NavigableCentre();
         _centreX = centre.x;
         _centreY = centre.y;
         _centreZ = centre.z;

         Mission.Scene.GetBoundingBox(out Vec3 min, out Vec3 max);
         float halfMin = Math.Min((max.x - min.x) * 0.5f, (max.y - min.y) * 0.5f);
         float fleeDist = Math.Min(FleeCap, halfMin * FleeMargin);
         _fleeDistanceSq = fleeDist * fleeDist;

         // The player stands unarmed on one side, facing the aggressor line.
         Vec3 playerPos = GroundAt(new Vec3(centre.x, centre.y - Separation * 0.5f, centre.z));
         _playerAgent = SpawnFighter(CharacterObject.PlayerCharacter, playerTeam, playerPos, new Vec2(0f, 1f),
            UnarmedCivilianEquipment(), club: false);
         if (_playerAgent != null)
         {
            _playerAgent.Controller = AgentControllerType.Player;
            _decayPerInterval = ComputeDecayPerInterval();
            _playerAgent.SetMaximumSpeedLimit(StartSpeedFactor, true); // starts at parity, then tires (OnMissionTick)
         }

         // The aggressors stand in a short line opposite, each with a Knobbed Club, and charge once Alarmed.
         // Each aggressor is a REQUESTED character (the real captor, so you face him in the flesh) where one was
         // given; a null falls back to a DIFFERENT bandit culture (Looters, Forest/Sea/Steppe/Mountain/Desert)
         // so mook fillers do not look like triplets.
         List<CharacterObject> pool = BanditTroopPool();
         float startX = -FoeSpread * (_foeCount - 1) * 0.5f;
         for (var i = 0; i < _foeCount; i++)
         {
            CharacterObject foeChar = _foes != null && i < _foes.Count && _foes[i] != null ? _foes[i] : pool[i % pool.Count];
            Vec3 pos = GroundAt(new Vec3(centre.x + startX + i * FoeSpread, centre.y + Separation * 0.5f, centre.z));
            Agent foe = SpawnFighter(foeChar, enemyTeam, pos, new Vec2(0f, -1f), AggressorEquipment(foeChar), club: AggressorsArmed);
            if (foe == null) continue;
            foe.SetWatchState(Agent.WatchState.Alarmed);
            foe.Defensiveness = 1f;
            foe.SetMaximumSpeedLimit(Math.Max(SpeedFloor, FoeLeadSpeed - i * FoeSpeedStagger), true); // strung-out pursuit
            _foeAgents.Add(foe);
         }

         if (_foeAgents.Count == 0) Latch(EscapeOutcome.BeatThemAll); // nothing left to fight (spawn failure fail-safe)
      }

      private Agent SpawnFighter(CharacterObject character, Team team, Vec3 position, Vec2 direction, Equipment equipment, bool club)
      {
         try
         {
            var build = new AgentBuildData(character)
               .Team(team)
               .InitialPosition(position)
               .InitialDirection(direction)
               .NoHorses(true);
            if (equipment != null) build = build.Equipment(equipment);

            Agent agent = Mission.SpawnAgent(build, false);
            if (club) agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp);

            return agent;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ESCAPE] spawning {character?.StringId ?? "(null)"} failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The player's civilian outfit with every weapon slot cleared: stripped of armour and disarmed.</summary>
      private static Equipment UnarmedCivilianEquipment()
      {
         Equipment src = Hero.MainHero?.CivilianEquipment
                      ?? CharacterObject.PlayerCharacter.FirstCivilianEquipment
                      ?? CharacterObject.PlayerCharacter.FirstBattleEquipment;

         return StripWeapons(src?.Clone());
      }

      /// <summary>Distinct bandit troops, one per bandit culture, so the aggressors vary in body and dress. Looter fallback.</summary>
      private static List<CharacterObject> BanditTroopPool()
      {
         var pool = new List<CharacterObject>();
         try
         {
            foreach (Clan clan in Clan.All)
            {
               if (!clan.IsBanditFaction) continue;
               CharacterObject troop = clan.Culture?.BasicTroop;
               if (troop != null && !pool.Contains(troop)) pool.Add(troop);
            }
         }
         catch (Exception ex) { ModLog.Error($"[ESCAPE] bandit pool failed: {ex.GetType().Name}: {ex.Message}"); }

         if (pool.Count == 0)
            pool.Add(MBObjectManager.Instance.GetObject<CharacterObject>("looter") ?? CharacterObject.PlayerCharacter);

         return pool;
      }

      /// <summary>
      ///   An aggressor's outfit, disarmed. A HERO foe (the real captor) wears his own LIVE gear so he looks
      ///   exactly like himself in the fight; a plain troop uses its template. Weapons are stripped either way (a
      ///   bare-handed brawl); a Knobbed Club is added back only when the aggressors are armed. The source is
      ///   cloned first, so a hero's real equipment is never touched.
      /// </summary>
      private static Equipment AggressorEquipment(CharacterObject foe)
      {
         Equipment src = foe.HeroObject?.BattleEquipment ?? foe.FirstBattleEquipment ?? foe.FirstCivilianEquipment;
         Equipment eq = StripWeapons(src?.Clone()) ?? new Equipment();

         if (AggressorsArmed)
         {
            var club = MBObjectManager.Instance.GetObject<ItemObject>(ClubItemId);
            if (club != null) eq[EquipmentIndex.Weapon0] = new EquipmentElement(club);
         }

         return eq;
      }

      /// <summary>Clears every weapon and mount slot, leaving only the clothing/armour.</summary>
      private static Equipment StripWeapons(Equipment eq)
      {
         if (eq == null) return null;

         for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
            eq[i] = default;
         eq[EquipmentIndex.Horse] = default;
         eq[EquipmentIndex.HorseHarness] = default;

         return eq;
      }

      /// <summary>A point on navigable ground near the centre of the scene's bounding box (duel-validated).</summary>
      private Vec3 NavigableCentre()
      {
         Scene scene = Mission.Scene;
         scene.GetBoundingBox(out Vec3 min, out Vec3 max);
         var midXy = new Vec3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z + 10f);

         return new Vec3(midXy.x, midXy.y, scene.GetGroundHeightAtPosition(midXy));
      }

      /// <summary>Snaps an x/y point down onto the scene's ground.</summary>
      private Vec3 GroundAt(Vec3 point)
      {
         float z = Mission.Scene.GetGroundHeightAtPosition(new Vec3(point.x, point.y, point.z + 10f));

         return new Vec3(point.x, point.y, z);
      }

      #endregion
   }
}
