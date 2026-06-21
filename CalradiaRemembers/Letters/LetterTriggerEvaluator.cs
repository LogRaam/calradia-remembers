// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Listens to campaign events and evaluates daily conditions to decide when
   ///   an NPC should initiate a letter to the player. Fires
   ///   <see cref="LetterGenerationService.QueueInitialLetter" /> when a trigger
   ///   condition is met, subject to per-NPC cooldowns and in-transit limits.
   /// </summary>
   public sealed class LetterTriggerEvaluator
   {
      // Fixed gold amount requested for child support.
      private const int ChildSupportAmount = 750;

      // Days after birth before the child-support request letter is sent.
      private const int ChildSupportDelayDays = 7;

      // Minimum days between two letters from the same NPC (anti-spam).
      private const int DefaultCooldownDays = 15;

      // We cap how many letters queue up from a single event.
      private const int MaxLettersPerEvent = 2;

      // Cumulative anti-flood: at most ONE unprompted daily-correspondence letter per this many days
      // across ALL NPCs, on top of each NPC's own cooldown. Stops a player with many correspondents
      // from being buried (tester feedback). Non-persisted — a reload may let one extra through.
      private const int GlobalLetterCooldownDays = 4;
      private int _lastDailyLetterDay = -1;

      // ── Daily evaluation (called from OnDailyTick) ────────────────────────

      public void EvaluateDaily(BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
      {
         if (!LetterCadence.CanSendGlobally(_lastDailyLetterDay < 0 ? (int?) null : _lastDailyLetterDay,
                                            today, GlobalLetterCooldownDays))
            return;

         // At most one daily-correspondence letter per tick (the || short-circuits after the first
         // that fires), and only once per global window.
         bool queued = EvaluateRomanticCorrespondence(store, llmServices, today)
                       || EvaluateAwaitingReplies(store, llmServices, today)
                       || EvaluateSpouseCorrespondence(store, llmServices, today)
                       || EvaluateMarriageSolicitations(store, llmServices, today);

         if (queued) _lastDailyLetterDay = today;
      }

      public void Subscribe()
      {
         CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
         // Signature: (CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         // Signature: (Hero mother, List<Hero> aliveChildren, int stillbornCount)
         ModLog.Info("LetterTriggerEvaluator subscribed.");
      }

      #region private

      // Anti-spam cadence + last-meeting proxy now live in the pure, unit-tested
      // CalradiaRemembers.Logic.LetterCadence; these thin wrappers keep the call sites unchanged.

      /// <summary>True when the NPC has no letter in transit and respects the cooldown.</summary>
      private static bool CanSendLetter(NpcProfile profile, int today, int cooldownDays)
         => LetterCadence.CanSendLetter(profile, today, cooldownDays);

      /// <summary>Days since the last notable event in this NPC's profile (proxy for last meeting).</summary>
      private static int DaysSinceLastEvent(NpcProfile profile, int today)
         => LetterCadence.DaysSinceLastEvent(profile, today);

      // ── Daily condition evaluators ─────────────────────────────────────────

      private static bool EvaluateRomanticCorrespondence(
         BannerlordNpcMemoryStore store,
         LlmServices llmServices,
         int today)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.Romantic == null) continue;
            if (profile.Romantic.Status < RomanticStatus.Courting) continue;
            if (profile.ReputationWithPlayer < 15) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero == null || !hero.IsAlive) continue;
            if (IsWithPlayer(hero)) continue; // travelling companion — no letter needed

            if (!CanSendLetter(profile, today, 10)) continue;
            if (DaysSinceLastEvent(profile, today) < 3) continue; // met recently

            string context = $"Romantic status: {profile.Romantic.Status}. " + $"Last saw the player {DaysSinceLastEvent(profile, today)} days ago.";

            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.RomanticCorrespondence, context,
               llmServices, store, today);

            return true; // one romantic letter per daily tick
         }

         return false;
      }

      /// <summary>
      ///   Checks for romantic letters that were delivered but never answered. When the
      ///   player has gone silent for five days after reading an NPC's romantic letter,
      ///   the NPC writes a single follow-up expressing their impatience or longing.
      ///   One follow-up per unanswered letter; bypasses the normal cooldown gate.
      /// </summary>
      private static bool EvaluateAwaitingReplies(
         BannerlordNpcMemoryStore store,
         LlmServices llmServices,
         int today)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.Romantic == null) continue;
            if (profile.Romantic.Status < RomanticStatus.Courting) continue;
            if (profile.ReputationWithPlayer < 15) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero == null || !hero.IsAlive) continue;
            if (IsWithPlayer(hero)) continue;

            // Find a delivered romantic letter with no player reply after 5 days.
            PendingLetter unanswered = null;
            foreach (PendingLetter l in profile.SentLetters)
            {
               if (l.Reason != LetterReason.RomanticCorrespondence) continue;
               if (!l.IsDelivered) continue;
               if (!string.IsNullOrWhiteSpace(l.PlayerReply)) continue;
               if (today - l.DeliveryDay < 5) continue;

               unanswered = l;
               break;
            }

            if (unanswered == null) continue;

            // Primary guard: flag on the original letter survives even when the follow-up
            // placeholder is removed by LLM failure or session-reload orphan cleanup.
            if (unanswered.FollowUpSent) continue;

            // Legacy fallback for saves pre-dating FollowUpSent: if the follow-up letter
            // still exists in SentLetters, backfill the flag and skip.
            bool followUpPresent = false;
            foreach (PendingLetter l in profile.SentLetters)
            {
               if (l.Reason == LetterReason.AwaitingReply && l.SentOnDay > unanswered.SentOnDay)
               {
                  followUpPresent = true;
                  break;
               }
            }

            if (followUpPresent)
            {
               unanswered.FollowUpSent = true;
               store.Set(profile);
               continue;
            }

            int daysSince = today - unanswered.DeliveryDay;
            string context = $"You wrote a letter {daysSince} days ago expressing your feelings. " +
                             $"The player has not replied. Their silence weighs on you.";

            unanswered.FollowUpSent = true;
            store.Set(profile);
            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.AwaitingReply, context,
               llmServices, store, today);

            ModLog.Info($"[LETTER] {profile.Name} sends awaiting-reply follow-up ({daysSince}d of silence).");
            return true; // one follow-up per daily tick
         }

         return false;
      }

      private static bool EvaluateSpouseCorrespondence(
         BannerlordNpcMemoryStore store,
         LlmServices llmServices,
         int today)
      {
         // Native (legal) spouse.
         Hero spouse = Hero.MainHero?.Spouse;
         if (spouse != null && spouse.IsAlive && !IsWithPlayer(spouse))
         {
            NpcProfile profile = store.Get(spouse.StringId);
            if (profile != null && CanSendLetter(profile, today, 7) && DaysSinceLastEvent(profile, today) >= 5)
            {
               string ctx = $"Player's spouse. Has not seen the player in {DaysSinceLastEvent(profile, today)} days.";
               LetterGenerationService.QueueInitialLetter(
                  profile, spouse, LetterReason.SpouseCorrespondence, ctx,
                  llmServices, store, today);
               return true;
            }
         }

         // Consorts — committed partners recognised by the mod but not by the game engine.
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            if (profile?.Romantic?.IsConsort != true) continue;
            if (profile.Romantic.Status != RomanticStatus.Committed) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);
            if (hero == null || !hero.IsAlive) continue;
            if (IsWithPlayer(hero)) continue;
            if (!CanSendLetter(profile, today, 10)) continue;
            if (DaysSinceLastEvent(profile, today) < 7) continue;

            string context = $"Player's consort. Has not seen the player in {DaysSinceLastEvent(profile, today)} days.";
            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.SpouseCorrespondence, context,
               llmServices, store, today);
            return true; // one consort letter per daily tick
         }

         return false;
      }

      /// <summary>
      ///   A clan head who regards the player well may proactively propose an alliance match
      ///   by letter. Fires when: NPC is a lord with eligible kin, player is single, player
      ///   has some standing (clan tier ≥ 1), personal regard ≥ 20, and no proposal was
      ///   already sent. One solicitation per daily tick; cooldown 30 days.
      /// </summary>
      private static bool EvaluateMarriageSolicitations(
         BannerlordNpcMemoryStore store,
         LlmServices llmServices,
         int today)
      {
         if (Hero.MainHero?.Spouse != null) return false;          // player already wed
         if (Clan.PlayerClan?.Tier < 1) return false;              // no standing

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            if (profile == null) continue;
            if (profile.ReputationWithPlayer < 20) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);
            if (hero == null || !hero.IsAlive || !hero.IsLord) continue;
            if (IsWithPlayer(hero)) continue;

            // Build prospects — non-empty means this NPC has authority and eligible kin.
            string prospects = MarriageProspectResolver.Build(hero);
            if (string.IsNullOrWhiteSpace(prospects)) continue;

            if (!CanSendLetter(profile, today, 30)) continue;

            // Only one marriage proposal letter ever (avoid pestering the player).
            bool alreadySent = false;
            foreach (PendingLetter l in profile.SentLetters)
            {
               if (l.Reason == LetterReason.MarriageProposal) { alreadySent = true; break; }
            }

            if (alreadySent) continue;

            string context = $"You are a lord of standing and you regard the player highly. " +
                             $"You are writing to propose a marriage alliance between your house and theirs. " +
                             $"Eligible members of your house: {prospects}";

            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.MarriageProposal, context,
               llmServices, store, today);

            ModLog.Info($"[LETTER] {profile.Name} sends marriage solicitation to player.");
            return true; // one solicitation per daily tick
         }

         return false;
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      /// <summary>
      ///   True when the NPC is currently traveling with the player — in the same
      ///   party or a party led by the player. They would write a letter to someone
      ///   far away, not to someone riding beside them.
      /// </summary>
      private static bool IsWithPlayer(Hero hero)
      {
         if (hero == null) return false;
         MobileParty main = MobileParty.MainParty;

         if (main == null) return false;
         MobileParty heroParty = hero.PartyBelongedTo;

         return heroParty == main || heroParty?.LeaderHero == Hero.MainHero;
      }

      private static void NpcCongratulatePlayerForTournamentVictory(NpcMemoryCampaignBehavior behavior, int today, string context)
      {
         var queued = 0;
         foreach (string id in behavior.Store.ListIds())
         {
            if (queued >= MaxLettersPerEvent) break;

            NpcProfile profile = behavior.Store.Get(id);

            if (profile == null) continue;
            if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true}) continue;
            if (IsWithPlayer(hero)) continue; // travelling companion — no letter needed

            // Friendly NPCs congratulate; rivals or hostiles might challenge.
            if (profile.ReputationWithPlayer < 10) continue;

            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.TournamentVictory, context,
               behavior.LlmServices, behavior.Store, today);
            queued++;
         }
      }

      private static void NpcCongratulatePlayerOnBattleVictory(NpcMemoryCampaignBehavior behavior, int today, string context)
      {
         var queued = 0;
         foreach (string id in behavior.Store.ListIds())
         {
            if (queued >= 1) break; // cap at 1 per battle

            NpcProfile profile = behavior.Store.Get(id);

            if (profile == null) continue;
            if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero == null || !hero.IsAlive) continue;
            if (IsWithPlayer(hero)) continue; // travelling companion — no letter needed

            // Ally of the player (same faction or high personal regard) celebrates.
            bool sameOrAllied = hero.MapFaction != null
                                && hero.MapFaction == Hero.MainHero?.MapFaction;

            if (!sameOrAllied || profile.ReputationWithPlayer < 20) continue;

            LetterGenerationService.QueueInitialLetter(
               profile, hero, LetterReason.BattleVictory, context,
               behavior.LlmServices, behavior.Store, today);
            queued++;
         }
      }

      private static IEnumerable<string> SelectLeaderName(MBReadOnlyList<MapEventParty> loserParties)
      {
         return loserParties.Where(p => p.Party?.LeaderHero?.IsLord == true).Select(p => p.Party.LeaderHero.Name.ToString()).Take(2);
      }

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         if (mother == null || aliveChildren == null || aliveChildren.Count == 0) return;

         // Only fire if the player is the father of at least one surviving child.
         var playerChildren = new List<Hero>();
         foreach (Hero c in aliveChildren)
            if (c.Father == Hero.MainHero)
               playerChildren.Add(c);

         if (playerChildren.Count == 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         NpcProfile profile = behavior?.Store.Get(mother.StringId);

         if (profile == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         bool isLegitimate = mother == Hero.MainHero?.Spouse;

         string childDesc = playerChildren.Count == 1
            ? $"a {(playerChildren[0].IsFemale ? "daughter" : "son")} named {playerChildren[0].Name}"
            : $"{playerChildren.Count} children";
         var context = $"{(isLegitimate ? "Legitimate" : "Illegitimate")} birth. She has given birth to {childDesc} on day {today}.";

         // Birth announcement — sent immediately.
         LetterGenerationService.QueueInitialLetter(
            profile, mother, LetterReason.BirthAnnouncement, context,
            behavior.LlmServices, behavior.Store, today);

         // Child support request — sent a week later; quest opens on delivery.
         string supportContext = context + $" She is now asking the player for {ChildSupportAmount} denars to help raise the child.";
         LetterGenerationService.QueueInitialLetter(
            profile, mother, LetterReason.ChildSupportRequest, supportContext,
            behavior.LlmServices, behavior.Store, today + ChildSupportDelayDays);

         ModLog.Info($"[LETTER] birth letters queued: {mother.Name} → player ({childDesc}, {(isLegitimate ? "legitimate" : "illegitimate")}).");
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (mapEvent is not {IsPlayerMapEvent: true}) return;

         bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
         bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;

         if (!playerAttacker && !playerDefender) return;

         BattleSideEnum playerSide = playerAttacker
            ? BattleSideEnum.Attacker
            : BattleSideEnum.Defender;

         if (mapEvent.WinningSide != playerSide) return;

         BattleSideEnum losingSide = playerAttacker
            ? BattleSideEnum.Defender
            : BattleSideEnum.Attacker;
         MBReadOnlyList<MapEventParty> loserParties = mapEvent.GetMapEventSide(losingSide)?.Parties;

         if (loserParties == null) return;

         // Only notable victories: at least one enemy lord was defeated.
         bool hasDefeatedLord = loserParties.Any(p =>
            p.Party?.LeaderHero is {IsLord: true});

         if (!hasDefeatedLord) return;

         // Skip bandit-only battles — nobody of note would write about clearing thieves.
         bool allBandits = loserParties.All(p => p.Party?.MapFaction?.IsBanditFaction == true);

         if (allBandits) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         string defeatedNames = string.Join(", ", SelectLeaderName(loserParties));
         var context = $"Player won a battle, defeating {defeatedNames}.";

         NpcCongratulatePlayerOnBattleVictory(behavior, today, context);
      }

      // ── Event handlers ────────────────────────────────────────────────────

      private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
      {
         if (winner?.HeroObject != Hero.MainHero) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         int arenaRank = behavior.GameStateBridge != null
            ? Campaign.Current?.TournamentManager?.GetLeaderBoardRank(Hero.MainHero) ?? 0
            : 0;

         var context = $"Player won a tournament at {town?.Name}.";
         if (arenaRank > 0) context += $" They are now ranked #{arenaRank} in the tournament leaderboard.";

         NpcCongratulatePlayerForTournamentVictory(behavior, today, context);
      }

      #endregion
   }
}