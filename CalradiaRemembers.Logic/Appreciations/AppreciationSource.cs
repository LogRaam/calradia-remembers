// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: the source catalog (ROADMAP ~line 306). Every source doubles as
// a migration backfill candidate marker key in principle (see AppreciationMigration.KnownMarkers), but
// AppreciationBackfillRules today only maps the two sources that have an unambiguous existing marker
// (KindnessInScene via the Intimacy NotableEvent, QuestDoneForThem via a Completed InformalQuest); the
// rest are live-plant-only for now, exactly mirroring how GrudgeBackfillRules deliberately omits
// SchemeWarned/SchemeAbetted. External is reserved for a future Extension Surface inbound appreciation
// (mirrors GrudgeSource.External / ExternalWorldEvent).
namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>
    ///   Where an <see cref="Appreciation" /> came from. ORDINALS ARE FROZEN: this enum is persisted
    ///   inside saved <see cref="Appreciation" /> records (a future byte[] SyncData key), so existing
    ///   values must never be reordered or removed. New sources are appended at the end only, exactly
    ///   like <c>GrudgeSource</c>.
    /// </summary>
    public enum AppreciationSource
    {
        /// <summary>The player gave the holder a gift.</summary>
        GiftReceived = 0,

        /// <summary>The player gave the holder gold.</summary>
        GoldReceived = 1,

        /// <summary>The player ransomed or freed the holder, or their kin.</summary>
        RansomedOrFreed = 2,

        /// <summary>A quest the holder themselves gave the player was completed.</summary>
        QuestDoneForThem = 3,

        /// <summary>The player spent troops or influence on the holder's behalf.</summary>
        TroopsOrInfluenceSpentForThem = 4,

        /// <summary>A kindness shown to the holder within a scene (e.g. a consensual intimate moment).</summary>
        KindnessInScene = 5,

        /// <summary>The relief/gratitude a family feels when an unwanted marriage ends, from DivorceConsequencePolicy.</summary>
        DivorceGratitude = 6,

        /// <summary>An appreciation planted by an external mod through the Extension Surface inbound API.</summary>
        External = 7
    }
}
