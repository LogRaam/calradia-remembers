// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence against an NPC: the ruling. Decides whether the act is permitted at all, how likely it is to come
// off, and therefore how the attempt ends. Pure, because this is the anti-exploit spine of the whole feature and
// Gabriel must be able to read the numbers and tune them without a campaign running.
//
// The design answer to "what stops the player slitting the emperor's throat" is deliberately NOT a whitelist of
// protected characters, which would be arbitrary and would break the moment a mod added a lord. It is the honest
// conjunction the world already provides: the emperor is a far better fighter than most, he is never alone, and
// (elsewhere) the realm hunts you afterwards. So a desperate attempt stays POSSIBLE, as Gabriel asked, and simply
// almost never works.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Violence
{
    /// <summary>Decides whether an act of violence is permitted, how likely it is, and how one attempt ends.</summary>
    public static class ViolencePolicy
    {
        /// <summary>Nothing is ever certain: even a peasant with a knife and surprise has a chance. Tuning.</summary>
        public const int MinChancePercent = 5;

        /// <summary>Nothing is ever guaranteed either: a blade can turn on the best of them. Tuning.</summary>
        public const int MaxChancePercent = 95;

        /// <summary>An even match comes off half the time; everything else scales from here. Tuning.</summary>
        public const int EvenMatchChancePercent = 50;

        /// <summary>Others about who could cry out or intervene: harder, never impossible (Gabriel's ruling). Tuning.</summary>
        public const double OnlookersFactor = 1.4;

        /// <summary>Guards or a retinue at their side: much harder, still not barred. Tuning.</summary>
        public const double GuardedFactor = 2.2;

        /// <summary>Beating someone down asks more than frightening them into paying. Tuning.</summary>
        public const double AssaultFactor = 1.15;

        /// <summary>Killing outright is the hardest thing on this list. Tuning.</summary>
        public const double KillFactor = 1.45;

        /// <summary>How much Roguery is worth to a shakedown, as a divisor (skill 100 = +20 points of chance). Tuning.</summary>
        public const int RogueryDivisor = 5;

        /// <summary>The most Roguery alone can add, so craft never replaces the odds entirely. Tuning.</summary>
        public const int RogueryBonusCap = 20;

        /// <summary>
        ///   Whether the act may be attempted at all. Only killing is gated: Gabriel's MCM toggle governs LETHAL
        ///   violence only (default off), so robbery and assault are always available. The prompt is advice and the
        ///   bridge is law, so this is asked again at execution, not only while teaching.
        /// </summary>
        public static bool IsPermitted(ViolentAct act, bool lethalViolenceEnabled)
            => act != ViolentAct.Kill || lethalViolenceEnabled;

        /// <summary>
        ///   The odds this attempt comes off, as a percentage. An even match is
        ///   <see cref="EvenMatchChancePercent" />; being outmatched drags it down and outclassing them lifts it,
        ///   while the victim's company (<see cref="ViolenceExposure" />) and the severity of the act both make the
        ///   effective opposition stiffer. Roguery sweetens a robbery only. Always inside
        ///   [<see cref="MinChancePercent" />, <see cref="MaxChancePercent" />].
        /// </summary>
        public static int SuccessChance(ViolentAct act, ViolenceFacts facts)
        {
            if (facts == null) return MinChancePercent;

            double player = Math.Max(facts.PlayerProwess, 1);
            double opposition = Math.Max(facts.TargetProwess, 1) * ExposureFactor(facts.Exposure) * ActFactor(act);
            var chance = (int) Math.Round(EvenMatchChancePercent * (player / opposition));

            if (act == ViolentAct.Rob) chance += Math.Min(Math.Max(facts.PlayerRoguery, 0) / RogueryDivisor, RogueryBonusCap);

            return Clamp(chance);
        }

        /// <summary>
        ///   How one attempt ends, given a roll in [0, 100). Deterministic on purpose: the engine rolls, this
        ///   decides, so every outcome is reproducible in a test. A forbidden act never even reaches the roll.
        /// </summary>
        public static ViolenceVerdict Resolve(ViolentAct act, ViolenceFacts facts, bool lethalViolenceEnabled, int rollPercent)
        {
            if (!IsPermitted(act, lethalViolenceEnabled)) return ViolenceVerdict.Forbidden;

            return rollPercent < SuccessChance(act, facts) ? ViolenceVerdict.Succeeded : ViolenceVerdict.Failed;
        }

        private static double ExposureFactor(ViolenceExposure exposure)
        {
            switch (exposure)
            {
                case ViolenceExposure.Guarded: return GuardedFactor;
                case ViolenceExposure.Onlookers: return OnlookersFactor;
                default: return 1.0;
            }
        }

        private static double ActFactor(ViolentAct act)
        {
            switch (act)
            {
                case ViolentAct.Kill: return KillFactor;
                case ViolentAct.Assault: return AssaultFactor;
                default: return 1.0;
            }
        }

        private static int Clamp(int chance)
            => chance < MinChancePercent ? MinChancePercent : chance > MaxChancePercent ? MaxChancePercent : chance;
    }
}
