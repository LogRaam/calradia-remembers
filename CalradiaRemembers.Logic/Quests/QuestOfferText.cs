// Code written by Gabriel Mailhot, 11/11/2026.
// What the player reads when a character puts a task to them, before it is theirs.
//
// Schmidt (Discord, 11/11/2026): "NPC's give you quests that you didn't agree to, like when you talk about an enemy lord
// and they expect you to get them, despite having never agreed to it. Same with asking NPC's to do something but it shows
// up for you as a quest." Traced: a [QUEST] block became an Active task, journal entry and all, in the same reply where the
// character first spoke of it, before the player had answered. Gabriel (11/11/2026): "Tu peux faire 11." A task is now an
// offer the player accepts or declines; this composes what that offer says, so the player sees the deed, what it pays, how
// long it runs, and, for a bargain, that it is the price of something the player asked for.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>A task put to the player, as the offer shows it.</summary>
    public sealed class QuestOfferFacts
    {
        /// <summary>The character offering it, as the game names them.</summary>
        public string GiverName { get; init; }

        /// <summary>The deed in plain words ("ride close to Hvalvik and report what it holds").</summary>
        public string Deed { get; init; }

        /// <summary>Denars promised on completion.</summary>
        public int RewardGold { get; init; }

        /// <summary>Personal regard promised on completion.</summary>
        public int RewardRelation { get; init; }

        /// <summary>The favour granted on completion instead of coin; anything but None makes the task a bargain.</summary>
        public RewardGrant Grant { get; init; }

        /// <summary>Days the task runs; null or 0 for no deadline.</summary>
        public int? DeadlineDays { get; init; }

        /// <summary>
        ///   The realm the task means attacking although the player is at peace with it (PeaceAttackPolicy), as the game
        ///   names it; null when the task attacks no one at peace.
        /// </summary>
        public string PeacefulRealm { get; init; }

        /// <summary>True when that realm is the player's own.</summary>
        public bool PeacefulRealmIsYours { get; init; }
    }

    /// <summary>Composes the offer a character puts to the player. Pure.</summary>
    public static class QuestOfferText
    {
        /// <summary>The offer's title: a bargain is told as the price of a favour, anything else as a task asked.</summary>
        public static string Title(QuestOfferFacts facts)
            => IsBargain(facts) ? $"{facts?.GiverName} names a price" : $"{facts?.GiverName} asks a task of you";

        /// <summary>The offer's body: the deed, what it earns, how long it runs, and that the choice is the player's.</summary>
        public static string Body(QuestOfferFacts facts)
        {
            if (facts == null) return "";

            string ask = IsBargain(facts)
                ? $"In return for what you asked, {facts.GiverName} would have you {facts.Deed}."
                : $"{facts.GiverName} would have you {facts.Deed}.";

            var earns = new List<string>();
            string grant = GrantWords(facts.Grant);
            if (grant != null) earns.Add(grant);
            if (facts.RewardGold > 0) earns.Add($"{facts.RewardGold:N0} denars");
            if (facts.RewardRelation > 0) earns.Add("their regard");

            string reward = earns.Count == 0 ? " No reward is promised." : $" On completion: {string.Join(", ", earns)}.";
            string deadline = facts.DeadlineDays > 0 ? $" You would have {facts.DeadlineDays} day{(facts.DeadlineDays == 1 ? "" : "s")}." : "";

            return ask + reward + deadline + PeaceWarning(facts) + " It becomes your task only if you accept; declining breaks no word.";
        }

        // EvilPoptart1987 (Nexus, 11/11/2026): "capturing a noble you are not at war with. You cannot complete that quest
        // without starting a war." Gabriel (12/11/2026, A): the offer says it before the player accepts.
        private static string PeaceWarning(QuestOfferFacts facts)
        {
            if (string.IsNullOrWhiteSpace(facts.PeacefulRealm)) return "";

            return facts.PeacefulRealmIsYours
                ? $" {facts.PeacefulRealm} is your own realm: carrying this out means attacking your own."
                : $" You are at peace with {facts.PeacefulRealm}: carrying this out means attacking them.";
        }

        /// <summary>The line the chat shows while the offer waits for the player's answer.</summary>
        public static string Pending(QuestOfferFacts facts) => $"{facts?.GiverName} puts a task to you: {facts?.Deed}. Accept or decline it.";

        private static bool IsBargain(QuestOfferFacts facts) => facts != null && facts.Grant != RewardGrant.None;

        private static string GrantWords(RewardGrant grant)
        {
            switch (grant)
            {
                case RewardGrant.JoinParty: return "they join your party";
                case RewardGrant.GiveItem: return "the item you asked for";
                case RewardGrant.GiveTroops: return "the troops you asked for";
                case RewardGrant.MarriageConsent: return "their consent to the match";
                case RewardGrant.ReleasePrisoner: return "the prisoner's release";
                case RewardGrant.GrantFief: return "the fief they promised";
                case RewardGrant.MarriageReward: return "the marriage they promised";
                default: return null;
            }
        }
    }
}
