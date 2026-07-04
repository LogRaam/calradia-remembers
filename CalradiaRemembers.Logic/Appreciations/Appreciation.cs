// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: the serializable model, a LEAN sibling of Grudge (ROADMAP
// ~line 302). A plain state carrier: AppreciationPolicy carries every decision rule; the host persists
// it and turns it into prompt colour (AppreciationNarrator) and, eventually, an encyclopedia
// "Kindnesses"/"Debts of gratitude" section. Anchored on ONE event, coexisting with regard, same as a
// Grudge — but with NO rigidity and NO resolution mechanic: its lifecycle is form, reinforce regard, fade
// slowly. There is nothing here to "resolve"; it just lingers and softens.

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>
    ///   One NPC's felt kindness toward a target, anchored on a specific event. A plain state carrier:
    ///   <see cref="AppreciationPolicy" /> drives formation and decay; the host persists it and turns it
    ///   into prompt colour (<see cref="AppreciationNarrator" />) and, eventually, an encyclopedia
    ///   "Kindnesses" section.
    /// </summary>
    public sealed class Appreciation
    {
        /// <summary>Stable unique id. Migration-synthesized appreciations use a deterministic composite key (holder+source+day) so re-running the migration never duplicates.</summary>
        public string Id { get; set; }

        /// <summary>Hero StringId of the NPC who feels this appreciation.</summary>
        public string HolderId { get; set; }

        /// <summary>Hero StringId of who the appreciation is felt toward. Usually the player, but general.</summary>
        public string TargetId { get; set; }

        /// <summary>Which system planted this appreciation.</summary>
        public AppreciationSource Source { get; set; }

        /// <summary>Short, prompt-facing cause text ("you paid my ransom without being asked twice"). No numbers; chronicler prose.</summary>
        public string CauseSummary { get; set; }

        /// <summary>In-game day the triggering event occurred. Decay and knownness reasoning both anchor on this.</summary>
        public int EventDay { get; set; }

        /// <summary>Intensity at formation, 0..100. Softens over time via <see cref="AppreciationPolicy.EffectiveMagnitude" />; this stored value never changes once set.</summary>
        public int Magnitude { get; set; }

        /// <summary>True once the player has learned of this appreciation (the NPC has voiced it, or it formed from a plainly visible deed). Drives both encyclopedia surfacing and the prompt's naming-vs-quiet-warmth instruction.</summary>
        public bool KnownToPlayer { get; set; }
    }
}
