// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Travel;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Spawns and manages the visible courier party that carries a
   ///   <see cref="PlayerLetter" /> from the player to its recipient. The courier is a
   ///   fast, lightly-escorted custom party that avoids picking fights but can still be
   ///   waylaid by bandits — if it is destroyed in transit, the letter is lost.
   ///   Lifecycle:
   ///   - <see cref="TrySpawnCourier" /> at send time, stamping the party id on the letter.
   ///   - <see cref="TickCouriers" /> daily: delivers on arrival, force-delivers past the
   ///   safety deadline, and prunes couriers whose party has vanished.
   ///   - <see cref="HandlePartyDestroyed" /> on the MobilePartyDestroyed event: marks the
   ///   letter lost when its courier is killed.
   /// </summary>
   public static class CourierService
   {
      // Map-unit distance at which the courier counts as "arrived" at a roaming recipient.
      private const float ArrivalDistance = 2.0f;

      // Base map speed of the courier. Set to 6.5 so it is faster than any army
      // but still looks like a fast horseman rather than a racing car.
      private const float CourierBaseSpeed = 6.5f;

      // A small, weak escort so the party is a real (defeatable) target, not invincible.
      private const int CourierEscortSize = 3;

      // Grace days past DeliveryDay before we force-deliver a stuck courier (anti-soft-lock).
      private const int ForceDeliveryGraceDays = 4;

      // StringIds of all active couriers. Used by CourierSpeedPatch for O(1) identity
      // checks on every party every tick — avoids display-string allocations on the hot path.
      // Rebuilt from live letters on session launch to survive save/load cycles.
      private static readonly HashSet<string> ActiveCourierIds = new();

      // Position of each courier captured at the last pre-save despawn, keyed by the
      // courier's (now stale) party id, so RespawnInFlight can restore it where it stood
      // rather than teleporting it back to its source. Empty after a load (couriers were
      // never saved), so loaded couriers respawn at their origin — acceptable, and unnoticed
      // given couriers are already at the mercy of bandit interception.
      private static readonly Dictionary<string, Vec2> DespawnPositions = new();

      // Set while DespawnAll is tearing couriers down before a save: their destruction must
      // NOT be mistaken for a bandit waylay (which would mark every in-flight letter lost).
      private static bool _suppressDestroyHandling;

      /// <summary>
      ///   True when the recipient can actually receive a letter — alive, present, and
      ///   not a prisoner. A dead, captured, or vanished recipient is undeliverable.
      /// </summary>
      public static bool CanReceiveLetter(Hero recipient)
         => recipient is {IsAlive: true, IsPrisoner: false} && ResolveCourierTarget(recipient) != null;

      /// <summary>
      ///   HourlyTickParty handler. Handles both directions:
      ///   - Player→NPC courier: delivers when close to the recipient.
      ///   - NPC→Player reply courier: delivers when close to the player; also
      ///   refreshes its <c>SetMoveGoToPoint</c> target so it tracks a moving player.
      ///   The <see cref="IsCourier" /> guard exits immediately for non-courier parties.
      /// </summary>
      public static void CheckCourierArrival(MobileParty courier, BannerlordNpcMemoryStore store)
      {
         if (!IsCourier(courier?.StringId)) return;
         if (store == null) return;
         if (CheckPlayerLetterCourierArrival(courier, store)) return;

         CheckReplyCourierArrival(courier, store);
      }

      /// <summary>
      ///   After a load, in-flight letters carry a courier id pointing to a party that was
      ///   never saved (despawned before the save). Rather than respawn a fragile party during
      ///   session launch — which the engine tends to cull moments later, taking the letter
      ///   down with it — we clear the stale id so the letter falls back to date-based delivery
      ///   and still arrives. Letters whose courier party IS live (a legacy save made before
      ///   couriers were kept out of the .sav) are left untouched. Couriers simply go invisible
      ///   after a load, which is fine — the road was always uncertain.
      /// </summary>
      public static void ClearOrphanedCouriers(BannerlordNpcMemoryStore store)
      {
         if (store == null) return;
         var cleared = 0;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            var changed = false;

            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
               if (InFlightWithDeadCourier(letter.CourierPartyId, letter.IsDelivered, letter.IsLost))
               {
                  letter.CourierPartyId = null;
                  cleared++;
                  changed = true;
               }

            foreach (PendingLetter letter in profile.SentLetters)
               if (InFlightWithDeadCourier(letter.CourierPartyId, letter.IsDelivered, letter.IsLost))
               {
                  letter.CourierPartyId = null;
                  cleared++;
                  changed = true;
               }

            if (changed) store.Set(profile);
         }

         DespawnPositions.Clear();
         if (cleared > 0)
            ModLog.Info($"[COURIER] {cleared} in-flight letter(s) reverted to date-based delivery after load.");
      }

      // ── Save safety: keep courier parties OUT of the .sav ─────────────────
      // A mod-spawned MobileParty in an unusual state at save time (mid bandit
      // intercept, in a MapEvent…) can corrupt the native load. Rather than risk it,
      // we despawn every courier before the game serializes and bring them back once
      // the save is over. The letters they carry persist in the store and are delivered
      // by a date-based fallback regardless, so nothing is lost to the player.

      /// <summary>
      ///   Removes every active courier party from the world. Call on OnBeforeSave so a
      ///   courier <see cref="MobileParty" /> is NEVER serialized — closing the entire class
      ///   of "an in-flight courier breaks the save on load" failures. Captures each courier's
      ///   position so <see cref="RespawnInFlight" /> can restore it where it stood.
      /// </summary>
      public static void DespawnAll(BannerlordNpcMemoryStore store)
      {
         DespawnPositions.Clear();

         _suppressDestroyHandling = true;
         try
         {
            // Snapshot ids first — Disband mutates ActiveCourierIds as it goes.
            foreach (string partyId in ActiveCourierIds.ToList())
            {
               MobileParty courier = FindCourier(partyId);

               if (courier == null) continue;
               DespawnPositions[partyId] = courier.GetPosition2D;
               Disband(courier);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] DespawnAll failed: {ex.Message}");
         }
         finally
         {
            _suppressDestroyHandling = false;
         }

         if (DespawnPositions.Count > 0)
            ModLog.Info($"[COURIER] despawned {DespawnPositions.Count} courier(s) before save.");
      }

      /// <summary>
      ///   MobilePartyDestroyed handler: if the destroyed party is a courier (either
      ///   direction), marks its letter lost and notifies the player.
      ///   Returns true when a courier was matched.
      /// </summary>
      public static void HandlePartyDestroyed(MobileParty destroyed, BannerlordNpcMemoryStore store)
      {
         // A pre-save despawn is not a waylay — don't mark the letters lost.
         if (_suppressDestroyHandling) return;
         if (destroyed == null || store == null) return;

         string partyId = destroyed.StringId;

         if (string.IsNullOrEmpty(partyId)) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            if (PlayerToNpcCourierLost(store, profile, partyId)) return;
            if (NpcToPlayerReplyCourierLost(store, profile, partyId)) return;
         }
      }

      /// <summary>
      ///   SettlementEntered handler: when a courier enters the settlement where its
      ///   recipient actually is, deliver immediately. This is the primary, prompt
      ///   delivery path (the daily tick is only a backup). Returns true on delivery.
      /// </summary>
      public static void HandleSettlementEntered(MobileParty party, Settlement settlement, BannerlordNpcMemoryStore store)
      {
         if (party == null || settlement == null || store == null) return;

         string partyId = party.StringId;

         if (string.IsNullOrEmpty(partyId)) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            PlayerLetter letter = profile?.ReceivedPlayerLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

            if (letter == null) continue;

            Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);

            if (!CanReceiveLetter(recipient) || ResolveCourierTarget(recipient) as Settlement != settlement) return;

            Deliver(letter, false);
            Disband(party);
            store.Set(profile);
         }
      }

      /// <summary>
      ///   Returns true when <paramref name="partyId" /> belongs to one of our active
      ///   couriers. Called by <see cref="CourierSpeedPatch" /> on every
      ///   <c>CalculateFinalSpeed</c> invocation — must be allocation-free.
      /// </summary>
      public static bool IsCourier(string partyId)
         => !string.IsNullOrEmpty(partyId) && ActiveCourierIds.Contains(partyId);

      /// <summary>
      ///   Rebuilds the in-memory courier id set from the persisted store after a
      ///   save/load. Call once at session launch so the speed patch keeps working
      ///   for couriers that were in flight when the game was saved.
      /// </summary>
      public static void RebuildActiveCourierSet(BannerlordNpcMemoryStore store)
      {
         ActiveCourierIds.Clear();

         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            RebuildCourierForPlayerToNpc(profile);
            RebuildCourierForNpcToPlayer(profile);
         }

         ModLog.Info($"[COURIER] rebuilt active set: {ActiveCourierIds.Count} courier(s) in flight.");
      }

      /// <summary>
      ///   Recreates the visible courier parties for every letter still in flight, from the
      ///   persisted store. Call on OnSaveOver (restoring couriers the pre-save despawn removed,
      ///   at the position they held) and at session launch (respawning couriers that were never
      ///   in the save, from their source). Letters whose courier party still lives are left
      ///   untouched; a fresh courier is spawned only where none exists.
      /// </summary>
      public static void RespawnInFlight(BannerlordNpcMemoryStore store)
      {
         if (store == null) return;
         var respawned = 0;

         // This runs from OnSaveOver — a save-completion callback. Respawning a courier is purely cosmetic
         // (letters fall back to date-based delivery if it fails), so NOTHING here may crash the save. The whole
         // pass is guarded, and each hero lookup is null-safe (the hero collection can hold a transient null at
         // save-over time, which made Hero.FindFirst's predicate throw — the reported autosave crash).
         try
         {
            foreach (string id in store.ListIds())
            {
               NpcProfile profile = store.Get(id);

               if (profile == null) continue;

               var changed = false;

               // Player → NPC letters.
               foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
               {
                  if (!InFlightWithDeadCourier(letter.CourierPartyId, letter.IsDelivered, letter.IsLost)) continue;
                  Hero recipient = Hero.FindFirst(h => h != null && h.StringId == letter.RecipientId);
                  Vec2? at = TakePosition(letter.CourierPartyId);

                  if (!TrySpawnCourier(letter, recipient, spawnOverride: at)) continue;
                  respawned++;
                  changed = true;
               }

               // NPC → player replies (the sender is the profile's hero).
               Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
               foreach (PendingLetter letter in profile.SentLetters)
               {
                  if (!InFlightWithDeadCourier(letter.CourierPartyId, letter.IsDelivered, letter.IsLost)) continue;
                  Vec2? at = TakePosition(letter.CourierPartyId);

                  if (!TrySpawnReplyCourier(letter, sender, at)) continue;
                  respawned++;
                  changed = true;
               }

               if (changed) store.Set(profile);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] RespawnInFlight failed — couriers left to date-based delivery, save unaffected. {ex.GetType().Name}: {ex.Message}");
         }

         DespawnPositions.Clear();
         if (respawned > 0) ModLog.Info($"[COURIER] respawned {respawned} in-flight courier(s).");
      }

      // ── Daily management ──────────────────────────────────────────────────

      /// <summary>
      ///   Daily pass over every in-transit player letter that has a courier. Delivers on
      ///   arrival, force-delivers past the safety deadline, and clears couriers that have
      ///   vanished without a destruction event. Returns the profiles that changed so the
      ///   caller can persist them.
      /// </summary>
      public static void TickCouriers(BannerlordNpcMemoryStore store, int today)
      {
         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.ReceivedPlayerLetters == null) continue;

            var changed = false;
            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
            {
               if (letter.IsDelivered || letter.IsLost) continue;
               if (string.IsNullOrEmpty(letter.CourierPartyId)) continue; // date-based fallback handles these

               Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);
               MobileParty courier = FindCourier(letter.CourierPartyId);

               // Recipient died, was captured, or vanished → undeliverable. Despawn.
               if (!CanReceiveLetter(recipient))
               {
                  MarkUndeliverable(letter, $"{letter.RecipientName} could not be reached");
                  Disband(courier);
                  changed = true;

                  continue;
               }

               // Courier vanished without a destruction event (edge). Past the deadline,
               // give up rather than leave the letter pending forever.
               if (courier is not {IsActive: true})
               {
                  if (today >= letter.DeliveryDay + ForceDeliveryGraceDays)
                  {
                     MarkUndeliverable(letter, $"the courier to {letter.RecipientName} never arrived");
                     changed = true;
                  }

                  continue;
               }


               changed = ResumeCourier(today, courier, recipient, letter, changed);
            }

            if (changed) store.Set(profile);
         }
      }

      // ── Spawn ─────────────────────────────────────────────────────────────

      /// <summary>
      ///   Spawns a courier party at the player's position bound for <paramref name="recipient" />
      ///   and stamps its party id onto the letter. Returns false (letter keeps its
      ///   date-based delivery) on any failure.
      ///   <paramref name="speedOverride" /> and <paramref name="avoidHostile" /> exist for
      ///   the <c>cr.courier_slow</c> test path, which spawns a slow, engageable courier so
      ///   the (otherwise rare) bandit-interception/destruction path can actually be observed.
      /// </summary>
      public static bool TrySpawnCourier(PlayerLetter letter,
                                         Hero recipient,
                                         float? speedOverride = null,
                                         bool avoidHostile = true,
                                         Vec2? spawnOverride = null)
      {
         try
         {
            if (letter == null || recipient == null) return false;
            MobileParty player = MobileParty.MainParty;

            if (player == null) return false;

            Settlement home = recipient.HomeSettlement ?? recipient.CurrentSettlement ?? Hero.MainHero?.HomeSettlement;
            var name = new TextObject($"Courier to {recipient.Name}");
            float speed = speedOverride ?? CourierBaseSpeed;

            // A respawn (post-save / post-load) overrides the source with the courier's captured position so
            // it does not jump back to the player. A player writing from shipboard dispatches the courier
            // from the nearest landfall, not the open water; one writing from inside a settlement (a tavern
            // hire) dispatches it from the settlement's GATE, never the root, where a fresh party sits stuck
            // inside the walls until it starves (the same fault player-reported on NPC reply couriers).
            Vec2 spawnPos = spawnOverride ??
                            (player.IsCurrentlyAtSea
                               ? TravelingParty.NearestLandfall(player.GetPosition2D)
                               : player.CurrentSettlement?.GatePosition.ToVec2() ?? player.GetPosition2D);

            MobileParty courier = TravelingParty.Spawn(spawnPos, home, name, speed, recipient.Culture, avoidHostile);

            if (courier == null) return false;

            DirectCourierToRecipient(courier, recipient);
            letter.CourierPartyId = courier.StringId;
            ActiveCourierIds.Add(courier.StringId);

            ModLog.Info($"[COURIER] spawned '{courier.StringId}' → {recipient.Name} " + $"(speed {speed}, escort {CourierEscortSize}, avoidHostile {avoidHostile}).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] TrySpawnCourier failed: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Spawns a visible courier at the NPC sender's location that travels toward
      ///   the player. Called from the main-thread daily tick once the NPC's chosen
      ///   <see cref="PendingLetter.DispatchDay" /> has arrived.
      ///   Returns false on any failure (letter retains its date-based fallback).
      /// </summary>
      public static bool TrySpawnReplyCourier(PendingLetter letter, Hero sender, Vec2? spawnOverride = null)
      {
         try
         {
            if (letter == null || sender == null) return false;
            MobileParty playerParty = MobileParty.MainParty;

            if (playerParty == null) return false;

            Vec2 spawnPos = spawnOverride ?? GetSenderPosition(sender);
            Settlement home = sender.HomeSettlement ?? sender.CurrentSettlement ?? Hero.MainHero?.HomeSettlement;
            var name = new TextObject($"Letter from {sender.Name}");

            MobileParty courier = TravelingParty.Spawn(spawnPos, home, name, CourierBaseSpeed, sender.Culture, true);

            if (courier == null) return false;

            DirectReplyCourierToPlayer(courier, playerParty);
            letter.CourierPartyId = courier.StringId;
            ActiveCourierIds.Add(courier.StringId);

            ModLog.Info($"[COURIER-REPLY] spawned '{courier.StringId}' from {sender.Name} → player (speed {CourierBaseSpeed}).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER-REPLY] TrySpawnReplyCourier failed: {ex.Message}");

            return false;
         }
      }

      #region private

      private static bool CheckPlayerLetterCourierArrival(MobileParty courier, BannerlordNpcMemoryStore store)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            PlayerLetter letter = profile?.ReceivedPlayerLetters.FirstOrDefault(l => l.CourierPartyId == courier.StringId && !l.IsDelivered && !l.IsLost);

            if (letter == null) continue;

            Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);
            if (!CanReceiveLetter(recipient))
            {
               MarkUndeliverable(letter, $"{letter.RecipientName} could not be reached");
               Disband(courier);
               store.Set(profile);

               return true;
            }

            if (!HasArrived(courier, recipient)) return true; // matched this courier — stop scanning

            Deliver(letter, false);
            Disband(courier);
            store.Set(profile);

            return true; // matched this courier — stop scanning
         }

         return false;
      }

      private static void CheckReplyCourierArrival(MobileParty courier, BannerlordNpcMemoryStore store)
      {
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            PendingLetter letter = profile?.SentLetters.FirstOrDefault(l => l.CourierPartyId == courier.StringId && !l.IsDelivered && !l.IsLost);

            if (letter == null) continue;

            MobileParty playerParty = MobileParty.MainParty;

            if (playerParty == null) return;

            float dist = courier.GetPosition2D.Distance(playerParty.GetPosition2D);
            if (dist < ArrivalDistance)
            {
               // Deliver before disband — clear the party id so the popup handler
               // and the next HourlyTick won't re-match this courier.
               letter.CourierPartyId = null;
               ActiveCourierIds.Remove(courier.StringId);
               Disband(courier);
               store.Set(profile);
               LetterPopupManager.Show(letter, profile);
               ModLog.Info($"[COURIER-REPLY] delivered from {profile.Name} to player.");
            }
            else
            {
               // Still travelling — refresh move target hourly so the courier
               // tracks a moving player without manual daily updates.
               DirectReplyCourierToPlayer(courier, playerParty);
            }

            return; // matched
         }
      }

      // ── Private helpers ───────────────────────────────────────────────────

      private static void Deliver(PlayerLetter letter, bool courierGone)
      {
         letter.IsDelivered = true;
         letter.CourierPartyId = null;

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your letter has reached {letter.RecipientName}."));
         ModLog.Info($"[COURIER] delivered to {letter.RecipientName} " + $"(courierGone={courierGone}).");
      }

      private static void DirectCourierToRecipient(MobileParty courier, Hero recipient)
      {
         object target = ResolveCourierTarget(recipient);
         switch (target)
         {
            case Settlement settlement:
               TravelingParty.DirectToSettlement(courier, settlement);

               break;
            // EngageParty uses the engine's built-in pursuit AI: the courier dynamically tracks the
            // target party at its own speed without manual position updates. No combat fires because the
            // courier and the lord are not hostile — the engage mode just means "catch up". (Letter-only:
            // a journey always travels to a settlement, so this case lives here, not in TravelingParty.)
            case MobileParty party:
               courier.Ai.SetDoNotMakeNewDecisions(false);
               courier.SetMoveEngageParty(party, MobileParty.NavigationType.Default);
               courier.Ai.SetDoNotMakeNewDecisions(true);

               break;
         }
      }

      private static void DirectReplyCourierToPlayer(MobileParty courier, MobileParty playerParty)
         => TravelingParty.DirectToMovingPoint(courier, playerParty.GetPosition2D);

      private static void Disband(MobileParty courier)
      {
         try
         {
            if (courier == null) return;
            ActiveCourierIds.Remove(courier.StringId);
            TravelingParty.Destroy(courier);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] Disband failed: {ex.Message}");
         }
      }

      private static MobileParty FindCourier(string partyId)
      {
         return string.IsNullOrEmpty(partyId)
            ? null
            : MobileParty.All.FirstOrDefault(p => p.StringId == partyId);
      }

      private static Vec2 GetSenderPosition(Hero sender)
      {
         // A sender sailing the sea cannot hand a letter to a land courier where they
         // stand — the courier spawns at the nearest landfall instead (tester report:
         // courier spawned in the water and sat immobile until the player drifted by).
         // And a settled sender (a governor wife in her castle) must dispatch from the
         // settlement's GATE, not its root: a party spawned at the root sits inside the
         // walls, cannot move, and starves until the letter is reported waylaid
         // (player-reported, with the courier visibly stuck in the castle).
         MobileParty party = sender.PartyBelongedTo;
         if (party != null)
         {
            if (party.IsCurrentlyAtSea) return TravelingParty.NearestLandfall(party.GetPosition2D);
            if (party.CurrentSettlement != null) return party.CurrentSettlement.GatePosition.ToVec2();

            return party.GetPosition2D;
         }

         if (sender.CurrentSettlement != null) return sender.CurrentSettlement.GatePosition.ToVec2();
         if (sender.StayingInSettlement != null) return sender.StayingInSettlement.GatePosition.ToVec2();

         return MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
      }

      private static bool HasArrived(MobileParty courier, Hero recipient)
      {
         object target = ResolveCourierTarget(recipient);
         switch (target)
         {
            case Settlement settlement when courier.CurrentSettlement == settlement: return true;
            case Settlement settlement when courier.GetPosition2D.Distance(settlement.GetPosition2D) < ArrivalDistance:
            case MobileParty party when courier.GetPosition2D.Distance(party.GetPosition2D) < ArrivalDistance: return true;
            default: return false;
         }
      }

      /// <summary>A letter still in flight whose courier party no longer exists in the world.</summary>
      private static bool InFlightWithDeadCourier(string courierPartyId, bool delivered, bool lost)
         => !delivered && !lost && !string.IsNullOrEmpty(courierPartyId) && FindCourier(courierPartyId) == null;

      /// <summary>
      ///   Terminal failure: the letter never reaches its recipient (recipient dead,
      ///   captured, or unreachable). Flags <see cref="PlayerLetter.IsLost" /> so it is
      ///   never delivered nor injected into a prompt, and tells the player.
      /// </summary>
      private static void MarkUndeliverable(PlayerLetter letter, string reason)
      {
         letter.IsLost = true;
         letter.CourierPartyId = null;

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your letter could not be delivered — {reason}."));
         ModLog.Info($"[COURIER] undeliverable to {letter.RecipientName}: {reason}.");
      }

      private static bool NpcToPlayerReplyCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId)
      {
         PendingLetter reply = profile.SentLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (reply == null) return false;

         reply.IsLost = true;
         reply.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);
         store.Set(profile);

         MBInformationManager.AddQuickInformation(new TextObject($"A courier from {reply.SenderName} was waylaid. Their letter is lost."));
         ModLog.Info($"[COURIER-REPLY] destroyed — reply from {reply.SenderName} lost.");

         return true;
      }

      private static bool PlayerToNpcCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId)
      {
         PlayerLetter forward = profile.ReceivedPlayerLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (forward == null) return false;

         forward.IsLost = true;
         forward.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);
         store.Set(profile);

         MBInformationManager.AddQuickInformation(new TextObject($"Your courier to {forward.RecipientName} was waylaid. The letter is lost."));
         ModLog.Info($"[COURIER] destroyed — letter to {forward.RecipientName} lost.");

         return true;
      }

      private static void RebuildCourierForNpcToPlayer(NpcProfile profile)
      {
         foreach (PendingLetter letter in from letter in profile.SentLetters
                                          where !letter.IsDelivered && !letter.IsLost
                                          where !string.IsNullOrEmpty(letter.CourierPartyId)
                                          let courier = FindCourier(letter.CourierPartyId)
                                          where courier is {IsActive: true}
                                          select letter) ActiveCourierIds.Add(letter.CourierPartyId);
      }

      private static void RebuildCourierForPlayerToNpc(NpcProfile profile)
      {
         // Player→NPC couriers
         foreach (PlayerLetter letter in from letter in profile.ReceivedPlayerLetters
                                         where !letter.IsDelivered && !letter.IsLost
                                         where !string.IsNullOrEmpty(letter.CourierPartyId)
                                         let courier = FindCourier(letter.CourierPartyId)
                                         where courier is {IsActive: true}
                                         select letter) ActiveCourierIds.Add(letter.CourierPartyId);
      }

      /// <summary>
      ///   Finds where the courier should go to reach the hero.
      ///   Returns either a MobileParty (hero's party) or a Settlement (where they stay).
      /// </summary>
      private static object ResolveCourierTarget(Hero recipient)
      {
         if (recipient == null) return null;
         if (recipient.PartyBelongedTo != null) return recipient.PartyBelongedTo;
         if (recipient.CurrentSettlement != null) return recipient.CurrentSettlement;
         if (recipient.StayingInSettlement != null) return recipient.StayingInSettlement;

         // Home settlement is a last-resort anchor, but only if they are not roaming elsewhere.
         if (recipient.HomeSettlement != null && recipient.PartyBelongedTo == null) return recipient.HomeSettlement;

         return null;
      }

      private static bool ResumeCourier(int today, MobileParty courier, Hero recipient, PlayerLetter letter, bool changed)
      {
         if (HasArrived(courier, recipient))
         {
            // Deliver BEFORE disbanding: Deliver sets IsDelivered + nulls
            // CourierPartyId, so if ApplyForDisbanding raises MobilePartyDestroyed
            // synchronously, HandlePartyDestroyed sees a delivered letter and no-ops.
            Deliver(letter, false);
            Disband(courier);
            changed = true;
         }
         else if (today >= letter.DeliveryDay + ForceDeliveryGraceDays)
         {
            // Stuck past the deadline — give up WITHOUT delivering. A courier
            // that cannot reach the recipient should not leave an idle party on
            // the map; it abandons the errand and the letter is lost.
            MarkUndeliverable(letter, $"the courier could not reach {letter.RecipientName} in time");
            Disband(courier);
            changed = true;
         }
         else
         {
            // Still travelling. Re-issue the move order ONLY when the courier is
            // not already sitting in the target settlement — re-issuing while it
            // is inside ejects it, causing the in-and-out bounce.
            object target = ResolveCourierTarget(recipient);
            switch (target)
            {
               case Settlement settlement when courier.CurrentSettlement == settlement:
                  // Already inside the target settlement.
                  break;
               default: DirectCourierToRecipient(courier, recipient); break;
            }
         }

         return changed;
      }

      private static Vec2? TakePosition(string oldPartyId)
         => oldPartyId != null && DespawnPositions.TryGetValue(oldPartyId, out Vec2 p)
            ? p
            : null;

      #endregion
   }
}