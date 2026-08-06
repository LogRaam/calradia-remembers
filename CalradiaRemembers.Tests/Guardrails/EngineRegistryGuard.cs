// Code written by Gabriel Mailhot, 05/08/2026.
// The pure decision core behind the engine-registry initialization guardrail (the whole-assembly IL scan that
// consumes it lives in EngineRegistryInitializationGuardTests). It answers ONE question, with no engine or Cecil
// dependency: given the simple names of the types a constructor references, which of them are engine registries
// or process-global singletons that are unsafe to touch at type-init / ctor time?
//
// WHY IT EXISTS: on 2026-08-05 a save-load hard crash (native 0xE0434352 / TypeInitializationException) was
// traced to CouncilWardBehavior holding a STATIC FIELD initializer that built a SkillObject[] out of
// DefaultSkills. A behavior's ctor writing a static field triggers the type initializer, which evaluated that
// array and touched the engine's DefaultSkills registry. On the SAVE-LOAD path OnGameStart adds the behavior
// BEFORE the skill registry is populated, so DefaultSkills threw inside the type initializer and the whole game
// crashed. A fresh campaign worked because the registry was already up (new-works / load-crashes), which is
// exactly why it slipped past ordinary testing. The rule this encodes: NEVER touch a Default* engine registry
// (or Campaign.Current / Game.Current / MBObjectManager.Instance) from a static initializer or a ctor; defer it
// (lazy accessor, OnGameStart, RegisterEvents, or an event handler).

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Tests.Guardrails
{
    /// <summary>
    ///   Pure, engine-free classifier for the load-order landmine described in this file's header. Kept in ONE
    ///   place so the blacklist is trivial to extend as new unsafe registries surface.
    /// </summary>
    internal static class EngineRegistryGuard
    {
        /// <summary>
        ///   Simple type names (no namespace) that must never be referenced from a `.cctor`, a static field
        ///   initializer, or a `.ctor`. Two families, matched by simple name per the guardrail's contract.
        /// </summary>
        public static readonly IReadOnlyList<string> UnsafeRegistryTypeNames = new[]
        {
            // Default* static registries: the engine fills these DURING game load. Reading any of them from a
            // static field initializer / .cctor / behavior .ctor can run BEFORE the registry is populated on the
            // save-load path and throw inside the type initializer. This is the exact CouncilWardBehavior /
            // DefaultSkills crash of 2026-08-05.
            "DefaultSkills",
            "DefaultTraits",
            "DefaultCharacterAttributes",
            "DefaultPerks",
            "DefaultItems",
            "DefaultCulture",
            "DefaultCultures",
            "DefaultPartyRoles",
            "DefaultFeats",
            // Process-global singletons reached through a static getter. Null (or only half-built) until the
            // campaign / game is up, so reading one from a ctor or a type initializer is the same class of
            // load-order landmine as the Default* registries above.
            "Campaign",        // Campaign.Current
            "Game",            // Game.Current
            "MBObjectManager"  // MBObjectManager.Instance
        };

        private static readonly HashSet<string> Set =
            new HashSet<string>(UnsafeRegistryTypeNames, StringComparer.Ordinal);

        /// <summary>
        ///   True when <paramref name="simpleTypeName"/> is a blacklisted registry / singleton. Matches by exact
        ///   simple name (ordinal): these engine type names are fixed PascalCase identifiers.
        /// </summary>
        public static bool IsUnsafeRegistry(string? simpleTypeName)
        {
            return !string.IsNullOrEmpty(simpleTypeName) && Set.Contains(simpleTypeName!);
        }

        /// <summary>
        ///   Given the declaring-type simple names a single constructor references, returns the distinct
        ///   blacklisted ones (order preserved). Empty means the constructor is clean.
        /// </summary>
        public static IReadOnlyList<string> FindReferencedUnsafeRegistries(
            IEnumerable<string?> referencedDeclaringTypeSimpleNames)
        {
            var hits = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string? name in referencedDeclaringTypeSimpleNames ?? Enumerable.Empty<string?>())
            {
                if (IsUnsafeRegistry(name) && seen.Add(name!)) hits.Add(name!);
            }

            return hits;
        }
    }
}
