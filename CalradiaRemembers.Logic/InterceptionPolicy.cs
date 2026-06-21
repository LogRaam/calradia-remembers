// Code written by Gabriel Mailhot, 21/06/2026.
// NPC-initiated interceptions: a lord may ride out to the player rather than wait to be sought. Pure
// so the trigger priority and the guards (one pursuer at a time, per-lord cooldown, notoriety floor)
// — which decide who comes for the player and why, and break in silence — stay protected by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a lord rides out to the player — <see cref="None"/> means they do not.</summary>
    public enum InterceptionTrigger
    {
        /// <summary>No reason to intercept right now.</summary>
        None,

        /// <summary>A hostile-realm lord challenges a notorious traveller crossing their land.</summary>
        TerritorialPatrol,

        /// <summary>A lord who has heard of a recent deed of the player's rides out to react while it is fresh.</summary>
        HeardOfDeed
    }

    /// <summary>The facts a single candidate lord's interception decision rests on — engine-gathered, pure to reason over.</summary>
    public sealed class InterceptionFacts
    {
        /// <summary>This lord intercepted the player too recently to do so again.</summary>
        public bool OnCooldown { get; set; }

        /// <summary>Another lord is already in pursuit — only one interception runs at a time.</summary>
        public bool AnotherPursuerActive { get; set; }

        /// <summary>The player is currently within this lord's realm's territory.</summary>
        public bool PlayerInOwnTerritory { get; set; }

        /// <summary>This lord's realm is at war with (or otherwise hostile to) the player.</summary>
        public bool FactionHostileToPlayer { get; set; }

        /// <summary>The player's notoriety (clan renown proxy) — a nobody is not worth challenging.</summary>
        public int PlayerNotoriety { get; set; }

        /// <summary>This lord has plausibly heard of a recent deed of the player's (from the rumour store).</summary>
        public bool HeardRecentPlayerDeed { get; set; }
    }

    /// <summary>
    ///   Decides whether and why a lord rides out to intercept the player. Guards come first (a
    ///   per-lord cooldown and a single global pursuer keep it from becoming harassment); then the
    ///   triggers in priority order — a hostile territorial challenge outranks merely reacting to
    ///   news. The host rolls a probability and handles the pursuit; this is the eligibility law.
    /// </summary>
    public static class InterceptionPolicy
    {
        /// <summary>A traveller of at least this notoriety (renown) is worth a hostile lord's challenge.</summary>
        public const int NotorietyThreshold = 50;

        /// <summary>A chase that has run this many days without connecting is given up.</summary>
        public const int MaxPursuitDays = 4;

        /// <summary>The player must be at least this much faster than the pursuer to be deemed uncatchable.</summary>
        public const float OutrunSpeedRatio = 1.15f;

        /// <summary>Inside this distance the encounter is imminent — the pursuer keeps pressing whatever the speeds.</summary>
        public const float AbandonMinDistance = 5f;

        /// <summary>
        ///   Whether a lord gives up the chase. A pursuer right on top of the player keeps pressing no
        ///   matter what — the encounter is about to happen, so neither the timeout nor a speed gap
        ///   matters. Otherwise a stale pursuit (past <see cref="MaxPursuitDays"/>) is abandoned, and
        ///   so is a hopeless one — when the player is well clear and clearly faster, the pursuer
        ///   cannot force the meeting and stops wasting the effort.
        /// </summary>
        public static bool ShouldAbandonChase(float pursuerSpeed, float playerSpeed, float distance, int daysPursuing)
        {
            if (distance < AbandonMinDistance) return false; // about to connect — never give up now
            if (daysPursuing >= MaxPursuitDays) return true;
            return pursuerSpeed > 0f && playerSpeed >= pursuerSpeed * OutrunSpeedRatio;
        }

        /// <summary>The reason this lord would intercept the player now, or <see cref="InterceptionTrigger.None"/>.</summary>
        public static InterceptionTrigger Choose(InterceptionFacts f)
        {
            if (f == null) return InterceptionTrigger.None;
            if (f.OnCooldown || f.AnotherPursuerActive) return InterceptionTrigger.None;

            if (f.FactionHostileToPlayer && f.PlayerInOwnTerritory && f.PlayerNotoriety >= NotorietyThreshold)
                return InterceptionTrigger.TerritorialPatrol;

            if (f.HeardRecentPlayerDeed)
                return InterceptionTrigger.HeardOfDeed;

            return InterceptionTrigger.None;
        }

        /// <summary>Convenience: whether any trigger fires.</summary>
        public static bool WouldIntercept(InterceptionFacts f) => Choose(f) != InterceptionTrigger.None;
    }
}
