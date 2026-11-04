// Code written by Gabriel Mailhot, 03/11/2026.
// The reply budget and the presence penalty a conversation turn is sent with. avalanche24 (Nexus, 03/11/2026), fine-tuning
// a local reasoning model: "No matter what setting I disable to get rid of reasoning budget limit, the model is still capped
// at 1200 token max" and "the presence penalty is killing my model's thought process prematurely". Both were fixed in code:
// 3000 tokens (1200 in Compact, for 8k windows: DuskSymphony) and a penalty of 0.5. A local server counts the thinking of a
// reasoning model against the reply budget, so 1200 cut it mid-thought. Gabriel's "go", 03/11/2026: two menu settings, the
// way Chat Temperature was given to fkasad. Auto keeps what every other player gets today.

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>Pure: the reply budget the player picked, or none when they left it on Auto.</summary>
    public static class ReplyBudgetPolicy
    {
        /// <summary>The menu's Auto: 3000 tokens, or 1200 in Compact.</summary>
        public const int Auto = 0;

        /// <summary>What a full prompt's reply has always been given.</summary>
        public const int FullDefault = 3000;

        /// <summary>The least a picked budget gives: below it even a short reply with its tags is cut.</summary>
        public const int Min = 500;

        /// <summary>The most the menu offers.</summary>
        public const int Max = 16000;

        /// <summary>The budget the player chose, held between <see cref="Min" /> and <see cref="Max" />; null on Auto.</summary>
        public static int? Chosen(int picked)
        {
            if (picked <= Auto) return null;
            if (picked < Min) return Min;

            return picked > Max ? Max : picked;
        }
    }

    /// <summary>Pure: the presence penalty a conversation turn is sent with.</summary>
    public static class PresencePenaltyPolicy
    {
        /// <summary>
        ///   What the mod has sent since July 2026. Adult-prompt audit M13: anti-repetition rested on prompt text, which a weaker
        ///   model ignores, so scenes reused the same gestures turn after turn; a sampling penalty also reaches those models.
        ///   Raised 0.3 to 0.5 on 24/07/2026. It is blunt: the tokens that recur most in roleplay are the NAMES, so higher buys
        ///   variety at the cost of drifting off the cast. A reasoning model (gpt-5*, o*) drops it with the temperature.
        /// </summary>
        public const float Default = 0.5f;

        /// <summary>None: nothing is sent.</summary>
        public const float Min = 0f;

        /// <summary>The most the menu offers: past it a model drifts off its own cast and names.</summary>
        public const float Max = 1f;

        /// <summary>The penalty sent: the player's pick, held between <see cref="Min" /> and <see cref="Max" />; the default when unreadable.</summary>
        public static float Clamp(float picked)
        {
            if (float.IsNaN(picked) || float.IsInfinity(picked)) return Default;
            if (picked < Min) return Min;

            return picked > Max ? Max : picked;
        }
    }
}
