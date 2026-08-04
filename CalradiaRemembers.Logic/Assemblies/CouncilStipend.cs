// Code written by Gabriel Mailhot, 03/08/2026.
// The persisted shape of one active council-granted stipend, mirrored 1:1 by the mod's own CouncilStipendBehavior
// SyncData (byte[] JSON via Newtonsoft.Json, key "CalradiaRemembers_CouncilStipends_v1bytes"). Kept as a pure
// POCO here in Logic (like AssemblyResolutionEntry, see AssemblyLedger.cs) rather than in the mod project, so its
// serialization contract can be unit-tested directly: the mod project (CampaignBehaviorBase, IDataStore, Hero) is
// not referenced by CalradiaRemembers.Tests and stays integration-only (see project_testing_architecture).

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One active stipend: who is paid, how much per day, how many of its 30 days remain, and what is left in escrow.</summary>
    public sealed class CouncilStipend
    {
        public string PayeeId { get; set; }
        public int AmountPerDay { get; set; }
        public int DaysRemaining { get; set; }
        public int EscrowBalance { get; set; }
    }
}
