// Code written by Gabriel Mailhot, 16/11/2026.
// The WHERE YOU ARE note: where the conversation happens and the speaker's standing there. It was written so a lord
// visiting another lord's town does not claim the hall as his own (tester report), and it said "It is NOT your holding,
// you are here as a guest, not its lord" to anyone whose clan does not own the place. VonLemberg and his tester Goeth
// (Discord, 16/11/2026, source audit of 2.8.12): a merchant, artisan, gang leader or headman in his own home town was
// called a guest there, while the seat pack told him the town was his home; a governor of another clan's fief was a guest
// in the town he administers. Gabriel (16/11/2026): "oui vas-y pour 1, 2 et 3". Ownership, governorship, residence and a
// visit are four standings, and the note now says which one it is.

namespace CalradiaRemembers.Logic
{
    /// <summary>Where the speaker is and what ties them to the place.</summary>
    public sealed class LocationStandingFacts
    {
        /// <summary>The settlement's name. Blank: not in a settlement, and nothing is said.</summary>
        public string PlaceName { get; init; }

        /// <summary>The speaker's own clan owns the place.</summary>
        public bool OwnClanHolds { get; init; }

        /// <summary>The player, the person they are talking to, leads the clan that owns it.</summary>
        public bool PlayerHolds { get; init; }

        /// <summary>The owner's name (the owning clan's leader). Blank: ownership unknown.</summary>
        public string OwnerName { get; init; }

        /// <summary>The owning clan's name, said after the owner's.</summary>
        public string OwnerClanName { get; init; }

        /// <summary>The speaker is this place's governor: they administer it, for its owner.</summary>
        public bool Governs { get; init; }

        /// <summary>The speaker lives here: a notable whose home is this place.</summary>
        public bool LivesHere { get; init; }
    }

    /// <summary>Words the speaker's standing in the place they are in: owner, governor, resident or guest.</summary>
    public static class LocationStandingPolicy
    {
        /// <summary>The WHERE YOU ARE note, or null when the speaker is in no settlement.</summary>
        public static string Note(LocationStandingFacts f)
        {
            if (f == null || string.IsNullOrWhiteSpace(f.PlaceName)) return null;

            string place = f.PlaceName.Trim();
            if (f.OwnClanHolds) return $"You are in {place}, a holding of your own clan.";

            // The PLAYER holds it: naming the owner by name would leave the speaker unaware that the person in front
            // of them is this place's lord (player report), so it is said plainly.
            if (f.PlayerHolds)
                return f.Governs
                    ? $"You are in {place}, which you govern for the player before you: they are its lord, and you administer it in their name."
                    : $"You are in {place}, which the player before you holds: they are its lord, and you dwell here under their rule. Address them as the holder of this place, not a passing stranger.";

            if (string.IsNullOrWhiteSpace(f.OwnerName)) return $"You are in {place}.";

            string owner = f.OwnerName.Trim() + (string.IsNullOrWhiteSpace(f.OwnerClanName) ? "" : $" of clan {f.OwnerClanName.Trim()}");
            if (f.Governs) return $"You are in {place}, which belongs to {owner}. You govern it in their name: you administer it, but it is not your holding.";
            if (f.LivesHere) return $"You are in {place}, your home, which belongs to {owner}. You live here under their rule: it is not your holding, and you are no guest in it.";

            return $"You are in {place}, which belongs to {owner}. It is NOT your holding: you are here as a guest, not its lord.";
        }
    }
}
