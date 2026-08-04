// Code written by Gabriel Mailhot, 04/08/2026.
// The persisted shape of one active tribute, mirrored 1:1 by the mod's own CouncilTributeBehavior SyncData (byte[]
// JSON via Newtonsoft.Json, key "CalradiaRemembers_CouncilTributes_v1bytes"), exactly like CouncilStipend mirrors
// CouncilStipendBehavior. Kept as a pure POCO here in Logic so its serialization contract can be unit-tested
// directly (the mod project - CampaignBehaviorBase, IDataStore, Hero - is not referenced by CalradiaRemembers.Tests,
// see project_testing_architecture).
//
// Design call: no PayeeId/PayeeClanId field. Every tribute this build supports pays TO the player (Hero.MainHero),
// never to a third clan - unlike CouncilStipend, whose PayeeId genuinely varies (whichever companion was granted
// the purse), a field that would always read "the player" on every single row is dead weight, not documentation
// (the same discipline give_influence's own council kind already applies: no amount field when the value never
// varies, see ResolutionCatalog.GiveInfluence's own doc). A future build paying tribute to a third party (not the
// player) would add the field then, additively, exactly like ParleyTargetFactionId was added to
// CouncilSessionSnapshot after the fact.
//
// Design call: no PayeeClanId either, and only ONE hero id (PayerHeroId), not a clan id. COUNCIL_ACTIONS.md's own
// Kimi review argues a tribute should survive the payer's own leader dying or being replaced ("il lie les clans,
// pas les personnes"), which would need persisting the CLAN rather than the hero. This build makes the simpler,
// documented choice instead: mirror CouncilStipend's own PayeeId-is-a-hero shape exactly, so a payer who dies
// mid-term quietly ends the tribute (CouncilTributeBehavior.Advance), the SAME graceful-end CouncilStipendBehavior
// already gives a companion who dies mid-tranche. Surviving a leader's death is a real, deliberately deferred
// enhancement, not silently dropped.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One active tribute: who pays, how much per day, how many of its 30 days remain, and how many consecutive days have been missed.</summary>
    public sealed class CouncilTribute
    {
        public string PayerHeroId { get; set; }
        public int AmountPerDay { get; set; }
        public int DaysRemaining { get; set; }
        public int MissedDays { get; set; }
    }
}
