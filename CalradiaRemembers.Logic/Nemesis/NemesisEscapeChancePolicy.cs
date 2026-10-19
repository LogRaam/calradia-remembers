// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (ratified 2026-07-05): a nemesis captor is NOT a perfect prison, but he LEARNS. The
// player always keeps some chance to slip away (Gabriel: "je laisserais une chance quand meme"), but every
// time the nemesis recaptures the player he grows more careful, so that chance decays toward a slim floor.
// This turns a recurring nemesis captivity into an escalating arc ("you slipped me once, not again"), and
// it replaces the flat scene-escape chance ONLY when the captor is the nemesis (ordinary captors keep the
// flat chance, and their vanilla drip). Pure: a recapture count in, a probability out; the host sources the
// count from the persisted NemesisIdentity and feeds the result to the scene-escape roll.

#region

#endregion

namespace CalradiaRemembers.Logic.Nemesis
{
    /// <summary>
    ///   The chance a player escapes a NEMESIS captor in a captive-scene attempt, decaying with each prior
    ///   recapture as the nemesis tightens his guard, but never reaching zero.
    /// </summary>
    public static class NemesisEscapeChancePolicy
    {
        /// <summary>TUNE: escape chance on the FIRST nemesis captivity (no prior recapture). Matches the ordinary scene chance.</summary>
        public const double BaseEscapeChance = 0.10;

        /// <summary>TUNE: how much the chance drops per prior recapture as the nemesis grows more careful.</summary>
        public const double DropPerRecapture = 0.025;

        /// <summary>TUNE: the floor a decaying chance never falls below, so a nemesis is never a perfect, hopeless cage.</summary>
        public const double MinEscapeChance = 0.02;

        /// <summary>TUNE: fraction of the vanilla spontaneous-escape drip a nemesis suppresses on the FIRST captivity (no prior recapture). Below 1.0 on purpose: a fresh nemesis is not a perfect passive cage, about one offered escape in five still stands.</summary>
        public const double VanillaDripSuppressBase = 0.80;

        /// <summary>TUNE: how much more of the drip the nemesis suppresses per prior recapture, as he tightens his guard.</summary>
        public const double VanillaDripSuppressPerRecapture = 0.05;

        /// <summary>TUNE: the ceiling on drip suppression, so even a many-times nemesis leaves a slim passive chance (never a hopeless cage).</summary>
        public const double VanillaDripSuppressMax = 0.95;

        /// <summary>
        ///   The scene-escape chance against a nemesis who has already recaptured the player
        ///   <paramref name="recaptureCount"/> times (0 on the first captivity). Decays linearly by
        ///   <see cref="DropPerRecapture"/> per recapture, clamped to <see cref="MinEscapeChance"/>. A
        ///   negative count is treated as zero.
        /// </summary>
        public static double EscapeChance(int recaptureCount)
        {
            if (recaptureCount < 0) recaptureCount = 0;

            double chance = BaseEscapeChance - recaptureCount * DropPerRecapture;

            return chance < MinEscapeChance ? MinEscapeChance : chance;
        }

        /// <summary>
        ///   The fraction of vanilla spontaneous-escape rolls a NEMESIS captor suppresses, rising with each
        ///   prior recapture as he tightens his guard, clamped to <see cref="VanillaDripSuppressMax"/> so a
        ///   passive escape is always at least slimly possible (the player is never left to wait out days with
        ///   no exit). A negative count is treated as zero. The passive-drip mirror of <see cref="EscapeChance"/>.
        /// </summary>
        public static double VanillaDripSuppressChance(int recaptureCount)
        {
            if (recaptureCount < 0) recaptureCount = 0;

            double suppress = VanillaDripSuppressBase + recaptureCount * VanillaDripSuppressPerRecapture;

            return suppress > VanillaDripSuppressMax ? VanillaDripSuppressMax : suppress;
        }
    }
}
