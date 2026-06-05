// Code written by Gabriel Mailhot, 04/06/2026.
// Sprint 12c: visible courier party carrying a player letter across the campaign map.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
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
    ///
    ///   Lifecycle:
    ///   - <see cref="TrySpawnCourier" /> at send time, stamping the party id on the letter.
    ///   - <see cref="TickCouriers" /> daily: delivers on arrival, force-delivers past the
    ///     safety deadline, and prunes couriers whose party has vanished.
    ///   - <see cref="HandlePartyDestroyed" /> on the MobilePartyDestroyed event: marks the
    ///     letter lost when its courier is killed.
    /// </summary>
    public static class CourierService
    {
        // Base map speed of the courier. Set to 6.5 so it is faster than any army
        // but still looks like a fast horseman rather than a racing car.
        private const float CourierBaseSpeed = 6.5f;

        // A small, weak escort so the party is a real (defeatable) target, not invincible.
        private const int CourierEscortSize = 3;

        // Map-unit distance at which the courier counts as "arrived" at a roaming recipient.
        private const float ArrivalDistance = 2.0f;

        // Grace days past DeliveryDay before we force-deliver a stuck courier (anti-soft-lock).
        private const int ForceDeliveryGraceDays = 4;

        // StringIds of all active couriers. Used by CourierSpeedPatch for O(1) identity
        // checks on every party every tick — avoids display-string allocations on the hot path.
        // Rebuilt from live letters on session launch to survive save/load cycles.
        private static readonly HashSet<string> _activeCourierIds = new HashSet<string>();

        /// <summary>
        ///   True when the recipient can actually receive a letter — alive, present, and
        ///   not a prisoner. A dead, captured, or vanished recipient is undeliverable.
        /// </summary>
        public static bool CanReceiveLetter(Hero recipient)
            => recipient != null && recipient.IsAlive && !recipient.IsPrisoner && ResolveCourierTarget(recipient) != null;

        /// <summary>
        ///   Returns true when <paramref name="partyId" /> belongs to one of our active
        ///   couriers. Called by <see cref="CourierSpeedPatch" /> on every
        ///   <c>CalculateFinalSpeed</c> invocation — must be allocation-free.
        /// </summary>
        public static bool IsCourier(string partyId)
            => !string.IsNullOrEmpty(partyId) && _activeCourierIds.Contains(partyId);

        /// <summary>
        ///   Rebuilds the in-memory courier id set from the persisted store after a
        ///   save/load. Call once at session launch so the speed patch keeps working
        ///   for couriers that were in flight when the game was saved.
        /// </summary>
        public static void RebuildActiveCourierSet(BannerlordNpcMemoryStore store)
        {
            _activeCourierIds.Clear();
            if (store == null) return;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile == null) continue;

                // Player→NPC couriers
                if (profile.ReceivedPlayerLetters != null)
                {
                    foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
                    {
                        if (letter.IsDelivered || letter.IsLost) continue;
                        if (string.IsNullOrEmpty(letter.CourierPartyId)) continue;
                        MobileParty courier = FindCourier(letter.CourierPartyId);
                        if (courier != null && courier.IsActive)
                            _activeCourierIds.Add(letter.CourierPartyId);
                    }
                }

                // NPC→Player reply couriers
                if (profile.SentLetters != null)
                {
                    foreach (PendingLetter letter in profile.SentLetters)
                    {
                        if (letter.IsDelivered || letter.IsLost) continue;
                        if (string.IsNullOrEmpty(letter.CourierPartyId)) continue;
                        MobileParty courier = FindCourier(letter.CourierPartyId);
                        if (courier != null && courier.IsActive)
                            _activeCourierIds.Add(letter.CourierPartyId);
                    }
                }
            }

            ModLog.Info($"[COURIER] rebuilt active set: {_activeCourierIds.Count} courier(s) in flight.");
        }

        /// <summary>
        ///   Finds where the courier should go to reach the hero.
        ///   Returns either a MobileParty (hero's party) or a Settlement (where they stay).
        /// </summary>
        public static object ResolveCourierTarget(Hero recipient)
        {
            if (recipient == null) return null;
            if (recipient.PartyBelongedTo != null) return recipient.PartyBelongedTo;
            if (recipient.CurrentSettlement != null) return recipient.CurrentSettlement;
            if (recipient.StayingInSettlement != null) return recipient.StayingInSettlement;
            // Home settlement is a last-resort anchor, but only if they are not roaming elsewhere.
            if (recipient.HomeSettlement != null && recipient.PartyBelongedTo == null) return recipient.HomeSettlement;
            return null;
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
        public static bool TrySpawnCourier(PlayerLetter letter, Hero recipient,
            float? speedOverride = null, bool avoidHostile = true)
        {
            try
            {
                if (letter == null || recipient == null) return false;
                MobileParty player = MobileParty.MainParty;
                if (player == null) return false;

                TroopRoster roster = BuildEscortRoster(recipient);
                Settlement home = recipient.HomeSettlement
                               ?? recipient.CurrentSettlement
                               ?? Hero.MainHero?.HomeSettlement;

                var name = new TextObject($"Courier to {recipient.Name}");
                float speed = speedOverride ?? CourierBaseSpeed;

                MobileParty courier = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
                    player.Position,
                    spawnRadius:           1f,
                    homeSettlement:        home,
                    name:                  name,
                    clan:                  Clan.PlayerClan,
                    troopRoster:           roster,
                    prisonerRoster:        TroopRoster.CreateDummyTroopRoster(),
                    owner:                 Hero.MainHero,
                    partyMountStringId:    null,
                    partyHarnessStringId:  null,
                    customPartyBaseSpeed:  speed,
                    avoidHostileActions:   avoidHostile);

                if (courier == null) return false;

                // Ensure a stable, identifiable StringId for the speed patch.
                // We don't control StringId in CreateCustomParty directly, but we can
                // check if it's already used or prefix-it if possible (most CustomParties
                // are named 'customparty_N'). Since Bannerlord manages StringId internally,
                // we rely on the prefix check in the patch, or we can use the Name.

                // Add spare horses to ensure maximum map speed even if troops are foot-bound.
                ItemObject sumpterHorse = Game.Current.ObjectManager.GetObject<ItemObject>("sumpter_horse");
                if (sumpterHorse != null)
                    courier.ItemRoster.AddToCounts(sumpterHorse, CourierEscortSize + 2);

                DirectCourierToRecipient(courier, recipient);
                letter.CourierPartyId = courier.StringId;
                _activeCourierIds.Add(courier.StringId);

                ModLog.Info($"[COURIER] spawned '{courier.StringId}' → {recipient.Name} " +
                            $"(speed {speed}, escort {CourierEscortSize}, avoidHostile {avoidHostile}).");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COURIER] TrySpawnCourier failed: {ex.Message}");
                return false;
            }
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

                bool changed = false;
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
                    if (courier == null || !courier.IsActive)
                    {
                        if (today >= letter.DeliveryDay + ForceDeliveryGraceDays)
                        {
                            MarkUndeliverable(letter, $"the courier to {letter.RecipientName} never arrived");
                            changed = true;
                        }
                        continue;
                    }

                    if (HasArrived(courier, recipient))
                    {
                        // Deliver BEFORE disbanding: Deliver sets IsDelivered + nulls
                        // CourierPartyId, so if ApplyForDisbanding raises MobilePartyDestroyed
                        // synchronously, HandlePartyDestroyed sees a delivered letter and no-ops.
                        Deliver(letter, recipient, courierGone: false);
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
                        if (target is Settlement settlement && courier.CurrentSettlement == settlement)
                        {
                             // Already inside the target settlement.
                        }
                        else
                        {
                            DirectCourierToRecipient(courier, recipient);
                        }
                    }
                }

                if (changed) store.Set(profile);
            }
        }

        /// <summary>
        ///   HourlyTickParty handler. Handles both directions:
        ///   - Player→NPC courier: delivers when close to the recipient.
        ///   - NPC→Player reply courier: delivers when close to the player; also
        ///     refreshes its <c>SetMoveGoToPoint</c> target so it tracks a moving player.
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
        ///   Spawns a visible courier at the NPC sender's location that travels toward
        ///   the player. Called from the main-thread daily tick once the NPC's chosen
        ///   <see cref="PendingLetter.DispatchDay" /> has arrived.
        ///   Returns false on any failure (letter retains its date-based fallback).
        /// </summary>
        public static bool TrySpawnReplyCourier(PendingLetter letter, Hero sender)
        {
            try
            {
                if (letter == null || sender == null) return false;
                MobileParty playerParty = MobileParty.MainParty;
                if (playerParty == null) return false;

                Vec2 spawnPos = GetSenderPosition(sender);
                Settlement home = sender.HomeSettlement
                               ?? sender.CurrentSettlement
                               ?? Hero.MainHero?.HomeSettlement;

                TroopRoster roster = BuildEscortRoster(sender);
                var name = new TextObject($"Letter from {sender.Name}");

                MobileParty courier = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
                    new CampaignVec2(spawnPos, true),
                    spawnRadius:          1f,
                    homeSettlement:       home,
                    name:                 name,
                    clan:                 Clan.PlayerClan,
                    troopRoster:          roster,
                    prisonerRoster:       TroopRoster.CreateDummyTroopRoster(),
                    owner:                Hero.MainHero,
                    partyMountStringId:   null,
                    partyHarnessStringId: null,
                    customPartyBaseSpeed: CourierBaseSpeed,
                    avoidHostileActions:  true);

                if (courier == null) return false;

                ItemObject sumpterHorse = Game.Current.ObjectManager.GetObject<ItemObject>("sumpter_horse");
                if (sumpterHorse != null)
                    courier.ItemRoster.AddToCounts(sumpterHorse, CourierEscortSize + 2);

                DirectReplyCourierToPlayer(courier, playerParty);
                letter.CourierPartyId = courier.StringId;
                _activeCourierIds.Add(courier.StringId);

                ModLog.Info($"[COURIER-REPLY] spawned '{courier.StringId}' from {sender.Name} → player (speed {CourierBaseSpeed}).");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COURIER-REPLY] TrySpawnReplyCourier failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///   MobilePartyDestroyed handler: if the destroyed party is a courier (either
        ///   direction), marks its letter lost and notifies the player.
        ///   Returns true when a courier was matched.
        /// </summary>
        public static bool HandlePartyDestroyed(MobileParty destroyed, BannerlordNpcMemoryStore store)
        {
            if (destroyed == null || store == null) return false;
            string partyId = destroyed.StringId;
            if (string.IsNullOrEmpty(partyId)) return false;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile == null) continue;

                // Player→NPC courier
                PlayerLetter forward = profile.ReceivedPlayerLetters?
                    .FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);
                if (forward != null)
                {
                    forward.IsLost         = true;
                    forward.CourierPartyId = null;
                    _activeCourierIds.Remove(partyId);
                    store.Set(profile);
                    MBInformationManager.AddQuickInformation(new TextObject(
                        $"Your courier to {forward.RecipientName} was waylaid. The letter is lost."));
                    ModLog.Info($"[COURIER] destroyed — letter to {forward.RecipientName} lost.");
                    return true;
                }

                // NPC→Player reply courier
                PendingLetter reply = profile.SentLetters?
                    .FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);
                if (reply != null)
                {
                    reply.IsLost         = true;
                    reply.CourierPartyId = null;
                    _activeCourierIds.Remove(partyId);
                    store.Set(profile);
                    MBInformationManager.AddQuickInformation(new TextObject(
                        $"A courier from {reply.SenderName} was waylaid. Their letter is lost."));
                    ModLog.Info($"[COURIER-REPLY] destroyed — reply from {reply.SenderName} lost.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///   SettlementEntered handler: when a courier enters the settlement where its
        ///   recipient actually is, deliver immediately. This is the primary, prompt
        ///   delivery path (the daily tick is only a backup). Returns true on delivery.
        /// </summary>
        public static bool HandleSettlementEntered(
            MobileParty party, Settlement settlement, BannerlordNpcMemoryStore store)
        {
            if (party == null || settlement == null || store == null) return false;
            string partyId = party.StringId;
            if (string.IsNullOrEmpty(partyId)) return false;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile?.ReceivedPlayerLetters == null) continue;

                PlayerLetter letter = profile.ReceivedPlayerLetters
                    .FirstOrDefault(l => l.CourierPartyId == partyId && !l.IsDelivered && !l.IsLost);
                if (letter == null) continue;

                Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);
                if (CanReceiveLetter(recipient) && ResolveCourierTarget(recipient) as Settlement == settlement)
                {
                    Deliver(letter, recipient, courierGone: false);
                    Disband(party);
                    store.Set(profile);
                    return true;
                }

                return false; // matched the courier, but not its recipient's settlement
            }

            return false;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void Deliver(PlayerLetter letter, Hero recipient, bool courierGone)
        {
            letter.IsDelivered    = true;
            letter.CourierPartyId = null;

            MBInformationManager.AddQuickInformation(new TextObject(
                $"Your letter has reached {letter.RecipientName}."));
            ModLog.Info($"[COURIER] delivered to {letter.RecipientName} " +
                        $"(courierGone={courierGone}).");
        }

        /// <summary>
        ///   Terminal failure: the letter never reaches its recipient (recipient dead,
        ///   captured, or unreachable). Flags <see cref="PlayerLetter.IsLost" /> so it is
        ///   never delivered nor injected into a prompt, and tells the player.
        /// </summary>
        private static void MarkUndeliverable(PlayerLetter letter, string reason)
        {
            letter.IsLost         = true;
            letter.CourierPartyId = null;

            MBInformationManager.AddQuickInformation(new TextObject(
                $"Your letter could not be delivered — {reason}."));
            ModLog.Info($"[COURIER] undeliverable to {letter.RecipientName}: {reason}.");
        }

        private static void DirectCourierToRecipient(MobileParty courier, Hero recipient)
        {
            object target = ResolveCourierTarget(recipient);

            // Release the AI lock before issuing the new order: if the lock is held while
            // the party has completed its previous GoToPoint, it cannot act on the new order
            // (SetDoNotMakeNewDecisions prevents the party from transitioning to a new goal).
            // We re-lock immediately after so autonomous flee-from-hostiles still can't
            // override the explicit order between daily ticks.
            courier.Ai.SetDoNotMakeNewDecisions(false);

            if (target is Settlement settlement)
            {
                courier.SetMoveGoToSettlement(settlement, MobileParty.NavigationType.Default, false);
            }
            else if (target is MobileParty party)
            {
                // EngageParty uses the engine's built-in pursuit AI: the courier
                // dynamically tracks the target party at its own speed without any
                // manual position updates. No combat fires because the courier and the
                // lord are not hostile to each other — the engage mode just means "catch up".
                courier.SetMoveEngageParty(party, MobileParty.NavigationType.Default);
            }

            courier.Ai.SetDoNotMakeNewDecisions(true);
        }

        private static bool HasArrived(MobileParty courier, Hero recipient)
        {
            object target = ResolveCourierTarget(recipient);
            if (target is Settlement settlement)
            {
                if (courier.CurrentSettlement == settlement) return true;
                if (courier.GetPosition2D.Distance(settlement.GetPosition2D) < ArrivalDistance) return true;
            }
            else if (target is MobileParty party)
            {
                if (courier.GetPosition2D.Distance(party.GetPosition2D) < ArrivalDistance) return true;
            }

            return false;
        }

        private static MobileParty FindCourier(string partyId)
        {
            if (string.IsNullOrEmpty(partyId)) return null;
            return MobileParty.All.FirstOrDefault(p => p.StringId == partyId);
        }

        private static void Disband(MobileParty courier)
        {
            try
            {
                if (courier == null) return;
                _activeCourierIds.Remove(courier.StringId);
                if (courier.IsActive)
                    DestroyPartyAction.ApplyForDisbanding(courier, courier.CurrentSettlement);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COURIER] Disband failed: {ex.Message}");
            }
        }

        private static bool CheckPlayerLetterCourierArrival(MobileParty courier, BannerlordNpcMemoryStore store)
        {
            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile?.ReceivedPlayerLetters == null) continue;

                PlayerLetter letter = profile.ReceivedPlayerLetters
                    .FirstOrDefault(l => l.CourierPartyId == courier.StringId
                                      && !l.IsDelivered && !l.IsLost);
                if (letter == null) continue;

                Hero recipient = Hero.FindFirst(h => h.StringId == letter.RecipientId);
                if (!CanReceiveLetter(recipient))
                {
                    MarkUndeliverable(letter, $"{letter.RecipientName} could not be reached");
                    Disband(courier);
                    store.Set(profile);
                    return true;
                }

                if (HasArrived(courier, recipient))
                {
                    Deliver(letter, recipient, courierGone: false);
                    Disband(courier);
                    store.Set(profile);
                }
                return true; // matched this courier — stop scanning
            }

            return false;
        }

        private static void CheckReplyCourierArrival(MobileParty courier, BannerlordNpcMemoryStore store)
        {
            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                PendingLetter letter = profile?.SentLetters?
                    .FirstOrDefault(l => l.CourierPartyId == courier.StringId
                                      && !l.IsDelivered && !l.IsLost);
                if (letter == null) continue;

                MobileParty playerParty = MobileParty.MainParty;
                if (playerParty == null) return;

                float dist = courier.GetPosition2D.Distance(playerParty.GetPosition2D);
                if (dist < ArrivalDistance)
                {
                    // Deliver before disband — clear the party id so the popup handler
                    // and the next HourlyTick won't re-match this courier.
                    letter.CourierPartyId = null;
                    _activeCourierIds.Remove(courier.StringId);
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

        private static void DirectReplyCourierToPlayer(MobileParty courier, MobileParty playerParty)
        {
            courier.Ai.SetDoNotMakeNewDecisions(false);
            courier.SetMoveGoToPoint(
                new CampaignVec2(playerParty.GetPosition2D, true),
                MobileParty.NavigationType.Default);
            courier.Ai.SetDoNotMakeNewDecisions(true);
        }

        private static Vec2 GetSenderPosition(Hero sender)
        {
            if (sender.PartyBelongedTo != null) return sender.PartyBelongedTo.GetPosition2D;
            if (sender.CurrentSettlement  != null) return sender.CurrentSettlement.GetPosition2D;
            if (sender.StayingInSettlement != null) return sender.StayingInSettlement.GetPosition2D;
            return MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
        }

        private static TroopRoster BuildEscortRoster(Hero recipient)
        {
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            // Prefer mounted troops so the courier rides faster than foot-bound parties
            // and can actually catch a roaming recipient; fall back to the basic recruit.
            CharacterObject troop = FindMountedTroop(recipient.Culture)
                                 ?? FindMountedTroop(Hero.MainHero?.Culture)
                                 ?? recipient.Culture?.BasicTroop
                                 ?? Hero.MainHero?.Culture?.BasicTroop;
            if (troop != null) roster.AddToCounts(troop, CourierEscortSize);
            return roster;
        }

        /// <summary>
        ///   Walks a culture's basic-troop upgrade tree (breadth-first) for the first
        ///   mounted troop, so the courier rides rather than marches. Returns null when
        ///   the culture's tree has no cavalry.
        /// </summary>
        private static CharacterObject FindMountedTroop(CultureObject culture)
        {
            CharacterObject basic = culture?.BasicTroop;
            if (basic == null) return null;

            var seen  = new HashSet<string>();
            var queue = new Queue<CharacterObject>();
            queue.Enqueue(basic);
            while (queue.Count > 0)
            {
                CharacterObject c = queue.Dequeue();
                if (c == null || !seen.Add(c.StringId)) continue;
                if (c.IsMounted) return c;
                if (c.UpgradeTargets != null)
                    foreach (CharacterObject up in c.UpgradeTargets) queue.Enqueue(up);
            }
            return null;
        }
    }
}
