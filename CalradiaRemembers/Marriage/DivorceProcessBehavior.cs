// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Tracks every outstanding <see cref="DivorceProcess" /> and advances it one day at a time. One
   ///   process per NPC at once (<see cref="HasActive" />). <see cref="Create" /> is called by
   ///   <see cref="Verbs.EndMarriageVerb" /> once it has re-validated eligibility itself; this class owns
   ///   nothing about eligibility, only the process's own lifecycle from that point on.
   /// </summary>
   public sealed class DivorceProcessBehavior : CampaignBehaviorBase
   {
      /// <summary>
      ///   Appreciations pillar: the fixed formation magnitude for a DivorceGratitude plant — comparable to
      ///   AppreciationBackfillRules.KindnessInSceneMagnitude.
      /// </summary>
      private const int DivorceGratitudeAppreciationMagnitude = 30;

      private const string SaveKey = "CalradiaRemembers_Divorce_v1bytes";

      public static DivorceProcessBehavior Instance;

      private List<DivorceProcess> _active = new();

      public DivorceProcessBehavior()
      {
         Instance = this;
      }

      /// <summary>The processes currently under way — for the cr.divorce_info diagnostic.</summary>
      public IReadOnlyList<DivorceProcess> Active => _active;

      /// <summary>
      ///   Records a new estrangement process, its end day computed by <see cref="DivorcePolicy" /> from
      ///   today. The caller has already re-validated eligibility live.
      /// </summary>
      public DivorceProcess Create(string npcId, string spouseId, int issuedDay)
      {
         var process = new DivorceProcess {
            Id = Guid.NewGuid().ToString("N"),
            NpcId = npcId,
            SpouseId = spouseId,
            StartDay = issuedDay,
            EndDay = DivorcePolicy.ComputeEndDay(issuedDay),
            CumulativeDelayDays = 0,
            State = DivorceProcessState.Active
         };
         _active.Add(process);
         ModLog.Info($"[DIVORCE] estrangement begun: npc={npcId} spouse={spouseId} end day {process.EndDay}.");

         return process;
      }

      /// <summary>Runs one day's advance immediately for every active process (console/self-test only).</summary>
      public void DebugRunDailyAdvance() => OnDailyTick();

      /// <summary>The NPC's own active process, or null.</summary>
      public DivorceProcess FindActive(string npcId)
         => string.IsNullOrEmpty(npcId)
            ? null
            : _active.FirstOrDefault(p => p.NpcId == npcId && p.State == DivorceProcessState.Active);

      /// <summary>True when this NPC already has a divorce process in flight.</summary>
      public bool HasActive(string npcId)
         => !string.IsNullOrEmpty(npcId) && _active.Any(p => p.NpcId == npcId && p.State == DivorceProcessState.Active);

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      // ── Persistence (mirrors CommitmentBehavior) ────────────────────────────

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list: an autosave racing a main-thread mutation
               // would otherwise throw mid-serialize and drop the whole blob this save.
               var snapshot = new List<DivorceProcess>(_active);
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[DIVORCE] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[DIVORCE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
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
               try { _active = JsonConvert.DeserializeObject<List<DivorceProcess>>(Encoding.UTF8.GetString(bytes)) ?? new List<DivorceProcess>(); }
               catch (Exception ex)
               {
                  _active = new List<DivorceProcess>();
                  ModLog.Error($"[DIVORCE] load failed — processes dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            ModLog.Info($"[DIVORCE] loaded {_active.Count} outstanding estrangement process(es) from save.");
         }
      }

      #region private

      /// <summary>
      ///   One clan's verdict, applied per living member (excluding <paramref name="excludeMember" /> and
      ///   the player): a grudge per member, or a relief/gratitude nudge per member. Shared by
      ///   <see cref="DivorceConsequences.SpouseClanReaction" />, <see cref="DivorceConsequences.DivorcerClanReaction" />
      ///   and <see cref="DivorceConsequences.UnblessedFamilyReaction" /> alike.
      /// </summary>
      private static void ApplyClanReaction(PartyReaction reaction, Clan clan, Hero excludeMember, string cause, int today)
      {
         if (reaction == null || clan?.Heroes == null || Hero.MainHero == null) return;

         foreach (Hero member in clan.Heroes.ToList())
         {
            if (member == null || member == excludeMember || member == Hero.MainHero || !member.IsAlive) continue;

            if (reaction.Kind == ReactionKind.PlantGrudge)
               GrudgeBehavior.Instance?.TryPlant(member, Hero.MainHero, GrudgeSource.Divorce, cause,
                  reaction.GrudgeMagnitude, reaction.KnownToPlayer, today);
            else if (reaction.RelationNudge != 0)
               ApplyPersonalRelief(member, reaction.RelationNudge);

            // Appreciations pillar: a clan whose Gratitude verdict fires (an unwanted marriage/alliance
            // finally over) also FEELS it as a lingering kindness, a channel separate from the personal
            // relation nudge above.
            TryPlantDivorceGratitude(reaction.Kind, member, today);
         }
      }

      /// <summary>
      ///   Relief/Gratitude is a PERSONAL warming toward the player (glad to see the union end), not clan
      ///   politics — routed to <paramref name="hero" />'s own <see cref="NpcProfile.ReputationWithPlayer" />
      ///   via the store, mirroring the fetch/apply/persist pattern <see cref="CommitmentBehavior.Fulfill" />
      ///   uses for its own positive credit. Distinct from the baseline SET applied on the spouse's
      ///   relations above (that channel corrects a NATIVE engine effect and stays on the vanilla ledger).
      /// </summary>
      private static void ApplyPersonalRelief(Hero hero, int relationNudge)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null) return;

         NpcProfile profile = store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         ProfileMutator.ApplyReputationDelta(profile, relationNudge);
         profile.LastRelationGainHour = CampaignTime.Now.ToHours;
         store.Set(profile);
      }

      /// <summary>The set-aside spouse's own verdict: a grudge against the player, or a relief nudge toward them.</summary>
      private static void ApplySpouseReaction(PartyReaction reaction, Hero spouse, string cause, int today)
      {
         if (reaction == null || Hero.MainHero == null) return;

         if (reaction.Kind == ReactionKind.PlantGrudge)
         {
            GrudgeBehavior.Instance?.TryPlant(spouse, Hero.MainHero, GrudgeSource.Divorce, cause,
               reaction.GrudgeMagnitude, reaction.KnownToPlayer, today);

            return;
         }

         if (reaction.RelationNudge != 0)
            ApplyPersonalRelief(spouse, reaction.RelationNudge);

         // Appreciations pillar: the resolver never actually assigns Gratitude to the spouse's own
         // verdict today (only Relief or PlantGrudge — see DivorceConsequencePolicy.ResolveSpouseReaction),
         // but the check is kept here too for symmetry with ApplyClanReaction and to stay correct should
         // that ever change.
         TryPlantDivorceGratitude(reaction.Kind, spouse, today);
      }

      /// <summary>
      ///   Conservative v1 "did these clans value the union as a political alliance" proxy (Gabriel,
      ///   04/07/2026): no dedicated alliance ledger exists yet, so the two spouses' clans are read as
      ///   having valued the match when they are the SAME clan or share a faction at divorce time — the
      ///   same same-faction pattern already used elsewhere in this codebase (SchemeStore, ContextualNameResolver,
      ///   LetterTriggerEvaluator) as an "allied" proxy. Symmetric by construction: the marriage was one
      ///   shared alliance, so both <see cref="DivorceFacts.SpouseClanValuedAlliance" /> and
      ///   <see cref="DivorceFacts.DivorcerClanValuedAlliance" /> read the same fact.
      /// </summary>
      private static bool ClansValuedTheAlliance(Hero npc, Hero spouse)
         => npc?.Clan != null &&
            spouse?.Clan != null &&
            (npc.Clan == spouse.Clan || npc.Clan.MapFaction != null && npc.Clan.MapFaction == spouse.Clan.MapFaction);

      private static int CurrentDay() => (int) CampaignTime.Now.ToDays;

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      /// <summary>
      ///   Appreciations pillar: when a divorce reaction's verdict is <see cref="ReactionKind.Gratitude" />
      ///   (never <see cref="ReactionKind.Relief" /> — Relief reads as too mild a feeling to remember as a
      ///   kindness in its own right, a judgment call), also plants a DivorceGratitude appreciation on
      ///   <paramref name="hero" />, hidden from the player (a family's private relief at an ended
      ///   marriage is not something they announce). Never lets a plant failure break the relation nudge
      ///   already applied.
      /// </summary>
      private static void TryPlantDivorceGratitude(ReactionKind kind, Hero hero, int today)
      {
         if (kind != ReactionKind.Gratitude || hero == null) return;

         try
         {
            AppreciationBehavior.Instance?.TryPlant(hero, Hero.MainHero, AppreciationSource.DivorceGratitude,
               "The player helped end a marriage my family never wanted.", DivorceGratitudeAppreciationMagnitude,
               false, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] divorce-gratitude plant failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void Advance(DivorceProcess process, int today)
      {
         if (process.State != DivorceProcessState.Active)
         {
            _active.Remove(process); // stray terminal entry — cleanup safety net

            return;
         }

         Hero npc = FindHero(process.NpcId);
         Hero spouse = FindHero(process.SpouseId);

         if (npc is not {IsAlive: true} || spouse is not {IsAlive: true})
         {
            _active.Remove(process); // one of the pair is gone — nothing left to see through

            return;
         }

         if (today >= process.EndDay)
         {
            Resolve(process, npc, spouse, today);

            return;
         }

         DivorceResistanceOutcome outcome = DivorcePolicy.RollResistance(
            TraitLevel(spouse, DefaultTraits.Honor), TraitLevel(spouse, DefaultTraits.Calculating),
            process.CumulativeDelayDays, MBRandom.RandomInt(100));

         switch (outcome)
         {
            case DivorceResistanceOutcome.Aborted:
               TerminateAborted(process, npc, spouse);

               break;
            case DivorceResistanceOutcome.Delayed:
               process.EndDay = DivorcePolicy.ExtendEndDay(process.EndDay);
               process.CumulativeDelayDays += DivorcePolicy.DelayDaysPerDelay;
               ModLog.Info($"[DIVORCE] {spouse.Name} resists — end day pushed to {process.EndDay} (cumulative delay {process.CumulativeDelayDays}).");

               break;
            // Continue: nothing to do this day.
         }
      }

      /// <summary>
      ///   Builds today's <see cref="DivorceFacts" /> from what the engine can observe, resolves the full
      ///   per-party matrix (<see cref="DivorceConsequencePolicy.Resolve" />), and applies each
      ///   <see cref="PartyReaction" />: <see cref="ReactionKind.PlantGrudge" /> plants a signed grudge
      ///   (<see cref="GrudgeBehavior.TryPlant" />), <see cref="ReactionKind.Relief" />/
      ///   <see cref="ReactionKind.Gratitude" /> apply an immediate positive relation nudge on top of the
      ///   baseline SET already applied by the caller, and <see cref="ReactionKind.None" /> is a no-op.
      ///   Runs inside the caller's <c>SafeRun.Step</c> guard, so this itself does not need its own
      ///   top-level try/catch, but still null-guards every optional collaborator (GrudgeBehavior.Instance,
      ///   clans, Hero.MainHero) since a divorce can complete even when one of those is unexpectedly absent.
      /// </summary>
      private void ApplyConsequenceMatrix(Hero npc,
                                          Hero spouse,
                                          bool mutualConsent,
                                          int spouseVsPlayerBefore,
                                          string setAsideCause,
                                          int today)
      {
         if (Hero.MainHero == null) return;

         bool alliedClans = ClansValuedTheAlliance(npc, spouse);
         var facts = new DivorceFacts {
            MutualConsent = mutualConsent,
            // Every vanilla marriage is blessed by default (Gabriel, 04/07/2026: "Le mariage est béni de
            // base en Vanille"); a future forced/unblessed-marriage feature would be the first to ever
            // set this false, which is the only thing that wakes UnblessedFamilyReaction up.
            MarriageWasBlessed = true,
            SpouseRegardForPlayer = spouseVsPlayerBefore,
            SpouseClanValuedAlliance = alliedClans,
            DivorcerClanValuedAlliance = alliedClans
         };

         DivorceConsequences consequences = DivorceConsequencePolicy.Resolve(facts);

         ApplySpouseReaction(consequences.SpouseReaction, spouse, setAsideCause, today);
         ApplyClanReaction(consequences.SpouseClanReaction, spouse.Clan, spouse, setAsideCause, today);
         ApplyClanReaction(consequences.DivorcerClanReaction, npc.Clan, npc, setAsideCause, today);

         // Dormant until unblessed marriages exist: MarriageWasBlessed is hardcoded true above, so this
         // reaction is always ReactionKind.None today and the loop below is a guaranteed no-op. The
         // spouse's own clan is the "family that never blessed it" once that fact can ever be false.
         ApplyClanReaction(consequences.UnblessedFamilyReaction, spouse.Clan, spouse, setAsideCause, today);
      }

      /// <summary>
      ///   Scandal world-event + NotableEvents on both profiles, the baseline relation SET (correcting for
      ///   whatever hidden penalty the engine applied during the act, so CR owns the emotional outcome), and
      ///   the FULL signed grudge matrix (Grudges pillar, <see cref="DivorceConsequencePolicy" />): the
      ///   set-aside spouse grudges or feels relief, and BOTH clans (the spouse's own and the divorcer's
      ///   own) either grudge a lost valued alliance or feel relief/gratitude to see it end. The baseline
      ///   SET and the matrix are two separate channels applied one after the other, never double-counted.
      /// </summary>
      private void ApplySuccessConsequences(Hero npc,
                                            Hero spouse,
                                            int today,
                                            bool mutualConsent,
                                            int spouseVsNpcBefore,
                                            int spouseVsPlayerBefore)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         // Exact marker text: GrudgeBackfillRules.FromProfileEvent (the "divorce" rule) matches this
         // precise prefix/suffix pair for old-save migration backfill — do not reword.
         var setAsideCause = $"Was set aside by {npc.Name}, the marriage dissolved at the player's urging.";

         if (store != null)
         {
            NpcProfile npcProfile = store.Get(npc.StringId) ?? HeroProfileMapper.FromHero(npc);
            npcProfile.Events.Add(new NotableEvent(today, NotableEventType.Agreement,
               $"The marriage to {spouse.Name} is dissolved, at the player's counsel."));
            store.Set(npcProfile);

            NpcProfile spouseProfile = store.Get(spouse.StringId) ?? HeroProfileMapper.FromHero(spouse);
            spouseProfile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal, setAsideCause));
            store.Set(spouseProfile);
         }

         // SET the target relations from the pre-dissolution baselines (baseline + signed hit), correcting
         // for whatever the engine already applied during the act itself. This channel is independent of
         // the grudge matrix below: it neutralizes the engine's own native penalty, the matrix is CR's own
         // additional layer of consequence on top of that corrected baseline.
         if (Hero.MainHero != null)
         {
            int playerTarget = spouseVsPlayerBefore + DivorcePolicy.ExSpousePlayerHit(mutualConsent);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, Hero.MainHero, playerTarget - spouse.GetRelation(Hero.MainHero), false);
         }

         int npcTarget = spouseVsNpcBefore + DivorcePolicy.ExSpouseNpcHit(mutualConsent);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, npc, npcTarget - spouse.GetRelation(npc), false);

         // Grudges pillar, first big client: the full signed per-party matrix replaces the old flat
         // interim single-plant + clan-hit loop. Guarded so a failure here never stops the scandal
         // world-event below it from being recorded.
         SafeRun.Step($"DivorceProcessBehavior.ApplyConsequenceMatrix({npc.StringId})",
            () => ApplyConsequenceMatrix(npc, spouse, mutualConsent, spouseVsPlayerBefore, setAsideCause, today));

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Scandal,
            Day = today,
            Magnitude = DivorcePolicy.ScaledScandalMagnitude(npc.Culture?.StringId),
            InvolvesPlayer = true,
            ActorIds = new List<string> {npc.StringId, spouse.StringId},
            FactionIds = new List<string>(new[] {npc.MapFaction?.StringId, spouse.MapFaction?.StringId}.Where(id => !string.IsNullOrEmpty(id))),
            Summary = $"Word spreads that {npc.Name} has set aside {spouse.Name}, dissolving their marriage.",
            ChronicleLine = $"at my urging, {npc.Name} broke faith with {spouse.Name} and dissolved their marriage, to both houses' outrage"
         });

         try { MBInformationManager.AddQuickInformation(new TextObject($"{npc.Name}'s marriage to {spouse.Name} has ended.")); }
         catch (Exception ex) { ModLog.Error($"[DIVORCE] quick notification failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnDailyTick()
      {
         if (_active.Count == 0) return;

         int today = CurrentDay();
         foreach (DivorceProcess p in _active.ToList())
         {
            DivorceProcess process = p;
            // One process's advance throwing must not skip every other process this tick.
            SafeRun.Step($"DivorceProcessBehavior.Advance({process.Id})", () => Advance(process, today));
         }
      }

      /// <summary>
      ///   The end day is reached: attempt the real dissolution and apply consequences, or fall through as a failed
      ///   process.
      /// </summary>
      private void Resolve(DivorceProcess process, Hero npc, Hero spouse, int today)
      {
         // Baselines BEFORE the dissolution: the engine adds its own hidden penalties during the act itself
         // (observed in game: the ex-spouse's regard for the player crashed to the -100 floor, -140 total,
         // when our own intended hit was -40). CR owns the outcome, so the consequences below SET the target
         // relation from these baselines rather than stacking a delta on an effect we do not control.
         // Mutual consent is read from the spouse's RESISTANCE (they never fought the estrangement =
         // acquiescence), since engine relations are symmetric and cannot say who was unhappy.
         int spouseVsNpcBefore = spouse.GetRelation(npc);
         int spouseVsPlayerBefore = Hero.MainHero != null
            ? spouse.GetRelation(Hero.MainHero)
            : 0;
         bool mutualConsent = DivorcePolicy.IsMutualConsent(process.CumulativeDelayDays);

         if (!MarriageDissolutionService.TryDissolve(npc, spouse, out string failReason))
         {
            ModLog.Info($"[DIVORCE] dissolution at deadline failed for {npc.Name} / {spouse.Name}: {failReason}.");
            TerminateAborted(process, npc, spouse);

            return;
         }

         process.State = DivorceProcessState.Fulfilled;
         _active.Remove(process);

         ApplySuccessConsequences(npc, spouse, today, mutualConsent, spouseVsNpcBefore, spouseVsPlayerBefore);
         ModLog.Info($"[DIVORCE] dissolved: {npc.Name} and {spouse.Name} are no longer wed" +
                     $"{(mutualConsent ? " (by mutual consent)" : " (repudiated)")}.");
      }

      /// <summary>The spouse's resistance won, or the dissolution guard failed at the deadline: the estrangement is off.</summary>
      private void TerminateAborted(DivorceProcess process, Hero npc, Hero spouse)
      {
         process.State = DivorceProcessState.Aborted;
         _active.Remove(process);

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(npc.StringId);

         if (profile != null)
         {
            profile.Events.Add(new NotableEvent(CurrentDay(), NotableEventType.Confrontation,
               $"The household closed ranks; the divorce from {spouse.Name} is off."));
            store.Set(profile);
         }

         if (Hero.MainHero != null)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, Hero.MainHero, DivorcePolicy.TerminatedNpcPlayerRelationHit, false);

         ModLog.Info($"[DIVORCE] aborted: {npc.Name}'s estrangement from {spouse.Name} failed.");
      }

      #endregion
   }
}