// Code written by Gabriel Mailhot, 01/10/2026.
// A hungry town remembers who fed it, and who emptied its market (fkasad and Lordfadooboo, Nexus, 01/10/2026). The rules
// are TownFoodPolicy's (pure, tested); this reads one market visit off the game (PlayerInventoryExchangeEvent: what the
// player bought and sold, in the town he stands in) and records what the town keeps as a world event placed there, so
// its notables and people hear it. The once-a-week dedup asks the persisted store, as the hardship news does.
// Increment 2: DeliverRelief puts the grain a character asked for into the hungry place he answers for.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Records a hungry town's memory of the player's food trade there.</summary>
   public sealed class FoodTradeMemoryBehavior : CampaignBehaviorBase
   {
      /// <summary>How far word of a famine relief carries: a deed the whole region talks of.</summary>
      private const int ReliefMagnitude = 35;

      /// <summary>How far word of hoarding carries: a disgrace that spreads, then fades.</summary>
      private const int HoardingMagnitude = 30;

      /// <summary>The phrase both memories share, by which the weekly dedup finds them again.</summary>
      private const string Marker = " loads of food ";

      public override void RegisterEvents() => CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, OnExchange);

      /// <summary>Nothing of its own: the memory lives in the world event store.</summary>
      public override void SyncData(IDataStore dataStore) { }

      private void OnExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
      {
         try
         {
            Settlement town = Settlement.CurrentSettlement;
            if (!isTrading || town?.Town == null || WorldEventStore.Instance == null) return;

            int bought = FoodUnits(purchased), soldUnits = FoodUnits(sold);
            if (bought == 0 && soldUnits == 0) return;

            var today = (int) CampaignTime.Now.ToDays;
            WorldEvent last = LastMemoryIn(town);
            FoodTradeVerdict verdict = TownFoodPolicy.Judge(new FoodTradeFacts {
               TownHungry = TownFoodPolicy.IsHungry(FactsOf(town)),
               FoodSold = soldUnits,
               FoodBought = bought,
               DaysSinceLastRemembered = last == null ? (int?) null : today - last.Day
            });

            if (verdict == FoodTradeVerdict.None) return;

            WorldEventStore.Instance.Record(EventFor(town, verdict, verdict == FoodTradeVerdict.Relief ? soldUnits - bought : bought - soldUnits, today));
            ModLog.Info($"[FOOD] {town.Name} remembers the player's food trade: {verdict} (sold {soldUnits}, bought {bought}).");
         }
         catch (Exception ex) { ModLog.Error($"[FOOD] reading a market visit failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>What the town's granary says, for TownFoodPolicy.</summary>
      internal static TownFoodFacts FactsOf(Settlement town)
         => new TownFoodFacts {IsStarving = town?.IsStarving ?? false, FoodStocks = town?.Town?.FoodStocks ?? 0f, FoodChange = town?.Town?.FoodChange ?? 0f};

      /// <summary>The world event a town keeps, worded for the people who will repeat it.</summary>
      internal static WorldEvent EventFor(Settlement town, FoodTradeVerdict verdict, int loads, int today)
      {
         string player = Hero.MainHero?.Name?.ToString() ?? "A traveller";
         bool relief = verdict == FoodTradeVerdict.Relief;
         string kingdom = town?.OwnerClan?.Kingdom?.StringId;

         return new WorldEvent {
            Type = relief ? WorldEventType.PlayerFeat : WorldEventType.Affront,
            Day = today,
            PlaceId = town?.StringId,
            InvolvesPlayer = true,
            Magnitude = relief ? ReliefMagnitude : HoardingMagnitude,
            Summary = relief
               ? $"{player} brought {loads}{Marker}to {town?.Name} while its people went hungry."
               : $"{player} bought up {loads}{Marker}in {town?.Name} while its people went hungry.",
            FactionIds = kingdom != null ? new List<string> {kingdom} : new List<string>()
         };
      }

      /// <summary>
      ///   Grain a character asked for, delivered while a place he answers for goes hungry: it goes into that place's
      ///   stores (up to the granary's ceiling) and the place remembers who brought it. Returns the place fed, or
      ///   null when nothing he answers for is hungry and the grain is simply his.
      /// </summary>
      internal static Settlement DeliverRelief(Hero giver, int units)
      {
         Settlement place = ReliefDestinationOf(giver);
         if (place?.Town == null || units <= 0) return null;

         Town town = place.Town;
         town.FoodStocks = TownFoodPolicy.StocksAfterRelief(town.FoodStocks, units, town.FoodStocksUpperLimit());
         WorldEventStore.Instance?.Record(EventFor(place, FoodTradeVerdict.Relief, units, (int) CampaignTime.Now.ToDays));
         ModLog.Info($"[FOOD] {units} grain asked for by {giver?.Name} went into the stores of {place.Name} (now {town.FoodStocks:0}).");

         return place;
      }

      /// <summary>The hungry place among those the character answers for, closest first, as his prompt orders them.</summary>
      internal static Settlement ReliefDestinationOf(Hero giver)
      {
         List<Settlement> seats = Mapping.HeroSeats.ClosestFirst(giver).Select(seat => seat.Place).Where(s => s.Town != null).ToList();

         string id = TownFoodPolicy.ReliefDestination(seats.Select(s => new ReliefSeat {SettlementId = s.StringId, Food = FactsOf(s)}));

         return id == null ? null : seats.FirstOrDefault(s => s.StringId == id);
      }

      private static WorldEvent LastMemoryIn(Settlement town)
      {
         Func<WorldEvent, bool> mine = e => e.PlaceId == town.StringId && e.InvolvesPlayer && (e.Summary ?? "").Contains(Marker);
         WorldEvent relief = WorldEventStore.Instance.MostRecentMatching(WorldEventType.PlayerFeat, mine);
         WorldEvent hoard = WorldEventStore.Instance.MostRecentMatching(WorldEventType.Affront, mine);

         return relief == null ? hoard : hoard == null || relief.Day >= hoard.Day ? relief : hoard;
      }

      private static int FoodUnits(List<(ItemRosterElement, int)> items)
         => items?.Where(x => x.Item1.EquipmentElement.Item?.IsFood == true).Sum(x => Math.Max(0, x.Item1.Amount)) ?? 0;
   }
}
