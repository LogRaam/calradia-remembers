// Code written by Gabriel Mailhot, 05/07/2026.
// Captivity fix (ratified 2026-07-05): when the player NEGOTIATES their release or is granted freedom in
// chat, the release must be made REAL in vanilla ("the prompt is advice, the bridge is law"). This pure
// policy decides WHETHER a captor can be brought to free the player at all, so the bridge only ever teaches
// / executes a release the captor would actually grant. Two paths: PERSUASION (a disposed captor talked
// round) and RANSOM (gold buys freedom). A captor holding a VENDETTA (a heavy grudge, or a nemesis who
// wants blood) refuses BOTH: they did not take you to let you buy your way out. Trait/grudge in, verdict
// out; the host sources Mercy from the captor Hero and the grudge magnitude from the grudge ledger.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What release paths (if any) a captor is open to for THIS captive player.</summary>
    public sealed class CaptorReleaseVerdict
    {
        public CaptorReleaseVerdict(bool allowsPersuasion, bool allowsRansom)
        {
            AllowsPersuasion = allowsPersuasion;
            AllowsRansom = allowsRansom;
        }

        /// <summary>True when the captor can be TALKED into freeing the player (a disposed, non-vendetta captor).</summary>
        public bool AllowsPersuasion { get; }

        /// <summary>True when the captor will accept a RANSOM in gold (any non-vendetta captor).</summary>
        public bool AllowsRansom { get; }

        /// <summary>Convenience: the captor is immovable, no release of any kind.</summary>
        public bool RefusesAll => !AllowsPersuasion && !AllowsRansom;
    }

    /// <summary>
    ///   Pure gate for chat-negotiated player release. Consulted by the bridge BEFORE it teaches a release
    ///   action to the captor and again before it commits one: eloquence and gold PROPOSE, this policy and
    ///   the bridge dispose.
    /// </summary>
    public static class CaptorReleasePolicy
    {
        /// <summary>TUNE: a grudge at/above this is a VENDETTA, the captor wants blood over gold and refuses every release.</summary>
        public const int VendettaGrudgeFloor = 40;

        /// <summary>TUNE: a grudge strictly below this counts as "no heavy grudge", so even an unmerciful captor can be talked round.</summary>
        public const int LightGrudgeCeiling = 15;

        /// <summary>TUNE: Mercy at/above this lets a captor be persuaded even when they carry a moderate (sub-vendetta) grudge.</summary>
        public const int MercyPersuasionFloor = 1;

        /// <summary>
        ///   Resolves how a captor may be brought to free the player. A VENDETTA
        ///   (<paramref name="isNemesisVendetta"/>, or a grudge at/above <see cref="VendettaGrudgeFloor"/>)
        ///   refuses both paths. Otherwise ransom is always open (any captor takes gold), and persuasion is
        ///   open to a merciful captor (Mercy at/above <see cref="MercyPersuasionFloor"/>) OR to any captor
        ///   who holds no heavy grudge (below <see cref="LightGrudgeCeiling"/>). Trait levels are the
        ///   standard Bannerlord scale (roughly -2..+2).
        /// </summary>
        public static CaptorReleaseVerdict Resolve(int captorMercy, int grudgeAgainstPlayer, bool isNemesisVendetta)
        {
            if (isNemesisVendetta || grudgeAgainstPlayer >= VendettaGrudgeFloor)
                return new CaptorReleaseVerdict(false, false);

            bool persuadable = captorMercy >= MercyPersuasionFloor || grudgeAgainstPlayer < LightGrudgeCeiling;

            return new CaptorReleaseVerdict(persuadable, true);
        }
    }
}
