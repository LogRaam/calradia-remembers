// Code written by Gabriel Mailhot, 17/10/2026.
// The temperature a conversation is sampled at. fkasad (Nexus, 17/10/2026) asked whether the mod tunes the temperature
// by the kind of call: it does (0.1 to 0.2 for extraction, 0.4 for summaries, 0.7 for conversation), but the
// conversation value was fixed in code. Gabriel the same day: a menu setting for it, so a player on a local model can
// fit it to that model. Varying it by relationship was declined: temperature spreads word choice, it does not make a
// lord formal or a friend talkative (the prompt does that), and a reasoning model ignores it altogether.

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>Pure: the conversation temperature the player picked, held inside a range a model stays coherent in.</summary>
    public static class ChatTemperaturePolicy
    {
        /// <summary>The value the mod has always sent for a conversation turn.</summary>
        public const float Default = 0.7f;

        /// <summary>The lowest the menu offers: near-deterministic, repetitive, still in character.</summary>
        public const float Min = 0.1f;

        /// <summary>The highest the menu offers: above it a local model starts losing its own cast and names.</summary>
        public const float Max = 1.2f;

        /// <summary>The value sent: the player's pick, held between <see cref="Min" /> and <see cref="Max" />; the default when unreadable.</summary>
        public static float Clamp(float picked)
        {
            if (float.IsNaN(picked) || float.IsInfinity(picked)) return Default;
            if (picked < Min) return Min;

            return picked > Max ? Max : picked;
        }
    }
}
