// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 4: how the realm SAYS it. The counterpart to BattleFeatText's gossiped exploit, and its dark
// mirror: the same machinery that carries word of a great deed carries word of an infamous one, so a player who
// murders their way across Calradia is met by lords who have heard of it.
//
// Two disciplines, both borrowed from the battle pillar because they earned their place there. The realm speaks as
// HEARSAY ("They say..."), never as though the listener saw it, since the recent-matter registers route a rumour
// about the player differently from something a lord lived. And the wording never claims more than happened: a
// beating is a beating, and only a real killing is spoken of as a death.

namespace CalradiaRemembers.Logic.Violence
{
    /// <summary>The realm's own words for what the player did to someone.</summary>
    public static class ViolenceRumorText
    {
        /// <summary>
        ///   Word of the deed as it travels: third person, hearsay, naming both parties.
        ///   <paramref name="playerName" /> and <paramref name="victimName" /> come through HeroLabel upstream, so
        ///   a namesake is never mistaken for another.
        /// </summary>
        public static string Gossiped(string playerName, string victimName, ViolentAct act, string place)
        {
            string who = string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
            string victim = string.IsNullOrWhiteSpace(victimName) ? "someone" : victimName.Trim();
            string where = string.IsNullOrWhiteSpace(place) ? "" : $" at {place.Trim()}";

            switch (act)
            {
                case ViolentAct.Kill:
                    return $"They say {who} murdered {victim}{where}.";
                case ViolentAct.Assault:
                    return $"They say {who} beat {victim} bloody{where}.";
                default:
                    return $"They say {who} robbed {victim} at knifepoint{where}.";
            }
        }

        /// <summary>The line for the chronicle, which records what happened rather than what people say of it.</summary>
        public static string Chronicled(string playerName, string victimName, ViolentAct act, string place)
        {
            string who = string.IsNullOrWhiteSpace(playerName) ? "The player" : playerName.Trim();
            string victim = string.IsNullOrWhiteSpace(victimName) ? "someone" : victimName.Trim();
            string where = string.IsNullOrWhiteSpace(place) ? "" : $" at {place.Trim()}";

            switch (act)
            {
                case ViolentAct.Kill:
                    return $"{who} murdered {victim}{where}.";
                case ViolentAct.Assault:
                    return $"{who} beat {victim} bloody{where}.";
                default:
                    return $"{who} robbed {victim} by force{where}.";
            }
        }

        /// <summary>What a bystander who SAW it carries away, in their own voice, so it lands as something they lived.</summary>
        public static string Witnessed(string playerName, string victimName, ViolentAct act)
        {
            string who = string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
            string victim = string.IsNullOrWhiteSpace(victimName) ? "someone" : victimName.Trim();

            switch (act)
            {
                case ViolentAct.Kill:
                    return $"I saw {who} kill {victim} with my own eyes.";
                case ViolentAct.Assault:
                    return $"I saw {who} beat {victim} to the ground.";
                default:
                    return $"I saw {who} take {victim}'s purse by force.";
            }
        }
    }
}
