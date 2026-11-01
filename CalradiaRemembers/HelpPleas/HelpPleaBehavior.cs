// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): "Friends, relatives, and allies could write to the player when they need help." The game sees
// a crisis (a siege, a ruin, a lost fight, a lost fief, an empty granary), lets one person who may ask write about it, and
// holds the player to what happens next, as the war duty does: by the deed, never by what became of the letter (Gabriel,
// 01/11/2026). A relief promised is a journal entry this behavior closes itself, because a deadline lapse in the quest service
// could not tell a broken word from a player who rode and came too late. Money moves on the letter's own button, as a
// prisoner offer's does; food is a real hand-over, by the supply quest that already exists.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.HelpPleas;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.HelpPleas
{
   /// <summary>Sees a friend's crisis, sends the plea, and holds the player to what happens next.</summary>
   public sealed class HelpPleaBehavior : CampaignBehaviorBase, ILetterPleaDesk
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_help_pleas";

      // A letter that never arrived after this long is lost; its plea must not stand open forever.
      private const int DaysALetterMayTake = 40;

      /// <summary>cr.test all seam: the letters are not written (no model call), everything else runs.</summary>
      internal static bool DebugSuppressLetters;

      /// <summary>cr.test all seam: a verdict leaves no grievance or kindness behind, so a test leaves the world as it found it.</summary>
      internal static bool DebugSuppressKeepsakes;

      // A lost fight or fief, waiting for the day's tick. Not saved: after a reload the moment has passed.
      private readonly List<Sighting> _fresh = new List<Sighting>();

      public HelpPleaBehavior()
      {
         Instance = this;
         LetterPleas.Desk = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static HelpPleaBehavior Instance { get; private set; }

      /// <summary>The ledger (never null).</summary>
      public HelpPleaLedger Ledger { get; internal set; } = new HelpPleaLedger();

      /// <summary>Today, in campaign days.</summary>
      internal static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[HELP PLEA] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[HELP PLEA] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            Ledger = HelpPleaLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      #region the letter's side

      /// <summary>The plea from this writer still waiting for the player's answer, or null.</summary>
      internal HelpPlea Waiting(string senderId)
      {
         HelpPlea plea = Ledger.From(senderId);

         return plea is {Answer: HelpAnswer.None, ReceivedOnDay: not null} ? plea : null;
      }

      /// <inheritdoc />
      public PleaOnLetter AwaitingAnswerFrom(string senderId)
      {
         HelpPlea plea = Waiting(senderId);
         if (plea == null) return null;

         HelpIncapacity incapacity = IncapacityFor(plea);

         return new PleaOnLetter {
            CanAccept = incapacity == HelpIncapacity.None,
            AcceptText = HelpPleaWording.AcceptButton(plea.Ask, plea.Amount),
            DeclineText = HelpPleaWording.DeclineButton(incapacity),
            Note = HelpPleaWording.Note(plea.Ask, incapacity, plea.DeadlineDay - Today)
         };
      }

      /// <inheritdoc />
      public void OnLetterStopped(string senderId)
      {
         HelpPlea plea = Ledger.From(senderId);
         if (plea == null || plea.ReceivedOnDay.HasValue) return;

         Close(plea, null, false, false);
      }

      /// <inheritdoc />
      public bool StillStands(string senderId) => Ledger.From(senderId) != null;

      /// <inheritdoc />
      public void Accept(string senderId) => Accept(Waiting(senderId));

      /// <inheritdoc />
      public void Refuse(string senderId) => Refuse(Waiting(senderId));

      /// <summary>What stops the player from doing what this plea asks, today.</summary>
      internal static HelpIncapacity IncapacityFor(HelpPlea plea) => HelpPleaPolicy.IncapacityOf(Means(plea.Ask, plea.Amount));

      /// <summary>The courier handed the letter over: the player's time starts now. Called once, on arrival, thrown or not.</summary>
      public void OnLetterReached(string senderId, int today)
      {
         HelpPlea plea = Ledger.From(senderId);
         if (plea == null || plea.ReceivedOnDay.HasValue) return;

         Ledger.MarkReceived(plea, today);
         ModLog.Info($"[HELP PLEA] {plea.Id} reached the player: {plea.Ask}, deadline day {plea.DeadlineDay}.");
      }

      /// <summary>The player said yes on the letter.</summary>
      internal void Accept(HelpPlea plea)
      {
         if (plea == null) return;
         Hero sender = FindHero(plea.SenderId);
         if (sender == null) return;

         switch (plea.Ask)
         {
            case HelpAsk.Relief:
               Promise(plea, sender);
               break;
            case HelpAsk.Coin:
               GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, sender, plea.Amount);
               plea.Answer = HelpAnswer.Promised;
               Close(plea, sender, true, false);
               break;
            default:
               plea.Answer = HelpAnswer.Promised;
               OpenFoodQuest(plea, sender);
               Ledger.Open.Remove(plea); // the supply quest holds the player to it from here
               break;
         }
      }

      /// <summary>The player said no on the letter. A siege stays open: it is the end of the siege that says what the no cost.</summary>
      internal void Refuse(HelpPlea plea)
      {
         if (plea == null) return;

         plea.Answer = HelpAnswer.Refused;
         plea.IncapacityWhenAnswered = IncapacityFor(plea);
         if (plea.Ask != HelpAsk.Relief) Close(plea, FindHero(plea.SenderId), false, false);
      }

      private static void Promise(HelpPlea plea, Hero sender)
      {
         plea.Answer = HelpAnswer.Promised;
         plea.DistanceWhenPromised = DistanceToPlayer(FindPlace(plea.PlaceId));

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(sender.StringId);
         if (profile == null) return;

         // No deadline on the quest: the plea's own close judges a late rider, which a lapse could not tell from a broken word.
         var quest = new InformalQuest {
            Type = QuestType.External,
            Description = HelpPleaWording.JournalLine(sender.Name.ToString(), plea.PlaceName, HelpPleaPolicy.DaysToReachTheSiege),
            IssuedOnDay = Today,
            ExternalId = plea.Id,
            ExternalSourceMod = "Calradia Remembers",
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, sender);
      }

      private static void OpenFoodQuest(HelpPlea plea, Hero sender)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(sender.StringId);
         if (profile == null) return;

         int today = Today;
         var quest = new InformalQuest {
            Type = QuestType.ProvideGoods,
            Description = $"Bring {plea.Amount} loads of food to {sender.Name}, as you promised.",
            RequiredItemCategory = ProvideGoodsPolicy.Food,
            RequiredItemCount = plea.Amount,
            IssuedOnDay = today,
            DeadlineDay = today + HelpPleaPolicy.DaysToBringFood,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, sender);
      }

      #endregion

      #region the day

      private void OnHourlyTick()
      {
         try
         {
            foreach (HelpPlea plea in Ledger.Open.Where(p => p.Ask == HelpAsk.Relief && p.ReceivedOnDay.HasValue).ToList())
            {
               // Not asking whether the siege still stands: a player who just broke it is at the walls, with no siege left.
               Settlement place = FindPlace(plea.PlaceId);
               if (place != null && IsAt(place)) Close(plea, FindHero(plea.SenderId), true, false);
            }
         }
         catch (Exception ex) { ModLog.Error($"[HELP PLEA] hourly tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnDailyTick()
      {
         try
         {
            int today = Today;
            Upkeep(today);
            Ledger.Prune(today);

            foreach (Sighting s in Sightings(today)) TryOpen(s, today);
            _fresh.Clear();
         }
         catch (Exception ex) { ModLog.Error($"[HELP PLEA] daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Closes every plea whose crisis has ended or whose time is up. Internal for cr.test all.</summary>
      internal void Upkeep(int today)
      {
         foreach (HelpPlea plea in Ledger.Open.ToList())
         {
            Hero sender = FindHero(plea.SenderId);
            // Gone, or now an enemy: nobody is left to hold the player to it.
            if (sender == null || sender.IsDead || AtWarWithThePlayer(sender))
            {
               Close(plea, null, false, false);
               continue;
            }

            if (!plea.ReceivedOnDay.HasValue)
            {
               bool crisisOver = plea.Ask == HelpAsk.Relief && FindPlace(plea.PlaceId)?.IsUnderSiege != true;
               if (crisisOver || today - plea.OpenedOnDay > DaysALetterMayTake) Close(plea, null, false, false);
               continue;
            }

            if (plea.Ask == HelpAsk.Relief)
            {
               Settlement place = FindPlace(plea.PlaceId);
               bool over = place == null || !place.IsUnderSiege;
               if (over || today > plea.DeadlineDay) Close(plea, sender, false, place != null && place.OwnerClan?.StringId != plea.ClanId);
            }
            else if (today > plea.DeadlineDay) Close(plea, sender, false, false);
         }
      }

      // Judges the plea, leaves its mark on the writer and takes it off the ledger. A null sender closes it with no consequence.
      private void Close(HelpPlea plea, Hero sender, bool helpCame, bool disaster)
      {
         Ledger.Open.Remove(plea);

         Settlement place = FindPlace(plea.PlaceId);
         HelpVerdict verdict = sender == null ? new HelpVerdict {Feeling = HelpFeeling.Nothing} : HelpPleaPolicy.Judge(new HelpCloseFacts {
            LetterReached = plea.ReceivedOnDay.HasValue,
            HelpCame = helpCame,
            Answer = plea.Answer,
            Incapacity = plea.Answer == HelpAnswer.Refused ? plea.IncapacityWhenAnswered : IncapacityFor(plea),
            Disaster = disaster,
            WasOnTheWay = plea.Answer == HelpAnswer.Promised && place != null && DistanceToPlayer(place) < plea.DistanceWhenPromised
         });

         if (sender != null) Leave(plea, sender, verdict, disaster);
         CloseJournal(plea, verdict);
         ModLog.Info($"[HELP PLEA] {plea.Id} closed: {verdict.Feeling} (regard {verdict.RegardDelta:+#;-#;0}, disaster {disaster}).");
      }

      private static void Leave(HelpPlea plea, Hero sender, HelpVerdict verdict, bool disaster)
      {
         int today = Today;
         string memory = HelpPleaWording.Memory(verdict, plea.Ask, plea.PlaceName, disaster);
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(sender.StringId);
         if (profile != null && memory != null)
         {
            if (verdict.RegardDelta != 0) ProfileMutator.ApplyReputationDelta(profile, verdict.RegardDelta, RegardCause.Letter);
            if (verdict.Feeling == HelpFeeling.Betrayed) StanceService.OnPromiseBroken(profile);
            profile.Events.Add(new NotableEvent(today, MemoryKind(verdict.Feeling), memory));
            behavior.Store.Set(profile);
         }

         if (DebugSuppressKeepsakes) return;

         string cause = HelpPleaWording.Cause(verdict, plea.Ask, plea.PlaceName);
         if (verdict.GrudgeMagnitude > 0)
            GrudgeBehavior.Instance?.TryPlant(sender, Hero.MainHero, GrudgeSource.AbandonedPlea, cause, verdict.GrudgeMagnitude, knownToPlayer: true, today);
         if (verdict.KindnessMagnitude > 0)
            AppreciationBehavior.Instance?.TryPlant(sender, Hero.MainHero,
               plea.Ask == HelpAsk.Coin ? AppreciationSource.GoldReceived : AppreciationSource.TroopsOrInfluenceSpentForThem, cause,
               verdict.KindnessMagnitude, knownToPlayer: true, today);
         if (verdict.Feeling == HelpFeeling.Thankful)
            MBInformationManager.AddQuickInformation(new TextObject($"{sender.Name} will remember that you answered."));
      }

      private static NotableEventType MemoryKind(HelpFeeling feeling)
      {
         switch (feeling)
         {
            case HelpFeeling.Thankful: return NotableEventType.Collaboration;
            case HelpFeeling.Betrayed: return NotableEventType.Betrayal;
            case HelpFeeling.Abandoned:
            case HelpFeeling.Disappointed: return NotableEventType.Conflict;
            default: return NotableEventType.Other;
         }
      }

      private static void CloseJournal(HelpPlea plea, HelpVerdict verdict)
      {
         if (plea.Ask != HelpAsk.Relief || plea.Answer != HelpAnswer.Promised) return;

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(plea.SenderId);
         InformalQuest quest = profile?.ActiveQuests.FirstOrDefault(q => q.ExternalId == plea.Id && q.Status == QuestStatus.Active);
         if (quest == null) return;

         Hero giver = FindHero(plea.SenderId);
         switch (verdict.Feeling)
         {
            case HelpFeeling.Thankful:
               quest.Status = QuestStatus.Completed;
               QuestJournalBridge.CloseAsCompleted(quest, giver);
               break;
            case HelpFeeling.Betrayed:
               quest.Status = QuestStatus.Expired;
               QuestJournalBridge.CloseAsFailed(quest, giver, $"You did not come to {plea.PlaceName}.");
               break;
            default:
               quest.Status = QuestStatus.Cancelled;
               QuestJournalBridge.CloseAsCancelled(quest, giver, $"The siege of {plea.PlaceName} is over.");
               break;
         }

         behavior.Store.Set(profile);
      }

      #endregion

      #region the crises

      private sealed class Sighting
      {
         public HelpCrisis Crisis;
         public Clan House;
         public Hero Person;
         public Settlement Place;
         public string Enemy;
         public string Key;
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            NoteThePlayersFight(mapEvent);
            if (mapEvent is not {IsFieldBattle: true, HasWinner: true}) return;

            foreach (MapEventParty party in mapEvent.GetMapEventSide(mapEvent.DefeatedSide).Parties)
            {
               Hero lord = party.Party?.LeaderHero;
               if (lord?.Clan == null || lord == Hero.MainHero || lord.IsPrisoner) continue;

               _fresh.Add(new Sighting {
                  Crisis = HelpCrisis.BattleLost, House = lord.Clan, Person = lord, Key = $"battle:{lord.StringId}:{Today}",
                  Enemy = mapEvent.GetMapEventSide(mapEvent.WinningSide).MapFaction?.Name?.ToString()
               });
            }
         }
         catch (Exception ex) { ModLog.Error($"[HELP PLEA] map event failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // Fighting the besiegers is coming to the place, wherever the party stood when the battle ended.
      private void NoteThePlayersFight(MapEvent mapEvent)
      {
         if (mapEvent is not {IsPlayerMapEvent: true} || mapEvent.DefenderSide?.IsMainPartyAmongParties() != true) return;

         string place = mapEvent.MapEventSettlement?.StringId;
         HelpPlea plea = Ledger.Open.FirstOrDefault(p => p.Ask == HelpAsk.Relief && p.ReceivedOnDay.HasValue && p.PlaceId == place);
         if (plea != null) Close(plea, FindHero(plea.SenderId), true, false);
      }

      private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer,
                                            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
      {
         if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || oldOwner?.Clan == null) return;

         _fresh.Add(new Sighting {
            Crisis = HelpCrisis.FiefLost, House = oldOwner.Clan, Place = settlement, Enemy = newOwner?.MapFaction?.Name?.ToString(),
            Key = $"fief:{settlement.StringId}:{Today}"
         });
      }

      private IEnumerable<Sighting> Sightings(int today)
      {
         foreach (Sighting s in _fresh) yield return s;

         foreach (Settlement place in Settlement.All.Where(s => s.IsFortification && s.OwnerClan != null))
         {
            if (place.IsUnderSiege && place.SiegeEvent != null)
            {
               int began = (int) place.SiegeEvent.SiegeStartTime.ToDays;
               if (today - began >= HelpPleaPolicy.DaysBeforeASiegeCounts)
                  yield return new Sighting {
                     Crisis = HelpCrisis.Besieged, House = place.OwnerClan, Place = place, Key = $"siege:{place.StringId}:{began}",
                     Enemy = place.SiegeEvent.BesiegerCamp?.LeaderParty?.MapFaction?.Name?.ToString()
                  };
            }
            else if (place.Town != null && place.Town.FoodStocks <= HelpPleaPolicy.StarvingFoodStocks)
               yield return new Sighting {Crisis = HelpCrisis.Starving, House = place.OwnerClan, Place = place, Key = $"hunger:{place.StringId}:{today / HelpPleaPolicy.DaysBetweenPleas}"};
         }

         foreach (Clan house in Clan.NonBanditFactions.Where(c => !c.IsEliminated && c != Clan.PlayerClan && c.Leader != null))
            if (house.Gold < HelpPleaPolicy.RuinedGold)
               yield return new Sighting {Crisis = HelpCrisis.Ruined, House = house, Key = $"ruin:{house.StringId}:{today / HelpPleaPolicy.DaysBetweenPleas}"};
      }

      private void TryOpen(Sighting s, int today)
      {
         if (s.House == Clan.PlayerClan || Ledger.PleadedCrises.ContainsKey(s.Key)) return;
         if (!HelpPleaPolicy.HouseIsInNeed(s.Crisis, s.House.Gold)) return;

         HelpAsk ask = HelpPleaPolicy.AskFor(s.Crisis);
         int amount = AmountFor(ask);
         if (ask == HelpAsk.Coin && amount <= 0) return;
         if (HelpPleaPolicy.IncapacityOf(Means(ask, amount)) != HelpIncapacity.None) return;

         Hero writer = BestWriter(s, today);
         if (writer == null) return;

         Open(writer, s.Crisis, s.House, s.Place, s.Enemy, amount, s.Key, today);
      }

      private static int AmountFor(HelpAsk ask)
      {
         switch (ask)
         {
            case HelpAsk.Coin: return HelpPleaPolicy.CoinAsked(Hero.MainHero?.Gold ?? 0);
            case HelpAsk.Food: return HelpPleaPolicy.FoodAsked;
            default: return 0;
         }
      }

      private Hero BestWriter(Sighting s, int today)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null || behavior.LlmServices?.ChatService == null) return null;

         Hero best = null;
         int bestRank = int.MinValue;
         IEnumerable<Hero> house = s.Person != null ? new[] {s.Person} : s.House.Heroes;
         foreach (Hero hero in house)
         {
            if (hero == Hero.MainHero || !ActionGate.Allows(hero, ActionKind.Letter) || HeroProximity.IsNearPlayer(hero)) continue;

            NpcProfile profile = behavior.Store.Get(hero.StringId);
            if (profile == null || LetterBox.IsSilenced(profile, LetterReason.ReinforcementRequest)) continue;

            HelpWriterFacts facts = WriterFacts(hero, profile, s.Crisis);
            bool mayOpen = HelpPleaPolicy.MayOpen(new HelpOpeningFacts {
               Today = today,
               LastPleaDay = Ledger.LastPleaDay.TryGetValue(hero.StringId, out int last) ? last : (int?) null,
               WriterHasOpenPlea = Ledger.From(hero.StringId) != null,
               OpenPleas = Ledger.Open.Count
            });
            if (!mayOpen || !HelpPleaPolicy.MayWrite(facts) || HelpPleaPolicy.WriterRank(facts) <= bestRank) continue;

            best = hero;
            bestRank = HelpPleaPolicy.WriterRank(facts);
         }

         return best;
      }

      private static HelpWriterFacts WriterFacts(Hero hero, NpcProfile profile, HelpCrisis crisis)
      {
         Clan mine = Clan.PlayerClan;

         return new HelpWriterFacts {
            HasMet = hero.HasMet,
            InThePlayersClan = hero.Clan == mine,
            AtWarWithThePlayer = AtWarWithThePlayer(hero),
            IsKin = EncounterContextBuilder.ResolveKinshipToPlayer(hero) != null,
            IsWarAlly = mine?.Kingdom != null && hero.Clan?.Kingdom == mine.Kingdom && crisis is not (HelpCrisis.Ruined or HelpCrisis.Starving),
            Regard = profile.ReputationWithPlayer
         };
      }

      /// <summary>Opens a plea and seals its letter. Internal for cr.help_plea and cr.test all.</summary>
      internal HelpPlea Open(Hero writer, HelpCrisis crisis, Clan house, Settlement place, string enemy, int amount, string key, int today)
      {
         HelpAsk ask = HelpPleaPolicy.AskFor(crisis);
         string placeName = place?.Name?.ToString();
         HelpPlea plea = Ledger.Add(new HelpPlea {
            SenderId = writer.StringId, ClanId = house?.StringId, Crisis = crisis, Ask = ask, PlaceId = place?.StringId, PlaceName = placeName, Amount = amount
         }, key, today);

         string context = HelpPleaWording.LetterContext(crisis, ask, placeName, enemy, amount);
         ModLog.Info($"[HELP PLEA] {plea.Id} from {writer.Name}: {crisis}, asks {ask} {amount} ({placeName ?? "no place"}).");

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (DebugSuppressLetters || behavior?.LlmServices == null || behavior.Store == null) return plea;

         NpcProfile profile = behavior.Store.Get(writer.StringId) ?? HeroProfileMapper.FromHero(writer);
         LetterGenerationService.QueueInitialLetter(profile, writer, LetterReason.ReinforcementRequest, context, behavior.LlmServices, behavior.Store, today);

         return plea;
      }

      #endregion

      #region reading the world

      private static HelpMeansFacts Means(HelpAsk ask, int amount)
      {
         Clan mine = Clan.PlayerClan;

         return new HelpMeansFacts {
            Ask = ask,
            Amount = amount,
            IsCaptive = Hero.MainHero?.IsPrisoner == true,
            OwnFiefBesieged = mine?.Settlements.Any(s => s.IsUnderSiege) == true,
            PartyMen = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0,
            Gold = Hero.MainHero?.Gold ?? 0
         };
      }

      private static bool AtWarWithThePlayer(Hero hero)
      {
         IFaction theirs = hero?.MapFaction;
         IFaction mine = Hero.MainHero?.MapFaction;

         return theirs != null && mine != null && theirs != mine && theirs.IsAtWarWith(mine);
      }

      private static bool IsAt(Settlement place) => Hero.MainHero?.IsPrisoner != true && DistanceToPlayer(place) <= HelpPleaPolicy.ArrivalDistance;

      private static float DistanceToPlayer(Settlement place)
      {
         MobileParty main = MobileParty.MainParty;

         return main == null || place == null ? float.MaxValue : main.GetPosition2D.Distance(place.GetPosition2D);
      }

      private static Hero FindHero(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);

      private static Settlement FindPlace(string id) => string.IsNullOrEmpty(id) ? null : Settlement.Find(id);

      #endregion
   }
}
