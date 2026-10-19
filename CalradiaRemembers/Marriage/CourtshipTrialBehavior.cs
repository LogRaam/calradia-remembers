// Code written by Gabriel Mailhot, 11/08/2026.
// COURTSHIP TRIAL (author's story): a prospect agrees to wed the player BUT first sets a condition: she rides at
// his side for a time, as a party member and not yet a wife, to take his measure. This behavior is the persisted
// bridge between the ACCOMPANY mechanics the mod already ships (follow_me escorts, ride_with_me retainers) and the
// marriage gate: it records the day a valid, not-hard-blocked marriage prospect BEGINS accompanying the player,
// lapses the trial cleanly if they stop before the term is served, and, once the term IS served, latches a
// "trial passed" flag AND has the prospect PROACTIVELY reach out (a MarriageProposal letter in her own voice) to
// accept the match. The flag then opens the offer through EncounterContextBuilder's marriage gate by clearing the
// one SOFT block (RegardTooLow); a hard blocker (war, captivity, already wed, same clan, unlawful pairing) keeps
// it shut, enforced by the ordered resolver plus the pure MarriageBlockOverridePolicy.
//
// Persistence mirrors FollowMeBehavior/RetainerBehavior: an additive, save-safe byte[] JSON key, idempotent load,
// graceful on missing/corrupt, nothing else touched. The engine is never read in the ctor (only Instance is set);
// all engine reads happen on the daily tick behind SafeRun.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Escort;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Retainer;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>Tracks courtship trials (a marriage prospect riding with the player to be tested), and opens the match when one is served.</summary>
   public sealed class CourtshipTrialBehavior : CampaignBehaviorBase
   {
      // Her own voice, not a parent's: the letter path (LetterReason.MarriageProposal) is reused, and its prompt
      // occasion now defers WHO speaks to this Context, so the generated letter reads as the prospect herself
      // coming to accept, after the seasons at his side settled her. Neutral on the sex of both parties.
      private const string TrialAcceptanceLetterContext =
         "You are the one the player has been courting. You agreed to wed them, but first you set a condition: " +
         "that you be allowed to ride at their side for a season or two, sharing the road and the danger, so you " +
         "might take their true measure before binding your life to theirs. That time is now served, and it has " +
         "settled your heart: you have made up your mind, and you will marry them. Write in your OWN voice, as " +
         "the one who set the trial and has now passed judgement on it, NOT as a parent arranging a match for " +
         "someone else. Say plainly that the testing is over and your answer is yes, and let the warmth or the " +
         "quiet certainty of your own nature carry the letter.";

      private const string SaveKey = "CalradiaRemembers_CourtshipTrials_v1bytes";

      public static CourtshipTrialBehavior Instance;

      // Keyed by the prospect's hero StringId. A record's presence means a trial is in progress or has been passed.
      private Dictionary<string, CourtshipTrialRecord> _trials = new Dictionary<string, CourtshipTrialRecord>();

      public CourtshipTrialBehavior()
      {
         Instance = this;
      }

      /// <summary>True when this prospect (by StringId) has SERVED a courtship trial: the match may now open (soft blocks only).</summary>
      public bool IsTrialPassed(string heroId)
         => !string.IsNullOrEmpty(heroId) && _trials.TryGetValue(heroId, out CourtshipTrialRecord r) && r.Passed;

      /// <summary>A multi-line status of every tracked trial: prospect, start day, and whether it has been served.</summary>
      public string DescribeStatus()
      {
         if (_trials.Count == 0) return "No courtship trial is currently tracked.";

         var sb = new StringBuilder();
         sb.AppendLine($"{_trials.Count} tracked courtship trial(s) (term {CourtshipTrialPolicy.TrialDays} days):");
         foreach (KeyValuePair<string, CourtshipTrialRecord> kv in _trials)
         {
            Hero hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == kv.Key);
            sb.AppendLine($"  - {hero?.Name?.ToString() ?? kv.Key}: startDay={kv.Value.StartDay}, passed={kv.Value.Passed}, " +
                          $"accompanyingNow={IsAccompanying(kv.Key)}.");
         }

         return sb.ToString();
      }

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_trials, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[COURTSHIP] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[COURTSHIP] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               // Key absent in a save written before this behavior existed: brand new, additive key, so it
               // degrades to no tracked trials, never a crash.
            }

            if (bytes is {Length: > 0})
               try { _trials = JsonConvert.DeserializeObject<Dictionary<string, CourtshipTrialRecord>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, CourtshipTrialRecord>(); }
               catch (Exception ex)
               {
                  _trials = new Dictionary<string, CourtshipTrialRecord>();
                  ModLog.Error($"[COURTSHIP] load failed - dropping any tracked trials so the campaign opens. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _trials = new Dictionary<string, CourtshipTrialRecord>();

            ModLog.Info($"[COURTSHIP] loaded {_trials.Count} tracked courtship trial(s) from save.");
         }
      }

      // ── Debug seams (self-test / console) ─────────────────────────────────

      /// <summary>
      ///   Debug seam: run ONE prospect through the same evaluation the daily tick uses, with the accompaniment
      ///   fact and the day supplied by the caller. Lets the self-test drive a full trial (start, then a second
      ///   call TrialDays later to complete) without staging a real 42-day accompaniment. Returns true when this
      ///   call is the one that PASSED the trial.
      /// </summary>
      public bool DebugEvaluate(Hero prospect, NpcProfile profile, bool accompanying, int today)
         => EvaluateProspect(prospect, profile, accompanying, today);

      /// <summary>Debug seam: force the passed flag for a pairing (self-test hard-blocker proof), creating a record if needed.</summary>
      public void DebugSetPassed(string heroId, bool passed)
      {
         if (string.IsNullOrEmpty(heroId)) return;
         if (!_trials.TryGetValue(heroId, out CourtshipTrialRecord r))
         {
            r = new CourtshipTrialRecord {StartDay = (int) CampaignTime.Now.ToDays};
            _trials[heroId] = r;
         }

         r.Passed = passed;
      }

      /// <summary>Debug seam: forget any trial record for this pairing (self-test cleanup). Returns true when one was present.</summary>
      public bool DebugForget(string heroId)
         => !string.IsNullOrEmpty(heroId) && _trials.Remove(heroId);

      #region private

      private static bool IsAccompanying(string heroId)
         => !string.IsNullOrEmpty(heroId) &&
            ((FollowMeBehavior.Instance?.IsEscorting(heroId) ?? false) ||
             (RetainerBehavior.Instance?.IsRetainer(heroId) ?? false));

      private void OnDailyTick()
      {
         SafeRun.Step("CourtshipTrialBehavior.RunDailyEvaluation", RunDailyEvaluation);
      }

      /// <summary>
      ///   The daily sweep. Evaluates every hero who is either currently accompanying the player (an escort or a
      ///   retainer) or already has a trial record, so a trial can start, lapse, or complete. Confined to those
      ///   heroes rather than the whole roster: the gate read is not free, and only these can matter.
      /// </summary>
      private void RunDailyEvaluation()
      {
         var candidates = new HashSet<string>(_trials.Keys);
         if (FollowMeBehavior.Instance != null)
            foreach (string id in FollowMeBehavior.Instance.EscortingLordIds)
               candidates.Add(id);
         if (RetainerBehavior.Instance != null)
            foreach (string id in RetainerBehavior.Instance.RetainerIds)
               candidates.Add(id);

         if (candidates.Count == 0) return;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         var today = (int) CampaignTime.Now.ToDays;

         foreach (string id in candidates)
         {
            Hero hero = Hero.FindFirst(h => h != null && h.StringId == id);

            // Truly gone (dead / removed): drop the record so a stale id never lingers. A completed trial on a
            // dead prospect is moot (they can no longer be wed), so this is safe to drop unconditionally.
            if (hero == null)
            {
               if (_trials.Remove(id)) ModLog.Info($"[COURTSHIP] dropped trial for a hero no longer present ({id}).");

               continue;
            }

            NpcProfile profile = store?.Get(hero.StringId);
            if (profile == null) continue; // no profile means the player never courted them; nothing to track yet

            bool accompanying = IsAccompanying(id);
            SafeRun.Step($"CourtshipTrialBehavior.Evaluate({id})", () => EvaluateProspect(hero, profile, accompanying, today));
         }
      }

      /// <summary>
      ///   The shared per-prospect core, driven by both the daily tick and the debug seam. Starts a trial when a
      ///   valid, not-hard-blocked prospect begins accompanying the player; lapses one that is abandoned early or
      ///   has since become hard-blocked; and, once the term is served, latches the passed flag and queues her
      ///   acceptance letter. A trial already passed is left untouched (idempotent). Returns true only on the tick
      ///   that PASSES the trial.
      /// </summary>
      private bool EvaluateProspect(Hero hero, NpcProfile profile, bool accompanying, int today)
      {
         if (hero == null) return false;
         string id = hero.StringId;

         MarriageBlockReason reason = EncounterContextBuilder.ResolveMarriageBlockReason(hero, profile);
         bool eligibleProspect = !MarriageBlockOverridePolicy.IsHardBlock(reason); // marriageable, no hard blocker

         bool tracked = _trials.TryGetValue(id, out CourtshipTrialRecord record);

         // A passed trial latches: keep the flag whatever they do afterwards (she has already reached out).
         if (tracked && record.Passed) return false;

         // Not (or no longer) a valid, accompanying prospect: lapse any in-progress trial so no stale start lingers.
         if (!accompanying || !eligibleProspect)
         {
            if (tracked)
            {
               _trials.Remove(id);
               ModLog.Info($"[COURTSHIP] trial lapsed for {hero.Name} (accompanying={accompanying}, eligible={eligibleProspect}).");
            }

            return false;
         }

         // Begins accompanying: record the start day for this pairing.
         if (!tracked)
         {
            _trials[id] = new CourtshipTrialRecord {StartDay = today, Passed = false};
            ModLog.Info($"[COURTSHIP] trial begun for {hero.Name} on day {today} (term {CourtshipTrialPolicy.TrialDays} days).");

            return false;
         }

         int daysAccompanied = today - record.StartDay;
         if (!CourtshipTrialPolicy.IsComplete(accompanying, daysAccompanied, CourtshipTrialPolicy.TrialDays, eligibleProspect))
            return false;

         record.Passed = true;
         ModLog.Info($"[COURTSHIP] trial PASSED for {hero.Name} after {daysAccompanied} days; she reaches out to accept.");
         QueueAcceptanceLetter(hero, profile, today);

         return true;
      }

      /// <summary>
      ///   Queues a MarriageProposal letter FROM the prospect TO the player, in HER voice (the trial-acceptance
      ///   context), reusing the ordinary letter path. Belt-and-suspenders one-letter guard on top of the passed
      ///   latch, so a marriage letter already in flight is never doubled.
      /// </summary>
      private static void QueueAcceptanceLetter(Hero hero, NpcProfile profile, int today)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.LlmServices == null || behavior.Store == null || profile?.SentLetters == null) return;

         if (profile.SentLetters.Any(l => l.Reason == LetterReason.MarriageProposal)) return;

         LlmServices llm = behavior.LlmServices;
         LetterGenerationService.QueueInitialLetter(
            profile, hero, LetterReason.MarriageProposal, TrialAcceptanceLetterContext, llm, behavior.Store, today);
         ModLog.Info($"[COURTSHIP] queued {hero.Name}'s marriage-acceptance letter to the player.");
      }

      #endregion
   }
}
