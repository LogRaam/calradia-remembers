// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 2 (resolution layer, R1 kill in battle + R2 execute): listens for
// CampaignEvents.HeroKilledEvent, mirroring ExecutedKinGrudgeBehavior's shape, and closes out any
// tracked NemesisIdentity whose Hero just died. IsAlive/DeathDay already exist on NemesisIdentity
// (added the day the pillar was ratified, 2026-07-05) so this is pure engine glue: no new persisted
// field, no migration concern. A dead nemesis never recurs (see NemesisIdentity.IsAlive's own doc).
// No SyncData of its own: every mutation lands on NemesisBehavior's own roster, which already owns
// the save contract (same pattern as ExecutedKinGrudgeBehavior owning nothing of its own).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Nemesis
{
    /// <summary>
    ///   Listens for <see cref="CampaignEvents.HeroKilledEvent" />; when the dead Hero is a tracked
    ///   nemesis, marks his identity resolved and, when the PLAYER personally dealt the death, records
    ///   a closure deed into the world-event store (worded distinctly for an execution vs a battle
    ///   death by <see cref="NemesisResolutionPolicy" />).
    /// </summary>
    public sealed class NemesisResolutionBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Intentionally empty: every mutation this behaviour makes (IsAlive, DeathDay) lands on
            // NemesisIdentity records inside NemesisBehavior's own roster, which already owns the save
            // contract. Nothing of this behaviour's own needs persisting.
        }

        /// <summary>
        ///   Runs the exact resolution the real HeroKilledEvent listener uses, without requiring an
        ///   actual kill event. Backs both the real hook and the <c>cr.nemesis_kill</c> /
        ///   <c>cr.nemesis_execute</c> test commands, so the console path proves the exact same code
        ///   the engine hook runs (mirrors <see cref="ExecutedKinGrudgeBehavior.PlantOnKinOf" />).
        ///   Returns the resolved identity, or null when <paramref name="victim" /> is not a tracked
        ///   nemesis.
        /// </summary>
        public static NemesisIdentity Resolve(Hero victim, Hero killer, bool wasExecuted)
        {
            if (victim == null || NemesisBehavior.Instance == null) return null;

            NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(victim.StringId);
            if (nem == null || !nem.IsAlive) return nem; // not a tracked nemesis, or already resolved (idempotent)

            var today = (int) CampaignTime.Now.ToDays;
            nem.IsAlive = false;
            nem.DeathDay = today;
            // Cooldown: no fresh nemesis of his band culture rises for 1..3 years, so killing him is not
            // instantly undone by an identical new boss on the next capture (Gabriel 2026-07-08).
            nem.RespawnBlockedUntilDay = today + MBRandom.RandomInt(
               NemesisRosterPolicy.RespawnCooldownMinDays, NemesisRosterPolicy.RespawnCooldownMaxDays + 1);

            // Any grudge the nemesis himself held against the player dies with him.
            GrudgeBehavior.Instance?.ClearGrudgesFor(victim.StringId);

            string displayName = $"{nem.Name} {nem.Epithet}";

            // KNOWN LIMITATION: only a death the player personally dealt (killer == MainHero) is
            // chronicled as a closure deed below. A nemesis who dies off-screen in an NPC-vs-NPC battle
            // the player's side won is still marked resolved above, but records no world event here.
            if (killer != Hero.MainHero)
            {
                // Audit M2: another hand took him, so no lord's bounty on his head can ever be earned now.
                // Close the outstanding ones (a player-earned, awaiting-reward bounty is left untouched, and
                // never exists on this path anyway since the player did not deal the death).
                CancelOutstandingBountiesFor(victim, $"Word comes that {displayName} is dead, by another's hand. The bounty is void.", today);
                ModLog.Info($"[NEMESIS] {displayName} has died, not by the player's own hand; vendetta resolved, no closure deed recorded.");

                return nem;
            }

            NemesisResolutionKind kind = NemesisResolutionPolicy.ResolutionKindFor(wasExecuted);
            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";

            WorldEventStore.Instance?.Record(new WorldEvent {
                Type = WorldEventType.PlayerFeat,
                Day = today,
                Magnitude = NemesisResolutionPolicy.ClosureMagnitude,
                InvolvesPlayer = true,
                Summary = NemesisResolutionPolicy.ClosureSummary(playerName, displayName, kind),
                ChronicleLine = NemesisResolutionPolicy.ClosureChronicleLine(displayName, kind),
                ActorIds = new List<string> {victim.StringId, Hero.MainHero.StringId},
                FactionIds = Hero.MainHero.MapFaction != null
                    ? new List<string> {Hero.MainHero.MapFaction.StringId}
                    : new List<string>()
            });

            ModLog.Info($"[NEMESIS] {displayName} resolved ({kind}); closure deed recorded.");

            return nem;
        }

        /// <summary>
        ///   Nemesis pillar increment 4 (SPARE + TURN): marks a tracked nemesis TURNED (spared and taken
        ///   into the player's service) and closes his vendetta (any grudge he held against the player dies),
        ///   recording the closure deed. The MECHANICAL conversion (release, clan, party) is the bridge's job
        ///   (<see cref="GameState.BannerlordGameStateBridge" />); this owns only the identity + chronicle,
        ///   exactly as <see cref="Resolve" /> does for a death, so the bridge action and the
        ///   <c>cr.test nemesis_turn</c> harness prove the same code. Returns the identity, or null when
        ///   <paramref name="nemesis" /> is not a tracked, still-active nemesis (idempotent when already turned).
        /// </summary>
        public static NemesisIdentity Turn(Hero nemesis)
        {
            if (nemesis == null || NemesisBehavior.Instance == null) return null;

            NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(nemesis.StringId);
            if (nem == null || !nem.IsAlive || nem.IsTurned) return nem; // not tracked, dead, or already turned

            var today = (int) CampaignTime.Now.ToDays;
            nem.TurnedDay = today;

            // His vendetta ends: any grudge he held against the player dies now that he serves them.
            GrudgeBehavior.Instance?.ClearGrudgesFor(nemesis.StringId);

            string displayName = $"{nem.Name} {nem.Epithet}";
            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";

            var actorIds = new List<string> {nemesis.StringId};
            if (Hero.MainHero?.StringId != null) actorIds.Add(Hero.MainHero.StringId);

            WorldEventStore.Instance?.Record(new WorldEvent {
                Type = WorldEventType.PlayerFeat,
                Day = today,
                Magnitude = NemesisResolutionPolicy.TurnedMagnitude,
                InvolvesPlayer = true,
                Summary = NemesisResolutionPolicy.TurnedSummary(playerName, displayName),
                ChronicleLine = NemesisResolutionPolicy.TurnedChronicleLine(displayName),
                ActorIds = actorIds,
                FactionIds = Hero.MainHero?.MapFaction != null
                    ? new List<string> {Hero.MainHero.MapFaction.StringId}
                    : new List<string>()
            });

            // Audit M2/M3: he now serves the player, so he can no longer be captured or slain as a foe. Any
            // bounty on his head is void (a turn is neither the capture nor the kill a bounty asks for).
            CancelOutstandingBountiesFor(nemesis, $"{displayName} has been turned to the player's service. The bounty on him is void.", today);

            ModLog.Info($"[NEMESIS] {displayName} spared and turned to the player's service; vendetta closed.");

            return nem;
        }

        /// <summary>
        ///   Closes every OUTSTANDING lord's bounty on <paramref name="nemesisHero" /> (audit M2): a bounty the
        ///   player can no longer complete (the nemesis is dead by another hand, or turned) must not linger and
        ///   block the giver's one-task slot. A bounty the player already earned (awaiting reward) is left for
        ///   him to collect. Static so both <see cref="Resolve" /> and <see cref="Turn" /> share one path.
        /// </summary>
        private static void CancelOutstandingBountiesFor(Hero nemesisHero, string closureNote, int today)
        {
            if (nemesisHero?.StringId == null) return;

            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            int n = QuestService.CancelOutstandingBountiesOn(nemesisHero.StringId, closureNote, today, store);
            if (n > 0) ModLog.Info($"[NEMESIS] closed {n} outstanding bounty(ies) on {nemesisHero.Name}.");
        }

        #region private

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            SafeRun.Step("NemesisResolutionBehavior.OnHeroKilled", () =>
            {
                Resolve(victim, killer, detail == KillCharacterAction.KillCharacterActionDetail.Executed);
            });
        }

        #endregion
    }
}
