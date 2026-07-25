// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1 robustness: is the chosen host still a place the assembly can be held? Reality can pull the
// ground out from under a convened assembly (Gabriel 2026-07-25): the host falls under siege, changes hands to
// an enemy, or ceases to exist. Any of those cancels the assembly, so this pure verdict is what the behavior
// checks each day and on the events that could invalidate a venue. Pure so every case is documented by a test.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Why a host settlement is, or is no longer, a place an assembly can convene.</summary>
    public enum HostValidity
    {
        /// <summary>The host still stands, is not besieged, and is not enemy ground: the assembly holds.</summary>
        Valid,

        /// <summary>The settlement no longer exists (razed).</summary>
        Gone,

        /// <summary>A siege is under way: no gathering can be held there.</summary>
        Besieged,

        /// <summary>Owned by, or handed to, a faction at war with the summoner: the ground is no longer safe.</summary>
        EnemyHeld
    }

    /// <summary>The host's live situation, as the validity check needs it.</summary>
    public readonly struct AssemblyHostFacts
    {
        public bool HostExists { get; init; }

        public bool HostBesieged { get; init; }

        /// <summary>The host's owner is at war with the assembly's initiator (the ground turned hostile).</summary>
        public bool HostOwnerAtWarWithInitiator { get; init; }
    }

    /// <summary>Decides whether a convened assembly's host is still a valid venue; an invalid one cancels it.</summary>
    public static class AssemblyHostValidity
    {
        /// <summary>
        ///   The verdict, from the most fundamental loss to the most situational: a settlement that is gone
        ///   cannot host anything, a besieged one is a battlefield, and enemy ground is unsafe. Any non-Valid
        ///   result cancels the assembly.
        /// </summary>
        public static HostValidity Evaluate(AssemblyHostFacts f)
        {
            if (!f.HostExists) return HostValidity.Gone;
            if (f.HostBesieged) return HostValidity.Besieged;
            if (f.HostOwnerAtWarWithInitiator) return HostValidity.EnemyHeld;

            return HostValidity.Valid;
        }

        public static bool IsValid(AssemblyHostFacts f) => Evaluate(f) == HostValidity.Valid;
    }
}
