// Code written by Gabriel Mailhot, 03/09/2026.
// give_item transferred exactly ONE unit, ignoring the count the player stated (player report, fkasad/TOR: "I
// give you 10 ingots of Gromril" moved a single ingot, leaving 9 behind). This policy decides how many units a
// gift actually moves: the number the player asked for, but never more than they hold, and never fewer than one.
// Pure and engine-free so the clamping is tested; the bridge reads the live stack size and does the transfer.

namespace CalradiaRemembers.Logic
{
    /// <summary>Resolves how many units of an item a gift transfers, clamped to what the player actually holds.</summary>
    public static class GiftQuantityPolicy
    {
        /// <summary>
        ///   The count to move: <paramref name="requested" /> clamped to [1, <paramref name="available" />]. A
        ///   missing or non-positive request defaults to one (a plain "I give you my sword" moves a single item);
        ///   a request larger than the stack gives the whole stack (you cannot give what you do not have). Returns
        ///   0 only when the player holds none, so the caller can refuse rather than transfer nothing.
        /// </summary>
        public static int Resolve(int requested, int available)
        {
            if (available <= 0) return 0;

            int wanted = requested <= 0 ? 1 : requested;

            return wanted < available ? wanted : available;
        }
    }
}
