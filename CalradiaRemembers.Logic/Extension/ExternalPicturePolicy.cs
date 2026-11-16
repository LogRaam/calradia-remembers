// Code written by Gabriel Mailhot, 16/11/2026.
// VonLemberg (Discord, 16/11/2026, feature request "On-Demand AI Illustrations for Chat Messages"): his mod (AI Rebel
// Relay) generates a picture of a chat line and wants CR to show it, "linked to its originating message", ignoring "stale
// results after the conversation closes or changes", and "If a per-message attachment is difficult ... we're open to a
// simpler first implementation using CR's existing scene-image panel". Gabriel (16/11/2026): "on affiche deja des images
// avec les contes. On peut reutiliser ca? ... leur mod peut le faire aussi avec leur propre prefab", then "oui vas-y pour la
// petite porte ... on verra ca plus tard pour les videos".
//
// CrConversation.ShowPicture puts another mod's picture in the open conversation's scene panel (the tales' picture). This
// decides whether a picture is shown, and bounds its caption. If it is wrong: a picture meant for a conversation the player
// has closed lands in the next one, a video or a missing file is handed to the panel, or a caption floods the margin.

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>What CR knows about one picture another mod asks it to show.</summary>
    public sealed class ExternalPictureFacts
    {
        /// <summary>A conversation is open now.</summary>
        public bool ConversationOpen { get; init; }

        /// <summary>The session id the mod named is the open conversation's (a picture of an older one is stale).</summary>
        public bool SessionMatches { get; init; }

        /// <summary>The file exists on disk.</summary>
        public bool FileExists { get; init; }

        /// <summary>The file's extension, with its dot (".png").</summary>
        public string Extension { get; init; }
    }

    /// <summary>Whether another mod's picture is shown in the open conversation, and its caption.</summary>
    public static class ExternalPicturePolicy
    {
        /// <summary>Longest caption shown under the picture, in characters: a title, not a paragraph.</summary>
        public const int MaxCaptionChars = 120;

        /// <summary>Null when the picture is shown; else why not, for the [EXT PICTURE] log line.</summary>
        public static string WhyRefused(ExternalPictureFacts f)
        {
            if (f == null || !f.ConversationOpen) return "no conversation is open";
            if (!f.SessionMatches) return "the session named is not the open conversation (stale or wrong id)";
            if (!IsStillImage(f.Extension)) return $"'{f.Extension}' is not a picture CR shows from another mod (.png, .jpg, .jpeg)";
            if (!f.FileExists) return "the file does not exist";

            return null;
        }

        /// <summary>Pictures only for now (Gabriel, 16/11/2026: videos later), the formats CR's image loader reads.</summary>
        public static bool IsStillImage(string extension)
        {
            string e = (extension ?? "").Trim().ToLowerInvariant();

            return e == ".png" || e == ".jpg" || e == ".jpeg";
        }

        /// <summary>The caption trimmed, on one line, cut at a word under <see cref="MaxCaptionChars" />; "" when none.</summary>
        public static string Caption(string caption)
        {
            string c = (caption ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (c.Length <= MaxCaptionChars) return c;

            int cut = c.LastIndexOf(' ', MaxCaptionChars - 1);
            if (cut < MaxCaptionChars / 2) cut = MaxCaptionChars - 1;

            return c.Substring(0, cut).TrimEnd() + "...";
        }
    }
}
