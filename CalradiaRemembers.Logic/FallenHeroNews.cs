// Code written by Gabriel Mailhot, 05/11/2026.
// How loud the death of a lord is, and how it is told. 3chmidt (Nexus, 05/11/2026): "if a lord or king dies, like me
// killing Monchug, it barely makes waves in the world, and only few know of it... At least the people of the realm should
// know if an important person of theirs is dead." A ruler's fall weighed exactly what any lord's did (50 in battle, 65
// executed), and a death in battle never named the player, because the engine passes no killer for it. This sets a
// ruler's death and a clan head's above a lord's, so it carries across realms and for longer (RumorAwareness reads the
// magnitude), names the rank in the news, and names the player when it was his host that struck. Pure; the host reads
// the hero and the battle.

namespace CalradiaRemembers.Logic
{
    /// <summary>Who died, and how, for the news of it.</summary>
    public sealed class FallenHeroFacts
    {
        /// <summary>How they are named in the news ("Monchug of the Arkits").</summary>
        public string Name { get; init; } = "";

        /// <summary>They ruled a realm; its name when so ("the Khuzait Khanate"), blank otherwise.</summary>
        public string RuledRealm { get; init; } = "";

        /// <summary>They led a clan; its name when so ("the Arkits"), blank otherwise. Ignored for a ruler.</summary>
        public string LedClan { get; init; } = "";

        /// <summary>They were put to death by judgement, not killed in the field.</summary>
        public bool Executed { get; init; }

        /// <summary>The settlement they fell near, when one is known; blank otherwise.</summary>
        public string Near { get; init; } = "";

        /// <summary>The player's own host was the side they fought and fell against; the player's name when so, blank otherwise.</summary>
        public string FellToPlayer { get; init; } = "";

        /// <summary>Who had them executed, when it was so and known; blank otherwise.</summary>
        public string ExecutedBy { get; init; } = "";
    }

    /// <summary>The weight and the words of the news of a lord's death.</summary>
    public static class FallenHeroNews
    {
        /// <summary>A ruler's death in battle: heard by other realms for a fortnight and more, by his own for three weeks.</summary>
        public const int RulerFellMagnitude = 90;

        /// <summary>A ruler put to death: the loudest news a death can make.</summary>
        public const int RulerExecutedMagnitude = 95;

        /// <summary>The head of a clan fallen in battle: above a lord's, below a ruler's.</summary>
        public const int ClanLeaderFellMagnitude = 65;

        /// <summary>The head of a clan put to death.</summary>
        public const int ClanLeaderExecutedMagnitude = 75;

        /// <summary>Any other lord executed (unchanged from before, see <see cref="LongTermHistoryPolicy.FellInBattleMagnitude" /> for the field).</summary>
        public const int LordExecutedMagnitude = 65;

        /// <summary>How loud the news is (0..100).</summary>
        public static int Magnitude(FallenHeroFacts f)
        {
            bool ruler = !string.IsNullOrWhiteSpace(f?.RuledRealm);
            bool head = !ruler && !string.IsNullOrWhiteSpace(f?.LedClan);
            if (f?.Executed == true) return ruler ? RulerExecutedMagnitude : head ? ClanLeaderExecutedMagnitude : LordExecutedMagnitude;

            return ruler ? RulerFellMagnitude : head ? ClanLeaderFellMagnitude : LongTermHistoryPolicy.FellInBattleMagnitude;
        }

        /// <summary>
        ///   The news: "Monchug of the Arkits, ruler of the Khuzait Khanate, fell in battle against Arwa's host near Makeb."
        ///   The rank only when there is one, the player only when his host struck, the place only when known.
        /// </summary>
        public static string Summary(FallenHeroFacts f)
        {
            string who = (f?.Name ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(f?.RuledRealm)) who += $", ruler of {f.RuledRealm.Trim()},";
            else if (!string.IsNullOrWhiteSpace(f?.LedClan)) who += $", head of the {f.LedClan.Trim()} clan,";

            if (f?.Executed == true)
                return string.IsNullOrWhiteSpace(f.ExecutedBy) ? $"{who} was executed." : $"{f.ExecutedBy.Trim()} had {who} executed.";

            string against = string.IsNullOrWhiteSpace(f?.FellToPlayer) ? "" : $" against {f.FellToPlayer.Trim()}'s host";
            string near = string.IsNullOrWhiteSpace(f?.Near) ? "" : $" near {f.Near.Trim()}";

            return $"{who} fell in battle{against}{near}.";
        }
    }
}
