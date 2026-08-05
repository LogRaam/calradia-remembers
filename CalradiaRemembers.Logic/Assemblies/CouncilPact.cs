// Code written by Gabriel Mailhot, 05/08/2026.
// The persisted shape of one active non-aggression pact, mirrored 1:1 by the mod's own CouncilPactBehavior
// SyncData (byte[] JSON via Newtonsoft.Json, key "CalradiaRemembers_CouncilPacts_v1bytes"), exactly like
// CouncilTribute mirrors CouncilTributeBehavior and CouncilHostage mirrors CouncilHostageBehavior. Kept as a pure
// POCO here in Logic so its serialization contract can be unit-tested directly (the mod project -
// CampaignBehaviorBase, IDataStore, Hero, IFaction - is not referenced by CalradiaRemembers.Tests, see
// project_testing_architecture).
//
// Design call: BOTH faction ids are stored (FactionAId = the player's OWN faction at the moment the pact was
// struck, FactionBId = the parley enemy faction), fixed at creation - the task's own explicit shape. Unlike
// CouncilTribute's/CouncilHostage's single hero id (a payer/pledger whose own fate the term tracks), a pact binds
// two FACTIONS, so no hero id is stored at all: the grudge/relation consequence a breach applies (see
// NonAggressionPactPolicy's own header) resolves whichever hero currently LEADS the stored FactionBId live, at
// the moment of the breach, rather than pinning a single hero who might since have died, been captured, or lost
// their own clan's leadership - the SAME "re-read live, never trust a stale reference" discipline CouncilLift's
// SettleMakePeace/SettleTribute already apply to their own faction/hero lookups.
//
// Known, documented limitation (mirrors CouncilTribute's own "payer dies mid-term quietly ends the tribute"
// simplification): if the PLAYER's own faction changes after the pact is struck (a rare mid-campaign event - the
// player joins or leaves a kingdom), FactionAId still names the ORIGINAL faction, so a later breach check against
// the player's NEW faction will not match. This build does not re-resolve FactionAId live; a future build could,
// mirroring the same "documented follow-up, not built" precedent CouncilTribute/CouncilHostage already set.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One active non-aggression pact: the player's own faction, the enemy faction, and how many of the 45 days remain.</summary>
    public sealed class CouncilPact
    {
        public string FactionAId { get; set; }
        public string FactionBId { get; set; }
        public int DaysRemaining { get; set; }
    }
}
