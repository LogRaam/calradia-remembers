// Code written by Gabriel Mailhot, 08/09/2026.
// Gabriel's ruling, 2026-09-08: "une négociation c'est une entente sur ce qui est dit. Si il a été dit 595, il
// faut payer 595. Pas de bargainage après la conversation car ça fait pas de sens. L'entente est dite et
// acceptée, rien de moins. Et si le joueur ou le NPC a pas assez, la transaction ne peut se faire."
//
// What it replaces: the hire price used to be clamped into a band of 75% to 125% of the game's asking price. That
// band IS bargaining after the conversation, and it produced exactly the incoherence that exposed it. A wanderer
// said "Five hundred and ninety-five denars, paid upfront", then counted aloud, "Five... ninety-five." The action
// carried 95, the band pulled it up to 446, and the player was charged 446 while being told 595 (log 2026-09-08).
//
// The deal is the deal. Whatever price was agreed is what changes hands, and if the purse cannot cover it the
// transaction simply does not happen. That leaves the price honest by construction, and moves the burden where it
// belongs: onto the model naming a sum it means, which is a prompt and bench problem, not something to paper over
// with arithmetic after the fact.

namespace CalradiaRemembers.Logic
{
    /// <summary>The price a struck bargain actually costs: the one that was agreed, never a renegotiated one.</summary>
    public static class HireAgreementPolicy
    {
        /// <summary>
        ///   The sum to charge for an agreed hire. <paramref name="proposed" /> is the price named in the deal;
        ///   any positive value is honoured exactly, because it is what both parties said. A missing or nonsense
        ///   value (zero or below) means no sum was ever named, so the game's own <paramref name="asking" /> price
        ///   stands in: that is the default nobody negotiated away, not a counter-offer.
        /// </summary>
        public static int AgreedPrice(int proposed, int asking)
        {
            if (proposed > 0) return proposed;

            return asking > 0 ? asking : 1;
        }

        /// <summary>
        ///   Whether the buyer can honour the bargain. A purse that cannot cover the agreed sum does not buy a
        ///   cheaper version of the deal: the transaction does not happen at all.
        /// </summary>
        public static bool CanAfford(int gold, int agreedPrice) => gold >= agreedPrice;
    }
}
