// Code written by Gabriel Mailhot, 17/07/2026.
// CHRONICLE: whether a conception the player is party to should be recorded as a PUBLIC world event (romance
// audit C5). The conception feeder records a PlayerFeat that feeds NPC knowledge, tavern rumours, and the
// player's spouse's own awareness. That is right for a legitimate heir, but recording an ADULTEROUS conception
// the same way broke the entire bastard pillar: its premise is that a hidden bastard stays secret until a
// seasonal discovery roll, and a public conception event leaked the secret the very day it was conceived, and
// falsely called a bastard "an heir". This pure rule draws the line so the regression is test-locked.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a conception involving the player is a PUBLIC heir worth chronicling, or a private
    ///   matter the chronicle must stay silent about (the seed of a hidden bastard).
    /// </summary>
    public static class ConceptionChroniclePolicy
    {
        /// <summary>
        ///   True only when the OTHER parent is the player's own spouse (so the child is legitimate and its
        ///   begetting is genuinely public) AND a lord of standing (a faceless commoner is not chronicle
        ///   material). Any other conception is adulterous: the seed of a hidden bastard, which the bastard
        ///   pillar reveals on its own seasonal-discovery timer, never up front.
        /// </summary>
        public static bool IsPublicHeir(bool partnerIsPlayerSpouse, bool partnerIsLord)
            => partnerIsPlayerSpouse && partnerIsLord;
    }
}
