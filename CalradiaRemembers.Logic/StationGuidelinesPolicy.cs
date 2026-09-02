// Code written by Gabriel Mailhot, 03/07/2026.
// Station-matched behavior guidelines (player report: a gang leader greeted the player as a lady rising
// from her throne). The lordly behavior_guidelines.txt was served to EVERY NPC; this policy maps an
// NPC's occupation to a station register file, with a conservative fallback chain, so the voice finally
// follows the speaker's station. Pure and engine-free: the engine hands in the occupation name and a
// text lookup, the policy decides which register applies.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Maps an NPC's occupation to the behavior-guidelines register that should voice them, and picks
    ///   the concrete text through a fallback chain. A null result at either step means "use the base
    ///   (lordly) guidelines", which keeps every existing save and every modder edit of
    ///   behavior_guidelines.txt working unchanged.
    /// </summary>
    public static class StationGuidelinesPolicy
    {
        /// <summary>Station key for gang leaders (and bandit heroes, whose register is the street, not the court).</summary>
        public const string GangLeaderKey = "gangleader";

        /// <summary>Station key for town and village notables: merchants, artisans, rural notables, headmen, preachers.</summary>
        public const string NotableKey = "notable";

        /// <summary>Station key for wanderers (loose free blades, potential companions not yet recruited).</summary>
        public const string WandererKey = "wanderer";

        /// <summary>Station key for a wanderer who has taken service with the player: risen from a free blade, sworn to the banner, not yet landed.</summary>
        public const string CompanionKey = "companion";

        /// <summary>
        ///   The station register for a hero, honoring the wanderer, companion, lord ladder: a wanderer who
        ///   has joined the player rises out of the free-blade register into the companion one, and a
        ///   companion granted a fief rises again into the lordly base register (a landed hero carries
        ///   themselves as a lord). The two signals are read FIRST because a recruited companion and a fresh
        ///   tavern wanderer share the same raw Occupation ("Wanderer"), so occupation alone cannot tell them
        ///   apart. With both false this is exactly the plain occupation mapping.
        /// </summary>
        public static string ResolveStationKey(string occupationName, bool isPlayerCompanion, bool holdsFief)
        {
            if (holdsFief) return null;
            if (isPlayerCompanion) return CompanionKey;

            return ResolveStationKey(occupationName);
        }

        /// <summary>
        ///   The station register for an occupation name (a Bannerlord <c>Occupation</c> enum value's
        ///   ToString), or null when the base lordly register is the right one (lords, and any occupation
        ///   this policy does not recognize — conservative by design).
        /// </summary>
        public static string ResolveStationKey(string occupationName)
        {
            switch (occupationName)
            {
                case "GangLeader":
                case "Bandit":
                    return GangLeaderKey;
                case "Merchant":
                case "Artisan":
                case "RuralNotable":
                case "Headman":
                case "Preacher":
                    return NotableKey;
                case "Wanderer":
                    return WandererKey;
                default:
                    return null;
            }
        }

        /// <summary>
        ///   The guidelines text for a station, through the fallback chain: the station's own file, then a
        ///   nearest-neighbour register (a gang leader falls to the notable file, a companion falls to the
        ///   wanderer file they rose from), then null meaning "fall back to the base guidelines". Missing or
        ///   blank files simply fall through, so shipping without a station file can never break a conversation.
        /// </summary>
        public static string SelectStationText(string stationKey, Func<string, string> textForKey)
        {
            if (string.IsNullOrEmpty(stationKey) || textForKey == null) return null;

            string text = textForKey(stationKey);
            if (!string.IsNullOrWhiteSpace(text)) return text;

            if (stationKey == GangLeaderKey)
            {
                text = textForKey(NotableKey);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            if (stationKey == CompanionKey)
            {
                text = textForKey(WandererKey);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            return null;
        }
    }
}
