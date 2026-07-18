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
      ///   Tuning: at/above this regard for the set-aside spouse, a living hero is a genuine enough friend to count
      ///   toward their "three best friends" for <see cref="DivorceConsequences.FriendReaction" /> purposes — set
      ///   above <c>ContextualNameResolver</c>'s own "friend (genuine regard)" bar of 10, since these three are meant
      ///   to be the CLOSEST bonds, not merely cordial acquaintances.
      /// </summary>
      private const int BestFriendThreshold = 20;

      /// <summary>
      ///   Appreciations pillar: the fixed formation magnitude for a DivorceGratitude plant — comparable to
      ///   AppreciationBackfillRules.KindnessInSceneMagnitude.
      /// </summary>
      private const int DivorceGratitudeAppreciationMagnitude = 30;

      /// <summary>Tuning: at most this many of the set-aside spouse's best friends take up a hidden grudge on their behalf.</summary>
      private const int FriendTierSize = 3;

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
         // M-D3: lock by the PAIR (see DivorceProcess.PairAlreadyEngaged), not the requester alone. If either
         // party is already in a live process, return THAT one instead of opening a second crossed process that
         // would resolve on a false "household closed ranks" and dock the player's relation. The caller's
         // HasActive(requester) gate does not catch this, since the second persuasion has a different requester.
         if (DivorceProcess.PairAlreadyEngaged(_active, npcId, spouseId))
         {
            DivorceProcess existing = _active.First(p => p.State == DivorceProcessState.Active
               && (p.NpcId == npcId || p.SpouseId == npcId || p.NpcId == spouseId || p.SpouseId == spouseId));
            ModLog.Info($"[DIVORCE] a process already involves {npcId} or {spouseId}; not opening a second (pair lock).");

            return existing;
         }

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
      ///   One hero's verdict: a grudge (<see cref="GrudgeBehavior.TryPlant" />), or a relief/gratitude relation nudge
      ///   plus its matching Appreciations-pillar plant. Shared by every member of both
      ///   <see cref="DivorceConsequences.DirectFamilyReaction" />'s family set and
      ///   <see cref="DivorceConsequences.FriendReaction" />'s friend set — the per-member body is identical, only
      ///   which hero and which reaction differ.
      /// </summary>
      private static void ApplyReactionTo(PartyReaction reaction, Hero hero, string cause, int today)
      {
         if (reaction == null || hero == null || Hero.MainHero == null) return;

         if (reaction.Kind == ReactionKind.PlantGrudge)
            GrudgeBehavior.Instance?.TryPlant(hero, Hero.MainHero, GrudgeSource.Divorce, cause,
               reaction.GrudgeMagnitude, reaction.KnownToPlayer, today);
         else if (reaction.RelationNudge != 0)
            ApplyPersonalRelief(hero, reaction.RelationNudge);

         // Appreciations pillar: a hero whose Gratitude verdict fires (an unwanted marriage/alliance
         // finally over) also FEELS it as a lingering kindness, a channel separate from the personal
         // relation nudge above.
         TryPlantDivorceGratitude(reaction.Kind, hero, today);
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
         // but the check is kept here too for symmetry with ApplyReactionTo and to stay correct should
         // that ever change.
         TryPlantDivorceGratitude(reaction.Kind, spouse, today);
      }

      private static int CurrentDay() => (int) CampaignTime.Now.ToDays;

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      /// <summary>
      ///   The set-aside spouse's immediate ADULT family (father, mother, siblings, children), resolved via the
      ///   reliable Hero family API rather than <c>OriginClan</c> (a clan can hold members far beyond the nuclear
      ///   family). Excludes the player, the divorcer, the spouse themselves, the dead, and children too young to
      ///   hold a grudge (<see cref="Hero.IsChild" /> — the same adult gate <c>CompanionRapportBehavior</c> already
      ///   uses elsewhere in this codebase), and dedups (a sibling who is also, say, a shared parent's other child
      ///   would otherwise appear twice).
      /// </summary>
      private static List<Hero> ResolveDirectFamily(Hero spouse, Hero npc)
      {
         var result = new List<Hero>();

         if (spouse == null) return result;

         var candidates = new List<Hero>();
         if (spouse.Father != null) candidates.Add(spouse.Father);
         if (spouse.Mother != null) candidates.Add(spouse.Mother);
         if (spouse.Siblings != null) candidates.AddRange(spouse.Siblings);
         if (spouse.Children != null) candidates.AddRange(spouse.Children);

         var seenIds = new HashSet<string>();
         foreach (Hero member in candidates)
         {
            if (member == null || member == npc || member == spouse || member == Hero.MainHero) continue;
            if (!member.IsAlive || member.IsChild) continue;
            if (!seenIds.Add(member.StringId)) continue;

            result.Add(member);
         }

         return result;
      }

      /// <summary>
      ///   The set-aside spouse's THREE BEST living friends: the top <see cref="FriendTierSize" /> heroes by regard
      ///   FOR THE SPOUSE (not for the player) that clear <see cref="BestFriendThreshold" />, excluding the player,
      ///   the divorcer, the spouse themselves, and anyone already counted in <paramref name="directFamily" /> (a
      ///   dedup guard only — <see cref="GrudgeBehavior.TryPlant" /> already dedups by holder+source+day, so a
      ///   family member who also clears the friend bar would not double-grudge even without this).
      /// </summary>
      private static List<Hero> ResolveThreeBestFriends(Hero spouse, Hero npc, IReadOnlyCollection<Hero> directFamily)
      {
         if (spouse == null) return new List<Hero>();

         var familyIds = new HashSet<string>(directFamily.Select(h => h.StringId));

         return Hero.AllAliveHeroes
                    .Where(h => h != null && h != spouse && h != npc && h != Hero.MainHero && !familyIds.Contains(h.StringId))
                    .Select(h => new {Hero = h, Regard = h.GetRelation(spouse)})
                    .Where(x => x.Regard >= BestFriendThreshold)
                    .OrderByDescending(x => x.Regard)
                    .Take(FriendTierSize)
                    .Select(x => x.Hero)
                    .ToList();
      }

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
      ///   Hero.MainHero) since a divorce can complete even when one of those is unexpectedly absent.
      ///   <paramref name="npc" /> is whoever ended the marriage (an NPC, Phase 1, or the player
      ///   themselves, Phase 2a) — only ever used here to EXCLUDE them from the family/friends circles, so
      ///   passing <c>Hero.MainHero</c> works exactly as well as any third-party NPC.
      /// </summary>
      private void ApplyConsequenceMatrix(Hero npc,
                                          Hero spouse,
                                          bool mutualConsent,
                                          int spouseVsPlayerBefore,
                                          string setAsideCause,
                                          int today)
      {
         if (Hero.MainHero == null) return;

         var facts = new DivorceFacts {
            MutualConsent = mutualConsent,
            // Every vanilla marriage is blessed by default (Gabriel, 04/07/2026: "Le mariage est béni de
            // base en Vanille"); a future forced/unblessed-marriage feature would be the first to ever
            // set this false, which is the only thing that wakes the family's gratitude fold-in up.
            MarriageWasBlessed = true,
            SpouseRegardForPlayer = spouseVsPlayerBefore
         };

         DivorceConsequences consequences = DivorceConsequencePolicy.Resolve(facts);

         ApplySpouseReaction(consequences.SpouseReaction, spouse, setAsideCause, today);

         // Owner ruling 04/07/2026: a repudiated divorce grudges the wronged spouse's IMMEDIATE FAMILY and
         // THREE BEST FRIENDS, not the whole clan (an extended-clan grudge would mostly fall below the
         // formation floor anyway). Resolve both circles live off the spouse, apply per member.
         List<Hero> directFamily = ResolveDirectFamily(spouse, npc);
         foreach (Hero member in directFamily)
            ApplyReactionTo(consequences.DirectFamilyReaction, member, setAsideCause, today);

         List<Hero> bestFriends = ResolveThreeBestFriends(spouse, npc, directFamily);
         foreach (Hero friend in bestFriends)
            ApplyReactionTo(consequences.FriendReaction, friend, setAsideCause, today);

         // Grudges pillar (UnblessedMarriage): only ever planted for the PLAYER's own marriage
         // (MarriageApprovalBehavior.OnBeforeHeroesMarried), so only clear it when the player is actually
         // a party to THIS divorce (npc is the Phase 2a divorcer, which is the player for end_own_marriage;
         // the Phase 1 NPC-NPC case never touches the player's own spouse, but the guard is kept anyway so
         // an unrelated NPC-NPC split can never wipe the player's own still-standing in-law grudge).
         if (npc == Hero.MainHero || spouse == Hero.MainHero)
            SafeRun.Step("DivorceProcessBehavior.ClearUnblessedMarriageGrudges",
               () => GrudgeBehavior.Instance?.ResolveUnblessedMarriageGrudgesAgainst(Hero.MainHero.StringId, today));
      }

      /// <summary>
      ///   Divorce, Phase 2a's shared engine: applies the SAME tiered consequence model Phase 1
      ///   (<see cref="Resolve" />/<see cref="ApplySuccessConsequences" />) uses for the NPC-NPC case, so
      ///   the player's own end_own_marriage action
      ///   (<see cref="GameState.BannerlordGameStateBridge" />) never duplicates the family/friends
      ///   resolution or the grudge planting. SETs the dissolved marriage's baseline relation between the
      ///   set-aside <paramref name="spouse" /> and whoever ended it (<paramref name="divorcer" /> — an NPC
      ///   for Phase 1, or the player for Phase 2a), correcting for whatever hidden penalty the engine's
      ///   own <c>Hero.Spouse = null</c> already applied, then resolves and applies the full
      ///   <see cref="DivorceConsequencePolicy" /> matrix on top: the spouse's own verdict, their immediate
      ///   adult family, and their three best friends. <paramref name="spouseVsDivorcerBefore" /> and
      ///   <paramref name="spouseVsPlayerBefore" /> are the PRE-dissolution baselines; when
      ///   <paramref name="divorcer" /> IS the player these are the same number, and the method still does
      ///   the right thing (a single relation channel, no separate third party to blame). Guarded so a
      ///   partial failure here never throws into either caller.
      /// </summary>
      internal void ApplyDivorceConsequences(Hero divorcer,
                                             Hero spouse,
                                             bool mutualConsent,
                                             int spouseVsDivorcerBefore,
                                             int spouseVsPlayerBefore,
                                             string setAsideCause,
                                             int today)
      {
         if (divorcer == null || spouse == null) return;

         int divorcerTarget = spouseVsDivorcerBefore + DivorcePolicy.ExSpouseNpcHit(mutualConsent);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, divorcer, divorcerTarget - spouse.GetRelation(divorcer), false);

         SafeRun.Step($"DivorceProcessBehavior.ApplyConsequenceMatrix({divorcer.StringId})",
            () => ApplyConsequenceMatrix(divorcer, spouse, mutualConsent, spouseVsPlayerBefore, setAsideCause, today));
      }

      /// <summary>
      ///   Scandal world-event + NotableEvents on both profiles, the baseline relation SET (correcting for
      ///   whatever hidden penalty the engine applied during the act, so CR owns the emotional outcome), and
      ///   the FULL signed grudge matrix (Grudges pillar, <see cref="DivorceConsequencePolicy" />): the
      ///   set-aside spouse grudges or feels relief, and their immediate family and three best friends either
      ///   grudge the lost match on the spouse's behalf or feel relief/gratitude to see it end. The baseline
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
         // the shared consequence engine below: it neutralizes the engine's own native penalty toward the
         // PLAYER specifically (npc != player here — the player is a third party blamed for orchestrating
         // the split, a harsher/milder hit than the npc's own, see DivorcePolicy.ExSpousePlayerHit's doc).
         if (Hero.MainHero != null)
         {
            int playerTarget = spouseVsPlayerBefore + DivorcePolicy.ExSpousePlayerHit(mutualConsent);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, Hero.MainHero, playerTarget - spouse.GetRelation(Hero.MainHero), false);
         }

         // Grudges pillar, first big client: the full signed per-party matrix replaces the old flat
         // interim single-plant + clan-hit loop. Shared with Phase 2a (the player's own end_own_marriage)
         // via ApplyDivorceConsequences, so neither path duplicates the family/friends resolution or the
         // grudge planting. Guarded so a failure here never stops the scandal world-event below it from
         // being recorded.
         ApplyDivorceConsequences(npc, spouse, mutualConsent, spouseVsNpcBefore, spouseVsPlayerBefore, setAsideCause, today);

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
         bool mutualConsent = DivorcePolicy.IsMutualConsent(process.CumulativeDelayDays, spouseVsPlayerBefore);

         if (!MarriageDissolutionService.TryDissolve(npc, spouse, out string failReason))
         {
            // M-D6: a pregnancy or captivity in play is TRANSIENT, not the household resisting; the marriage
            // simply cannot dissolve yet. Defer and retry once it clears (birth, release, or death; a death is
            // caught by Advance's alive-guard, so this never loops forever) instead of a false "closed ranks"
            // abort that would cost the player an unearned relation hit. A conception early in a 15-30 day
            // process would otherwise GUARANTEE that false failure, since a pregnancy runs far longer.
            if (MarriageDissolutionService.ClassifyBlock(npc, spouse) == DivorceBlock.Transient)
            {
               process.EndDay = DivorcePolicy.ExtendEndDay(process.EndDay);
               ModLog.Info($"[DIVORCE] {npc.Name} / {spouse.Name}: dissolution deferred ({failReason}); end day pushed to {process.EndDay}, no penalty.");

               return;
            }

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