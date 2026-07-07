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
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

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

            // Any grudge the nemesis himself held against the player dies with him.
            GrudgeBehavior.Instance?.ClearGrudgesFor(victim.StringId);

            string displayName = $"{nem.Name} {nem.Epithet}";

            // KNOWN LIMITATION: only a death the player personally dealt (killer == MainHero) is
            // chronicled as a closure deed below. A nemesis who dies off-screen in an NPC-vs-NPC battle
            // the player's side won is still marked resolved above, but records no world event here.
            if (killer != Hero.MainHero)
            {
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
