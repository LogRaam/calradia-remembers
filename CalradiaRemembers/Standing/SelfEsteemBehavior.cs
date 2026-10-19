// Code written by Gabriel Mailhot, 30/09/2026.
// SELF-ESTEEM, increment E2 (ROADMAP, ruled 30/09/2026): what befalls a character the player has met, read from the
// game's own events. Lordfadooboo: "a lord who has been humbled regularly might have it lowered, and one who is doing
// well might have it higher." Maifailun: captivity as trauma. Each listener only records; SelfEsteemPolicy decides.
// Nothing new is saved here: the record lives on the character's profile (SelfEsteemService).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Standing
{
   /// <summary>Listens for battles, captivities, tournaments, fiefs and deaths, and records them on met characters.</summary>
   public sealed class SelfEsteemBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
      {
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
         CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
         CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
      }

      /// <summary>Nothing of its own to save: the record lives on each character's profile.</summary>
      public override void SyncData(IDataStore dataStore) { }

      // A battle lost or won at the head of one's own party.
      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent is not {HasWinner: true}) return;

            // What the win was worth (fkasad, 01/10/2026: "half a dozen looters" is no feat): who led the beaten side,
            // and how strong they were against the winner, from the men each side brought into the fight.
            MapEventSide won = mapEvent.GetMapEventSide(mapEvent.WinningSide), beaten = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            int wonMen = won?.Parties?.Sum(p => p.HealthyManCountAtStart) ?? 0, beatenMen = beaten?.Parties?.Sum(p => p.HealthyManCountAtStart) ?? 0;
            SelfEsteemEvent? win = SelfEsteemPolicy.ClassifyWin(new BattleWorthFacts {
               EnemyHadLord = beaten?.Parties?.Any(p => p.Party?.LeaderHero is {IsLord: true}) == true,
               StrengthRatio = wonMen <= 0 ? 1f : beatenMen / (float) wonMen
            });

            if (win is SelfEsteemEvent counted)
               foreach (Hero h in Leaders(won?.Parties))
                  SelfEsteemService.Record(h, counted);
            foreach (Hero h in Leaders(mapEvent.GetMapEventSide(mapEvent.DefeatedSide)?.Parties))
               SelfEsteemService.Record(h, SelfEsteemEvent.BattleLost);
         }
         catch (Exception ex) { ModLog.Error($"[SELF-ESTEEM] battle read failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
         => SelfEsteemService.Record(prisoner, SelfEsteemEvent.Captured, capturer?.LeaderHero?.Name?.ToString());

      private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
      {
         if (detail == EndCaptivityDetail.Death) return;

         SelfEsteemService.Record(prisoner, SelfEsteemEvent.Released);
      }

      // The winner is lifted; the others lost a bout in front of a crowd.
      private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
      {
         try
         {
            if (winner?.HeroObject != null) SelfEsteemService.Record(winner.HeroObject, SelfEsteemEvent.TournamentWon, town?.Name?.ToString());
            foreach (CharacterObject c in participants ?? new MBReadOnlyList<CharacterObject>())
               if (c?.HeroObject != null && c != winner)
                  SelfEsteemService.Record(c.HeroObject, SelfEsteemEvent.TournamentLost);
         }
         catch (Exception ex) { ModLog.Error($"[SELF-ESTEEM] tournament read failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
                                            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
      {
         if (settlement == null || !(settlement.IsTown || settlement.IsCastle) || newOwner == oldOwner) return;

         if (oldOwner != null) SelfEsteemService.Record(oldOwner, SelfEsteemEvent.FiefLost, settlement.Name?.ToString());
         if (newOwner != null) SelfEsteemService.Record(newOwner, SelfEsteemEvent.FiefGained, settlement.Name?.ToString());
      }

      // A death in the house: parents, spouse, children, siblings.
      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         try
         {
            if (victim == null) return;

            var kin = new List<Hero> {victim.Father, victim.Mother, victim.Spouse};
            kin.AddRange(victim.Children ?? new MBList<Hero>());
            kin.AddRange(victim.Siblings ?? Enumerable.Empty<Hero>());

            foreach (Hero h in kin.Where(h => h != null).Distinct())
               SelfEsteemService.Record(h, SelfEsteemEvent.KinDied, $"your {KinWord(h, victim)} {victim.Name}");
         }
         catch (Exception ex) { ModLog.Error($"[SELF-ESTEEM] bereavement read failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // What the dead was to the mourner, for the day's cause ("the death of your brother Olek").
      private static string KinWord(Hero mourner, Hero dead)
      {
         if (dead.Father == mourner || dead.Mother == mourner) return dead.IsFemale ? "daughter" : "son";
         if (dead.Spouse == mourner) return dead.IsFemale ? "wife" : "husband";
         if (mourner.Father == dead || mourner.Mother == dead) return dead.IsFemale ? "mother" : "father";

         return dead.IsFemale ? "sister" : "brother";
      }

      private static IEnumerable<Hero> Leaders(MBReadOnlyList<MapEventParty> parties)
         => parties == null
            ? Enumerable.Empty<Hero>()
            : parties.Select(p => p?.Party?.LeaderHero).Where(h => h != null).Distinct();
   }
}
