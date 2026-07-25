// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1 robustness: what becomes of an invitee who was travelling in person when reality intervenes
// (Gabriel 2026-07-25). A sea crossing or sheer distance means they cannot arrive in time; a battle lost on the
// road leaves them captured or dead; a siege or a battle pins them. The rule: death empties the seat, anything
// short of death that stops them arriving in person sends an AMBASSADOR in their stead, and otherwise they keep
// coming. Pure so every combination is documented by a test.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>What happens to an EnRoute invitee re-checked against the live world.</summary>
    public enum TravelOutcome
    {
        /// <summary>Still on the road and still able to reach the host in time: keep coming.</summary>
        Arriving,

        /// <summary>Cannot come in person (captured, pinned, too far, or at sea), so a stand-in sits in their name.</summary>
        Ambassador,

        /// <summary>Died on the way: the seat is simply empty.</summary>
        Fell
    }

    /// <summary>One travelling invitee's live situation, as the re-check needs it.</summary>
    public readonly struct AssemblyTravelFacts
    {
        public bool IsAlive { get; init; }

        /// <summary>Taken prisoner: cannot attend, but their house can still send a stand-in.</summary>
        public bool IsPrisoner { get; init; }

        /// <summary>Pinned in a battle, or besieged inside a settlement: cannot break away to travel.</summary>
        public bool IsOccupied { get; init; }

        /// <summary>ETA is within the days left. False when at sea, too far, or the deadline has already passed.</summary>
        public bool CanReachByDeadline { get; init; }
    }

    /// <summary>Re-decides a travelling invitee's fate when the world moves under them.</summary>
    public static class AssemblyTravelOutcome
    {
        /// <summary>
        ///   Ordered most final first: death empties the seat outright; short of that, captivity, being pinned,
        ///   or simply being unable to reach the host in time all send an ambassador; only a hero still alive,
        ///   free, unpinned and able to arrive keeps travelling.
        /// </summary>
        public static TravelOutcome Evaluate(AssemblyTravelFacts f)
        {
            if (!f.IsAlive) return TravelOutcome.Fell;
            if (f.IsPrisoner) return TravelOutcome.Ambassador;
            if (f.IsOccupied) return TravelOutcome.Ambassador;
            if (!f.CanReachByDeadline) return TravelOutcome.Ambassador;

            return TravelOutcome.Arriving;
        }
    }
}
