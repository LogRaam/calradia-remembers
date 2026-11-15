// Code written by Gabriel Mailhot, 15/11/2026.
// One stable number from a text, for everything that is DRAWN rather than saved: a character's habit of speech, the
// gestures they love, the folk tale they tell, the first impression they give, the letter a lord starts with. Nothing
// stores those draws: the number IS the memory, so it must be the same in every session and on every machine, which
// string.GetHashCode is not (it is randomised per process). The same loop had been written out nine times across the
// mod; the generated maps showed it when a tenth copy was about to tie two namespaces into a cycle (Gabriel,
// 15/11/2026: "fais l'utilitaire commun"). It lives in a namespace of its own that uses no other, so any namespace may
// use it without ever closing a cycle.
// NEVER change what this returns: every character in every save would draw a different voice, taste and tale.

namespace CalradiaRemembers.Logic.Hashing
{
    /// <summary>A number from a text, the same in every session and on every machine (FNV-1a, 32 bits, over its characters).</summary>
    public static class StableHash
    {
        /// <summary>The number of this text; null reads as the empty text.</summary>
        public static uint Of(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text ?? "")
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return hash;
            }
        }
    }
}
