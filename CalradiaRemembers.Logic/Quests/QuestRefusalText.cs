// Code written by Gabriel Mailhot, 11/11/2026.
// Why a task a character offered was not recorded, said to the player and to the character.
//
// Aporo (Discord, 11/11/2026, "Quest was not started", with screenshots): Grand Prince Raganvad offered three times to
// make him a vassal and give him Mazhadan Castle for a captured lord, and three times the game answered "No task was
// recorded: what they offered could not be matched to the world. Ask them to restate it." The refusal named no reason,
// so the character restated the same castle each time and the player could not tell what to change. Gabriel
// (11/11/2026): "go". Each refusal now names its cause: the player learns what stands in the way, and the character is
// told, so it can offer the task again with a reward the game can pay.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>Why a task offer could not be recorded.</summary>
    public enum QuestRefusal
    {
        /// <summary>A target, a place or a detail the type needs could not be found in the world.</summary>
        NotPlaced,

        /// <summary>A fief was the price, and fief rewards are off in the player's settings.</summary>
        FiefRewardsOff,

        /// <summary>A fief was the price, but the giver does not lead a clan that holds it.</summary>
        FiefNotTheirs,

        /// <summary>A marriage was the price, and marriage rewards are off in the player's settings.</summary>
        MarriageRewardsOff,

        /// <summary>A hand in marriage (or a blessing for one) was the price, and it is not the giver's to give.</summary>
        HandNotTheirs,

        /// <summary>The giver's service was the price, and they are not free to take service.</summary>
        NotHireable,

        /// <summary>The favour promised (an item, troops, a prisoner) is one the game cannot pay yet.</summary>
        GrantNotPayable,

        /// <summary>The deed is aimed at a realm the player's realm is bound to by a non-aggression pact.</summary>
        PactProtects
    }

    /// <summary>A refused task offer, as the refusal names it.</summary>
    public sealed class QuestRefusalFacts
    {
        /// <summary>Why it was refused.</summary>
        public QuestRefusal Reason { get; init; }

        /// <summary>The character who offered it, as the game names them.</summary>
        public string GiverName { get; init; }

        /// <summary>The fief named as the price, as written (FiefNotTheirs), or null.</summary>
        public string Fief { get; init; }

        /// <summary>The favour promised (GrantNotPayable).</summary>
        public RewardGrant Grant { get; init; }
    }

    /// <summary>What a refused task offer says, to the player in the chat and to the character on its next turn.</summary>
    public static class QuestRefusalText
    {
        /// <summary>The line the player reads in the chat.</summary>
        public static string ForThePlayer(QuestRefusalFacts facts)
        {
            string who = string.IsNullOrWhiteSpace(facts?.GiverName) ? "they" : facts.GiverName;
            string fief = string.IsNullOrWhiteSpace(facts?.Fief) ? "a fief" : facts.Fief;

            switch (facts?.Reason ?? QuestRefusal.NotPlaced)
            {
                case QuestRefusal.FiefRewardsOff:
                    return $"No task was recorded: {who} offered a fief as its price, and fiefs are not quest rewards in your settings (\"Allow Fief And Marriage Quest Rewards\"). Ask for another reward.";
                case QuestRefusal.FiefNotTheirs:
                    return $"No task was recorded: {who} offered {fief} as its price, which is not theirs to give (only a fief their own clan holds, given by its head).";
                case QuestRefusal.MarriageRewardsOff:
                    return $"No task was recorded: {who} offered a marriage as its price, and marriages are not quest rewards in your settings (\"Allow Fief And Marriage Quest Rewards\"). Ask for another reward.";
                case QuestRefusal.HandNotTheirs:
                    return $"No task was recorded: {who} offered a hand in marriage that is not theirs to give.";
                case QuestRefusal.NotHireable:
                    return $"No task was recorded: {who} offered their service as its price, but they are not free to take service.";
                case QuestRefusal.GrantNotPayable:
                    return $"No task was recorded: {who} offered {GrantWords(facts.Grant)} as its price, which the game cannot pay yet. Ask for coin or their favour instead.";
                case QuestRefusal.PactProtects:
                    return $"No task was recorded: {who} asked a deed against a realm your realm is bound to by a non-aggression pact.";
                default:
                    return "No task was recorded: what they offered could not be matched to the world. Ask them to restate it.";
            }
        }

        /// <summary>The note the character reads on its next turn, so it offers again with what the game can pay.</summary>
        public static string ForTheCharacter(QuestRefusalFacts facts)
        {
            const string head = "Your last task offer was NOT recorded, so it is no task: ";
            string fief = string.IsNullOrWhiteSpace(facts?.Fief) ? "that fief" : facts.Fief;

            switch (facts?.Reason ?? QuestRefusal.NotPlaced)
            {
                case QuestRefusal.FiefRewardsOff:
                    return head + "a fief, a title or vassalage cannot be the price of a task. If you still want the deed, offer it again for gold, your regard or a listed favour; a fief may be a hope, never the price.";
                case QuestRefusal.FiefNotTheirs:
                    return head + $"{fief} is not yours to give. Only a town or castle your own clan holds, if you lead it, can be the price; otherwise offer gold or your regard.";
                case QuestRefusal.MarriageRewardsOff:
                    return head + "a marriage cannot be the price of a task. If you still want the deed, offer it again for gold, your regard or a listed favour.";
                case QuestRefusal.HandNotTheirs:
                    return head + "that hand in marriage is not yours to give. Offer another reward.";
                case QuestRefusal.NotHireable:
                    return head + "you are not free to take service, so your service cannot be the price. Offer another reward.";
                case QuestRefusal.GrantNotPayable:
                    return head + $"{GrantWords(facts.Grant)} cannot be paid. Offer gold or your regard instead.";
                case QuestRefusal.PactProtects:
                    return head + "its target is protected by a pact with the player's realm. Ask another deed.";
                default:
                    return head + "a target or a place it named could not be found. Name only people and places you know by their exact names, or ask another deed.";
            }
        }

        private static string GrantWords(RewardGrant grant)
        {
            switch (grant)
            {
                case RewardGrant.GiveItem: return "an item";
                case RewardGrant.GiveTroops: return "troops";
                case RewardGrant.ReleasePrisoner: return "a prisoner";
                default: return "a favour";
            }
        }
    }
}
