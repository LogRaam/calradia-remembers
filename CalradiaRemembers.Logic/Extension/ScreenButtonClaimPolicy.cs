// Code written by Gabriel Mailhot, 20/11/2026.
// Bellum Civile bridge (Discord, 20/11/2026): "your beautiful Balance of Power screen already has Diplomacy and Rumors &
// Events buttons marked as 'Available Soon'. It would be perfect if those could open our own interfaces... If nothing is
// registered, the existing 'Available Soon' behavior could remain unchanged. That would let us integrate cleanly without
// Harmony-patching your UI." Gabriel: "oui vas-y". One mod holds a button at a time: the first to claim it keeps it, so
// two mods installed together never take turns over the same button depending on load order; the holder may replace its
// own screen or let the button go.

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>What a mod asks of a Balance of Power button.</summary>
    public sealed class ScreenButtonClaimFacts
    {
        /// <summary>The mod that holds the button now; null or blank when it still says "Available Soon".</summary>
        public string Holder { get; init; }

        /// <summary>The mod asking for it; blank is refused (nobody could later let the button go).</summary>
        public string Claimant { get; init; }

        /// <summary>The claimant gave something to open; a claim with nothing to open would leave a dead button.</summary>
        public bool HasScreen { get; init; }
    }

    /// <summary>The answer to a claim.</summary>
    public enum ScreenButtonClaim
    {
        /// <summary>The button was free and is now the claimant's.</summary>
        Taken,

        /// <summary>The claimant already held it and its screen is replaced.</summary>
        Replaced,

        /// <summary>Refused: another mod holds it, or the claim names no mod or no screen.</summary>
        Refused
    }

    /// <summary>Who may hold a Balance of Power button. Pure.</summary>
    public static class ScreenButtonClaimPolicy
    {
        /// <summary>The first mod to claim a button keeps it; it alone may replace its screen.</summary>
        public static ScreenButtonClaim Decide(ScreenButtonClaimFacts facts)
        {
            string claimant = facts?.Claimant?.Trim();
            if (string.IsNullOrEmpty(claimant) || facts.HasScreen == false) return ScreenButtonClaim.Refused;

            string holder = facts.Holder?.Trim();
            if (string.IsNullOrEmpty(holder)) return ScreenButtonClaim.Taken;

            return string.Equals(holder, claimant, System.StringComparison.OrdinalIgnoreCase) ? ScreenButtonClaim.Replaced : ScreenButtonClaim.Refused;
        }

        /// <summary>Only the holder lets a button go back to "Available Soon".</summary>
        public static bool MayRelease(string holder, string asker)
            => !string.IsNullOrWhiteSpace(holder) && string.Equals(holder.Trim(), asker?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
