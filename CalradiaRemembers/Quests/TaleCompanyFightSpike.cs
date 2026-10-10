// Code written by Gabriel Mailhot, 09/10/2026.
// SPIKE (Gabriel, 09/10/2026): a tale's last fight with only those who did the scene with the player, not the whole
// army (fkasad, 06/10/2026: "Players should choose who to bring... reusing the hideout entry screen"). The question
// this answers: can the game's own hideout battle (SandBoxMissions.OpenHideoutBattleMission, the mission a hideout
// assault opens with the troops the player picked) be opened on the REAL map battle with an ordinary band on the
// map, with a roster of the player and his companions only, and does the map battle then settle as it should (the
// band beaten or the player beaten, casualties applied, the encounter closed)? cr.tale_fight_spike spawns a small
// band beside the player; meeting it, the encounter menu offers "Fight with those beside you (spike)". Every step is
// logged under [SPIKE]. Nothing here is the feature: it is removed or rebuilt once the answer is known.

#region

using System;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>The spike's menu option and battle-end report. See the file header.</summary>
   public sealed class TaleCompanyFightSpike : CampaignBehaviorBase
   {
      private static string _bandId;

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, starter =>
            starter.AddGameMenuOption("encounter", "cr_tale_company_fight", "Fight with those beside you (spike)", OnCondition, OnConsequence));
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      public override void SyncData(IDataStore dataStore) { }

      /// <summary>cr.tale_fight_spike: a band of the region's bandits beside the player, to meet and fight.</summary>
      internal static string SpawnBand(int size)
      {
         MobileParty main = MobileParty.MainParty;
         if (main == null) return "No campaign.";
         Settlement near = Settlement.All.Where(s => s.IsVillage || s.IsTown).OrderBy(s => s.GetPosition2D.DistanceSquared(main.GetPosition2D)).FirstOrDefault();
         Vec2 at = main.GetPosition2D + new Vec2(0.6f, 0.6f);
         MobileParty band = AuthoredQuestBehavior.SpawnBandForSpike(at, near, "Spike band", Logic.AuthoredQuests.AuthoredBandMakeupPolicy.LocalTemplate, Math.Max(1, size));
         if (band == null) return "The band could not be spawned (see the log).";

         band.Ai.SetDoNotMakeNewDecisions(true);
         band.SetMoveGoToPoint(band.Position, MobileParty.NavigationType.Default);
         _bandId = band.StringId;
         ModLog.Info($"[SPIKE] band {band.StringId} of {band.MemberRoster.TotalManCount} spawned beside the player near {near?.Name}.");

         return $"A band of {band.MemberRoster.TotalManCount} stands beside you. Click it, and in the encounter menu choose \"Fight with those beside you (spike)\".";
      }

      private static bool OnCondition(MenuCallbackArgs args)
      {
         args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
         MapEvent battle = PlayerEncounter.Battle;
         PartyBase other = battle?.GetLeaderParty(PartyBase.MainParty.OpponentSide);

         return _bandId != null && other?.MobileParty?.StringId == _bandId;
      }

      private static void OnConsequence(MenuCallbackArgs args)
      {
         try
         {
            MapEvent battle = PlayerEncounter.Battle;
            PartyBase leader = battle.GetLeaderParty(PartyBase.MainParty.OpponentSide);
            BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, leader);
            if (PlayerEncounter.Current == null)
            {
               ModLog.Info("[SPIKE] the encounter ended on the hostile action; nothing to open.");

               return;
            }

            // The company: the player and the heroes riding with him, no regular troops.
            TroopRoster company = TroopRoster.CreateDummyTroopRoster();
            company.AddToCounts(Hero.MainHero.CharacterObject, 1);
            foreach (TroopRosterElement e in MobileParty.MainParty.MemberRoster.GetTroopRoster())
               if (e.Character is {IsHero: true} c && c.HeroObject != Hero.MainHero && c.HeroObject?.IsWounded == false)
                  company.AddToCounts(c, 1);

            Settlement hideout = Settlement.All.Where(s => s.IsHideout).OrderBy(s => s.GetPosition2D.DistanceSquared(MobileParty.MainParty.GetPosition2D)).FirstOrDefault();
            string scene = hideout?.LocationComplex?.GetLocationWithId("hideout_center")?.GetSceneName(0);
            ModLog.Info($"[SPIKE] opening the hideout battle: company {company.TotalManCount} ({string.Join(", ", company.GetTroopRoster().Select(e => e.Character.Name.ToString()))}), " +
                        $"band {leader.NumberOfHealthyMembers}, scene '{scene}' of {hideout?.Name}, mapEvent {battle.Id}, playerSide {PartyBase.MainParty.Side}.");
            if (string.IsNullOrEmpty(scene))
            {
               ModLog.Error("[SPIKE] no hideout scene found; nothing opened.");

               return;
            }

            CampaignMission.OpenHideoutBattleMission(scene, company.ToFlattenedRoster(), false);
            ModLog.Info("[SPIKE] hideout battle mission opened.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SPIKE] the company fight failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
         }
      }

      private static void OnMapEventEnded(MapEvent mapEvent)
      {
         if (_bandId == null || mapEvent == null) return;
         bool ours = mapEvent.InvolvedParties.Any(p => p.MobileParty?.StringId == _bandId);
         if (!ours) return;

         ModLog.Info($"[SPIKE] map battle ended: winner {mapEvent.WinningSide}, playerSide {PartyBase.MainParty.Side}, " +
                     $"player party now {MobileParty.MainParty.MemberRoster.TotalHealthyCount} healthy / {MobileParty.MainParty.MemberRoster.TotalWounded} wounded.");
      }
   }
}
