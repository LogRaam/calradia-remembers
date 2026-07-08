// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: the tiered source catalog from the ROADMAP ("faire le tour des
// systèmes pour alimenter le grudge"). Tier 1 (Jealousy..BastardBlackmail) doubles as the migration
// backfill list: their markers already sit in existing saves. Tier 2 (DeliveredPrisoner..
// UnblessedMarriage) are new detections. Tier 3 (ExecutedKin) is the vendetta end of the spectrum.
// External is reserved for a future Extension Surface inbound grudge (mirrors ExternalWorldEvent).
// 04/07/2026: CoercedRelease appended (Stance mechanical behaviour-gating, increment 1, Fear) — a
// fear-coerced freedom bargain plants this instead of the deed-gesture's usual regard.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   Where a <see cref="Grudge" /> came from. ORDINALS ARE FROZEN: this enum is persisted inside
    ///   saved <see cref="Grudge" /> records (a future byte[] SyncData key), so existing values must
    ///   never be reordered or removed. New sources are appended at the end only, exactly like
    ///   <c>NotableEventType</c> in the SDK.
    /// </summary>
    public enum GrudgeSource
    {
        /// <summary>A jealous party's grudge over a romantic act (spouse-of-X / player-partner / rival / spurned admirer).</summary>
        Jealousy = 0,

        /// <summary>The plotter grudges the player for tipping off their scheme's target (a one-shot warning today).</summary>
        SchemeWarned = 1,

        /// <summary>The wronged target grudges the player once an abetted scheme against them is discovered.</summary>
        SchemeAbetted = 2,

        /// <summary>The quest giver grudges the player over a failed or abandoned quest ("you gave your word").</summary>
        FailedQuest = 3,

        /// <summary>The repudiated or set-aside spouse's grudge from a divorce.</summary>
        Divorce = 4,

        /// <summary>A bastard child's coming-of-age claim pressed against the player's house.</summary>
        BastardClaim = 5,

        /// <summary>An unpaid or resented bastard blackmail demand.</summary>
        BastardBlackmail = 6,

        /// <summary>"You sold me to my enemy" — a prisoner the player delivered to another faction.</summary>
        DeliveredPrisoner = 7,

        /// <summary>An NPC or held companion tormented or assaulted in a captive scene.</summary>
        CaptiveAbuse = 8,

        /// <summary>A sway target who hears the rumour that the player turned opinion against them.</summary>
        SwayExposed = 9,

        /// <summary>A family that never blessed a marriage, grudging the player while the union lasts.</summary>
        UnblessedMarriage = 10,

        /// <summary>The kin of an executed lord: a permanent vendetta, always maximum rigidity.</summary>
        ExecutedKin = 11,

        /// <summary>A grudge planted by an external mod through the Extension Surface inbound API.</summary>
        External = 12,

        /// <summary>Stance mechanical behaviour-gating (increment 1, Fear): a captive fear-coerced into a freedom bargain, resenting the coercion in secret rather than feeling freely given goodwill.</summary>
        CoercedRelease = 13,

        /// <summary>
        ///   A held companion's REVENGE grudge against the NAMED captor who tormented or assaulted them before
        ///   the player (companion-victim scene, Phase 3). Distinct from <see cref="CaptiveAbuse" /> (which the
        ///   same companion may hold against the PLAYER for coercing them or failing to protect them), so both
        ///   coexist rather than colliding on the holder+source+day dedup. Only planted against a persistent
        ///   Hero captor (a nemesis or lord); a faceless bandit is ephemeral and earns no lasting vendetta.
        /// </summary>
        CaptorCruelty = 14
    }
}
