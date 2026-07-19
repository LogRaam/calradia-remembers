// Code written by Gabriel Mailhot, 18/07/2026.
// How the world is told that the player got out of captivity. Pure: the host establishes WHO (if anyone)
// broke the captors, this decides what the chronicle says about it.
//
// The fault it fixes (Gabriel, in game, 2026-07-18). A lord chased the bandits holding the player, beat
// them, and set her free by doing so. She thanked him; he denied it. He was not hallucinating, he was
// reading the chronicle, which said she "won free of captivity" with no other actor recorded. The feeder
// received the engine's EndCaptivityDetail and discarded it, so an escape, a ransom and a rescue were all
// written the same way: the player freed themself. Every lord in Calradia was being told that.
//
// So the liberator is NAMED, and the wording states only the DEED. Not the motive: the engine cannot know
// whether he rode out for her or merely happened upon the band, and the transcript shows the guess would
// have been wrong (he had ridden out to look for her). Give the persona the fact and it supplies the
// meaning, which is exactly what that lord did with the half of the story he had.

namespace CalradiaRemembers.Logic
{
    /// <summary>Composes the chronicle wording for the end of the player's captivity.</summary>
    public static class CaptivityReleaseNarrator
    {
        /// <summary>
        ///   True when this battle's winner may be credited with freeing the player.
        /// </summary>
        /// <param name="captorWasEngaged">
        ///   Somebody fell upon the party holding the player. NOT "the captor lost": the engine hands the
        ///   player a chance to slip away in the confusion of a fight their captors are merely IN, and reports
        ///   that as an ESCAPE. The attacker still caused it, whichever way the battle then went.
        /// </param>
        /// <param name="liberatorKnown">A leader could be resolved for the attacking side.</param>
        /// <param name="liberatorIsCaptor">
        ///   Guard against crediting the gaoler: a captor fighting their own battle has freed nobody, and the
        ///   player simply changed hands or stayed put.
        /// </param>
        public static bool CreditsLiberator(bool captorWasEngaged, bool liberatorKnown, bool liberatorIsCaptor)
            => captorWasEngaged && liberatorKnown && !liberatorIsCaptor;

        /// <summary>
        ///   The world-event summary. <paramref name="liberatorName" /> null or blank keeps the original
        ///   wording, which is correct for an escape or a ransom and is also the honest fallback when a
        ///   battle freed the player but no leader could be named.
        /// </summary>
        /// <param name="brokeFree">
        ///   The player got themself out during the fight rather than being cut loose by the winner. The
        ///   engine reports this as an escape, and it is what actually happens most of the time: the captors
        ///   are set upon, the guard slips, and the prisoner walks. Crediting the attacker with a clean
        ///   rescue would overstate it, and saying the player simply escaped would erase him. Both are true
        ///   and the sentence says both.
        /// </param>
        public static string Summary(string playerName, string liberatorName, bool brokeFree = false)
        {
            if (string.IsNullOrWhiteSpace(liberatorName))
                return $"{playerName} won free of captivity.";

            return brokeFree
                ? $"{playerName} slipped away in the confusion when {liberatorName} fell upon {playerName}'s captors."
                : $"{playerName} was set free when {liberatorName} broke the band holding {playerName} captive.";
        }

        /// <summary>
        ///   The chronicle line, in the register of the surrounding chronicle (lowercase, no full stop).
        /// </summary>
        /// <param name="possessivePronoun">"her" or "his", for the no-liberator wording already in use.</param>
        public static string ChronicleLine(string playerName, string possessivePronoun, string liberatorName, bool brokeFree = false)
        {
            if (string.IsNullOrWhiteSpace(liberatorName))
                return $"after captivity, {playerName} won free again, and came back to {possessivePronoun} own";

            return brokeFree
                ? $"after captivity, {playerName} broke loose in the din of {liberatorName}'s attack upon {possessivePronoun} captors"
                : $"after captivity, {playerName} went free when {liberatorName} cut down {possessivePronoun} captors";
        }
    }
}
