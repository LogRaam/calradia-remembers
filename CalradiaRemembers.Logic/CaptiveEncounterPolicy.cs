// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's CaptiveEncounterService: how often a captor initiates a scene, and which
// intent it takes from their kinks and their regard for the prisoner. Pure so the probability tuning
// and the (precedence-sensitive) intent ladder — which shape the pacing and variety of a flagship
// adult feature and break in silence — stay protected by tests.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The pure decisions behind a captive encounter: the per-tick probability a captor acts
    ///   (<see cref="EncounterProbability" />) and the intent the scene takes
    ///   (<see cref="SelectIntent" />). The host owns the roll, the witnesses, and every engine guard.
    /// </summary>
    public static class CaptiveEncounterPolicy
    {
        private const float BaseProbabilityPerDay = 0.10f;
        private const float MaxBaseProbability = 0.50f;
        private const float MaxTotalProbability = 0.60f;

        private const float KinkBonusPowerImbalance = 0.10f;
        private const float KinkBonusDominance = 0.05f;
        private const float KinkBonusSadism = 0.05f;
        private const float KinkBonusFreeUse = 0.10f;
        private const float KinkBonusMasterSlave = 0.05f;

        /// <summary>
        ///   Chance, this tick, that the captor initiates an encounter: a base that climbs with days
        ///   in captivity (capped), plus bonuses for the kinks that make a captor more predatory, the
        ///   whole capped again. Pure over the captor's kink set.
        /// </summary>
        public static float EncounterProbability(int daysInCaptivity, IReadOnlyList<Kink> kinks)
        {
            float baseProb = Math.Min(daysInCaptivity * BaseProbabilityPerDay, MaxBaseProbability);

            float bonus = 0f;
            if (kinks != null)
            {
                if (kinks.Contains(Kink.PowerImbalance)) bonus += KinkBonusPowerImbalance;
                if (kinks.Contains(Kink.Dominance)) bonus += KinkBonusDominance;
                if (kinks.Contains(Kink.Sadism)) bonus += KinkBonusSadism;
                if (kinks.Contains(Kink.FreeUse)) bonus += KinkBonusFreeUse;
                if (kinks.Contains(Kink.MasterSlave)) bonus += KinkBonusMasterSlave;
            }

            return Math.Min(baseProb + bonus, MaxTotalProbability);
        }

        /// <summary>
        ///   Picks the captor's intent from their kinks and their regard for the prisoner, walked as a
        ///   priority ladder: cruelty or strong contempt → Torture; warmth + a praise kink → Reward;
        ///   a conditioning kink → Training; warmth (or a desire/use kink with neutral-or-better
        ///   regard) → PersonalDesire; ownership kinks → Domination; otherwise Interrogation. The
        ///   ordering and the &amp;&amp;/|| precedence on the PersonalDesire rung are deliberate.
        /// </summary>
        public static CaptiveSceneIntent SelectIntent(IReadOnlyList<Kink> kinks, int relation)
        {
            bool Has(Kink k) => kinks != null && kinks.Contains(k);

            // Cruelty / pain kinks or strong contempt → torture.
            if (Has(Kink.Sadism) || Has(Kink.ImpactPlay) || Has(Kink.FearPlay) || relation <= -15)
                return CaptiveSceneIntent.Torture;

            // Strong positive regard + praise kink → reward (the positive register).
            if (relation >= 20 && Has(Kink.Praise))
                return CaptiveSceneIntent.Reward;

            // Conditioning kink → an ongoing training session.
            if (Has(Kink.Training))
                return CaptiveSceneIntent.Training;

            // Positive regard, or desire/use kinks with neutral-or-better relation → personal desire.
            // NOTE: && binds tighter than ||, so this is: relation>=10 OR (anyDesireKink AND relation>=0).
            if (relation >= 10
                || (Has(Kink.Dominance)
                    || Has(Kink.FreeUse)
                    || Has(Kink.Breeding)
                    || Has(Kink.OrgasmControl)
                    || Has(Kink.PetPlay))
                && relation >= 0)
                return CaptiveSceneIntent.PersonalDesire;

            // Power / ownership kinks → domination.
            if (Has(Kink.PowerImbalance)
                || Has(Kink.Dominance)
                || Has(Kink.MasterSlave)
                || Has(Kink.Objectification)
                || Has(Kink.Degradation))
                return CaptiveSceneIntent.Domination;

            return CaptiveSceneIntent.Interrogation;
        }
    }
}
