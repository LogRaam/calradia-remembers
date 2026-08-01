// Code written by Gabriel Mailhot, 31/07/2026.
// The save-type BASE ID for CalradiaRemembers' quest definer. It shipped at 300_000_000, but Europe1100 / EOE
// 1100 (its WhileThyCome assembly) registers a SaveableTypeDefiner at the SAME base(300000000) with its first
// type at id 1, so both mods claim save-id 300000001 and whichever loads SECOND hard-crashes the game at boot
// (confirmed by decompile 2026-07-31). We cannot share the id, and we cannot change our shipped base for
// everyone without moving the ids baked into existing CR-quest saves. So the base is chosen at boot: keep
// 300_000_000 when no known collider is loaded (existing saves untouched, zero quest loss), and shift to a
// distinct base only when a collider (Europe1100) is present, so CR and EOE can coexist. The only cost is that a
// player who ADDS or REMOVES EOE mid-campaign flips the base and loses in-progress CR quests, but that action
// already desyncs CR's memory from the game, so it is an accepted edge. NPC memories/relations are unaffected
// either way (they live in a separate byte[] store, not this definer). Pure so the choice is unit-tested.

#region

using System;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides the quest definer's save-type base id, avoiding a known boot-crash collision with Europe1100.</summary>
    public static class QuestSaveBasePolicy
    {
        /// <summary>The shipped base. Kept whenever no known collider is loaded, so existing CR-quest saves load untouched.</summary>
        public const int DefaultBase = 300_000_000;

        /// <summary>A distinct base used ONLY when a known collider is present, so CR can coexist with it. Well clear of vanilla (~4.14M), ButterLib (~222.44M) and the round 300M.</summary>
        public const int CoexistenceBase = 1_734_920_641;

        // Assembly names known to register a SaveableTypeDefiner that collides with ours at save-id 300000001.
        // Europe1100 / EOE 1100 ships its definer in WhileThyCome. Extend this list (with a test) if another mod
        // is ever confirmed to claim 300000001 the same way.
        private static readonly string[] KnownColliderAssemblies = {"WhileThyCome", "Europe1100"};

        /// <summary>True if <paramref name="assemblyName" /> is a known save-id collider (case-insensitive).</summary>
        public static bool IsColliderAssembly(string assemblyName)
            => !string.IsNullOrEmpty(assemblyName)
            && KnownColliderAssemblies.Any(n => string.Equals(n, assemblyName, StringComparison.OrdinalIgnoreCase));

        /// <summary>The base id to register the quest types under: the shipped one, unless a collider forces the coexistence base.</summary>
        public static int Choose(bool colliderPresent) => colliderPresent ? CoexistenceBase : DefaultBase;
    }
}
