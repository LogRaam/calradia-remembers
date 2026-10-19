// Code written by Gabriel Mailhot, 15/10/2026.
// Thoragoros1 (Nexus, 15/10/2026): "I've fought a few battles on my new playthrough, and none of my companions (I have
// three so far) have recorded the battles to memory. I've tried battles against named characters, bandits, and one
// battle against a patrol." By design until then: a companion only remembered the battles the chronicle records (a
// siege, armies on both sides or 150 men, and a lord leading one side), so an early game left them with nothing.
// Gabriel's go (15/10/2026): a companion remembers every fight shared with the player. The small ones FOLD into one
// running line ("Rode with X in 3 small fights of late; the last against a band of Looters, and we won it") so a
// looter-hunting campaign does not write a memory per fight into every companion's profile and the save.

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>The facts of one small fight a companion shared with the player, as its memory line reads them.</summary>
    public sealed class SkirmishFacts
    {
        /// <summary>The player's name ("the player" when the host cannot supply it).</summary>
        public string PlayerName { get; init; }

        /// <summary>Who was fought, already phrased by <see cref="SkirmishMemoryText.Foe" />.</summary>
        public string Foe { get; init; }

        /// <summary>The player's side won.</summary>
        public bool PlayerWon { get; init; }

        /// <summary>A trailing place clause from <see cref="BattleMemoryText.PlaceClause" />, or empty.</summary>
        public string PlaceClause { get; init; }
    }

    /// <summary>The other side of a small fight, as the game knows it.</summary>
    public sealed class SkirmishFoe
    {
        /// <summary>The lord leading the other side, already labelled (HeroLabel), or empty when no lord led it.</summary>
        public string LordName { get; init; }

        /// <summary>The leading party's own name ("Looters", "Sea Raiders"), or empty.</summary>
        public string PartyName { get; init; }

        /// <summary>The other side was a bandit band.</summary>
        public bool IsBandit { get; init; }

        /// <summary>The realm the other side fought for ("Vlandia"), or empty.</summary>
        public string FactionName { get; init; }
    }

    /// <summary>Pure: the folded "small fights" memory a companion keeps of the skirmishes shared with the player.</summary>
    public static class SkirmishMemoryText
    {
        /// <summary>A small fight folds into the running line when that line was last touched within this many days.</summary>
        public const int FoldWindowDays = 30;

        /// <summary>A running line stops growing at this many fights and a fresh one starts, so it keeps meaning "of late".</summary>
        public const int FoldCap = 12;

        /// <summary>Names the other side of a small fight: a lord's men, a band of bandits, or men of a realm.</summary>
        public static string Foe(SkirmishFoe foe)
        {
            if (foe == null) return "the enemy";
            if (!string.IsNullOrWhiteSpace(foe.LordName)) return $"{foe.LordName.Trim()}'s men";
            if (foe.IsBandit) return string.IsNullOrWhiteSpace(foe.PartyName) ? "a band of outlaws" : $"a band of {foe.PartyName.Trim()}";
            if (!string.IsNullOrWhiteSpace(foe.FactionName)) return $"men of {foe.FactionName.Trim()}";

            return string.IsNullOrWhiteSpace(foe.PartyName) ? "the enemy" : foe.PartyName.Trim();
        }

        /// <summary>The memory line for <paramref name="count" /> small fights, the last of them described by <paramref name="last" />.</summary>
        public static string Line(int count, SkirmishFacts last)
        {
            string who = BattleMemoryText.NameOrPlayer(last?.PlayerName);
            string foe = string.IsNullOrWhiteSpace(last?.Foe) ? "the enemy" : last.Foe;
            string place = last?.PlaceClause ?? string.Empty;
            string outcome = last?.PlayerWon == true ? "we won it" : "we lost it";

            return count <= 1
                ? $"Rode with {who} in a small fight against {foe}{place}, and {outcome}."
                : $"Rode with {who} in {count} small fights of late; the last against {foe}{place}, and {outcome}.";
        }

        /// <summary>
        ///   How many small fights a memory line already counts: 1 for a single fight, N for a folded line, 0 for any
        ///   other memory (a notable battle, a conversation), which is never folded into.
        /// </summary>
        public static int CountIn(string summary, string playerName)
        {
            if (string.IsNullOrEmpty(summary)) return 0;

            string opening = $"Rode with {BattleMemoryText.NameOrPlayer(playerName)} in ";

            if (!summary.StartsWith(opening, System.StringComparison.Ordinal)) return 0;

            string rest = summary.Substring(opening.Length);

            if (rest.StartsWith("a small fight ", System.StringComparison.Ordinal)) return 1;

            int space = rest.IndexOf(' ');

            if (space <= 0 || !int.TryParse(rest.Substring(0, space), out int n) || n < 2) return 0;

            return rest.Substring(space).StartsWith(" small fights ", System.StringComparison.Ordinal) ? n : 0;
        }

        /// <summary>
        ///   True when a new small fight folds into this existing line: it is a small-fight line, touched within
        ///   <see cref="FoldWindowDays" />, and below <see cref="FoldCap" />.
        /// </summary>
        public static bool FoldsInto(SkirmishLine existing, int today)
        {
            if (existing == null) return false;

            int count = CountIn(existing.Summary, existing.PlayerName);

            return count > 0 && count < FoldCap && today - existing.Day <= FoldWindowDays;
        }
    }

    /// <summary>A memory line already in a companion's profile, as the fold reads it.</summary>
    public sealed class SkirmishLine
    {
        /// <summary>The day the line was last written.</summary>
        public int Day { get; init; }

        /// <summary>The line itself.</summary>
        public string Summary { get; init; }

        /// <summary>The player's name, as the line would have been written with it.</summary>
        public string PlayerName { get; init; }
    }
}
