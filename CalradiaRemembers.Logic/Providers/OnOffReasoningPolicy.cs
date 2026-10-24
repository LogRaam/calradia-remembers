// Code written by Gabriel Mailhot, 24/10/2026.
// Some models think or do not, with nothing in between. Probed 24/10/2026 on OpenRouter, the scene opener sent to Grok 4.20
// twice at each setting: effort minimal, low and high, and a 300-token thinking budget, all thought 1,400 to 2,300 tokens
// (10 to 15 s, about $0.008 a warm reply); only "off" did not (2 s, $0.0038). The cost bench the same day: at Low, Grok
// spent 90% of what it wrote thinking, and at Off the writing held (same voice, same memory, no lecture). Gabriel
// (24/10/2026): "off par défaut". So on such a model the MCM's Minimal and Low, which ask for little thinking, send Off,
// the nearest the model can do; Medium and High still let it think. The default (Low) is therefore Off on Grok.

namespace CalradiaRemembers.Logic.Providers
{
    /// <summary>
    ///   Maps the reasoning setting onto a model that only thinks or does not: Minimal and Low become Off there.
    /// </summary>
    public static class OnOffReasoningPolicy
    {
        /// <summary>
        ///   True for a model whose reasoning has no levels, only on or off: the Grok 4 family (any provider prefix).
        /// </summary>
        public static bool ThinksOrNot(string model)
            => model != null && model.IndexOf("grok-4", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        ///   The setting to send to <paramref name="model" />: "Off" for Minimal or Low on a model that only thinks or not,
        ///   the setting unchanged everywhere else.
        /// </summary>
        public static string ForModel(string model, string setting)
        {
            if (!ThinksOrNot(model) || setting == null) return setting;

            string s = setting.Trim();

            return string.Equals(s, "Minimal", System.StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Low", System.StringComparison.OrdinalIgnoreCase)
                ? "Off"
                : setting;
        }
    }
}
