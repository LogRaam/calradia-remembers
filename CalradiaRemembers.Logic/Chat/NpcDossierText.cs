// Code written by Gabriel Mailhot, 31/10/2026.
// The words of the chat's NPC dossier, the panel beside the conversation. Gabriel (31/10/2026): "Dans le panneau du NPC,
// on pourrait ajouter tout ce que le joueur a appris sur ce NPC et qui a ete ajoute a l'Encyclopedia". A section with no
// line stays empty so its heading hides, and the regard line carries the same reason the encyclopedia gives beside it.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>Formats the lines of the NPC dossier.</summary>
    public static class NpcDossierText
    {
        /// <summary>One bullet per line, blank lines dropped. Empty when no line remains, which hides the section.</summary>
        public static string Bullets(IEnumerable<string> lines)
            => lines == null ? "" : string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => "• " + l));

        /// <summary>The regard with its score, and the reason behind it when the player knows one.</summary>
        public static string RegardLine(string band, int score, string why)
            => $"{band} ({score:+#;-#;0})" + (string.IsNullOrWhiteSpace(why) ? "" : ": " + why);
    }
}
