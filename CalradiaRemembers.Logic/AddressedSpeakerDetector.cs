// Code written by Gabriel Mailhot, 12/06/2026.
// Moved from CalradiaRemembers/Chat into CalradiaRemembers.Logic (03/07/2026) so it can be unit-tested
// directly, following the same extraction pattern as DialogueHeuristics/CaptiveIntentRules. No engine
// dependency, so the relocation is namespace/accessibility only; the detection rules were tightened in
// the same pass (see the class remarks below) to fix a witness-impersonation bug.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Heuristic detection of WHICH conversation participant the player is addressing in a typed
    ///   message. A bare MENTION of a name is NEVER an address by itself — "What do you make of Martira?"
    ///   or "Martira has been raiding my villages" are ABOUT Martira, not TO her, and must not steal the
    ///   turn from whoever is actually being spoken with. Only these count as a genuine address:
    ///   <list type="bullet">
    ///     <item>a leading vocative comma ("Martira, what do you think?")</item>
    ///     <item>a vocative comma just before the name ("Tell me, Martira, ..." / "What say you, Martira?")</item>
    ///     <item>the name directly followed by "you" ("Martira you saw it yourself")</item>
    ///     <item>the whole message is just the name ("Martira?" / "Martira!")</item>
    ///     <item>a stage direction that explicitly addresses them ("*turns to Martira*", "*addresses Martira*"),
    ///       never a stage direction that merely mentions them ("*glances at Martira*", "*points at Martira*")</item>
    ///   </list>
    ///   so the sticky addressee survives ordinary references to absent thirds.
    /// </summary>
    public static class AddressedSpeakerDetector
    {
        // Bannerlord name connectors and address filler — never distinctive enough to
        // address by. Mirrors the stop-word set noted for ContextualNameResolver so an
        // epithet like "Hophtalamos of the Hand" is not matched on "the"/"of"/"hand".
        private static readonly HashSet<string> AddressStopWords =
            new(StringComparer.OrdinalIgnoreCase) {
                "the", "of", "and", "al", "de", "van", "von", "ibn", "bint", "fen", "hand"
            };

        // Stage-direction address cues: a name is addressed only when one of these appears in
        // the two tokens before it ("turns TO Olaf", "speaks TO Olaf", "ADDRESSES Olaf",
        // "ASKS Olaf", "FACES Olaf"). A bare mention ("glances AT Olaf", "points AT Olaf",
        // "Olaf smiles") does not qualify.
        private static readonly HashSet<string> StageDirectionCues =
            new(StringComparer.OrdinalIgnoreCase) { "to", "addresses", "asks", "faces" };

        /// <summary>
        ///   Returns the id of the participant the player is addressing, or null when
        ///   none is explicitly addressed (caller keeps the sticky addressee).
        /// </summary>
        public static string Detect(string text, List<Participant> participants)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (participants == null || participants.Count == 0) return null;

            // 1. Stage directions (between asterisks) — strongest address signal, but only
            //    when the segment carries an explicit addressing cue (see StageDirectionCues).
            int star = text.IndexOf('*');
            while (star >= 0)
            {
                int end = text.IndexOf('*', star + 1);

                if (end < 0) break;
                string seg = text.Substring(star + 1, end - star - 1);
                string id = MatchStageDirection(seg, participants);

                if (id != null) return id;
                star = text.IndexOf('*', end + 1);
            }

            // 2. Vocative — a participant name adjacent to a genuine address cue.
            return MatchVocative(text, participants);
        }

        /// <summary>
        ///   The tokens of a name worth matching on: length >= 3 and not a connector
        ///   ("Hophtalamos of the Hand" -> {hophtalamos}).
        /// </summary>
        public static HashSet<string> DistinctiveNameTokens(string name)
        {
            var set = new HashSet<string>();
            foreach (string t in Tokenize(name))
                if (t.Length >= 3 && !AddressStopWords.Contains(t))
                    set.Add(t);

            return set;
        }

        /// <summary>One addressable participant: an id and their distinctive name tokens.</summary>
        public readonly struct Participant
        {
            public Participant(string id, HashSet<string> tokens)
            {
                Id = id;
                Tokens = tokens;
            }

            public string Id { get; }
            public HashSet<string> Tokens { get; }
        }

        #region private

        private static string BareWord(string w)
            => w.Trim(' ', '\t', '\n', '\r', ',', '.', '?', '!', '"', '\'', ':', ';', '(', ')', '*')
                .ToLowerInvariant();

        /// <summary>
        ///   Matches a name inside a stage direction, but only when it is explicitly ADDRESSED
        ///   (a cue word within the two preceding tokens) rather than merely mentioned.
        /// </summary>
        private static string MatchStageDirection(string segment, List<Participant> participants)
        {
            string[] words = SplitWords(segment);
            for (var i = 0; i < words.Length; i++)
            {
                string bare = BareWord(words[i]);

                if (bare.Length < 3) continue;

                bool cue = i >= 1 && StageDirectionCues.Contains(BareWord(words[i - 1]))
                        || i >= 2 && StageDirectionCues.Contains(BareWord(words[i - 2]));

                if (!cue) continue;

                foreach (Participant p in participants)
                    if (p.Tokens.Contains(bare))
                        return p.Id;
            }

            return null;
        }

        private static string MatchVocative(string text, List<Participant> participants)
        {
            // Single tokenization: one raw-word pass. Compare on the punctuation-stripped
            // word, but read the address cue from the SAME word's (and its neighbours')
            // trailing punctuation — so the two never desync.
            string[] words = SplitWords(text);
            bool wholeMessageIsOneWord = words.Length == 1;

            for (var i = 0; i < words.Length; i++)
            {
                string bare = BareWord(words[i]);

                if (bare.Length < 3) continue;

                bool cue =
                    words[i].EndsWith(",")                                             // "Martira, ..."
                 || i > 0 && words[i - 1].EndsWith(",")                                 // "..., Martira, ..." / "..., Martira?"
                 || i + 1 < words.Length && BareWord(words[i + 1]) == "you"             // "Martira you ..."
                 || wholeMessageIsOneWord;                                             // "Martira?" alone

                if (!cue) continue;

                foreach (Participant p in participants)
                    if (p.Tokens.Contains(bare))
                        return p.Id;
            }

            return null;
        }

        private static string[] SplitWords(string s)
            => (s ?? "").Split(new[] {' ', '\t', '\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);

        private static string[] Tokenize(string s)
            => (s ?? "")
               .ToLowerInvariant()
               .Split(new[] {' ', '\t', '\n', '\r', ',', '.', '?', '!', '"', '\'', ':', ';', '(', ')', '*'},
                  StringSplitOptions.RemoveEmptyEntries);

        #endregion
    }
}
