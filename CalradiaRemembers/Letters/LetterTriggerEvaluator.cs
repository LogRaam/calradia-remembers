// Code written by Gabriel Mailhot, 21/06/2026.
// B2: replaced the fire-immediately / round-robin daily evaluation with a weighted, budgeted mailbox
// scheduler (CalradiaRemembers.Logic.LetterMailboxPolicy). Event handlers now ENQUEUE a LetterCandidateIntent
// instead of writing straight away; EvaluateDaily collects every eligible candidate across every correspondent
// into one pool and lets the scheduler pick at most one winner per tick, aged and budgeted against a
// rolling window derived from persisted SentLetters. Guaranteed obligations (ProcessScheduledLetters, and
// every LetterMailboxPolicy.BypassesBudget reason) never enter the pool and never consume budget.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Letters;
using CalradiaRemembers.Logic.Prisoners;
using NpcMemoryService.Core.Standing;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
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
   ///   an NPC should initiate a letter to the player. Event handlers enqueue a
   ///   <see cref="LetterCandidateIntent" /> (or, for a few live-recomputed categories,
   ///   a fresh <see cref="LetterCandidate" /> built each tick); <see cref="EvaluateDaily" />
   ///   pools every eligible candidate and hands the pool to
   ///   <see cref="LetterMailboxPolicy.SelectNext" />, which picks at most one winner per
   ///   tick, subject to per-NPC cooldowns, in-transit limits, and the rolling-window budget.
   /// </summary>
   public sealed class LetterTriggerEvaluator
   {
      // Fixed gold amount requested for child support. Public so the quest opened on delivery (LetterPopupManager)
      // records it as the amount that must actually be paid to close the demand (courier audit M14).
      public const int ChildSupportAmount = 750;

      // Days after birth before the child-support request letter is sent.
      private const int ChildSupportDelayDays = 7;

      // Minimum days between two letters from the same NPC (anti-spam).
      private const int DefaultCooldownDays = 15;

      // We cap how many letters queue up from a single event.
      private const int MaxLettersPerEvent = 2;

      // B2: no longer gates anything (the budget scheduler owns that job now); kept purely so a log line can
      // still report how long it has been since the mailbox last actually released something.
      private int _lastDailyLetterDay = -1;

      // B1: persisted per-subject and global correspondence-interest dampers (see CorrespondenceInterestPolicy).
      // Keyed by (int)LetterReason; values are dampers <= 0, moved only through the clamped accessors below.
      // B2: now READ by the mailbox scheduler (LetterWeights.WeightWithInterest) to weight every candidate.
      private Dictionary<int, int> _subjectInterest = new Dictionary<int, int>();
      private int _globalInterest;

      // Additive, distinct save key: a save written before this feature simply has no such key, and an
      // absent/garbled blob degrades to the empty defaults above (0 = neutral everywhere) rather than throwing.
      private const string LetterPacingSaveKey = "CalradiaRemembers_LetterPacing_v1bytes";

      // ── Daily evaluation (called from OnDailyTick) ────────────────────────

      /// <summary>
      ///   B2: the mailbox scheduler's daily tick. Guaranteed obligations are processed first and
      ///   unconditionally (<see cref="ProcessScheduledLetters" />); everything else is pooled as
      ///   <see cref="LetterCandidate" />s (persisted event intents plus freshly recomputed romantic/spouse/
      ///   marriage/follow-up candidates) and handed to <see cref="LetterMailboxPolicy.SelectNext" />, which
      ///   picks at most one winner for today, weighed against a budget derived purely from persisted
      ///   <see cref="PendingLetter" /> history (<see cref="GatherRecentReleaseDays" />), robust to a reload,
      ///   unlike the old in-memory global cooldown it replaces.
      /// </summary>
      public void EvaluateDaily(BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
      {
         // 2.5: committed obligations (child support) are written on their due day with bounded retry, BEFORE
         // the mailbox competes for anything: a pension the NPC already committed to must never be throttled
         // (or lost) by the spontaneous-correspondence budget.
         ProcessScheduledLetters(store, llmServices, today);

         var pool = new List<LetterCandidate>();
         var release = new Dictionary<LetterCandidate, Action>();

         CollectEventCandidates(store, llmServices, today, pool, release);
         CollectRomanticCorrespondence(store, llmServices, today, pool, release);
         CollectAwaitingReplies(store, llmServices, today, pool, release);
         CollectSpouseCorrespondence(store, llmServices, today, pool, release);
         CollectMarriageSolicitations(store, llmServices, today, pool, release);
         CollectPrisonerOffers(store, llmServices, today, pool, release);
         CollectServiceOffers(store, llmServices, today, pool, release);
         CollectAimLetters(store, llmServices, today, pool, release);

         LetterBudget budget = LetterPacingRules.BudgetFor(ModSettings.LettersFromNpcs);
         IReadOnlyList<int> recent = GatherRecentReleaseDays(store, today, budget.WindowDays);

         LetterCandidate chosen = LetterMailboxPolicy.SelectNext(pool, recent, today, budget, MBRandom.RandomInt(100));

         if (ModSettings.VerboseLogging)
            ModLog.Info($"[LETTER] mailbox tick: pool={pool.Count}, budget={budget.MaxPerWindow}/{budget.WindowDays}d, released={(chosen != null ? chosen.Reason.ToString() : "none")}.");

         if (chosen == null || !release.TryGetValue(chosen, out Action act)) return;

         act();
         _lastDailyLetterDay = today;
         ModLog.Info($"[LETTER] mailbox released {chosen.Reason} from {chosen.SenderId} (pool of {pool.Count}, weight {chosen.Weight}).");
      }

      /// <summary>
      ///   A lord writes about an aim another mod registered for him (CrAims; tashmetu, 14/10/2026: "nobody takes the
      ///   first step with the player"; Gabriel, 28/10/2026). One candidate per lord whose aim is open to letters and may
      ///   be raised now (AimsBehavior.AimToRaise), and who may write today (CanSendLetter, then ActionGate.Letter when
      ///   it is queued). The mailbox's budget and weights pace it like any other letter; released, the aim is marked
      ///   raised by letter, so he does not raise it again too soon by any channel.
      /// </summary>
      private void CollectAimLetters(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         Aims.AimsBehavior aims = Aims.AimsBehavior.Instance;
         if (aims == null) return;

         foreach (Logic.Aims.StandingAim standing in aims.Book.All.ToList())
         {
            Hero hero = Hero.FindFirst(h => h != null && h.StringId == standing.NpcId);
            if (hero == null || !Mapping.HeroPresence.IsInTheWorld(hero)) continue;

            Logic.Aims.StandingAim aim = aims.AimToRaise(hero, Logic.Aims.AimChannels.Letter);
            if (aim == null) continue;

            NpcProfile profile = store.Get(hero.StringId);
            if (profile == null || !CanSendLetter(profile, today, DefaultCooldownDays)) continue;
            // Asked here as well as when the letter is queued, so a lord who may not write never takes the mailbox's slot.
            if (!Mapping.ActionGate.Allows(hero, Logic.ActionKind.Letter)) continue;

            string context = Logic.Aims.AimLetterText.Context(aim, Logic.Aims.AimApproachWording.Describe(new Logic.Aims.AimApproachFacts {
               Honor = hero.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor),
               Calculating = hero.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Calculating),
               HasVerifiedService = !string.IsNullOrWhiteSpace(aim.MissionId)
            }));
            if (context == null) continue;

            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {
               BaseWeight = LetterWeights.BaseWeight(LetterReason.StandingAim), SenderInterest = profile.CorrespondenceInterest,
               SubjectInterest = GetSubjectInterest(LetterReason.StandingAim), GlobalInterest = GlobalInterest
            });
            var candidate = new LetterCandidate {Reason = LetterReason.StandingAim, SenderId = hero.StringId, Weight = weight, EligibleSinceDay = aim.RegisteredOnDay};
            pool.Add(candidate);

            release[candidate] = () =>
            {
               LetterGenerationService.QueueInitialLetter(profile, hero, LetterReason.StandingAim, context, llmServices, store, today);
               aims.MarkRaised(aim, Logic.Aims.AimChannels.Letter);
               ModLog.Info($"[AIMS] {hero.Name} writes of {aim.SourceMod}/{aim.Id}.");
            };
         }
      }

      /// <summary>
      ///   B2: the mailbox budget's window membership, derived purely from persisted
      ///   <see cref="NpcProfile.SentLetters" />: every letter that actually competed for (and won) a
      ///   budget slot recently, across EVERY correspondent. Deliberately reload-proof: the old in-memory
      ///   global cooldown could let one extra letter through after a reload; this cannot, since it is
      ///   recomputed from the save every tick. <see cref="LetterMailboxPolicy.CountsTowardBudget" /> excludes
      ///   both a lost courier's letter and any guaranteed-bypass reason, so neither shrinks the window.
      /// </summary>
      private static IReadOnlyList<int> GatherRecentReleaseDays(BannerlordNpcMemoryStore store, int today, int windowDays)
      {
         var days = new List<int>();

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.SentLetters == null) continue;

            foreach (PendingLetter l in profile.SentLetters)
            {
               if (!LetterMailboxPolicy.CountsTowardBudget(l.Reason, l.IsLost)) continue;
               if (today - l.SentOnDay >= windowDays) continue;

               days.Add(l.SentOnDay);
            }
         }

         return days;
      }

      // ── B2: candidate collectors (populate the pool; mutate nothing until release() fires) ──────────────

      /// <summary>
      ///   Drains every profile's persisted <see cref="NpcProfile.LetterCandidates" /> (enqueued by the event
      ///   handlers below) into the pool: one <see cref="LetterCandidate" /> per intent whose
      ///   <see cref="LetterCandidateIntent.EligibleSinceDay" /> has arrived and whose sender still passes the
      ///   shared <see cref="CanSendLetter" /> gate (in-transit, cooldown, proximity, prisoner). A sender who
      ///   has since died can never write the letter they intended to, so their whole backlog is dropped
      ///   rather than left to accumulate forever. Likewise, an intent that outlives its
      ///   <see cref="LetterCandidateFreshness" /> window without ever being chosen is quietly retired: we do
      ///   not need to deliver every letter, only the ones that still matter, so stale news is dropped rather
      ///   than force-delivered.
      /// </summary>
      private void CollectEventCandidates(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.LetterCandidates == null || profile.LetterCandidates.Count == 0) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero != null && !hero.IsAlive)
            {
               // Nobody left to write these; do not let a dead sender's backlog linger in the save forever.
               profile.LetterCandidates.Clear();
               store.Set(profile);

               continue;
            }

            if (hero == null) continue; // transient: not resolvable this tick, try again later.

            var stale = new List<LetterCandidateIntent>();

            foreach (LetterCandidateIntent intent in profile.LetterCandidates)
            {
               bool sharesRealm = hero.MapFaction != null && hero.MapFaction == Hero.MainHero?.MapFaction;

               if (LetterCandidateFreshness.IsStale(intent.Reason, intent.EligibleSinceDay, today)
                   || !LetterCandidateFreshness.PremiseStillHolds(intent.Reason, sharesRealm))
               {
                  stale.Add(intent);
                  continue;
               }

               if (intent.EligibleSinceDay > today) continue;
               if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

               int baseWeight = intent.BaseWeightOverride > 0 ? intent.BaseWeightOverride : LetterWeights.BaseWeight(intent.Reason);
               int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(intent.Reason), GlobalInterest = GlobalInterest});

               var candidate = new LetterCandidate {
                  Reason = intent.Reason,
                  SenderId = id,
                  Weight = weight,
                  EligibleSinceDay = intent.EligibleSinceDay
               };
               pool.Add(candidate);

               NpcProfile capturedProfile = profile;
               Hero capturedHero = hero;
               LetterCandidateIntent capturedIntent = intent;
               release[candidate] = () =>
               {
                  LetterGenerationService.QueueInitialLetter(
                     capturedProfile, capturedHero, capturedIntent.Reason, capturedIntent.Context ?? "",
                     llmServices, store, today);
                  capturedProfile.LetterCandidates.Remove(capturedIntent);
                  store.Set(capturedProfile);
               };
            }

            if (stale.Count > 0)
            {
               foreach (LetterCandidateIntent intent in stale)
                  profile.LetterCandidates.Remove(intent);

               store.Set(profile);
               ModLog.Info($"[LETTER] retired {stale.Count} stale candidate(s) for {profile.Name}.");
            }
         }
      }

      /// <summary>
      ///   Every NPC in an active romance who has not seen the player in a while and passes cooldown becomes
      ///   one candidate, not just the first found (as the old round-robin evaluator returned). Weighed and
      ///   pooled here; the actual letter is written only if <see cref="LetterMailboxPolicy.SelectNext" />
      ///   picks it, inside the release closure, exactly as it was fired unconditionally before.
      /// </summary>
      private void CollectRomanticCorrespondence(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.Romantic == null) continue;
            // M-R1: an ACTIVE romance (courting or deeper); Estranged/Broken must not draw romantic letters.
            if (!profile.Romantic.Status.IsCourtingOrDeeper()) continue;
            if (profile.ReputationWithPlayer < Regard.GenuineTier) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true}) continue;
            if (IsWithPlayer(hero)) continue; // travelling companion, no letter needed

            if (!CanSendLetter(profile, today, 10)) continue;
            if (DaysSinceLastEvent(profile, today) < 3) continue; // met recently

            int daysSince = DaysSinceLastEvent(profile, today);
            string context = $"Romantic status: {profile.Romantic.Status}. " + $"Last saw the player {daysSince} days ago." + $" Your feeling toward the player right now: {ThirdPartyDescriptor.DescribeRelation(profile.ReputationWithPlayer)}. Write in a tone true to that feeling, not a generic longing.";

            int baseWeight = LetterWeights.BaseWeight(LetterReason.RomanticCorrespondence);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.RomanticCorrespondence), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.RomanticCorrespondence,
               SenderId = id,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = profile;
            Hero capturedHero = hero;
            release[candidate] = () =>
               LetterGenerationService.QueueInitialLetter(
                  capturedProfile, capturedHero, LetterReason.RomanticCorrespondence, context,
                  llmServices, store, today);
         }
      }

      /// <summary>
      ///   Checks for romantic letters that were delivered but never answered. When the player has gone
      ///   silent for five days after reading an NPC's romantic letter, that NPC becomes a candidate for a
      ///   single impatient follow-up. The <see cref="PendingLetter.FollowUpSent" /> WRITE only happens inside
      ///   the release closure now (not here): an unanswered letter whose candidate is NOT chosen today must
      ///   stay eligible tomorrow, not be silently marked as already followed-up.
      /// </summary>
      private void CollectAwaitingReplies(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.Romantic == null) continue;
            // M-R1: an ACTIVE romance (courting or deeper); Estranged/Broken must not draw romantic letters.
            if (!profile.Romantic.Status.IsCourtingOrDeeper()) continue;
            if (profile.ReputationWithPlayer < Regard.GenuineTier) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true}) continue;
            if (hero.IsPrisoner) continue; // M15: this follow-up bypasses the shared cadence gate, so guard the prisoner case here too.
            if (IsWithPlayer(hero)) continue;

            // Find a delivered romantic letter with no player reply after 5 days.
            PendingLetter unanswered = null;
            foreach (PendingLetter l in profile.SentLetters)
            {
               if (l.Reason != LetterReason.RomanticCorrespondence) continue;
               if (!l.IsDelivered) continue;
               if (!string.IsNullOrWhiteSpace(l.PlayerReply)) continue;
               // 2.3: measure the silence from the REAL delivery day (a slow courier used to make "your silence
               // weighs on me" fire the day after a late arrival). EffectiveDeliveredDay falls back to DeliveryDay
               // on old saves.
               if (today - l.EffectiveDeliveredDay < 5) continue;

               unanswered = l;

               break;
            }

            if (unanswered == null) continue;

            // Primary guard: flag on the original letter survives even when the follow-up
            // placeholder is removed by LLM failure or session-reload orphan cleanup.
            if (unanswered.FollowUpSent) continue;

            // Legacy fallback for saves pre-dating FollowUpSent: if the follow-up letter still exists in
            // SentLetters, backfill the flag and skip. This reflects TRUE past state (a follow-up was already
            // truly sent), not a decision about today's candidate, so it is safe to apply immediately rather
            // than deferring to a release closure.
            var followUpPresent = false;
            foreach (PendingLetter l in profile.SentLetters)
               if (l.Reason == LetterReason.AwaitingReply && l.SentOnDay > unanswered.SentOnDay)
               {
                  followUpPresent = true;

                  break;
               }

            if (followUpPresent)
            {
               unanswered.FollowUpSent = true;
               store.Set(profile);

               continue;
            }

            int daysSince = today - unanswered.EffectiveDeliveredDay;
            string context = $"You wrote a letter {daysSince} days ago expressing your feelings. " +
                             $"The player has not replied. Their silence weighs on you.";

            int baseWeight = LetterWeights.BaseWeight(LetterReason.AwaitingReply);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.AwaitingReply), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.AwaitingReply,
               SenderId = id,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = profile;
            Hero capturedHero = hero;
            PendingLetter capturedUnanswered = unanswered;
            release[candidate] = () =>
            {
               // The flag write is the whole reason this candidate must not fire twice; deferred here so an
               // un-selected candidate stays eligible next tick instead of being silently marked "handled".
               capturedUnanswered.FollowUpSent = true;
               store.Set(capturedProfile);
               LetterGenerationService.QueueInitialLetter(
                  capturedProfile, capturedHero, LetterReason.AwaitingReply, context,
                  llmServices, store, today);

               ModLog.Info($"[LETTER] {capturedProfile.Name} sends awaiting-reply follow-up ({daysSince}d of silence).");
            };
         }
      }

      /// <summary>
      ///   A clan head who regards the player well may become a candidate to proactively propose an alliance
      ///   match by letter: NPC is a lord with eligible kin, player is single, player has some standing (clan
      ///   tier ≥ 1), personal regard ≥ 20, and no proposal was already sent. The one-proposal-ever dedupe is a
      ///   pure read of already-persisted <see cref="NpcProfile.SentLetters" />, which the release closure's
      ///   own <see cref="LetterGenerationService.QueueInitialLetter" /> call keeps accurate for future ticks,
      ///   so it needs no deferral of its own.
      /// </summary>
      private void CollectMarriageSolicitations(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         if (Hero.MainHero?.Spouse != null) return; // player already wed
         if (!Mapping.StatureReader.Player.IsEstablished) return; // no standing

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;
            if (profile.ReputationWithPlayer < Regard.WarmTier) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true} || !hero.IsLord) continue;
            if (IsWithPlayer(hero)) continue;

            // Build prospects — non-empty means this NPC has authority and eligible kin.
            string prospects = MarriageProspectResolver.Build(hero);

            if (string.IsNullOrWhiteSpace(prospects)) continue;

            if (!CanSendLetter(profile, today, 30)) continue;

            // Only one marriage proposal letter ever (avoid pestering the player).
            var alreadySent = false;
            foreach (PendingLetter l in profile.SentLetters)
               if (l.Reason == LetterReason.MarriageProposal)
               {
                  alreadySent = true;

                  break;
               }

            if (alreadySent) continue;

            string context = $"You are a lord of standing and you regard the player highly. " +
                             $"You are writing to propose a marriage alliance between your house and theirs. " +
                             $"Eligible members of your house: {prospects}";

            int baseWeight = LetterWeights.BaseWeight(LetterReason.MarriageProposal);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.MarriageProposal), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.MarriageProposal,
               SenderId = id,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = profile;
            Hero capturedHero = hero;
            release[candidate] = () =>
            {
               LetterGenerationService.QueueInitialLetter(
                  capturedProfile, capturedHero, LetterReason.MarriageProposal, context,
                  llmServices, store, today);

               ModLog.Info($"[LETTER] {capturedProfile.Name} sends marriage solicitation to player.");
            };
         }
      }

      /// <summary>
      ///   A lord who holds a prisoner the player currently keeps (their own kin, else their realm's own) writes
      ///   to buy that captive, naming a fair price (PrisonerOfferPolicy, clamped to the honest ransom band). The
      ///   read window's Accept then settles the exact trade. One outstanding offer per captive from a given buyer;
      ///   a buyer who cannot raise the honest floor makes no offer (the Accept would only refuse it). A buyer the
      ///   player has never met gets a profile created here so any kin/liege can make the offer.
      /// </summary>
      private void CollectPrisonerOffers(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         List<Hero> captives = BuyPrisonerVerb.PlayerHeldHeroPrisoners();
         if (captives.Count == 0) return;

         foreach (Hero captive in captives)
         {
            if (string.IsNullOrEmpty(captive?.StringId)) continue;
            // Word of the capture must reach his people, and their envoy ride out, before anyone treats (aphareish).
            if (!PrisonerExchangePolicy.NewsHasReachedHisPeople((int) (CampaignTime.Now - captive.CaptivityStartTime).ToDays)) continue;

            // The first who may treat (PrisonerBuyerPolicy's order: the captive's house, a parent of another house, the
            // realm, a bitter enemy in the player's own realm), whose own envoy is not on cooldown for this captive and
            // who can pay a fair price. Since 25/09/2026 the offer comes as an ENVOY, never a letter (fkasad; Gabriel).
            int anchor = BuyPrisonerVerb.RansomAnchor(captive);
            Hero buyer = null;
            var buyerRole = PrisonerBuyerRole.House;
            foreach ((Hero prospect, PrisonerBuyerRole role) in ResolvePrisonerBuyers(captive))
            {
               if (PrisonerEnvoyBehavior.Instance?.EnvoyMayCome(captive, prospect.StringId) != true) continue;
               if (prospect.Gold < PrisonerTradePolicy.MinAcceptablePayment(anchor)) continue; // cannot pay a fair price

               buyer = prospect;
               buyerRole = role;

               break;
            }

            if (buyer == null) continue;

            // A buyer needs a profile to carry cadence + memory; create one if the player has never met them
            // (mirrors CourierService's leak antagonist), so any kin/liege can make the offer.
            NpcProfile buyerProfile = store.Get(buyer.StringId);
            if (buyerProfile == null)
            {
               buyerProfile = HeroProfileMapper.FromHero(buyer);
               store.Set(buyerProfile);
            }

            if (!CanSendLetter(buyerProfile, today, 20)) continue;

            // One outstanding offer per captive from this buyer: do not pester about the same prisoner.
            var alreadyOffered = false;
            foreach (PendingLetter l in buyerProfile.SentLetters)
               if (l.Reason == LetterReason.PrisonerOffer && l.OfferTargetId == captive.StringId)
               {
                  alreadyOffered = true;

                  break;
               }

            // A letter an older save still has pending about this captive is honoured: no envoy on top of it.
            if (alreadyOffered) continue;

            // fkasad (12/10/2026): a lord whose envoy was sent away raises his offer with the next.
            int turnedAway = PrisonerEnvoyBehavior.Instance?.EnvoysTurnedAway(captive) ?? 0;
            int amount = PrisonerOfferPolicy.OfferAmount(anchor, personalStake: true, envoysTurnedAway: turnedAway);

            int baseWeight = LetterWeights.BaseWeight(LetterReason.PrisonerOffer);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = buyerProfile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.PrisonerOffer), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.PrisonerOffer,
               SenderId = buyer.StringId,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = buyerProfile;
            Hero capturedBuyer = buyer;
            string capturedCaptiveId = captive.StringId;
            int capturedAmount = amount;
            Hero capturedCaptive = captive;
            PrisonerBuyerRole capturedRole = buyerRole;
            bool capturedAgain = turnedAway > 0;
            release[candidate] = () =>
            {
               // An envoy, carrying the same fair ceiling the letter named, who can also offer an exchange. Family and
               // realm want the captive back; a bitter enemy wants him in his own hands.
               PrisonerEnvoyBehavior.Instance?.Send(capturedBuyer, capturedCaptive, capturedAmount,
                  PrisonerBuyerPolicy.Stake(capturedRole, capturedBuyer.Name?.ToString(), capturedCaptive.IsFemale),
                  PrisonerBuyerPolicy.WantsCaptiveBack(capturedRole), capturedAgain);

               ModLog.Info($"[LETTER] {capturedProfile.Name} sends an envoy about the player's prisoner {capturedCaptiveId} (up to {capturedAmount} denars).");
            };
         }
      }

      /// <summary>
      ///   A faction ruler who is NOT the player's own liege, and who regards the player highly, writes to poach
      ///   them into their service, dangling a concrete enticement (a castle if they have a fief to spare, a
      ///   company of soldiers if they lead men, else gold). Deliberately RARE: low base weight, a long per-lord
      ///   cooldown, and one offer per lord ever. A light hook: accepting only warms the bond and signals interest
      ///   (see the read window's ExecuteAcceptOffer); it never changes the player's allegiance.
      /// </summary>
      private void CollectServiceOffers(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         Hero player = Hero.MainHero;
         if (player == null) return;

         IFaction playerFaction = player.MapFaction;
         if (playerFaction is Kingdom pk && pk.Leader == player) return; // do not poach a sovereign into vassalage

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;
            if (profile.ReputationWithPlayer < Regard.ConfidingTier) continue; // they must genuinely rate the player to poach them

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true} || !hero.IsLord || hero.IsPrisoner) continue;
            if (IsWithPlayer(hero)) continue;

            // Only a faction RULER (someone who could truly take the player into their service), and never the
            // ruler of the kingdom the player already serves.
            Kingdom kingdom = hero.Clan?.Kingdom;
            if (kingdom == null || kingdom.Leader != hero) continue;
            if (playerFaction != null && hero.MapFaction == playerFaction) continue;

            // Not the realm the player has only just left (see ServiceOfferPolicy.FormerLiegeSilenceDays).
            if (!ServiceOfferPolicy.RulerMayWrite(WorldEventFeeders.DaysSincePlayerLeft(kingdom, today))) continue;

            if (!CanSendLetter(profile, today, 90)) continue; // rare on purpose

            // One service offer per lord ever: never pester.
            var alreadyOffered = false;
            foreach (PendingLetter l in profile.SentLetters)
               if (l.Reason == LetterReason.ServiceOffer)
               {
                  alreadyOffered = true;

                  break;
               }

            if (alreadyOffered) continue;

            bool canOfferCastle = hero.Clan?.Settlements != null &&
                                  hero.Clan.Settlements.Count(s => s != null && (s.IsTown || s.IsCastle)) > 1;
            bool canOfferTroops = hero.PartyBelongedTo != null;
            ServiceEnticement enticement = ServiceOfferPolicy.ChooseEnticement(canOfferCastle, canOfferTroops);

            string context = ServiceOfferPolicy.Premise(kingdom.Name?.ToString(), Clan.PlayerClan?.Kingdom?.Name?.ToString()) +
                             $" The enticement you dangle: {DescribeEnticement(enticement)}.";

            int baseWeight = LetterWeights.BaseWeight(LetterReason.ServiceOffer);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.ServiceOffer), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.ServiceOffer,
               SenderId = id,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = profile;
            Hero capturedHero = hero;
            string capturedContext = context;
            release[candidate] = () =>
            {
               LetterGenerationService.QueueInitialLetter(
                  capturedProfile, capturedHero, LetterReason.ServiceOffer, capturedContext,
                  llmServices, store, today);

               ModLog.Info($"[LETTER] {capturedProfile.Name} offers the player a place in their service.");
            };
         }
      }


      /// <summary>The plain-words enticement a ServiceOffer dangles, fed into the letter's Context.</summary>
      private static string DescribeEnticement(ServiceEnticement enticement)
      {
         switch (enticement)
         {
            case ServiceEnticement.Castle: return "a castle of their own to hold";
            case ServiceEnticement.Troops: return "a company of your own soldiers to command";
            default: return "a handsome purse of gold";
         }
      }

      /// <summary>
      ///   Everyone who might want this captive, in PrisonerBuyerPolicy's order. Until 25/09/2026 only the clan leader,
      ///   else the realm's leader; fkasad asked for the two who were missing: a father whose daughter married into
      ///   another house (her clan leader is now her husband's kin), and an ally who bitterly hates the prisoner. An
      ///   "ally" is a lord of the player's own realm, the only side certain to be at peace with him.
      /// </summary>
      private List<(Hero Hero, PrisonerBuyerRole Role)> ResolvePrisonerBuyers(Hero captive)
      {
         var byId = new Dictionary<string, Hero>();
         var facts = new List<PrisonerBuyerCandidate>();

         void Consider(Hero h, PrisonerBuyerRole role)
         {
            if (h?.StringId == null) return;
            byId[h.StringId] = h;
            facts.Add(new PrisonerBuyerCandidate {
               HeroId = h.StringId, Role = role, CanTreat = IsEligibleBuyer(h, captive), RelationToCaptive = h.GetRelation(captive),
               IsOfPlayersClan = h.Clan != null && h.Clan == Clan.PlayerClan
            });
         }

         if (captive == null) return new List<(Hero, PrisonerBuyerRole)>();

         Consider(captive.Clan?.Leader, PrisonerBuyerRole.House);

         // A parent of ANOTHER house: within the captive's own house the house head already speaks for the family.
         foreach (Hero parent in new[] {captive.Father, captive.Mother})
            if (parent != null && parent.Clan != captive.Clan)
               Consider(parent, PrisonerBuyerRole.Parent);

         Consider(captive.MapFaction?.Leader, PrisonerBuyerRole.Realm);

         IFaction playerRealm = Hero.MainHero?.MapFaction;
         if (playerRealm != null && playerRealm != captive.MapFaction)
            foreach (Hero lord in Hero.AllAliveHeroes)
               if (lord.IsLord && lord.MapFaction == playerRealm && lord != captive && lord.GetRelation(captive) <= VanillaRelation.BitterTier)
                  Consider(lord, PrisonerBuyerRole.Enemy);

         return PrisonerBuyerPolicy.Order(facts).Select(c => (byId[c.HeroId], c.Role)).ToList();
      }

      /// <summary>A buyer must be a living free lord, not the captive or the player, and not currently with the player.</summary>
      private bool IsEligibleBuyer(Hero buyer, Hero captive)
         => buyer is {IsAlive: true} && buyer.IsLord && !buyer.IsPrisoner
            && buyer != captive && buyer != Hero.MainHero && !IsWithPlayer(buyer);

      /// <summary>
      ///   The player's own vanilla spouse AND every committed consort each become their own candidate when
      ///   eligible (not just the first found, as the old evaluator returned): a spouse and a consort may now
      ///   compete in the SAME tick, with the mailbox scheduler (not evaluator order) deciding which one
      ///   actually reaches the player.
      /// </summary>
      private void CollectSpouseCorrespondence(
         BannerlordNpcMemoryStore store, LlmServices llmServices, int today,
         List<LetterCandidate> pool, Dictionary<LetterCandidate, Action> release)
      {
         // Native (legal) spouse.
         Hero spouse = Hero.MainHero?.Spouse;
         if (spouse is {IsAlive: true} && !IsWithPlayer(spouse))
         {
            NpcProfile profile = store.Get(spouse.StringId);
            if (profile != null && CanSendLetter(profile, today, 7) && DaysSinceLastEvent(profile, today) >= 5
                && PassesAbsenceDistanceRoll(spouse))
            {
               int daysSince = DaysSinceLastEvent(profile, today);
               var ctx = $"Player's spouse. Has not seen the player in {daysSince} days." + $" Your feeling toward the player right now: {ThirdPartyDescriptor.DescribeRelation(profile.ReputationWithPlayer)}. Write in a tone true to that feeling: a warm marriage writes with warmth, never generic reproach.";

               int baseWeight = LetterWeights.BaseWeight(LetterReason.SpouseCorrespondence);
               int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.SpouseCorrespondence), GlobalInterest = GlobalInterest});

               var candidate = new LetterCandidate {
                  Reason = LetterReason.SpouseCorrespondence,
                  SenderId = spouse.StringId,
                  Weight = weight,
                  EligibleSinceDay = today
               };
               pool.Add(candidate);

               NpcProfile capturedProfile = profile;
               Hero capturedSpouse = spouse;
               release[candidate] = () =>
                  LetterGenerationService.QueueInitialLetter(
                     capturedProfile, capturedSpouse, LetterReason.SpouseCorrespondence, ctx,
                     llmServices, store, today);
            }
         }

         // Consorts: committed partners recognised by the mod but not by the game engine.
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.Romantic?.IsConsort != true) continue;
            if (profile.Romantic.Status != RomanticStatus.Committed) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true}) continue;
            if (IsWithPlayer(hero)) continue;
            if (!CanSendLetter(profile, today, 10)) continue;
            if (DaysSinceLastEvent(profile, today) < 7) continue;
            if (!PassesAbsenceDistanceRoll(hero)) continue;

            var context = $"Player's consort. Has not seen the player in {DaysSinceLastEvent(profile, today)} days.";

            int baseWeight = LetterWeights.BaseWeight(LetterReason.SpouseCorrespondence);
            int weight = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = baseWeight, SenderInterest = profile.CorrespondenceInterest, SubjectInterest = GetSubjectInterest(LetterReason.SpouseCorrespondence), GlobalInterest = GlobalInterest});

            var candidate = new LetterCandidate {
               Reason = LetterReason.SpouseCorrespondence,
               SenderId = id,
               Weight = weight,
               EligibleSinceDay = today
            };
            pool.Add(candidate);

            NpcProfile capturedProfile = profile;
            Hero capturedHero = hero;
            release[candidate] = () =>
               LetterGenerationService.QueueInitialLetter(
                  capturedProfile, capturedHero, LetterReason.SpouseCorrespondence, context,
                  llmServices, store, today);
         }
      }

      // ── B1: correspondence interest (persisted; B2 wires this into the mailbox's weighting) ────

      /// <summary>The current damper for one subject (a <see cref="LetterReason" />), or 0 (neutral) if never nudged.</summary>
      public int GetSubjectInterest(LetterReason reason)
         => _subjectInterest.TryGetValue((int) reason, out int v) ? v : 0;

      /// <summary>Moves one subject's damper by <paramref name="delta" />, clamped via <see cref="CorrespondenceInterestPolicy.ClampSubject" />.</summary>
      public void NudgeSubject(LetterReason reason, int delta)
      {
         int key = (int) reason;
         int current = _subjectInterest.TryGetValue(key, out int v) ? v : 0;
         _subjectInterest[key] = CorrespondenceInterestPolicy.ClampSubject(current + delta);
      }

      /// <summary>The player's overall correspondence damper (0 = neutral, never a bonus).</summary>
      public int GlobalInterest => _globalInterest;

      /// <summary>Moves the global damper by <paramref name="delta" />, clamped via <see cref="CorrespondenceInterestPolicy.ClampGlobal" />.</summary>
      public void NudgeGlobal(int delta)
         => _globalInterest = CorrespondenceInterestPolicy.ClampGlobal(_globalInterest + delta);

      /// <summary>
      ///   Persists the subject/global damper state as a UTF-8 JSON byte[] blob, mirroring
      ///   <see cref="Storage.BannerlordNpcMemoryStore.SyncData" />'s byte[] pattern: additive and save-safe, so
      ///   a save written before this feature has no such key, and a garbled or absent blob degrades to the
      ///   empty defaults rather than throwing out of a save or load. Never resets on session launch (SyncData
      ///   runs before OnSessionLaunched), so loaded state is never clobbered.
      /// </summary>
      public void SyncData(IDataStore dataStore)
      {
         try
         {
            if (dataStore.IsSaving)
            {
               string json;
               try
               {
                  json = JsonConvert.SerializeObject(new LetterPacingState {
                     SubjectInterest = _subjectInterest,
                     GlobalInterest = _globalInterest
                  });
               }
               catch (Exception ex)
               {
                  ModLog.Error($"[LETTER PACING] serialize failed, persisting empty state this save. {ex.GetType().Name}: {ex.Message}");
                  json = "{}";
               }

               byte[] bytes = Encoding.UTF8.GetBytes(json);
               try { dataStore.SyncData(LetterPacingSaveKey, ref bytes); }
               catch (Exception ex)
               {
                  ModLog.Error($"[LETTER PACING] byte[] save failed: {ex.GetType().Name}: {ex.Message}");
               }
            }
            else
            {
               byte[] bytes = null;
               try { dataStore.SyncData(LetterPacingSaveKey, ref bytes); }
               catch
               {
                  // key absent on a save written before this feature: fall through to the empty defaults
               }

               if (bytes is {Length: > 0})
                  try
                  {
                     string json = Encoding.UTF8.GetString(bytes);
                     LetterPacingState state = JsonConvert.DeserializeObject<LetterPacingState>(json);
                     if (state != null)
                     {
                        _subjectInterest = state.SubjectInterest ?? new Dictionary<int, int>();
                        _globalInterest = state.GlobalInterest;
                     }
                  }
                  catch (Exception ex)
                  {
                     ModLog.Error($"[LETTER PACING] load failed; keeping empty defaults. {ex.GetType().Name}: {ex.Message}");
                  }

               ModLog.Info($"[LETTER PACING] loaded ({_subjectInterest.Count} subject entr{(_subjectInterest.Count == 1 ? "y" : "ies")}, global {_globalInterest}).");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LETTER PACING] SyncData failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The wire shape for <see cref="SyncData" />: kept trivial so the JSON round-trips without fuss.</summary>
      private sealed class LetterPacingState
      {
         public Dictionary<int, int> SubjectInterest { get; set; }
         public int GlobalInterest { get; set; }
      }

      /// <summary>
      ///   2.5 (courier audit): writes each committed-but-unwritten <see cref="ScheduledLetter" /> on its due day
      ///   with a bounded retry (<see cref="ScheduledLetterPolicy" />), so a transient LLM failure never loses the
      ///   pension and its quest. The intent lives on the sender's persisted profile, so it survives reloads; it is
      ///   dropped once the letter is confirmed written, once the sender dies, or after too many failed attempts.
      /// </summary>
      private void ProcessScheduledLetters(BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
      {
         if (store == null || llmServices?.ChatService == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.ScheduledLetters == null || profile.ScheduledLetters.Count == 0) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);
            if (hero == null) continue; // transient: the profile is here but the hero is not resolvable this tick.

            var toRemove = new List<ScheduledLetter>();
            var changed = false;

            foreach (ScheduledLetter scheduled in profile.ScheduledLetters)
            {
               // A committed letter dies with its author, exactly as an in-flight one does.
               if (!hero.IsAlive)
               {
                  toRemove.Add(scheduled);
                  changed = true;

                  continue;
               }

               // fulfilled: a real letter of this reason, generated on or after the due day, now exists (an earlier
               // attempt succeeded). in-flight: a blank placeholder of that reason is mid-generation right now.
               var fulfilled = false;
               var inFlight = false;
               foreach (PendingLetter l in profile.SentLetters)
               {
                  if (l.Reason != scheduled.Reason) continue;
                  if (!string.IsNullOrEmpty(l.Content)) { if (l.SentOnDay >= scheduled.SendDay) fulfilled = true; }
                  else if (!l.IsLost) inFlight = true;
               }

               switch (ScheduledLetterPolicy.Decide(new ScheduledLetterPolicy.ScheduledLetterFacts {SendDay = scheduled.SendDay, Today = today, Attempts = scheduled.Attempts, Fulfilled = fulfilled, GenerationInFlight = inFlight}))
               {
                  case ScheduledLetterAction.Fulfilled:
                     toRemove.Add(scheduled);
                     changed = true;

                     break;

                  case ScheduledLetterAction.GiveUp:
                     toRemove.Add(scheduled);
                     changed = true;
                     ModLog.Info($"[LETTER] scheduled {scheduled.Reason} for {profile.Name} abandoned after {scheduled.Attempts} attempt(s).");

                     break;

                  case ScheduledLetterAction.Generate:
                     scheduled.Attempts++;
                     changed = true;
                     LetterGenerationService.QueueInitialLetter(
                        profile, hero, scheduled.Reason, scheduled.Context ?? "", llmServices, store, today);
                     ModLog.Info($"[LETTER] writing scheduled {scheduled.Reason} for {profile.Name} (attempt {scheduled.Attempts}, due day {scheduled.SendDay}).");

                     break;

                  case ScheduledLetterAction.Wait:
                  default:
                     break;
               }
            }

            foreach (ScheduledLetter r in toRemove) profile.ScheduledLetters.Remove(r);
            if (changed) store.Set(profile);
         }
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
      // CalradiaRemembers.Logic.Letters.LetterCadence; these thin wrappers keep the call sites unchanged.

      /// <summary>True when the NPC has no letter in transit and respects the cooldown.</summary>
      private static bool CanSendLetter(NpcProfile profile, int today, int cooldownDays)
      {
         // A correspondent who is PRESENT with the player, not apart, does not send a courier letter: it is
         // nonsensical to courier someone at your side, and it reads as "you are never here" while they stand
         // beside you. Player-reported: a governor spouse in the same castle lamented an absent father the
         // instant the player stepped out for grain. Proximity decides this, not a stale "days since we talked".
         Hero hero = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
         if (hero != null && IsSenderPresentWithPlayer(hero)) return false;

         // M15 (courier audit): a prisoner cannot hire a courier. A lover held in an enemy dungeon must not keep
         // mailing the player. This is the shared cadence gate for every unprompted letter type, so the guard sits
         // here once rather than in each trigger.
         if (hero is {IsPrisoner: true}) return false;

         // The one initiative gate (30/09/2026): of age, alive, free, not the player. Several triggers trust IsLord,
         // which is a rank and not an age. A profile with no hero resolved keeps the cadence rule alone, as before.
         if (hero != null && !Mapping.ActionGate.Allows(hero, Logic.ActionKind.Letter)) return false;

         return LetterCadence.CanSendLetter(profile, today, cooldownDays);
      }

      /// <summary>
      ///   True when the sender is effectively at the player's side: in the player's own party, in the same
      ///   settlement, or near enough on the map that they are together (the player has merely stepped outside
      ///   the walls). A companion away on an errand has their own distant party, so they still write.
      /// </summary>
      private static bool IsSenderPresentWithPlayer(Hero sender)
      {
         if (sender == null) return false;
         MobileParty main = MobileParty.MainParty;
         if (main == null) return false;
         if (sender.PartyBelongedTo == main) return true;

         Settlement senderAt = sender.CurrentSettlement ?? sender.StayingInSettlement;
         Settlement playerAt = Hero.MainHero?.CurrentSettlement ?? Hero.MainHero?.StayingInSettlement;

         return senderAt != null && senderAt == playerAt;
      }

      /// <summary>
      ///   A graduated distance roll for a spouse or consort absence letter: none while the player is near, a
      ///   rising chance the farther the courier's journey would be, capped so it is never systematic (pure
      ///   <see cref="LetterDistancePolicy" />, reusing the same travel-days a letter's delivery uses). An
      ///   unreadable position does not lament an absence it cannot measure.
      /// </summary>
      private static bool PassesAbsenceDistanceRoll(Hero sender)
      {
         float days = LetterGenerationService.TravelDaysTo(sender);
         if (days < 0f) return false;

         int chance = LetterDistancePolicy.AbsenceLetterChancePercent(days);

         return chance > 0 && MBRandom.RandomInt(100) < chance;
      }

      /// <summary>Days since the last notable event in this NPC's profile (proxy for last meeting).</summary>
      private static int DaysSinceLastEvent(NpcProfile profile, int today)
         => LetterCadence.DaysSinceLastEvent(profile, today);

      // ── Helpers ───────────────────────────────────────────────────────────

      /// <summary>
      ///   True when the NPC is effectively beside the player right now: same party, a party the player
      ///   leads, the same ARMY, or the same town/castle (player report: a lady in the player's own army
      ///   kept "writing from afar"). Such an NPC talks, they do not post letters. Delegates to the letter
      ///   system's single authoritative <see cref="HeroProximity.IsNearPlayer" />.
      /// </summary>
      private static bool IsWithPlayer(Hero hero) => HeroProximity.IsNearPlayer(hero);

      /// <summary>
      ///   B2: tournament congratulations no longer fire immediately: each eligible NPC (same guards, same
      ///   cap of <see cref="MaxLettersPerEvent" />) ENQUEUES a <see cref="LetterCandidateIntent" /> instead,
      ///   for the mailbox scheduler to weigh against everything else pending on its next daily tick.
      /// </summary>
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
            if (IsWithPlayer(hero)) continue; // travelling companion, no letter needed

            // Friendly NPCs congratulate; rivals or hostiles might challenge.
            if (profile.ReputationWithPlayer < Regard.FriendlyTier) continue;

            profile.LetterCandidates.Add(new LetterCandidateIntent {
               Reason = LetterReason.TournamentVictory,
               Context = context,
               EligibleSinceDay = today,
               BaseWeightOverride = 0 // derive from LetterWeights.BaseWeight(TournamentVictory)
            });
            behavior.Store.Set(profile);
            queued++;
         }
      }

      /// <summary>
      ///   B2: battle congratulations no longer fire immediately: the single eligible NPC (same guards, same
      ///   cap of 1 per battle) ENQUEUES a <see cref="LetterCandidateIntent" /> carrying the underdog-scaled
      ///   <paramref name="weightOverride" /> computed once at the moment the battle ended
      ///   (<see cref="LetterWeights.BattleVictoryWeight" />), so the mailbox scheduler weighs it correctly
      ///   whenever it is eventually released, however many days later that turns out to be.
      /// </summary>
      private static void NpcCongratulatePlayerOnBattleVictory(NpcMemoryCampaignBehavior behavior, int today, string context, int weightOverride)
      {
         var queued = 0;
         foreach (string id in behavior.Store.ListIds())
         {
            if (queued >= 1) break; // cap at 1 enqueued candidate per battle

            NpcProfile profile = behavior.Store.Get(id);

            if (profile == null) continue;
            if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero is not {IsAlive: true}) continue;
            if (IsWithPlayer(hero)) continue; // travelling companion, no letter needed

            // Ally of the player (same faction or high personal regard) celebrates.
            bool sameOrAllied = hero.MapFaction != null && hero.MapFaction == Hero.MainHero?.MapFaction;

            if (!sameOrAllied || profile.ReputationWithPlayer < Regard.WarmTier) continue;

            profile.LetterCandidates.Add(new LetterCandidateIntent {
               Reason = LetterReason.BattleVictory,
               Context = context,
               EligibleSinceDay = today,
               BaseWeightOverride = weightOverride
            });
            behavior.Store.Set(profile);
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
         bool motherWithPlayer = IsWithPlayer(mother);

         string childDesc = playerChildren.Count == 1
            ? $"a {(playerChildren[0].IsFemale ? "daughter" : "son")} named {playerChildren[0].Name}"
            : $"{playerChildren.Count} children";
         var context = $"{(isLegitimate ? "Legitimate" : "Illegitimate")} birth. She has given birth to {childDesc} on day {today}.";

         // Birth announcement: a fond note from afar, never sent to a mother riding beside the player (the game
         // already announces the birth). B2: ENQUEUED for the mailbox scheduler rather than sent immediately.
         if (BirthLetterPolicy.ShouldSendAnnouncement(motherWithPlayer))
         {
            profile.LetterCandidates.Add(new LetterCandidateIntent {
               Reason = LetterReason.BirthAnnouncement,
               Context = context,
               EligibleSinceDay = today,
               BaseWeightOverride = 0 // derive from LetterWeights.BaseWeight(BirthAnnouncement)
            });
            behavior.Store.Set(profile);
         }

         // Child support request: due a week later, quest opens on delivery. The player's own spouse never duns
         // him for their shared child, and a mother travelling with him writes nothing (player report 2026-07-10).
         // 2.5 (courier audit): SCHEDULE the intent rather than generate it now with a future-dated stamp. The
         // content is written on the due day with bounded retry (ProcessScheduledLetters), so an LLM outage at the
         // birth no longer loses the pension and its quest forever, and SentOnDay is never a future day. This is a
         // GUARANTEED obligation (LetterMailboxPolicy.BypassesBudget), never a mailbox candidate.
         if (BirthLetterPolicy.ShouldRequestChildSupport(motherWithPlayer, isLegitimate))
         {
            string supportContext = context + $" She is now asking the player for {ChildSupportAmount} denars to help raise the child.";
            profile.ScheduledLetters.Add(new ScheduledLetter {
               Reason  = LetterReason.ChildSupportRequest,
               SendDay = today + ChildSupportDelayDays,
               Context = supportContext,
               Attempts = 0
            });
            behavior.Store.Set(profile);
         }

         ModLog.Info($"[LETTER] birth letters evaluated: {mother.Name} → player ({childDesc}, {(isLegitimate ? "legitimate" : "illegitimate")}, withPlayer={motherWithPlayer}).");
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

         // B2: scale the letter's weight by how outmatched the player was. Player WON, so the enemy is the
         // LOSING side and the player's own side (attacker or defender, whichever it was) is the winning one.
         float SideStrength(BattleSideEnum side)
            => (mapEvent.GetMapEventSide(side)?.Parties)?.Sum(mp => mp.Party?.EstimatedStrength ?? 0f) ?? 0f;

         float playerStrength = SideStrength(playerSide);
         float enemyStrength = SideStrength(losingSide);
         float ratio = playerStrength <= 0f ? 1f : enemyStrength / playerStrength; // guard divide-by-zero

         int weight = LetterWeights.BattleVictoryWeight(ratio);

         if (ratio >= 1.5f)
            context += $" The player won against heavy odds (roughly {ratio:0.#} to 1).";

         NpcCongratulatePlayerOnBattleVictory(behavior, today, context, weight);
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
         RecordTournamentVictoryChronicle(today, town);
      }

      /// <summary>Tournament victories are naturally rare per town, so no further anti-spam guard is needed.</summary>
      internal static void RecordTournamentVictoryChronicle(int today, Town town) // internal: cr.test tournament_audience
      {
         try
         {
            string townName = town?.Name?.ToString() ?? "the arena";
            // chmidt (23/10/2026): with no place and no realm the victory weighed 40 against an awareness threshold of 50,
            // so no one could hear of it. The town and its realm now carry it (realm +25, the country around by nearness).
            Settlement place = town?.Settlement;
            WorldEventStore.Instance?.Record(new WorldEvent {
               Id = place == null ? null : $"cr_tourney_won_{place.StringId}_{today}",
               Type = WorldEventType.PlayerFeat,
               Day = today,
               Magnitude = 40,
               InvolvesPlayer = true,
               PlaceId = place?.StringId,
               FactionIds = place?.MapFaction == null ? new List<string>() : new List<string> {place.MapFaction.StringId},
               Summary = $"{Hero.MainHero.Name} won the tournament at {townName}.",
               ActorIds = new List<string> {Hero.MainHero.StringId},
               ChronicleLine = $"{Hero.MainHero.Name} took the prize of the tourney at {townName}, and the crowd hailed the victor"
            });
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] tournament chronicle feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}
