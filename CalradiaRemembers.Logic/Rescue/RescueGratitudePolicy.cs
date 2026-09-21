// Code written by Gabriel Mailhot, 11/07/2026.

#region

#endregion

namespace CalradiaRemembers.Logic.Rescue
{
    /// <summary>
    ///   Decides how strongly an NPC reacts to being rescued (or to a loved one being rescued) by the
    ///   player. The rescued lord and their close family record a lasting memory and feel strong gratitude;
    ///   a mere friend only warms slightly toward the player and keeps no memory of it.
    /// </summary>
    public static class RescueGratitudePolicy
    {
        /// <summary>Regard points granted to the prisoner who was themselves rescued.</summary>
        public const int SelfRegard = 12;

        /// <summary>Regard points granted to a close family member of the rescued prisoner.</summary>
        public const int FamilyRegard = 6;

        /// <summary>Regard points granted to a friend of the rescued prisoner.</summary>
        public const int FriendRegard = 3;

        /// <summary>Relation at/above this counts as a friend of the rescued prisoner.</summary>
        public const int FriendRelationThreshold = 20;

        /// <summary>True when <paramref name="relationToRescued"/> is high enough to count as a friendship.</summary>
        public static bool QualifiesAsFriend(int relationToRescued) => relationToRescued >= FriendRelationThreshold;

        /// <summary>
        ///   Whether someone tied to the rescued prisoner can feel the debt at all. A child cannot: gratitude here is a
        ///   MEMORY plus regard ("You freed my father from the deserters. My house owes you a debt."), and an infant
        ///   neither knows what happened nor has a house to pledge. Every grudge path already gated on adulthood
        ///   (ExecutedKinGrudgeBehavior, DivorceProcessBehavior); the rescue path did not, and a critical read of the
        ///   generated maps found it (21/09/2026): a baby recorded the memory and warmed to the player. The child grows
        ///   up to meet a stranger, which is the truth.
        /// </summary>
        public static bool CanFeelTheDebt(bool isChild) => !isChild;

        /// <summary>Resolves the gratitude an NPC feels for a rescue, based on their tie to the rescued prisoner.</summary>
        public static RescueGratitude GratitudeFor(RescueTie tie)
        {
            switch (tie)
            {
                case RescueTie.RescuedSelf:
                    return new RescueGratitude(SelfRegard, true);
                case RescueTie.Family:
                    return new RescueGratitude(FamilyRegard, true);
                case RescueTie.Friend:
                    return new RescueGratitude(FriendRegard, false);
                default:
                    return new RescueGratitude(0, false);
            }
        }
    }
}
