// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, RFC "Headless NPC Persona Invocation API"): "Reuse the configured CR/Relay provider
// infrastructure where possible ... Support short, bounded outputs ... Return a clear failure when a required route is
// unavailable." Gabriel (15/11/2026): show him "la surface du service LLM si il veut lancer l'appel a partir de CR".
// CrLlm lets another mod send one request through the player's own CR connection (the prose model the player chose). This
// policy decides, before a cent is spent, whether a request may go and how long its answer may be.
//
// If this is wrong: another mod spends the player's credit after the player turned the door off, an unnamed caller spends it
// with no trace of who, or a request asks for 50,000 tokens on the player's key.

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>What CrLlm knows about one request from another mod, before sending it.</summary>
    public sealed class ExternalLlmRequestFacts
    {
        /// <summary>The calling mod's name, as it signs the request.</summary>
        public string SourceMod { get; init; }

        /// <summary>Whether the request carries a system prompt or at least one message: something to answer.</summary>
        public bool HasSomethingToAnswer { get; init; }

        /// <summary>The answer length the caller asked for, in tokens (0 or less: CR's default).</summary>
        public int AskedMaxOutputTokens { get; init; }

        /// <summary>The player's MCM choice "Other mods may use my connection".</summary>
        public bool PlayerAllowsOtherMods { get; init; }

        /// <summary>Whether CR's connection is set up (a key, a model); false outside a campaign or before setup.</summary>
        public bool ConnectionReady { get; init; }
    }

    /// <summary>Why a request from another mod was not sent.</summary>
    public enum ExternalLlmRefusal
    {
        /// <summary>Sent.</summary>
        None = 0,

        /// <summary>The player turned "Other mods may use my connection" off.</summary>
        PlayerTurnedTheDoorOff,

        /// <summary>CR's connection is not set up (no key, no model) or no campaign is loaded.</summary>
        ConnectionNotReady,

        /// <summary>The request names no calling mod.</summary>
        NoSourceMod,

        /// <summary>Neither a system prompt nor a message: nothing to answer.</summary>
        NothingToAnswer
    }

    /// <summary>The ruling on one request: whether it goes, and the answer length it may have.</summary>
    public sealed class ExternalLlmRuling
    {
        /// <summary>None when the request may go.</summary>
        public ExternalLlmRefusal Refusal { get; init; }

        /// <summary>The answer length sent to the model, in tokens.</summary>
        public int MaxOutputTokens { get; init; }

        /// <summary>Whether the request may go.</summary>
        public bool Allowed => Refusal == ExternalLlmRefusal.None;
    }

    /// <summary>Rules on a request another mod sends through CrLlm. Pure.</summary>
    public static class ExternalLlmRequestPolicy
    {
        /// <summary>The answer length when the caller names none: a short reply or letter.</summary>
        public const int DefaultMaxOutputTokens = 600;

        /// <summary>The longest answer a caller may ask for on the player's key: a long letter, never a book.</summary>
        public const int CeilingMaxOutputTokens = 4000;

        /// <summary>Whether the request goes, and how long its answer may be.</summary>
        public static ExternalLlmRuling Rule(ExternalLlmRequestFacts facts)
        {
            ExternalLlmRefusal refusal =
                !facts.PlayerAllowsOtherMods ? ExternalLlmRefusal.PlayerTurnedTheDoorOff
                : !facts.ConnectionReady ? ExternalLlmRefusal.ConnectionNotReady
                : string.IsNullOrWhiteSpace(facts.SourceMod) ? ExternalLlmRefusal.NoSourceMod
                : !facts.HasSomethingToAnswer ? ExternalLlmRefusal.NothingToAnswer
                : ExternalLlmRefusal.None;

            int asked = facts.AskedMaxOutputTokens;
            int max = asked <= 0 ? DefaultMaxOutputTokens : asked > CeilingMaxOutputTokens ? CeilingMaxOutputTokens : asked;

            return new ExternalLlmRuling {Refusal = refusal, MaxOutputTokens = max};
        }

        /// <summary>The reason a refused request reports to its caller, in plain English.</summary>
        public static string Explain(ExternalLlmRefusal refusal)
        {
            switch (refusal)
            {
                case ExternalLlmRefusal.PlayerTurnedTheDoorOff: return "The player turned off 'Other mods may use my connection' in Calradia Remembers' options.";
                case ExternalLlmRefusal.ConnectionNotReady: return "Calradia Remembers has no LLM connection ready (no API key or model set, or no campaign loaded).";
                case ExternalLlmRefusal.NoSourceMod: return "The request names no SourceMod.";
                case ExternalLlmRefusal.NothingToAnswer: return "The request carries neither a system prompt nor a message.";
                default: return "";
            }
        }
    }
}
