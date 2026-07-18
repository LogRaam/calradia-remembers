// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Travel;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
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
   ///   Lifecycle (player -> NPC direction):
   ///   - <see cref="TrySpawnCourier" /> at send time, stamping the party id on the letter.
   ///   - <see cref="TickCouriers" /> daily: delivers on arrival, force-delivers past the
   ///   safety deadline, and prunes couriers whose party has vanished.
   ///   - <see cref="HandlePartyDestroyed" /> on the MobilePartyDestroyed event: marks the
   ///   letter lost when its courier is killed.
   ///   The NPC -> player reply direction mirrors this: <see cref="TrySpawnReplyCourier" /> spawns,
   ///   <see cref="CheckReplyCourierArrival" /> (hourly) delivers, and <see cref="TickReplyCouriers" />
   ///   (daily, courier audit 2.1) is the safety net that releases an orphaned reply courier so its
   ///   letter never hangs undelivered forever.
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

      // Map-unit range within which a bandit band is close enough to notice a passing courier and be
      // rolled once for the chase (see CourierWaylayPolicy). TUNE.
      private const float WaylayScanRadius = 12f;

      // Courier id -> bandit ids already rolled for this courier, so each band decides only ONCE whether to
      // give chase (not every hour it stays in range). Pruned when the courier is delivered or destroyed.
      private static readonly Dictionary<string, HashSet<string>> RolledBandits = new();

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

         ScanForWaylayers(courier); // some bandit bands nearby may decide to give chase this hour
         if (CheckPlayerLetterCourierArrival(courier, store)) return;

         CheckReplyCourierArrival(courier, store);
      }

      /// <summary>
      ///   Player ruling (2026-07-10): the courier is IGNORED by every party by default, and each bandit band it
      ///   passes gets ONE independent chance to give chase (<see cref="CourierWaylayPolicy" />), not the whole map
      ///   converging on a lone weak party (the 6-of-6 report). The IGNORE reliably stops the auto-targeting (the
      ///   engine's target scan skips ShouldBeIgnored parties, confirmed by decompile). Forcing an ignored party's
      ///   pursuit through the AI is NOT reliable (the AI drops the ignored target on its next think), so a caught
      ///   chase is resolved DIRECTLY by disbanding the courier, which fires the normal waylay (letter lost + the
      ///   resend prompt). The fast courier escapes most chases, so real interception stays rare and speed-rewarding.
      /// </summary>
      private static void ScanForWaylayers(MobileParty courier)
      {
         try
         {
            if (courier is not {IsActive: true}) return;

            // 1.5 (courier audit): a courier that has reached a settlement or port is off the open road, so it is
            // not ambushed by a passing band there (it has effectively arrived, or is waiting to cross water).
            if (courier.CurrentSettlement != null) return;

            courier.IgnoreByOtherPartiesTill(CampaignTime.DaysFromNow(3f)); // no band auto-targets it

            if (!RolledBandits.TryGetValue(courier.StringId, out HashSet<string> rolled))
            {
               rolled = new HashSet<string>();
               RolledBandits[courier.StringId] = rolled;
            }

            Vec2 pos = courier.GetPosition2D;
            foreach (MobileParty bandit in MobileParty.AllBanditParties)
            {
               if (bandit is not {IsActive: true} || !bandit.IsBandit) continue;
               if (bandit.GetPosition2D.Distance(pos) > WaylayScanRadius) continue;
               if (!rolled.Add(bandit.StringId)) continue; // this band decides only once for this courier

               if (!CourierWaylayPolicy.ShouldTurnHostile(MBRandom.RandomInt(100))) continue; // it lets the courier pass

               if (!CourierWaylayPolicy.ChaseCatchesCourier(MBRandom.RandomInt(100)))
               {
                  ModLog.Info($"[COURIER] band '{bandit.StringId}' gave chase but courier '{courier.StringId}' outran them.");

                  continue;
               }

               ModLog.Info($"[COURIER] band '{bandit.StringId}' waylays courier '{courier.StringId}'.");
               TravelingParty.Destroy(courier); // fires MobilePartyDestroyed -> HandlePartyDestroyed (lost + resend)

               return;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] ScanForWaylayers failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Drops a courier's per-band roll memory once it is delivered or destroyed.</summary>
      private static void ForgetWaylayRolls(string courierId)
      {
         if (!string.IsNullOrEmpty(courierId)) RolledBandits.Remove(courierId);
      }

      /// <summary>
      ///   TEST hook (cr.courier_waylay): waylays the given courier NOW by disbanding it, exactly as a catching
      ///   bandit band does, so the loss handling and the Resend prompt can be seen on demand. Returns a report.
      /// </summary>
      public static string WaylayCourierNow(string courierId)
      {
         try
         {
            MobileParty courier = MobileParty.All.FirstOrDefault(p => p.StringId == courierId);
            if (courier is not {IsActive: true}) return "courier party not found.";

            TravelingParty.Destroy(courier); // fires MobilePartyDestroyed -> HandlePartyDestroyed (lost + resend)

            return $"courier '{courierId}' waylaid; the Resend prompt should appear.";
         }
         catch (Exception ex)
         {
            return $"waylay error: {ex.GetType().Name}: {ex.Message}";
         }
      }

      /// <summary>
      ///   TEST hook (self-test): revives a waylaid letter with the same text and re-dispatches a courier, the
      ///   same action the "Send again" prompt takes, but callable without the UI so it can be asserted.
      /// </summary>
      internal static void ResendWaylaidLetterForTest(BannerlordNpcMemoryStore store, NpcProfile profile, PlayerLetter letter)
         => ResendWaylaidLetter(store, profile, letter);

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
      public static void HandlePartyDestroyed(MobileParty destroyed, BannerlordNpcMemoryStore store, PartyBase destroyer = null)
      {
         // A pre-save despawn is not a waylay — don't mark the letters lost.
         if (_suppressDestroyHandling) return;
         if (destroyed == null || store == null) return;

         string partyId = destroyed.StringId;

         if (string.IsNullOrEmpty(partyId)) return;

         ForgetWaylayRolls(partyId);

         // 1.10 (courier audit): only call it a "waylay by bandits" when a BANDIT band actually destroyed the
         // courier. Any other cause (starvation despite rations, caught in a battle, a despawn) gets a neutral
         // "lost on the road" message instead of a false bandit tale.
         bool byBandit = destroyer?.MobileParty is {IsBandit: true};

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            if (PlayerToNpcCourierLost(store, profile, partyId, byBandit)) return;
            if (NpcToPlayerReplyCourierLost(store, profile, partyId, byBandit)) return;
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
      /// <summary>
      ///   M8 (courier audit): the courier speed floor is a Harmony postfix on
      ///   <see cref="DefaultPartySpeedCalculatingModel" />.<c>CalculateFinalSpeed</c>. Any mod that REPLACES the
      ///   party-speed model with its own type silently disables that postfix, so couriers crawl at the vanilla
      ///   small-party speed, starve, and miss deadlines with no visible cause. This logs a single warning at
      ///   launch so the failure is diagnosable from the log instead of looking like a CR bug.
      /// </summary>
      public static void WarnIfSpeedModelReplaced()
      {
         try
         {
            var model = Campaign.Current?.Models?.PartySpeedCalculatingModel;
            if (model != null && model is not DefaultPartySpeedCalculatingModel)
               ModLog.Info($"[COURIER] WARNING: another mod replaced the party-speed model ({model.GetType().Name}); " +
                           "the courier speed floor is inactive, so couriers may crawl, starve, and miss deadlines.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] speed-model check failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      public static void RebuildActiveCourierSet(BannerlordNpcMemoryStore store)
      {
         ActiveCourierIds.Clear();
         RolledBandits.Clear(); // 4.6: a static keyed by courier id, never purged, leaked slowly across campaigns in one process.

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

                  // Never respawn a courier for a dead sender: the letter died with its author.
                  if (sender is not {IsAlive: true})
                  {
                     letter.IsLost = true;
                     changed = true;

                     continue;
                  }

                  Vec2? at = TakePosition(letter.CourierPartyId);

                  if (!TrySpawnReplyCourier(letter, sender, at))
                  {
                     // 1.6 (courier audit): the respawn failed, so the stale party id points at a courier that no
                     // longer exists. Clear it, so date-based delivery (which requires an empty CourierPartyId) can
                     // take the letter over instead of it hanging in limbo forever.
                     letter.CourierPartyId = null;
                     changed = true;

                     continue;
                  }

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
                     MarkUndeliverable(letter, $"the courier to {letter.RecipientName} never arrived", store, profile);
                     changed = true;
                  }

                  continue;
               }


               changed = ResumeCourier(today, courier, recipient, letter, changed, store, profile);
            }

            if (changed) store.Set(profile);
         }
      }

      /// <summary>
      ///   Courier audit 2.1: the daily safety net for the NPC->player direction, symmetric to
      ///   <see cref="TickCouriers" />. The reply courier is normally delivered by the hourly
      ///   <see cref="CheckReplyCourierArrival" />, but a courier that vanishes without a destruction event, or a
      ///   player who stays unreachable (long sea crossing) well past the deadline, used to leave the letter
      ///   pending FOREVER: the date-based fallback in <see cref="LetterDeliveryService" /> explicitly skips any
      ///   letter that still carries a <see cref="PendingLetter.CourierPartyId" /> (the orphan-party bug documented
      ///   as "NOT fixed"). Here we stop waiting on the physical courier past the grace deadline and clear the id,
      ///   so the fallback (a popup, which reaches the player anywhere, at sea included) delivers on the next tick.
      /// </summary>
      public static void TickReplyCouriers(BannerlordNpcMemoryStore store, int today)
      {
         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.SentLetters == null || profile.SentLetters.Count == 0) continue;

            var changed = false;
            foreach (PendingLetter letter in profile.SentLetters)
            {
               if (letter.IsDelivered || letter.IsLost) continue;
               if (string.IsNullOrEmpty(letter.CourierPartyId)) continue; // no reply courier; date fallback owns it

               // The sender died with the letter on the road: it dies with its author (same rule as arrival).
               Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
               if (sender is not {IsAlive: true})
               {
                  letter.IsLost = true;
                  Disband(FindCourier(letter.CourierPartyId));
                  letter.CourierPartyId = null;
                  changed = true;

                  continue;
               }

               // Only intervene once past the safety deadline, so a just-spawned or briefly-missing courier
               // (e.g. mid save/load) is never cut short.
               if (today < letter.DeliveryDay + ForceDeliveryGraceDays) continue;

               // Past the deadline: stop chasing. A blank placeholder (LLM never filled it) is dropped so the
               // correspondent is not muted forever; a real letter has its courier released to the date fallback.
               if (string.IsNullOrWhiteSpace(letter.Content)) letter.IsLost = true;
               Disband(FindCourier(letter.CourierPartyId));
               letter.CourierPartyId = null;
               changed = true;
               ModLog.Info($"[COURIER-REPLY] {profile.Name}'s reply courier gave up past the deadline; " +
                           (letter.IsLost ? "blank letter dropped." : "handed to date-based delivery."));
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
            courier.IgnoreByOtherPartiesTill(CampaignTime.DaysFromNow(3f)); // ignored until a band is rolled to chase it
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
            if (letter == null || sender is not {IsAlive: true}) return false; // no couriers for the dead
            MobileParty playerParty = MobileParty.MainParty;

            if (playerParty == null) return false;

            Vec2 spawnPos = spawnOverride ?? GetSenderPosition(sender);
            Settlement home = sender.HomeSettlement ?? sender.CurrentSettlement ?? Hero.MainHero?.HomeSettlement;
            var name = new TextObject($"Letter from {sender.Name}");

            MobileParty courier = TravelingParty.Spawn(spawnPos, home, name, CourierBaseSpeed, sender.Culture, true);

            if (courier == null) return false;

            DirectReplyCourierToPlayer(courier, playerParty);
            courier.IgnoreByOtherPartiesTill(CampaignTime.DaysFromNow(3f)); // ignored until a band is rolled to chase it
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

            // The sender died while the courier was on the road: the letter is never delivered
            // (word of the death overtakes the courier, who turns back).
            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
            if (sender is not {IsAlive: true})
            {
               letter.IsLost = true;
               letter.CourierPartyId = null;
               ActiveCourierIds.Remove(courier.StringId);
               Disband(courier);
               store.Set(profile);
               ModLog.Info($"[COURIER-REPLY] courier turned back: {profile.Name} died with their letter on the road.");

               return;
            }

            MobileParty playerParty = MobileParty.MainParty;

            if (playerParty == null) return;

            float dist = courier.GetPosition2D.Distance(playerParty.GetPosition2D);
            if (dist < ArrivalDistance)
            {
               // A letter must never outlive the intent that wrote it: re-validate against the sender's
               // CURRENT intention on arrival too, so a divorce or war-threat letter still on the road when
               // its sender's intention lapses is dropped rather than sprung on the player.
               if (LetterIntentionResolver.ShouldDrop(letter, sender))
               {
                  letter.IsDelivered = true;
                  letter.CourierPartyId = null;
                  store.Set(profile);
                  ActiveCourierIds.Remove(courier.StringId);
                  Disband(courier);
                  ModLog.Info($"[LETTER] dropped on arrival (intention lapsed): reason={letter.Reason}.");

                  return; // matched
               }

               // C1: only CONSUME the courier once the letter has actually been shown. If a window is already
               // open, Show defers (marks nothing) and the courier stays at the player to retry next hourly tick,
               // so a letter is never flagged delivered-but-unseen and lost.
               if (!LetterPopupManager.Show(letter, profile)) return; // deferred: keep the courier, retry next tick

               letter.CourierPartyId = null;
               store.Set(profile);
               ActiveCourierIds.Remove(courier.StringId);
               Disband(courier);
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

      /// <summary>M5 (courier audit): the recipient is sailing, so a land courier cannot reach them where they float.</summary>
      private static bool RecipientAtSea(Hero recipient)
         => recipient?.PartyBelongedTo is {IsCurrentlyAtSea: true};

      private static void DirectCourierToRecipient(MobileParty courier, Hero recipient)
      {
         // M5: a recipient at sea cannot be met on the water. Head for the nearest landfall and hold there; when
         // they make port and move inland, ResolveCourierTarget returns their party again and the pursuit resumes.
         if (RecipientAtSea(recipient))
         {
            TravelingParty.DirectToMovingPoint(courier, TravelingParty.NearestLandfall(recipient.PartyBelongedTo.GetPosition2D));

            return;
         }

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
         if (RecipientAtSea(recipient)) return false; // M5: never "arrive" on a recipient still at sea; wait for landfall.

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
      private static void MarkUndeliverable(PlayerLetter letter, string reason,
                                            BannerlordNpcMemoryStore resendStore = null, NpcProfile resendProfile = null)
      {
         letter.IsLost = true;
         letter.CourierPartyId = null;

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your letter could not be delivered: {reason}."));
         ModLog.Info($"[COURIER] undeliverable to {letter.RecipientName}: {reason}.");

         // 4.6: when the courier (not the recipient) is what failed, the recipient can still receive a fresh one,
         // so the player's typed text should not be lost without recourse: offer the same one-click resend an
         // interception does. Only the courier-failed sites pass the store/profile; the "recipient dead/gone"
         // sites leave them null, where a resend would be pointless.
         if (resendStore != null && resendProfile != null)
            OfferResend(resendStore, resendProfile, letter, byBandit: false);
      }

      private static bool NpcToPlayerReplyCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId, bool byBandit)
      {
         PendingLetter reply = profile.SentLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (reply == null) return false;

         reply.IsLost = true;
         reply.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);
         store.Set(profile);

         string how = byBandit ? "was waylaid" : "was lost on the road"; // 1.10: honest cause
         MBInformationManager.AddQuickInformation(new TextObject($"A courier from {reply.SenderName} {how}. Their letter is lost."));
         ModLog.Info($"[COURIER-REPLY] destroyed ({(byBandit ? "waylaid" : "non-bandit")}): reply from {reply.SenderName} lost.");

         return true;
      }

      private static bool PlayerToNpcCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId, bool byBandit)
      {
         PlayerLetter forward = profile.ReceivedPlayerLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (forward == null) return false;

         forward.IsLost = true;
         forward.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);
         store.Set(profile);

         ModLog.Info($"[COURIER] destroyed ({(byBandit ? "waylaid" : "non-bandit")}): letter to {forward.RecipientName} lost.");

         // Player report: losing the TYPED letter is the frustrating part. The text is preserved on the letter,
         // so offer a one-click resend rather than making the player retype it. The loss still happened (the
         // stakes stay), but the writing is never wasted.
         OfferResend(store, profile, forward, byBandit);

         return true;
      }

      /// <summary>Asks the player whether to send a lost letter again, re-dispatching it with the same text.</summary>
      private static void OfferResend(BannerlordNpcMemoryStore store, NpcProfile profile, PlayerLetter letter, bool byBandit = true)
      {
         try
         {
            // 1.10: honest framing, so the prompt does not invent bandits when the courier was lost some other way.
            string cause = byBandit
               ? "was waylaid by bandits and the letter did not arrive"
               : "was lost on the road and the letter did not arrive";
            InformationManager.ShowInquiry(new InquiryData(
               byBandit ? "Letter Waylaid" : "Letter Lost",
               $"Your courier to {letter.RecipientName} {cause}. Send it again? Your message is kept.",
               true, true, "Send again", "Let it go",
               () => ResendWaylaidLetter(store, profile, letter),
               () => MBInformationManager.AddQuickInformation(new TextObject($"The letter to {letter.RecipientName} is abandoned."))));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] OfferResend failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Revives a waylaid letter (same content) and sends a fresh courier; falls back to date delivery.</summary>
      private static void ResendWaylaidLetter(BannerlordNpcMemoryStore store, NpcProfile profile, PlayerLetter letter)
      {
         try
         {
            Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);
            if (!CanReceiveLetter(recipient))
            {
               MBInformationManager.AddQuickInformation(new TextObject($"{letter.RecipientName} can no longer receive letters."));

               return;
            }

            // SentOnDay/DeliveryDay are init-only, so re-queue a FRESH letter carrying the same text rather than
            // revive the lost one; CreateAndQueue recomputes the delay and dispatches a new courier. The old lost
            // letter stays as history.
            PlayerLetterService.CreateAndQueue(profile, letter.Content, (int) CampaignTime.Now.ToDays, store);
            MBInformationManager.AddQuickInformation(new TextObject($"A fresh courier carries your letter to {letter.RecipientName}."));
            ModLog.Info($"[COURIER] resent waylaid letter to {letter.RecipientName}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] ResendWaylaidLetter failed: {ex.GetType().Name}: {ex.Message}");
         }
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

      private static bool ResumeCourier(int today, MobileParty courier, Hero recipient, PlayerLetter letter, bool changed,
                                        BannerlordNpcMemoryStore store, NpcProfile profile)
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
            MarkUndeliverable(letter, $"the courier could not reach {letter.RecipientName} in time", store, profile);
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