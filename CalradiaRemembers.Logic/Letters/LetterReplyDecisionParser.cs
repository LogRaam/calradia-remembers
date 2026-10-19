// Code written by Gabriel Mailhot, 17/07/2026.
// Courier audit 5.1. When the player writes to an NPC, an LLM decides whether that NPC answers, and if so after
// how long. The model's raw reply is a terse control line ("PASS: too busy", "DELAY:3\n<letter body>", or just a
// bare letter body) that used to be parsed inline in LetterGenerationService with no tests. A mis-parse is not
// cosmetic: read a reply as a PASS and the player's letter goes forever unanswered; read a PASS as a reply and a
// blank courier is dispatched; let a garbage delay through and the courier departs on a nonsense schedule. This
// pure parser isolates that decision so its every branch (and every tolerance) is pinned by tests. It is
// deliberately forgiving: an unrecognised shape is treated as a plain letter body on the default delay, never a
// dropped reply, because a weak model that ignores the format should still get its letter delivered.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>Whether the NPC answers the player's letter at all.</summary>
    public enum LetterReplyKind
    {
        /// <summary>The NPC chose not to reply. No courier is dispatched.</summary>
        Pass,

        /// <summary>The NPC replies; a courier departs after <see cref="LetterReplyDecision.DelayDays" /> days.</summary>
        Reply
    }

    /// <summary>The parsed outcome of an NPC's letter-reply decision. A value type so it is trivially testable.</summary>
    public readonly struct LetterReplyDecision
    {
        private LetterReplyDecision(LetterReplyKind kind, string passReason, int delayDays, string body)
        {
            Kind = kind;
            PassReason = passReason;
            DelayDays = delayDays;
            Body = body;
        }

        /// <summary>Whether the NPC replies or passes.</summary>
        public LetterReplyKind Kind { get; }

        /// <summary>On a <see cref="LetterReplyKind.Pass" />, the reason the model gave (may be empty). Logged only.</summary>
        public string PassReason { get; }

        /// <summary>On a <see cref="LetterReplyKind.Reply" />, days before the courier departs, clamped to a sane range.</summary>
        public int DelayDays { get; }

        /// <summary>
        ///   On a <see cref="LetterReplyKind.Reply" />, the letter text. May be empty when the model emitted a
        ///   DELAY line with no body: the host treats an empty body as "nothing to send" and dispatches nothing.
        /// </summary>
        public string Body { get; }

        internal static LetterReplyDecision Passed(string reason)
            => new(LetterReplyKind.Pass, reason ?? string.Empty, 0, string.Empty);

        internal static LetterReplyDecision Replied(int delayDays, string body)
            => new(LetterReplyKind.Reply, string.Empty, delayDays, body ?? string.Empty);
    }

    /// <summary>
    ///   Parses the terse control line an LLM returns when deciding whether an NPC answers the player's letter,
    ///   into a typed <see cref="LetterReplyDecision" />. Tolerant by construction: any shape that is not clearly
    ///   a pass becomes a reply on the default delay, so a badly-formatted answer is delivered rather than dropped.
    /// </summary>
    public static class LetterReplyDecisionParser
    {
        /// <summary>Delay used when the model omits (or garbles) an explicit DELAY line. Tuning.</summary>
        public const int DefaultDelayDays = 2;

        /// <summary>Floor on an explicit delay: a courier cannot depart in the past or the same instant. Tuning.</summary>
        public const int MinDelayDays = 1;

        /// <summary>Ceiling on an explicit delay, so a stray large number cannot park a reply for months. Tuning.</summary>
        public const int MaxDelayDays = 14;

        /// <summary>
        ///   Parses <paramref name="raw" />. An empty or whitespace input reads as a pass (there is nothing to
        ///   send). A line beginning "PASS" (or a bare "no"/"no reply") is a pass, carrying any reason after the
        ///   first colon. Anything else is a reply: a leading "DELAY:N" sets the (clamped) delay and the body is
        ///   what follows it; with no DELAY line the whole text is the body on the default delay.
        /// </summary>
        public static LetterReplyDecision Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return LetterReplyDecision.Passed(string.Empty);

            string trimmed = raw.Trim();

            if (trimmed.StartsWith("PASS", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("no reply", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                var reason = string.Empty;
                int colon = trimmed.IndexOf(':');
                if (colon >= 0 && colon < trimmed.Length - 1)
                    reason = trimmed.Substring(colon + 1).Split('\n')[0].Trim();

                return LetterReplyDecision.Passed(reason);
            }

            int delayDays = DefaultDelayDays;
            string body = trimmed;

            int firstNewline = trimmed.IndexOf('\n');
            string firstLine = firstNewline > 0
                ? trimmed.Substring(0, firstNewline).Trim()
                : trimmed;

            if (firstLine.StartsWith("DELAY:", StringComparison.OrdinalIgnoreCase))
            {
                string delayStr = firstLine.Substring("DELAY:".Length).Trim();
                if (int.TryParse(delayStr, out int parsed) && parsed >= MinDelayDays && parsed <= MaxDelayDays)
                    delayDays = parsed;

                body = firstNewline > 0
                    ? trimmed.Substring(firstNewline).Trim()
                    : string.Empty;
            }

            return LetterReplyDecision.Replied(delayDays, body);
        }
    }
}
