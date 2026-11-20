// Code written by Gabriel Mailhot, 19/11/2026.
// What the player is told while a letter they opened is being written. Vexilary (Nexus, 19/11/2026, v2.8.16, small
// local models): "Letters from letters box doesn't load". An envelope is written by the model when the player opens it;
// the box closed, "You break the seal of X's letter." showed once, and then nothing until the letter opened or the model
// timed out, which on a slow model reads as a game that hangs. Gabriel (19/11/2026): "oui ajoute le message lors de
// l'attente". If this is wrong, the player sees nothing for a minute and thinks the letter, or the game, is broken.

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>The words shown while an opened letter is being written, and when to repeat them. Pure.</summary>
    public static class LetterWaitNotice
    {
        /// <summary>Seconds between two reminders while the letter is still being written.</summary>
        public const int ReminderSeconds = 15;

        /// <summary>The words when the seal breaks and the text must still be written.</summary>
        public static string Opening(string sender)
            => $"You break the seal of {sender}'s letter. It is being written, and opens when it is ready.";

        /// <summary>A reminder is due once each <see cref="ReminderSeconds" /> waited, counting the reminders already shown.</summary>
        public static bool ReminderDue(double waitedSeconds, int remindersShown)
            => waitedSeconds >= ReminderSeconds * (remindersShown + 1);

        /// <summary>"Derthert's letter is still being written (30 s)..."</summary>
        public static string Reminder(string sender, double waitedSeconds)
            => $"{sender}'s letter is still being written ({(int) waitedSeconds} s)...";
    }
}
