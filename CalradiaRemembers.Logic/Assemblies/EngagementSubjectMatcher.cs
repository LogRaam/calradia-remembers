// Code written by Gabriel Mailhot, 31/07/2026.
// An engagement's Subject is free text read off the conversation ("Aeldric", "Aeldric and Ira", "the clan"): it
// names who the table considers bound, but never a seated hero's StringId. Sealing the decision onto the right
// hero's durable memory (Assemblies apply-at-End) needs that free text mapped back to one of the seated council
// members, so this is a pure, tolerant token match: any seated name that turns up anywhere in the subject text is
// the bound member. A collective or unresolved subject ("the clan", a name naming nobody seated) intentionally
// returns no match, so the caller can fall back to sealing the decision on the presiding anchor instead.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   Resolves an engagement's free-text <see cref="CouncilEngagement.Subject" /> to one of the seated council
    ///   members' names, so the decision can be sealed as a memory on the right hero rather than the presiding
    ///   anchor by default.
    /// </summary>
    public static class EngagementSubjectMatcher
    {
        /// <summary>
        ///   Returns the index into <paramref name="participantNames" /> of the FIRST seated name that shares a
        ///   token with <paramref name="subject" />, case- and diacritic-insensitively (folded via
        ///   <see cref="NameNormalization.Fold" />), so "Aeldric" matches both "Aeldric" and "Aeldric and Ira". A
        ///   blank subject, a null/empty roster, or a subject naming nobody seated ("the clan") all return -1;
        ///   never throws.
        /// </summary>
        public static int MatchIndex(string subject, IReadOnlyList<string> participantNames)
        {
            if (string.IsNullOrWhiteSpace(subject) || participantNames == null || participantNames.Count == 0) return -1;

            string foldedSubject = NameNormalization.Fold(subject);
            if (foldedSubject.Length == 0) return -1;

            for (int i = 0; i < participantNames.Count; i++)
            {
                string name = participantNames[i];
                if (string.IsNullOrWhiteSpace(name)) continue;

                foreach (string rawToken in name.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (rawToken.Length < 3) continue;

                    string foldedToken = NameNormalization.Fold(rawToken);
                    if (foldedToken.Length == 0) continue;

                    if (foldedSubject.Contains(foldedToken)) return i;
                }
            }

            return -1;
        }
    }
}
