// Code written by Gabriel Mailhot, 16/11/2026.
// Aporo (15/11/2026, playing 2.8.10): sworn as a vassal in the chat (join_as_vassal), he received none of what the game's
// own oath gives: the ruler's gift of arms and of men, the ruler's goodwill and the influence of joining. The game gives
// them in LordConversationsCampaignBehavior (decompiled from 1.5.7): influence at every oath
// (GainKingdomInfluenceAction.ApplyForJoiningFaction), and once per campaign (its saved _receivedVassalRewards) the items
// screen, the troops screen and the relation with the ruler. Gabriel (16/11/2026): "oui fais les recompenses de vassal au
// oui" - at the player's yes to the oath (the Y/n confirmation of 2.8.13).
//
// This decides which of them an oath sworn through CR gives. If it is wrong: a chat oath stays poorer than the game's, or
// the gift of arms and men is handed out again at every oath, which the game never does.

namespace CalradiaRemembers.Logic
{
    /// <summary>What CR knows when the player's clan has just sworn fealty through it.</summary>
    public sealed class VassalRewardFacts
    {
        /// <summary>The game's own record that this campaign's player already received the gift of an oath (saved by the game).</summary>
        public bool AlreadyRewardedThisCampaign { get; init; }

        /// <summary>The realm has a ruler to be grateful to (the relation is with them).</summary>
        public bool RealmHasRuler { get; init; }
    }

    /// <summary>Which of the game's oath rewards an oath sworn through CR gives.</summary>
    public sealed class VassalRewardGrant
    {
        /// <summary>The influence of joining a realm: every oath, as the game gives it.</summary>
        public bool Influence { get; init; }

        /// <summary>The ruler's gift of arms and of men, shown in the game's own screens once the talk is over: once per campaign.</summary>
        public bool GiftOfArmsAndMen { get; init; }

        /// <summary>The ruler's goodwill (the game's relation reward): once per campaign, with the gift.</summary>
        public bool RulersGoodwill { get; init; }
    }

    /// <summary>Mirrors the game's own oath: influence every time, the gift and the goodwill once per campaign.</summary>
    public static class VassalRewardPolicy
    {
        /// <summary>What this oath gives.</summary>
        public static VassalRewardGrant Decide(VassalRewardFacts f)
        {
            bool firstOath = f != null && !f.AlreadyRewardedThisCampaign;

            return new VassalRewardGrant {
                Influence = f != null,
                GiftOfArmsAndMen = firstOath,
                RulersGoodwill = firstOath && f.RealmHasRuler
            };
        }
    }
}
