// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1 (the mechanical spine): the TrackedCommitment engine. The
// flagship kind is a lord pledging to move against a rival — powered by the existing (CR-owned,
// deterministic) schemes system. A pledge is a POLITICAL commitment only: it launches a scheme
// (slander/sabotage), never a war. Fulfillment is verified two ways (the scheme resolving in the
// promisor's favour, or any real hostile act between the promisor and the target); a breach is
// verified by the deadline lapsing with neither. Persisted as a UTF-8 byte[] (mirrors SchemeStore /
// CompanionMissionBehavior) with the same "never brick a save" degradation.
// 04/08/2026: R7-light (COUNCIL_ACTIONS.md's swear_oath, Partie 8) generalizes this engine to THREE more
// kinds (PayGold, KeepPeace, Protect), each verified by its OWN real event: OnGoldGivenToPlayer (called
// from both give_gold paths, 1:1 and council) for PayGold, OnWarDeclared (a new CampaignEvents listener)
// for KeepPeace's immediate breach, and CheckProtectFulfillment (extending the existing OnMapEventEnded
// hook) for Protect. KeepPeace is the one kind where reaching the deadline unbroken IS the fulfillment
// (CommitmentPolicy.OnOverdue), the exact inverse of every other kind's "silence is failure". A publicly
// sworn oath (WitnessIds set) costs more to break: CommitmentPolicy.WitnessBreachPenalty stacks onto the
// usual BreachedRelationPenalty, and a GrudgeSource.BrokenOath is planted through the real TryPlant gate.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Commitments
{
   /// <summary>
   ///   Tracks every outstanding <see cref="TrackedCommitment" /> and advances it: a daily sweep retires
   ///   an overdue pledge as Breached, <see cref="OnSchemeResolved" /> is called by
   ///   <see cref="WorldEvents.SchemeStore" /> when a launched scheme succeeds, and
   ///   <see cref="OnMapEventEnded" /> recognizes an organic battle between the promisor and the target as
   ///   fulfillment too (it never causes one — see the guardrail in <see cref="ExecutePledgeAgainst" />
   ///   equivalent at <c>BannerlordGameStateBridge</c>). Phase 1 only: no journal mirror, no console
   ///   commands, no self-tests — those are a later phase; <see cref="Active" /> and each commitment's
   ///   stable <see cref="TrackedCommitment.Id" /> are the seam left for them.
   /// </summary>
   public sealed class CommitmentBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_Commitments_v1bytes";

      public static CommitmentBehavior Instance;

      private List<TrackedCommitment> _active = new();

      public CommitmentBehavior()
      {
         Instance = this;
      }

      /// <summary>The pledges currently outstanding — for the later journal/console phase.</summary>
      public IReadOnlyList<TrackedCommitment> Active => _active;

      /// <summary>True when this promisor already has a pledge in flight — a lord vows one thing at a time.</summary>
      public bool HasActive(string promisorId)
         => !string.IsNullOrEmpty(promisorId) && _active.Any(c => c.PromisorId == promisorId && c.State == CommitmentState.Active);

      /// <summary>
      ///   Records a new pledge, deadline computed by <see cref="CommitmentPolicy" /> from today. The
      ///   caller (the bridge, re-validating every guard) has already launched the underlying scheme.
      ///   Opens the vanilla quest-journal mirror when a live promisor Hero can be resolved
      ///   (<see cref="Commitments.CommitmentJournalBridge" /> is itself defensive, so a missing/invalid
      ///   promisor never blocks the pledge from being recorded).
      ///   <paramref name="kind" /> defaults to MoveAgainst (pledge_against's own flagship kind, unchanged
      ///   for its existing caller, <see cref="GameState.CourtActionResolver.LaunchPledge" />).
      ///   <paramref name="promisedAmount" /> is R7-light's own PayGold payload (the denars still owed);
      ///   <paramref name="witnessIds" /> are the OTHER seats present when a swear_oath was struck at a
      ///   council table (null for a private pledge, e.g. pledge_against's own 1:1 or council path, which
      ///   never carries witnesses onto the commitment itself).
      /// </summary>
      public TrackedCommitment Create(string promisorId, string targetId, int issuedDay,
         CommitmentKind kind = CommitmentKind.MoveAgainst, int? promisedAmount = null, List<string> witnessIds = null)
      {
         var commitment = new TrackedCommitment {
            Id = Guid.NewGuid().ToString("N"),
            PromisorId = promisorId,
            TargetId = targetId,
            Kind = kind,
            IssuedDay = issuedDay,
            DeadlineDay = CommitmentPolicy.ComputeDeadline(issuedDay),
            State = CommitmentState.Active,
            PromisedAmount = promisedAmount,
            WitnessIds = witnessIds
         };
         _active.Add(commitment);
         ModLog.Info($"[COMMITMENT] pledged: promisor={promisorId} target={targetId} kind={kind} deadline day {commitment.DeadlineDay}.");

         CommitmentJournalBridge.OpenMirror(commitment, FindHero(promisorId));

         return commitment;
      }

      /// <summary>Runs one breach-sweep pass immediately (console/self-test only) — mirrors <see cref="WorldEvents.SchemeStore.DebugAdvance" />.</summary>
      public void DebugRunBreachSweep() => OnDailyTick();

      /// <summary>
      ///   Called by <see cref="WorldEvents.SchemeStore" /> when a launched scheme resolves. A SUCCEEDED
      ///   scheme between a matching promisor/target pair fulfills the pledge; a FAILED scheme is not a
      ///   breach by itself (the lord genuinely tried) — the commitment stays Active until its own
      ///   deadline. Defensive by design: never throws into the scheme pipeline that calls it.
      /// </summary>
      public void OnSchemeResolved(string plotterId, string targetId, bool succeeded)
      {
         try
         {
            if (!succeeded || string.IsNullOrEmpty(plotterId) || string.IsNullOrEmpty(targetId)) return;

            TrackedCommitment commitment = FindActivePledge(plotterId, targetId);
            if (commitment == null) return;

            Fulfill(commitment, "the scheme they set in motion came to fruition");
         }
         catch (Exception ex) { ModLog.Error($"[COMMITMENT] OnSchemeResolved failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   PayGold's own verification hook (R7-light, COUNCIL_ACTIONS.md's swear_oath): called from BOTH
      ///   gold-giving paths (<see cref="GameState.BannerlordGameStateBridge.ExecuteAction" />'s 1:1
      ///   give_gold, and the council's own <c>CouncilLift.SettleGiveGold</c>) right after the real
      ///   <c>GiveGoldAction.ApplyBetweenCharacters</c> call, so ANY channel of payment counts toward the
      ///   oath, never just one. Reduces the FIRST active PayGold commitment's <see cref="TrackedCommitment.PromisedAmount" />
      ///   by <paramref name="amount" /> (a partial payment simply lowers what is still owed); once it
      ///   reaches zero or below, the oath is fulfilled. Defensive by design: never throws into the gold
      ///   pipeline that calls it.
      /// </summary>
      public void OnGoldGivenToPlayer(string giverId, int amount)
      {
         try
         {
            if (string.IsNullOrEmpty(giverId) || amount <= 0) return;

            TrackedCommitment commitment = _active.FirstOrDefault(c =>
               c.State == CommitmentState.Active && c.Kind == CommitmentKind.PayGold && c.PromisorId == giverId);
            if (commitment == null) return;

            int remaining = (commitment.PromisedAmount ?? 0) - amount;

            if (remaining > 0)
            {
               commitment.PromisedAmount = remaining;
               ModLog.Info($"[COMMITMENT] partial payment: promisor={giverId} paid {amount}, {remaining} still owed.");

               return;
            }

            Fulfill(commitment, "the promised sum was paid in full");
         }
         catch (Exception ex) { ModLog.Error($"[COMMITMENT] OnGoldGivenToPlayer failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   KeepPeace's own verification hook (R7-light): a war declared BETWEEN the two factions the oath
      ///   named breaches it IMMEDIATELY, never waiting for the daily sweep (the sweep instead treats
      ///   reaching the deadline with no such war as the oath honored, see <see cref="CommitmentPolicy.OnOverdue" />).
      ///   Matches on the promisor's CURRENT faction (re-resolved live, "bridge is law": the promisor may
      ///   have changed clans since swearing) against either side of the declaration. Defensive by design.
      /// </summary>
      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
      {
         if (_active.Count == 0 || faction1 == null || faction2 == null) return;

         foreach (TrackedCommitment c in _active.Where(x => x.State == CommitmentState.Active && x.Kind == CommitmentKind.KeepPeace).ToList())
         {
            TrackedCommitment commitment = c;
            SafeRun.Step($"CommitmentBehavior.CheckKeepPeaceBreach({commitment.Id})", () => CheckKeepPeaceBreach(commitment, faction1, faction2));
         }
      }

      private void CheckKeepPeaceBreach(TrackedCommitment commitment, IFaction faction1, IFaction faction2)
      {
         Hero promisor = FindHero(commitment.PromisorId);
         IFaction promisorFaction = promisor?.MapFaction;
         if (promisorFaction == null) return;

         bool nameFaction1 = string.Equals(faction1.StringId, commitment.TargetId, StringComparison.Ordinal);
         bool nameFaction2 = string.Equals(faction2.StringId, commitment.TargetId, StringComparison.Ordinal);
         if (!nameFaction1 && !nameFaction2) return;

         bool promisorInvolved = promisorFaction == faction1 || promisorFaction == faction2;
         if (!promisorInvolved) return;

         Breach(commitment, "their own faction declared war on the very house they swore to keep the peace with");
      }

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         // R7-light: KeepPeace's own immediate-breach hook (a war declared is caught the moment it happens,
         // never waiting for the next daily sweep).
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
      }

      // ── Persistence (mirrors SchemeStore / CompanionMissionBehavior) ──────

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list: an autosave racing a main-thread mutation
               // would otherwise throw mid-serialize and drop the whole blob this save.
               var snapshot = new List<TrackedCommitment>(_active);
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[COMMITMENT] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[COMMITMENT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
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
               try { _active = JsonConvert.DeserializeObject<List<TrackedCommitment>>(Encoding.UTF8.GetString(bytes)) ?? new List<TrackedCommitment>(); }
               catch (Exception ex)
               {
                  _active = new List<TrackedCommitment>();
                  ModLog.Error($"[COMMITMENT] load failed — pledges dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            ModLog.Info($"[COMMITMENT] loaded {_active.Count} outstanding pledge(s) from save.");
         }
      }

      #region private

      private static int ClampInt(int value, int min, int max)
         => value < min ? min : value > max ? max : value;

      private static int CurrentDay() => (int) CampaignTime.Now.ToDays;

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      /// <summary>True when any party on this side is either LED by, or belongs to the faction of, the given hero/faction.</summary>
      private static bool SideHasHeroOrFaction(MBReadOnlyList<MapEventParty> parties, string heroId, string factionId)
      {
         if (parties == null) return false;

         foreach (MapEventParty mp in parties)
         {
            PartyBase p = mp.Party;
            if (p == null) continue;

            if (!string.IsNullOrEmpty(heroId) && p.LeaderHero?.StringId == heroId) return true;
            if (!string.IsNullOrEmpty(factionId) && p.MapFaction?.StringId == factionId) return true;
         }

         return false;
      }

      /// <summary>
      ///   The R7-light kinds' own display name for a commitment's target: a rival Hero for MoveAgainst, a
      ///   faction for KeepPeace, or null (unused) for PayGold/Protect. Internal (not private): reused by
      ///   <see cref="CommitmentJournalBridge" /> so the vanilla quest-journal mirror's own text stays kind-aware
      ///   too, never just the profile-event/world-event text this class itself writes.
      /// </summary>
      internal static string ResolveTargetDisplayName(TrackedCommitment commitment)
      {
         switch (commitment.Kind)
         {
            case CommitmentKind.MoveAgainst:
               return FindHero(commitment.TargetId)?.Name?.ToString();
            case CommitmentKind.KeepPeace:
               return FindFactionById(commitment.TargetId)?.Name?.ToString();
            default:
               return null;
         }
      }

      /// <summary>Applies the fulfilled consequence and retires the pledge. Shared by every verification path (scheme resolved, real battle, gold paid, or deadline reached unbroken for KeepPeace).</summary>
      private void Fulfill(TrackedCommitment commitment, string reasonForLog)
      {
         Hero promisor = FindHero(commitment.PromisorId);
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(commitment.PromisorId);

         if (profile == null)
         {
            _active.Remove(commitment); // no profile to credit — quietly retire rather than leave it dangling

            return;
         }

         commitment.State = CommitmentState.Fulfilled;

         int before = profile.ReputationWithPlayer;
         int after = ClampInt(before + CommitmentPolicy.FulfilledRelationBonus, -100, 100);
         profile.ReputationWithPlayer = after;
         profile.LastRelationGainHour = CampaignTime.Now.ToHours;
         StanceService.OnPromiseKept(profile); // Trust + a little Respect — a kept oath

         var today = CurrentDay();
         string targetName = ResolveTargetDisplayName(commitment);

         if (commitment.Kind == CommitmentKind.MoveAgainst)
         {
            // UNCHANGED: pledge_against's own original fulfillment text/world-event, exactly as shipped;
            // the R7-light kinds below get their own generic wording instead of reusing "moved against".
            string rivalName = targetName ?? "their rival";
            profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
               $"Honored the pledge to move against {rivalName}."));
            store.Set(profile);

            // The pledge only ever exists because the player asked for it (pledge_against), so its
            // fulfillment is chronicle material, the same event enriched with InvolvesPlayer + a
            // ChronicleLine, not a second record (the breach sweep's own event is left as third-party
            // rumor; a broken oath is not the player's deed to claim credit for).
            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Rivalry,
               Day = today,
               InvolvesPlayer = true,
               Magnitude = 24,
               Status = "pledge_fulfilled",
               Summary = $"{promisor?.Name?.ToString() ?? "A lord"} honored a pledge and moved against {rivalName}, as promised.",
               ActorIds = new List<string> {commitment.PromisorId, commitment.TargetId},
               FactionIds = promisor?.MapFaction != null
                  ? new List<string> {promisor.MapFaction.StringId}
                  : new List<string>(),
               ChronicleLine = $"at {(Hero.MainHero?.Name?.ToString() ?? "the player")}'s word, {promisor?.Name?.ToString() ?? "a lord"} moved against {rivalName}, and kept faith"
            });
         }
         else
         {
            // R7-light (swear_oath): the three whitelisted oath kinds share ONE generic wording path,
            // driven by CommitmentJournalText's kind-aware builders (amount only meaningful for PayGold).
            string oathLine = CommitmentJournalText.BuildFulfilledLine(commitment.Kind, promisor?.Name?.ToString(), targetName, commitment.PromisedAmount);
            profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration, oathLine));
            store.Set(profile);

            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Other,
               Day = today,
               InvolvesPlayer = true,
               Magnitude = 20,
               Status = "oath_fulfilled",
               Summary = oathLine,
               ActorIds = new List<string> {commitment.PromisorId},
               FactionIds = promisor?.MapFaction != null
                  ? new List<string> {promisor.MapFaction.StringId}
                  : new List<string>(),
               ChronicleLine = $"at {(Hero.MainHero?.Name?.ToString() ?? "the player")}'s word, {oathLine}"
            });
         }

         CommitmentJournalBridge.CloseFulfilled(commitment);

         _active.Remove(commitment);
         ModLog.Info($"[COMMITMENT] fulfilled: {promisor?.Name} kind={commitment.Kind} ({reasonForLog}).");
      }

      /// <summary>
      ///   Applies the broken-oath consequence and retires the pledge: the mirror of <see cref="Fulfill" />,
      ///   shared by the deadline sweep (<see cref="CheckOverdue" />) and KeepPeace's own immediate breach
      ///   (<see cref="CheckKeepPeaceBreach" />). R7-light's own addition beyond the original MoveAgainst-only
      ///   penalty: a PUBLICLY sworn oath (<see cref="TrackedCommitment.WitnessIds" /> non-empty) costs MORE,
      ///   <see cref="CommitmentPolicy.WitnessBreachPenalty" /> stacks onto the base
      ///   <see cref="CommitmentPolicy.BreachedRelationPenalty" /> against the SAME player-regard ledger
      ///   (ReputationWithPlayer), plus a <see cref="GrudgeSource.BrokenOath" /> planted through the real
      ///   <c>GrudgeBehavior.TryPlant</c> gate (never a bare relation hit, the same anti-noise/rigidity rules
      ///   every other tier-2 source respects). Design call: the "cost with each witness" COUNCIL_ACTIONS.md
      ///   describes is modelled here as a heavier hit to the ONE ledger the player can actually see
      ///   (ReputationWithPlayer/the encyclopedia), rather than a scatter of vanilla NPC-to-NPC relation edits
      ///   between third parties the player never directly observes.
      /// </summary>
      private void Breach(TrackedCommitment commitment, string reasonForLog)
      {
         Hero promisor = FindHero(commitment.PromisorId);
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(commitment.PromisorId);

         if (profile == null)
         {
            _active.Remove(commitment); // no profile to charge, quietly retire rather than leave it dangling

            return;
         }

         var today = CurrentDay();
         string targetName = ResolveTargetDisplayName(commitment);
         commitment.State = CommitmentState.Breached;

         int witnessCount = commitment.WitnessIds?.Count(id => !string.IsNullOrEmpty(id)) ?? 0;
         int penalty = CommitmentPolicy.BreachedRelationPenalty + CommitmentPolicy.WitnessBreachPenalty(witnessCount);

         int before = profile.ReputationWithPlayer;
         int after = ClampInt(before + penalty, -100, 100);
         profile.ReputationWithPlayer = after;
         StanceService.OnPromiseBroken(profile); // a real trust crater

         if (witnessCount > 0 && promisor is {IsAlive: true})
            GrudgeBehavior.Instance?.TryPlant(promisor, Hero.MainHero, GrudgeSource.BrokenOath,
               "swore an oath before the table and broke it", CommitmentPolicy.BrokenOathGrudgeMagnitude,
               knownToPlayer: true, today);

         string oathLine;
         WorldEventType worldEventType;
         string status;

         if (commitment.Kind == CommitmentKind.MoveAgainst)
         {
            // UNCHANGED: pledge_against's own original breach text/world-event, exactly as shipped.
            string rivalName = targetName ?? "their rival";
            oathLine = $"Broke the pledge to move against {rivalName}, and did nothing.";
            worldEventType = WorldEventType.Rivalry;
            status = "pledge_breached";
         }
         else
         {
            oathLine = CommitmentJournalText.BuildBreachedLine(commitment.Kind, promisor?.Name?.ToString(), targetName, commitment.PromisedAmount);
            worldEventType = WorldEventType.Other;
            status = "oath_breached";
         }

         profile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal, oathLine));
         store.Set(profile);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = worldEventType,
            Day = today,
            InvolvesPlayer = false,
            Magnitude = 24,
            Status = status,
            Summary = $"It is said that {promisor?.Name?.ToString() ?? "a lord"} {oathLine.ToLowerInvariant()}",
            // TargetId is unused (null) for PayGold/Protect: never add a null entry to the actor list.
            ActorIds = string.IsNullOrEmpty(commitment.TargetId)
               ? new List<string> {commitment.PromisorId}
               : new List<string> {commitment.PromisorId, commitment.TargetId},
            FactionIds = promisor?.MapFaction != null
               ? new List<string> {promisor.MapFaction.StringId}
               : new List<string>()
         });

         CommitmentJournalBridge.CloseBreached(commitment);

         _active.Remove(commitment);
         ModLog.Info($"[COMMITMENT] breached: {promisor?.Name} kind={commitment.Kind} witnesses={witnessCount} penalty={penalty} (deadline day {commitment.DeadlineDay}, today {today}, {reasonForLog}).");
      }

      /// <summary>The Active MoveAgainst pledge between this exact promisor/target pair, or null.</summary>
      private TrackedCommitment FindActivePledge(string promisorId, string targetId)
         => _active.FirstOrDefault(c =>
            c.State == CommitmentState.Active && c.Kind == CommitmentKind.MoveAgainst &&
            c.PromisorId == promisorId && c.TargetId == targetId);

      /// <summary>Resolves a faction (Kingdom or independent Clan) by its live StringId: KeepPeace's own TargetId lookup.</summary>
      private static IFaction FindFactionById(string id)
      {
         if (string.IsNullOrEmpty(id)) return null;

         IFaction kingdom = Kingdom.All?.FirstOrDefault(k => k.StringId == id);
         if (kingdom != null) return kingdom;

         return Clan.All?.FirstOrDefault(c => c.StringId == id);
      }

      // ── Breach (daily overdue sweep) ────────────────────────────────────────

      private void OnDailyTick()
      {
         if (_active.Count == 0) return;

         int today = CurrentDay();
         foreach (TrackedCommitment c in _active.ToList())
         {
            TrackedCommitment commitment = c;
            // One pledge's check throwing must not skip every other pledge this tick.
            SafeRun.Step($"CommitmentBehavior.CheckOverdue({commitment.Id})", () => CheckOverdue(commitment, today));
         }
      }

      private void CheckOverdue(TrackedCommitment commitment, int today)
      {
         if (commitment.State != CommitmentState.Active)
         {
            _active.Remove(commitment); // stray terminal entry — cleanup safety net

            return;
         }

         if (!CommitmentPolicy.IsOverdue(today, commitment.DeadlineDay)) return;

         Hero promisor = FindHero(commitment.PromisorId);

         if (promisor is not {IsAlive: true})
         {
            _active.Remove(commitment); // the promisor is gone — nothing to hold them to

            return;
         }

         // R7-light: KeepPeace is the ONE kind whose silence at the deadline is a KEPT oath (the vow was
         // never to go to war, and a war is caught immediately by OnWarDeclared, not here); every other
         // kind's silence at the deadline is a BROKEN one, the original rule this sweep always enforced.
         if (CommitmentPolicy.OnOverdue(commitment.Kind) == OverdueOutcome.Fulfill)
            Fulfill(commitment, "the deadline passed with the peace unbroken");
         else
            Breach(commitment, "the deadline passed with nothing done");
      }

      // ── Fulfillment via any real hostile act (recognizes, never causes) ────

      private void CheckBattleFulfillment(TrackedCommitment commitment, MapEvent mapEvent)
      {
         if (commitment.State != CommitmentState.Active) return;

         Hero target = FindHero(commitment.TargetId);
         if (target == null) return;

         MBReadOnlyList<MapEventParty> attackers = mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.Parties;
         MBReadOnlyList<MapEventParty> defenders = mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.Parties;

         bool promisorAttacker = SideHasHeroOrFaction(attackers, commitment.PromisorId, null);
         bool promisorDefender = SideHasHeroOrFaction(defenders, commitment.PromisorId, null);
         if (!promisorAttacker && !promisorDefender) return;

         string targetFactionId = target.MapFaction?.StringId;
         bool targetOnOppositeSide = promisorAttacker
            ? SideHasHeroOrFaction(defenders, commitment.TargetId, targetFactionId)
            : SideHasHeroOrFaction(attackers, commitment.TargetId, targetFactionId);

         if (!targetOnOppositeSide) return;

         Fulfill(commitment, "a real clash between them settled it");
      }

      /// <summary>
      ///   Protect's own verification hook (R7-light): fulfilled the moment a real MapEvent ends with the
      ///   promisor and the PLAYER's own party fighting on the SAME side: the promisor genuinely stood at
      ///   the player's side in battle, exactly what was sworn. Never causes a fight, only recognizes one,
      ///   mirroring <see cref="CheckBattleFulfillment" />'s own discipline for MoveAgainst.
      /// </summary>
      private void CheckProtectFulfillment(TrackedCommitment commitment, MapEvent mapEvent)
      {
         if (commitment.State != CommitmentState.Active || Hero.MainHero == null) return;

         MBReadOnlyList<MapEventParty> attackers = mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.Parties;
         MBReadOnlyList<MapEventParty> defenders = mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.Parties;

         bool promisorAttacker = SideHasHeroOrFaction(attackers, commitment.PromisorId, null);
         bool promisorDefender = SideHasHeroOrFaction(defenders, commitment.PromisorId, null);
         if (!promisorAttacker && !promisorDefender) return;

         bool playerAttacker = SideHasHeroOrFaction(attackers, Hero.MainHero.StringId, null);
         bool playerDefender = SideHasHeroOrFaction(defenders, Hero.MainHero.StringId, null);
         if (!playerAttacker && !playerDefender) return;

         bool sameSide = promisorAttacker && playerAttacker || promisorDefender && playerDefender;
         if (!sameSide) return;

         Fulfill(commitment, "they stood at the player's side in a real battle");
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (_active.Count == 0 || mapEvent == null) return;

         foreach (TrackedCommitment c in _active.Where(x => x.State == CommitmentState.Active && x.Kind == CommitmentKind.MoveAgainst).ToList())
         {
            TrackedCommitment commitment = c;
            SafeRun.Step($"CommitmentBehavior.CheckBattleFulfillment({commitment.Id})", () => CheckBattleFulfillment(commitment, mapEvent));
         }

         // R7-light: Protect's own fulfillment path, alongside MoveAgainst's above.
         foreach (TrackedCommitment c in _active.Where(x => x.State == CommitmentState.Active && x.Kind == CommitmentKind.Protect).ToList())
         {
            TrackedCommitment commitment = c;
            SafeRun.Step($"CommitmentBehavior.CheckProtectFulfillment({commitment.Id})", () => CheckProtectFulfillment(commitment, mapEvent));
         }
      }

      #endregion
   }
}
