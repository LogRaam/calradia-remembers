// Code written by Gabriel Mailhot, 04/08/2026.
// The persisted shape of one active hostage term, mirrored 1:1 by the mod's own CouncilHostageBehavior SyncData
// (byte[] JSON via Newtonsoft.Json, key "CalradiaRemembers_CouncilHostages_v1bytes"), exactly like CouncilTribute
// mirrors CouncilTributeBehavior. Kept as a pure POCO here in Logic so its serialization contract can be
// unit-tested directly (the mod project - CampaignBehaviorBase, IDataStore, Hero - is not referenced by
// CalradiaRemembers.Tests, see project_testing_architecture).
//
// Design call: HomeClanId (not just PledgerId) is carried separately, unlike CouncilTribute's single-hero shape.
// A hostage's TERM outlives any one person's fate (the pledger who gave them may die, be captured, or lose their
// own party long before the 45 days are up, COUNCIL_ACTIONS.md's own "le garant meurt/change de faction ->
// l'otage retourne a sa famille" case): CouncilHostageBehavior.Advance falls back from the pledger's own live
// party to ANY other alive lord of the same home clan leading a party, and only then to the clan's own
// HomeSettlement, so the clan id is needed even when the original pledger is long gone.
//
// Design call: Residence is a plain descriptive string, not an enum, FIXED to "MainParty" this build (the
// hostage always joins MobileParty.MainParty, never a fief - see CouncilHostageBehavior's own header for why a
// party was chosen over StayingInSettlement). Kept as a field rather than a hard-coded assumption so a future
// build offering a fief residence choice extends this additively, mirroring how ParleyTargetFactionId was added
// to CouncilSessionSnapshot after the fact rather than reshaping an existing field.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One active hostage term: who is held, who gave them, which clan they call home, how many of the 45 days remain, and where they currently reside.</summary>
    public sealed class CouncilHostage
    {
        public string HostageId { get; set; }
        public string PledgerId { get; set; }
        public string HomeClanId { get; set; }
        public int DaysRemaining { get; set; }
        public string Residence { get; set; }
    }
}
