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
    ///     <item>a vocative comma just before the name ("Tell me, Martira, ..." / "What say you, Martira?"),
    ///       but NOT when the word before the comma names a relationship ("your friend, Manteos" / "my
    ///       sister, Aitheen"): that is an appositive naming a third party, a mention and not an address</item>
    ///     <item>the name directly followed by "you" ("Martira you saw it yourself")</item>
    ///     <item>the whole message is just the name ("Martira?" / "Martira!")</item>
    ///     <item>a stage direction that explicitly addresses them ("*turns to Martira*", "*addresses Martira*"),
    ///       never a stage direction that merely mentions them ("*glances at Martira*", "*points at Martira*")</item>
    ///   </list>
    ///   so the sticky addressee survives ordinary references to absent thirds.
    ///   <para>
    ///     Japanese and Chinese are written without spaces (yozakura12, 24/09/2026: 「カリー、聞かせてくれるか？」 never
    ///     reached the witness カリー). For a name in those scripts, an address is the name opening the line followed by a
    ///     comma or an exclamation or question mark, or the name closing the line after a comma; a name followed by a
    ///     particle (カリーは, カリーのこと) is a mention.
    ///   </para>
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

        // Punctuation that is never part of a name, Latin and CJK (full-width forms and corner brackets included).
        private static readonly char[] Punctuation = {
            ' ', '\t', '\n', '\r', ',', '.', '?', '!', '"', '\'', ':', ';', '(', ')', '*',
            '\u3001', '\u3002', '\uFF0C', '\uFF0E', '\uFF1F', '\uFF01', '\uFF1A', '\uFF1B', '\uFF08', '\uFF09',
            '\u300C', '\u300D', '\u300E', '\u300F', '\u201C', '\u201D', '\u2018', '\u2019', '\u2026'
        };

        // A comma, Latin, ideographic or full-width: the vocative mark in every script handled here.
        private static readonly char[] Commas = {',', '\u3001', '\uFF0C'};

        // What may follow a name that opens an unspaced line for it to be an address: a comma, or a call ("カリー！").
        private static readonly char[] UnspacedVocativeMarks = {',', '\u3001', '\uFF0C', '!', '?', '\uFF01', '\uFF1F', '\u2026'};

        // What may open or close a line around the words: quotation brackets, stage asterisks, spaces.
        private static readonly char[] LineWrappers = {
            ' ', '\t', '\n', '\r', '"', '\'', '*', '(', ')', '\uFF08', '\uFF09',
            '\u300C', '\u300D', '\u300E', '\u300F', '\u201C', '\u201D', '\u2018', '\u2019'
        };

        // What may end a line after a closing name ("そうだろう、カリー？").
        private static readonly char[] SentenceEnds = {'.', '!', '?', '\u3002', '\uFF01', '\uFF1F', '\u2026'};

        // Vocative tag words: a confirmation/tag that in ordinary speech precedes a tacked-on direct
        // address, usually with the comma dropped ("isn't that right Tezzeret?", "we ride at dawn aye
        // Olaf?"). A participant name immediately after one of these reads as an address. Kept small and
        // specific so a preposition or verb of mention ("of Tezzeret", "make of Tezzeret", "seen Tezzeret")
        // never qualifies, and an ordinary reference to an absent third is not stolen.
        private static readonly HashSet<string> VocativeTagWords =
            new(StringComparer.OrdinalIgnoreCase) {
                "right", "eh", "yeah", "aye", "yes", "no", "huh", "correct", "agreed", "hey", "okay", "ok"
            };

        // Person-descriptor nouns that turn "..., Name" into an APPOSITIVE (a third-party reference)
        // rather than a vocative (a direct address): "your friend, Manteos", "my sister, Aitheen", "the
        // lord, Derthert". Here the name RENAMES the noun before the comma, so it is a mention, not an
        // address, and must not steal the turn (player report 2026-07-27). The distinction is exactly the
        // noun's meaning: "your friend, X" is an appositive, but "your judgment, X" ("I trust your
        // judgment, Martira") is a vocative with an object, so only person-describing nouns belong here.
        private static readonly HashSet<string> AppositiveDescriptorNouns =
            new(StringComparer.OrdinalIgnoreCase) {
                "friend", "friends", "sister", "brother", "father", "mother", "son", "daughter", "cousin",
                "uncle", "aunt", "nephew", "niece", "wife", "husband", "kin", "kinsman", "kinswoman",
                "sibling", "parent", "child", "children", "grandfather", "grandmother", "grandson",
                "granddaughter", "lord", "lady", "king", "queen", "prince", "princess", "companion",
                "ally", "enemy", "rival", "comrade", "master", "mistress", "servant", "man", "woman",
                "lad", "lass", "girl", "boy", "neighbor", "neighbour", "liege", "vassal", "retainer",
                "captain", "commander", "leader", "chief", "partner", "colleague", "acquaintance",
                "guest", "host", "mentor", "apprentice", "ward", "guardian", "betrothed", "lover",
                "beloved", "spouse", "heir", "cousins", "kinsfolk"
            };

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
            // 3. The same, for a name in a script written without spaces.
            return MatchVocative(text, participants) ?? MatchUnspacedVocative(text, participants);
        }

        /// <summary>
        ///   The tokens of a name worth matching on: length >= 3 and not a connector
        ///   ("Hophtalamos of the Hand" -> {hophtalamos}). A CJK name counts from two characters (エマ, 李明), and a
        ///   middle dot separates its parts (カリー・ロス -> {カリー, ロス}).
        /// </summary>
        public static HashSet<string> DistinctiveNameTokens(string name)
        {
            var set = new HashSet<string>();
            foreach (string t in Tokenize(name))
                if ((t.Length >= 3 || t.Length == 2 && IsUnspacedScript(t)) && !AddressStopWords.Contains(t))
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
            => w.Trim(Punctuation).ToLowerInvariant();

        private static bool EndsWithComma(string w) => w.Length > 0 && Array.IndexOf(Commas, w[w.Length - 1]) >= 0;

        /// <summary>Hiragana, katakana (full and half width) or CJK ideographs: scripts written without spaces.</summary>
        private static bool IsUnspacedScript(string s)
        {
            foreach (char c in s)
                if (c >= '\u3040' && c <= '\u30FF' || c >= '\u3400' && c <= '\u9FFF' || c >= '\uF900' && c <= '\uFAFF'
                    || c >= '\uFF66' && c <= '\uFF9F')
                    return true;

            return false;
        }

        /// <summary>
        ///   An address in a line with no spaces to split on: a CJK name opening the line and followed by a comma or a
        ///   call mark, or closing the line right after a comma. The longest name wins, so カリー is not read as カリ.
        /// </summary>
        private static string MatchUnspacedVocative(string text, List<Participant> participants)
        {
            string line = text.Trim(LineWrappers);
            string closing = line.TrimEnd(SentenceEnds).TrimEnd(LineWrappers).TrimEnd(SentenceEnds);
            string bestId = null;
            var bestLength = 0;

            foreach (Participant p in participants)
            foreach (string token in p.Tokens)
            {
                if (token.Length <= bestLength || !IsUnspacedScript(token)) continue;

                bool opens = line.StartsWith(token, StringComparison.Ordinal)
                             && (line.Length == token.Length || Array.IndexOf(UnspacedVocativeMarks, line[token.Length]) >= 0);
                bool closes = closing.Length > token.Length
                              && closing.EndsWith(token, StringComparison.Ordinal)
                              && Array.IndexOf(Commas, closing[closing.Length - token.Length - 1]) >= 0;

                if (!opens && !closes) continue;

                bestId = p.Id;
                bestLength = token.Length;
            }

            return bestId;
        }

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
                    EndsWithComma(words[i])                                            // "Martira, ..."
                 || i > 0 && EndsWithComma(words[i - 1])                                // "..., Martira, ..." / "..., Martira?"
                    && !AppositiveDescriptorNouns.Contains(BareWord(words[i - 1]))      // but NOT "your friend, Manteos"
                 || i > 0 && VocativeTagWords.Contains(BareWord(words[i - 1]))          // "... right Tezzeret?" (comma dropped)
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
               .Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries);

        // Punctuation, plus the middle dots that join the parts of a CJK full name.
        private static readonly char[] NameSeparators = Concat(Punctuation, '\u30FB', '\u00B7', '\uFF65');

        private static char[] Concat(char[] a, params char[] b)
        {
            var all = new char[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);

            return all;
        }

        #endregion
    }
}
