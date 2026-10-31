// Code written by Gabriel Mailhot, 31/10/2026.
// The letter box, step 3 (Gabriel, 31/10/2026: "oui go pour etape 3"): a letter travels as an ENVELOPE and its text is
// written only when the player first opens it, so a letter thrown away or never opened costs no model call. Until now a
// letter with no text meant one of two things only: still being written, or dead (the model never answered). Four places
// read it that way, and each would have lost an envelope: delivery skipped it forever, the session-start cleanup deleted
// it, a courier past its deadline dropped it as blank, and a scheduled letter (a child's pension) read it as still being
// written and wrote it again. This is the one place that tells an envelope from a blank.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>What a letter without text is: an envelope waiting to be opened, or a placeholder. Pure.</summary>
    public static class LetterEnvelopePolicy
    {
        /// <summary>An envelope whose text is not written yet.</summary>
        public static bool IsUnwritten(PendingLetter letter)
            => letter != null && letter.WrittenOnOpening && string.IsNullOrWhiteSpace(letter.Content);

        /// <summary>The letter can reach the player: it has its text, or it is an envelope (written when opened).</summary>
        public static bool IsReadyToDeliver(PendingLetter letter)
            => letter != null && (!string.IsNullOrWhiteSpace(letter.Content) || letter.WrittenOnOpening);

        /// <summary>
        ///   A placeholder whose writing died with the last session (the model never answered before the game closed): it is
        ///   removed at load so its sender is not muted. Never an envelope.
        /// </summary>
        public static bool IsOrphanedPlaceholder(PendingLetter letter)
            => letter != null && string.IsNullOrWhiteSpace(letter.Content) && letter.DispatchDay == int.MaxValue && !letter.IsDelivered && !letter.WrittenOnOpening;

        /// <summary>A courier gone past its deadline drops a blank placeholder; an envelope goes on to the date fallback.</summary>
        public static bool DroppedAtCourierDeadline(PendingLetter letter)
            => letter != null && string.IsNullOrWhiteSpace(letter.Content) && !letter.WrittenOnOpening;

        /// <summary>The letter counts as SENT (a scheduled letter is fulfilled by it): it has its text, or it is an envelope.</summary>
        public static bool CountsAsSent(PendingLetter letter)
            => letter != null && (!string.IsNullOrEmpty(letter.Content) || letter.WrittenOnOpening);
    }
}
