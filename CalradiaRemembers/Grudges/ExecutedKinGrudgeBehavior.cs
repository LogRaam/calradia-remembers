// Code written by Gabriel Mailhot, 06/07/2026.
// Grudges pillar, Tier 3 LIVE plant site (ROADMAP "the vendetta end of the spectrum"): the player
// executes a lord, that lord's close living kin each form a permanent ExecutedKin vendetta. No
// SyncData of its own (nothing here needs persisting: every plant lands in GrudgeBehavior's own
// ledger, which already owns the save contract), so a past execution simply degrades gracefully
// (no backfill by design, exactly like the rest of Tier 3).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.Grudges;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Grudges
{
    /// <summary>
    ///   Listens for <see cref="CampaignEvents.HeroKilledEvent" />; when the PLAYER personally executes
    ///   a lord, plants an <see cref="GrudgeSource.ExecutedKin" /> vendetta grudge on that lord's close
    ///   living kin (spouse, parents, adult children, adult siblings), capped to a handful per
    ///   execution so it never sweeps the whole clan or faction (anti-noise). Battle deaths and
    ///   executions carried out by anyone other than the player never plant anything here.
    /// </summary>
    public sealed class ExecutedKinGrudgeBehavior : CampaignBehaviorBase
    {
        /// <summary>Tuning: the maximum number of kin who ever receive a vendetta grudge from a single execution ("a handful", never the whole house).</summary>
        public const int MaxKinPerExecution = 4;

        public override void RegisterEvents()
        {
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Intentionally empty: this behavior owns no state of its own. Every grudge it plants is
            // persisted by GrudgeBehavior's own ledger, which already has its own SyncData contract.
        }

        /// <summary>
        ///   Runs the exact plant loop the real execution listener uses (kin enumeration, the
        ///   per-execution cap, the real <see cref="GrudgeBehavior.TryPlant" /> gate), without requiring
        ///   an actual execution event. Backs both the real listener and the <c>cr.grudge_execute_kin</c>
        ///   test command, so the console path proves the exact same code the engine hook runs. Returns
        ///   how many kin were considered (up to <see cref="MaxKinPerExecution" />) vs how many actually
        ///   received a grudge (<see cref="GrudgeBehavior.TryPlant" /> can still refuse a full, stronger
        ///   house).
        /// </summary>
        public static (int Considered, int Planted) PlantOnKinOf(Hero victim, string cause, int today)
        {
            if (victim == null || Hero.MainHero == null || GrudgeBehavior.Instance == null) return (0, 0);

            var considered = 0;
            var planted = 0;

            foreach ((Hero kin, ExecutedKinKind kind) in CloseLivingKinOf(victim))
            {
                if (considered >= MaxKinPerExecution) break;

                considered++;

                var facts = new ExecutedKinFacts {
                    Kinship = kind,
                    KinRegardForPlayer = kin.GetRelation(Hero.MainHero)
                };

                GrudgeProposal proposal = ExecutedKinRules.ExecutedKinGrudge(facts);

                if (!proposal.Forms) continue;

                // notify:false: a single execution can seed several kin at once, so we suppress each per-kin
                // banner and raise ONE aggregated notice below instead of a stack of near-identical ones (5.6).
                bool didPlant = GrudgeBehavior.Instance.TryPlant(kin, Hero.MainHero, GrudgeSource.ExecutedKin,
                    cause, proposal.Magnitude, knownToPlayer: true, today, notify: false);

                if (didPlant) planted++;
            }

            if (planted > 0) NotifyKinGrudge(victim);

            return (considered, planted);
        }

        /// <summary>
        ///   One aggregated banner for a whole execution: however many kin took up the vendetta, the player
        ///   sees a single notice under the executed lord's portrait rather than one line per grudging relative.
        ///   Never throws (mirrors GrudgeBehavior.NotifyFormed's own guard).
        /// </summary>
        private static void NotifyKinGrudge(Hero victim)
        {
            try
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject($"{victim.Name}'s kin now hold a blood grudge against you."), 0, victim.CharacterObject);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] executed-kin banner failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #region private

        /// <summary>
        ///   When the player executes a lord, plants a vendetta on that lord's close living kin. Never
        ///   throws into the engine (wrapped in <see cref="SafeRun.Step" />, matching every other
        ///   HeroKilledEvent listener in the mod).
        /// </summary>
        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            SafeRun.Step("ExecutedKinGrudgeBehavior.OnHeroKilled", () =>
            {
                if (victim == null || Hero.MainHero == null || killer != Hero.MainHero) return;
                if (detail != KillCharacterAction.KillCharacterActionDetail.Executed) return;

                var today = (int) CampaignTime.Now.ToDays;
                string cause = $"{Hero.MainHero.Name} had {victim.Name} put to death.";

                PlantOnKinOf(victim, cause, today);
            });
        }

        /// <summary>
        ///   The victim's close living kin eligible for a vendetta: spouse, parents, adult children, adult
        ///   siblings. Excludes the player (should the victim somehow be the player's own kin) and any
        ///   child hero too young to hold an opinion. Never reaches into the wider clan or faction.
        /// </summary>
        private static IEnumerable<(Hero Kin, ExecutedKinKind Kind)> CloseLivingKinOf(Hero victim)
        {
            if (victim.Spouse is {IsAlive: true} spouse && spouse != Hero.MainHero)
                yield return (spouse, ExecutedKinKind.Spouse);

            if (victim.Father is {IsAlive: true} father && father != Hero.MainHero)
                yield return (father, ExecutedKinKind.Parent);

            if (victim.Mother is {IsAlive: true} mother && mother != Hero.MainHero)
                yield return (mother, ExecutedKinKind.Parent);

            if (victim.Children != null)
                foreach (Hero child in victim.Children)
                    if (child is {IsAlive: true, IsChild: false} && child != Hero.MainHero)
                        yield return (child, ExecutedKinKind.Child);

            if (victim.Siblings != null)
                foreach (Hero sibling in victim.Siblings)
                    if (sibling is {IsAlive: true, IsChild: false} && sibling != Hero.MainHero)
                        yield return (sibling, ExecutedKinKind.Sibling);
        }

        #endregion
    }
}
