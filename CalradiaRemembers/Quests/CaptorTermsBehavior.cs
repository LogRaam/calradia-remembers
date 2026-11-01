// Code written by Gabriel Mailhot, 01/11/2026.
// Gabriel, 26/10/2026: "il faudra d'autres scenarios que de la maltraitance"; 01/11/2026: "go". The real side of a captor's
// terms: what he may be taught this turn (the price the game names, the prisoner he would take in exchange), what each term
// does when the captor grants it, and the player's oath of parole, held to by what they do in the world, as a journal entry.
// The prompt proposes; here the gold moves and the word binds.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>A captor's terms made real, and the player's oath held to.</summary>
   public sealed class CaptorTermsBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_captor_parole";

      /// <summary>What an oath kept to its end earns in the captor's regard.</summary>
      private const int GainWhenKept = 3;

      /// <summary>What an oath broken costs in the captor's regard: more than a lapsed task (5), for a word that bought freedom.</summary>
      private const int LossWhenBroken = 10;

      /// <summary>The grudge an oath broken leaves, a vendetta's near neighbour.</summary>
      private const int OathBrokenGrudge = 35;

      /// <summary>cr.test all seam: a broken oath leaves no grudge, so a test leaves the world as it found it.</summary>
      internal static bool DebugSuppressGrudge;

      public CaptorTermsBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static CaptorTermsBehavior Instance { get; private set; }

      /// <summary>The oaths in force (never null).</summary>
      public CaptorParoleLedger Ledger { get; internal set; } = new CaptorParoleLedger();

      private static int Today => (int) CampaignTime.Now.ToDays;

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
               ModLog.Error($"[PAROLE] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[PAROLE] save failed: {ex.GetType().Name}: {ex.Message}");
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

            Ledger = CaptorParoleLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      #region what the captor may be taught

      /// <summary>The ransom the game itself would ask of a captive player, or 0 when the player does not have it on them.</summary>
      internal static int RansomThePlayerCanPay()
      {
         int price = Campaign.Current?.PlayerCaptivity?.CurrentRansomAmount ?? 0;

         return price > 0 && (Hero.MainHero?.Gold ?? 0) >= price ? price : 0;
      }

      /// <summary>The captive of the captor's house or realm the player holds, the one he would take in exchange, or null.</summary>
      internal static Hero ExchangeableCaptive(Hero captor)
      {
         if (captor == null) return null;

         return BuyPrisonerVerb.PlayerHeldHeroPrisoners().FirstOrDefault(h => h.Clan == captor.Clan)
                ?? BuyPrisonerVerb.PlayerHeldHeroPrisoners().FirstOrDefault(h => h.MapFaction != null && h.MapFaction == captor.MapFaction);
      }

      #endregion

      #region what a term does

      /// <summary>The captor agreed the price: the gold changes hands now, and the player goes free as the scene closes.</summary>
      internal static bool GrantRansom(Hero captor)
      {
         int price = RansomThePlayerCanPay();
         if (price <= 0 || !Release(captor)) return false;

         ExchangeService.Pay(ExchangeKind.Ransom, Hero.MainHero, captor, price);
         ModLog.Info($"[RELEASE] {captor.Name} took {price} denars for the player's freedom.");

         return true;
      }

      /// <summary>The captor took the player's word: they go free as the scene closes, and the word now binds.</summary>
      internal static bool GrantParole(Hero captor)
      {
         IFaction realm = captor?.MapFaction;
         if (realm == null || Instance == null || !Release(captor)) return false;

         CaptorParole oath = Instance.Ledger.Add(captor.StringId, realm.StringId, realm.Name?.ToString(), Today);
         OpenJournal(oath, captor);
         ModLog.Info($"[PAROLE] the player swore to {captor.Name} not to attack {oath.FactionName} until day {oath.UntilDay}.");

         return true;
      }

      /// <summary>The captor took his man back for the player: the captive goes home now, and the player as the scene closes.</summary>
      internal static bool GrantExchange(Hero captor)
      {
         Hero captive = ExchangeableCaptive(captor);
         if (captive == null || !Release(captor)) return false;

         EndCaptivityAction.ApplyByReleasedByChoice(captive, captor);
         ModLog.Info($"[RELEASE] {captor.Name} took {captive.Name} back in exchange for the player.");

         return true;
      }

      // A term frees a prisoner: with none, nothing is paid, sworn or handed back, whatever the model emitted.
      private static bool Release(Hero captor)
         => Hero.MainHero?.IsPrisoner == true && (NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.CommitCaptorRelease(captor) ?? false);

      #endregion

      #region the oath

      private void OnDailyTick()
      {
         try
         {
            int today = Today;
            foreach (CaptorParole oath in Ledger.Oaths.Where(o => today > o.UntilDay).ToList())
               Close(oath, broken: false, today);
         }
         catch (Exception ex) { ModLog.Error($"[PAROLE] daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent is not {IsPlayerMapEvent: true} || Ledger.Oaths.Count == 0) return;

            bool attacked = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            string enemy = mapEvent.DefenderSide?.MapFaction?.StringId;
            int today = Today;
            foreach (CaptorParole oath in Ledger.Oaths.ToList())
               if (CaptorAudiencePolicy.BreaksParole(new ParoleFightFacts {PlayerAttacked = attacked, EnemyFactionId = enemy, SwornFactionId = oath.FactionId, Today = today, UntilDay = oath.UntilDay}))
                  Close(oath, broken: true, today);
         }
         catch (Exception ex) { ModLog.Error($"[PAROLE] map event failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Ends an oath, kept or broken, and leaves its mark on the one who took it. Internal for cr.test all.</summary>
      internal void Close(CaptorParole oath, bool broken, int today)
      {
         Ledger.Oaths.Remove(oath);

         Hero captor = Hero.FindFirst(h => h.StringId == oath.CaptorId);
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(oath.CaptorId);
         if (profile == null) return;

         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.ExternalId == oath.Id && q.Status == QuestStatus.Active);
         if (broken)
         {
            ProfileMutator.ApplyReputationDelta(profile, -LossWhenBroken, RegardCause.Commitment);
            StanceService.OnPromiseBroken(profile);
            profile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal, $"I freed the player on their oath not to attack {oath.FactionName}, and they broke it."));
            if (captor != null && !DebugSuppressGrudge)
               GrudgeBehavior.Instance?.TryPlant(captor, Hero.MainHero, GrudgeSource.BrokenOath,
                  $"You broke the oath that bought your freedom, not to attack {oath.FactionName}.", OathBrokenGrudge, knownToPlayer: true, today);
            if (quest != null)
            {
               quest.Status = QuestStatus.Expired;
               QuestJournalBridge.CloseAsFailed(quest, captor, $"You broke your oath to {profile.Name}.");
            }
         }
         else
         {
            ProfileMutator.ApplyReputationDelta(profile, GainWhenKept, RegardCause.Commitment);
            profile.Events.Add(new NotableEvent(today, NotableEventType.Agreement, $"The player kept the oath I freed them on, not to attack {oath.FactionName}."));
            if (quest != null)
            {
               quest.Status = QuestStatus.Completed;
               QuestJournalBridge.CloseAsCompleted(quest, captor);
            }
         }

         behavior.Store.Set(profile);
         ModLog.Info($"[PAROLE] {oath.Id} closed: {(broken ? "broken" : "kept")}.");
      }

      private static void OpenJournal(CaptorParole oath, Hero captor)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(captor.StringId);
         if (profile == null) return;

         // No deadline on the quest: keeping an oath is reaching its end unbroken, which a lapse would read as a failure.
         var quest = new InformalQuest {
            Type = QuestType.External,
            Description = $"You swore to {captor.Name} not to attack {oath.FactionName} for {CaptorAudiencePolicy.ParoleDays} days. Defending yourself breaks nothing.",
            IssuedOnDay = Today,
            ExternalId = oath.Id,
            ExternalSourceMod = "Calradia Remembers",
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, captor);
      }

      #endregion
   }
}
