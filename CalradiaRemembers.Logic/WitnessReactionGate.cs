// Code written by Gabriel Mailhot, 18/07/2026.
// Which [WITNESS_REACTION] lines the model emitted may actually be SHOWN and, more importantly, committed to
// the transcript. Extracted from ChatViewModel.CollectWitnessReactions, where it was written straight into the
// controller and therefore had no test at all: the mod project is net472 and the test project is net10, so
// anything left in a controller is unreachable. Every rule below guards a reported fault, and one of them
// (Absent) was still reaching players through the 15C witness path when the round-table audit found it.
//
// The stake is higher than a stray line on screen. A kept reaction is written to the TRANSCRIPT, so it is
// replayed into every prompt rebuilt afterwards: an invented speaker does not merely appear once, it becomes
// part of the conversation's history and the model keeps them talking.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a witness reaction was kept or dropped.</summary>
    public enum WitnessReactionVerdict
    {
        /// <summary>Show it, attribute it, and commit it to the transcript.</summary>
        Keep,

        /// <summary>Nothing was actually said.</summary>
        DropBlank,

        /// <summary>Attributed to the NPC who is speaking: their own words are the [DIALOGUE], never a reaction.</summary>
        DropSelfAttributed,

        /// <summary>Attributed to somebody who is not in the room. A hallucination that would feed itself.</summary>
        DropAbsent,

        /// <summary>The same line this speaker was last shown saying, verbatim.</summary>
        DropRepeated
    }

    /// <summary>The verdict, plus the canonical spelling of the speaker's name when the line is kept.</summary>
    public sealed class WitnessReactionDecision
    {
        public WitnessReactionDecision(WitnessReactionVerdict verdict, string canonicalName)
        {
            Verdict = verdict;
            CanonicalName = canonicalName;
        }

        public WitnessReactionVerdict Verdict { get; }

        /// <summary>The name as the room knows it (the model's casing and spelling are not trusted).</summary>
        public string CanonicalName { get; }

        public bool IsKept => Verdict == WitnessReactionVerdict.Keep;
    }

    /// <summary>
    ///   Decides, for one emitted reaction, whether it may be voiced. The host supplies who is actually present
    ///   and what each of them was last heard saying; this decides what that means.
    /// </summary>
    public static class WitnessReactionGate
    {
        /// <summary>
        ///   The verdict for one reaction. <paramref name="presentNames" /> is the room as the mod knows it,
        ///   matched by NAME rather than by hero id on purpose: a synthetic flavour witness (a gang enforcer, an
        ///   apprentice) legitimately has no hero id but is genuinely standing there.
        /// </summary>
        /// <param name="emittedName">The speaker name the model wrote, possibly with a parenthetical role.</param>
        /// <param name="text">The line itself.</param>
        /// <param name="mainSpeakerName">The NPC whose reply this turn is; they can never be their own witness.</param>
        /// <param name="presentNames">Everyone else actually in the room.</param>
        /// <param name="lastLines">
        ///   What each speaker was last shown saying. Looked up by the CANONICAL name this method resolves,
        ///   never by the name the model wrote: the model's casing is not trustworthy, and a lookup on the raw
        ///   spelling would silently miss the stored line and let a verbatim repeat straight through.
        /// </param>
        public static WitnessReactionDecision Decide(string emittedName,
                                                    string text,
                                                    string mainSpeakerName,
                                                    IReadOnlyList<string> presentNames,
                                                    IReadOnlyDictionary<string, string> lastLines)
        {
            string stripped = DialogueHeuristics.StripWitnessRole(emittedName);

            if (string.IsNullOrWhiteSpace(text))
                return new WitnessReactionDecision(WitnessReactionVerdict.DropBlank, stripped);

            if (!string.IsNullOrEmpty(mainSpeakerName)
             && string.Equals(stripped, mainSpeakerName, StringComparison.OrdinalIgnoreCase))
                return new WitnessReactionDecision(WitnessReactionVerdict.DropSelfAttributed, stripped);

            string canonical = Canonical(stripped, presentNames);

            if (canonical == null)
                return new WitnessReactionDecision(WitnessReactionVerdict.DropAbsent, stripped);

            string previous = LastLineFor(canonical, lastLines);

            if (!string.IsNullOrEmpty(previous)
             && string.Equals(previous.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                return new WitnessReactionDecision(WitnessReactionVerdict.DropRepeated, canonical);

            return new WitnessReactionDecision(WitnessReactionVerdict.Keep, canonical);
        }

        /// <summary>This speaker's last shown line, matched case-insensitively so a stored key still answers.</summary>
        private static string LastLineFor(string canonical, IReadOnlyDictionary<string, string> lastLines)
        {
            if (lastLines == null) return null;
            if (lastLines.TryGetValue(canonical, out string exact)) return exact;

            foreach (KeyValuePair<string, string> kv in lastLines)
                if (string.Equals(kv.Key, canonical, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;

            return null;
        }

        /// <summary>The room's own spelling of <paramref name="name" />, or null when nobody there answers to it.</summary>
        private static string Canonical(string name, IReadOnlyList<string> presentNames)
        {
            if (presentNames == null) return null;

            for (var i = 0; i < presentNames.Count; i++)
                if (string.Equals(presentNames[i], name, StringComparison.OrdinalIgnoreCase))
                    return presentNames[i];

            return null;
        }
    }
}
