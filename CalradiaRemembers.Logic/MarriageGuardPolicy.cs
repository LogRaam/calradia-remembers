// Code written by Gabriel Mailhot, 17/07/2026.
// Marriage & blessing guards (romance audit M-B3/M-B4/M-B5). The chat marriage and blessing actions trusted the
// LLM to only propose them in sane circumstances, but "the prompt is advice, the bridge is law": a hallucinated
// marry could wed a captive or an enemy across an active war (vanilla forbids both at its own screen), and a
// hallucinated grant_blessing / a quest completed late could record a blessing from a dead, imprisoned, at-war,
// or openly hostile lord. These pure predicates name the live state a marriage or a blessing requires, so the
// three bridge sites re-validate one shared, tested rule instead of each guessing its own guards.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Live-state guards a marriage or a marriage-blessing must satisfy, whatever the LLM proposed.</summary>
    public static class MarriageGuardPolicy
    {
        /// <summary>
        ///   The minimum personal regard a lord must hold for the player before blessing a match into their
        ///   house is credible (romance audit M-B3). A floor, not a courtship bar: it only shuts out a lord who
        ///   holds the player in NEGATIVE regard, so an enemy at war and at -100 can no longer "bless" the union.
        /// </summary>
        public const int MinBlessingRegard = 0;

        /// <summary>
        ///   A marriage sealed through conversation (romance audit M-B5) requires that neither party is a captive
        ///   and that the two are not on opposite sides of an active war, exactly the states vanilla blocks at its
        ///   own marriage screen.
        /// </summary>
        public static bool MarriageAllowed(bool candidatePrisoner, bool playerPrisoner, bool atWarWithCandidate)
            => !candidatePrisoner && !playerPrisoner && !atWarWithCandidate;

        /// <summary>
        ///   A marriage blessing (romance audit M-B3/M-B4) requires the blesser alive, free (not a prisoner), at
        ///   peace with the player, and holding them in at least neutral regard. Re-checked both when the action
        ///   fires and when a marriage-consent quest completes, since the blesser's state may have changed since.
        /// </summary>
        public static bool BlessingAllowed(bool blesserAlive, bool blesserPrisoner, bool atWarWithBlesser, int blesserRegard)
            => blesserAlive && !blesserPrisoner && !atWarWithBlesser && blesserRegard >= MinBlessingRegard;

        /// <summary>
        ///   Whether a recorded blessing still HONOURS the family the marriage swing will land on (romance audit
        ///   M-B1). The +40 warm swing may only reward the clan that actually gave the blessing, so the clan
        ///   recorded at blessing time must still equal the spouse's current family clan. A spouse who changed
        ///   clans since the blessing (or whose blessing clan was destroyed and replaced) weds UNBLESSED, so the
        ///   swing never turns an unrelated family warm on a blessing it never gave. False when no record exists
        ///   or either clan id is missing.
        /// </summary>
        public static bool BlessingHonoursFamily(bool hasRecord, string blessedClanId, string spouseFamilyClanId)
            => hasRecord
               && !string.IsNullOrEmpty(blessedClanId)
               && !string.IsNullOrEmpty(spouseFamilyClanId)
               && blessedClanId == spouseFamilyClanId;
    }
}
