// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   Owns the quest lifecycle on the SDK side of the bridge: issuing a quest the LLM
   ///   offered, paying out a quest the LLM acknowledged as done, and the daily checks
   ///   that expire overdue quests (with a consequence) or cancel ones whose premise has
   ///   evaporated (the target faction made peace). The deed-verification half lives in
   ///   <see cref="QuestDeedTracker" />, which stamps a quest satisfied from real events.
   /// </summary>
   public static class QuestService
   {
      /// <summary>
      ///   Personal-regard hit when the player openly abandons a quest — lighter
      ///   than a silent deadline failure, because they were honest about withdrawing.
      /// </summary>
      private const int AbandonPenalty = 3;

      /// <summary>Personal-regard hit when the player lets a quest's deadline lapse.</summary>
      private const int DeadlineFailurePenalty = 5;

      /// <summary>Grudges pillar: an honest, spoken abandonment grudges LESS than a silent deadline lapse.</summary>
      private const int AbandonedQuestGrudgeMagnitude = 25;

      /// <summary>Cap on how many terminated quests we keep per NPC before pruning oldest.</summary>
      private const int MaxRetainedQuests = 10;

      /// <summary>
      ///   Applies a turn's quest outcomes: a newly offered quest (<c>[QUEST]</c>) and/or
      ///   an acknowledged completion (<c>[QUEST_COMPLETE]</c>). Chat lines are emitted
      ///   through <paramref name="notify" /> (text, isPositive).
      /// </summary>
      public static void ApplyResponse(NpcProfile giver,
                                       Hero giverHero,
                                       ParsedResponse response,
                                       int gameDay,
                                       IGameStateBridge bridge,
                                       BannerlordNpcMemoryStore store,
                                       Action<string, bool> notify)
      {
         if (giver == null || response == null) return;

         // The model emitted a [QUEST] block that could not be parsed (unknown type, or truncated).
         // Say so plainly: the spoken offer and the registered state must never silently diverge.
         if (response.QuestBlockMalformed)
         {
            // avalanche24 (08/11/2026): the block itself is logged, so a report shows what the model wrote.
            ModLog.Info($"[QUEST] {giver.Name} emitted a [QUEST] block that could not be parsed; nothing recorded. Block: "
                        + (response.QuestBlockRaw ?? "(an [ACTION] whose task kind could not be read)").Replace("\n", " | "));
            notify?.Invoke("No task was recorded: the offer came through garbled. Ask them to state it plainly again.", false);
            // C5 feedback loop: tell the MODEL next turn too, so it does not keep speaking of a task never recorded.
            LlmFeedbackNotes.Set(giver.Id, "Your last task offer could not be registered (it came through garbled, or named a target I could not place).");
         }

         if (response.QuestGiven != null)
            IssueQuest(giver, giverHero, response.QuestGiven, gameDay, store, notify);

         if (response.QuestCompleted != null)
            CompleteQuest(giver, giverHero, response.QuestCompleted, gameDay, bridge, store, notify);
         else
            // B1 reward net: the model claimed no completion this turn, so pay any reward the giver still OWES
            // for a deed the game has already verified. Without this, a weak model that never emits
            // [QUEST_COMPLETE] leaves the player's "Done" quest paying out never.
            SettleUnclaimedRewards(giver, giverHero, gameDay, bridge, store, notify);

         if (response.QuestAbandoned != null)
            AbandonQuest(giver, response.QuestAbandoned, gameDay, store, notify);
      }

      /// <summary>
      ///   Finds an outstanding letter the player is carrying for <paramref name="recipient" />,
      ///   scanning every giver's quests. Returns the giver profile and the quest, or null.
      /// </summary>
      public static LetterErrand FindOutstandingLetterFor(Hero recipient, BannerlordNpcMemoryStore store)
      {
         if (recipient == null || store == null) return null;

         foreach (string id in store.ListIds())
         {
            NpcProfile giver = store.Get(id);

            if (giver == null) continue;

            InformalQuest letter = giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverLetter
               && q.IsOutstanding
               && string.Equals(q.TargetHeroId, recipient.StringId, StringComparison.Ordinal));

            if (letter != null) return new LetterErrand(giver, letter);
         }

         return null;
      }

      /// <summary>
      ///   An outstanding arrange_match task whose lady <paramref name="authorizer" /> has the say over (her house's
      ///   head, a parent, or she herself), with its giver (the suitor); null when none.
      /// </summary>
      public static LetterErrand FindOutstandingMatchFor(Hero authorizer, BannerlordNpcMemoryStore store)
      {
         if (authorizer == null || store == null) return null;

         foreach (string id in store.ListIds())
         {
            NpcProfile giver = store.Get(id);
            if (giver == null || giver.Id == authorizer.StringId) continue;

            foreach (InformalQuest q in giver.ActiveQuests.Where(q => q.Type == QuestType.ArrangeMatch && q.IsOutstanding))
            {
               Hero bride = Hero.FindFirst(h => h.StringId == q.TargetHeroId);
               if (bride != null && (bride == authorizer || Marriage.MarriageProspectResolver.HasAuthority(authorizer, bride)))
                  return new LetterErrand(giver, q);
            }
         }

         return null;
      }

      /// <summary>An outstanding scout_town task aimed at <paramref name="town" />, with its giver; null when none.</summary>
      public static LetterErrand FindOutstandingScoutFor(TaleWorlds.CampaignSystem.Settlements.Settlement town, BannerlordNpcMemoryStore store)
      {
         string name = town?.Name?.ToString();
         if (name == null || store == null) return null;

         foreach (string id in store.ListIds())
         {
            NpcProfile giver = store.Get(id);
            InformalQuest scout = giver?.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.ScoutTown && q.IsOutstanding
                                                                         && string.Equals(q.TargetSettlement, name, StringComparison.OrdinalIgnoreCase));
            if (scout != null) return new LetterErrand(giver, scout);
         }

         return null;
      }

      /// <summary>
      ///   Stamps a letter quest as delivered the moment the player hands it to the
      ///   recipient in conversation. The reward is collected later, when the player
      ///   reports back to the giver and the giver acknowledges the delivery.
      /// </summary>
      public static void MarkLetterDelivered(LetterErrand errand,
                                             Hero recipient,
                                             int gameDay,
                                             BannerlordNpcMemoryStore store)
      {
         if (errand == null || store == null) return;

         errand.Quest.SatisfiedOnDay = gameDay;
         errand.Quest.Evidence = $"Delivered the message to {recipient?.Name} on day {gameDay}";
         ModLog.Info($"[QUEST] letter delivered: {errand.Giver.Name} → {recipient?.Name} (day {gameDay}).");
         store.Set(errand.Giver);
      }

      /// <summary>
      ///   Closes an item-barter bargain that was settled on the spot (the player handed the
      ///   goods over via the picker and the bridge already executed the favor). Marks the
      ///   quest done, records the memory, mirrors the journal entry as completed, and
      ///   persists. The reward grant itself is NOT re-run here — the bridge already did it.
      /// </summary>
      public static void MarkBargainSettled(NpcProfile giver,
                                            InformalQuest quest,
                                            Hero giverHero,
                                            int gameDay,
                                            BannerlordNpcMemoryStore store)
      {
         if (giver == null || quest == null || store == null) return;

         quest.SatisfiedOnDay = gameDay;
         quest.Evidence = $"Handed over goods of the agreed value on day {gameDay}.";
         quest.Status = QuestStatus.Completed;
         AddEvent(giver, gameDay, NotableEventType.Collaboration,
            $"The player settled our bargain in goods — {Describe(quest)}.");
         QuestJournalBridge.CloseAsCompleted(quest, giverHero);
         Stance.StanceService.OnPromiseKept(giver);
         store.Set(giver);

         ModLog.Info($"[BARGAIN] settled for {giver.Name}: {Describe(quest)} (day {gameDay}).");
      }

      /// <summary>
      ///   Daily pass over every profile: cancel quests whose war premise ended, expire
      ///   overdue ones (with a relation penalty and a remembered slight), and prune the
      ///   oldest terminated quests so the list cannot grow without bound.
      /// </summary>
      public static void RunDailyMaintenance(BannerlordNpcMemoryStore store, int gameDay)
      {
         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null || profile.ActiveQuests.Count == 0) continue;

            Hero giverHero = Hero.FindFirst(h => h.StringId == id);

            // The giver is dead or gone from the world: close their open tasks now (not the player's
            // fault, so no penalty), instead of leaving them stuck in the journal until the next reload
            // (which is all ReconcileOnSessionLaunch did). The HeroKilledEvent path closes them the
            // instant the giver is slain; this daily sweep is the safety net for anything it missed.
            // Gone from the world counts too: the engine reads a disabled hero as alive, and their tasks stayed open.
            if (!Mapping.HeroPresence.IsInTheWorld(giverHero))
            {
               CancelQuestsOfDeadGiver(profile, giverHero, gameDay, store);

               continue;
            }

            var changed = false;

            foreach (InformalQuest quest in profile.ActiveQuests)
            {
               if (quest.Status != QuestStatus.Active) continue;

               if (quest.IsOutstanding && PremiseGone(quest, giverHero))
               {
                  quest.Status = QuestStatus.Cancelled;
                  AddEvent(profile, gameDay, NotableEventType.Other,
                     $"Set aside the task to {Describe(quest)} — circumstances changed.");
                  QuestJournalBridge.CloseAsCancelled(quest, giverHero, "Circumstances changed — this task is no longer needed.");
                  changed = true;

                  continue;
               }

               // v1.34.2 fault/no-fault hardening (audit Pattern 2), reworded v2.5.3 after a player report: a
               // capture/attack/execute/rescue/deliver quest whose TARGET hero died or vanished (or, for a
               // rescue, is still not held by the time the daily sweep sees it) can never be answered through
               // any fault of the player. Cancel it as a clean NEUTRAL VOID (no penalty, no grudge, no fail
               // banner), exactly like a dead giver's quest above, instead of letting the deadline lapse into
               // an unjust breach with a lasting grudge. QuestDeedTracker.OnHeroPrisonerReleased now credits a
               // qualifying rescue BEFORE this sweep ever runs, so this branch only still fires for a release
               // that hook never observed (an old save, a missed event) or one of the ruling's own exclusions.
               // The wording must say what is TRUE for each reason: a rescue's target being free is not proof
               // they vanished, and telling a player who just freed someone that their target is "gone from
               // the world" is exactly the bug being fixed here.
               CalradiaRemembers.Logic.QuestVoidReason voidReason = quest.IsOutstanding
                  ? TargetLostToVoid(quest, profile.Id)
                  : CalradiaRemembers.Logic.QuestVoidReason.None;

               if (voidReason != CalradiaRemembers.Logic.QuestVoidReason.None)
               {
                  quest.Status = QuestStatus.Cancelled;
                  AddEvent(profile, gameDay, NotableEventType.Other,
                     $"Set aside the task to {Describe(quest)}: it can no longer be answered.");
                  QuestJournalBridge.CloseAsCancelled(quest, giverHero, VoidClosureReason(voidReason));
                  ReturnEnvoyAdvance(profile, giverHero, quest, gameDay);
                  changed = true;

                  continue;
               }

               // Player report: a "capture an enemy lord" task keyed on a specific hero (so PremiseGone above,
               // which needs a target FACTION, never saw it) outlived the peace treaty and could not be dropped.
               // Carrying it out would be a fresh act of war against a realm the giver just signed a treaty with,
               // so once peace holds, void it as a clean NEUTRAL cancel, exactly like the other no-fault voids.
               if (quest.IsOutstanding && WarPremiseGoneForTargetHero(quest, giverHero))
               {
                  quest.Status = QuestStatus.Cancelled;
                  AddEvent(profile, gameDay, NotableEventType.Other,
                     $"Set aside the task to {Describe(quest)}: our realms are no longer at war.");
                  QuestJournalBridge.CloseAsCancelled(quest, giverHero, "Our realms have made peace; striking their lord now would break the treaty. This task is set aside.");
                  changed = true;

                  continue;
               }

               if (!quest.IsOutstanding || !quest.DeadlineDay.HasValue || gameDay <= quest.DeadlineDay.Value) continue;

               // fkasad (01/10/2026): the deadline ran out while the man was a fugitive, or after the player beat him
               // and he escaped. The game's own escape mechanic, not the player's broken word: a neutral cancel.
               Hero lapsedTarget = string.IsNullOrEmpty(quest.TargetHeroId) ? null : Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
               if (CalradiaRemembers.Logic.EscapedTargetPolicy.LapsesWithoutFault(quest, lapsedTarget?.IsFugitive == true))
               {
                  quest.Status = QuestStatus.Cancelled;
                  AddEvent(profile, gameDay, NotableEventType.Other,
                     $"Set aside the task to {Describe(quest)}: {quest.TargetHeroName ?? "the man"} slipped out of reach.");
                  QuestJournalBridge.CloseAsCancelled(quest, giverHero, $"{quest.TargetHeroName ?? "The target"} slipped out of reach; no fault of yours. This task is set aside.");
                  ModLog.Info($"[QUEST] lapsed without fault for {profile.Name}: {Describe(quest)} (target fled or was beaten).");
                  changed = true;

                  continue;
               }

               quest.Status = QuestStatus.Expired;

               // No-fault forgiveness (same rule as AbandonQuest): a lapsed deadline on a hard-to-find bandit
               // deed is a mild disappointment, not a broken word, since the target can hide until scouted. Every
               // other quest keeps the full consequence with this path's own DeadlineFailurePenalty.
               CalradiaRemembers.Logic.Quests.QuestFailureLeniency leniency =
                  CalradiaRemembers.Logic.Quests.QuestFailureLeniencyPolicy.Resolve(quest.Type, DeadlineFailurePenalty);

               ProfileMutator.ApplyReputationDelta(profile, -leniency.RegardPenalty, NpcMemoryService.Core.Standing.RegardCause.Quest); // 5.1: single clamp path.

               if (leniency.SpendPromiseTrust)
                  Stance.StanceService.OnPromiseBroken(profile); // a lapsed deadline is a broken word too

               if (leniency.RecordBrokenWordMemory)
                  AddEvent(profile, gameDay, NotableEventType.Conflict,
                     $"The player let the deadline lapse on the task to {Describe(quest)}, a disappointment.");

               QuestJournalBridge.CloseAsFailed(quest, giverHero, $"You did not complete this task in time. {profile.Name} is disappointed.");
               ModLog.Info($"[QUEST] expired for {profile.Name}: {Describe(quest)} (-{leniency.RegardPenalty} regard).");
               // Grudges pillar: adds RESOLVABILITY on top of the regard hit above, never a second penalty.
               // C6: anchor the grudge on the SAME day the backfill uses (DeadlineDay ?? IssuedOnDay), not the
               // detection day, so the holder+source+day dedup matches and a future MigrationVersion bump cannot
               // plant a SECOND FailedQuest grudge for this same lapse. (A future EventDay reads as full magnitude
               // until it passes, per GrudgePolicy.EffectiveMagnitude, so this is safe.) Skipped for the bandit class.
               if (leniency.PlantGrudge)
                  GrudgeBehavior.Instance?.TryPlant(giverHero, Hero.MainHero, GrudgeSource.FailedQuest,
                     $"The player let the deadline lapse on the task to {Describe(quest)}.", GrudgeBackfillRules.FailedQuestMagnitude,
                     knownToPlayer: true, quest.DeadlineDay ?? quest.IssuedOnDay);
               changed = true;
            }

            if (Prune(profile)) changed = true;
            if (changed) store.Set(profile);
         }
      }

      /// <summary>
      ///   Closes every still-open quest of a giver who has died (or been removed from the world):
      ///   both an outstanding task and one whose deed is already done but whose reward can no longer
      ///   be collected because the giver is gone. This is never the player's fault, so there is NO
      ///   relation penalty and no broken-word: the quests are Cancelled and their journal mirrors
      ///   closed with a death reason. Fires mid-session (on the giver's death, and as a daily safety
      ///   net) so a quest no longer lingers in the log until the next reload. Returns true if it
      ///   closed anything.
      /// </summary>
      public static bool CancelQuestsOfDeadGiver(NpcProfile giver, Hero giverHero, int gameDay, BannerlordNpcMemoryStore store)
      {
         if (giver == null || store == null) return false;

         var changed = false;
         foreach (InformalQuest quest in giver.ActiveQuests)
         {
            if (quest.Status != QuestStatus.Active) continue; // Active covers both outstanding and awaiting-reward

            quest.Status = QuestStatus.Cancelled;
            QuestJournalBridge.CloseAsCancelled(quest, giverHero,
               $"{giver.Name} is dead. This task can no longer be answered.");
            changed = true;
            ModLog.Info($"[QUEST] cancelled on giver death: {giver.Name} — {Describe(quest)} (day {gameDay}).");
         }

         if (changed) store.Set(giver);

         return changed;
      }

      /// <summary>
      ///   Nemesis pillar increment 3 (bounty missions): true when SOME giver, anywhere, already holds an
      ///   active (outstanding or awaiting-reward) bounty quest on the nemesis embodied by
      ///   <paramref name="nemesisHeroId" />. A living nemesis may only ever have one bounty on his head at
      ///   a time, checked store-wide (mirrors <see cref="FindOutstandingLetterFor" />'s scan shape) so a
      ///   second lord cannot stack a duplicate bounty on the same foe.
      /// </summary>
      /// <summary>
      ///   Nemesis pillar (audit M2, 2026-07-24): closes every OUTSTANDING bounty on a nemesis who is now dead
      ///   by a hand that was not the player's, or turned, so a bounty the player can no longer complete never
      ///   lingers forever and blocks that lord's one-task slot. Deliberately touches only OUTSTANDING quests:
      ///   a bounty the player already earned (awaiting-reward, stamped by QuestDeedTracker at the capture or
      ///   the player-dealt kill) is his to collect and must NOT be cancelled. Records an honest closure note
      ///   on each giver and closes its journal mirror. Returns how many were cancelled.
      /// </summary>
      public static int CancelOutstandingBountiesOn(string nemesisHeroId, string closureNote, int gameDay, BannerlordNpcMemoryStore store)
      {
         if (string.IsNullOrEmpty(nemesisHeroId) || store == null) return 0;

         var cancelled = 0;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            if (profile == null) continue;

            var changed = false;
            foreach (InformalQuest quest in profile.ActiveQuests)
            {
               if (quest.Type != QuestType.NemesisBounty
                   || !quest.IsOutstanding
                   || !string.Equals(quest.TargetHeroId, nemesisHeroId, StringComparison.Ordinal))
                  continue;

               quest.Status = QuestStatus.Cancelled;
               Hero giverHero = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
               QuestJournalBridge.CloseAsCancelled(quest, giverHero, closureNote);
               AddEvent(profile, gameDay, NotableEventType.Other, closureNote);
               changed = true;
               cancelled++;
               ModLog.Info($"[QUEST] bounty cancelled ({closureNote}): {profile.Name} -> {quest.TargetHeroName} (day {gameDay}).");
            }

            if (changed) store.Set(profile);
         }

         return cancelled;
      }

      public static bool HasActiveBountyFor(string nemesisHeroId, BannerlordNpcMemoryStore store)
      {
         if (string.IsNullOrEmpty(nemesisHeroId) || store == null) return false;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            if (profile.ActiveQuests.Any(q =>
                   q.Type == QuestType.NemesisBounty
                   && q.Status == QuestStatus.Active
                   && string.Equals(q.TargetHeroId, nemesisHeroId, StringComparison.Ordinal)))
               return true;
         }

         return false;
      }

      /// <summary>
      ///   COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case, Kimi review): true when SOME giver, anywhere,
      ///   already holds an active (outstanding or awaiting-reward) grant_fief quest promising this exact
      ///   settlement. Mirrors <see cref="HasActiveBountyFor" />'s own store-wide scan, so two lords can never
      ///   both promise the same fief (or the same lord promise it twice) at once.
      /// </summary>
      public static bool IsFiefAlreadyPromised(string settlementId, BannerlordNpcMemoryStore store)
      {
         if (string.IsNullOrEmpty(settlementId) || store == null) return false;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            if (profile.ActiveQuests.Any(q =>
                   q.Reward == RewardGrant.GrantFief
                   && q.Status == QuestStatus.Active
                   && string.Equals(q.PromisedSettlementId, settlementId, StringComparison.Ordinal)))
               return true;
         }

         return false;
      }

      /// <summary>
      ///   Nemesis pillar increment 3 (bounty missions): issues a scripted bounty on a tracked nemesis,
      ///   never proposed by the LLM (the target and reward are already known, deterministically, before
      ///   the offer is even shown), so this bypasses <see cref="QuestFactory" /> and builds the
      ///   <see cref="InformalQuest" /> directly, the same shape the console negotiation-framework seeds
      ///   use (e.g. <c>cr.deliver_prisoner</c>). Respects the same "one task at a time" rule as an
      ///   ordinary LLM-offered quest. Returns null (and issues nothing) when the giver already has an
      ///   outstanding task: the caller (the dialogue offer, or a console test) should not have shown the
      ///   offer in that case, but this is re-checked here as the final guard. The reward is fixed at
      ///   issuance by the caller (see <see cref="CalradiaRemembers.Logic.Nemesis.NemesisBountyRewardPolicy" />), never renegotiated.
      /// </summary>
      public static InformalQuest IssueNemesisBounty(NpcProfile giver,
                                                      Hero giverHero,
                                                      Hero nemesisHero,
                                                      string nemesisDisplayName,
                                                      int rewardGold,
                                                      int rewardRelation,
                                                      int gameDay,
                                                      BannerlordNpcMemoryStore store)
      {
         if (giver == null || giverHero == null || nemesisHero == null || store == null) return null;

         if (giver.ActiveQuests.Any(q => q.IsOutstanding))
         {
            ModLog.Info($"[QUEST] {giver.Name} offered a nemesis bounty while one task is already outstanding, ignored.");

            return null;
         }

         var quest = new InformalQuest {
            Type = QuestType.NemesisBounty,
            Description = $"Capture or slay {nemesisDisplayName}.",
            TargetHeroId = nemesisHero.StringId,
            TargetHeroName = nemesisHero.Name?.ToString() ?? nemesisDisplayName,
            IssuedOnDay = gameDay,
            Reward = RewardGrant.None,
            RewardGold = rewardGold,
            RewardRelation = rewardRelation,
            Status = QuestStatus.Active
         };

         giver.ActiveQuests.Add(quest);
         AddEvent(giver, gameDay, NotableEventType.Agreement, $"Placed a bounty on {nemesisDisplayName}.");
         store.Set(giver);

         QuestJournalBridge.OpenMirror(quest, giverHero);

         ModLog.Info($"[QUEST] {giver.Name} issued a nemesis bounty on {nemesisDisplayName} " + $"(reward {rewardGold}g/+{rewardRelation}).");

         return quest;
      }

      #region private

      private static void AbandonQuest(NpcProfile giver,
                                       QuestAbandonClaim claim,
                                       int gameDay,
                                       BannerlordNpcMemoryStore store,
                                       Action<string, bool> notify)
      {
         // A block that NAMED a type the parser could not recognise is a hallucination, not a wildcard: ignoring
         // it protects the player from a garbage token abandoning the wrong quest (a false -regard + grudge).
         // A claim with no type at all (TypeUnrecognized false) still legitimately targets the single quest.
         if (claim.TypeUnrecognized)
         {
            ModLog.Info($"[QUEST] {giver.Name} acknowledged an abandonment naming an unrecognized type: ignored (not applied to any quest).");

            return;
         }

         // Only an outstanding (not-yet-done) quest can be abandoned — a finished one is done.
         InformalQuest quest = giver.ActiveQuests.FirstOrDefault(q =>
            q.IsOutstanding && (claim.Type == null || q.Type == claim.Type.Value));

         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} acknowledged an abandonment with no matching outstanding quest — ignored.");

            return;
         }

         quest.Status = QuestStatus.Abandoned;

         // No-fault forgiveness (player report: reporting you could not find an unfindable bandit base turned the
         // giver disgusted and hostile). A bandit deed whose target hides until scouted is a mild disappointment,
         // not a broken word: no grudge, no broken-word memory, no trust spent, only a token regard nick. Every
         // other quest keeps the full broken-word consequence with this path's own AbandonPenalty.
         CalradiaRemembers.Logic.Quests.QuestFailureLeniency leniency =
            CalradiaRemembers.Logic.Quests.QuestFailureLeniencyPolicy.Resolve(quest.Type, AbandonPenalty);

         ProfileMutator.ApplyReputationDelta(giver, -leniency.RegardPenalty, NpcMemoryService.Core.Standing.RegardCause.Quest); // 5.1: single clamp path.
         Hero giverHero = FindGiverHero(giver);
         QuestJournalBridge.CloseAsFailed(quest, giverHero, "You chose not to complete this task.");

         if (leniency.RecordBrokenWordMemory)
            AddEvent(giver, gameDay, NotableEventType.Conflict,
               $"The player gave up the task to {Describe(quest)}, a broken word, however honest.");

         if (leniency.SpendPromiseTrust)
            Stance.StanceService.OnPromiseBroken(giver); // a broken word spends trust

         store.Set(giver);

         // Grudges pillar: adds RESOLVABILITY on top of the regard hit above, never a second penalty.
         // C6: same day anchor as the backfill (DeadlineDay ?? IssuedOnDay), so a later migration does not plant a
         // duplicate of this same abandonment. Skipped entirely for the forgiven bandit class.
         if (leniency.PlantGrudge)
            GrudgeBehavior.Instance?.TryPlant(giverHero, Hero.MainHero, GrudgeSource.FailedQuest,
               $"The player gave up the task to {Describe(quest)}.", AbandonedQuestGrudgeMagnitude,
               knownToPlayer: true, quest.DeadlineDay ?? quest.IssuedOnDay);

         ModLog.Info($"[QUEST] {giver.Name}: player abandoned '{quest.Type}' (-{leniency.RegardPenalty} regard{(leniency.PlantGrudge ? "" : ", no-fault bandit forgiveness")}).");
         notify?.Invoke($"You abandoned {giver.Name}'s task ({Describe(quest)}). -{leniency.RegardPenalty} regard.", false);
      }

      private static void AddEvent(NpcProfile profile, int gameDay, NotableEventType type, string summary)
         => profile.Events.Add(new NotableEvent(gameDay, type, summary));

      private static void CompleteQuest(NpcProfile giver,
                                        Hero giverHero,
                                        QuestCompletionClaim claim,
                                        int gameDay,
                                        IGameStateBridge bridge,
                                        BannerlordNpcMemoryStore store,
                                        Action<string, bool> notify)
      {
         // A named-but-unrecognized type must not pay out the first awaiting-reward quest by accident: ignore it.
         // A claim with no type (TypeUnrecognized false) still targets the single satisfied quest as before.
         if (claim.TypeUnrecognized)
         {
            ModLog.Info($"[QUEST] {giver.Name} claimed completion naming an unrecognized type: ignored (not paid out).");

            return;
         }

         InformalQuest quest = giver.ActiveQuests.FirstOrDefault(q =>
            q.IsAwaitingReward && (claim.Type == null || q.Type == claim.Type.Value));

         // fkasad (01/10/2026): the giver may accept a RECORDED victory over the man he wanted in place of the task,
         // for half the reward (a victory is not a capture). Words alone open nothing: TargetBeatenOnDay is set only by
         // a battle the player won with that lord on the losing side.
         if (quest == null)
         {
            InformalQuest beaten = giver.ActiveQuests.FirstOrDefault(q =>
               CalradiaRemembers.Logic.EscapedTargetPolicy.MaySettleByVictory(q) && (claim.Type == null || q.Type == claim.Type.Value));
            if (beaten != null)
            {
               (int gold, int relationReward) = CalradiaRemembers.Logic.EscapedTargetPolicy.VictoryReward(beaten.RewardGold, beaten.RewardRelation);
               beaten.RewardGold = gold;
               beaten.RewardRelation = relationReward;
               beaten.Reward = RewardGrant.None; // a promised grant (a fief, a favour) was for the man himself, not a victory
               beaten.SatisfiedOnDay = gameDay;
               beaten.Evidence = $"Defeated {beaten.TargetHeroName} in battle on day {beaten.TargetBeatenOnDay}; he escaped. Accepted in place of the task, for half the reward.";
               ModLog.Info($"[QUEST] {giver.Name} accepts the player's victory over {beaten.TargetHeroName} in place of the task (half reward).");
               quest = beaten;
            }
         }

         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} claimed completion with no verified quest to match — ignored.");
            // Pattern 3: the NPC narrated a completion that paid nothing. Tell the PLAYER plainly (no silent
            // purse), and feed the MODEL a correction so next turn it stops treating the task as done. Only an
            // IsAwaitingReward quest (a real game event verified the deed) ever pays, so nothing is granted here.
            notify?.Invoke($"No reward was paid: no task of {giver.Name}'s is verified complete and awaiting one.", false);
            LlmFeedbackNotes.Set(giver.Id, "You spoke of a task as complete, but the game has recorded no finished task of yours awaiting a reward. Do not treat that task as done or paid.");

            return;
         }

         SettleQuest(giver, giverHero, quest, gameDay, bridge, store, notify, null);
      }

      /// <summary>
      ///   Pays a VERIFIED-done quest and closes it: the reward, the completed event / journal, the trust bump,
      ///   the appreciation and grudge-deed channels, and the chat line. Shared by <see cref="CompleteQuest" />
      ///   (the LLM's [QUEST_COMPLETE]) and the reward-safety-net (<see cref="SettleUnclaimedRewards" />). Safe
      ///   to call only on an <c>IsAwaitingReward</c> quest: that state already means a real game event proved
      ///   the deed, so this never pays for unfinished work. An optional <paramref name="netLine" /> overrides
      ///   the chat line for the net path (a plain "they pay what they owe", since the NPC did not narrate it).
      /// </summary>
      private static void SettleQuest(NpcProfile giver,
                                      Hero giverHero,
                                      InformalQuest quest,
                                      int gameDay,
                                      IGameStateBridge bridge,
                                      BannerlordNpcMemoryStore store,
                                      Action<string, bool> notify,
                                      string netLine)
      {
         ActionOutcome reward = bridge?.GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);

         // Negotiation framework: a bargain whose reward is a favor (join the party, etc.)
         // honors it now that the deed is verified. The grant outcome takes the chat line.
         ActionOutcome grant = quest.Reward != RewardGrant.None
            ? bridge?.GrantConditionalReward(giverHero, giver, quest)
            : null;

         quest.Status = QuestStatus.Completed;
         AddEvent(giver, gameDay, NotableEventType.Collaboration, $"The player completed the task to {Describe(quest)}.");
         QuestJournalBridge.CloseAsCompleted(quest, giverHero);
         Stance.StanceService.OnPromiseKept(giver); // a kept promise earns trust
         store.Set(giver);

         // Appreciations pillar: the GIVER remembers the player making good on the task — a channel
         // separate from the trust bump above. Never lets a plant failure break the reward already granted.
         try
         {
            AppreciationBehavior.Instance?.TryPlant(giverHero, Hero.MainHero, AppreciationSource.QuestDoneForThem,
               $"The player did what they promised: {Describe(quest)}.", AppreciationBackfillRules.QuestDoneForThemMagnitude,
               knownToPlayer: true, gameDay);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] quest-done plant failed: {ex.GetType().Name}: {ex.Message}");
         }

         // Grudges pillar (Deed gesture): completing the giver's quest is a verified real favour toward
         // them, a channel separate from the trust bump and the appreciation plant above; never lets a
         // resolution failure break the reward already granted.
         string grudgeTail = GrudgeResolutionService.TryGrudgeDeedTail(giverHero, gameDay);

         if (netLine != null) notify?.Invoke(netLine + grudgeTail, true);
         else if (grant != null) notify?.Invoke(grant.Message + grudgeTail, grant.IsPositive);
         else if (reward != null) notify?.Invoke(reward.Message + grudgeTail, true);
         else notify?.Invoke($"{giver.Name} thanks you for {Describe(quest)}.{grudgeTail}", true);
      }

      /// <summary>
      ///   The reward SAFETY NET (quest audit B1): a giver may hold a quest whose deed the game has already
      ///   verified (<c>IsAwaitingReward</c>) yet the model never emits [QUEST_COMPLETE], so the player has
      ///   "Done" in the journal, returns, and nothing happens. When the player is in conversation with that
      ///   giver and no completion was claimed this turn, settle any still-owed reward here, with a plain system
      ///   line. SAFE because IsAwaitingReward means the work is proven done; this never pays for unfinished work,
      ///   and once paid the quest is Completed so it never fires twice. Generalises the ProvideGold-on-payment fix.
      /// </summary>
      private static void SettleUnclaimedRewards(NpcProfile giver,
                                                 Hero giverHero,
                                                 int gameDay,
                                                 IGameStateBridge bridge,
                                                 BannerlordNpcMemoryStore store,
                                                 Action<string, bool> notify)
      {
         // Snapshot: SettleQuest mutates the quest's status, so iterate a copy.
         var owed = giver.ActiveQuests.Where(q => q.IsAwaitingReward).ToList();

         foreach (InformalQuest quest in owed)
         {
            ModLog.Info($"[QUEST] reward net: {giver.Name} owed for '{quest.Type}' but no [QUEST_COMPLETE] emitted; settling it.");
            SettleQuest(giver, giverHero, quest, gameDay, bridge, store, notify,
               $"{giver.Name} makes good on what they owe you for {Describe(quest)}.");
         }
      }

      /// <summary>What a task is against, as the repeat check reads it (QuestRepeatPolicy.KeyOf).</summary>
      private static string KeyOf(InformalQuest q) => QuestRepeatPolicy.KeyOf(q.TargetFactionId, q.TargetHeroId, q.TargetSettlement);

      /// <summary>
      ///   Every task the game holds for the player, open or carried out, across every giver, for the repeat check and
      ///   the war council (bdionysis80, 10/11/2026). Failed, expired and given-up tasks are left out. Never throws.
      /// </summary>
      internal static List<HeldQuest> HeldQuests(BannerlordNpcMemoryStore store)
      {
         var held = new List<HeldQuest>();
         if (store == null) return held;

         try
         {
            foreach (string id in store.ListIds())
            {
               NpcProfile p = store.Get(id);
               if (p?.ActiveQuests == null) continue;

               foreach (InformalQuest q in p.ActiveQuests)
               {
                  bool done = q.Status == QuestStatus.Completed || q.IsAwaitingReward;
                  if (!q.IsOutstanding && !done) continue;

                  string deed;
                  try { deed = QuestJournalText.Title(CalradiaRemembersQuest.FactsOf(q, null, true)); }
                  catch { deed = null; }

                  held.Add(new HeldQuest {
                     Type = q.Type,
                     TargetKey = KeyOf(q),
                     GiverName = p.Name,
                     Deed = deed,
                     Open = q.IsOutstanding,
                     DoneOnDay = done ? q.SatisfiedOnDay ?? q.IssuedOnDay : (int?) null,
                     Evidence = q.Evidence
                  });
               }
            }
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] reading the tasks held failed: {ex.GetType().Name}: {ex.Message}"); }

         return held;
      }

      /// <summary>One short clause describing the task, for chat lines and memory events.</summary>
      private static string Describe(InformalQuest q)
      {
         if (!string.IsNullOrWhiteSpace(q.Description)) return q.Description.TrimEnd('.');

         switch (q.Type)
         {
            case QuestType.BanditClear: return $"clear bandits near {q.TargetSettlement}";
            case QuestType.BanditHideout: return $"clear the hideout near {q.TargetSettlement}";
            case QuestType.AttackFaction: return $"strike {q.TargetFactionName}";
            case QuestType.AttackLord: return $"defeat {q.TargetHeroName}";
            case QuestType.RaidVillage: return $"raid {q.TargetSettlement}";
            case QuestType.AttackCaravan: return $"destroy a caravan of {q.TargetFactionName}";
            case QuestType.Siege: return $"help take {q.TargetSettlement}";
            case QuestType.CapturePrisoner: return $"capture {q.TargetHeroName}";
            case QuestType.ExecuteEnemy: return $"kill {q.TargetHeroName}";
            case QuestType.NemesisBounty: return $"capture or kill {q.TargetHeroName}";
            case QuestType.RescuePrisoner: return $"free {q.TargetHeroName}";
            case QuestType.DeliverPrisoner:
                return !string.IsNullOrWhiteSpace(q.TargetHeroName)
                    ? $"take {q.TargetHeroName} prisoner and bring them into the giver's hands"
                    : $"bring them a captive lord of {q.TargetFactionName}";
            case QuestType.DeliverLetter: return $"carry a message to {q.TargetHeroName}";
            case QuestType.ProvideGold: return "provide financial support for your child";
            case QuestType.DeliverItems:
                return q.Reward == RewardGrant.JoinParty
                    ? "bring goods in payment for their service"
                    : $"deliver goods worth {q.RequiredItemValue} denars";
            case QuestType.ProvideGoods:
                return $"bring {q.RequiredItemCount} {q.RequiredItemCategory}";
            case QuestType.EarnRegard: return $"win the good graces of {q.TargetHeroName}";
            case QuestType.PleadCase: return $"plead their case before {q.TargetHeroName}";
            case QuestType.ReinforceGarrison: return $"bring {q.RequiredItemCount} soldiers for the garrison of {q.TargetSettlement}";
            case QuestType.ArrangeMatch: return $"win {q.TargetHeroName} for them, and her family's consent";
            case QuestType.CraftWeapon: return $"forge them a {CalradiaRemembers.Logic.CraftWeaponPolicy.Label(CalradiaRemembers.Logic.CraftWeaponPolicy.KindOfKey(q.RequiredItemCategory))}";
            case QuestType.RecruitTroops: return $"bring {q.RequiredItemCount} fresh recruits of {q.RequiredCultureName ?? "their people"}";
            case QuestType.ScoutTown: return $"scout {q.TargetSettlement} from inside, through its gang leader";
            case QuestType.ScoutPlace: return $"ride close to {q.TargetSettlement} and report what it holds";
            case QuestType.RansomEnvoy: return $"buy back {q.TargetHeroName} from their captors, with the {q.AdvanceGold} denars advanced";
            default: return "carry out a task";
         }
      }

      /// <summary>
      ///   The safety wall of an envoy's errand (fkasad, 01/10/2026): the man went free without the player, so the
      ///   advance goes back to the giver, as far as the player's purse holds, and the giver remembers it returned.
      /// </summary>
      private static void ReturnEnvoyAdvance(NpcProfile giver, Hero giverHero, InformalQuest quest, int gameDay)
      {
         if (quest.Type != QuestType.RansomEnvoy || quest.AdvanceGold <= 0 || giverHero == null || Hero.MainHero == null) return;

         int refund = CalradiaRemembers.Logic.RansomEnvoyPolicy.RefundOnVoid(quest.AdvanceGold, Hero.MainHero.Gold);
         if (refund <= 0) return;

         TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, giverHero, refund);
         AddEvent(giver, gameDay, NotableEventType.Other,
            $"{Hero.MainHero.Name} gave back {refund} of the {quest.AdvanceGold} denars I had advanced, {quest.TargetHeroName} having gone free without them.");
         ModLog.Info($"[QUEST] ransom_envoy voided: {refund} of {quest.AdvanceGold} denars returned to {giverHero.Name}.");
      }

      private static Hero FindGiverHero(NpcProfile profile)
         => profile == null
            ? null
            : Hero.FindFirst(h => h.StringId == profile.Id);

      /// <summary>
      ///   Widened from private to <c>internal</c> for the ASSEMBLY pillar's council lift (ChatViewModel's
      ///   <c>FinalizeCouncilResolutions</c>): a resolution's "quest" kind is grounded and issued through this
      ///   exact same path a conversational [QUEST] block already uses, giver/one-at-a-time rule included,
      ///   rather than a second, parallel issuance path.
      /// </summary>
      internal static void IssueQuest(NpcProfile giver,
                                     Hero giverHero,
                                     QuestProposal proposal,
                                     int gameDay,
                                     BannerlordNpcMemoryStore store,
                                     Action<string, bool> notify)
      {
         // One task at a time: never stack a new quest on an unfinished one. Say so (the NPC just SPOKE
         // the offer, so silence here reads as a broken promise).
         if (giver.ActiveQuests.Any(q => q.IsOutstanding))
         {
            ModLog.Info($"[QUEST] {giver.Name} offered a task while one is outstanding — ignored.");
            notify?.Invoke($"No new task was recorded: you already carry one for {giver.Name}. Finish or renounce it first.", false);

            return;
         }

         InformalQuest quest = QuestFactory.Create(proposal, giverHero, gameDay);
         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} offered a task that could not be grounded (type {proposal.Type}) — ignored.");
            notify?.Invoke("No task was recorded: what they offered could not be matched to the world. Ask them to restate it.", false);

            return;
         }

         // The same deed against the same target is not set twice, by anyone, while one is open or within a week of one
         // done (bdionysis80, Nexus 10/11/2026: "Scout an army of Sturgia" set by Athina, done, then by Vamina).
         HeldQuest repeat = QuestRepeatPolicy.RepeatOf(quest.Type, KeyOf(quest), gameDay, HeldQuests(store));
         if (repeat != null)
         {
            string deed = repeat.Deed ?? Describe(quest);
            string state = repeat.Open ? "still open" : $"done on day {repeat.DoneOnDay}";
            ModLog.Info($"[QUEST] {giver.Name} offered '{deed}', a repeat of {repeat.GiverName}'s ({state}), ignored.");
            notify?.Invoke($"No task was recorded: you already carry this task for {repeat.GiverName} ({deed}, {state}).", false);

            return;
         }

         // COUNCIL_ACTIONS.md Partie 5 (Kimi review): the same fief can never be promised twice at once, by
         // one lord or two. QuestFactory validates OWNERSHIP/authority but has no store to scan for a rival
         // promise, so the uniqueness check lives here, the one caller that has both the new quest and the
         // store in hand.
         if (quest.Reward == RewardGrant.GrantFief && IsFiefAlreadyPromised(quest.PromisedSettlementId, store))
         {
            ModLog.Info($"[QUEST] {giver.Name} offered {quest.PromisedSettlementName} as a reward, but it is already promised elsewhere, ignored.");
            notify?.Invoke($"No task was recorded: {quest.PromisedSettlementName} is already promised elsewhere.", false);

            return;
         }

         giver.ActiveQuests.Add(quest);
         AddEvent(giver, gameDay, NotableEventType.Agreement, $"Asked the player to {Describe(quest)}.");
         store.Set(giver);

         // An envoy's purse is in the player's hand from the start (fkasad, Gabriel's option A, 01/10/2026).
         if (quest.Type == QuestType.RansomEnvoy && quest.AdvanceGold > 0 && giverHero != null && Hero.MainHero != null)
         {
            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(giverHero, Hero.MainHero, quest.AdvanceGold);
            notify?.Invoke($"{giver.Name} hands you {quest.AdvanceGold} denars to buy back {quest.TargetHeroName}. What you do not spend is yours.", true);
         }

         // Open a vanilla quest journal mirror — StartQuest() fires its own
         // native notification, so we don't add a second one here.
         QuestJournalBridge.OpenMirror(quest, giverHero);

         ModLog.Info($"[QUEST] {giver.Name} issued: {Describe(quest)} " + $"(reward {quest.RewardGold}g/+{quest.RewardRelation}, deadline {quest.DeadlineDay?.ToString() ?? "none"}).");
      }

      /// <summary>
      ///   True when a war-bound quest's premise has ended — the giver's faction is no
      ///   longer at war with the target faction, so striking it serves no purpose.
      /// </summary>
      private static bool PremiseGone(InformalQuest quest, Hero giverHero)
      {
         if (string.IsNullOrEmpty(quest.TargetFactionId)) return false;
         if (giverHero?.MapFaction == null) return false;

         IFaction target = ResolveFactionById(quest.TargetFactionId);

         if (target == null) return false;

         return !FactionManager.IsAtWarAgainstFaction(giverHero.MapFaction, target);
      }

      /// <summary>
      ///   True when a war-hostile hero quest (capture/attack/execute a specific enemy lord) has lost its premise
      ///   because the giver's realm made PEACE with the target hero's realm, so carrying it out would break the
      ///   fresh treaty (player report). The pure choice lives in
      ///   <see cref="CalradiaRemembers.Logic.QuestTargetVoidPolicy.ShouldVoidOnPeace" />; this only resolves the
      ///   live faction war state. A target that has LEFT the world (dead/vanished/factionless) is not our case:
      ///   <see cref="TargetLostToVoid" /> owns that, and we bail so the two rules never double-handle a quest.
      /// </summary>
      private static bool WarPremiseGoneForTargetHero(InformalQuest quest, Hero giverHero)
      {
         if (string.IsNullOrEmpty(quest.TargetHeroId)) return false;
         if (giverHero?.MapFaction == null) return false;

         Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
         if (target?.MapFaction == null) return false;

         bool giverAtWar = FactionManager.IsAtWarAgainstFaction(giverHero.MapFaction, target.MapFaction);

         return CalradiaRemembers.Logic.QuestTargetVoidPolicy.ShouldVoidOnPeace(
            quest.Type, hasTargetHero: true, giverAtWarWithTargetFaction: giverAtWar);
      }

      /// <summary>
      ///   Console recovery (cr.quest_void): cancels ONE outstanding quest as a clean NEUTRAL void, the exact
      ///   shape the daily sweep uses for a no-fault cancel (Cancelled status, journal closed, a neutral memory
      ///   line), with NO relation penalty, NO grudge, and NO broken-word. It is for unsticking a quest that has
      ///   become impossible or bugged, where cr.quest_abandon's broken-word penalty would be unjust. Returns a
      ///   short report line for the console.
      /// </summary>
      public static string VoidQuestNeutral(NpcProfile profile, Hero giverHero, InformalQuest quest, int gameDay, BannerlordNpcMemoryStore store)
      {
         if (profile == null || quest == null) return "No quest to void.";

         quest.Status = QuestStatus.Cancelled;
         AddEvent(profile, gameDay, NotableEventType.Other, $"Set aside the task to {Describe(quest)}.");
         QuestJournalBridge.CloseAsCancelled(quest, giverHero, "This task has been set aside.");
         store?.Set(profile);

         ModLog.Info($"[QUEST] neutral void (cr.quest_void): {Describe(quest)} from {giverHero?.Name?.ToString() ?? profile.Name}.");

         return $"Voided: {Describe(quest)} (from {giverHero?.Name?.ToString() ?? profile.Name}). No penalty applied.";
      }

      /// <summary>
      ///   v1.34.2 fault/no-fault hardening (audit Pattern 2): why a hero-bound quest's TARGET has left the
      ///   board (died, vanished, or, for a rescue, is no longer a prisoner), or <c>None</c> when it stands.
      ///   The pure rule lives in <see cref="CalradiaRemembers.Logic.QuestTargetVoidPolicy" />; this only
      ///   resolves the live hero state. Returns the REASON (not a plain bool) since v2.5.3, so the caller can
      ///   give the player an honest closure line instead of one wording for every case.
      /// </summary>
      private static CalradiaRemembers.Logic.QuestVoidReason TargetLostToVoid(InformalQuest quest, string giverId)
      {
         if (string.IsNullOrEmpty(quest.TargetHeroId)) return CalradiaRemembers.Logic.QuestVoidReason.None;

         Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);

         // A match: either of the two wed to someone else (fkasad's matchmaker, 01/10/2026).
         bool wedToAnother = false;
         if (quest.Type == QuestType.ArrangeMatch && target != null)
         {
            Hero suitor = Hero.FindFirst(h => h.StringId == giverId);
            wedToAnother = target.Spouse != null && target.Spouse != suitor || suitor?.Spouse != null && suitor.Spouse != target;
         }

         return CalradiaRemembers.Logic.QuestTargetVoidPolicy.ReasonToVoid(new CalradiaRemembers.Logic.QuestTargetVoidPolicy.QuestTargetFacts {Type = quest.Type, HasTargetHero = true, TargetResolved = target != null, TargetAlive = target is {IsAlive: true}, TargetIsPrisoner = target is {IsPrisoner: true}, WedToAnother = wedToAnother});
      }

      /// <summary>
      ///   The closure line the player reads when <see cref="TargetLostToVoid" /> voids a quest. v2.5.3 player
      ///   report ("his success cancelled his quest"): a rescue's target being free is not proof they left the
      ///   world, it may be the player's OWN success reaching this safety net late (an old save, or a release
      ///   this build's hook never observed), so that one reason gets its own honest wording, never the old
      ///   blanket "gone from the world" line, which read as the target having vanished the moment the player
      ///   had personally freed them.
      /// </summary>
      private static string VoidClosureReason(CalradiaRemembers.Logic.QuestVoidReason reason)
      {
         if (reason == CalradiaRemembers.Logic.QuestVoidReason.TargetWedElsewhere)
            return "One of the two has wed another; the match can no longer be made. This task is set aside.";

         return reason == CalradiaRemembers.Logic.QuestVoidReason.RescueTargetFreed
            ? "The one you were to free is already out of captivity, however that came to pass. This task is set aside."
            : "The mark of this task is gone from the world. It can no longer be answered.";
      }

      private static bool Prune(NpcProfile profile)
      {
         if (profile.ActiveQuests.Count <= MaxRetainedQuests) return false;

         // Drop the oldest terminated quests first; never drop a live one.
         var terminated = new List<InformalQuest>();
         foreach (InformalQuest quest in profile.ActiveQuests.Where(q => q.Status != QuestStatus.Active)
                                                .OrderBy(q => q.IssuedOnDay)) terminated.Add(quest);

         var removed = false;
         foreach (InformalQuest q in terminated)
         {
            if (profile.ActiveQuests.Count <= MaxRetainedQuests) break;
            profile.ActiveQuests.Remove(q);
            removed = true;
         }

         return removed;
      }

      private static IFaction ResolveFactionById(string id)
      {
         if (string.IsNullOrEmpty(id)) return null;
         IFaction k = Kingdom.All?.FirstOrDefault(x => x.StringId == id);

         if (k != null) return k;

         return Clan.All?.FirstOrDefault(x => x.StringId == id);
      }

      #endregion
   }

   /// <summary>Pairs a letter quest with the giver who issued it, for the delivery flow.</summary>
   public sealed class LetterErrand
   {
      public LetterErrand(NpcProfile giver, InformalQuest quest)
      {
         Giver = giver;
         Quest = quest;
      }

      public NpcProfile Giver { get; }
      public InformalQuest Quest { get; }
   }
}