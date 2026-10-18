// Code written by Gabriel Mailhot, 18/10/2026.
// Extension Surface: the companion patrols, PUBLIC and read-only. tashmetu (Regard and Standing, 18/10/2026): R&S makes
// the patrolled place's lord grateful for each band broken and has the companion and the notables remember the patrol,
// but it read CompanionPatrolBehavior._patrols by reflection, "which will break the day you rename it". He asked for a
// read-only view, "or, even better, an event when a patrol ends (with the CompanionPatrol and how it ended: back,
// scattered, captured, killed) and one when it wins or loses a fight". Every patrol handed out is a copy, so no other mod
// can change one; only CR raises the events, each subscriber under a guard so one that throws breaks nothing.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Missions;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The companion patrols, read-only, and their public events. CR is the only raiser.</summary>
   public static class CrPatrols
   {
      /// <summary>The patrols under way (out, or riding home), as copies; empty when none or before a campaign is loaded.</summary>
      public static IReadOnlyList<CrPatrolInfo> Current
         => CompanionPatrolBehavior.Instance?.Snapshot() ?? (IReadOnlyList<CrPatrolInfo>) Array.Empty<CrPatrolInfo>();

      /// <summary>Fired after each battle a patrol fought, won or lost, once its tallies are counted.</summary>
      public static event Action<CrPatrolBattle> Fought;

      /// <summary>Fired when a patrol ends, however it ended (<see cref="CrPatrolEnd.How" />), after it left the list.</summary>
      public static event Action<CrPatrolEnd> Ended;

      // Raised by CR (internal). A caller only subscribes; it never raises.
      internal static void RaiseFought(CrPatrolBattle b) => Invoke(Fought, b);
      internal static void RaiseEnded(CrPatrolEnd e) => Invoke(Ended, e);

      private static void Invoke<T>(Action<T> evt, T arg) where T : class
      {
         if (evt == null || arg == null) return;

         foreach (Delegate handler in evt.GetInvocationList())
            try { ((Action<T>) handler)(arg); }
            catch (Exception ex)
            {
               ModLog.Error($"[PATROL EVENT] a subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }
   }

   /// <summary>How a patrol ended.</summary>
   public enum CrPatrolEndKind
   {
      /// <summary>Came back to the player and handed every man over (called back, or by letter).</summary>
      Back,

      /// <summary>The band was broken with no capture or death recorded; a free companion made their way back.</summary>
      Scattered,

      /// <summary>The companion was taken prisoner.</summary>
      Captured,

      /// <summary>The companion was killed.</summary>
      Killed
   }

   /// <summary>A patrol as it stood when copied. Changing it changes nothing in the game.</summary>
   public sealed class CrPatrolInfo
   {
      internal CrPatrolInfo(CompanionPatrol p)
      {
         Id = p.Id;
         CompanionId = p.CompanionId;
         CompanionName = p.CompanionName;
         SettlementId = p.SettlementId;
         SettlementName = p.SettlementName;
         PartyId = p.PartyId;
         StartDay = p.StartDay;
         MenAtDeparture = p.MenAtDeparture;
         BattlesWon = p.BattlesWon;
         BattlesLost = p.BattlesLost;
         FoesBeaten = p.FoesBeaten;
         RidingHome = p.State == CompanionPatrolState.Returning;
      }

      /// <summary>The patrol's own stable id.</summary>
      public string Id { get; }

      /// <summary>StringId of the companion who leads it.</summary>
      public string CompanionId { get; }

      public string CompanionName { get; }

      /// <summary>StringId of the settlement patrolled.</summary>
      public string SettlementId { get; }

      public string SettlementName { get; }

      /// <summary>StringId of the patrol's party (gone once the patrol ends).</summary>
      public string PartyId { get; }

      /// <summary>In-game day it rode out.</summary>
      public int StartDay { get; }

      /// <summary>Healthy men who rode out.</summary>
      public int MenAtDeparture { get; }

      public int BattlesWon { get; }

      public int BattlesLost { get; }

      /// <summary>Men the beaten foes brought to the field, over all its victories.</summary>
      public int FoesBeaten { get; }

      /// <summary>Ordered home and riding back; false while it patrols.</summary>
      public bool RidingHome { get; }
   }

   /// <summary>One battle a patrol fought, for <see cref="CrPatrols.Fought" />.</summary>
   public sealed class CrPatrolBattle
   {
      internal CrPatrolBattle(CrPatrolInfo patrol, bool won, string foeName, int foeMen, int gameDay)
      {
         Patrol = patrol;
         Won = won;
         FoeName = foeName;
         FoeMen = foeMen;
         GameDay = gameDay;
      }

      /// <summary>The patrol, its tallies already counting this battle.</summary>
      public CrPatrolInfo Patrol { get; }

      public bool Won { get; }

      /// <summary>The foe's leading party name ("a band" when unknown).</summary>
      public string FoeName { get; }

      /// <summary>The foe's healthy men when the battle began.</summary>
      public int FoeMen { get; }

      public int GameDay { get; }
   }

   /// <summary>A patrol's end, for <see cref="CrPatrols.Ended" />.</summary>
   public sealed class CrPatrolEnd
   {
      internal CrPatrolEnd(CrPatrolInfo patrol, CrPatrolEndKind how, int menBack, int gameDay)
      {
         Patrol = patrol;
         How = how;
         MenBack = menBack;
         GameDay = gameDay;
      }

      /// <summary>The patrol as it ended.</summary>
      public CrPatrolInfo Patrol { get; }

      public CrPatrolEndKind How { get; }

      /// <summary>Men handed back to the player (Back only; 0 otherwise).</summary>
      public int MenBack { get; }

      public int GameDay { get; }
   }
}
