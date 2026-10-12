// Code written by Gabriel Mailhot, 21/06/2026.
// NPC-initiated interceptions: a lord may ride out to the player rather than wait to be sought. Pure
// so the trigger priority and the guards (one pursuer at a time, per-lord cooldown, notoriety floor)
// — which decide who comes for the player and why, and break in silence — stay protected by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Why an abandoned pursuit ended. Pure (moved out of the host behavior so the letter/rememberance
    ///   gates can be pinned by tests): the host maps its engine facts onto this and the policy decides.
    /// </summary>
    public enum AbandonReason
    {
        /// <summary>The chase went to sea, where no meeting can physically be staged.</summary>
        WentToSea,

        /// <summary>The pursuer vanished from the chase (died, lost their party) or the player is now inside an army.</summary>
        OtherBusiness,

        /// <summary>The pursuit ran past <see cref="InterceptionPolicy.MaxPursuitDays" /> without connecting.</summary>
        GaveUp,

        /// <summary>The player was clearly faster and pulled well clear (the outrun branch of ShouldAbandonChase).</summary>
        LostTrail,

        /// <summary>
        ///   The fief the player sits in came under siege: nobody waits at the gate of a besieged town (Fakade,
        ///   03/10/2026: the waiting lord dragged him into a field battle in the middle of his siege defence).
        /// </summary>
        Siege
    }

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
        AppreciationOwed,

        /// <summary>
        ///   A lord whose region is plagued by a nemesis he is eligible to put a bounty on (the SAME gate
        ///   <c>NemesisBountyDialogFlow.FindEligibleNemesisFor</c> checks before the scripted
        ///   <c>hero_main_options</c> offer) seeks the player out to name the nemesis and where he operates,
        ///   pointing them to the bounty. Announcement only: accepting the bounty itself stays entirely at
        ///   that existing scripted line, never here. Appended at the end of the enum (frozen ordinals).
        /// </summary>
        NemesisBounty
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

        /// <summary>
        ///   This lord has a nemesis eligible to be bountied in his own region (the SAME gate
        ///   <c>NemesisBountyDialogFlow.FindEligibleNemesisFor</c> checks) and has not yet raised it with the
        ///   player. Reusing that gate rather than re-deriving eligibility keeps this trigger and the scripted
        ///   bounty offer it announces from ever disagreeing about who is eligible.
        /// </summary>
        public bool HasNemesisBountyToOffer { get; set; }
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

        /// <summary>
        ///   How many days a lord whose chase came to nothing still remembers he meant to speak with the
        ///   player: if the player seeks HIM out within this window, he raises his matter in person (player
        ///   report, 2026-09-03: "if I go and meet them they don't remember they wanted to tell me
        ///   something"). Short, so a stale errand does not ambush a conversation a week later.
        /// </summary>
        public const int RememberedIntentWindowDays = 2;

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
        ///   The state of one pursuit. THREE FLOATS in a row, two speeds and a distance: swap the speeds and a
        ///   pursuer who is faster reads as outrun and gives up on a meeting he was about to make. That is the
        ///   class of bug the isClosing override exists to answer in the first place, after a player reported
        ///   riding straight at an announced lord and having the meeting cancelled from under him.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ChaseFacts
        {
           /// <summary>How fast the lord travels.</summary>
           public float PursuerSpeed { get; set; }

           /// <summary>How fast the player travels. Clearly faster, and clear away, is hopeless.</summary>
           public float PlayerSpeed { get; set; }

           /// <summary>How far apart they are. Right on top of the player, nothing gives up.</summary>
           public float Distance { get; set; }

           /// <summary>How long the chase has run. Past the cap it is stale.</summary>
           public int DaysPursuing { get; set; }

           /// <summary>
           ///   The player is actively riding at the meeting. Overrides both give-ups: doing exactly what the
           ///   feature promised must never be punished by a timeout or a speed gap.
           /// </summary>
           public bool IsClosing { get; set; } = false;

           /// <summary>
           ///   The player is inside a town, castle or village. The pursuer waits at the gate, always "within
           ///   reach", but no meeting can happen until the player comes out, so nearness proves nothing.
           /// </summary>
           public bool PlayerInsideSettlement { get; set; }

           /// <summary>The settlement the player is in is besieged, or being besieged.</summary>
           public bool PlayerSettlementBesieged { get; set; }

           /// <summary>
           ///   The player's own party is in a siege (besieging or besieged) or a battle on the map. A pursuer reaching
           ///   him then makes the game's own encounter, a fight (Maifailun, 10/10/2026).
           /// </summary>
           public bool PlayerInSiegeOrBattle { get; set; }
        }

        /// <summary>
        ///   Whether a lord may ride out to the player now: never while the player besieges, is besieged, or fights
        ///   (Maifailun, 10/10/2026: a lord reaching a besieging army forced a fight).
        /// </summary>
        public static bool PlayerCanBeApproached(bool playerInSiegeOrBattle) => !playerInSiegeOrBattle;

        /// <summary>
        ///   Whether a lord gives up the chase. A pursuer right on top of the player keeps pressing no
        ///   matter what — the encounter is about to happen, so neither the timeout nor a speed gap
        ///   matters. Otherwise a stale pursuit (past <see cref="MaxPursuitDays"/>) is abandoned, and
        ///   so is a hopeless one — when the player is well clear and clearly faster, the pursuer
        ///   cannot force the meeting and stops wasting the effort.
        ///   <c>IsClosing</c> overrides both of those give-ups: a player reported riding
        ///   straight at a lord who had been announced ("I'll ride up to them and everything") only to
        ///   have the meeting cancelled out from under them. A player who is actively closing the gap is
        ///   doing exactly what the feature promised, so neither slowness (outrun) nor patience
        ///   (timeout) may give up on them while that holds, save for a genuinely impossible situation
        ///   (sea, army, death), which this method knows nothing about and the caller checks separately,
        ///   and which still stands the chase down regardless.
        /// </summary>
        public static bool ShouldAbandonChase(ChaseFacts facts)
        {
           if (facts == null) return false;

           float pursuerSpeed = facts.PursuerSpeed;
           float playerSpeed = facts.PlayerSpeed;
           float distance = facts.Distance;
           int daysPursuing = facts.DaysPursuing;
           bool isClosing = facts.IsClosing;

            // Fakade (03/10/2026): a lord camped at the gate of a fief about to be besieged, and the siege's battle
            // took the player with it. A besieged fief ends the wait at once; inside walls, nearness proves nothing,
            // so the day limit below applies to a lord at the gate as to one on the road.
            if (facts.PlayerInsideSettlement && facts.PlayerSettlementBesieged) return true;
            // Maifailun (10/10/2026): reaching a player who besieges or fights is a fight, not a meeting. At once,
            // however close: closeness is exactly the danger here.
            if (facts.PlayerInSiegeOrBattle) return true;
            if (!facts.PlayerInsideSettlement)
            {
                if (distance < AbandonMinDistance) return false; // about to connect: never give up now
                if (isClosing) return false; // the player is riding straight at the meeting, so let them arrive
            }
            if (daysPursuing >= MaxPursuitDays) return true;
            return pursuerSpeed > 0f && playerSpeed >= pursuerSpeed * OutrunSpeedRatio;
        }

        /// <summary>
        ///   Whether a lord who abandoned a chase on <paramref name="abandonedDay" /> still remembers his
        ///   errand today — inside the <see cref="RememberedIntentWindowDays" /> grace window. An unset
        ///   slot (-1) is never active.
        /// </summary>
        public static bool IsRememberedIntentActive(int abandonedDay, int today)
            => abandonedDay >= 0 && today - abandonedDay <= RememberedIntentWindowDays;

        /// <summary>
        ///   Whether an abandoned chase ends in a missed-meeting letter. An ordinary give-up (outrun or
        ///   timeout) always does; being drawn into other business (a battle, the player vanishing into an
        ///   army) does too, but only while the pursuer is alive to write — a dead man sends no letter.
        ///   A genuinely impossible chase (out to sea) never does: nothing was ever going to connect, and
        ///   the grace window (<see cref="IsRememberedIntentActive" />) already covers the player who comes
        ///   back ashore and seeks the lord out.
        /// </summary>
        public static bool ShouldWriteMissedMeetingLetter(AbandonReason reason, bool pursuerAlive)
            => reason is AbandonReason.GaveUp or AbandonReason.LostTrail
               || (reason is AbandonReason.OtherBusiness or AbandonReason.Siege && pursuerAlive);

        /// <summary>
        ///   The reason this lord would intercept the player now, or <see cref="InterceptionTrigger.None"/>.
        ///   Priority, after the guards: <see cref="InterceptionTrigger.TerritorialPatrol"/> (a hostile
        ///   realm defending its own land outranks everything else) &gt;
        ///   <see cref="InterceptionTrigger.Grudge"/> (a wrong pressed) &gt;
        ///   <see cref="InterceptionTrigger.NemesisBounty"/> (a lord asking for the player's help against a
        ///   real, named threat still outranks generic news, but a personal grudge against the PLAYER comes
        ///   first) &gt; <see cref="InterceptionTrigger.HeardOfDeed"/> (fresh news) &gt;
        ///   <see cref="InterceptionTrigger.AppreciationOwed"/> (a warm acknowledgement, the least urgent of
        ///   the five: nothing is lost by a kindness waiting one more day to be voiced).
        /// </summary>
        public static InterceptionTrigger Choose(InterceptionFacts f)
        {
            if (f == null) return InterceptionTrigger.None;
            if (f.OnCooldown || f.AnotherPursuerActive) return InterceptionTrigger.None;

            if (f.FactionHostileToPlayer && f.PlayerInOwnTerritory && f.PlayerNotoriety >= NotorietyThreshold)
                return InterceptionTrigger.TerritorialPatrol;

            if (f.HoldsLiveGrudgeAgainstPlayer)
                return InterceptionTrigger.Grudge;

            if (f.HasNemesisBountyToOffer)
                return InterceptionTrigger.NemesisBounty;

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
