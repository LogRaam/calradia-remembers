// Code written by Gabriel Mailhot, 31/10/2026.
// avalanche24 (Nexus, 31/10/2026), with a screenshot of the journal: "I just got this quest from an npc. What do I need
// to do here exactly?" The entry read only the giver's words ("Sargot's gang leader hoards what he learns. You'll slip
// into its shadows and return with what it hides"), as its title and as its only line: pretty, and silent on what to
// do. Gabriel the same day: "Il faudrait que les quetes soient decrites simplement pour les joueurs."
// So the game writes a short title and a "What to do" line from what the task truly is and how the game truly checks
// it (each line below follows the deed's real check, traced 31/10/2026); the giver's words stay underneath, as flavour.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>What a task's plain journal text is written from. Read off the task when it is shown.</summary>
    public sealed class QuestJournalFacts
    {
        /// <summary>The kind of deed.</summary>
        public QuestType Type { get; set; }

        /// <summary>Who set the task, as the player knows them.</summary>
        public string GiverName { get; set; }

        /// <summary>The place the deed is bound to (town to scout, village to raid, garrison to reinforce), or null.</summary>
        public string TargetSettlement { get; set; }

        /// <summary>The person the deed is about (lord to defeat, captive to free, lady to win), or null.</summary>
        public string TargetHeroName { get; set; }

        /// <summary>The realm the deed is about (parties to beat, war to declare), or null.</summary>
        public string TargetFactionName { get; set; }

        /// <summary>A bearing to a hidden lair, e.g. "north of Pravend", or null.</summary>
        public string DirectionHint { get; set; }

        /// <summary>How many goods, soldiers or recruits are asked for.</summary>
        public int RequiredCount { get; set; }

        /// <summary>The goods asked for, in player words ("horses", "forged two-handed sword"), or null.</summary>
        public string GoodsLabel { get; set; }

        /// <summary>The denar value of goods asked in payment (a wanderer's service paid in kind).</summary>
        public int RequiredItemValue { get; set; }

        /// <summary>The denars asked in all for a support demand; zero when any payment settles it.</summary>
        public int RequiredGold { get; set; }

        /// <summary>The purse the giver advanced for a ransom errand.</summary>
        public int AdvanceGold { get; set; }

        /// <summary>The people the recruits must come from ("Vlandian"), or null.</summary>
        public string CultureName { get; set; }

        /// <summary>The deadline as the game shows dates, or null when there is none.</summary>
        public string DeadlineText { get; set; }

        /// <summary>The mod that registered an outside task, or null.</summary>
        public string ExternalSourceMod { get; set; }
    }

    /// <summary>A task's plain title and "What to do" line, written by the game. Pure.</summary>
    public static class QuestJournalText
    {
        /// <summary>Opens the plain line, so a journal that already holds it is recognised (old saves get it once).</summary>
        public const string WhatToDoPrefix = "What to do: ";

        /// <summary>
        ///   A short title naming the deed and who asked, e.g. "Scout Sargot from inside (Dnin)", or null when a fact the
        ///   title needs is missing (the caller keeps the giver's words then).
        /// </summary>
        public static string Title(QuestJournalFacts f)
        {
            string deed = Deed(f);
            if (deed == null) return null;

            return string.IsNullOrWhiteSpace(f.GiverName) ? deed : $"{deed} ({f.GiverName})";
        }

        /// <summary>
        ///   The steps, in the order the player takes them, ending with how the reward comes, and the deadline when there
        ///   is one. Null when a fact the steps need is missing.
        /// </summary>
        public static string WhatToDo(QuestJournalFacts f)
        {
            string steps = Steps(f);
            if (steps == null) return null;

            // The deadline day itself still counts (QuestService expires the day after), so "by", never "before".
            string deadline = string.IsNullOrWhiteSpace(f.DeadlineText) ? "" : $" Do it by {f.DeadlineText}.";

            return WhatToDoPrefix + steps + deadline;
        }

        /// <summary>The giver's own words, set under the plain line and marked as theirs.</summary>
        public static string GiverWords(string giverName, string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return null;
            string who = string.IsNullOrWhiteSpace(giverName) ? "The giver" : giverName;

            return $"{who}'s words: \"{description.Trim()}\"";
        }

        private static string Deed(QuestJournalFacts f)
        {
            if (f == null) return null;
            string s = Clean(f.TargetSettlement), h = Clean(f.TargetHeroName), r = Clean(f.TargetFactionName);

            switch (f.Type)
            {
                case QuestType.BanditClear: return s == null ? null : $"Clear bandits near {s}";
                case QuestType.BanditHideout: return s == null ? "Clear a bandit hideout" : $"Clear the bandit hideout near {s}";
                case QuestType.AttackFaction: return r == null ? null : $"Defeat a party of {r}";
                case QuestType.AttackLord: return h == null ? null : $"Defeat {h} in battle";
                case QuestType.RaidVillage: return s == null ? null : $"Raid {s}";
                case QuestType.AttackCaravan: return r == null ? null : $"Defeat a caravan of {r}";
                case QuestType.Siege: return s == null ? null : $"Take {s} by storm";
                case QuestType.CapturePrisoner: return h == null ? null : $"Capture {h}";
                case QuestType.ExecuteEnemy: return h == null ? null : $"See {h} dead";
                case QuestType.NemesisBounty: return h == null ? null : $"Capture or kill {h}";
                case QuestType.RescuePrisoner: return h == null ? null : $"Free {h}";
                case QuestType.DeliverLetter: return h == null ? null : $"Carry a message to {h}";
                case QuestType.ProvideGold: return f.RequiredGold > 0 ? $"Pay {f.RequiredGold} denars" : "Give coin in support";
                case QuestType.ScoutArmy: return h != null ? $"Scout the army of {h}" : r != null ? $"Scout an army of {r}" : "Scout an enemy army";
                case QuestType.DeliverItems: return "Pay in goods";
                case QuestType.DeliverPrisoner: return h != null ? $"Hand over {h}" : r != null ? $"Hand over a lord of {r}" : null;
                case QuestType.DeclareWar: return r == null ? null : $"Declare war on {r}";
                case QuestType.External: return null; // the other mod's own words are the only name such a task has
                case QuestType.ProvideGoods: return f.RequiredCount <= 0 ? null : $"Bring {f.RequiredCount} {Goods(f)}";
                case QuestType.EarnRegard: return h == null ? null : $"Win the good graces of {h}";
                case QuestType.PleadCase: return h == null ? null : $"Plead the case before {h}";
                case QuestType.ReinforceGarrison: return s == null || f.RequiredCount <= 0 ? null : $"Bring {f.RequiredCount} soldiers to {s}";
                case QuestType.RansomEnvoy: return h == null ? null : $"Buy back {h}";
                case QuestType.ScoutTown: return s == null ? null : $"Scout {s} from inside";
                case QuestType.RecruitTroops: return f.RequiredCount <= 0 ? null : $"Bring {f.RequiredCount} {Recruits(f)}";
                case QuestType.CraftWeapon: return $"Forge a {Weapon(f)}";
                case QuestType.ArrangeMatch: return h == null ? null : $"Win {h} as a bride";
                default: return null;
            }
        }

        private static string Steps(QuestJournalFacts f)
        {
            if (f == null) return null;
            string s = Clean(f.TargetSettlement), h = Clean(f.TargetHeroName), r = Clean(f.TargetFactionName);
            string giver = Clean(f.GiverName) ?? "the one who asked";
            string report = $" Then talk with {giver} again to collect your reward.";
            string onTheSpot = " It is settled on the spot, with no need to come back.";
            string bag = "open the bag button (give item) in the chat";

            switch (f.Type)
            {
                case QuestType.BanditClear:
                    return s == null ? null : $"Find bandits near {s} and win a battle against them there." + report;
                case QuestType.BanditHideout:
                    string where = Clean(f.DirectionHint) ?? (s == null ? null : $"near {s}");
                    return $"Find the bandit hideout{(where == null ? "" : $" {where}")} (it is hidden until you scout close to it), attack it and win." + report;
                case QuestType.AttackFaction:
                    return r == null ? null : $"Win a battle against any party of {r}." + report;
                case QuestType.AttackLord:
                    return h == null ? null : $"Win a battle against a party led by {h}, or take {h} prisoner." + report;
                case QuestType.RaidVillage:
                    return s == null ? null : $"Go to {s} and raid it until the raid succeeds." + report;
                case QuestType.AttackCaravan:
                    return r == null ? null : $"Find a caravan of {r} on the roads, attack it and win." + report;
                case QuestType.Siege:
                    return s == null ? null : $"Take part in a successful assault on {s}." + report;
                case QuestType.CapturePrisoner:
                    return h == null ? null : $"Defeat {h} and take them prisoner yourself." + report;
                case QuestType.ExecuteEnemy:
                    return h == null ? null : $"{h} must die, by your hand or another's." + report;
                case QuestType.NemesisBounty:
                    return h == null ? null : $"Take {h} prisoner, or kill them with your own hand. If someone else kills them first, the bounty is lost." + report;
                case QuestType.RescuePrisoner:
                    return h == null ? null : $"{h} is held captive. Free them: beat the party that holds them, break them out, or take the place where they are kept." + report;
                case QuestType.DeliverLetter:
                    return h == null ? null : $"Find {h} and, when you speak with them, choose \"I bear a message for you, carried on another's behalf.\"" + report;
                case QuestType.ProvideGold:
                    string amount = f.RequiredGold > 0 ? $"{f.RequiredGold} denars in all (you may pay in several times)" : "coin";
                    return $"Talk with {giver} in the Calradia Remembers chat, {bag}, choose Gold and give {amount}." + onTheSpot;
                case QuestType.ScoutArmy:
                    string army = h != null ? $"the army led by {h}" : r != null ? $"an army of {r}" : "an enemy army";
                    return $"Find {army} on the march and ride close enough to see it (an army, not a lone lord's party)." + report;
                case QuestType.DeliverItems:
                    return $"Talk with {giver} in the Calradia Remembers chat, {bag} and offer goods worth at least {f.RequiredItemValue} denars." + onTheSpot;
                case QuestType.DeliverPrisoner:
                    string captive = h ?? (r == null ? null : $"a lord of {r}");
                    return captive == null ? null
                        : $"Take {captive} prisoner. Then talk with {giver} in the Calradia Remembers chat, {bag}, choose \"A prisoner\" and hand them over." + onTheSpot;
                case QuestType.DeclareWar:
                    return r == null ? null : $"Your own realm must declare war on {r} (being declared on does not count)." + report;
                case QuestType.External:
                    string mod = Clean(f.ExternalSourceMod);
                    return $"This task comes from {(mod == null ? "another mod" : mod)}, which decides when it is done." + report;
                case QuestType.ProvideGoods:
                    return f.RequiredCount <= 0 ? null
                        : $"Bring {f.RequiredCount} {Goods(f)} in your party. Talk with {giver} in the Calradia Remembers chat, {bag} and hand them over." + onTheSpot;
                case QuestType.EarnRegard:
                    return h == null ? null : $"Raise {h}'s regard for you: talk with them, help them, keep your word to them. It counts by itself once they think well enough of you." + report;
                case QuestType.PleadCase:
                    return h == null ? null : $"Talk with {h} in the Calradia Remembers chat, name {giver} and speak in their favour until {h} is won over." + report;
                case QuestType.ReinforceGarrison:
                    return s == null || f.RequiredCount <= 0 ? null
                        : $"Have at least {f.RequiredCount} fit soldiers in your party, go to {s} and, inside it, tell {giver} in the Calradia Remembers chat that you leave them the men." + onTheSpot;
                case QuestType.RansomEnvoy:
                    return h == null ? null
                        : $"{giver} gave you {f.AdvanceGold} denars. Buy {h} from those who hold them, then set them free or hand them to {giver}; breaking them out also counts. Keep what you do not spend." + report;
                case QuestType.ScoutTown:
                    return s == null ? null
                        : $"Get inside {s}, find its gang leader (one of the town's notables) and, in the Calradia Remembers chat, persuade them to tell you what they know of the town. They may ask a price." + report;
                case QuestType.RecruitTroops:
                    return f.RequiredCount <= 0 ? null
                        : $"Have {f.RequiredCount} {Recruits(f)} (tier 2 or lower) in your party, then tell {giver} in the Calradia Remembers chat that you hand them over." + onTheSpot;
                case QuestType.CraftWeapon:
                    return $"Forge a {Weapon(f)} yourself at a smithy (a bought one does not count). Talk with {giver} in the Calradia Remembers chat, {bag} and hand it over." + onTheSpot;
                case QuestType.ArrangeMatch:
                    return h == null ? null
                        : $"Win {h}'s heart for {giver} (plead for {giver} to her), then, in the Calradia Remembers chat, get the consent of whoever has the say over her marriage. The task is done when the two are wed." + report;
                default:
                    return null;
            }
        }

        private static string Goods(QuestJournalFacts f) => Clean(f.GoodsLabel) ?? "goods";

        private static string Recruits(QuestJournalFacts f) => Clean(f.CultureName) == null ? "fresh recruits" : $"fresh {f.CultureName} recruits";

        private static string Weapon(QuestJournalFacts f)
        {
            string label = Clean(f.GoodsLabel);
            if (label == null) return "weapon";

            return label.StartsWith("forged ") ? label.Substring("forged ".Length) : label;
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
