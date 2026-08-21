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
        HeardOfDeed,

        /// <summary>
        ///   A lord who holds a live, unresolved, significant grudge against the player comes to air it: a
        ///   wrong pressed while it still stings. Appended at the end of the enum (frozen ordinals).
        /// </summary>
        Grudge,

        /// <summary>
        ///   A lord who holds a significant, recent appreciation of the player comes to acknowledge or thank
        ///   them for it. The gentlest of the four: a warm word, never a request or a bargain. Appended at
        ///   the end of the enum (frozen ordinals).
        /// </summary>
        AppreciationOwed
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

        /// <summary>
        ///   This lord holds a live (unresolved), significant-magnitude grudge against the PLAYER specifically
        ///   (a grudge against someone else never sets this). Callers gate on
        ///   <see cref="InterceptionPolicy.MinGrudgeMagnitudeForInterception" /> before setting this, so a
        ///   trivial or already-mended grievance never reaches the pure decision.
        /// </summary>
        public bool HoldsLiveGrudgeAgainstPlayer { get; set; }

        /// <summary>
        ///   This lord holds a significant, recent appreciation felt toward the PLAYER specifically (an
        ///   appreciation toward someone else never sets this). Callers gate on
        ///   <see cref="InterceptionPolicy.MinAppreciationMagnitudeForInterception" /> before setting this, so
        ///   a merely-formed kindness never reaches the pure decision.
        /// </summary>
        public bool HoldsSignificantAppreciation { get; set; }
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
        ///   The floor a grudge's live effective magnitude (GrudgePolicy.EffectiveMagnitude) must clear before
        ///   it is worth a whole face-to-face confrontation, mirroring CompanionGratitudePolicy.MinEffectiveMagnitude
        ///   (40, "well above the bare formation floor... only a genuinely meaningful [grievance] pulls the
        ///   player aside, not every slight that barely registered"). Fact-gathering (InterceptionBehavior)
        ///   applies this before ever setting InterceptionFacts.HoldsLiveGrudgeAgainstPlayer.
        /// </summary>
        public const int MinGrudgeMagnitudeForInterception = 40;

        /// <summary>The same "worth a whole audience" floor, mirrored for a felt kindness. See <see cref="MinGrudgeMagnitudeForInterception" />.</summary>
        public const int MinAppreciationMagnitudeForInterception = 40;

        /// <summary>
        ///   Whether a forced meeting can physically happen right now. A land party cannot ride out to a
        ///   ship, and, far more dangerously, the engine has no conversation scene to place over open
        ///   water: staging one there leaves the game on a loading screen that never resolves (reported
        ///   twice against v1.30.16, both while sailing). So neither side may be at sea, whichever of the
        ///   two took to the water. Callers use this to refuse to start such a pursuit, to hold the
        ///   meeting closed until both are ashore, and to stand a pursuit down when the player sails.
        /// </summary>
        public static bool CanMeetOnLand(bool playerAtSea, bool pursuerAtSea) => !playerAtSea && !pursuerAtSea;

        /// <summary>
        ///   Whether a lord gives up the chase. A pursuer right on top of the player keeps pressing no
        ///   matter what — the encounter is about to happen, so neither the timeout nor a speed gap
        ///   matters. Otherwise a stale pursuit (past <see cref="MaxPursuitDays"/>) is abandoned, and
        ///   so is a hopeless one — when the player is well clear and clearly faster, the pursuer
        ///   cannot force the meeting and stops wasting the effort.
        ///   <paramref name="isClosing"/> overrides both of those give-ups: a player reported riding
        ///   straight at a lord who had been announced ("I'll ride up to them and everything") only to
        ///   have the meeting cancelled out from under them. A player who is actively closing the gap is
        ///   doing exactly what the feature promised, so neither slowness (outrun) nor patience
        ///   (timeout) may give up on them while that holds, save for a genuinely impossible situation
        ///   (sea, army, death), which this method knows nothing about and the caller checks separately,
        ///   and which still stands the chase down regardless.
        /// </summary>
        public static bool ShouldAbandonChase(float pursuerSpeed, float playerSpeed, float distance, int daysPursuing, bool isClosing = false)
        {
            if (distance < AbandonMinDistance) return false; // about to connect — never give up now
            if (isClosing) return false; // the player is riding straight at the meeting, so let them arrive
            if (daysPursuing >= MaxPursuitDays) return true;
            return pursuerSpeed > 0f && playerSpeed >= pursuerSpeed * OutrunSpeedRatio;
        }

        /// <summary>
        ///   The reason this lord would intercept the player now, or <see cref="InterceptionTrigger.None"/>.
        ///   Priority, after the guards: <see cref="InterceptionTrigger.TerritorialPatrol"/> (a hostile
        ///   realm defending its own land outranks everything else) &gt;
        ///   <see cref="InterceptionTrigger.Grudge"/> (a wrong pressed) &gt;
        ///   <see cref="InterceptionTrigger.HeardOfDeed"/> (fresh news) &gt;
        ///   <see cref="InterceptionTrigger.AppreciationOwed"/> (a warm acknowledgement, the least urgent of
        ///   the four: nothing is lost by a kindness waiting one more day to be voiced).
        /// </summary>
        public static InterceptionTrigger Choose(InterceptionFacts f)
        {
            if (f == null) return InterceptionTrigger.None;
            if (f.OnCooldown || f.AnotherPursuerActive) return InterceptionTrigger.None;

            if (f.FactionHostileToPlayer && f.PlayerInOwnTerritory && f.PlayerNotoriety >= NotorietyThreshold)
                return InterceptionTrigger.TerritorialPatrol;

            if (f.HoldsLiveGrudgeAgainstPlayer)
                return InterceptionTrigger.Grudge;

            if (f.HeardRecentPlayerDeed)
                return InterceptionTrigger.HeardOfDeed;

            if (f.HoldsSignificantAppreciation)
                return InterceptionTrigger.AppreciationOwed;

            return InterceptionTrigger.None;
        }

        /// <summary>Convenience: whether any trigger fires.</summary>
        public static bool WouldIntercept(InterceptionFacts f) => Choose(f) != InterceptionTrigger.None;
    }
}
