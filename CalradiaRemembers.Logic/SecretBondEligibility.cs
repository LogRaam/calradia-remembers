// Code written by Gabriel Mailhot, 09/09/2026.
// ONE decision about whether a character and the player may take up a hidden bond (companions only until 22/11/2026), asked by everyone who needs
// the answer instead of re-derived by each of them.
//
// Why it exists, and why sharing a constant was not enough. A player reported on 2026-09-09 that a companion
// declared their feelings while the game answered that intimacy was insufficient. The cause was one regard bar
// written as a literal 50 in three places, only one of which had moved onto the Relationship Pacing dial. Sharing
// the NUMBER fixed that symptom and left the real fault standing: three components still each carried their own
// LIST of preconditions, and those lists had already drifted apart on two conditions nobody had noticed.
//
//   the teaching gate (EncounterContextBuilder) required adult content on and refused an ended romance
//   the bridge guard (take_as_secret_lover)     checked neither
//
// So a dead romance, or a campaign with adult content switched off, would refuse to TEACH the verb and then
// execute it anyway if the model emitted it untaught. That inverts this mod's own doctrine, where the prompt
// proposes and the bridge is law: the law was the laxer of the two.
//
// Gabriel's rule, 2026-09-09: a proven regression sensitivity earns a MECHANISM, not just a test. The mechanism is
// this file. A new condition is added here once, and no caller can hold a private opinion about the rule, because
// no caller knows the rule. The verdict is named rather than boolean so the refusal a player reads can say which
// door is closed, which a shared boolean could never do.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a hidden bond may or may not be taken up, in the order the doors are tried.</summary>
    public enum SecretBondVerdict
    {
        /// <summary>Nothing stands in the way.</summary>
        Eligible,

        /// <summary>The whole romantic arc is switched off for this campaign.</summary>
        AdultContentOff,

        /// <summary>No romantic profile has ever been built for them, so there is nothing to judge.</summary>
        NoRomanticProfile,

        /// <summary>Their orientation makes the player no object of affection.</summary>
        NoSpark,

        /// <summary>They are already the player's consort, secret lover, spouse, or openly committed.</summary>
        AlreadyBound,

        /// <summary>There WAS a romance and it is over: estranged, or broken past return.</summary>
        RomanceEnded,

        /// <summary>Real, mutual, and simply not deep enough yet for this.</summary>
        BondTooShallow
    }

    /// <summary>The plain facts the decision needs, read off the live campaign by whoever is asking.</summary>
    public sealed class SecretBondFacts
    {
        public bool AdultContentEnabled { get; set; }
        public bool HasRomanticProfile { get; set; }
        public bool OrientationCompatible { get; set; }

        /// <summary>Consort, secret lover, or the player's own spouse: bound already, by any name.</summary>
        public bool AlreadyBoundToPlayer { get; set; }

        /// <summary>Their romantic standing with the player, which decides both "already bound" and "over".</summary>
        public RomanticStatus Status { get; set; }

        /// <summary>The mod's own personal regard ledger, never the vanilla clan relation.</summary>
        public int Regard { get; set; }

        /// <summary>The player's Relationship Pacing, which sets how deep the bond must run.</summary>
        public RelationWarmth Warmth { get; set; }
    }

    /// <summary>The single source of truth for whether a hidden bond may be taken up, and if not, why not.</summary>
    public static class SecretBondEligibility
    {
        /// <summary>
        ///   Tries each door in turn and names the first one closed. The ORDER is deliberate: the campaign-wide
        ///   content gate before anything personal, then who they are, then what they are to the player, and the
        ///   depth of the bond last, because "not yet" is only worth saying once everything else is in order.
        /// </summary>
        public static SecretBondVerdict Evaluate(SecretBondFacts facts)
        {
            if (facts == null) return SecretBondVerdict.NoRomanticProfile;
            if (!facts.AdultContentEnabled) return SecretBondVerdict.AdultContentOff;
            // The companions-only door is gone. Icezera (Discord 22/11/2026) asked how to keep a bond secret; Gabriel
            // (22/11/2026): "il ne devrait pas y avoir de restrictions". A lord, a lady or a notable may be one too.
            if (!facts.HasRomanticProfile) return SecretBondVerdict.NoRomanticProfile;
            if (!facts.OrientationCompatible) return SecretBondVerdict.NoSpark;

            if (facts.AlreadyBoundToPlayer
                || facts.Status == RomanticStatus.SecretLover
                || facts.Status == RomanticStatus.Committed)
                return SecretBondVerdict.AlreadyBound;

            if (facts.Status == RomanticStatus.Estranged || facts.Status == RomanticStatus.Broken)
                return SecretBondVerdict.RomanceEnded;

            return facts.Regard >= CompanionSecretAffectionPolicy.MinRegardFor(facts.Warmth)
                ? SecretBondVerdict.Eligible
                : SecretBondVerdict.BondTooShallow;
        }

        /// <summary>Convenience for a caller that only needs the yes or no.</summary>
        public static bool IsEligible(SecretBondFacts facts) => Evaluate(facts) == SecretBondVerdict.Eligible;

        /// <summary>
        ///   What to tell the player when the bond cannot be taken up, in their own words rather than ours. Empty
        ///   when it can. Naming the door is the point: "there is no such spark" and "that is over between you"
        ///   were the same sentence while this was a boolean.
        /// </summary>
        public static string Describe(SecretBondVerdict verdict, string npcName)
        {
            string who = string.IsNullOrWhiteSpace(npcName) ? "They" : npcName.Trim();

            switch (verdict)
            {
                case SecretBondVerdict.AdultContentOff:
                    return "(That is not a path this campaign allows. Raise the Adult Content Level to open it.)";
                case SecretBondVerdict.NoRomanticProfile:
                    return $"({who} has no romantic connection to build upon.)";
                case SecretBondVerdict.NoSpark:
                    return $"(There is no such spark between you and {who}.)";
                case SecretBondVerdict.AlreadyBound:
                    return $"(You and {who} are already bound.)";
                case SecretBondVerdict.RomanceEnded:
                    return $"(What there was between you and {who} is over.)";
                case SecretBondVerdict.BondTooShallow:
                    return $"({who} is not yet close enough to you for this.)";
                default:
                    return "";
            }
        }
    }
}
