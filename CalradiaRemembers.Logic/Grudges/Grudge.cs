// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: the serializable model, mirroring TrackedCommitment's shape (a
// plain state carrier the future engine-side GrudgeBehavior persists as byte[] SyncData; GrudgePolicy
// carries every decision rule). Anchored on ONE event, coexisting with regard, and resolvable — the
// three properties that RELATION (one blended number) and passive memory text both lack.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   One NPC's grievance against a target, anchored on a specific event. A plain state carrier:
    ///   <see cref="GrudgePolicy" /> drives formation, rigidity, decay, and resolution; the host
    ///   persists it and turns it into prompt colour (<see cref="GrudgeNarrator" />) and, eventually, an
    ///   encyclopedia "Grievances" section.
    /// </summary>
    public sealed class Grudge
    {
        /// <summary>Stable unique id. Migration-synthesized grudges use a deterministic composite key (holder+source+day) so re-running the migration never duplicates.</summary>
        public string Id { get; set; }

        /// <summary>Hero StringId of the NPC who nurses this grudge.</summary>
        public string HolderId { get; set; }

        /// <summary>Hero StringId of who the grudge is held against. Usually the player, but general — a divorced ex-spouse grudges the NPC who left them, not the player.</summary>
        public string TargetId { get; set; }

        /// <summary>Which system planted this grudge.</summary>
        public GrudgeSource Source { get; set; }

        /// <summary>Short, prompt-facing cause text ("you delivered me to my enemy"). No numbers; chronicler prose.</summary>
        public string CauseSummary { get; set; }

        /// <summary>In-game day the triggering event occurred. Decay and knownness reasoning both anchor on this.</summary>
        public int EventDay { get; set; }

        /// <summary>
        ///   Intensity, 0..100. Set at formation and read through <see cref="GrudgePolicy.EffectiveMagnitude" />,
        ///   which applies time decay on top. 5.7 (regard audit) doc fix: this stored value IS mutated, a partial
        ///   resolution (a Softened outcome in GrudgeResolutionService) subtracts from it directly; the old note
        ///   claiming it "never changes once set" was false.
        /// </summary>
        public int Magnitude { get; set; }

        /// <summary>How hard resolution is, 0..100, computed once at formation from the holder's regard for the target. 100 = vendetta/unforgivable.</summary>
        public int Rigidity { get; set; }

        /// <summary>True once the player has learned of this grudge (public at formation, or voiced by the NPC). Drives both encyclopedia surfacing and the prompt's open-vs-conceal instruction.</summary>
        public bool KnownToPlayer { get; set; }

        /// <summary>True once the player has learned WHAT would resolve it, not merely that it exists.</summary>
        public bool ResolutionHintDiscovered { get; set; }

        /// <summary>The resolution path this grudge offers.</summary>
        public GrudgeResolutionKind ResolutionKind { get; set; }

        /// <summary>True once mended.</summary>
        public bool IsResolved { get; set; }

        /// <summary>In-game day it was resolved, or -1 while unresolved.</summary>
        public int ResolvedDay { get; set; } = -1;
    }
}
