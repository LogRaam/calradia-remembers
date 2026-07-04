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

      private const string SaveKey = "CalradiaRemembers_SpouseDivorceDemand_v1";

      public static SpouseDivorceDemandBehavior Instance;

      // spouseId -> the day their demand began.
      private Dictionary<string, int> _demandStartDay = new();

      public SpouseDivorceDemandBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>The demand is accepted (accept_divorce executed successfully): clears the flag.</summary>
      public void Accept(string spouseId)
      {
         if (!string.IsNullOrEmpty(spouseId)) _demandStartDay.Remove(spouseId);
      }

      /// <summary>The day <paramref name="spouseId" />'s demand began, or null when they are not currently demanding.</summary>
      public int? DemandStartDay(string spouseId)
         => !string.IsNullOrEmpty(spouseId) && _demandStartDay.TryGetValue(spouseId, out int day)
            ? day
            : (int?) null;

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

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>
      ///   Registers a refusal: the spouse's resentment deepens (a <see cref="GrudgeSource.Divorce" />
      ///   grudge, known to the player since it was voiced or felt in the moment) and the demand flag is
      ///   cleared so it can resurface fresh at a later evaluation, rather than sitting stuck forever.
      /// </summary>
      public void RegisterRefusal(Hero spouse, int today)
      {
         if (spouse == null || Hero.MainHero == null) return;

         _demandStartDay.Remove(spouse.StringId);

         GrudgeBehavior.Instance?.TryPlant(spouse, Hero.MainHero, GrudgeSource.Divorce,
            "Begged the player to end our marriage, and was refused.", RefusalGrudgeMagnitude, true, today);

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s divorce demand was refused — resentment deepens.");
      }

      /// <summary>Runs one weekly evaluation immediately (console / self-test), skipping the wait for the real weekly tick.</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_demandStartDay, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SPOUSE_DIVORCE] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SPOUSE_DIVORCE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               // Key absent in older saves — the whole point of the additive-key save-safety rule.
            }

            if (bytes is {Length: > 0})
               try { _demandStartDay = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>(); }
               catch (Exception ex)
               {
                  _demandStartDay = new Dictionary<string, int>();
                  ModLog.Error($"[SPOUSE_DIVORCE] load failed — flag(s) reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _demandStartDay = new Dictionary<string, int>();

            ModLog.Info($"[SPOUSE_DIVORCE] loaded {_demandStartDay.Count} outstanding demand(s) from save.");
         }
      }

      #region private

      /// <summary>Records a fresh demand and queues the announcing letter, in the spouse's own voice.</summary>
      private void Begin(Hero spouse, int today)
      {
         _demandStartDay[spouse.StringId] = today;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(spouse.StringId);

         if (behavior?.LlmServices == null || profile == null)
         {
            ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s demand begins (no profile/LLM available — no letter queued).");

            return;
         }

         const string context = "You have grown truly, deeply unhappy in this marriage and have resolved that it must " +
                                 "end. You are writing to announce this before you press the matter in person.";

         LetterGenerationService.QueueInitialLetter(
            profile, spouse, LetterReason.SpouseDivorceDemand, context, behavior.LlmServices, behavior.Store, today);

         ModLog.Info($"[SPOUSE_DIVORCE] {spouse.Name}'s demand begins — announcing letter queued.");
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

         return new SpouseDivorceDemandFacts {
            SpouseRelationWithPlayer = player != null
               ? spouse.GetRelation(player)
               : 0,
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

         SpouseDivorceDemandFacts facts = BuildFacts(spouse);

         if (SpouseDivorceDemandPolicy.ShouldDemand(facts)) Begin(spouse, Today);
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
               _demandStartDay.Remove(spouseId); // stale — no longer a live demand against the player

               continue;
            }

            if (today - _demandStartDay[spouseId] < GracePeriodDays) continue;

            Hero timedOutSpouse = spouse;
            SafeRun.Step($"SpouseDivorceDemandBehavior.SweepTimeouts({spouseId})",
               () => RegisterRefusal(timedOutSpouse, today));
         }
      }

      #endregion
   }
}
