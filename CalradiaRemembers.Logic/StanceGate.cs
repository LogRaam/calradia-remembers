// Code written by Gabriel Mailhot, 21/06/2026.
// Generalizes RelationGate's "deeds over words" philosophy to the Trust/Respect/Fear axes: the player's
// WORDS may nudge a stance, but only a little and rate-limited (charm and threats colour a meeting, they
// do not remake a judgement); a proven DEED moves it strongly, because there is proof. Pure so these
// caps — the whole anti-exploit guarantee, and they break in silence — stay protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One exchange's spoken nudges to the moveable axes (Affection is not moved here — it is
    ///   the existing reputation, moved by change_relation). Each is capped hard by <see cref="StanceGate"/>.</summary>
    public sealed class StanceShift
    {
        public int Trust { get; set; }
        public int Respect { get; set; }
        public int Fear { get; set; }
    }

    /// <summary>
    ///   Moves the Trust/Respect/Fear axes under the "words nudge, deeds prove" rule. A spoken round is
    ///   capped to <see cref="WordCap"/> per axis and rate-limited to one per <see cref="WordCooldownHours"/>;
    ///   a deed applies a far larger (still bounded) delta with no cooldown. Fear fades over time.
    /// </summary>
    public static class StanceGate
    {
        /// <summary>Most a single spoken round can shift an axis — words only colour, never remake.</summary>
        public const int WordCap = 2;

        /// <summary>Minimum in-game hours between two accepted spoken rounds (matches RelationGate).</summary>
        public const double WordCooldownHours = 8.0;

        /// <summary>Most a single proven deed can shift an axis — strong, because there is proof.</summary>
        public const int DeedCap = 30;

        /// <summary>How much fear fades per in-game day as the threat recedes.</summary>
        public const int FearDecayPerDay = 2;

        /// <summary>Clamps a value to its axis's range: Fear is 0..100, the rest are −100..+100.</summary>
        public static int ClampAxis(StanceAxis axis, int value)
            => axis == StanceAxis.Fear ? Clamp(value, 0, 100) : Clamp(value, -100, 100);

        /// <summary>
        ///   Applies one exchange's spoken nudges to Trust/Respect/Fear, each capped to
        ///   <see cref="WordCap"/>, but ONLY when the cooldown has elapsed and the round actually moves
        ///   something — so repeated flattery cannot grind a stance. Returns true when it was applied.
        /// </summary>
        public static bool TryApplyWordRound(NpcStance stance, StanceShift shift, double currentHour)
        {
            if (stance == null || shift == null) return false;
            if (shift.Trust == 0 && shift.Respect == 0 && shift.Fear == 0) return false; // nothing to say
            if (stance.LastWordInfluenceHour.HasValue
                && currentHour - stance.LastWordInfluenceHour.Value < WordCooldownHours)
                return false;

            stance.Trust   = ClampAxis(StanceAxis.Trust,   stance.Trust   + Clamp(shift.Trust,   -WordCap, WordCap));
            stance.Respect = ClampAxis(StanceAxis.Respect, stance.Respect + Clamp(shift.Respect, -WordCap, WordCap));
            stance.Fear    = ClampAxis(StanceAxis.Fear,    stance.Fear    + Clamp(shift.Fear,    -WordCap, WordCap));
            stance.LastWordInfluenceHour = currentHour;
            return true;
        }

        /// <summary>
        ///   Applies a proven deed to one axis: a strong shift (bounded by <see cref="DeedCap"/>),
        ///   clamped to the axis range, with no cooldown — proof moves perception decisively. Affection
        ///   may be passed but the host keeps it on the existing reputation path to avoid double-moving.
        /// </summary>
        public static void ApplyDeed(NpcStance stance, StanceAxis axis, int delta)
        {
            if (stance == null || delta == 0) return;
            int capped = Clamp(delta, -DeedCap, DeedCap);
            SetAxis(stance, axis, ClampAxis(axis, GetAxis(stance, axis) + capped));
        }

        /// <summary>Fades fear toward zero as days pass and the threat recedes. A no-op on calm or non-positive days.</summary>
        public static void ApplyFearDecay(NpcStance stance, int daysElapsed)
        {
            if (stance == null || daysElapsed <= 0 || stance.Fear <= 0) return;
            stance.Fear = ClampAxis(StanceAxis.Fear, stance.Fear - FearDecayPerDay * daysElapsed);
        }

        /// <summary>
        ///   M-S2 (regard audit): fades Trust and Respect ONE step toward zero (both directions). Without any
        ///   fade, a standing earned or lost once became a permanent switch, a five-year-old favour opening troop
        ///   delegation (Trust >= 20) forever. Deliberately far gentler than fear: the host calls this only on a
        ///   multi-day interval, so a standing lingers for YEARS of neglect before it lapses, and any deed renews
        ///   it in full. Returns true when it actually moved. To disable, the host simply never calls it.
        /// </summary>
        public static bool ApplyStandingDecay(NpcStance stance)
        {
            if (stance == null) return false;

            int trust = StepTowardZero(stance.Trust);
            int respect = StepTowardZero(stance.Respect);
            bool changed = trust != stance.Trust || respect != stance.Respect;

            stance.Trust = trust;
            stance.Respect = respect;

            return changed;
        }

        #region private

        private static int StepTowardZero(int v) => v > 0 ? v - 1 : v < 0 ? v + 1 : 0;

        private static int GetAxis(NpcStance s, StanceAxis axis)
        {
            switch (axis)
            {
                case StanceAxis.Affection: return s.Affection;
                case StanceAxis.Trust:     return s.Trust;
                case StanceAxis.Respect:   return s.Respect;
                default:                   return s.Fear;
            }
        }

        private static void SetAxis(NpcStance s, StanceAxis axis, int value)
        {
            switch (axis)
            {
                case StanceAxis.Affection: s.Affection = value; break;
                case StanceAxis.Trust:     s.Trust = value; break;
                case StanceAxis.Respect:   s.Respect = value; break;
                default:                   s.Fear = value; break;
            }
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        #endregion
    }
}
