// Code written by Gabriel Mailhot, 04/08/2026.
// The persisted shape of one in-flight lend_troops delegation, mirrored 1:1 by the mod's own
// CouncilTroopDeliveryBehavior SyncData (byte[] JSON via Newtonsoft.Json, key
// "CalradiaRemembers_CouncilTroopDeliveries_v1bytes"). Kept as a pure POCO here in Logic (like CouncilStipend, see
// CouncilStipend.cs, and AssemblyResolutionEntry, see AssemblyLedger.cs) rather than in the mod project, so its
// serialization contract can be unit-tested directly: the mod project (CampaignBehaviorBase, IDataStore, Hero,
// MobileParty) is not referenced by CalradiaRemembers.Tests and stays integration-only (see
// project_testing_architecture). Gabriel's CONDITIONS (COUNCIL_ACTIONS.md, lend_troops): (A) the troops travel
// with a TEMPORARY delegation (never the lending lord himself - "trop facile a pieger"); (B) the gift is
// PERMANENT, never a loan that returns. EscortLeaderId is null exactly once: the FALLBACK path
// (COUNCIL_ACTIONS.md's Kimi review under lend_troops) when the donor's clan has no minor hero free to lead the
// escort - see CouncilTroopDeliveryBehavior's own header for which of Kimi's two proposed fallbacks (instant
// delivery vs. refusal) this build picked, and why.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   One in-flight troop delegation: who sent it, who leads it, which party it rides as, and how many troops
    ///   of each tier it carries toward the player. Removed from the active list the moment it resolves (arrival,
    ///   destruction en route, or a war declared mid-transit) - unlike CouncilStipend, there is no day countdown to
    ///   persist, only the journey itself.
    /// </summary>
    public sealed class CouncilTroopDelivery
    {
        /// <summary>Stable id assigned at the lift, for logging/diagnostics only (no lookup keys on it).</summary>
        public string Id { get; set; }

        /// <summary>StringId of the seated lord whose party the troops left (their clan, never the player's own).</summary>
        public string DonorHeroId { get; set; }

        /// <summary>
        ///   StringId of the minor hero leading the delegation, or null on the rare occasions the donor's clan had
        ///   nobody free to send (see this file's own header for the fallback that then applies instead of this
        ///   behavior tracking a delivery at all).
        /// </summary>
        public string EscortLeaderId { get; set; }

        /// <summary>StringId of the delegation's own MobileParty, so the daily tick can find and steer it.</summary>
        public string PartyId { get; set; }

        /// <summary>Low-tier troops the delegation carries (Gabriel's CONDITIONS C: a genuine tier mix, never all levies).</summary>
        public int LowCount { get; set; }

        /// <summary>Medium-tier troops the delegation carries.</summary>
        public int MediumCount { get; set; }

        /// <summary>High-tier troops the delegation carries.</summary>
        public int HighCount { get; set; }
    }
}
