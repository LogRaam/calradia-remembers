// Code written by Gabriel Mailhot, 05/08/2026.
// The persisted shape of one active sponsor_ward mentorship bond, mirrored 1:1 by the mod's own
// CouncilWardBehavior SyncData (byte[] JSON via Newtonsoft.Json, key "CalradiaRemembers_CouncilWards_v1bytes"),
// exactly like CouncilStipend/CouncilTribute/CouncilHostage mirror their own behaviors. Kept as a pure POCO here
// in Logic so its serialization contract can be unit-tested directly (the mod project - CampaignBehaviorBase,
// IDataStore, Hero - is not referenced by CalradiaRemembers.Tests, see project_testing_architecture).
//
// Design call: NO term field, unlike every sibling in this file's own family (CouncilStipend.DaysRemaining,
// CouncilTribute.DaysRemaining, CouncilHostage.DaysRemaining all carry a fixed countdown). COUNCIL_ACTIONS.md's
// own "sponsor_ward" section (Kimi's review): a sponsorship is not a fixed-day pledge like a stipend tranche or
// a hostage's residency - it is an ongoing bond that persists until the WORLD dissolves it (either party dying
// or leaving the player's own clan, see CouncilWardBehavior.Advance), never a calendar. Also NO escrow/amount
// field (unlike CouncilStipend): nothing is transferred or reserved when a bond starts, only a link recorded.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One active mentorship bond: which seated clan member mentors which young companion of the player's own clan.</summary>
    public sealed class CouncilWard
    {
        public string MentorId { get; set; }
        public string ProtegeId { get; set; }
    }
}
