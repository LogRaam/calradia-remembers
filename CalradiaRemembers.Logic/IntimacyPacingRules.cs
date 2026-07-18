// Code written by Gabriel Mailhot, 18/07/2026.
// Adult-prompt audit M3. The intimacy consent bar used to be a fixed wall written into the prompt prose,
// while every OTHER gate in the mod already bends to the player's Relationship Pacing dial (the change_relation
// wording, the deeds-over-words stance, the RelationGate cooldown and cap). A player who set the dial to
// Storyteller because they want a warmer campaign still met an unmoved bar in the one place where warmth is
// the entire point. This maps the dial onto the bar.
//
// The mapping lives mod-side on purpose: RelationWarmth is a mod-side player setting the SDK cannot see, so
// the SDK owns the bar and its floor (IntimacyThresholdPolicy) and the host owns how far the dial lowers it.

namespace CalradiaRemembers.Logic
{
    /// <summary>Turns the player's Relationship Pacing dial into the relief applied to the intimacy consent bar.</summary>
    public static class IntimacyPacingRules
    {
        /// <summary>
        ///   Grounded keeps the bar exactly where it shipped, so a player who never touches the dial sees no
        ///   change at all. This zero is load-bearing: it is what makes the whole feature invisible by default.
        /// </summary>
        public const int NormalRelief = 0;

        /// <summary>Warm: a quarter off the ordinary bar of 20, which becomes 15. Tuning.</summary>
        public const int EasierRelief = 5;

        /// <summary>Storyteller: half off the ordinary bar, which becomes 10. Tuning.</summary>
        public const int VeryEasyRelief = 10;

        /// <summary>
        ///   How far <paramref name="warmth" /> lowers the regard an NPC asks for before physical intimacy.
        ///   Never negative: the dial only ever loosens, exactly as it does for positive relation gains, so
        ///   turning it up can never make a campaign harsher than the one the mod was balanced around.
        /// </summary>
        public static int Relief(RelationWarmth warmth)
        {
            switch (warmth)
            {
                case RelationWarmth.Easier:   return EasierRelief;
                case RelationWarmth.VeryEasy: return VeryEasyRelief;
                default:                      return NormalRelief;
            }
        }
    }
}
