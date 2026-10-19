// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment: the sentences a battle leaves behind, now naming what actually happened instead of the
// single generic "Fought at your side when we broke the enemy" (BattleMemoryText, which this sits beside and does
// not replace: that line still frames the engagement, these add the deeds inside it).
//
// TRUTH RULE (Gabriel, ratified 2026-09-06): a LORD is bested, put down, or taken alive, NEVER killed, unless the
// engine really killed them. Bannerlord knocks heroes out far more often than it kills them, so writing "you killed
// Lucon" would have NPCs mourning a man the player can visit in his own dungeon. Rank-and-file soldiers do die and
// may be spoken of plainly as fallen.


namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>The first-hand lines a battle's real deeds leave in the memory of those who were there.</summary>
    public static class BattleFeatText
    {
        /// <summary>
        ///   What the player did to one named foe, in the voice of someone who SAW it (so it lands as a first-hand
        ///   memory, the LIVED register the recent-matter approach already speaks in). <paramref name="engagement" />
        ///   is the shaped place clause from <see cref="BattleMemoryText.ChronicleEngagement" /> ("the siege of
        ///   Poros", "open battle") or empty.
        ///   <para>
        ///     tashmetu (Nexus, 13/10/2026, via cr.prompt_dump): "I saw you cut down 15 of them yourself" reads
        ///     "you" as the character these memory lines sit under, when it means the PLAYER. Naming the player
        ///     (<see cref="BattleMemoryText.NameOrPlayer" />, the same convention <see cref="WitnessedPlayerDowned" />
        ///     already follows) removes the question.
        ///   </para>
        /// </summary>
        public static string WitnessedFeat(string foeName, CombatFeatOutcome outcome, string engagement, string playerName = null)
        {
            string who = string.IsNullOrWhiteSpace(foeName) ? "a foe" : foeName.Trim();
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";
            string hero = BattleMemoryText.NameOrPlayer(playerName);

            switch (outcome)
            {
                case CombatFeatOutcome.TakenAlive:
                    return $"I saw {hero} beat {who} down and take them alive{place}.";
                case CombatFeatOutcome.Slain:
                    return $"I saw {hero} cut {who} down{place}.";
                default:
                    return $"I saw {hero} best {who} in the fighting{place}.";
            }
        }

        /// <summary>
        ///   The mirror: a foe who put the PLAYER down. Kept as its own line because it is the seed of a rivalry or
        ///   a grudge, and because a lord who unhorsed the player has something of his own to say about it.
        /// </summary>
        public static string WitnessedPlayerDowned(string foeName, string engagement, string playerName = null)
        {
            string who = string.IsNullOrWhiteSpace(foeName) ? "a foe" : foeName.Trim();
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";

            return $"I saw {who} put {BattleMemoryText.NameOrPlayer(playerName)} down in the fighting{place}.";
        }

        /// <summary>
        ///   The same deed as the FOE himself remembers it. A witness line handed to the very man it is about would
        ///   read "I saw you best Lucon" in Lucon's own memory, which is nonsense: the subject of a deed speaks of
        ///   it in his own voice, as something done TO him.
        /// </summary>
        public static string SubjectBestedByPlayer(CombatFeatOutcome outcome, string engagement, string playerName = null)
        {
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";
            string who = BattleMemoryText.NameOrPlayer(playerName);

            switch (outcome)
            {
                case CombatFeatOutcome.TakenAlive:
                    return $"{who} beat me down and took me alive{place}.";
                case CombatFeatOutcome.Slain:
                    return $"{who} cut me down{place}."; // a dead hero keeps no memory; kept for completeness
                default:
                    return $"{who} bested me in the fighting{place}.";
            }
        }

        /// <summary>The mirror, as the man who did it remembers it: he did not WATCH someone put the player down, he did it himself.</summary>
        public static string SubjectDownedThePlayer(string engagement, string playerName = null)
        {
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";

            return $"I put {BattleMemoryText.NameOrPlayer(playerName)} down myself in the fighting{place}.";
        }

        /// <summary>
        ///   The EXPLOIT as the realm hears it: third person, no witness voice, because this is the version that
        ///   circulates to lords who were never there (only ever built for
        ///   <see cref="CombatFeatTier.Exploit" />). <paramref name="playerName" /> is the player as the world
        ///   names them.
        /// </summary>
        public static string GossipedExploit(string playerName, string foeName, CombatFeatOutcome outcome, string engagement)
        {
            string hero = string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
            string who = string.IsNullOrWhiteSpace(foeName) ? "a renowned fighter" : foeName.Trim();
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";

            switch (outcome)
            {
                case CombatFeatOutcome.TakenAlive:
                    return $"They say {hero} beat {who} down and took them captive{place}.";
                case CombatFeatOutcome.Slain:
                    return $"They say {hero} cut {who} down{place}.";
                default:
                    return $"They say {hero} bested {who}{place}.";
            }
        }

        /// <summary>
        ///   A remarkable showing against the rank and file, who really do die. Only ever built when the tally is
        ///   genuinely worth remarking on (the caller decides): a plain count of ordinary kills is a statistic, not
        ///   a memory, and NPCs must not recite statistics.
        ///   <para>
        ///     tashmetu (Nexus, 13/10/2026): names the player instead of "you", same convention as
        ///     <see cref="WitnessedFeat" />. "yourself" is dropped rather than turned into a second mention of the
        ///     name: "I saw Adam cut down 15 of them" reads just as naturally.
        ///   </para>
        /// </summary>
        public static string WitnessedToll(int foesFelled, string engagement, string playerName = null)
        {
            string place = string.IsNullOrWhiteSpace(engagement) ? "" : $" at {engagement.Trim()}";
            string hero = BattleMemoryText.NameOrPlayer(playerName);

            return $"I saw {hero} cut down {foesFelled} of them{place}.";
        }
    }
}
