// Code written by Gabriel Mailhot, 15/10/2026.
// The vassal war duty, engine side (Maifailun, Nexus 15/10/2026; Gabriel's ladder the same day: a summons to answer
// in person, a levy in coin, or troops, horses or arms, and a fief taken if nothing is given). The rules are pure
// (Logic/WarDuty); this keeps their ledger. It runs whether or not the MCM switch is on, so turning the duty on later
// never finds a stale ledger that would summon the player at once: the switch gates only what the RULER does.
//
// Service is any battle the player fights against a realm his own is at war with (won or lost: he rode), a siege
// among them, and every day his party marches in an army of his realm. Bandits are not the realm's war.
//
// The reckoning (switch on): the day WarDutyPolicy says he owes, the ruler opens it, writes the SUMMONS, and holds a
// standing aim of Calradia Remembers' own in CrAims (Gabriel: "passe par la nouvelle architecture"). Coming before the
// ruler (a conversation with him, or marching in his army) moves it to the levy, demanded face to face; never coming
// brings the levy by letter. Serving settles it at any step. At the last deadline, a fief is taken (WarDutyConfiscation)
// or, with none, the ruler only remembers. Every step leaves the ruler a memory.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Aims;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Aims;
using CalradiaRemembers.Logic.WarDuty;
using CalradiaRemembers.Storage;
using Helpers;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.WarDuty
{
   /// <summary>Keeps the war-duty ledger: the oath, the realm's wars, and the player's service.</summary>
   public sealed class WarDutyBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_war_duty";

      /// <summary>The id of the standing aim the war duty holds in CrAims on the ruler.</summary>
      internal const string AimId = "war_duty";

      private static bool _subscribed;

      /// <summary>cr.testall seam: the letters are not written (no model call), everything else runs.</summary>
      internal static bool DebugSuppressLetters;

      public WarDutyBehavior()
      {
         Instance = this;

         // A conversation with the ruler is coming before him. The event is static: subscribe once, route to the live one.
         if (_subscribed) return;
         _subscribed = true;
         CrConversation.Started += info => Instance?.OnConversationStarted(info?.NpcId);
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static WarDutyBehavior Instance { get; private set; }

      /// <summary>The ledger (never null).</summary>
      public WarServiceLedger Ledger { get; internal set; } = new WarServiceLedger();

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[WAR DUTY] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[WAR DUTY] save failed: {ex.GetType().Name}: {ex.Message}");
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

            Ledger = WarServiceLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>The game day, as the ledger counts it.</summary>
      internal static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>What the duty reads live today.</summary>
      internal static WarDutyLive Live()
      {
         Clan clan = Clan.PlayerClan;
         Kingdom kingdom = clan?.Kingdom;

         return new WarDutyLive {
            IsSwornVassal = kingdom != null && !clan.IsUnderMercenaryService,
            RulesTheRealm = kingdom != null && kingdom.RulingClan == clan,
            IsCaptive = Hero.MainHero?.IsPrisoner == true,
            WindowDays = ModSettings.WarDutyWindowDays
         };
      }

      /// <summary>The realm the player is sworn to (not as a mercenary), or null.</summary>
      internal static Kingdom SwornKingdom()
      {
         Clan clan = Clan.PlayerClan;

         return clan?.Kingdom != null && !clan.IsUnderMercenaryService ? clan.Kingdom : null;
      }

      /// <summary>Brings the ledger up to today: the oath, the wars, a day in the realm's army. Also the cr.testall seam.</summary>
      internal void Upkeep(int today)
      {
         Kingdom kingdom = SwornKingdom();
         Ledger.NoteSworn(kingdom?.StringId, today);
         Ledger.NoteWars(kingdom == null ? Enumerable.Empty<string>() : FactionHelper.GetEnemyKingdoms(kingdom).Select(k => k.StringId), today);

         Army army = MobileParty.MainParty?.Army;
         if (kingdom != null && army != null && army.Kingdom == kingdom)
         {
            Ledger.NoteServed(today);
            if (ModSettings.EnableWarDuty)
            {
               WarDutyCase c = Ledger.Case;
               if (c is {IsOpen: true} && army.LeaderParty?.LeaderHero?.StringId == c.RulerId) Step(WarDutyEvent.Appeared, today);
               Step(WarDutyEvent.Served, today);
            }
         }

         // The duty ends with the war, or when he is no longer the realm's sworn vassal.
         WarDutyCase open = Ledger.Case;
         if (open is {IsOpen: true} && (kingdom == null || kingdom.StringId != open.KingdomId || Ledger.WarSeenSince.Count == 0))
            Step(WarDutyEvent.DutyEnded, today);
      }

      private void OnDailyTick()
      {
         try
         {
            int today = Today;
            Upkeep(today);
            if (ModSettings.EnableWarDuty) Reckon(today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WAR DUTY] daily upkeep failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The day's reckoning, with the switch on: checks an open reckoning's deadline, or opens one when the player
      ///   owes. Returns the verdict of the day. Also the cr.testall seam.
      /// </summary>
      internal WarDutyStanding Reckon(int today)
      {
         if (Ledger.Case is {IsOpen: true})
         {
            // A captive cannot come or ride: his days do not run while he is held.
            if (Hero.MainHero?.IsPrisoner == true)
            {
               Ledger.Case.DeadlineDay++;

               return WarDutyStanding.Captive;
            }

            Step(WarDutyEvent.DayPassed, today);

            return WarDutyStanding.AlreadyCalledThisWar;
         }

         WarDutyStanding standing = WarDutyPolicy.Decide(Ledger.Facts(today, Live()));
         if (standing != WarDutyStanding.Owes) return standing;

         Kingdom kingdom = SwornKingdom();
         Hero ruler = kingdom?.Leader;
         if (ruler == null || ruler == Hero.MainHero || !Mapping.HeroPresence.IsInTheWorld(ruler)) return WarDutyStanding.NotAVassal;

         Open(ruler, kingdom, today);

         return standing;
      }

      /// <summary>Opens a reckoning for this ruler and realm. Also the cr.testall seam.</summary>
      internal void Open(Hero ruler, Kingdom kingdom, int today)
      {
         int levy = WarLevyPolicy.Amount(new WarLevyFacts {
            ClanTier = Clan.PlayerClan?.Tier ?? 0, FiefCount = PlayerFiefs().Count(), Scale = ModSettings.WarLevyScale
         });
         WarDutyCase c = Ledger.OpenCase(ruler.StringId, levy, today);
         ModLog.Info($"[WAR DUTY] {ruler.Name} summons the player to answer for sitting out the war; levy {levy}, come by day {c.DeadlineDay}.");

         List<string> enemies = FactionHelper.GetEnemyKingdoms(kingdom).Select(k => k.Name?.ToString()).ToList();
         int daysAtWar = Ledger.WarStretchSince.HasValue ? today - Ledger.WarStretchSince.Value : 0;
         string context = WarDutyWording.SummonsContext(new SummonsFacts {
            EnemyNames = enemies, DaysAtWar = daysAtWar, DaysToCome = c.DeadlineDay - today, LevyAmount = levy
         });

         var aim = new StandingAim {
            Id = AimId, SourceMod = AimsBehavior.CrSource, NpcId = ruler.StringId, Channels = AimChannels.Letter | AimChannels.Conversation,
            Want = "Your sworn vassal, the player, has sat out your realm's war. You want them to answer for it in person, and to pay "
                   + "a levy for their absence if they will not ride.",
            ExpiresOnDay = c.DeadlineDay + WarDutyLadder.StageDays + 1
         };
         AimsBehavior aims = AimsBehavior.Instance;
         if (aims != null)
         {
            // The duty outranks another mod's aim on the same lord (Claude's default, 15/10/2026, to review): a liege's
            // reckoning is not set aside for a bridge's ambition. That aim is retracted, and the log says so.
            StandingAim other = aims.Book.ForNpc(ruler.StringId);
            if (other != null && !string.Equals(other.SourceMod, AimsBehavior.CrSource, StringComparison.OrdinalIgnoreCase))
            {
               aims.Retract(other.SourceMod, other.Id);
               ModLog.Info($"[WAR DUTY] {other.SourceMod}/{other.Id} on {ruler.Name} set aside for the war duty.");
            }

            AimAdmission admitted = aims.Register(aim);
            if (admitted == AimAdmission.Accepted || admitted == AimAdmission.Updated)
               aims.MarkRaised(aims.Book.Find(AimsBehavior.CrSource, AimId), AimChannels.Letter);
         }

         SendLetter(ruler, LetterReason.WarSummons, context, today);
      }

      /// <summary>Moves the reckoning a step and carries out what it returns. Also the cr.testall seam.</summary>
      internal WarDutyEffect Step(WarDutyEvent ev, int today, int amount = 0)
      {
         WarDutyCase c = Ledger.Case;
         if (c == null || !c.IsOpen) return WarDutyEffect.None;

         List<FiefCandidate> fiefs = ev == WarDutyEvent.DayPassed ? FiefCandidates() : new List<FiefCandidate>();
         WarDutyEffect effect = WarDutyLadder.Advance(c, new WarDutyInput {
            Event = ev, Today = today, Amount = amount, HoldsATakeableFief = WarDutyConfiscation.HasTakeable(fiefs)
         });
         if (effect == WarDutyEffect.None) return effect;

         Hero ruler = Hero.FindFirst(h => h != null && h.StringId == c.RulerId);
         ModLog.Info($"[WAR DUTY] {ev}: {effect}; now {c.Stage} (levy {c.LevyPaid}/{c.LevyAmount}, deadline day {c.DeadlineDay}).");

         switch (effect)
         {
            case WarDutyEffect.DemandInPerson:
               Remember(ruler, NotableEventType.Confrontation, "The player came before me to answer for sitting out the war. I demanded a levy for their absence.", today);
               break;

            case WarDutyEffect.SendLevyLetter:
               Remember(ruler, NotableEventType.Confrontation, "The player ignored my summons to answer for sitting out the war.", today);
               if (ruler != null)
                  SendLetter(ruler, LetterReason.WarLevyDemand, WarDutyWording.LevyLetterContext(new LevyFacts {
                     Outstanding = c.LevyOutstanding, DaysLeft = c.DeadlineDay - today, HoldsATakeableFief = WarDutyConfiscation.HasTakeable(FiefCandidates())
                  }), today);
               break;

            case WarDutyEffect.Settled:
               Remember(ruler, NotableEventType.Agreement, ev == WarDutyEvent.Served
                  ? "The player rode to war for the realm again, as a vassal owes; the matter of their absence is settled."
                  : $"The player paid the levy of {c.LevyAmount} denars for their absence from the war; the matter is settled.", today);
               Close();
               break;

            case WarDutyEffect.Confiscate:
               Confiscate(ruler, fiefs, today);
               Close();
               break;

            case WarDutyEffect.RememberDefault:
               Remember(ruler, NotableEventType.Confrontation, "The player neither fought for the realm nor paid the levy I demanded. I will not forget it.", today);
               Close();
               break;

            case WarDutyEffect.Drop:
               Close();
               break;
         }

         return effect;
      }

      /// <summary>A conversation opened with this hero. Also the cr.testall seam.</summary>
      internal void OnConversationStarted(string npcId)
      {
         try
         {
            if (!ModSettings.EnableWarDuty) return;
            WarDutyCase c = Ledger.Case;
            if (c is {IsOpen: true} && npcId == c.RulerId) Step(WarDutyEvent.Appeared, Today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WAR DUTY] conversation check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static void Close() => AimsBehavior.Instance?.Retract(AimsBehavior.CrSource, AimId);

      private static void Confiscate(Hero ruler, List<FiefCandidate> fiefs, int today)
      {
         FiefCandidate pick = WarDutyConfiscation.Choose(fiefs);
         Settlement fief = pick == null ? null : Settlement.Find(pick.Id);
         if (ruler == null || fief == null)
         {
            Remember(ruler, NotableEventType.Confrontation, "The player neither fought for the realm nor paid the levy I demanded. I will not forget it.", today);

            return;
         }

         // Released first, or the player's governor keeps governing a fief that is no longer theirs (the revoke path's lesson).
         CourtActionResolver.ReleaseSittingGovernor(fief);
         ChangeOwnerOfSettlementAction.ApplyByKingDecision(ruler, fief);

         Remember(ruler, NotableEventType.Confrontation, $"The player neither fought for the realm nor paid the levy; I took {fief.Name} from them.", today);
         MBInformationManager.AddQuickInformation(new TextObject($"{ruler.Name} has taken {fief.Name} from you for sitting out the war."));
         ModLog.Info($"[WAR DUTY] {ruler.Name} confiscated {fief.Name}.");
      }

      private static IEnumerable<Settlement> PlayerFiefs()
         => Clan.PlayerClan?.Settlements?.Where(s => s != null && (s.IsTown || s.IsCastle)) ?? Enumerable.Empty<Settlement>();

      private static List<FiefCandidate> FiefCandidates()
         => PlayerFiefs().Select(s => new FiefCandidate {
            Id = s.StringId, IsTown = s.IsTown, Prosperity = s.Town?.Prosperity ?? 0f, UnderSiege = s.IsUnderSiege
         }).ToList();

      private static void SendLetter(Hero ruler, LetterReason reason, string context, int today)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.LlmServices == null || behavior.Store == null) return;
         if (DebugSuppressLetters)
         {
            ModLog.Info($"[WAR DUTY] (testall) {reason} from {ruler.Name} not written: {context}");

            return;
         }

         NpcProfile profile = behavior.Store.Get(ruler.StringId) ?? Mapping.HeroProfileMapper.FromHero(ruler);
         LetterGenerationService.QueueInitialLetter(profile, ruler, reason, context, behavior.LlmServices, behavior.Store, today);
      }

      private static void Remember(Hero ruler, NotableEventType type, string line, int day)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (ruler == null || store == null) return;

         NpcProfile profile = store.Get(ruler.StringId) ?? Mapping.HeroProfileMapper.FromHero(ruler);
         profile.Events.Add(new NotableEvent(day, type, line));
         store.Set(profile);
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent == null) return;

            bool attacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            bool defender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
            if (!attacker && !defender) return;

            MapEventSide other = attacker ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            if (ServedAgainst(other?.LeaderParty?.MapFaction?.StringId))
               ModLog.Info($"[WAR DUTY] the player fought the realm's enemy ({other?.LeaderParty?.MapFaction?.Name}); service noted.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WAR DUTY] battle check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Notes service when the player fought a faction his realm is at war with, as the ledger knows its wars.
      ///   True when it counted. The cr.testall seam for a battle.
      /// </summary>
      internal bool ServedAgainst(string enemyFactionId)
      {
         if (string.IsNullOrEmpty(enemyFactionId)) return false;

         // A war declared today is not in the ledger until the daily upkeep: see it now.
         if (!Ledger.WarSeenSince.ContainsKey(enemyFactionId)) Upkeep(Today);
         if (!Ledger.WarSeenSince.ContainsKey(enemyFactionId)) return false;

         Ledger.NoteServed(Today);
         if (ModSettings.EnableWarDuty) Step(WarDutyEvent.Served, Today);

         return true;
      }
   }
}
