// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 3: what it COSTS. This is the half that makes the sandbox honest, and the reporter asked for
// it himself ("I understand this could be pretty OP"). Inc 1 decided whether a deed comes off; this decides what the
// world does about it afterwards, and it is the reason a player cannot simply walk the map cutting throats.
//
// Two rules shape every number here. First, the deed is never free even when nobody watched: an assaulted or robbed
// victim is ALIVE and names you, and a body is eventually found, so the realm always learns something. Being seen
// only makes it worse, which keeps isolation a real advantage without making murder-in-private consequence-free.
// Second, a FAILED attempt still costs: you tried, they know, and attempted murder is not a misunderstanding.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Violence
{
    /// <summary>What one act of violence costs the player: with the law, with the victim, with their kin, and with the player's own name.</summary>
    public sealed class ViolenceConsequence
    {
        public ViolenceConsequence(int crimeRating, int relationHit, int renownLoss, int kinGrudgeMagnitude)
        {
            CrimeRating = crimeRating;
            RelationHit = relationHit;
            RenownLoss = renownLoss;
            KinGrudgeMagnitude = kinGrudgeMagnitude;
        }

        /// <summary>Crime the victim's realm charges the player with (the engine's own MainHeroCrimeRating, which drives guards and bounties).</summary>
        public int CrimeRating { get; }

        /// <summary>How far the victim's own regard for the player falls. Meaningless when they are dead, and zero there.</summary>
        public int RelationHit { get; }

        /// <summary>Standing lost. Only ever charged when someone SAW it: a secret deed shames no one publicly.</summary>
        public int RenownLoss { get; }

        /// <summary>The grudge the victim's kin carry, or 0 when the deed is not the sort that makes enemies of a family.</summary>
        public int KinGrudgeMagnitude { get; }
    }

    /// <summary>Prices an act of violence: what the law, the victim, their kin, and the player's name make of it.</summary>
    public static class ViolenceConsequencePolicy
    {
        /// <summary>Crime charged for a robbery. Tuning.</summary>
        public const int RobCrime = 10;

        /// <summary>Crime charged for beating someone down. Tuning.</summary>
        public const int AssaultCrime = 25;

        /// <summary>Crime charged for a killing: the act the realm cannot overlook. Tuning.</summary>
        public const int KillCrime = 70;

        /// <summary>What a FAILED attempt still costs, as a share of the deed itself: you tried, and they know. Tuning.</summary>
        public const double FailedShare = 0.6;

        /// <summary>Being watched multiplies what the realm hears. Tuning.</summary>
        public const double OnlookersMultiplier = 1.5;

        /// <summary>Doing it in front of guards multiplies it further. Tuning.</summary>
        public const double GuardedMultiplier = 2.0;

        /// <summary>Regard lost by a robbed victim. Tuning.</summary>
        public const int RobRelationHit = -30;

        /// <summary>Regard lost by a beaten victim. Tuning.</summary>
        public const int AssaultRelationHit = -50;

        /// <summary>Regard lost by someone who survived your attempt on their LIFE: there is no coming back from that. Tuning.</summary>
        public const int AttemptedMurderRelationHit = -100;

        /// <summary>Standing lost when a robbery is seen. Tuning.</summary>
        public const int RobRenownLoss = 2;

        /// <summary>Standing lost when a beating is seen. Tuning.</summary>
        public const int AssaultRenownLoss = 5;

        /// <summary>Standing lost when a killing is seen. Tuning.</summary>
        public const int KillRenownLoss = 15;

        /// <summary>The grudge a family carries for a murdered kinsman. Tuning.</summary>
        public const int KillKinGrudge = 70;

        /// <summary>The grudge a family carries for a beaten kinsman. Tuning.</summary>
        public const int AssaultKinGrudge = 25;

        /// <summary>
        ///   What this act costs. A forbidden act costs nothing, because nothing happened. Otherwise the law always
        ///   hears something (an living victim accuses, a body is found), scaled by who was watching; renown is
        ///   charged ONLY when it was seen; the victim's regard collapses unless they are past caring; and kin carry
        ///   a grudge for blood and for beatings, never for a stolen purse.
        /// </summary>
        public static ViolenceConsequence Resolve(ViolentAct act, ViolenceVerdict verdict, ViolenceExposure exposure)
        {
            if (verdict == ViolenceVerdict.Forbidden) return new ViolenceConsequence(0, 0, 0, 0);

            bool succeeded = verdict == ViolenceVerdict.Succeeded;
            bool seen = exposure != ViolenceExposure.Alone;
            double share = succeeded ? 1.0 : FailedShare;

            var crime = (int) Math.Round(BaseCrime(act) * share * ExposureMultiplier(exposure));
            int renown = seen ? (int) Math.Round(BaseRenownLoss(act) * share) : 0;
            int relation = RelationHitFor(act, succeeded);
            var kin = (int) Math.Round(BaseKinGrudge(act) * share);

            return new ViolenceConsequence(crime, relation, renown, kin);
        }

        // A dead victim has no regard left to lose; a survivor of an attempt on their life has every reason to hate.
        private static int RelationHitFor(ViolentAct act, bool succeeded)
        {
            switch (act)
            {
                case ViolentAct.Kill: return succeeded ? 0 : AttemptedMurderRelationHit;
                case ViolentAct.Assault: return AssaultRelationHit;
                default: return RobRelationHit;
            }
        }

        private static int BaseCrime(ViolentAct act)
        {
            switch (act)
            {
                case ViolentAct.Kill: return KillCrime;
                case ViolentAct.Assault: return AssaultCrime;
                default: return RobCrime;
            }
        }

        private static int BaseRenownLoss(ViolentAct act)
        {
            switch (act)
            {
                case ViolentAct.Kill: return KillRenownLoss;
                case ViolentAct.Assault: return AssaultRenownLoss;
                default: return RobRenownLoss;
            }
        }

        private static int BaseKinGrudge(ViolentAct act)
        {
            switch (act)
            {
                case ViolentAct.Kill: return KillKinGrudge;
                case ViolentAct.Assault: return AssaultKinGrudge;
                default: return 0;
            }
        }

        private static double ExposureMultiplier(ViolenceExposure exposure)
        {
            switch (exposure)
            {
                case ViolenceExposure.Guarded: return GuardedMultiplier;
                case ViolenceExposure.Onlookers: return OnlookersMultiplier;
                default: return 1.0;
            }
        }
    }
}
