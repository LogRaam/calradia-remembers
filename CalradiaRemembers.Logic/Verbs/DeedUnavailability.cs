// Code written by Gabriel Mailhot, 09/09/2026.
// Gabriel's ruling, 2026-09-09: "Le LLM declare, et un gating peut ensuite confirmer ou refuter. C'est la qu'il
// peut y avoir une situation frustrante pour le joueur. Il faudrait que le LLM declare en connaissant deja le
// gating."
//
// He is right, and the consequence that makes it cheap is this: a gate does not depend on what the model is about
// to say. Eligibility is a function of world state alone (regard, coin, troops, trust, cooldowns), all of it known
// BEFORE the call. Only the PARAMETER of a deed depends on the utterance: which price, which item, which person.
// So letting the model know the gating first costs no extra call and no extra latency. It is purely a matter of
// telling it.
//
// What was actually missing. A verb today has two states, not three: taught, with its full instructions, or simply
// ABSENT. To a language model, absent does not read as "forbidden", it reads as "not mentioned", so it invents the
// permission. Meanwhile every one of these verbs already knows perfectly well why it would refuse, and says so in
// a good, in-world sentence: give_influence answers "has no influence to spare you just now", lend_troops answers
// "does not yet trust you enough to risk their own men on your word". Those sentences exist only in Execute, which
// is to say only AFTER the NPC has already promised. This file moves them in front of the conversation.
//
// The mod had already solved this once, for somebody else. VerbRegistryCore.ComposeGuidance + IProvidesGuidance
// (30/08/2026) answers a modder's report in exactly these words: "an ineligible action can still tell the NPC to
// decline in-character instead of roleplaying agreement to a deed that will then fail to execute", and it runs
// regardless of eligibility. Its only implementor in the whole repository is ExternalVerbAdapter. The mechanism
// was built for third parties and never turned inward. This is the inward half, and it is deliberately a SEPARATE,
// STRUCTURED channel rather than a reuse of that one, for two reasons: a third party's guidance is free-form text
// CR cannot format or budget, and the existing channel is concatenated into ExtraActionTeachings, whose emptiness
// is what TaughtActionFacts.CourtVerbsOffered reads to mirror what was taught. Pouring refusals into that string
// would have told the action audit that the court verbs WERE taught on precisely the turns they were refused.
//
// The budget is why this is a list and not a paragraph per verb. It rides in the Full prompt only (Lean already
// drops ExtraActionTeachings deliberately), and a refusal is one line: the deed, and the one reason it is shut.
//
// WHEN a verb should speak here is the verb's own business and is documented on IStatesUnavailability: the rule is
// that a deed names its closed door only when the deed is plausibly in play, never merely because it is not
// available. Otherwise the prompt fills with the fourteen things this conversation was never about.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Verbs
{
    /// <summary>One deed that cannot be done in this conversation, and the in-world reason it cannot.</summary>
    public sealed class DeedRefusal
    {
        /// <summary>
        ///   The deed as the NPC would think of it, in the second person toward the player and WITHOUT a verb
        ///   name: "lend them soldiers", "buy the captive they hold". Never "lend_troops", which would teach the
        ///   model a token it must not emit.
        /// </summary>
        public string Deed { get; set; }

        /// <summary>
        ///   Why it is shut, in world terms, in the NPC's own frame: "your own party is under strength", "you do
        ///   not trust them enough to risk your standing". Never a number, never a threshold, never a rule name.
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    ///   Renders the deeds that are shut this turn as ONE block in ONE voice, so fourteen verbs cannot invent
    ///   fourteen ways of saying no.
    /// </summary>
    public static class DeedUnavailabilityPolicy
    {
        /// <summary>
        ///   At most this many closed doors are named. A conversation where more than a handful of deeds are
        ///   simultaneously in play and simultaneously shut does not exist; a list that long is a bug in a verb's
        ///   sense of what counts as "in play", and the cap keeps that bug out of the player's prompt budget.
        /// </summary>
        public const int MaxRefusals = 4;

        /// <summary>A reason longer than this is a paragraph, not a reason. Trimmed rather than dropped.</summary>
        public const int MaxReasonChars = 140;

        /// <summary>
        ///   The block, or empty when nothing is shut. The framing does three things the bare list cannot: it
        ///   forbids the PROMISE and not merely the deed (the whole failure being an NPC who agrees to something
        ///   that then does not happen), it forbids deferring it to later (which is the same promise wearing a
        ///   hat), and it forbids quoting the list, because a model handed a list will read it aloud.
        /// </summary>
        public static string Compose(IEnumerable<DeedRefusal> refusals)
        {
            List<DeedRefusal> kept = Clean(refusals);
            if (kept.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("WHAT YOU CANNOT DO IN THIS CONVERSATION, whatever is said and however it is asked:");

            foreach (DeedRefusal refusal in kept)
                sb.AppendLine($"- You cannot {refusal.Deed}: {refusal.Reason}.");

            sb.AppendLine("Do not agree to any of it, do not promise it, and do not offer it for later: a promise you");
            sb.AppendLine("cannot keep is the same failure as the deed itself. If the player raises one of these, say so");
            sb.AppendLine("plainly, in your own voice and in character. Never quote or list this back to them.");

            return sb.ToString();
        }

        /// <summary>
        ///   The surviving refusals, in order: blanks dropped, one entry per deed, reasons tidied to a single
        ///   sentence fragment, and never more than <see cref="MaxRefusals" />. Public because the self-test and
        ///   the log want to know WHICH doors were named, not only how the block reads.
        /// </summary>
        public static List<DeedRefusal> Clean(IEnumerable<DeedRefusal> refusals)
        {
            var kept = new List<DeedRefusal>();
            if (refusals == null) return kept;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DeedRefusal refusal in refusals)
            {
                if (kept.Count >= MaxRefusals) break;
                if (refusal == null) continue;

                string deed = Tidy(refusal.Deed, int.MaxValue);
                string reason = Tidy(refusal.Reason, MaxReasonChars);

                // Half a refusal is worse than none: "You cannot lend them soldiers: ." reads as a bug to a
                // player who ever sees it echoed, and as noise to the model.
                if (deed.Length == 0 || reason.Length == 0) continue;
                if (!seen.Add(deed)) continue;

                kept.Add(new DeedRefusal {Deed = deed, Reason = reason});
            }

            return kept;
        }

        /// <summary>Trims, collapses the trailing full stop the template supplies itself, and bounds the length.</summary>
        private static string Tidy(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string tidy = text.Trim().TrimEnd('.', ' ');
            if (tidy.Length > maxChars) tidy = tidy.Substring(0, maxChars).TrimEnd().TrimEnd('.', ',', ' ');

            return tidy;
        }
    }
}
