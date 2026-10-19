// Code written by Gabriel Mailhot, 17/07/2026.
// Marriage & blessing guards (romance audit M-B3/M-B4/M-B5). The chat marriage and blessing actions trusted the
// LLM to only propose them in sane circumstances, but "the prompt is advice, the bridge is law": a hallucinated
// marry could wed a captive or an enemy across an active war (vanilla forbids both at its own screen), and a
// hallucinated grant_blessing / a quest completed late could record a blessing from a dead, imprisoned, at-war,
// or openly hostile lord. These pure predicates name the live state a marriage or a blessing requires, so the
// three bridge sites re-validate one shared, tested rule instead of each guessing its own guards.

#region

#endregion

namespace CalradiaRemembers.Logic.Marriage
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
        ///   The states vanilla itself blocks at its own marriage screen, asked here because the marriage is
        ///   being sealed through conversation instead. Three booleans in a row, all disqualifiers.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MarriageStateFacts
        {
           /// <summary>The intended is somebody's captive.</summary>
           public bool CandidatePrisoner { get; set; }

           /// <summary>The player is somebody's captive.</summary>
           public bool PlayerPrisoner { get; set; }

           /// <summary>The two are on opposite sides of an active war.</summary>
           public bool AtWarWithCandidate { get; set; }
        }

        /// <summary>
        ///   A marriage sealed through conversation (romance audit M-B5) requires that neither party is a captive
        ///   and that the two are not on opposite sides of an active war, exactly the states vanilla blocks at its
        ///   own marriage screen.
        /// </summary>
        public static bool MarriageAllowed(MarriageStateFacts facts)
        {
           if (facts == null) return false;

           bool candidatePrisoner = facts.CandidatePrisoner;
           bool playerPrisoner = facts.PlayerPrisoner;
           bool atWarWithCandidate = facts.AtWarWithCandidate;

           return !candidatePrisoner && !playerPrisoner && !atWarWithCandidate;
        }

        /// <summary>
        ///   What is asked of the person giving a marriage their blessing. Three booleans in a row, one of them
        ///   inverting, plus the regard. Re-checked both when the action fires and when a marriage-consent quest
        ///   completes, since the blesser's state may have changed since.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class BlesserFacts
        {
           /// <summary>They are alive to give it.</summary>
           public bool BlesserAlive { get; set; }

           /// <summary>They are held prisoner, and a captive blesses nothing.</summary>
           public bool BlesserPrisoner { get; set; }

           /// <summary>The player is at war with them.</summary>
           public bool AtWarWithBlesser { get; set; }

           /// <summary>Their regard for the player, which must be at least neutral.</summary>
           public int BlesserRegard { get; set; }
        }

        /// <summary>
        ///   A marriage blessing (romance audit M-B3/M-B4) requires the blesser alive, free (not a prisoner), at
        ///   peace with the player, and holding them in at least neutral regard. Re-checked both when the action
        ///   fires and when a marriage-consent quest completes, since the blesser's state may have changed since.
        /// </summary>
        public static bool BlessingAllowed(BlesserFacts facts)
        {
           if (facts == null) return false;

           bool blesserAlive = facts.BlesserAlive;
           bool blesserPrisoner = facts.BlesserPrisoner;
           bool atWarWithBlesser = facts.AtWarWithBlesser;
           int blesserRegard = facts.BlesserRegard;

           return blesserAlive && !blesserPrisoner && !atWarWithBlesser && blesserRegard >= MinBlessingRegard;
        }

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

        /// <summary>
        ///   Whether the spouse's family approves the match. A marriage the BASE GAME made always had the family's
        ///   consent: the clan head agreed the bargain, or the family offered the match itself. Only a marriage CR
        ///   made (a conversation, a quest reward) can bypass the family, and only there does a blessing decide it.
        ///   <para>
        ///     Gabriel, 25/09/2026, on zhenja1's report: a family that OFFERED its daughter was being affronted
        ///     (-40 and grudges) because CR held no blessing record for a match it never brokered.
        ///   </para>
        /// </summary>
        public static bool FamilyApproves(bool madeThroughCr, bool hasRecord, string blessedClanId, string spouseFamilyClanId)
            => !madeThroughCr || BlessingHonoursFamily(hasRecord, blessedClanId, spouseFamilyClanId);
    }
}
