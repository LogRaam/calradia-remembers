// Code written by Gabriel Mailhot, 25/11/2026.
// YubYub02 (Discord, 24/11/2026), LM Studio with nvidia/nemotron-3-nano-4b: "In the settings menu test it says the
// endpoint is reachable, ai responded and is ready to play. HOWEVER any attempt in the game immediately 'Message did
// not reach the AI'... Less than a second delay on this." The Test button sent a one-word "ping", which any server
// answers, while every message in the game carries a prompt of thousands of tokens: a model loaded with a short
// context refuses it at once, and nothing told the player that the context length was the knob to turn. This decides
// how big a request the Test button sends to prove the server can hold CR, which servers it sends it to, and how a
// server's "too long for my context" refusal is said to the player, at the button and in the game alike.

#region

using System;
using System.Text;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Logic.Providers
{
    /// <summary>
    ///   The size of the Test button's context probe, which endpoints get it, and the plain advice for a server that
    ///   refused a request longer than its context.
    /// </summary>
    public static class ContextLengthPolicy
    {
        /// <summary>
        ///   The probe size when no local window is declared: more than the 4,096 tokens a local server often loads
        ///   a model with, less than the 8,192 of the next common setting. CR's smallest Compact prompt alone is
        ///   about 2,100 tokens (the SDK's Lean budget guard: 7,400 characters) before any memory or conversation,
        ///   so a server that cannot hold this cannot hold a turn.
        /// </summary>
        public const int DefaultProbeTokens = 7000;

        /// <summary>The share of a declared window the probe fills, leaving room for the chat template's own tokens.</summary>
        private const double DeclaredWindowShare = 0.9;

        private static readonly Regex ServerWindow = new Regex(@"context[ _](?:length|size|window)\D{0,40}?(\d{3,7})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        ///   How many tokens the probe should carry: most of the <paramref name="declaredWindow" /> when the player
        ///   declared one (proving the server really holds what CR will fill), else <see cref="DefaultProbeTokens" />.
        /// </summary>
        public static int ProbeTokens(int declaredWindow)
            => declaredWindow > 0 ? (int) (declaredWindow * DeclaredWindowShare) : DefaultProbeTokens;

        /// <summary>
        ///   Whether the Test button should send the probe at all: only to a server on this machine or the home
        ///   network, or when a local window is declared. A paid cloud endpoint is not billed for thousands of tokens
        ///   by a click meant to check a key.
        /// </summary>
        public static bool ShouldProbe(string endpointUrl, int declaredWindow)
            => declaredWindow > 0 || IsLocalUrl(endpointUrl);

        /// <summary>True for localhost, a loopback address, a private (home network) address or a .local name.</summary>
        public static bool IsLocalUrl(string endpointUrl)
        {
            if (string.IsNullOrWhiteSpace(endpointUrl)) return false;
            if (!Uri.TryCreate(endpointUrl.Trim(), UriKind.Absolute, out Uri uri)) return false;

            string host = uri.Host.Trim('[', ']').ToLowerInvariant();
            if (host == "localhost" || host == "::1" || host == "0.0.0.0" || host.EndsWith(".local", StringComparison.Ordinal)) return true;

            string[] parts = host.Split('.');
            if (parts.Length != 4 || !int.TryParse(parts[0], out int a) || !int.TryParse(parts[1], out int b)) return false;

            return a == 127 || a == 10 || (a == 192 && b == 168) || (a == 172 && b >= 16 && b <= 31);
        }

        /// <summary>
        ///   Filler text of roughly <paramref name="tokens" /> tokens (at the SDK's own characters-per-token
        ///   estimate) for the probe's message. Ordinary prose, so every tokenizer reads it at an ordinary rate.
        /// </summary>
        public static string Filler(int tokens)
        {
            const string sentence = "The road to Pravend runs past the old mill, and the river is high this spring. ";
            int chars = (int) (Math.Max(0, tokens) * ContextFitPolicy.DefaultCharsPerToken);
            var sb = new StringBuilder(chars + sentence.Length);
            while (sb.Length < chars) sb.Append(sentence);

            return sb.ToString();
        }

        /// <summary>
        ///   The advice for a server <paramref name="error" /> that refused a request for being longer than the
        ///   model's context, naming the server's own number when it gives one; null for any other error.
        /// </summary>
        public static string ExplainOverflow(string error, int declaredWindow)
        {
            if (string.IsNullOrWhiteSpace(error) || !IsOverflow(error)) return null;

            Match m = ServerWindow.Match(error);
            string theirs = m.Success ? $" ({m.Groups[1].Value} tokens)" : "";
            string declared = declaredWindow > 0 ? $" (it is set to {declaredWindow} now)" : "";

            return $"Your server refused the request: it is longer than the model's context{theirs}. Load the model with a " +
                   "larger context length in your server (LM Studio: Context Length, when loading the model; Ollama: num_ctx), " +
                   "16384 or more if your memory allows, then set 'Local model context (tokens)' in Mod Options -> Calradia " +
                   $"Remembers -> Prompt & Memory to that same number{declared}. CR then fits every request to it.";
        }

        private static bool IsOverflow(string error)
        {
            string e = error.ToLowerInvariant();

            return e.Contains("context length") || e.Contains("context size") || e.Contains("context window") || e.Contains("context_length")
                   || e.Contains("maximum context") || e.Contains("n_ctx");
        }
    }
}
