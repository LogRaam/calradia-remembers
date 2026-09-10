// Code written by Gabriel Mailhot, 10/09/2026.
// What one console command takes and what it does. Lives in Logic rather than beside the table it fills so
// ConsoleHelpPolicy can be tested without the game assembly, which is the whole point of that project.

namespace CalradiaRemembers.Logic
{
    /// <summary>One console command, described in a player's terms.</summary>
    public sealed class ConsoleCommandDoc
    {
        public ConsoleCommandDoc(string usage, string text)
        {
            Usage = usage ?? "";
            Text = text ?? "";
        }

        /// <summary>
        ///   The arguments as a player would type them ("[name]", "&lt;amount&gt; [name]"). Empty when the command
        ///   takes none. Kept apart from <see cref="Text" /> so a listing can show the shape of a command without
        ///   spending a line on what it does.
        /// </summary>
        public string Usage { get; }

        /// <summary>
        ///   What it does, at whatever length it deserves. Short listings show the first sentence
        ///   (<see cref="ConsoleHelpPolicy.Shorten" />); cr.help &lt;name&gt; shows all of it.
        /// </summary>
        public string Text { get; }
    }
}
