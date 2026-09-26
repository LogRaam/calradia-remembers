// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2b: the PLAYER's own unhappy spouse DEMANDS a divorce, entirely through conversation (no
// UI button, owner ruling). This behavior owns the persisted "who is demanding, since when" flag and the
// weekly evaluation that raises a fresh demand (SpouseDivorceDemandPolicy); the conversational resolution
// (accept_divorce / decline_divorce) is executed by BannerlordGameStateBridge, and the actual dissolution
// by MarriageDissolutionService.TryDissolvePlayerMarriage. REFUSAL is registered two ways: an explicit
// decline_divorce action (the player firmly refuses in the moment), or, since a refusal need not come as an
// action at all, a demand left unanswered beyond a grace period is read as a refusal at the next weekly
// sweep — the primary, most robust path, since it never depends on the LLM choosing to emit a negative
// action.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Owns the persisted state for the player's spouse's divorce demand: which spouse is demanding and
   ///   since which day (absent key = no demand pending — save-safe by construction, an older save simply
   ///   has no entries here). Evaluates weekly whether the player's CURRENT spouse should raise a fresh
   ///   demand, and sweeps pending demands for the grace-period refusal timeout.
   /// </summary>
   public sealed class SpouseDivorceDemandBehavior : CampaignBehaviorBase
   {
      /// <summary>
      ///   Tuning: how many in-game days a pending demand may go unanswered before the spouse's silence
      ///   itself counts as a refusal (see the class remarks) — comfortably longer than one weekly
      ///   evaluation, so the player has real room to bring it up themselves first.
      /// </summary>
      private const int GracePeriodDays = 12;

      /// <summary>Tuning: how far back a jealousy wound or a discovered bastard scandal still counts as "fresh".</summary>
      private const int RecentWoundWindowDays = 21;

      /// <summary>Tuning: the grudge magnitude planted on the spouse when their divorce demand is refused.</summary>
      private const int RefusalGrudgeMagnitude = 40;

      /// <summary>Small vanilla-relation cost applied on each refusal (romance audit M-D4), so a grudge-triggered demand loop converges toward an escalation that resolves rather than spamming refusals forever.</summary>
      private const int RefusalRelationHit = 5;

      /// <summary>
      ///   Tuning: the grudge magnitude planted on the spouse when they actually WALK OUT at the end of an
      ///   estrangement (see <see cref="ResolveWalkOut" />) — a touch heavier than a single refusal's
      ///   <see cref="RefusalGrudgeMagnitude" />, since this is the culmination of several, but still one
      ///   single grudge, never the tiered family/friends storm a player-initiated repudiation earns.
      /// </summary>
      private const int EstrangementWalkOutGrudgeMagnitude = 45;

      /// <summary>Moderate relation hit the spouse's regard takes on walking out (romance audit M-D1), applied as a SET from the pre-dissolution baseline so it neutralises the engine's hidden penalty rather than stacking on it. Moderate on purpose: the spouse chose to leave, not the tiered storm a player-initiated repudiation earns.</summary>
      private const int WalkOutRelationHit = -20;

      private const string SaveKey = "CalradiaRemembers_SpouseDivorceDemand_v1";

      /// <summary>Additive save key for the escalation's per-spouse refusal count (old saves load with none).</summary>
      private const string RefusalCountSaveKey = "CalradiaRemembers_SpouseDivorceRefusalCount_v1";

      /// <summary>Additive save key for the escalation's per-spouse deadline day (old saves load with none).</summary>
      private const string EscalationSaveKey = "CalradiaRemembers_SpouseDivorceEscalation_v1";

      /// <summary>Additive save key for the announce phase's per-spouse cycle-begin day (romance audit C1/C2; old saves load with none).</summary>
      private const string AnnounceCycleSaveKey = "CalradiaRemembers_SpouseDivorceAnnounceCycle_v1";

      /// <summary>Additive save key for the announce phase's per-spouse re-announce count (romance audit C1; old saves load with none).</summary>
      private const string AnnounceRetriesSaveKey = "CalradiaRemembers_SpouseDivorceAnnounceRetries_v1";

      public static SpouseDivorceDemandBehavior Instance;

      // spouseId -> the day their demand began.
      private Dictionary<string, int> _demandStartDay = new();

      // spouseId -> how many times their demand has been refused (explicit decline, or grace-period timeout).
      private Dictionary<string, int> _refusalCount = new();

      // spouseId -> the day their active estrangement's deadline falls (absent key = not currently estranging).
      private Dictionary<string, int> _escalationDeadlineDay = new();

      // Announce phase (romance audit C1/C2): spouseId -> the day the CURRENT announcing letter was queued. Bounds
      // the delivery window and lets DemandLetterDelivered ignore a stale letter from a previous demand cycle.
      private Dictionary<string, int> _demandCycleBeginDay = new();

      // Announce phase (romance audit C1): spouseId -> how many times the announcing letter has been re-queued
      // after a lapsed window, so a persistently-undeliverable letter escapes to a forced notice instead of looping.
      private Dictionary<string, int> _announceRetries = new();

      public SpouseDivorceDemandBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>The demand is accepted (accept_divorce executed successfully): clears the flag and the refusal count.</summary>
      public void Accept(string spouseId)
      {
         if (!string.IsNullOrEmpty(spouseId))
         {
            ClearDemandCycle(spouseId);
            _refusalCount.Remove(spouseId);
         }
      }

      /// <summary>
      ///   Clears every per-demand-cycle entry for a spouse (romance audit C1/C2): the refusal-clock/sentinel
      ///   day AND the announce-phase state (cycle-begin day, re-announce count). Used wherever a demand ends or
      ///   transitions, so the announce dictionaries never outlive the demand they describe. The refusal COUNT is
      ///   deliberately NOT touched here: it survives across the demand-to-escalation transition and is reset only
      ///   by an explicit resolution (<see cref="Accept" />, <see cref="CancelEstrangement" />).
      /// </summary>
      private void ClearDemandCycle(string spouseId)
      {
         _demandStartDay.Remove(spouseId);
         _demandCycleBeginDay.Remove(spouseId);
         _announceRetries.Remove(spouseId);
      }

      /// <summary>
      ///   Clears <paramref name="spouseId" />'s active estrangement (reconciled, accepted, or otherwise
      ///   resolved) WITHOUT dissolving the marriage, and resets their refusal count along with it — a
      ///   resolved estrangement, whichever way it resolved, earns a fresh start rather than a demand
      ///   primed to reopen on the very next slight. A no-op when they are not currently estranging.
      /// </summary>
      public void CancelEstrangement(string spouseId)
      {
         if (string.IsNullOrEmpty(spouseId)) return;

         _escalationDeadlineDay.Remove(spouseId);
         _refusalCount.Remove(spouseId);
      }

      /// <summary>The day <paramref name="spouseId" />'s demand began, or null when they are not currently demanding.</summary>
      public int? DemandStartDay(string spouseId)
         => !string.IsNullOrEmpty(spouseId) && _demandStartDay.TryGetValue(spouseId, out int day)
            ? day
            : (int?) null;

      /// <summary>
      ///   Test/console support: moves <paramref name="spouseId" />'s active estrangement deadline to
      ///   <paramref name="day" /> (e.g. today, so the next sweep triggers the walk-out immediately without
      ///   waiting out the real estrangement length). A no-op when they are not currently estranging.
      /// </summary>
      public void DebugForceEscalationDeadline(string spouseId, int day)
      {
         if (!string.IsNullOrEmpty(spouseId) && _escalationDeadlineDay.ContainsKey(spouseId))
            _escalationDeadlineDay[spouseId] = day;
      }

      /// <summary>
      ///   The day <paramref name="spouseId" />'s active estrangement's deadline falls, or null when they
      ///   are not currently estranging.
      /// </summary>
      public int? EscalationDeadlineDay(string spouseId)
         => !string.IsNullOrEmpty(spouseId) && _escalationDeadlineDay.TryGetValue(spouseId, out int day)
            ? day
            : (int?) null;

      /// <summary>
      ///   Forces the player's CURRENT spouse into the estrangement state right now (console / self-test),
      ///   bypassing the refusal-count gate entirely. Fails when the player is unmarried or the spouse is
      ///   already estranging.
      /// </summary>
      public (bool Success, string Message) ForceEscalate()
      {
         Hero spouse = Hero.MainHero?.Spouse;

         if (spouse is not {IsAlive: true}) return (false, "You are not currently married.");
         if (IsEstranging(spouse.StringId)) return (false, $"{spouse.Name} has already begun ending the marriage.");

         BeginEstrangement(spouse, Today);

         return (true, $"{spouse.Name} now begins ending the marriage themselves. Deadline day {EscalationDeadlineDay(spouse.StringId)}.");
      }

      /// <summary>
      ///   Forces the player's CURRENT spouse into the demanding state right now (console / self-test).
      ///   Fails when the player is unmarried or a demand is already pending.
      /// </summary>
      public (bool Success, string Message) ForceDemand()
      {
         Hero spouse = Hero.MainHero?.Spouse;

         if (spouse is not {IsAlive: true}) return (false, "You are not currently married.");
         if (IsDemanding(spouse.StringId)) return (false, $"{spouse.Name} is already pressing you to end the marriage.");

         Begin(spouse, Today);

         return (true, $"{spouse.Name} now presses you to end the marriage. Speak with them to resolve it.");
      }

      /// <summary>True when <paramref name="spouseId" /> currently has a pending demand.</summary>
      public bool IsDemanding(string spouseId)
         => !string.IsNullOrEmpty(spouseId) && _demandStartDay.ContainsKey(spouseId);

      /// <summary>True when <paramref name="spouseId" />'s estrangement is currently active (deadline pending).</summary>
      public bool IsEstranging(string spouseId)
         => !string.IsNullOrEmpty(spouseId) && _escalationDeadlineDay.ContainsKey(spouseId);

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>
      ///   Registers a refusal: the spouse's resentment deepens (a <see cref="GrudgeSource.Divorce" />
      ///   grudge, known to the player since it was voiced or felt in the moment), the demand flag is
      ///   cleared so it can resurface fresh at a later evaluation rather than sitting stuck forever, and
      ///   the running refusal count grows. When that count and the marriage's continuing misery clear
      ///   <see cref="SpouseDivorceDemandPolicy.ShouldEscalate" />, the spouse stops merely demanding and
      ///   BEGINS an estrangement of their own (<see cref="BeginEstrangement" />) instead of quietly waiting
      ///   to be asked again.
      /// </summary>
      public void RegisterRefusal(Hero spouse, int today)
      {
         if (spouse == null || Hero.MainHero == null) return;

         ClearDemandCycle(spouse.StringId);

         int refusals = IncrementRefusalCount(spouse.StringId);

         GrudgeBehavior.Instance?.TryPlant(spouse, Hero.MainHero, GrudgeSource.Divorce,
            "Begged the player to end our marriage, and was refused.", RefusalGrudgeMagnitude, true, today);

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s divorce demand was refused (refusal #{refusals}) — resentment deepens.");

         // M-D4: each refusal also costs a little vanilla relation. Otherwise a grudge-triggered demand can loop
         // forever (demand, timeout, refusal, grudge, demand...) because ShouldEscalate gates on the vanilla
         // relation being low, which nothing else on this path lowers. A steady drip converges the loop to an
         // escalation that actually resolves. Applied BEFORE the escalation facts below, so a refusal that crosses
         // the threshold can escalate on the same tick.
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, Hero.MainHero, -RefusalRelationHit, false);

         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId)?.ReputationWithPlayer ?? 0;

         var escalationFacts = new SpouseDivorceEscalationFacts {
            RefusalCount = refusals,
            SpouseRelationWithPlayer = spouse.GetRelation(Hero.MainHero),
            SpouseReputationWithPlayer = regard,
            AlreadyEscalating = IsEstranging(spouse.StringId),
            EitherPregnant = spouse.IsPregnant || Hero.MainHero.IsPregnant,
            EitherPrisoner = spouse.IsPrisoner || Hero.MainHero.IsPrisoner
         };

         if (SpouseDivorceDemandPolicy.ShouldEscalate(escalationFacts)) BeginEstrangement(spouse, today);
      }

      /// <summary>Runs one weekly evaluation immediately (console / self-test), skipping the wait for the real weekly tick.</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore)
      {
         _demandStartDay = SyncDictionary(dataStore, SaveKey, _demandStartDay, "demand(s)");
         _refusalCount = SyncDictionary(dataStore, RefusalCountSaveKey, _refusalCount, "refusal count(s)");
         _escalationDeadlineDay = SyncDictionary(dataStore, EscalationSaveKey, _escalationDeadlineDay, "active estrangement(s)");
         _demandCycleBeginDay = SyncDictionary(dataStore, AnnounceCycleSaveKey, _demandCycleBeginDay, "announce cycle(s)");
         _announceRetries = SyncDictionary(dataStore, AnnounceRetriesSaveKey, _announceRetries, "announce retry count(s)");
      }

      #region private

      /// <summary>
      ///   Escalation: the demand stops waiting on the player's consent and becomes an outright
      ///   estrangement with a deadline (<see cref="DivorcePolicy.ProcessDurationDays" /> from today).
      ///   Clears any pending demand flag (<see cref="SweepEscalations" />, not <see cref="SweepTimeouts" />,
      ///   now owns this spouse) and queues the announcing letter in the spouse's own voice.
      /// </summary>
      private void BeginEstrangement(Hero spouse, int today)
      {
         ClearDemandCycle(spouse.StringId);
         int deadline = today + DivorcePolicy.ProcessDurationDays;
         _escalationDeadlineDay[spouse.StringId] = deadline;

         // C1 (aggravated variant): the estrangement sets its walk-out deadline HERE, so a guaranteed in-person
         // notice must fire alongside the LLM letter below. Otherwise a failed or intercepted letter would let the
         // spouse walk out with no warning ever received, exactly the blindside the CHANGELOG promise forbids.
         ForceDeliveredNotice(spouse,
            $"{spouse.Name} has resolved to end your marriage, and will not wait much longer to be free of it.");

         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(spouse.StringId);

         if (behavior?.LlmServices == null || profile == null)
         {
            ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name} begins an estrangement (deadline day {deadline} — no profile/LLM available, no letter queued).");

            return;
         }

         const string context = "You have been refused too many times, and have resolved to wait no longer: you begin " +
                                 "to end this marriage yourself, whether or not the player consents.";

         LetterGenerationService.QueueInitialLetter(
            profile, spouse, LetterReason.SpouseDivorceEscalation, context, behavior.LlmServices, behavior.Store, today);

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name} begins an estrangement — deadline day {deadline} — announcing letter queued.");
      }

      /// <summary>Records a fresh demand and queues the announcing letter, in the spouse's own voice.</summary>
      private void Begin(Hero spouse, int today)
      {
         // The refusal clock does NOT start here: it starts only once the announcing letter has actually reached
         // the player (SweepTimeouts starts it on delivery). A demand the player never saw must never time out
         // into a refusal, so a deferred or in-flight letter cannot silently escalate to a divorce.
         _demandStartDay[spouse.StringId] = DivorceDemandTimeoutPolicy.ClockNotStarted;

         // Open the announce phase (romance audit C1/C2): remember the day this cycle's letter is queued so a
         // stale letter from a previous cycle cannot start the clock, and so a lapsed window can re-announce.
         _demandCycleBeginDay[spouse.StringId] = today;
         _announceRetries[spouse.StringId] = 0;

         if (!QueueDemandLetter(spouse, today))
         {
            ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s demand begins (no profile/LLM available; the announce phase will fall back to a notice).");

            return;
         }

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s demand begins, announcing letter queued.");
      }

      /// <summary>
      ///   Queues the spouse's divorce-demand announcing letter, in their own voice. Returns false when there is
      ///   no profile or LLM to write it (so the caller knows the announce phase must fall back to a forced
      ///   notice, romance audit C1). Shared by <see cref="Begin" /> and the re-announce path so a lapsed letter
      ///   is re-queued exactly as the first was.
      /// </summary>
      private static bool QueueDemandLetter(Hero spouse, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(spouse.StringId);

         if (behavior?.LlmServices == null || profile == null) return false;

         const string context = "You have grown truly, deeply unhappy in this marriage and have resolved that it must " +
                                 "end. You are writing to announce this before you press the matter in person.";

         LetterGenerationService.QueueInitialLetter(
            profile, spouse, LetterReason.SpouseDivorceDemand, context, behavior.LlmServices, behavior.Store, today);

         return true;
      }

      /// <summary>
      ///   A guaranteed, courier-free in-person notice (romance audit C1): the last resort when the letter path
      ///   cannot reach the player, so the demand/estrangement arc always informs them and never blindsides. Uses
      ///   the vanilla quick-information banner, which cannot be lost or deferred. Never throws.
      /// </summary>
      private static void ForceDeliveredNotice(Hero spouse, string message)
      {
         try { MBInformationManager.AddQuickInformation(new TextObject(message)); }
         catch (Exception ex) { ModLog.Error($"[SPOUSE_DIVORCE] forced notice failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Every fact <see cref="SpouseDivorceDemandPolicy.ShouldDemand" /> needs, gathered live for the player's current spouse.</summary>
      private SpouseDivorceDemandFacts BuildFacts(Hero spouse)
      {
         Hero player = Hero.MainHero;
         int today = Today;

         TraitFacts traits = GrudgeBridge.BuildTraitFacts(spouse);
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(spouse, today) ?? Array.Empty<Grudge>();

         var maxGrudgeMagnitude = 0;
         var recentJealousyWound = false;
         foreach (Grudge g in live)
         {
            if (g.TargetId != player?.StringId) continue;

            maxGrudgeMagnitude = Math.Max(maxGrudgeMagnitude, GrudgePolicy.EffectiveMagnitude(g, today, traits));

            if (g.Source == GrudgeSource.Jealousy && today - g.EventDay <= RecentWoundWindowDays)
               recentJealousyWound = true;
         }

         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId)?.ReputationWithPlayer ?? 0;

         return new SpouseDivorceDemandFacts {
            SpouseRelationWithPlayer = player != null
               ? spouse.GetRelation(player)
               : 0,
            SpouseReputationWithPlayer = regard,
            RecentJealousyWound = recentJealousyWound,
            DiscoveredBastardScandal = HasRecentBastardScandal(spouse, today),
            LiveGrudgeMagnitudeAgainstPlayer = maxGrudgeMagnitude,
            AlreadyDemanding = IsDemanding(spouse.StringId),
            EitherPregnant = spouse.IsPregnant || player?.IsPregnant == true,
            EitherPrisoner = spouse.IsPrisoner || player?.IsPrisoner == true
         };
      }

      private void Evaluate()
      {
         Hero spouse = Hero.MainHero?.Spouse;

         if (spouse is not {IsAlive: true}) return;
         if (IsDemanding(spouse.StringId)) return; // one demand at a time — the grace-period sweep owns it now
         if (IsEstranging(spouse.StringId)) return; // already past demanding — SweepEscalations owns them now

         SpouseDivorceDemandFacts facts = BuildFacts(spouse);

         if (SpouseDivorceDemandPolicy.ShouldDemand(facts)) Begin(spouse, Today);
      }

      /// <summary>Bumps and returns spouseId's running refusal count (starts at 1 on their first refusal).</summary>
      private int IncrementRefusalCount(string spouseId)
      {
         _refusalCount.TryGetValue(spouseId, out int count);
         count++;
         _refusalCount[spouseId] = count;

         return count;
      }

      /// <summary>
      ///   True when a Scandal-type world event naming a natural-born child and touching the player has
      ///   reached the spouse within <see cref="RecentWoundWindowDays" /> — the Bastards pillar's discovery
      ///   scandal (see <c>BastardBehavior.RecordScandal</c>'s matching "natural child"/"natural-born child"
      ///   phrasing, which this deliberately keys on so an unrelated Scandal, such as another couple's
      ///   divorce, does not false-positive as a bastard wound).
      /// </summary>
      private static bool HasRecentBastardScandal(Hero spouse, int today)
      {
         try
         {
            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> known =
               WorldEventStore.Instance?.GetKnownEventsRated(spouse, today, 12);

            if (known == null) return false;

            return known.Any(e => e.Event.Type == WorldEventType.Scandal
                                   && e.Event.InvolvesPlayer
                                   && today - e.Event.Day <= RecentWoundWindowDays
                                   && e.Event.Summary != null
                                   && e.Event.Summary.IndexOf("natural", StringComparison.OrdinalIgnoreCase) >= 0);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SPOUSE_DIVORCE] HasRecentBastardScandal failed (treated as no wound): {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      private void OnWeeklyTick()
      {
         SafeRun.Step("SpouseDivorceDemandBehavior.Evaluate", Evaluate);
         SafeRun.Step("SpouseDivorceDemandBehavior.SweepTimeouts", SweepTimeouts);
         SafeRun.Step("SpouseDivorceDemandBehavior.SweepEscalations", SweepEscalations);
      }

      /// <summary>
      ///   The player genuinely repaired the bond in time: the estrangement is called off, with relief
      ///   rather than rancour. Resets the refusal count too (<see cref="CancelEstrangement" />) — a truly
      ///   warmed marriage earns a fresh start, not a demand primed to reopen on the very next slight.
      /// </summary>
      private void ResolveReconciliation(Hero spouse, int today)
      {
         CancelEstrangement(spouse.StringId);

         // Clear the spouse's divorce grudge(s): a called-off divorce must not keep shadowing the marriage in
         // conversation (the "she is warm until you mention the divorce, then fixates" report).
         GrudgeBehavior.Instance?.ResolveDivorceGrudgesFrom(spouse.StringId, today);

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId);

         if (profile != null)
         {
            profile.Events.Add(new NotableEvent(today, NotableEventType.Agreement,
               "Our marriage stood at the edge of ending, but a genuine warmth called me back from it."));
            NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
         }

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s estrangement is called off — the marriage is reconciled.");

         // Belt on top of the delivery-time safety net (LetterIntentionResolver): a reconciled spouse no longer
         // wants a divorce, so their own already-queued divorce letters are purged right away rather than left
         // to be caught only when they would otherwise be delivered.
         PurgeStaleDivorceLetters(profile);
      }

      /// <summary>
      ///   Removes every pending <see cref="LetterReason.SpouseDivorceDemand" />/
      ///   <see cref="LetterReason.SpouseDivorceEscalation" /> letter from <paramref name="profile" />'s own
      ///   <see cref="NpcProfile.SentLetters" />, since the reconciliation just called off means the spouse
      ///   no longer wants a divorce. Fail-soft: this is a proactive cleanup on top of the delivery-time
      ///   <see cref="Letters.LetterIntentionResolver" /> guard, never the sole line of defense, so any problem
      ///   here is logged and swallowed rather than thrown.
      /// </summary>
      private static void PurgeStaleDivorceLetters(NpcProfile profile)
      {
         try
         {
            if (profile?.SentLetters == null) return;

            List<PendingLetter> stale = profile.SentLetters
               .Where(l => l.Reason == LetterReason.SpouseDivorceDemand || l.Reason == LetterReason.SpouseDivorceEscalation)
               .ToList();

            if (stale.Count == 0) return;

            var inFlight = 0;
            foreach (PendingLetter letter in stale)
            {
               if (!string.IsNullOrEmpty(letter.CourierPartyId)) inFlight++;
               profile.SentLetters.Remove(letter);
            }

            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);

            // No known API cleanly recalls/destroys an in-flight reply courier by id from here (CourierService's
            // own destroy/waylay paths mark the letter lost AND surface a player-visible "courier waylaid"
            // notification, which would be a confusing leak of its own): an in-flight courier is simply left to
            // arrive, where CourierService's own LetterIntentionResolver check silently drops it on arrival.
            ModLog.Info(inFlight > 0
               ? $"[SPOUSE_DIVORCE] purged {stale.Count} stale divorce letter(s) for {profile.Name} on reconciliation ({inFlight} still in flight by courier, left to the delivery-time safety net)."
               : $"[SPOUSE_DIVORCE] purged {stale.Count} stale divorce letter(s) for {profile.Name} on reconciliation.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SPOUSE_DIVORCE] stale divorce letter purge failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The deadline passed with neither acceptance nor reconciliation: the spouse walks out for real,
      ///   through the SAME <see cref="MarriageDissolutionService" /> every other divorce path already
      ///   uses — its own live re-validation IS the anti-double-dissolve guard, so an already-dissolved or
      ///   otherwise no-longer-eligible marriage (say, a pregnancy or captivity that arose in the meantime)
      ///   simply fails here and the escalation is left active for a retry at next week's sweep, rather
      ///   than silently vanishing. Consequences are deliberately MODERATE: one grudge on the spouse (never
      ///   the tiered family/friends storm a player-initiated repudiation earns), a Scandal world event
      ///   framed as the SPOUSE's own departure, and a profile note.
      /// </summary>
      private void ResolveWalkOut(Hero spouse, int today)
      {
         Hero player = Hero.MainHero;
         if (player == null) return;

         // M-D1: capture the PRE-dissolution baseline before dissolving, so the SET below can neutralise the
         // engine's own hidden relation penalty (observed crashing to -100) the way every other dissolution path
         // does. Without this SET the "deliberately moderate" walk-out was in practice the most violent path.
         int spouseVsPlayerBefore = spouse.GetRelation(player);

         if (!MarriageDissolutionService.TryDissolvePlayerMarriage(spouse, out string reason))
         {
            // M-D2: a TRANSIENT block (pregnancy/captivity) clears on its own, so a silent daily retry is
            // correct. A STRUCTURAL block (the spouse commands a party or governs a fief) never clears by
            // itself: left alone it froze the walk-out forever and let a player freeze ANY divorce just by
            // handing their spouse a party. Release what can be released so tomorrow's retry can succeed.
            if (MarriageDissolutionService.ClassifyBlock(player, spouse) == DivorceBlock.Structural)
               MarriageDissolutionService.ReleaseStructuralRoles(spouse);

            ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s estrangement deadline passed but the walk-out could not complete ({reason}) — will retry.");

            return;
         }

         CancelEstrangement(spouse.StringId);

         // Set the spouse's regard for the player from the baseline plus a MODERATE hit, correcting for whatever
         // the engine applied during the dissolution itself, so CR owns the emotional outcome rather than a raw
         // engine crash to -100.
         int walkOutTarget = spouseVsPlayerBefore + WalkOutRelationHit;
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, player, walkOutTarget - spouse.GetRelation(player), false);

         GrudgeBehavior.Instance?.TryPlant(spouse, player, GrudgeSource.Divorce,
            "Waited long enough to be heard, was refused too many times, and finally ended our marriage myself.",
            EstrangementWalkOutGrudgeMagnitude, true, today);

         // Grudges pillar (UnblessedMarriage): this walk-out ends the player's OWN marriage just as
         // surely as end_own_marriage does, so any live UnblessedMarriage grudge the spouse's family holds
         // against the player (planted at the wedding by MarriageApprovalBehavior when unblessed) clears
         // here too, not only on the player-initiated path.
         GrudgeBehavior.Instance?.ResolveUnblessedMarriageGrudgesAgainst(player.StringId, today);

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId);

         if (profile != null)
         {
            profile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal,
               "Grew weary of being refused, again and again, and ended our marriage myself, unable to wait any longer."));
            NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
         }

         try
         {
            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Scandal,
               Day = today,
               Magnitude = DivorcePolicy.ScaledScandalMagnitude(spouse.Culture?.StringId),
               InvolvesPlayer = true,
               ActorIds = new List<string> {player.StringId, spouse.StringId},
               FactionIds = new List<string>(new[] {player.MapFaction?.StringId, spouse.MapFaction?.StringId}.Where(id => !string.IsNullOrEmpty(id))),
               Summary = $"Weary of waiting, {spouse.Name} has ended their marriage to {player.Name} and departed.",
               ChronicleLine = $"weary of waiting on me, {spouse.Name} ended our marriage and departed"
            });
         }
         catch (Exception ex) { ModLog.Error($"[SPOUSE_DIVORCE] walk-out world-event record failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name} walked out: player and {spouse.Name} are no longer wed (estrangement deadline reached).");
      }

      /// <summary>
      ///   The primary refusal mechanism: a demand left unanswered beyond <see cref="GracePeriodDays" /> is
      ///   read as the spouse's silence, not consent, so it registers exactly the same refusal an explicit
      ///   decline_divorce would (see <see cref="RegisterRefusal" />). Also drops any stale entry whose
      ///   spouse died or is no longer actually wed to the player (divorced or widowed by some other means
      ///   since the demand began), so the dictionary never accumulates orphans.
      /// </summary>
      private void SweepTimeouts()
      {
         if (_demandStartDay.Count == 0) return;

         int today = Today;
         foreach (string spouseId in _demandStartDay.Keys.ToList())
         {
            Hero spouse = Hero.FindFirst(h => h != null && h.StringId == spouseId);

            if (spouse is not {IsAlive: true} || spouse != Hero.MainHero?.Spouse)
            {
               ClearDemandCycle(spouseId); // stale: no longer a live demand against the player

               continue;
            }

            // Reconciliation during the DEMAND phase (romance audit M-D5): a spouse the player has genuinely
            // warmed calls the demand off, mirroring SweepEscalations, instead of grinding on to a timeout that
            // stamps a refusal + grudge on a marriage the player has actually mended. Read the mod's PERSONAL
            // regard (the number conversation moves), not the lagging vanilla relation; a resolved demand earns a
            // fresh start (refusal count reset), like every other resolution path.
            int reconcileRegard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouseId)?.ReputationWithPlayer
                                  ?? spouse.GetRelation(Hero.MainHero);
            if (SpouseDivorceDemandPolicy.IsReconciled(new NpcMemoryService.Core.Standing.Regard(reconcileRegard)))
            {
               ClearDemandCycle(spouseId);
               _refusalCount.Remove(spouseId);
               ModLog.Info($"[DIVORCE] {spouse.Name} reconciled during the demand phase; the demand is withdrawn.");

               continue;
            }

            // Announce phase (romance audit C1/C2): the refusal clock starts only once THIS cycle's announcing
            // letter has actually reached the player. A stale letter from a previous cycle can never start it
            // (the cycle-begin filter), and a letter that never arrives re-announces and, failing that, forces a
            // guaranteed notice, so the arc neither soft-locks nor blindsides.
            if (_demandStartDay[spouseId] == DivorceDemandTimeoutPolicy.ClockNotStarted)
            {
               AdvanceAnnouncePhase(spouse, spouseId, today);

               continue; // never time out on the same tick the clock starts
            }

            if (!DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = _demandStartDay[spouseId], Today = today, GracePeriodDays = GracePeriodDays})) continue;

            Hero timedOutSpouse = spouse;
            SafeRun.Step($"SpouseDivorceDemandBehavior.SweepTimeouts({spouseId})",
               () => RegisterRefusal(timedOutSpouse, today));
         }
      }

      /// <summary>
      ///   Drives one tick of the announce phase for a demand whose clock has not started (romance audit C1/C2),
      ///   via the pure <see cref="DivorceAnnouncePolicy" />: start the clock once the CURRENT cycle's letter is
      ///   delivered; wait while it is still in flight; re-announce a lapsed one; and, after
      ///   <see cref="DivorceAnnouncePolicy.MaxReannounces" /> lapses, give a guaranteed in-person notice and
      ///   start the clock so the arc can never dead-lock or blindside.
      /// </summary>
      private void AdvanceAnnouncePhase(Hero spouse, string spouseId, int today)
      {
         int cycleBegin = _demandCycleBeginDay.TryGetValue(spouseId, out int begin) ? begin : today;
         _announceRetries.TryGetValue(spouseId, out int retries);
         bool delivered = DemandLetterDelivered(spouse, cycleBegin);

         switch (DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = delivered, Today = today, CycleBeginDay = cycleBegin, ReannounceCount = retries}))
         {
            case DivorceAnnounceStep.StartClock:
               _demandStartDay[spouseId] = today;
               break;

            case DivorceAnnounceStep.Reannounce:
               _announceRetries[spouseId] = retries + 1;
               _demandCycleBeginDay[spouseId] = today; // a fresh delivery window for the re-queued letter
               QueueDemandLetter(spouse, today);
               ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s divorce announcement went undelivered; re-announcing (attempt {retries + 1}).");
               break;

            case DivorceAnnounceStep.ForceNotice:
               ForceDeliveredNotice(spouse,
                  $"Word reaches you at last: {spouse.Name} means to end your marriage, and has grown weary of waiting to be heard.");
               _demandStartDay[spouseId] = today; // the player has now been told, so the grace clock may run
               ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s divorce announcement could not be delivered after {retries} re-announces; forced an in-person notice and started the grace clock.");
               break;

            // Wait: the letter is still within its delivery window; do nothing this tick.
         }
      }

      /// <summary>
      ///   True once the spouse's divorce-demand letter FROM THE CURRENT CYCLE (sent on or after
      ///   <paramref name="sinceDay" />, the day this demand cycle's letter was queued) has actually been
      ///   delivered to the player. The cycle filter is romance audit C2: without it a stale, still-delivered
      ///   letter from a PREVIOUS cycle would falsely start the clock, stamping a refusal for a demand the player
      ///   never saw this time. Until a current-cycle letter lands, the clock does not start.
      /// </summary>
      private static bool DemandLetterDelivered(Hero spouse, int sinceDay)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId);
         if (profile?.SentLetters == null) return false;

         PendingLetter demand = profile.SentLetters
            .Where(l => l.Reason == LetterReason.SpouseDivorceDemand && l.SentOnDay >= sinceDay)
            .OrderByDescending(l => l.SentOnDay)
            .FirstOrDefault();

         return demand != null && demand.IsDelivered && !demand.IsLost;
      }

      /// <summary>
      ///   Sweeps every active estrangement: a spouse whose relation with the player has risen to a
      ///   genuine warmth (<see cref="SpouseDivorceDemandPolicy.IsReconciled" />) calls it off — checked
      ///   BEFORE the deadline, so a reconciliation always wins even on the deadline's own day; one whose
      ///   deadline has passed without reconciling walks out for real (<see cref="ResolveWalkOut" />);
      ///   otherwise the estrangement simply remains pending. Also drops any stale entry whose spouse died
      ///   or is no longer actually wed to the player, mirroring <see cref="SweepTimeouts" />.
      /// </summary>
      private void SweepEscalations()
      {
         if (_escalationDeadlineDay.Count == 0) return;

         int today = Today;
         foreach (string spouseId in _escalationDeadlineDay.Keys.ToList())
         {
            Hero spouse = Hero.FindFirst(h => h != null && h.StringId == spouseId);

            if (spouse is not {IsAlive: true} || spouse != Hero.MainHero?.Spouse)
            {
               CancelEstrangement(spouseId); // stale — no longer a live estrangement against the player

               continue;
            }

            // Reconciliation reads the mod's PERSONAL regard (the number the player actually warms through
            // conversation), not the vanilla clan relation, so a marriage the player has genuinely mended calls
            // off the walk-out even when the vanilla number lags behind.
            int reconcileRegard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouseId)?.ReputationWithPlayer
                                  ?? spouse.GetRelation(Hero.MainHero);
            if (SpouseDivorceDemandPolicy.IsReconciled(new NpcMemoryService.Core.Standing.Regard(reconcileRegard)))
            {
               Hero reconciledSpouse = spouse;
               SafeRun.Step($"SpouseDivorceDemandBehavior.SweepEscalations.Reconciled({spouseId})",
                  () => ResolveReconciliation(reconciledSpouse, today));

               continue;
            }

            if (today < _escalationDeadlineDay[spouseId]) continue; // still pending — deadline not yet reached

            Hero departingSpouse = spouse;
            SafeRun.Step($"SpouseDivorceDemandBehavior.SweepEscalations.WalkOut({spouseId})",
               () => ResolveWalkOut(departingSpouse, today));
         }
      }

      /// <summary>
      ///   Shared byte[] SyncData plumbing for this behavior's spouseId-keyed int dictionaries (demand
      ///   start day, refusal count, escalation deadline day): each lives under its own save key, so an
      ///   older save simply lacks the newer ones, and a corrupt blob degrades to an empty dictionary for
      ///   that key alone rather than failing the whole load. <paramref name="label" /> only feeds the
      ///   post-load info log.
      /// </summary>
      private Dictionary<string, int> SyncDictionary(IDataStore dataStore, string saveKey, Dictionary<string, int> current, string label)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(current, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SPOUSE_DIVORCE] serialize failed for {saveKey} — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(saveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SPOUSE_DIVORCE] byte[] save failed for {saveKey}: {ex.GetType().Name}: {ex.Message}"); }

            return current;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(saveKey, ref loaded); }
         catch
         {
            // Key absent in older saves — the whole point of the additive-key save-safety rule.
         }

         Dictionary<string, int> result;
         if (loaded is {Length: > 0})
            try { result = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(loaded)) ?? new Dictionary<string, int>(); }
            catch (Exception ex)
            {
               result = new Dictionary<string, int>();
               ModLog.Error($"[SPOUSE_DIVORCE] load failed for {saveKey} — reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
            }
         else
            result = new Dictionary<string, int>();

         ModLog.Info($"[SPOUSE_DIVORCE] loaded {result.Count} {label} from save.");

         return result;
      }

      #endregion
   }
}
