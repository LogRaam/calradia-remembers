// Code written by Gabriel Mailhot, 08/10/2026.
// The lord who takes the player prisoner remembers how. Windmolen (Nexus, 08/10/2026): taken after a battle of a
// hundred men a side, his captor called him a lone spy crossing his lands. A battle leaves its memory only with the
// lords the player has already spoken with (WorldEventFeeders), and nothing told the captor how he took the player, so
// the model made a story up. On capture, the captor now remembers it (CaptureMemoryText): the battle when there was one,
// where, and the men the player led into it, read when the battle began, before the field took them.

#region

using System;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Writes the captor's memory of taking the player prisoner.</summary>
   public sealed class CaptureMemoryBehavior : CampaignBehaviorBase
   {
      /// <summary>TUNE: a capture this soon after the player's last battle began is that battle's capture.</summary>
      private const double BattleWindowHours = 24;

      private double _battleBeganHours = double.MinValue;
      private int _battleMen;
      private string _battlePlace;

      public override void RegisterEvents()
      {
         // Guarded here rather than through SafeRun: written while SafeRun lived in Diagnostics, whose cr.testall drives
         // this behaviour, so the reverse reference would have closed a namespace cycle. SafeRun is at the root since
         // 19/10/2026 and could serve; this guard is kept as it is.
         CampaignEvents.MapEventStarted.AddNonSerializedListener(this, (e, a, d) => Guard("battle", () => OnBattle(e)));
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, (capturer, prisoner) => Guard("taken", () => OnTaken(capturer, prisoner)));
      }

      private static void Guard(string step, Action act)
      {
         try { act(); }
         catch (Exception ex) { ModLog.Error($"[CAPTURE] {step} failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      public override void SyncData(IDataStore dataStore)
      {
      }

      private void OnBattle(MapEvent e)
      {
         if (e == null || !e.IsPlayerMapEvent || MobileParty.MainParty == null) return;

         _battleBeganHours = CampaignTime.Now.ToHours;
         _battleMen = MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty
            ? (int) MobileParty.MainParty.Army.TotalManCount
            : MobileParty.MainParty.MemberRoster?.TotalManCount ?? 0;
         _battlePlace = (e.MapEventSettlement ?? NearestSettlement())?.Name?.ToString();
      }

      private void OnTaken(PartyBase capturer, Hero prisoner)
      {
         if (prisoner != Hero.MainHero || capturer?.LeaderHero is not {IsLord: true} captor || captor == Hero.MainHero) return;

         bool afterBattle = CampaignTime.Now.ToHours - _battleBeganHours <= BattleWindowHours;
         Record(captor.StringId, new CaptureFacts {
            PlayerName = Hero.MainHero.Name?.ToString(), AfterBattle = afterBattle,
            Place = afterBattle ? _battlePlace : NearestSettlement()?.Name?.ToString(), PlayerMen = afterBattle ? _battleMen : 0
         });
      }

      /// <summary>Writes the captor's memory (cr.testall drives it with facts of its own).</summary>
      internal static MemoryResult Record(string captorId, CaptureFacts facts)
      {
         MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry {
            NpcId = captorId, Summary = CaptureMemoryText.Compose(facts), Kind = "Confrontation", DaysAgo = 0, SourceMod = "CalradiaRemembers (capture)"
         });
         ModLog.Info($"[CAPTURE] {captorId} remembers taking the player ({(facts.AfterBattle ? $"after a battle, {facts.PlayerMen} men" : "no battle")}, near {facts.Place ?? "-"}): {result}.");

         return result;
      }

      private static Settlement NearestSettlement()
      {
         if (MobileParty.MainParty == null) return null;
         Settlement best = null;
         float bestDistance = float.MaxValue;
         foreach (Settlement s in Settlement.All)
         {
            float d = s.GetPosition2D.DistanceSquared(MobileParty.MainParty.GetPosition2D);
            if (d >= bestDistance) continue;
            bestDistance = d;
            best = s;
         }

         return best;
      }
   }
}
