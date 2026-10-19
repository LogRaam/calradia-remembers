// Code written by Gabriel Mailhot, 21/06/2026.
// The dedicated courier window's mode rules: which parts of the parchment are shown, and when the
// player may actually seal a letter. Pure so the "can't send blank" / "can't afford the courier"
// guards — the things that lose a player's words or their gold if they drift — stay protected by tests.

#region

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>What the courier window is doing right now.</summary>
    public enum LetterWindowMode
    {
        /// <summary>Reading a letter that just arrived — acknowledge or open a reply.</summary>
        Read,

        /// <summary>Writing a fresh letter to a chosen recipient (a courier fee may apply).</summary>
        Compose,

        /// <summary>Replying to a received letter (its body shown above the reply).</summary>
        Reply
    }

    /// <summary>
    ///   The view-agnostic state of the courier window for one open session: its mode and the courier
    ///   fee, and from those the widget visibility and the send eligibility. The host owns the actual
    ///   parchment and the sealing; this owns the rules.
    /// </summary>
    public sealed class LetterComposerState
    {
        /// <summary>The window's current purpose.</summary>
        public LetterWindowMode Mode { get; }

        /// <summary>The denars a courier charges to carry a freshly composed letter (0 = none / not compose).</summary>
        public int CourierCost { get; }

        public LetterComposerState(LetterWindowMode mode, int courierCost = 0)
        {
            Mode = mode;
            CourierCost = courierCost < 0 ? 0 : courierCost;
        }

        /// <summary>The writing area is offered only when the player is composing or replying.</summary>
        public bool ShowsInput => Mode == LetterWindowMode.Compose || Mode == LetterWindowMode.Reply;

        /// <summary>The received letter's body is shown when reading it or replying to it.</summary>
        public bool ShowsReceivedBody => Mode == LetterWindowMode.Read || Mode == LetterWindowMode.Reply;

        /// <summary>Acknowledge / Reply choices belong only to a just-delivered letter being read.</summary>
        public bool ShowsAcknowledge => Mode == LetterWindowMode.Read;

        /// <summary>The Reply button (turns a read into a reply) shows only while reading.</summary>
        public bool ShowsReplyButton => Mode == LetterWindowMode.Read;

        /// <summary>The Seal &amp; Send button shows only when there is something to write.</summary>
        public bool ShowsSend => Mode == LetterWindowMode.Compose || Mode == LetterWindowMode.Reply;

        /// <summary>True when the player's purse covers the courier fee (always true when there is none).</summary>
        public bool CanAfford(int playerGold) => playerGold >= CourierCost;

        /// <summary>
        ///   Whether the player may seal and send right now: there must be a writing mode, a non-blank
        ///   draft, and — for a freshly composed letter — enough gold for the courier. A reply rides
        ///   the existing exchange and carries no fee.
        /// </summary>
        public bool CanSend(string draftText, int playerGold)
        {
            if (!ShowsSend) return false;
            if (string.IsNullOrWhiteSpace(draftText)) return false;
            if (Mode == LetterWindowMode.Compose && !CanAfford(playerGold)) return false;

            return true;
        }
    }
}
