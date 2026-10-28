// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment (Gabriel, 2026-09-06), Inc 1 (the journal). A battle currently leaves every NPC present
// the same generic line; it should leave real facts. This mission-scoped logic keeps the ledger the facts are
// distilled from: which NAMED heroes the player put out of the fight, who put the PLAYER down, and how many
// rank-and-file the player felled. It is deliberately EPHEMERAL: nothing here is persisted, because the raw ledger
// is not the memory, the distilled deed is (a later increment writes that into the profiles of those who were
// there).
//
// TRUTH DISCIPLINE: the mission's own AgentState is NOT taken as final for a hero. Bannerlord routinely reports a
// hero agent as Killed while the campaign then merely wounds them, so the journal records only WHO went down, and
// the real outcome (dead, captive, or simply beaten) is read from the hero afterwards, when the campaign has had
// its say. That is what stops NPCs mourning a lord the player can visit in his own dungeon.
//
// chmidt (Nexus, 23/10/2026) and Gabriel the same day: one capture for every fight, for EVERY hero present, so a
// companion can remember their own part (CombatTally). The journal is also bound to the battle it belongs to (the
// player's MapEvent when the mission starts) and knows whether it was a tournament, so a tournament's deeds can no
// longer be read as those of the next auto-resolved battle, which has no mission of its own.

#region

using System;
using System.Collections.Generic;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Mission-scoped ledger of what the player actually did, and suffered, in this engagement.</summary>
   public sealed class BattleFeatJournal : MissionLogic
   {
      /// <summary>The live journal for the mission in progress, or null outside one.</summary>
      public static BattleFeatJournal Current { get; private set; }

      // The mission ENDS before the map event resolves, so a journal that cleared itself on OnEndMission would be
      // gone by the time the battle feeder runs. The finished ledger is parked here instead, and consumed exactly
      // once (see ConsumeCompleted): that also stops an auto-resolved battle, which has no mission at all, from
      // picking up the deeds of an earlier fight.
      private static BattleFeatJournal _completed;

      // Hero StringIds the player personally put out of the fight, in the order it happened, without duplicates.
      private readonly List<string> _heroesDowned = new List<string>();

      // Hero StringIds who put the PLAYER down (a battle can knock the player out once, but a duel-heavy mission
      // could see it twice; keep them all, the feeder decides what is worth saying).
      private readonly List<string> _downedThePlayer = new List<string>();

      /// <summary>Named heroes the player put out of the fight in this mission.</summary>
      public IReadOnlyList<string> HeroesDowned => _heroesDowned;

      /// <summary>Named heroes who put the player out of the fight in this mission.</summary>
      public IReadOnlyList<string> DownedThePlayer => _downedThePlayer;

      /// <summary>Rank-and-file the player felled with their own hand. Soldiers really do die, so this is a plain count of the fallen.</summary>
      public int TroopsFelled { get; private set; }

      // Every hero's own part, by hero id (the player's too, though the lines above remain the player's own record).
      private readonly Dictionary<string, HeroTally> _tallies = new Dictionary<string, HeroTally>();

      /// <summary>The battle this mission was fought for (the player's MapEvent when it started), or null outside one.</summary>
      public MapEvent BoundMapEvent { get; private set; }

      /// <summary>The mission was a tournament (no one dies in the lists).</summary>
      public bool IsTournament { get; private set; }

      /// <summary>The heroes who did or suffered anything in this fight.</summary>
      public IEnumerable<string> TalliedHeroIds => _tallies.Keys;

      /// <summary>One hero's own part in this fight, or null when they did and suffered nothing.</summary>
      public HeroTally TallyOf(string heroId) => heroId != null && _tallies.TryGetValue(heroId, out HeroTally t) ? t : null;

      public override void OnBehaviorInitialize()
      {
         base.OnBehaviorInitialize();
         Current = this;
         _completed = null; // a new fight invalidates any ledger nobody consumed
         BoundMapEvent = MobileParty.MainParty?.MapEvent;
      }

      protected override void OnEndMission()
      {
         try { IsTournament = Mission?.GetMissionBehavior<TournamentBehavior>() != null; }
         catch (Exception ex) { ModLog.Error($"[BATTLEFEAT] tournament check failed: {ex.GetType().Name}: {ex.Message}"); }

         // Park the finished ledger for the battle feeder, which runs AFTER the mission is gone.
         _completed = this;
         if (Current == this) Current = null;

         base.OnEndMission();
      }

      /// <summary>
      ///   The ledger of the fight that just ended, or null when there was none (an auto-resolved battle has no
      ///   mission, so it has no deeds to report). Consumed once: a second read returns null, so one fight's deeds
      ///   can never be attributed to a later battle.
      /// </summary>
      public static BattleFeatJournal ConsumeCompleted()
      {
         BattleFeatJournal completed = _completed;
         _completed = null;

         return completed;
      }

      /// <summary>
      ///   The ledger of the mission fought for <paramref name="mapEvent" />, consumed once; null when the last mission
      ///   was another fight (a tournament, an alley brawl, or no mission at all for an auto-resolved battle), which is
      ///   then left for its own reader.
      /// </summary>
      public static BattleFeatJournal ConsumeFor(MapEvent mapEvent)
      {
         if (mapEvent == null || _completed == null || _completed.BoundMapEvent != mapEvent) return null;

         return ConsumeCompleted();
      }

      /// <summary>The ledger of the tournament just fought, consumed once; null when the last mission was not one.</summary>
      public static BattleFeatJournal ConsumeTournament() => _completed is {IsTournament: true} ? ConsumeCompleted() : null;

      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
      {
         try
         {
            // Routed or merely deleted agents were not put down by anyone; only a real knockout or kill counts.
            if (agentState != AgentState.Unconscious && agentState != AgentState.Killed) return;
            if (affectedAgent == null || affectorAgent == null || affectedAgent == affectorAgent) return;

            TallyEveryHero(affectedAgent, affectorAgent);

            bool playerStruck = affectorAgent.IsPlayerControlled;
            bool playerFell = affectedAgent.IsPlayerControlled;
            if (!playerStruck && !playerFell) return; // this mod only remembers what the PLAYER did or suffered

            if (playerStruck)
            {
               string heroId = HeroIdOf(affectedAgent);
               if (heroId == null) TroopsFelled++;
               else if (!_heroesDowned.Contains(heroId)) _heroesDowned.Add(heroId);

               return;
            }

            string felledBy = HeroIdOf(affectorAgent);
            if (felledBy != null && !_downedThePlayer.Contains(felledBy)) _downedThePlayer.Add(felledBy);
         }
         catch (Exception ex) { ModLog.Error($"[BATTLEFEAT] journal failed on agent removal: {ex.GetType().Name}: {ex.Message}"); }
      }

      // Every hero's own part (chmidt, 23/10/2026). A charge is the rider's blow, a fallen horse is no man, and a blow
      // between friends (or a stray arrow into one's own line) is no deed.
      private void TallyEveryHero(Agent affected, Agent affector)
      {
         if (affected.IsMount) return;
         Agent striker = affector.IsMount && affector.RiderAgent != null ? affector.RiderAgent : affector;
         if (striker == affected) return;
         if (striker.Team != null && affected.Team != null && striker.Team.IsFriendOf(affected.Team)) return;

         string strikerId = HeroIdOf(striker);
         string victimId = HeroIdOf(affected);
         if (strikerId != null)
         {
            HeroTally s = Tally(strikerId);
            if (victimId == null) s.TroopsFelled++;
            else if (!s.HeroesDowned.Contains(victimId)) s.HeroesDowned.Add(victimId);
         }

         if (victimId != null)
         {
            HeroTally v = Tally(victimId);
            if (!v.WasDowned)
            {
               v.WasDowned = true;
               v.DownedById = strikerId;
            }
         }
      }

      private HeroTally Tally(string heroId)
      {
         if (!_tallies.TryGetValue(heroId, out HeroTally t)) _tallies[heroId] = t = new HeroTally();

         return t;
      }

      /// <summary>cr.test all seam: a hero's own part as though the mission had reported it.</summary>
      internal void DebugTally(string heroId, int troopsFelled, string heroDownedId, string downedById)
      {
         if (string.IsNullOrEmpty(heroId)) return;
         HeroTally t = Tally(heroId);
         t.TroopsFelled += troopsFelled;
         if (!string.IsNullOrEmpty(heroDownedId) && !t.HeroesDowned.Contains(heroDownedId)) t.HeroesDowned.Add(heroDownedId);
         if (downedById != null)
         {
            t.WasDowned = true;
            t.DownedById = downedById.Length == 0 ? null : downedById;
         }
      }

      /// <summary>cr.test all seam: binds a hand-made journal to a battle, as a mission would at its start.</summary>
      internal void DebugBind(MapEvent mapEvent, bool tournament)
      {
         BoundMapEvent = mapEvent;
         IsTournament = tournament;
      }

      /// <summary>cr.test all seam: parks a hand-made journal as the finished one, as a mission's end would.</summary>
      internal static void DebugPark(BattleFeatJournal journal) => _completed = journal;

      /// <summary>The hero behind an agent, or null when it is a nameless soldier (whose fall is a tally, not a deed).</summary>
      private static string HeroIdOf(Agent agent)
      {
         var character = agent?.Character as CharacterObject;

         return character?.HeroObject?.StringId;
      }

      /// <summary>cr.test all seam: record a deed as though the mission had reported it, so the real ledger is exercised without a battle.</summary>
      internal void DebugRecord(string heroDownedId, string downedPlayerById, int troopsFelled)
      {
         if (!string.IsNullOrEmpty(heroDownedId) && !_heroesDowned.Contains(heroDownedId)) _heroesDowned.Add(heroDownedId);
         if (!string.IsNullOrEmpty(downedPlayerById) && !_downedThePlayer.Contains(downedPlayerById)) _downedThePlayer.Add(downedPlayerById);
         TroopsFelled += troopsFelled;
      }
   }

   /// <summary>One hero's raw part in one mission, by hero id; CombatTally is its worded form.</summary>
   public sealed class HeroTally
   {
      /// <summary>Rank-and-file this hero put out of the fight.</summary>
      public int TroopsFelled { get; set; }

      /// <summary>Hero ids this hero put out of the fight, in order, each once.</summary>
      public List<string> HeroesDowned { get; } = new List<string>();

      /// <summary>This hero was put out of the fight.</summary>
      public bool WasDowned { get; set; }

      /// <summary>The hero id of who put them down, or null for a common soldier.</summary>
      public string DownedById { get; set; }
   }
}
