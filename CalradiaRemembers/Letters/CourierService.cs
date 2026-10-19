// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Travel;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
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
   ///   - <see cref="HandlePartyDestroyed" /> when <see cref="TransientPartyRegistry" /> reports the courier truly
   ///   destroyed: the letter is resent (or lost) and its content may become a carried secret.
   ///   - Around a save, the registry takes the courier off the map and <see cref="RestoreCourier" /> puts it back
   ///   where it stood (the recipe the registry generalised from the couriers, 26/09/2026).
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

      // ── Courier robustness batch (ROADMAP 2026-07-20, from the Realistic Courier decompile) ──
      // The DECISIONS live in tested Logic policies (CourierStuckPolicy, ReplyCourierGatePolicy,
      // CourierRationPolicy); this class only reads the engine and executes what they decide.

      // Grain units granted when a courier has eaten through its spawn rations (the M5 landfall wait for a
      // sailing recipient can outlast any fixed stock). How MUCH is an engine detail; WHEN is policy. TUNE.
      private const int RationTopUpUnits = 20;

      // Courier id -> position and day of the last DAILY tick that saw it actually move. Runtime-only, like
      // RolledBandits: couriers never survive a save, so there is nothing to persist.
      private static readonly Dictionary<string, StuckRecord> LastMovements = new();

      private struct StuckRecord
      {
         public Vec2 Position;
         public int Day;
      }

      // StringIds of all active couriers. Used by CourierSpeedPatch for O(1) identity
      // checks on every party every tick — avoids display-string allocations on the hot path.
      // Rebuilt from live letters on session launch to survive save/load cycles.
      private static readonly HashSet<string> ActiveCourierIds = new();

      // StringIds of couriers riding an ESCORTED correspondence thread (the player paid an armed escort for that
      // correspondent, NpcProfile.EscortedChannel). ScanForWaylayers never rolls a band against these, so they are
      // immune to interception. Populated on spawn from the owning profile's flag, forgotten on the same
      // delivery/destroy/recall/orphan-clear beats as ActiveCourierIds. Runtime-only like ActiveCourierIds: couriers
      // are never saved (they go invisible after a load), so there is nothing to persist; the durable truth is the
      // per-correspondent flag on the profile, which both spawn directions re-read to re-escort every later leg.
      private static readonly HashSet<string> EscortedCourierIds = new();

      /// <summary>The couriers' role in <see cref="TransientPartyRegistry" />.</summary>
      internal const string CourierRole = "letters.courier";

      private static BannerlordNpcMemoryStore CurrentStore => NpcMemoryCampaignBehavior.Instance?.Store;

      /// <summary>
      ///   True when the recipient can actually receive a letter — alive, present, and
      ///   not a prisoner. A dead, captured, or vanished recipient is undeliverable. Asks the one action gate
      ///   (30/09/2026): the game counts a hero it has taken out of the world as alive, and one received letters.
      /// </summary>
      public static bool CanReceiveLetter(Hero recipient)
         => Mapping.ActionGate.Allows(recipient, Logic.ActionKind.ReceiveLetter) && ResolveCourierTarget(recipient) != null;

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

         TopUpRations(courier); // a long errand (the M5 landfall wait) can outlast the spawn rations
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

            // Player option: an invulnerable courier stays un-auto-targeted (above) but is never rolled against,
            // so a player who never wants to lose a letter switches interception off entirely.
            if (ModSettings.InvulnerableCouriers) return;

            // Per-correspondent armed escort (the player paid for this thread): the courier is under guard and is
            // never rolled against. It stays IgnoreByOtherParties'd from the line above, so it is fully safe.
            if (EscortedCourierIds.Contains(courier.StringId)) return;

            if (!RolledBandits.TryGetValue(courier.StringId, out HashSet<string> rolled))
            {
               rolled = new HashSet<string>();
               RolledBandits[courier.StringId] = rolled;
            }

            // Distance-independent cap (player report 2026-08-05: letters lost almost every time when far). Only
            // the first few bands the courier EVER passes may decide; past that it is through the gauntlet, so a
            // long trip no longer compounds the loss (the rolled set persists across ticks for this courier).
            if (!CourierWaylayPolicy.HasWaylayBudget(rolled.Count)) return;

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

      /// <summary>Drops a courier's per-band roll memory and movement record once it is delivered or destroyed.</summary>
      private static void ForgetWaylayRolls(string courierId)
      {
         if (string.IsNullOrEmpty(courierId)) return;
         RolledBandits.Remove(courierId);
         LastMovements.Remove(courierId);
         EscortedCourierIds.Remove(courierId); // forgotten on destroy alongside the other per-courier runtime state
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
      ///   Diagnostic (cr.couriers): one line per in-flight courier: direction, letter, position, and how long
      ///   it has sat still, so a player report of a wandering courier can be read straight off the console.
      /// </summary>
      public static string DescribeActiveCouriers(BannerlordNpcMemoryStore store, int today)
      {
         if (ActiveCourierIds.Count == 0) return "No couriers in flight.";

         var lines = new List<string>();
         foreach (string partyId in ActiveCourierIds.ToList())
         {
            MobileParty courier = FindCourier(partyId);
            string what = DescribeCourierErrand(store, partyId);

            if (courier is not {IsActive: true})
            {
               lines.Add($"{partyId}: {what}: party MISSING (daily tick will reclaim it).");

               continue;
            }

            string still = LastMovements.TryGetValue(partyId, out StuckRecord rec) && today > rec.Day
               ? $", still for {today - rec.Day}d (fallback at {CourierStuckPolicy.StuckDaysBeforeFallback}d)"
               : "";
            lines.Add($"{partyId}: {what} at ({courier.GetPosition2D.x:F0}, {courier.GetPosition2D.y:F0})" +
                      $", food {courier.TotalFoodAtInventory}{still}.");
         }

         return string.Join("\n", lines);
      }

      /// <summary>
      ///   Support valve (cr.couriers_recall): despawns every in-flight courier and clears the letters' party
      ///   ids so each letter reverts to date-based delivery (nothing is lost, the road is simply abandoned).
      ///   Returns how many couriers were recalled.
      /// </summary>
      public static int RecallAll(BannerlordNpcMemoryStore store)
      {
         var recalled = 0;
         if (store == null) return recalled;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            var changed = false;

            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
               if (!letter.IsDelivered && !letter.IsLost && !string.IsNullOrEmpty(letter.CourierPartyId))
               {
                  Disband(FindCourier(letter.CourierPartyId));
                  letter.CourierPartyId = null;
                  recalled++;
                  changed = true;
               }

            foreach (PendingLetter letter in profile.SentLetters)
               if (!letter.IsDelivered && !letter.IsLost && !string.IsNullOrEmpty(letter.CourierPartyId))
               {
                  Disband(FindCourier(letter.CourierPartyId));
                  letter.CourierPartyId = null;
                  recalled++;
                  changed = true;
               }

            if (changed) store.Set(profile);
         }

         ModLog.Info($"[COURIER] recall: {recalled} courier(s) recalled, letters reverted to date-based delivery.");

         return recalled;
      }

      /// <summary>What a courier id is carrying, resolved from the store: direction plus the correspondent.</summary>
      private static string DescribeCourierErrand(BannerlordNpcMemoryStore store, string partyId)
      {
         if (store == null) return "unknown errand";

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;
            if (profile.ReceivedPlayerLetters?.Any(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost) == true)
               return $"letter to {profile.Name}";
            if (profile.SentLetters?.Any(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost) == true)
               return $"reply from {profile.Name}";
         }

         return "unknown errand";
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

         if (cleared > 0)
            ModLog.Info($"[COURIER] {cleared} in-flight letter(s) reverted to date-based delivery after load.");
      }

      // ── Save safety: keep courier parties OUT of the .sav ─────────────────
      // A mod-spawned MobileParty in an unusual state at save time (mid bandit
      // intercept, in a MapEvent...) can corrupt the native load. So every courier is
      // tracked by TransientPartyRegistry, which takes it off the map before the game
      // serializes and calls RestoreCourier once the save is over. The letters persist in
      // the store and are delivered by a date-based fallback regardless.

      /// <summary>Hands a freshly spawned courier to the registry: a save despawn and restore, and its true loss.</summary>
      private static void TrackCourier(MobileParty courier)
      {
         string id = courier.StringId;
         TransientPartyRegistry.Instance?.Track(courier, CourierRole,
            onDestroyed: (party, destroyer) => HandlePartyDestroyed(party, CurrentStore, destroyer),
            onRestore: at => RestoreCourier(id, at));
      }

      /// <summary>
      ///   After a save: the courier the registry took off the map as <paramref name="oldId" /> is sent again from
      ///   <paramref name="at" />, where it stood, for the letter still stamped with that id. Returns the new courier
      ///   (already tracked by its spawn), or null when there is nothing left to carry or it cannot be spawned.
      /// </summary>
      private static MobileParty RestoreCourier(string oldId, Vec2 at)
      {
         // The old party is gone: its runtime state goes first, so a reused id never inherits an escort.
         ActiveCourierIds.Remove(oldId);
         ForgetWaylayRolls(oldId);

         BannerlordNpcMemoryStore store = CurrentStore;
         if (store == null) return null;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            // Player -> NPC.
            PlayerLetter forward = profile.ReceivedPlayerLetters?.FirstOrDefault(l => l.CourierPartyId == oldId && !l.IsDelivered && !l.IsLost);
            if (forward != null)
            {
               Hero recipient = Hero.FindFirst(h => h != null && h.StringId == forward.RecipientId);
               bool spawned = TrySpawnCourier(forward, recipient, spawnOverride: at, escorted: profile.EscortedChannel || forward.Resent);
               store.Set(profile);

               return spawned ? FindCourier(forward.CourierPartyId) : null;
            }

            // NPC -> player (the sender is the profile's hero).
            PendingLetter reply = profile.SentLetters?.FirstOrDefault(l => l.CourierPartyId == oldId && !l.IsDelivered && !l.IsLost);
            if (reply == null) continue;

            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
            if (sender is not {IsAlive: true})
            {
               // Never a courier for a dead sender: the letter died with its author.
               reply.IsLost = true;
               reply.CourierPartyId = null;
               store.Set(profile);

               return null;
            }

            bool sent = TrySpawnReplyCourier(reply, sender, at, profile.EscortedChannel || reply.Resent);
            // 1.6 (courier audit): a failed respawn clears the stale id, so the date-based delivery (which needs an
            // empty CourierPartyId) takes the letter over instead of it hanging in limbo.
            if (!sent) reply.CourierPartyId = null;
            store.Set(profile);

            return sent ? FindCourier(reply.CourierPartyId) : null;
         }

         return null;
      }

      /// <summary>
      ///   A courier truly destroyed (the registry reports only those, never its own despawn): marks its letter
      ///   resent or lost, per direction, and notifies the player.
      /// </summary>
      public static void HandlePartyDestroyed(MobileParty destroyed, BannerlordNpcMemoryStore store, PartyBase destroyer = null)
      {
         if (destroyed == null || store == null) return;

         string partyId = destroyed.StringId;

         if (string.IsNullOrEmpty(partyId)) return;

         ForgetWaylayRolls(partyId);

         // 1.10 (courier audit): the honest cause of the loss is read per direction from the destroyer
         // (ClassifyLoss: bandits, a troop at war with the courier's clan, or anything else), never a false bandit tale.
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null) continue;

            if (PlayerToNpcCourierLost(store, profile, partyId, destroyer)) return;
            if (NpcToPlayerReplyCourierLost(store, profile, partyId, destroyer)) return;
         }
      }

      /// <summary>
      ///   What took a courier sent by <paramref name="courierClan" />: bandits, a troop at war with that clan, or
      ///   anything else (CourierSecretPolicy.Classify on the facts read here).
      /// </summary>
      internal static CourierLoss ClassifyLoss(PartyBase destroyer, Clan courierClan)
      {
         try
         {
            IFaction destroyerRealm = destroyer?.MapFaction;
            IFaction courierRealm = courierClan?.MapFaction ?? courierClan;

            return CourierSecretPolicy.Classify(new CourierLossFacts {
               DestroyerIsBandit = destroyer?.MobileParty is {IsBandit: true},
               DestroyerAtWarWithCourierClan = destroyerRealm != null && courierRealm != null && destroyerRealm != courierRealm
                                               && FactionManager.IsAtWarAgainstFaction(destroyerRealm, courierRealm)
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] could not tell what took a courier: {ex.GetType().Name}: {ex.Message}");

            return CourierLoss.Lost;
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

            // Both links need the null-conditional: "profile?.List.First()" guards the profile and then
            // dereferences the list regardless, which is the shape that crashes on a save whose list is null.
            PlayerLetter letter = profile?.ReceivedPlayerLetters?.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

            if (letter == null) continue;

            Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);

            if (!CanReceiveLetter(recipient) || ResolveCourierTarget(recipient) as Settlement != settlement) return;

            Deliver(letter, false, profile);
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

      public static void RebuildActiveCourierSet(BannerlordNpcMemoryStore store)
      {
         ActiveCourierIds.Clear();
         EscortedCourierIds.Clear(); // couriers were never saved; the durable escort truth is NpcProfile.EscortedChannel.
         RolledBandits.Clear(); // 4.6: a static keyed by courier id, never purged, leaked slowly across campaigns in one process.
         LastMovements.Clear(); // same lesson: runtime cache keyed by courier id, reset with the set it shadows.

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

               // Recipient died, was captured, or vanished → undeliverable. Despawn. The reason is SPECIFIC
               // (dead / captured / vanished): the courier's report is how the player learns the recipient's fate.
               if (!CanReceiveLetter(recipient))
               {
                  MarkUndeliverable(letter, UndeliverableReason(recipient, letter.RecipientName));
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


               // Courier audit (island report): a courier with NO land route left to its recipient (an
               // island across the water) can only starve on a shore — abandon TODAY and clear the id, so
               // the letter returns to the date fallback instead of waiting out the deadline (which stays
               // as the safety net). A recipient AT SEA is exempt: the courier waits at their landfall (M5),
               // it is not stuck.
               if (!RecipientAtSea(recipient) &&
                   !LandPathExists(courier.GetPosition2D, CourierDestinationPoint(recipient, courier.GetPosition2D)))
               {
                  Disband(courier);
                  letter.CourierPartyId = null;
                  changed = true;
                  ModLog.Info($"[COURIER] courier to {letter.RecipientName} has no land route left; handed to date-based delivery.");

                  continue;
               }

               // Robustness batch: a LIVE courier jammed on the same spot for days is going nowhere. Give the
               // letter back to date-based delivery NOW instead of burning the grace window and losing it.
               // (A courier waiting at a landfall for a sailing recipient is exempt: waiting is its job.)
               if (!RecipientAtSea(recipient) && CourierIsStuck(courier, today))
               {
                  Disband(courier);
                  letter.CourierPartyId = null;
                  changed = true;
                  ModLog.Info($"[COURIER] courier to {letter.RecipientName} has not moved for {CourierStuckPolicy.StuckDaysBeforeFallback} days; handed to date-based delivery.");

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
      ///   so the fallback (a popup, which reaches the player anywhere on land) delivers on the next tick
      ///   the player is ashore — the fallback itself never delivers at sea.
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

               // Courier audit (island report): a courier with NO land route left to the player (the player
               // sailed away, or the road was never there) would otherwise ride in place until the deadline.
               // Give up TODAY: disband it and clear the id, so the letter returns to the date fallback —
               // which never delivers at sea, but lands on the first tick the player is ashore past the
               // delivery day. The deadline below stays as the safety net.
               // Robustness batch: the SAME tested gate as the spawn site decides whether the road to the
               // player is still open for a courier already in flight: at sea, captive player, or no land
               // route left, and any block reclaims the courier while the letter reverts to the date fallback.
               MobileParty courier = FindCourier(letter.CourierPartyId);
               MobileParty playerParty = MobileParty.MainParty;
               if (courier is {IsActive: true} && playerParty != null)
               {
                  ReplyCourierBlock block = ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = playerParty.IsCurrentlyAtSea, PlayerIsPrisoner = Hero.MainHero.IsPrisoner, LandRouteExists = LandPathExists(courier.GetPosition2D, playerParty.GetPosition2D)});
                  if (block != ReplyCourierBlock.None)
                  {
                     Disband(courier);
                     letter.CourierPartyId = null;
                     changed = true;
                     ModLog.Info($"[COURIER-REPLY] {DescribeReplyBlock(block, profile.Name)}, courier reclaimed, letter handed to date-based delivery.");

                     continue;
                  }
               }

               // Robustness batch: a LIVE reply courier jammed on the same spot for days is going nowhere;
               // give the letter back to date-based delivery now rather than waiting out the deadline.
               if (courier is {IsActive: true} && CourierIsStuck(courier, today))
               {
                  Disband(courier);
                  letter.CourierPartyId = null;
                  changed = true;
                  ModLog.Info($"[COURIER-REPLY] {profile.Name}'s courier has not moved for {CourierStuckPolicy.StuckDaysBeforeFallback} days; handed to date-based delivery.");

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
                                         Vec2? spawnOverride = null,
                                         bool escorted = false)
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

            // Courier audit (island report): a courier rides overland (NavigationType.Default), so when NO
            // land route exists from the spawn point to where it must go (a recipient on an island, a
            // landfall across the water), it would spawn only to starve on the shore. Refuse the spawn:
            // the letter keeps its date-based fallback instead.
            Vec2 destination = CourierDestinationPoint(recipient, spawnPos);
            if (!LandPathExists(spawnPos, destination))
            {
               ModLog.Info($"[COURIER] no land route to {recipient.Name} — courier not spawned, letter stays on date-based delivery.");

               return false;
            }

            // An escorted thread rides under a larger, mounted guard detail (cosmetic justification for its
            // interception immunity); an ordinary courier keeps the small, defeatable escort.
            int escortSize = escorted ? TravelingParty.HeavyEscortSize : TravelingParty.EscortSize;
            MobileParty courier = TravelingParty.Spawn(spawnPos, home, name, speed, recipient.Culture, avoidHostile, escortSize);

            if (courier == null) return false;

            DirectCourierToRecipient(courier, recipient);
            courier.IgnoreByOtherPartiesTill(CampaignTime.DaysFromNow(3f)); // ignored until a band is rolled to chase it
            letter.CourierPartyId = courier.StringId;
            ActiveCourierIds.Add(courier.StringId);
            if (escorted) EscortedCourierIds.Add(courier.StringId); // never rolled against in ScanForWaylayers
            TrackCourier(courier);

            ModLog.Info($"[COURIER] spawned '{courier.StringId}' → {recipient.Name} " + $"(speed {speed}, escort {escortSize}, escorted {escorted}, avoidHostile {avoidHostile}).");

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
      public static bool TrySpawnReplyCourier(PendingLetter letter, Hero sender, Vec2? spawnOverride = null, bool escorted = false)
      {
         try
         {
            if (letter == null || sender is not {IsAlive: true}) return false; // no couriers for the dead
            MobileParty playerParty = MobileParty.MainParty;

            if (playerParty == null) return false;

            // Robustness batch: ONE tested gate (ReplyCourierGatePolicy) decides whether a rider can be on the
            // road to the player at all: at sea (a land courier cannot meet them on the water, and the date
            // fallback never delivers at sea either), a captive player (the rider would chase the captor's own
            // party), or no overland route (an island: the courier would spawn only to starve on the shore).
            // Every blocked case is safe: the letter keeps its date-based fallback.
            Vec2 spawnPos = spawnOverride ?? GetSenderPosition(sender);
            ReplyCourierBlock block = ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = playerParty.IsCurrentlyAtSea, PlayerIsPrisoner = Hero.MainHero.IsPrisoner, LandRouteExists = LandPathExists(spawnPos, playerParty.GetPosition2D)});
            if (block != ReplyCourierBlock.None)
            {
               ModLog.Info($"[COURIER-REPLY] {DescribeReplyBlock(block, sender.Name.ToString())}, letter stays on date-based delivery.");

               return false;
            }

            Settlement home = sender.HomeSettlement ?? sender.CurrentSettlement ?? Hero.MainHero?.HomeSettlement;
            var name = new TextObject($"Letter from {sender.Name}");

            // The reply inherits the thread's escort for free: same larger guard detail, same interception immunity.
            int escortSize = escorted ? TravelingParty.HeavyEscortSize : TravelingParty.EscortSize;
            MobileParty courier = TravelingParty.Spawn(spawnPos, home, name, CourierBaseSpeed, sender.Culture, true, escortSize);

            if (courier == null) return false;

            DirectReplyCourierToPlayer(courier, playerParty);
            courier.IgnoreByOtherPartiesTill(CampaignTime.DaysFromNow(3f)); // ignored until a band is rolled to chase it
            letter.CourierPartyId = courier.StringId;
            ActiveCourierIds.Add(courier.StringId);
            if (escorted) EscortedCourierIds.Add(courier.StringId); // never rolled against in ScanForWaylayers
            TrackCourier(courier);

            ModLog.Info($"[COURIER-REPLY] spawned '{courier.StringId}' from {sender.Name} → player (speed {CourierBaseSpeed}, escorted {escorted}).");

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
               MarkUndeliverable(letter, UndeliverableReason(recipient, letter.RecipientName));
               Disband(courier);
               store.Set(profile);

               return true;
            }

            if (!HasArrived(courier, recipient)) return true; // matched this courier — stop scanning

            Deliver(letter, false, profile);
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

            // Robustness batch: never deliver to (or keep chasing) a captive player this hour; the daily
            // TickReplyCouriers reclaims the courier and the letter returns to date-based delivery.
            if (Hero.MainHero.IsPrisoner) return;

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

      private static void Deliver(PlayerLetter letter, bool courierGone, NpcProfile profile)
      {
         letter.IsDelivered = true;
         letter.CourierPartyId = null;
         PlayerLetterService.RecordReceipt(profile, letter); // durable memory so the NPC remembers it in later talk

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your letter has reached {letter.RecipientName}."));
         ModLog.Info($"[COURIER] delivered to {letter.RecipientName} " + $"(courierGone={courierGone}).");
      }

      /// <summary>M5 (courier audit): the recipient is sailing, so a land courier cannot reach them where they float.</summary>
      private static bool RecipientAtSea(Hero recipient)
         => recipient?.PartyBelongedTo is {IsCurrentlyAtSea: true};

      /// <summary>
      ///   Courier audit (island report): true when an overland route exists between two map points for a
      ///   NavigationType.Default party (couriers ride, they never sail). Fails OPEN on any engine hiccup —
      ///   a letter must never be muted by a pathing query failure, only by a confirmed absence of road.
      /// </summary>
      private static bool LandPathExists(Vec2 from, Vec2 to)
      {
         try
         {
            MapDistanceModel model = Campaign.Current?.Models?.MapDistanceModel;
            if (model == null) return true;

            return model.PathExistBetweenPoints(new CampaignVec2(from, true), new CampaignVec2(to, true), MobileParty.NavigationType.Default);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] land-path check failed: {ex.GetType().Name}: {ex.Message}");

            return true;
         }
      }

      /// <summary>
      ///   Where a courier must actually ride to reach <paramref name="recipient" /> today: their landfall
      ///   when they sail (the courier waits there, M5), their party or settlement otherwise. Used by the
      ///   land-route checks, so a recipient at sea is judged by the shore the courier heads for, not by
      ///   the water they float on. <paramref name="fallback" /> when no target resolves (a path from a
      ///   point to itself always exists, so an unresolvable target never trips the island guard).
      /// </summary>
      private static Vec2 CourierDestinationPoint(Hero recipient, Vec2 fallback)
      {
         if (RecipientAtSea(recipient))
            return TravelingParty.NearestLandfall(recipient.PartyBelongedTo.GetPosition2D);

         switch (ResolveCourierTarget(recipient))
         {
            case Settlement settlement: return settlement.GatePosition.ToVec2();
            case MobileParty party: return party.GetPosition2D;
            default: return fallback;
         }
      }

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
            EscortedCourierIds.Remove(courier.StringId); // delivery/recall/orphan-clear all route through here
            LastMovements.Remove(courier.StringId);

            // Released through the registry, so a courier the mod retires on purpose is never reported as a loss.
            if (TransientPartyRegistry.Instance != null) TransientPartyRegistry.Instance.Release(courier);
            else TravelingParty.Destroy(courier);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] Disband failed: {ex.Message}");
         }
      }

      /// <summary>
      ///   The specific reason a letter could not reach its recipient, for the player's report: a death and a
      ///   capture are different stories, and the courier's report is the only channel that tells them.
      ///   Wording rules live in <see cref="UndeliverableReasonDescriber" /> (pure, tested).
      /// </summary>
      private static string UndeliverableReason(Hero recipient, string recipientName)
         => UndeliverableReasonDescriber.Describe(
            UndeliverableReasonDescriber.Resolve(recipient is {IsAlive: false}, recipient is {IsPrisoner: true}),
            recipientName);

      /// <summary>Log wording for each road-gate block, so the log tells the accurate story per reason.</summary>
      private static string DescribeReplyBlock(ReplyCourierBlock block, string senderName)
      {
         switch (block)
         {
            case ReplyCourierBlock.PlayerAtSea: return $"player at sea, no rider from {senderName}";
            case ReplyCourierBlock.PlayerIsPrisoner: return $"player is a prisoner, no rider from {senderName}";
            default: return $"no land route from {senderName} to the player";
         }
      }

      /// <summary>
      ///   Robustness batch: refills a courier that has eaten through its spawn grain (TravelingParty stocks
      ///   100 units, but the M5 wait at a landfall for a sailing recipient can outlast any fixed stock).
      ///   Without food the engine starves and destroys the party, which reads to the player as a lost letter.
      ///   WHEN is <see cref="CourierRationPolicy" /> (tested); how much is the constant above.
      /// </summary>
      private static void TopUpRations(MobileParty courier)
      {
         try
         {
            if (courier == null || !CourierRationPolicy.ShouldTopUp((int) courier.TotalFoodAtInventory)) return;

            var grain = Game.Current.ObjectManager.GetObject<ItemObject>("grain");
            if (grain != null) courier.ItemRoster.AddToCounts(grain, RationTopUpUnits);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] TopUpRations failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Robustness batch: true when this LIVE courier has sat within <see cref="StuckEpsilon" /> of the same
      ///   spot for <see cref="StuckDaysBeforeFallback" /> daily ticks. Movement resets the clock. The record
      ///   is pruned on disband/destroy and cleared with the other runtime caches at session launch.
      /// </summary>
      private static bool CourierIsStuck(MobileParty courier, int today)
      {
         if (courier?.StringId == null) return false;

         Vec2 pos = courier.GetPosition2D;
         if (!LastMovements.TryGetValue(courier.StringId, out StuckRecord last) ||
             CourierStuckPolicy.HasMoved(pos.Distance(last.Position)))
         {
            LastMovements[courier.StringId] = new StuckRecord {Position = pos, Day = today};

            return false;
         }

         return CourierStuckPolicy.IsStuck(today, last.Day);
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
            ResendAutomatically(resendStore, resendProfile, letter, CourierLoss.Lost);
      }

      private static bool NpcToPlayerReplyCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId, PartyBase destroyer)
      {
         PendingLetter reply = profile.SentLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (reply == null) return false;

         Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
         CourierLoss loss = ClassifyLoss(destroyer, sender?.Clan);
         bool byBandit = loss == CourierLoss.InterceptedByBandits;

         // Gabriel (26/09/2026): the letter goes again until it arrives. Not lost: its courier id is cleared, so the
         // daily dispatch sends a fresh courier, guarded because it is a resend, and the date-based fallback still
         // covers a courier that cannot be spawned. A dead sender's letter still dies with them, on that same tick.
         bool resent = CourierSecretPolicy.IsResent(loss);
         if (resent) reply.Resent = true;
         else reply.IsLost = true;
         reply.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);

         // The sender knows the courier never arrived, and why (raphareish, 24/09/2026: asked about a letter lost on
         // the road, a sender said he had "been busy", because nothing of it was left anywhere).
         var today = (int) CampaignTime.Now.ToDays;
         profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
            CourierSecretPolicy.SenderLossMemory(loss, Hero.MainHero?.Name?.ToString(), resent)));
         store.Set(profile);

         MBInformationManager.AddQuickInformation(new TextObject(
            $"A courier from {reply.SenderName} {HowLost(loss)}. " + (resent ? $"{reply.SenderName} sends the letter again." : "Their letter is lost.")));

         // Courier Inc 4a: what it said now travels with whoever took it.
         if (CourierSecretPolicy.BecomesSecret(loss, reply.Content))
            CourierSecretBehavior.Instance?.Capture(new CarriedSecret {
               SenderId = profile.Id, SenderName = reply.SenderName ?? profile.Name,
               RecipientId = Hero.MainHero?.StringId, RecipientName = Hero.MainHero?.Name?.ToString(),
               Content = reply.Content, FromPlayer = false
            }, destroyer, loss);
         ModLog.Info($"[COURIER-REPLY] destroyed ({(byBandit ? "waylaid" : "non-bandit")}): reply from {reply.SenderName} lost.");

         return true;
      }

      private static bool PlayerToNpcCourierLost(BannerlordNpcMemoryStore store, NpcProfile profile, string partyId, PartyBase destroyer)
      {
         PlayerLetter forward = profile.ReceivedPlayerLetters.FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);

         if (forward == null) return false;

         CourierLoss loss = ClassifyLoss(destroyer, Clan.PlayerClan);
         bool byBandit = loss == CourierLoss.InterceptedByBandits;

         forward.IsLost = true;
         forward.CourierPartyId = null;
         ActiveCourierIds.Remove(partyId);
         store.Set(profile);

         ModLog.Info($"[COURIER] destroyed ({(byBandit ? "waylaid" : "non-bandit")}): letter to {forward.RecipientName} lost.");

         // Gabriel (26/09/2026): the letter goes again until it arrives, no longer on a Send-again prompt the player
         // could decline. The lost letter stays as history; a fresh one carries the same text.
         ResendAutomatically(store, profile, forward, loss);

         // Courier Inc 4a (Gabriel, 26/09/2026): an intercepted letter with substance becomes a secret carried in the
         // world (CourierSecretBehavior), replacing the old instant leak to an antagonist picked by a roll.
         if (CourierSecretPolicy.BecomesSecret(loss, forward.Content))
            CourierSecretBehavior.Instance?.Capture(new CarriedSecret {
               SenderId = Hero.MainHero?.StringId, SenderName = Hero.MainHero?.Name?.ToString(),
               RecipientId = forward.RecipientId, RecipientName = forward.RecipientName,
               Content = forward.Content, FromPlayer = true
            }, destroyer, loss);

         return true;
      }

      /// <summary>The honest cause of a courier's loss, as the player is told it (1.10: never invent bandits).</summary>
      private static string HowLost(CourierLoss loss) => loss switch
      {
         CourierLoss.InterceptedByBandits => "was waylaid by bandits",
         CourierLoss.InterceptedByEnemy => "was taken by enemy soldiers",
         _ => "was lost on the road"
      };

      /// <summary>
      ///   A player letter whose courier was lost goes again at once, with the same text, on a guarded courier
      ///   (Gabriel, 26/09/2026: sent again until it arrives). Replaces the old Send-again prompt.
      /// </summary>
      private static void ResendAutomatically(BannerlordNpcMemoryStore store, NpcProfile profile, PlayerLetter letter, CourierLoss loss)
      {
         try
         {
            MBInformationManager.AddQuickInformation(new TextObject($"Your courier to {letter.RecipientName} {HowLost(loss)}."));
            ResendWaylaidLetter(store, profile, letter);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] ResendAutomatically failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Re-queues a lost letter (same content) on a fresh, guarded courier; falls back to date delivery.</summary>
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
            PlayerLetterService.CreateAndQueue(profile, letter.Content, (int) CampaignTime.Now.ToDays, store, resent: true);
            MBInformationManager.AddQuickInformation(new TextObject($"A fresh, guarded courier carries your letter to {letter.RecipientName}."));
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
            Deliver(letter, false, profile);
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

      #endregion
   }
}