// Code written by Gabriel Mailhot, 24/06/2026.
// Guards are a rule's eligibility predicates (e.g. IF_PLAYER_RULES) — AND-combined, no arbitrary boolean
// language, so the evaluator stays trivial and the modder's authoring stays simple. The MEANING of a guard
// against real game state lives in the engine; here we only AND the injected resolutions. Pure, so the
// "all must pass / empty passes" contract is pinned by tests.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Combines a rule's guards: every guard must resolve true. No guards means eligible.</summary>
    public static class GuardEvaluator
    {
        /// <summary>
        ///   True when every guard resolves true via <paramref name="resolve" /> (the engine-supplied fact
        ///   lookup that knows what each keyword means). Null or empty guards pass. Short-circuits on the
        ///   first failure.
        /// </summary>
        public static bool AllPass(IEnumerable<KeywordToken> guards, Func<KeywordToken, bool> resolve)
        {
            if (guards == null) return true;
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));

            foreach (KeywordToken guard in guards)
                if (!resolve(guard))
                    return false;

            return true;
        }
    }
}
