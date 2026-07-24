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
        CaptorCruelty = 14,

        /// <summary>
        ///   "You stormed my walls and stole my prize": a captor whose valuable prisoner the player freed by force,
        ///   and who has traced the deed back to the player. Only planted when the captor DISCOVERS the player was
        ///   responsible (see <c>CaptorGrudgePolicy</c>); a public siege is both easier to trace and openly resented.
        /// </summary>
        PrisonerRescued = 15,

        /// <summary>
        ///   "My family gave me away": a spouse wed to the player through the family's blessing while their own
        ///   regard was still below the marriage bar (ratified 2026-07-23: a political match MARRIES rather than
        ///   refusing, and the bride's unwon heart becomes this grudge instead of an invisible gate). Magnitude
        ///   scales with how far below the bar their regard stood (<c>ForcedMarriagePolicy</c>). Deliberately a
        ///   grudge and not a flat relation malus: it surfaces in the prompt (the spouse knows WHY they are
        ///   cold), in the encyclopedia (the player can see it), it tarnishes kindnesses, and it DECAYS and can
        ///   be resolved, so a marriage begun in duty can be won over: a playable redemption arc, not a mute
        ///   permanent penalty. The mirror of <see cref="UnblessedMarriage" /> (love without the family), as
        ///   this is the family without love.
        /// </summary>
        ForcedMarriage = 16
    }
}
