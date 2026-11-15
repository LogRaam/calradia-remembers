// Code written by Gabriel Mailhot, 15/09/2026.
// A character is told about their OWN people, not about all eight.
//
// The FACTION BEHAVIOR section of behavior_guidelines*.txt is an if-then list: "If you are from Vlandia:
// ... If you are from Sturgia: ...", all eight cultures, shipped in every prompt to a character whose
// culture the mod knows exactly. Seven eighths of it is addressed to somebody else.
//
// It costs tokens, but the reason to fix it is the PROSE. basildingleberry (Nexus, 14/09/2026): "many of
// them seem to talk the same way". A model handed eight personality prescriptions and told to pick reads
// all eight, and what comes back is their average - courtly and steppe and seafaring at once, which is to
// say no culture at all. Showing a Sturgian only the Sturgian paragraph is the whole change.
//
// Narrowing NEVER goes to nothing on a guess. An Imperial with no kingdom matches no single heading (the
// Empire splits Southern from Northern-or-Western by REALM, not by culture), so rather than strip his
// culture away entirely, the partial pass keeps every heading his identity touches - two, not eight. Only
// a character who matches nothing at all, which in practice means a total conversion's own culture, has
// the section dropped: a Wood Elf has no business being told about Vlandian knightly honour either.
//
// CULTURE FIRST, and the realm only when it is the character's OWN people's (tashmetu, Nexus, 27/09/2026). A
// companion's realm is the player's and a notable's is whoever holds the town, so matching on culture plus realm
// handed a Battanian companion of the "Kingdom of Rhodok" the Rhodok block too, and retitled a Vlandian notable's
// block "If you are from Kingdom of Prueba reino:". The caller now passes a realm only when its culture is the
// character's (RealmOfOwnPeople), and the culture alone is tried before the realm is consulted at all.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Faction guidelines and the culture and faction they are narrowed to.</summary>
    public sealed class GuidelinesNarrowing
    {
        /// <summary>The guidelines.</summary>
        public string Guidelines { get; init; }

        /// <summary>The culture name.</summary>
        public string CultureName { get; init; }

        /// <summary>The faction name.</summary>
        public string FactionName { get; init; }
    }

    /// <summary>A realm and whether it is of a character's own people.</summary>
    public sealed class RealmPeopleFacts
    {
        /// <summary>The realm name.</summary>
        public string RealmName { get; init; }

        /// <summary>The realm culture id.</summary>
        public string RealmCultureId { get; init; }

        /// <summary>The character culture id.</summary>
        public string CharacterCultureId { get; init; }
    }

    /// <summary>
    ///   Narrows the FACTION BEHAVIOR section of a behaviour-guidelines text to the headings that could
    ///   apply to one character. Pure and engine-free: the caller hands in the text and the character's
    ///   culture and faction names, this decides what survives.
    /// </summary>
    public static class FactionGuidelinesPolicy
    {
        /// <summary>The section this policy narrows. Text outside it is returned untouched.</summary>
        public const string SectionHeading = "=== FACTION BEHAVIOR ===";

        private const string BlockPrefix = "If you are from ";

        /// <summary>Words that carry no identity on their own, so they never make a heading match.</summary>
        private static readonly HashSet<string> Noise =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"the", "or", "of", "a", "an"};

        /// <summary>
        ///   The same guidelines with the FACTION BEHAVIOR section narrowed to this character's own
        ///   heading(s). Returned unchanged when the text has no such section (a modder may have removed
        ///   or renamed it), when it has no headings to choose between, or when the character is unknown -
        ///   every one of which keeps a hand-edited file working exactly as it did.
        /// </summary>
        /// <summary>
        ///   The realm's name when it is the character's OWN people's (its culture is theirs), otherwise null. Only such
        ///   a realm may help choose their block or retitle it: a companion serving the player, or a notable in a town
        ///   another people holds, lives in a realm that is not theirs.
        /// </summary>
        public static string RealmOfOwnPeople(RealmPeopleFacts facts) => RealmOfOwnPeople(facts.RealmName, facts.RealmCultureId, facts.CharacterCultureId);

        // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
        // way round and still compile (swap-risk ratchet, 14/11/2026).
        private static string RealmOfOwnPeople(string realmName, string realmCultureId, string characterCultureId)
        {
            if (string.IsNullOrWhiteSpace(realmName) || string.IsNullOrWhiteSpace(realmCultureId)
                || string.IsNullOrWhiteSpace(characterCultureId)) return null;

            return string.Equals(realmCultureId.Trim(), characterCultureId.Trim(), StringComparison.OrdinalIgnoreCase)
                ? realmName
                : null;
        }

        public static string NarrowToOwnCulture(GuidelinesNarrowing facts) => NarrowToOwnCulture(facts.Guidelines, facts.CultureName, facts.FactionName);

        // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
        // way round and still compile (swap-risk ratchet, 14/11/2026).
        private static string NarrowToOwnCulture(string guidelines, string cultureName, string factionName)
        {
            if (string.IsNullOrWhiteSpace(guidelines)) return guidelines;

            int sectionAt = guidelines.IndexOf(SectionHeading, StringComparison.Ordinal);
            if (sectionAt < 0) return guidelines;

            string identity = Identity(cultureName, factionName);
            if (identity.Length == 0) return guidelines;

            int sectionEnd = SectionEnd(guidelines, sectionAt + SectionHeading.Length);
            string section = guidelines.Substring(sectionAt, sectionEnd - sectionAt);

            List<string> blocks = SplitBlocks(section);
            if (blocks.Count == 0) return guidelines;

            // The culture decides first; the realm only settles what the culture alone cannot (the Empire's split).
            string culture = Identity(cultureName, null);
            List<string> kept = culture.Length > 0
                ? blocks.Where(b => Applies(b, culture, requireAll: true)).ToList()
                : new List<string>();

            if (kept.Count == 0)
                kept = blocks.Where(b => Applies(b, identity, requireAll: true)).ToList();

            // Nothing matched outright: keep whatever the identity TOUCHES rather than stripping the
            // character's culture out of the prompt. An Imperial with no realm lands here.
            if (kept.Count == 0 && culture.Length > 0)
                kept = blocks.Where(b => Applies(b, culture, requireAll: false)).ToList();

            if (kept.Count == 0)
                kept = blocks.Where(b => Applies(b, identity, requireAll: false)).ToList();

            if (kept.Count == blocks.Count) return guidelines;

            string head = guidelines.Substring(0, sectionAt);
            string tail = guidelines.Substring(sectionEnd);

            // Nothing applies at all - a total conversion's own culture. The heading goes with the blocks:
            // a section header introducing nothing is worse than no section.
            if (kept.Count == 0) return (head.TrimEnd() + Environment.NewLine + tail.TrimStart()).TrimEnd();

            var rebuilt = new StringBuilder();
            rebuilt.Append(head);
            rebuilt.AppendLine(SectionHeading);
            rebuilt.AppendLine();

            foreach (string block in kept)
            {
                rebuilt.AppendLine(RenamedForLiveRealm(block.Trim(), factionName, kept.Count));
                rebuilt.AppendLine();
            }

            return (rebuilt.ToString().TrimEnd() + Environment.NewLine + tail.TrimStart()).TrimEnd();
        }

        #region private

        private static string Identity(string cultureName, string factionName)
            => ((cultureName ?? "") + " " + (factionName ?? "")).Trim().ToLowerInvariant();

        /// <summary>Where the section stops: the next "=== " heading, or the end of the text.</summary>
        private static int SectionEnd(string text, int searchFrom)
        {
            int next = text.IndexOf("=== ", searchFrom, StringComparison.Ordinal);

            return next < 0 ? text.Length : next;
        }

        /// <summary>
        ///   The section's per-culture blocks, each running from its "If you are from X:" line to the next
        ///   one. Anything before the first heading (a modder's preamble) is not a block and is dropped with
        ///   the rest of the narrowing, which is why a section with no headings is refused above.
        /// </summary>
        private static List<string> SplitBlocks(string section)
        {
            var blocks = new List<string>();
            var current = new StringBuilder();

            foreach (string line in section.Split('\n'))
            {
                if (line.TrimStart().StartsWith(BlockPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (current.Length > 0) blocks.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0 || line.TrimStart().StartsWith(BlockPrefix, StringComparison.OrdinalIgnoreCase))
                    current.Append(line).Append('\n');
            }

            if (current.Length > 0) blocks.Add(current.ToString());

            return blocks;
        }

        /// <summary>
        ///   Whether a block's heading names this character. With <paramref name="requireAll" /> every word
        ///   of one alternative must be present ("Southern Empire" does not match a Northern lord); without
        ///   it a single word is enough ("Empire" matches both imperial headings), which is the fallback for
        ///   a character no heading names outright.
        /// </summary>
        private static bool Applies(string block, string identity, bool requireAll)
            => Alternatives(HeadingKey(block))
               .Any(words => words.Count > 0
                          && (requireAll
                                 ? words.All(w => Mentions(identity, w))
                                 : words.Any(w => Mentions(identity, w))));

        /// <summary>
        ///   Re-titles a matched block with the realm's LIVE name when that name is not the one the heading
        ///   uses. The body is untouched: this renames the character's realm, it does not rewrite what their
        ///   people are like.
        ///   <para>
        ///     Modder report (Bellum Civile bridge, 18/09/2026): Bellum renames every kingdom at runtime
        ///     ("Southern Empire" becomes "Basileia ton Notion Kalradon") and characters went on saying the
        ///     vanilla name. His diagnosis was that the prompt matched a fixed list and fell back to the live
        ///     name only for factions it did not recognise, which is right in substance: rebel kingdoms his
        ///     mod spawns at runtime were named correctly because nothing static mentions them.
        ///   </para>
        ///   <para>
        ///     There is no name lookup to invert. The vanilla names are PROSE, in behavior_guidelines.txt and
        ///     world.txt, and the heading of the block a character reads about themselves is the one place a
        ///     stale name is asserted about THEM. So that one is corrected from the live realm and the rest is
        ///     left to the precedence rule in the prompt.
        ///   </para>
        ///   Only when exactly ONE block survived: with two or more the headings are what tell them apart, and
        ///   giving both the same live name would merge two different peoples into one.
        /// </summary>
        private static string RenamedForLiveRealm(string block, string factionName, int keptCount)
        {
            string live = (factionName ?? "").Trim();

            if (keptCount != 1 || live.Length == 0 || block.Length == 0) return block;

            string firstLine = block.Split('\n')[0].TrimEnd('\r');

            if (!firstLine.TrimStart().StartsWith(BlockPrefix, StringComparison.OrdinalIgnoreCase)) return block;
            if (Mentions(live, HeadingKey(block))) return block; // the heading already names this realm

            string rest = block.Substring(Math.Min(firstLine.Length, block.Length));

            return BlockPrefix + live + ":" + rest;
        }

        /// <summary>The "X" of "If you are from X:", lowercased and stripped of its trailing colon.</summary>
        private static string HeadingKey(string block)
        {
            string firstLine = block.Split('\n')[0].Trim();
            string key = firstLine.Substring(Math.Min(BlockPrefix.Length, firstLine.Length));

            return key.TrimEnd().TrimEnd(':').Trim().ToLowerInvariant();
        }

        /// <summary>
        ///   The identities one heading stands for. "Northern or Western Empire" is two of them, and the
        ///   noun they share sits only on the last: each earlier fragment borrows the trailing words it is
        ///   missing, so "northern" becomes "northern empire". A heading without "or" is one alternative.
        /// </summary>
        private static List<List<string>> Alternatives(string key)
        {
            string[] fragments = key.Split(new[] {" or "}, StringSplitOptions.RemoveEmptyEntries);
            List<string> last = Words(fragments[fragments.Length - 1]);
            var alternatives = new List<List<string>>();

            for (var i = 0; i < fragments.Length; i++)
            {
                List<string> words = Words(fragments[i]);

                if (i < fragments.Length - 1 && words.Count < last.Count)
                    words.AddRange(last.Skip(words.Count));

                alternatives.Add(words);
            }

            return alternatives;
        }

        private static List<string> Words(string fragment)
            => fragment.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries)
                       .Select(w => w.Trim().ToLowerInvariant())
                       .Where(w => w.Length > 0 && !Noise.Contains(w))
                       .ToList();

        /// <summary>
        ///   Whether the identity names this word, allowing a plural heading to match a singular culture
        ///   ("the Nords" is the heading; the culture is "Nord").
        /// </summary>
        private static bool Mentions(string identity, string word)
        {
            if (identity.IndexOf(word, StringComparison.Ordinal) >= 0) return true;

            return word.Length > 3
                && word.EndsWith("s", StringComparison.Ordinal)
                && identity.IndexOf(word.Substring(0, word.Length - 1), StringComparison.Ordinal) >= 0;
        }

        #endregion
    }
}
