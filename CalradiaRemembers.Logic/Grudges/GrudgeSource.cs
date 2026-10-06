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
        ForcedMarriage = 16,

        /// <summary>
        ///   "You stripped me of my fief": the council's own revoke_fief (COUNCIL_ACTIONS.md, Gabriel: "une des
        ///   pires punitions qui puisse être faite"), a sovereign taking a crown fief back from a seated
        ///   WarCouncil vassal. Always planted at maximum magnitude (FiefTransferPolicy.RevokeGrudgeMagnitude);
        ///   rigidity still follows the usual regard-based formula, so a vassal who otherwise trusted the player
        ///   deeply can, in principle, still mend this, but a lord already distrustful earns a near-vendetta.
        /// </summary>
        FiefRevoked = 17,

        /// <summary>
        ///   "You cast me out of your own house": the council's own expel_from_clan (COUNCIL_ACTIONS.md, Kimi
        ///   review, Partie 8), the player-chief severing a seated COMPANION from the clan entirely. Planted at
        ///   a high magnitude (ExpulsionPolicy.GrudgeMagnitude, the review's own 50-70 range), deliberately below
        ///   <see cref="FiefRevoked" />'s maximum: Gabriel's own words reserve that ceiling for a vassal's fief
        ///   specifically, and a hired companion's expulsion, while still one of the gravest social costs the
        ///   table can hand down, is not quite that.
        /// </summary>
        Expelled = 18,

        /// <summary>
        ///   "You swore before us and broke it": R7-light's own public-oath sanction (COUNCIL_ACTIONS.md's
        ///   swear_oath section, "un serment prete devant temoins doit couter plus cher"). Planted only when the
        ///   broken <see cref="CalradiaRemembers.Logic.TrackedCommitment" /> carried at least one recorded
        ///   witness (a private oath's breach stays a plain relation hit, no grudge); magnitude fixed at
        ///   <see cref="CalradiaRemembers.Logic.CommitmentPolicy.BrokenOathGrudgeMagnitude" />.
        /// </summary>
        BrokenOath = 19,

        /// <summary>
        ///   "We swore not to raise arms, and you broke it": the council's own non_aggression_pact (COUNCIL_ACTIONS.md's
        ///   Partie 8), planted ONLY when the PLAYER's own faction is the party that attacked during the pact's
        ///   term (<c>NonAggressionPactPolicy.IsPlayerFactionAtFault</c>) - the one direction a grudge can honestly
        ///   be planted, since an NPC resenting the player for THEIR OWN faction's attack would make no causal
        ///   sense (see <c>NonAggressionPactPolicy</c>'s own header). The reverse breach still ends the pact and
        ///   applies a relation hit and a WorldEvent note naming the true attacker, just never this grudge.
        /// </summary>
        PactBroken = 20,

        /// <summary>
        ///   "I read what you truly wrote": intercepted-letter-leak Inc 2. An antagonist came into possession of
        ///   the player's private letter, waylaid on the road by bandits, and turned its contents against them.
        ///   Planted only when the leak feature is on and a substantive letter actually fell into a hostile hand
        ///   (a grudge-holder makes the sharpest use of it, a merely hostile lord a milder one). The grudge is
        ///   what makes the leak LAST: the grudge system then escalates it into future hostile letters or an
        ///   interception, so a single waylaid letter can seed a running enmity rather than one exploiting note.
        /// </summary>
        LeakedLetter = 21,

        /// <summary>
        ///   "You took my spouse from me": Stolen Love pillar. The player carried on an affair with this hero's
        ///   husband or wife until the marriage came apart and they were left. Planted on the JILTED spouse
        ///   against the player when a stolen-love estrangement ends in divorce. A purely PERSONAL grudge (the
        ///   ratified design: no clan or faction fallout), sized to the sting of it; it feeds nemesis and the
        ///   vanilla Blood Feuds so a broken marriage can seed a running enmity.
        /// </summary>
        StolenSpouse = 22,

        /// <summary>
        ///   Left by a written tale's ending: one lord holds a deed the tale uncovered against another (Gabriel,
        ///   06/10/2026: "a quest can influence the regard between lords; CR already handles the time for that"). It
        ///   mends with time, as the grudge ledger already lets every grudge fade, unless it hardened into a vendetta.
        /// </summary>
        Tale = 23
    }
}
