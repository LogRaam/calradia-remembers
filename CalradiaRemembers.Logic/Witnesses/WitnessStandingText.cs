// Code written by Gabriel Mailhot, 29/10/2026.
// Who a person in the room IS to the one speaking: their kinship, and the rank they hold in the realm. fearente (Nexus,
// 29/10/2026): a lady governor spoke to her father, the head of her clan, "like he was a servant", and a ruler was
// treated like an ordinary noble; "NPCs should know who the others are, their clan, rulers and superiors, especially
// when they are present". The witness line used to carry only how the speaker FELT about them ("your clan member",
// "a lord you know"); a companion of the player's party carried nothing at all but "the player's companion".

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Witnesses
{
    /// <summary>How a person present is kin to the one speaking, read off the engine's family links.</summary>
    public enum WitnessKin
    {
        /// <summary>No close kinship.</summary>
        None,

        /// <summary>The speaker's husband or wife.</summary>
        Spouse,

        /// <summary>The speaker's father or mother.</summary>
        Parent,

        /// <summary>The speaker's son or daughter.</summary>
        Child,

        /// <summary>The speaker's brother or sister.</summary>
        Sibling,

        /// <summary>The speaker's grandfather or grandmother.</summary>
        Grandparent,

        /// <summary>The speaker's grandson or granddaughter.</summary>
        Grandchild
    }

    /// <summary>What the speaker knows of a person in the room: kinship and rank. One fact per field.</summary>
    public sealed class WitnessStandingFacts
    {
        /// <summary>How they are kin to the speaker, if at all.</summary>
        public WitnessKin Kin { get; init; }

        /// <summary>The person present is a woman (kin words are gendered: father or mother).</summary>
        public bool IsFemale { get; init; }

        /// <summary>The realm they rule, when they are its sovereign; blank otherwise.</summary>
        public string RulesRealm { get; init; } = "";

        /// <summary>The realm they rule is the speaker's own: they are the speaker's sovereign.</summary>
        public bool RulesSpeakersRealm { get; init; }

        /// <summary>The clan they lead, when they lead one; blank otherwise.</summary>
        public string HeadsClan { get; init; } = "";

        /// <summary>The clan they lead is the speaker's own.</summary>
        public bool HeadsSpeakersClan { get; init; }

        /// <summary>The town or castle they govern; blank otherwise.</summary>
        public string Governs { get; init; } = "";
    }

    /// <summary>
    ///   The words for who a person present is to the speaker: "your father, head of your clan", "ruler of Vlandia",
    ///   "your sister, governor of Pravend". Null when they are no kin and hold no rank: the witness line then says
    ///   only what it said before.
    /// </summary>
    public static class WitnessStandingText
    {
        /// <summary>The standing clause, or null when there is nothing to say.</summary>
        public static string Describe(WitnessStandingFacts f)
        {
            if (f == null) return null;

            var parts = new List<string>();
            string kin = KinWord(f.Kin, f.IsFemale);
            if (kin != null) parts.Add($"your {kin}");

            var titles = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.RulesRealm))
                titles.Add(f.RulesSpeakersRealm ? $"ruler of {f.RulesRealm.Trim()} and your sovereign" : $"ruler of {f.RulesRealm.Trim()}");

            // A ruler leads the realm's ruling clan by definition; saying so only matters when it is the speaker's own.
            if (f.HeadsSpeakersClan)
                titles.Add("head of your clan");
            else if (!string.IsNullOrWhiteSpace(f.HeadsClan) && string.IsNullOrWhiteSpace(f.RulesRealm))
                titles.Add($"head of the {f.HeadsClan.Trim()} clan");

            if (!string.IsNullOrWhiteSpace(f.Governs)) titles.Add($"governor of {f.Governs.Trim()}");

            parts.AddRange(titles);

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        private static string KinWord(WitnessKin kin, bool female) => kin switch {
            WitnessKin.Spouse => female ? "wife" : "husband",
            WitnessKin.Parent => female ? "mother" : "father",
            WitnessKin.Child => female ? "daughter" : "son",
            WitnessKin.Sibling => female ? "sister" : "brother",
            WitnessKin.Grandparent => female ? "grandmother" : "grandfather",
            WitnessKin.Grandchild => female ? "granddaughter" : "grandson",
            _ => null
        };
    }
}
