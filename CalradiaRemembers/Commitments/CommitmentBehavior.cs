// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1 (the mechanical spine): the TrackedCommitment engine. The
// flagship kind is a lord pledging to move against a rival — powered by the existing (CR-owned,
// deterministic) schemes system. A pledge is a POLITICAL commitment only: it launches a scheme
// (slander/sabotage), never a war. Fulfillment is verified two ways (the scheme resolving in the
// promisor's favour, or any real hostile act between the promisor and the target); a breach is
// verified by the deadline lapsing with neither. Persisted as a UTF-8 byte[] (mirrors SchemeStore /
// CompanionMissionBehavior) with the same "never brick a save" degradation.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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
      /// </summary>
      public TrackedCommitment Create(string promisorId, string targetId, int issuedDay)
      {
         var commitment = new TrackedCommitment {
            Id = Guid.NewGuid().ToString("N"),
            PromisorId = promisorId,
            TargetId = targetId,
            Kind = CommitmentKind.MoveAgainst,
            IssuedDay = issuedDay,
            DeadlineDay = CommitmentPolicy.ComputeDeadline(issuedDay),
            State = CommitmentState.Active
         };
         _active.Add(commitment);
         ModLog.Info($"[COMMITMENT] pledged: promisor={promisorId} target={targetId} deadline day {commitment.DeadlineDay}.");

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

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
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

      /// <summary>Applies the fulfilled consequence and retires the pledge. Shared by both verification paths.</summary>
      private void Fulfill(TrackedCommitment commitment, string reasonForLog)
      {
         Hero promisor = FindHero(commitment.PromisorId);
         Hero target = FindHero(commitment.TargetId);
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
         string targetName = target?.Name?.ToString() ?? "their rival";
         profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
            $"Honored the pledge to move against {targetName}."));
         store.Set(profile);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Rivalry,
            Day = today,
            InvolvesPlayer = false,
            Magnitude = 24,
            Status = "pledge_fulfilled",
            Summary = $"{promisor?.Name?.ToString() ?? "A lord"} honored a pledge and moved against {targetName}, as promised.",
            ActorIds = new List<string> {commitment.PromisorId, commitment.TargetId},
            FactionIds = promisor?.MapFaction != null
               ? new List<string> {promisor.MapFaction.StringId}
               : new List<string>()
         });

         CommitmentJournalBridge.CloseFulfilled(commitment);

         _active.Remove(commitment);
         ModLog.Info($"[COMMITMENT] fulfilled: {promisor?.Name} → {target?.Name} ({reasonForLog}).");
      }

      /// <summary>The Active MoveAgainst pledge between this exact promisor/target pair, or null.</summary>
      private TrackedCommitment FindActivePledge(string promisorId, string targetId)
         => _active.FirstOrDefault(c =>
            c.State == CommitmentState.Active && c.Kind == CommitmentKind.MoveAgainst &&
            c.PromisorId == promisorId && c.TargetId == targetId);

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

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(commitment.PromisorId);

         if (profile == null)
         {
            _active.Remove(commitment);

            return;
         }

         Hero target = FindHero(commitment.TargetId);
         commitment.State = CommitmentState.Breached;

         int before = profile.ReputationWithPlayer;
         int after = ClampInt(before + CommitmentPolicy.BreachedRelationPenalty, -100, 100);
         profile.ReputationWithPlayer = after;
         StanceService.OnPromiseBroken(profile); // a real trust crater

         string targetName = target?.Name?.ToString() ?? "their rival";
         profile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal,
            $"Broke the pledge to move against {targetName} — did nothing."));
         store.Set(profile);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Rivalry,
            Day = today,
            InvolvesPlayer = false,
            Magnitude = 24,
            Status = "pledge_breached",
            Summary = $"It is said that {promisor.Name} pledged to move against {targetName} and did nothing.",
            ActorIds = new List<string> {commitment.PromisorId, commitment.TargetId},
            FactionIds = promisor.MapFaction != null
               ? new List<string> {promisor.MapFaction.StringId}
               : new List<string>()
         });

         CommitmentJournalBridge.CloseBreached(commitment);

         _active.Remove(commitment);
         ModLog.Info($"[COMMITMENT] breached: {promisor.Name} → {targetName} (deadline day {commitment.DeadlineDay}, today {today}).");
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

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (_active.Count == 0 || mapEvent == null) return;

         foreach (TrackedCommitment c in _active.Where(x => x.State == CommitmentState.Active && x.Kind == CommitmentKind.MoveAgainst).ToList())
         {
            TrackedCommitment commitment = c;
            SafeRun.Step($"CommitmentBehavior.CheckBattleFulfillment({commitment.Id})", () => CheckBattleFulfillment(commitment, mapEvent));
         }
      }

      #endregion
   }
}
