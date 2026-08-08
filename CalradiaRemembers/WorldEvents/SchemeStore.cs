// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Stance;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>
   ///   Drives the v1.3 feud schemes: bitter rivals quietly plot against one another, the plots
   ///   mature or collapse mechanically each day, and word of them spreads through the rumour
   ///   network. The player never triggers the simulation — they only ever hear of it. Persisted as
   ///   a UTF-8 byte[] (mirrors <see cref="WorldEventStore" />) with the same "never brick a save"
   ///   degradation.
   /// </summary>
   public sealed class SchemeStore : CampaignBehaviorBase
   {
      // Living World pillar, Increment 2: the autonomous generator's only engine-side bound, how many living
      // lords a single weekly pass will consider as instigators before giving up. A curated generator, not a
      // simulation: the eligibility/budget/cooldown DECISIONS all live in the pure AutonomousSchemePolicy.
      private const int MaxInstigatorScan = 60;

      private const string SaveKey = "CalradiaRemembers_Schemes_v1bytes";

      // Additive, save-safe key for the per-instigator autonomous-plot cooldown (Increment 2): absent in older
      // saves, it degrades to an empty ledger, so nothing is on cooldown after an upgrade.
      private const string CooldownSaveKey = "CalradiaRemembers_SchemeAutoCooldown_v1bytes";
      private const int SchemeAbettedGrudgeMagnitude = 55;

      // Grudges pillar (Tier 1, unrecoverable from a save — live-planted only): the plotter conceals their
      // own grudge over being exposed; the wronged target's grudge, once the player's hand comes out, is public.
      private const int SchemeWarnedGrudgeMagnitude = 45;

      private const int SuccessInfluenceHit = 15; // standing damage the target's clan takes on success
      public static SchemeStore Instance;

      // Per-instigator last-autonomous-launch day (Increment 2), persisted so one lord's ~30-day plot cooldown
      // survives save/load rather than resetting on every reload (the old non-persisted pair cooldown did not).
      private Dictionary<string, int> _autonomousCooldown = new();

      private List<Scheme> _schemes = new();

      public SchemeStore()
      {
         Instance = this;
      }

      /// <summary>The plots currently in motion — for diagnostics (cr.schemes).</summary>
      public IReadOnlyList<Scheme> Active => _schemes;

      /// <summary>Runs one tick of advancement only (no spawn roll) — for console testing without waiting days.</summary>
      public void DebugAdvance() => AdvanceActive((int) CampaignTime.Now.ToDays);

      /// <summary>
      ///   Launches a scheme through the ONE shared launch path (console testing forces it, pledge_against
      ///   drives it, and the Living World autonomous generator drives it): it uses the real
      ///   advance/effect/rumour pipeline from the next daily tick. Returns a short status line.
      ///   <paramref name="autonomous" /> marks a plot a lord launched on their own initiative (Increment 2)
      ///   so it counts against the autonomous budget; every existing caller keeps the default (false).
      /// </summary>
      public string ForcePlot(Hero plotter, Hero target, SchemeKind kind, bool autonomous = false)
      {
         if (plotter == null || target == null || plotter == target) return "Need two distinct heroes.";
         if (HasActiveBetween(plotter.StringId, target.StringId)) return $"A scheme between {plotter.Name} and {target.Name} is already afoot.";

         _schemes.Add(new Scheme {
            PlotterId = plotter.StringId, TargetId = target.StringId, Kind = kind, State = SchemeState.Plotted, Autonomous = autonomous
         });
         ModLog.Info($"[SCHEME] plotted{(autonomous ? " (autonomous)" : " (forced)")}: {plotter.Name} → {target.Name} ({kind}).");

         return $"{plotter.Name} now plots {kind} against {target.Name}. Advance a day to see it move.";
      }

      // ── Player as agent (v1.3 player involvement) ─────────────────────────

      /// <summary>
      ///   The name of the target this plotter could recruit the player against, or null when no
      ///   recruitable scheme applies. Used to gate the <c>scheme_assist</c> prompt teaching.
      /// </summary>
      public string GetRecruitableTargetName(Hero plotter)
         => ResolveRecruitableScheme(plotter, out Hero target) == null
            ? null
            : target.Name?.ToString();

      /// <summary>True when a still-secret scheme is aimed at <paramref name="target" /> — gates the scheme_heed teaching.</summary>
      public bool HasSecretSchemeAgainst(Hero target)
         => target != null && _schemes.Any(s => s.TargetId == target.StringId && SchemeAgentPolicy.CanWarnTarget(s.State));

      /// <summary>
      ///   The plotter of a still-secret scheme against <paramref name="target" />, or null when none
      ///   applies. Used by the stance Warning payoff ("stance with teeth" increment 2): a trusted friend
      ///   NAMES the real threat instead of speaking only in generic warmth.
      /// </summary>
      public Hero GetSecretSchemerAgainst(Hero target)
      {
         if (target == null) return null;

         Scheme scheme = _schemes.FirstOrDefault(s => s.TargetId == target.StringId && SchemeAgentPolicy.CanWarnTarget(s.State));

         return scheme == null ? null : FindHero(scheme.PlotterId);
      }

      /// <summary>
      ///   The player throws in with the plotter's scheme: it leaps forward (pure
      ///   <see cref="SchemeAgentPolicy.ApplyPlayerAid" />), and if that carries it to fruition the
      ///   standing-damage effect lands and a rumour spreads. Secret partial help stays quiet.
      ///   Re-validates the gate (the bridge is the law); returns null when nothing applies.
      /// </summary>
      public string PlayerAssists(Hero plotter)
      {
         Scheme scheme = ResolveRecruitableScheme(plotter, out Hero target);

         if (scheme == null) return null;

         var today = (int) CampaignTime.Now.ToDays;
         scheme.PlayerAbetted = true; // the player's hand can now implicate them if it surfaces
         SchemeAgentPolicy.ApplyPlayerAid(scheme);

         if (scheme.State == SchemeState.Succeeded)
         {
            ApplyStandingDamage(target);
            ApplyFeud(scheme, plotter, target, SchemeState.Succeeded, today);
            RecordRumor(scheme, plotter, target, today, "succeeded", 34, scheme.Kind == SchemeKind.Slander
               ? $"{plotter.Name} has blackened {target.Name}'s name, and it has cost them dearly."
               : $"{plotter.Name} has undermined {target.Name}, weakening their standing.");
            MaybeAbettorBacklash(scheme, target); // records the player's complicity as its own rumour
            // RP with teeth, increment 2: if the plotter had PLEDGED this scheme to the player, it is
            // now fulfilled. CommitmentBehavior is defensive internally — never throws into this pipeline.
            CommitmentBehavior.Instance?.OnSchemeResolved(scheme.PlotterId, scheme.TargetId, true);
            _schemes.Remove(scheme);

            return $"With your help, {plotter.Name}'s plot against {target.Name} comes to fruition.";
         }

         // Secret help — no public rumour yet; the plot simply gains ground.
         ModLog.Info($"[SCHEME] player aided {plotter.Name}'s plot against {target.Name} (progress {scheme.Progress}).");

         return $"You quietly advance {plotter.Name}'s scheme against {target.Name}.";
      }

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         // Living World pillar, Increment 2: the autonomous scheme generator runs on a weekly cadence (the daily
         // tick only ADVANCES plots already in motion). Weekly keeps it a light, curated pass, never per-hero daily work.
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
      }

      // ── Persistence (mirrors WorldEventStore) ─────────────────────────────

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list (see WorldEventStore): an autosave racing a
               // main-thread mutation would otherwise throw mid-serialize and drop the whole blob this save.
               var snapshot = new List<Scheme>(_schemes);
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[SCHEME] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SCHEME] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }

            byte[] cooldownBytes;
            try { cooldownBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Dictionary<string, int>(_autonomousCooldown), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SCHEME] cooldown serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               cooldownBytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(CooldownSaveKey, ref cooldownBytes); }
            catch (Exception ex) { ModLog.Error($"[SCHEME] cooldown byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is {Length: > 0})
               try { _schemes = JsonConvert.DeserializeObject<List<Scheme>>(Encoding.UTF8.GetString(bytes)) ?? new List<Scheme>(); }
               catch (Exception ex)
               {
                  _schemes = new List<Scheme>();
                  ModLog.Error($"[SCHEME] load failed — schemes dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            byte[] cooldownBytes = null;
            try { dataStore.SyncData(CooldownSaveKey, ref cooldownBytes); }
            catch
            {
               /* key absent in saves written before Increment 2: degrades to an empty ledger */
            }

            if (cooldownBytes is {Length: > 0})
               try { _autonomousCooldown = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(cooldownBytes)) ?? new Dictionary<string, int>(); }
               catch (Exception ex)
               {
                  _autonomousCooldown = new Dictionary<string, int>();
                  ModLog.Error($"[SCHEME] cooldown load failed, dropped so the campaign can still open (a lord may plot again sooner). {ex.GetType().Name}: {ex.Message}");
               }
            else
               _autonomousCooldown = new Dictionary<string, int>();

            ModLog.Info($"[SCHEME] loaded {_schemes.Count} active scheme(s) and {_autonomousCooldown.Count} instigator cooldown(s) from save.");
         }
      }

      /// <summary>
      ///   The player warns <paramref name="target" /> of a plot against them: the scheme is dragged
      ///   into the light (pure <see cref="SchemeAgentPolicy.ExposeToTarget" />) and the discovery
      ///   spreads as a rumour. Forewarned, the target's counterplay (the daily tick's elevated
      ///   backfire on an exposed plot) gives them a real chance. Returns null when nothing applies.
      /// </summary>
      public string TargetHeeds(Hero target)
      {
         if (target == null) return null;

         Scheme scheme = _schemes.FirstOrDefault(s => s.TargetId == target.StringId && SchemeAgentPolicy.CanWarnTarget(s.State));

         if (scheme == null) return null;

         Hero plotter = FindHero(scheme.PlotterId);

         if (plotter == null) return null;

         SchemeAgentPolicy.ExposeToTarget(scheme);
         RecordRumor(scheme, plotter, target, (int) CampaignTime.Now.ToDays, "discovered", 28,
            $"{target.Name}, warned in time, has uncovered {plotter.Name}'s plot against them.");

         // The exposed plotter suspects the meddler; the warned target is grateful. Both magnitudes scale
         // with traits: a vindictive calculating plotter hates hardest; an honorable target feels a real debt.
         if (Hero.MainHero == null) return $"You warn {target.Name} of {plotter.Name}'s scheme. Grateful, {target.Name} thinks the better of you — but {plotter.Name} may suspect your hand in it.";

         int resentment = SchemeBacklash.WarnedPlotterResentment(TraitLevel(plotter, DefaultTraits.Mercy), TraitLevel(plotter, DefaultTraits.Calculating));
         int gratitude = SchemeBacklash.WarnedTargetGratitude(TraitLevel(target, DefaultTraits.Honor), TraitLevel(target, DefaultTraits.Calculating));

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(plotter, Hero.MainHero, -resentment, false);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(target, Hero.MainHero, gratitude, false);
         ModLog.Info($"[SCHEME] player warned {target.Name} of {plotter.Name}'s plot — now exposed; {plotter.Name} resents player (-{resentment}), {target.Name} is grateful (+{gratitude}).");

         // Grudges pillar: the PLOTTER grudges the player for exposing their design — but conceals it
         // (the plotter stays outwardly smooth), on top of the relation hit already applied above.
         GrudgeBehavior.Instance?.TryPlant(plotter, Hero.MainHero, GrudgeSource.SchemeWarned,
            $"The player warned {target.Name} of my design against them.", SchemeWarnedGrudgeMagnitude,
            false, (int) CampaignTime.Now.ToDays);

         return $"You warn {target.Name} of {plotter.Name}'s scheme. Grateful, {target.Name} thinks the better of you — but {plotter.Name} may suspect your hand in it.";
      }

      #region private

      /// <summary>
      ///   The v1 effect: a hit to the target clan's influence. The plotter/target relation rupture is no
      ///   longer applied here: it is now the feud's job (<see cref="ApplyFeud" />), sized by
      ///   <see cref="FeudRelationPolicy" /> and applied by the caller on the same resolution.
      /// </summary>
      private static void ApplyStandingDamage(Hero target)
      {
         try
         {
            if (target.Clan != null)
               ChangeClanInfluenceAction.Apply(target.Clan, -SuccessInfluenceHit);
            ModLog.Info($"[SCHEME] success effect: {target.Name} clan influence -{SuccessInfluenceHit}.");
         }
         catch (Exception ex) { ModLog.Error($"[SCHEME] effect failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Living World pillar, Increment 1: a resolved scheme becomes a VISIBLE feud. Worsens the REAL
      ///   plotter/target relation (the lever vanilla's <c>GetScoreOfDeclaringWar</c> reads, so a feud that
      ///   holds lets the kingdom AI escalate to war on its OWN; no war is ever declared here), cools the
      ///   target's closest clan allies toward the plotter so third parties learn and adjust, and records the
      ///   feud as news so it spreads by the rumour network and lands in the chronicle. All sizes come from
      ///   the pure <see cref="FeudRelationPolicy" />; this method only feeds it facts and applies the deltas.
      /// </summary>
      private static void ApplyFeud(Scheme scheme, Hero plotter, Hero target, SchemeState outcome, int today)
      {
         if (scheme == null || plotter == null || target == null) return;

         FeudRelationOutcome feud = FeudRelationPolicy.Resolve(scheme.Kind, outcome);
         if (!feud.CreatesFeud) return;

         try
         {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(plotter, target, feud.PlotterTargetDelta, false);
            CoolTargetAllies(plotter, target, feud.AllyCoolingDelta, feud.AllyFanOutCap);

            int magnitude = Math.Min(60, 20 + Math.Abs(feud.PlotterTargetDelta));
            RecordRumor(scheme, plotter, target, today, "feud", magnitude,
               $"{plotter.Name} and {target.Name} are now locked in open feud.");

            ModLog.Info($"[SCHEME] feud ({outcome}): {plotter.Name} vs {target.Name} relation {feud.PlotterTargetDelta}, allies cooled {feud.AllyCoolingDelta} (cap {feud.AllyFanOutCap}).");
         }
         catch (Exception ex) { ModLog.Error($"[SCHEME] feud effect failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Cools the target's closest clan-mates toward the plotter by a small, bounded delta so the feud is
      ///   visible to third parties. Closest allies first (highest regard for the target), capped so a feud
      ///   never ripples across a whole clan; the player is never touched by this NPC-to-NPC adjustment.
      /// </summary>
      private static void CoolTargetAllies(Hero plotter, Hero target, int delta, int cap)
      {
         if (cap <= 0 || delta == 0 || target?.Clan == null) return;

         var allies = target.Clan.Heroes
                             .Where(h => h is {IsAlive: true, IsLord: true} && h != target && h != plotter && h != Hero.MainHero)
                             .OrderByDescending(h => h.GetRelation(target))
                             .Take(cap)
                             .ToList();

         foreach (Hero ally in allies)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(ally, plotter, delta, false);

         if (allies.Count > 0)
            ModLog.Info($"[SCHEME] feud fan-out: {allies.Count} of {target.Name}'s clan cooled toward {plotter.Name} ({delta}).");
      }

      // ── Factors (engine traits → pure SchemeFactors) ──────────────────────

      private static SchemeFactors BuildFactors(Hero plotter, Hero target) => new() {
         PlotterCunning = Cunning(plotter),
         PlotterBoldness = Clamp0to100(30 + TraitLevel(plotter, DefaultTraits.Valor) * 20),
         Animosity = Clamp0to100(-plotter.GetRelation(target)),
         TargetVigilance = Cunning(target)
      };

      private static int Clamp0to100(int v) => Math.Max(0, Math.Min(100, v));

      private static int Cunning(Hero h) => Clamp0to100(30 + TraitLevel(h, DefaultTraits.Calculating) * 20);

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      /// <summary>
      ///   When an abetted plot's outcome surfaces the player's hand (it succeeded, or was caught),
      ///   the wronged target turns on the player. One-shot: the flag is cleared so it cannot re-fire
      ///   if the scheme later changes state again.
      /// </summary>
      private static void MaybeAbettorBacklash(Scheme scheme, Hero target)
      {
         if (scheme is not {PlayerAbetted: true}) return;

         int relation = target != null && Hero.MainHero != null
            ? target.GetRelation(Hero.MainHero)
            : 0;

         int penalty = SchemeBacklash.AbettorPenalty(scheme.State, relation);
         scheme.PlayerAbetted = false; // one-shot regardless of outcome

         if (penalty <= 0 || target == null || Hero.MainHero == null) return;

         // 1. The wronged target turns on the player — affection falls, and trust collapses.
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(target, Hero.MainHero, -penalty, false);
         StanceService.OnTreacheryDiscovered(target);

         // Grudges pillar: the wronged target's grudge — public, once their own rumour spreads it below.
         GrudgeBehavior.Instance?.TryPlant(target, Hero.MainHero, GrudgeSource.SchemeAbetted,
            "It came out that the player had a secret hand in the plot against me.", SchemeAbettedGrudgeMagnitude,
            true, (int) CampaignTime.Now.ToDays);

         // 2. Word of the treachery spreads as a disgrace rumour — fed to commoners and to lords'
         //    "what you've heard", so they speak of the player as a known schemer. Reach scales with
         //    gravity (a graver betrayal carries furthest); InvolvesPlayer=false so it spreads then fades.
         var today = (int) CampaignTime.Now.ToDays;
         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Affront,
            Day = today,
            Magnitude = Math.Min(60, 20 + penalty),
            InvolvesPlayer = false,
            Status = "treachery",
            Summary = $"It is whispered that {Hero.MainHero.Name} had a secret hand in the plot that wronged {target.Name}.",
            ActorIds = new List<string> {Hero.MainHero.StringId, target.StringId},
            FactionIds = target.MapFaction != null
               ? new List<string> {target.MapFaction.StringId}
               : new List<string>()
         });

         ModLog.Info($"[SCHEME] abettor backlash: {target.Name} turns on the player (-{penalty}); treachery rumour recorded.");
         try
         {
            MBInformationManager.AddQuickInformation(
               new TextObject($"Word spreads that you had a hand in the plot against {target.Name}."));
         }
         catch
         {
            /* notification is best-effort */
         }
      }

      private static void RecordRumor(Scheme scheme, Hero plotter, Hero target, int today, string status, int magnitude, string summary)
      {
         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Rivalry, // a scheme is an active rivalry; Status carries its phase
            Day = today,
            Magnitude = magnitude,
            InvolvesPlayer = false,
            Status = status,
            Summary = summary,
            ActorIds = new List<string> {plotter.StringId, target.StringId},
            FactionIds = new[] {plotter.MapFaction?.StringId, target.MapFaction?.StringId}
                         .Where(s => s != null).Distinct().ToList()
         });
      }

      private static int TraitLevel(Hero h, TraitObject trait)
      {
         try { return h?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      // ── Advancing active schemes ──────────────────────────────────────────

      private void AdvanceActive(int today)
      {
         // Iterate a copy: the list is mutated as terminal schemes are removed.
         foreach (Scheme scheme in _schemes.ToList())
         {
            Hero plotter = FindHero(scheme.PlotterId);
            Hero target = FindHero(scheme.TargetId);

            // A dead or vanished party ends the plot quietly. v1.34.2 fault/no-fault hardening (audit Pattern 2):
            // before dropping the scheme, resolve any pledge linked to this exact pair as a NEUTRAL VOID, so a
            // MoveAgainst promise whose rival just died does not survive to be branded a public parjury by the
            // commitment breach sweep (the target's death is an act of the world, not the promisor's breach).
            if (plotter is not {IsAlive: true} || target is not {IsAlive: true})
            {
               CommitmentBehavior.Instance?.OnSchemeVoided(scheme.PlotterId, scheme.TargetId,
                  "The rival is beyond reach now: the pledge is set aside, no fault to answer for.");
               _schemes.Remove(scheme);

               continue;
            }

            SchemeState before = scheme.State;
            int progressBefore = scheme.Progress;
            SchemeAdvancer.Advance(scheme, BuildFactors(plotter, target), MBRandomSource.Instance);

            if (scheme.State != before)
               OnStateChanged(scheme, plotter, target, today);
            else if (scheme.Progress != progressBefore)
               ModLog.Info($"[SCHEME] {plotter?.Name} → {target?.Name}: progress {progressBefore} → {scheme.Progress} (still {scheme.State}).");

            if (SchemeAdvancer.IsTerminal(scheme.State))
               _schemes.Remove(scheme);
         }
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      private bool HasActiveBetween(string id1, string id2)
         => _schemes.Any(s => s.PlotterId == id1 && s.TargetId == id2 || s.PlotterId == id2 && s.TargetId == id1);

      private void OnDailyTick()
      {
         var today = (int) CampaignTime.Now.ToDays;
         // The daily tick only ADVANCES plots already in motion; the autonomous generator that STARTS new ones
         // runs weekly (OnWeeklyTick), a lighter, curated cadence.
         SafeRun.Step("SchemeStore.AdvanceActive", () => AdvanceActive(today));
      }

      private void OnWeeklyTick()
      {
         SafeRun.Step("SchemeStore.TryLaunchAutonomousScheme", () => TryLaunchAutonomousScheme((int) CampaignTime.Now.ToDays));
      }

      private void OnStateChanged(Scheme scheme, Hero plotter, Hero target, int today)
      {
         switch (scheme.State)
         {
            case SchemeState.Progressing: // the plot is now afoot — a first whisper
               ModLog.Info($"[SCHEME] {plotter.Name} → {target.Name} ({scheme.Kind}) now Progressing (progress {scheme.Progress}).");
               RecordRumor(scheme, plotter, target, today, "plotted", 18,
                  $"There is talk that {plotter.Name} is quietly moving against {target.Name}.");

               break;

            case SchemeState.Discovered: // it has come to light
               ModLog.Info($"[SCHEME] {plotter.Name} → {target.Name} ({scheme.Kind}) Discovered (progress {scheme.Progress}).");
               RecordRumor(scheme, plotter, target, today, "discovered", 28,
                  $"{plotter.Name}'s plot against {target.Name} has come to light.");
               ApplyFeud(scheme, plotter, target, SchemeState.Discovered, today);
               MaybeAbettorBacklash(scheme, target);

               break;

            case SchemeState.Succeeded:
               ModLog.Info($"[SCHEME] {plotter.Name} → {target.Name} ({scheme.Kind}) Succeeded — applying standing damage.");
               ApplyStandingDamage(target);
               ApplyFeud(scheme, plotter, target, SchemeState.Succeeded, today);
               RecordRumor(scheme, plotter, target, today, "succeeded", 34, scheme.Kind == SchemeKind.Slander
                  ? $"{plotter.Name} has blackened {target.Name}'s name, and it has cost them dearly."
                  : $"{plotter.Name} has undermined {target.Name}, weakening their standing.");
               MaybeAbettorBacklash(scheme, target);
               // RP with teeth, increment 2: fulfills a matching pledge, if the plotter had made one.
               CommitmentBehavior.Instance?.OnSchemeResolved(scheme.PlotterId, scheme.TargetId, true);

               break;

            case SchemeState.Failed:
               ModLog.Info($"[SCHEME] {plotter.Name} → {target.Name} ({scheme.Kind}) Failed (progress {scheme.Progress}).");
               RecordRumor(scheme, plotter, target, today, "failed", 20,
                  $"{plotter.Name}'s scheme against {target.Name} came to nothing.");

               break;
         }
      }

      /// <summary>The plotter's secret, active scheme the player is eligible to abet, or null.</summary>
      private Scheme ResolveRecruitableScheme(Hero plotter, out Hero target)
      {
         target = null;

         if (plotter == null || Hero.MainHero == null) return null;

         Scheme scheme = _schemes.FirstOrDefault(s => s.PlotterId == plotter.StringId && !SchemeAdvancer.IsTerminal(s.State));

         if (scheme == null) return null;

         target = FindHero(scheme.TargetId);

         if (target is not {IsAlive: true}) return null;

         int regard = plotter.GetRelation(Hero.MainHero);
         bool allied = target.GetRelation(Hero.MainHero) >= 20 || target.MapFaction != null && target.MapFaction == Hero.MainHero.MapFaction;

         return SchemeAgentPolicy.CanRecruitPlayer(scheme.State, regard, allied)
            ? scheme
            : null;
      }

      // ── Launching autonomous schemes (Living World pillar, Increment 2) ────

      /// <summary>
      ///   Once a week, a bitter rival lord may LAUNCH a plot on their own initiative against a genuine enemy,
      ///   feeding the very front of the Increment 1 feud chain (a plot that ripens or is exposed sours the real
      ///   relation and lets vanilla escalate to war on its own; no war is ever decreed here). Curated and
      ///   capped, never a simulation: the pure <see cref="AutonomousSchemePolicy" /> owns every boundary (a
      ///   global cap on concurrent AUTONOMOUS plots, counted distinctly from player-abetted ones; a persisted
      ///   per-instigator cooldown; and a low per-eligible-pair roll), so the world feels alive rather than a
      ///   plot-storm. Reuses the SAME launch path pledge_against uses (<see cref="ForcePlot" />). One new plot
      ///   per weekly pass keeps the intrigue ambient.
      /// </summary>
      private void TryLaunchAutonomousScheme(int today)
      {
         int activeAutonomous = _schemes.Count(s => s.Autonomous);
         if (!AutonomousSchemePolicy.BudgetAllowsLaunch(activeAutonomous)) return;

         foreach (Hero instigator in EligibleInstigators(today))
         {
            string targetId = SelectAutonomousTargetFor(instigator);
            if (targetId == null) continue;
            if (HasActiveBetween(instigator.StringId, targetId)) continue;

            // A low per-eligible-pair roll: even a real rivalry only occasionally boils over into a fresh plot.
            if (MBRandom.RandomInt(100) >= AutonomousSchemePolicy.PerPairLaunchChancePercent) continue;

            Hero target = FindHero(targetId);
            if (target is not {IsAlive: true}) continue;

            SchemeKind kind = MBRandom.RandomInt(2) == 0
               ? SchemeKind.Slander
               : SchemeKind.Sabotage;

            ForcePlot(instigator, target, kind, true); // the shared launch path; marks the plot autonomous
            _autonomousCooldown[instigator.StringId] = today;

            return; // one new autonomous plot per weekly pass
         }
      }

      /// <summary>
      ///   The living, free lords with standing to plot autonomously whose per-instigator cooldown has elapsed:
      ///   never the player nor a member of the player's own clan (they plot only via the player-driven path).
      ///   Bounded by <see cref="MaxInstigatorScan" />, a curated candidate set, not per-hero simulation.
      /// </summary>
      private List<Hero> EligibleInstigators(int today)
      {
         var result = new List<Hero>();

         foreach (Hero h in Hero.AllAliveHeroes)
         {
            bool isPlayerClan = h.Clan != null && h.Clan == Clan.PlayerClan;

            if (!AutonomousSchemePolicy.IsEligibleInstigator(h.IsAlive, h.IsLord, h.IsPrisoner, h == Hero.MainHero, isPlayerClan)) continue;
            if (!AutonomousSchemePolicy.InstigatorCooldownElapsed(LastAutonomousDay(h.StringId), today)) continue;

            result.Add(h);
            if (result.Count >= MaxInstigatorScan) break;
         }

         return result;
      }

      private int? LastAutonomousDay(string instigatorId)
         => _autonomousCooldown.TryGetValue(instigatorId, out int day)
            ? day
            : (int?) null;

      /// <summary>
      ///   The grounded target this instigator would autonomously plot against, or null. Reuses the same kin-safe,
      ///   already-disliked, no-faction-leader selection pledge_against relies on
      ///   (<see cref="AutonomousSchemePolicy.SelectAutonomousTarget" />), and never the player or the player's clan.
      /// </summary>
      private string SelectAutonomousTargetFor(Hero instigator)
      {
         var candidates = new List<AutonomousSchemePolicy.SchemeTargetCandidate>();

         foreach (Hero other in Hero.AllAliveHeroes)
         {
            if (other == instigator || !other.IsLord) continue;

            bool isPlayerSide = other == Hero.MainHero || other.Clan != null && other.Clan == Clan.PlayerClan;
            bool isKin = CourtActionResolver.IsCloseKin(instigator, other);
            bool isFactionLeader = other.MapFaction?.Leader == other;

            candidates.Add(new AutonomousSchemePolicy.SchemeTargetCandidate(
               other.StringId, instigator.GetRelation(other), isKin, isPlayerSide, isFactionLeader));
         }

         return AutonomousSchemePolicy.SelectAutonomousTarget(candidates);
      }

      #endregion
   }
}