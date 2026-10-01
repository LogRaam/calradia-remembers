// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "A lord (faction A) is at war with faction B. You are not. He hires you as envoy to
// negotiate the release of one of his guys... 'Your mission, if you accept it, is to go and get me that lord back to
// his family. Here is 5000 denars. Bring my lad alive. You can keep the change.'" And: "Prisoners tend to escape or be
// released. So safety wall needed here."
//
// Gabriel's ruling the same day (option A): the giver ADVANCES the purse and the player keeps what he does not spend.
// The safety wall: if the man goes free some other way before the player frees him (a ransom, a peace, his own
// escape, death), the task ends with no fault and the advance goes back to the giver, as far as the player's purse
// holds. If the player lets the deadline pass, he keeps the purse, and the broken word and the grudge do their work.

namespace CalradiaRemembers.Logic
{
    /// <summary>Who a giver may send the player to buy back.</summary>
    public sealed class RansomEnvoyFacts
    {
        /// <summary>The man is a prisoner now.</summary>
        public bool IsPrisoner { get; set; }

        /// <summary>He is of the giver's house or realm: a lord buys back his own, not a stranger.</summary>
        public bool IsTheGiversOwn { get; set; }

        /// <summary>The player holds him already: there is no one to buy him from.</summary>
        public bool HeldByThePlayer { get; set; }

        /// <summary>
        ///   The player is at war with the realm that holds him. Then the errand is a rescue by force, not an
        ///   envoy's: an envoy is what a lord sends where the player can walk in and he cannot.
        /// </summary>
        public bool PlayerAtWarWithTheCaptor { get; set; }

        /// <summary>The giver has the purse to advance.</summary>
        public bool GiverCanPay { get; set; }
    }

    /// <summary>How a captive's captivity ended, as the envoy task reads it.</summary>
    public sealed class EnvoyReleaseFacts
    {
        /// <summary>The engine's reason.</summary>
        public CaptivityEndReason Reason { get; set; }

        /// <summary>The player held him when he went free (bought, then let go or handed home).</summary>
        public bool HeldByThePlayer { get; set; }

        /// <summary>The player broke him out of a prison (the game's own prison break).</summary>
        public bool BrokenOutByThePlayer { get; set; }
    }

    /// <summary>The rules of a ransom_envoy task. Pure.</summary>
    public static class RansomEnvoyPolicy
    {
        /// <summary>
        ///   The purse, against the game's own ransom value of the man: half again as much, so a hard bargain is
        ///   covered and a good one leaves change ("you can keep the change"). The captor's sale is clamped to
        ///   0.4x-3x that same value (PrisonerTradePolicy), so a very hard captor can still cost more.
        /// </summary>
        public const float AdvanceFactor = 1.5f;

        /// <summary>Whether the giver may send the player to buy this man back.</summary>
        public static bool MaySend(RansomEnvoyFacts f)
            => f != null && f.IsPrisoner && f.IsTheGiversOwn && !f.HeldByThePlayer && !f.PlayerAtWarWithTheCaptor && f.GiverCanPay;

        /// <summary>The advance for a man of this ransom value.</summary>
        public static int Advance(int ransomValue)
            => ransomValue <= 0 ? 0 : (int) System.Math.Ceiling(ransomValue * AdvanceFactor);

        /// <summary>
        ///   Whether this freeing is the player's deed: he let go (or handed home) a man he held, or broke him out.
        ///   A ransom paid by someone else, a peace, an escape nobody helped, a death: none of those.
        /// </summary>
        public static bool IsThePlayersDeed(EnvoyReleaseFacts f)
            => f != null && (f.BrokenOutByThePlayer || f.HeldByThePlayer && f.Reason == CaptivityEndReason.ByChoice);

        /// <summary>What goes back to the giver when the task ends through no one's fault: the advance, as far as the player's purse holds.</summary>
        public static int RefundOnVoid(int advance, int playerGold)
            => advance <= 0 || playerGold <= 0 ? 0 : advance < playerGold ? advance : playerGold;
    }
}
