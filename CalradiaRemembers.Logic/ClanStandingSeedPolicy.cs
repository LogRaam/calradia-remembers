// Code written by Gabriel Mailhot, 30/09/2026.
// A clan's standing warms a first meeting (tashmetu, Nexus, 30/09/2026): a lord whose whole clan counted the player a
// friend (+54) still met him as a near-stranger, because personal regard starts at 0 and nothing read the clan's
// standing. Ruled by Gabriel the same day:
//
//   - the clan's standing seeds personal regard ONCE, at the first conversation where it warrants a seed;
//   - never past Regard.FriendlyTier, the ceiling letters have: a reputation earned without meeting stops at friendly;
//   - a clan the game counts a close friend (VanillaRelation.CloseTier, "counts this player a trusted friend" in the
//     prompt) gives friendly, a friendly one (VanillaRelation.FriendTier, "regards this player favorably") cordial;
//   - never lowers regard, never touches a character the player has personally offended (regard below neutral);
//   - the pace of regard (RelationGate's cooldown and cap) is untouched.

#region

using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The facts the seed is decided on.</summary>
    public sealed class ClanStandingSeedFacts
    {
        /// <summary>The game's relation between the character and the player (the prompt's "clan standing").</summary>
        public int ClanStanding { get; set; }

        /// <summary>The character's personal regard for the player now.</summary>
        public int CurrentRegard { get; set; }

        /// <summary>The seed was already spent on this character: it never comes back.</summary>
        public bool AlreadySeeded { get; set; }

        /// <summary>The MCM option. Off: regard starts from zero with everyone, as before 2.6.8.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>What the clan's standing does to one character's regard.</summary>
    public readonly struct ClanStandingSeed
    {
        public ClanStandingSeed(int shift, bool consumed)
        {
            Shift = shift;
            Consumed = consumed;
        }

        /// <summary>How much regard rises now (never negative).</summary>
        public int Shift { get; }

        /// <summary>The seed is spent: record it so it is never given again.</summary>
        public bool Consumed { get; }
    }

    /// <summary>Decides the one-time warm start a friendly clan gives. Pure.</summary>
    public static class ClanStandingSeedPolicy
    {
        /// <summary>The regard a clan's standing alone is worth: friendly, cordial, or nothing.</summary>
        public static int SeedFor(int clanStanding)
            => clanStanding >= VanillaRelation.CloseTier ? Regard.FriendlyTier
               : clanStanding >= VanillaRelation.FriendTier ? Regard.CordialTier
               : Regard.NeutralTier;

        /// <summary>Whether and how far this character's regard rises from their clan's standing.</summary>
        public static ClanStandingSeed Decide(ClanStandingSeedFacts f)
        {
            if (f == null || !f.Enabled || f.AlreadySeeded) return new ClanStandingSeed(0, false);

            int seed = SeedFor(f.ClanStanding);
            if (seed <= Regard.NeutralTier) return new ClanStandingSeed(0, false);
            if (f.CurrentRegard < Regard.NeutralTier) return new ClanStandingSeed(0, true);

            return new ClanStandingSeed(f.CurrentRegard >= seed ? 0 : seed - f.CurrentRegard, true);
        }
    }
}
