// Code written by Gabriel Mailhot, 23/10/2026.
// The player's prisoners, brought into the characters' memory as the game moves them. chmidt (Nexus, 23/10/2026):
// "Lords and ladies should also know who I put in their dungeons, which they refuse to acknowledge if I tell them that I
// put an enemy lord in their own town dungeon." Gabriel (23/10/2026): a listener dedicated to prisoners, to consolidate
// what happens in the game with the characters' memory. A lord the player takes remembers being taken; a prisoner the
// player leaves in a dungeon remembers it, and so does the lord who holds that place; when such a captivity ends, the
// captive remembers how. The dungeons the player filled are kept (an additive save key) so a lord's seat can say who
// left whom there (EncounterContextBuilder.ReadSeat). The captor's memory of taking the PLAYER is CaptureMemoryBehavior's;
// the realm's news of a lord taken is WorldEventFeeders'; a captive handed to his enemies is the deed sites' own.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Writes the memories of the player's prisoners: taken, left in a dungeon, and how it ended.</summary>
   public sealed class PrisonerMemoryBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_PlayerDungeons_v1";

      // Where the player left each prisoner lives in Mapping.PlayerDungeons (the encounter builder reads it); this saves it.
      public PrisonerMemoryBehavior() => PlayerDungeons.Reset();

      public override void RegisterEvents()
      {
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, (capturer, prisoner) => SafeRun.Step("PrisonerMemory.Taken", () => OnTaken(capturer, prisoner)));
         CampaignEvents.OnPrisonerDonatedToSettlementEvent.AddNonSerializedListener(this, (party, roster, place) => SafeRun.Step("PrisonerMemory.Donated", () => OnDonated(party, roster, place)));
         CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, (prisoner, party, faction, detail, notify) => SafeRun.Step("PrisonerMemory.Released", () => OnReleased(prisoner, party, detail)));
      }

      public override void SyncData(IDataStore dataStore)
      {
         string blob = PlayerDungeons.Write();
         dataStore.SyncData(SaveKey, ref blob);
         if (dataStore.IsLoading) PlayerDungeons.Read(blob);
      }

      private static string PlayerName => Hero.MainHero?.Name?.ToString();

      private static void OnTaken(PartyBase capturer, Hero prisoner)
      {
         if (prisoner == null || prisoner == Hero.MainHero || !prisoner.IsLord || capturer?.LeaderHero != Hero.MainHero) return;

         bool afterBattle = MobileParty.MainParty?.MapEvent != null || capturer.MapEvent != null;
         Settlement place = (MobileParty.MainParty?.MapEvent ?? capturer.MapEvent)?.MapEventSettlement ?? NearestSettlement();
         Remember(prisoner, PrisonerMemoryText.Taken(PlayerName, afterBattle, place?.Name?.ToString()));
      }

      private static void OnDonated(MobileParty donor, FlattenedTroopRoster roster, Settlement place)
      {
         if (donor != MobileParty.MainParty || roster == null || place == null) return;

         foreach (FlattenedTroopRosterElement el in roster)
         {
            Hero captive = el.Troop?.HeroObject;
            if (captive == null || captive == Hero.MainHero) continue;

            RecordLeftInDungeon(captive, place);
         }
      }

      /// <summary>
      ///   The player left <paramref name="captive" /> in <paramref name="place" />'s dungeon: the captive remembers it, and
      ///   so do the head of the house that holds the place and its governor (when they are not the player's). Also the
      ///   cr.testall seam.
      /// </summary>
      internal static void RecordLeftInDungeon(Hero captive, Settlement place)
      {
         PlayerDungeons.Left(captive, place);
         Remember(captive, PrisonerMemoryText.LeftInDungeon(PlayerName, place.Name?.ToString()));

         string label = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = captive.Name?.ToString(), ClanName = captive.Clan?.Name?.ToString()});
         string line = PrisonerMemoryText.DepositedWithMe(PlayerName, label, place.Name?.ToString());
         foreach (Hero keeper in new[] {place.OwnerClan?.Leader, place.Town?.Governor}.Where(h => h != null && h != Hero.MainHero && h != captive).Distinct())
            Remember(keeper, line);

         ModLog.Info($"[PRISONER] {captive.Name} left by the player in the dungeon of {place.Name}.");
      }

      /// <summary>cr.testall seam: forgets that the player left this prisoner anywhere.</summary>
      internal static void DebugForget(string heroId) => PlayerDungeons.Release(heroId);

      private static void OnReleased(Hero prisoner, PartyBase party, EndCaptivityDetail detail)
      {
         if (prisoner == null || prisoner == Hero.MainHero || detail == EndCaptivityDetail.Death) return;

         bool inThePlayersHands = party != null && (party == MobileParty.MainParty?.Party || party.Settlement?.OwnerClan == Clan.PlayerClan);
         string leftAt = PlayerDungeons.Release(prisoner.StringId);
         if (!inThePlayersHands && leftAt == null) return;

         string dungeon = inThePlayersHands ? null : Settlement.Find(leftAt)?.Name?.ToString();
         Remember(prisoner, PrisonerMemoryText.Released(EndOf(detail), PlayerName, dungeon));
      }

      private static CaptivityEnd EndOf(EndCaptivityDetail detail) => detail switch
      {
         EndCaptivityDetail.Ransom => CaptivityEnd.Ransomed,
         EndCaptivityDetail.ReleasedAfterPeace => CaptivityEnd.PeaceMade,
         EndCaptivityDetail.ReleasedAfterBattle => CaptivityEnd.FreedInBattle,
         EndCaptivityDetail.ReleasedAfterEscape => CaptivityEnd.Escaped,
         EndCaptivityDetail.ReleasedByCompensation => CaptivityEnd.Compensated,
         _ => CaptivityEnd.SetFree
      };

      // A memory on a hero's own profile, made from the hero when they have none: being taken or locked away by the player
      // is having met them.
      private static void Remember(Hero hero, string line)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || hero == null || string.IsNullOrEmpty(line)) return;

         NpcProfile profile = store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Other, line));
         store.Set(profile);
      }

      private static Settlement NearestSettlement()
      {
         if (MobileParty.MainParty == null) return null;

         return Settlement.All.OrderBy(s => s.GetPosition2D.DistanceSquared(MobileParty.MainParty.GetPosition2D)).FirstOrDefault();
      }
   }
}
