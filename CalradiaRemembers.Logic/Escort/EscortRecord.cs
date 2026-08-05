// Code written by Gabriel Mailhot, 04/08/2026.
// The persisted shape of one active follow_me escort, mirrored 1:1 by the mod's own FollowMeBehavior SyncData
// (byte[] JSON via Newtonsoft.Json, key "CalradiaRemembers_FollowEscorts_v1bytes"). Kept as a pure POCO here in
// Logic (like CouncilStipend, see Assemblies/CouncilStipend.cs) so its serialization contract can be
// unit-tested directly: the mod project (CampaignBehaviorBase, IDataStore, Hero) is not referenced by
// CalradiaRemembers.Tests and stays integration-only (see project_testing_architecture).
namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>One active escort: which lord is riding along, and how many of the fixed term's days remain.</summary>
    public sealed class EscortRecord
    {
        public string LordId { get; set; }
        public int DaysRemaining { get; set; }
    }
}
