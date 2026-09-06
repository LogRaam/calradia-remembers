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

#region

using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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

      public override void OnBehaviorInitialize()
      {
         base.OnBehaviorInitialize();
         Current = this;
         _completed = null; // a new fight invalidates any ledger nobody consumed
      }

      protected override void OnEndMission()
      {
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

      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
      {
         try
         {
            // Routed or merely deleted agents were not put down by anyone; only a real knockout or kill counts.
            if (agentState != AgentState.Unconscious && agentState != AgentState.Killed) return;
            if (affectedAgent == null || affectorAgent == null || affectedAgent == affectorAgent) return;

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

      /// <summary>The hero behind an agent, or null when it is a nameless soldier (whose fall is a tally, not a deed).</summary>
      private static string HeroIdOf(Agent agent)
      {
         var character = agent?.Character as CharacterObject;

         return character?.HeroObject?.StringId;
      }

      /// <summary>cr.testall seam: record a deed as though the mission had reported it, so the real ledger is exercised without a battle.</summary>
      internal void DebugRecord(string heroDownedId, string downedPlayerById, int troopsFelled)
      {
         if (!string.IsNullOrEmpty(heroDownedId) && !_heroesDowned.Contains(heroDownedId)) _heroesDowned.Add(heroDownedId);
         if (!string.IsNullOrEmpty(downedPlayerById) && !_downedThePlayer.Contains(downedPlayerById)) _downedThePlayer.Add(downedPlayerById);
         TroopsFelled += troopsFelled;
      }
   }
}
