// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 2 (engine): tracks each married NPC whose marriage is fraying because of an
// ongoing affair with the player, and paces that rift toward a divorce. Player-experience-first: it never
// simulates NPC-NPC affairs across Calradia, it only watches the NPCs the PLAYER has actually seduced (their
// stored profile carries RomanticStatus.SecretLover, set by the SDK on a married-NPC intimacy). Mirrors
// DivorceProcessBehavior's shape (daily tick, additive byte[] save key, degrade-to-empty load). The actual
// divorce and the jilted spouse's grudge are Increment 3, which reads the ReadyToDivorce phase this exposes.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Watches the player's married affair partners and advances a per-NPC estrangement (see
   ///   <see cref="EstrangementProgressPolicy" />). Begins one when a seducible, third-party-married NPC becomes
   ///   the player's secret lover; prunes it when the affair ends, the NPC dies, or the marriage is already gone.
   ///   Exposes the current phase so the prompt can voice a fraying marriage and Increment 3 can act on a spouse
   ///   ready to leave. Gated by the MCM EnableStolenLove toggle.
   /// </summary>
   public sealed class NpcMarriageEstrangementBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_StolenLoveEstrangement_v1bytes";
      public static NpcMarriageEstrangementBehavior Instance;

      private List<StolenLoveEstrangement> _active = new();

      public NpcMarriageEstrangementBehavior() => Instance = this;

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
         => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

      /// <summary>The estrangement phase for this NPC, or <see cref="EstrangementPhase.None" /> when none is tracked for them.</summary>
      public EstrangementPhase PhaseFor(string npcId)
      {
         StolenLoveEstrangement e = Find(npcId);

         return e == null
            ? EstrangementPhase.None
            : EstrangementProgressPolicy.Phase(Today - e.StartDay, e.Difficulty);
      }

      /// <summary>A 0..100 sense of how far this NPC's marriage has come apart, for the fraying note. 0 when none is tracked.</summary>
      public int ProgressFor(string npcId)
      {
         StolenLoveEstrangement e = Find(npcId);

         return e == null ? 0 : EstrangementProgressPolicy.ProgressPercent(Today - e.StartDay, e.Difficulty);
      }

      /// <summary>
      ///   Records that this NPC has DECLARED to the player they will leave their spouse: called when the ready-to-
      ///   leave note is shown in conversation, so the divorce clock starts only once the player has actually heard
      ///   it. No-op unless the NPC is at ReadyToDivorce, not yet declared, and not already divorced.
      /// </summary>
      public void MarkDeclared(string npcId, int today)
      {
         StolenLoveEstrangement e = Find(npcId);
         if (e == null || e.DivorcedDay.HasValue || e.DeclaredDay.HasValue) return;
         if (EstrangementProgressPolicy.Phase(today - e.StartDay, e.Difficulty) != EstrangementPhase.ReadyToDivorce) return;

         e.DeclaredDay = today;
         Hero npc = Hero.FindFirst(h => h != null && h.StringId == npcId);
         ModLog.Info($"[STOLEN_LOVE] {npc?.Name?.ToString() ?? npcId} has declared they will leave their spouse for the player; divorce due in {EstrangementProgressPolicy.DivorceDelayDays} days.");
      }

      /// <summary>True once this NPC's stolen-love arc has concluded (the divorce fired, or could not and was given up); read by the marry/sabotage exits.</summary>
      public bool HasConcludedFor(string npcId) => Find(npcId)?.DivorcedDay.HasValue == true;

      // ── Debug seam (cr.stolen_love + the self-test): drive the arc without waiting weeks ──────────────

      /// <summary>Test/dev: seed (or report an existing) estrangement for a third-party-married NPC, bypassing the affair/seducibility gate. Returns a short status line.</summary>
      public string DebugBegin(Hero npc, int today)
      {
         if (npc?.Spouse is not {IsAlive: true}) return $"{npc?.Name?.ToString() ?? "That hero"} is not married to a living spouse.";
         if (npc.Spouse == Hero.MainHero) return $"{npc.Name} is married to you, not a third party.";

         StolenLoveEstrangement e = Find(npc.StringId);
         if (e == null)
         {
            e = new StolenLoveEstrangement {NpcId = npc.StringId, SpouseId = npc.Spouse.StringId, StartDay = today, Difficulty = SeductionDifficulty.Easy};
            _active.Add(e);
         }

         return DebugDescribe(npc.StringId);
      }

      /// <summary>Test/dev: force this NPC to the point of declaring (stamps DeclaredDay now) so the divorce is only the delay away.</summary>
      public string DebugDeclare(string npcId, int today)
      {
         StolenLoveEstrangement e = Find(npcId);
         if (e == null) return "No estrangement is tracked for that hero (seed one first).";

         e.StartDay = today - EstrangementProgressPolicy.DaysToReady(e.Difficulty); // pull it up to ReadyToDivorce
         if (!e.DivorcedDay.HasValue && !e.DeclaredDay.HasValue) e.DeclaredDay = today;

         return DebugDescribe(npcId);
      }

      /// <summary>Test/dev: fire the divorce for this NPC right now (unmarry + jilted grudge), regardless of the delay.</summary>
      public string DebugForceDivorce(Hero npc, int today)
      {
         if (DebugBegin(npc, today).StartsWith("No ") || npc?.Spouse == null) { /* fall through */ }

         StolenLoveEstrangement e = Find(npc?.StringId);
         if (e == null) return "No estrangement could be seeded or found for that hero.";
         if (e.DivorcedDay.HasValue) return $"{npc.Name}'s arc already concluded on day {e.DivorcedDay}.";

         if (!e.DeclaredDay.HasValue) e.DeclaredDay = today;
         Hero jiltedBefore = npc.Spouse;
         ExecuteDivorce(e, today);

         // Report the REAL outcome: DivorcedDay is set whether the marriage actually dissolved or the divorce was
         // given up (a clan-leading leaving spouse TryDissolve refuses), so distinguish them for the tester.
         StolenLoveEstrangement after = Find(npc.StringId);
         string outcome = npc.Spouse == null
            ? $"DIVORCED from {jiltedBefore?.Name?.ToString() ?? "their spouse"} (a StolenSpouse grudge is planted on them)"
            : after?.DivorcedDay == null
               ? $"still married to {npc.Spouse.Name}: blocked this attempt (a role was released or it is transient), run divorce again after a day to complete"
               : $"could NOT divorce (still married to {npc.Spouse.Name}: their spouse cannot be relocated); arc given up (see the log for the reason)";

         return $"{outcome}. {DebugDescribe(npc.StringId)}";
      }

      /// <summary>Test/dev cleanup: forget any tracked estrangement for this NPC, so a non-destructive test leaves no state behind.</summary>
      public void DebugClear(string npcId) => _active.RemoveAll(e => e.NpcId == npcId);

      /// <summary>Test/dev: a one-line report of the tracked estrangement's phase and milestones.</summary>
      public string DebugDescribe(string npcId)
      {
         StolenLoveEstrangement e = Find(npcId);
         if (e == null) return "No stolen-love estrangement is tracked for that hero.";

         EstrangementPhase phase = EstrangementProgressPolicy.Phase(Today - e.StartDay, e.Difficulty);

         return $"phase={phase} ({ProgressFor(npcId)}%), difficulty={e.Difficulty}, "
              + $"declared={(e.DeclaredDay.HasValue ? e.DeclaredDay.ToString() : "no")}, "
              + $"divorced={(e.DivorcedDay.HasValue ? e.DivorcedDay.ToString() : "no")}.";
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy so an autosave racing the daily tick cannot throw mid-serialize.
               var snapshot = new List<StolenLoveEstrangement>(_active);
               bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[STOLEN_LOVE] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = System.Text.Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[STOLEN_LOVE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves, additive-key save-safety */
            }

            if (bytes is {Length: > 0})
               try { _active = JsonConvert.DeserializeObject<List<StolenLoveEstrangement>>(System.Text.Encoding.UTF8.GetString(bytes)) ?? new List<StolenLoveEstrangement>(); }
               catch (Exception ex)
               {
                  _active = new List<StolenLoveEstrangement>();
                  ModLog.Error($"[STOLEN_LOVE] load failed, estrangements dropped for this load so the campaign still opens. {ex.GetType().Name}: {ex.Message}");
               }
         }
      }

      #region private

      private StolenLoveEstrangement Find(string npcId)
         => string.IsNullOrEmpty(npcId) ? null : _active.FirstOrDefault(e => e.NpcId == npcId);

      private void OnDailyTick()
      {
         if (!ModSettings.EnableStolenLove) return; // dormant when the pillar is off; state is kept, not torn down

         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return;

         SafeRun.Step("NpcMarriageEstrangementBehavior.BeginNewAffairs", () => BeginNewAffairs(behavior, Today));
         SafeRun.Step("NpcMarriageEstrangementBehavior.ResolveDueDivorces", () => ResolveDueDivorces(Today));
         SafeRun.Step("NpcMarriageEstrangementBehavior.PruneEnded", () => PruneEnded(behavior));
      }

      /// <summary>Begins an estrangement for any seducible, third-party-married NPC who has become the player's secret lover and is not yet tracked.</summary>
      private void BeginNewAffairs(NpcMemoryCampaignBehavior behavior, int today)
      {
         foreach (string id in behavior.Store.ListIds())
         {
            if (_active.Any(e => e.NpcId == id)) continue;

            NpcProfile profile = behavior.Store.Get(id);
            if (profile?.Romantic?.Status != RomanticStatus.SecretLover) continue;

            Hero npc = Hero.FindFirst(h => h != null && h.StringId == id);
            if (npc?.Spouse is not {IsAlive: true}) continue;
            if (npc.Spouse == Hero.MainHero) continue;

            SeductionVerdict verdict = SeductionEligibilityPolicy.Evaluate(SeductionFactsMapper.For(npc, profile));
            if (!verdict.Seducible) continue;

            _active.Add(new StolenLoveEstrangement {
               NpcId = id,
               SpouseId = npc.Spouse.StringId,
               StartDay = today,
               Difficulty = verdict.Difficulty
            });
            ModLog.Info($"[STOLEN_LOVE] {npc.Name}'s marriage to {npc.Spouse.Name} begins to fray (difficulty {verdict.Difficulty}).");
         }
      }

      /// <summary>
      ///   Fires the divorce for any estrangement the NPC declared and whose month-long delay has elapsed: unmarry
      ///   the couple and plant the jilted spouse's grudge against the player. Marks the record concluded either
      ///   way (a divorce that could not complete, e.g. a clan-leading leaving spouse, is given up rather than
      ///   retried forever), so it stops declaring and the marry/sabotage exits can read the outcome.
      /// </summary>
      private void ResolveDueDivorces(int today)
      {
         foreach (StolenLoveEstrangement e in _active)
         {
            if (e.DivorcedDay.HasValue || !e.DeclaredDay.HasValue) continue;
            if (!EstrangementProgressPolicy.IsDivorceDue(e.DeclaredDay.Value, today)) continue;

            ExecuteDivorce(e, today);
         }
      }

      /// <summary>Unmarries the seduced NPC from their spouse and plants the jilted spouse's StolenSpouse grudge. Marks the record concluded whatever the outcome.</summary>
      private static void ExecuteDivorce(StolenLoveEstrangement e, int today)
      {
         Hero npc = Hero.FindFirst(h => h != null && h.StringId == e.NpcId);
         Hero jilted = Hero.FindFirst(h => h != null && h.StringId == e.SpouseId);

         if (npc == null || jilted == null || npc.Spouse != jilted)
         {
            e.DivorcedDay = today; // the marriage is already gone or the pair is invalid, nothing to do
            return;
         }

         // Capture the grudge inputs BEFORE the dissolution: the jilted spouse's relation to the partner who is
         // leaving them (a loved partner lost cuts deeper) and their forgiving nature.
         int jiltedRelationToDeparted = jilted.GetRelation(npc);
         int jiltedMercy = jilted.GetTraitLevel(DefaultTraits.Mercy);

         if (!MarriageDissolutionService.TryDissolve(npc, jilted, out string reason))
         {
            DivorceBlock block = MarriageDissolutionService.ClassifyBlock(npc, jilted);
            Hero leaving = DivorceEligibilityResolver.ResolveLeavingSpouse(npc, jilted);

            // A RELEASABLE structural block (the leaving spouse governs or leads a party, but does NOT head their
            // own clan) is not real resistance: release the post and leave the estrangement standing so a later
            // daily tick retries and completes (a party disband takes days), mirroring the player-divorce walk-out.
            if (block == DivorceBlock.Structural && leaving is {IsClanLeader: false})
            {
               MarriageDissolutionService.ReleaseStructuralRoles(leaving);
               ModLog.Info($"[STOLEN_LOVE] {npc.Name}'s divorce is structurally blocked ({reason}); released {leaving.Name}'s post, will retry.");

               return; // NOT concluded: retry next tick
            }

            // A pregnancy or captivity is transient: retry once it clears, do not conclude.
            if (block == DivorceBlock.Transient)
            {
               ModLog.Info($"[STOLEN_LOVE] {npc.Name}'s divorce is deferred ({reason}); will retry.");

               return; // NOT concluded
            }

            // Genuine spousal resistance, or a leaving spouse who heads their OWN clan (cannot be relocated): give
            // up cleanly rather than loop forever.
            e.DivorcedDay = today;
            ModLog.Info($"[STOLEN_LOVE] could not complete {npc.Name}'s divorce from {jilted.Name} ({reason}); the arc is given up.");

            return;
         }

         e.DivorcedDay = today;

         int magnitude = StolenSpouseGrudgePolicy.Magnitude(jiltedRelationToDeparted, jiltedMercy);
         GrudgeBehavior.Instance?.TryPlant(jilted, Hero.MainHero, GrudgeSource.StolenSpouse,
            $"the player carried on with {npc.Name} until our marriage came apart", magnitude, true, today);

         ModLog.Info($"[STOLEN_LOVE] {npc.Name} has left {jilted.Name} for the player; a StolenSpouse grudge (magnitude {magnitude}) is planted on {jilted.Name}.");
      }

      /// <summary>Drops a PRE-DIVORCE estrangement whose NPC is gone, whose marriage already ended or changed, or whose affair with the player is over. A concluded record is kept as a marker for the exits.</summary>
      private void PruneEnded(NpcMemoryCampaignBehavior behavior)
      {
         _active.RemoveAll(e =>
         {
            if (e.DivorcedDay.HasValue) return false; // concluded: keep as the "left their spouse for the player" marker

            Hero npc = Hero.FindFirst(h => h != null && h.StringId == e.NpcId);
            if (npc == null || !npc.IsAlive) return true;
            if (npc.Spouse == null || npc.Spouse.StringId != e.SpouseId) return true; // divorced, widowed, or spouse changed

            NpcProfile profile = behavior.Store.Get(e.NpcId);

            return profile?.Romantic?.Status != RomanticStatus.SecretLover; // the affair is over
         });
      }

      #endregion
   }
}
