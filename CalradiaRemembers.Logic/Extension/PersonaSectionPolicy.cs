// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, RFC "Headless NPC Persona Invocation API"): a diplomacy mod wants a ruler to speak to
// another ruler "using their established persona and knowledge, without initiating a player conversation", and
// "CrNpcMemory.GetMemories intentionally excludes CR-private memories. Reconstructing an NPC's full persona in an external
// mod would therefore be incomplete". Gabriel (15/11/2026): "et si on offre plutot une surface qui permet de recuperer le
// persona complet d'un NPC? Et on laisse l'autre mod gerer les appels LLM."
//
// CR already renders every section of a character's prompt for a mod (CrPrompt.StockSections, 2.8.10), but that prompt is
// written for a conversation WITH THE PLAYER: what the character owes, offers and hides from the player, CR's own verbs and
// reply format. This policy names the sections that are the character himself (who he is, how he sounds, what he wants,
// what he remembers and knows) and leaves the rest out. It is an ALLOW-list on purpose: a section added later stays out of
// a persona until someone decides it belongs there, so a new offer or a player's secret can never leak by default.
//
// If this is wrong: a secret another mod confided to the character (mod_private) reaches a third mod, a lord negotiating
// with another lord recites the player's standing or a duel offer meant for the player, or CR's [ACTION] verbs land in a
// prompt CR does not read back.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>Which sections of the character prompt make up a character's persona for another mod (CrPrompt.Persona). Pure.</summary>
    public static class PersonaSectionPolicy
    {
        /// <summary>
        ///   The sections that describe the character himself, in no particular order (the persona keeps the prompt's stock
        ///   order). Their words may still mention "the player" where the character's own memories or aims involve them.
        /// </summary>
        public static IReadOnlyCollection<string> PersonaKeys => Keys;

        private static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "world_description",
            "identity",
            "archetype",
            "personality",
            "npc_self_appearance",
            "authored_backstory",
            "authored_conviction",
            "mod_backstory",
            "character_voice",
            "relationships",
            "family_compact",
            "matters_most",
            "fear_and_superstition",
            "inherited_note",
            "background_context",
            "history",
            "legend",
            "own_aim",
            "volatile_knowledge",
            "day_mood"
        };

        /// <summary>
        ///   The section that holds what ANOTHER mod confided to the character as known to them alone (CrCharacterText,
        ///   Private). It is never handed to a mod, neither in a persona nor in the stock sections.
        /// </summary>
        public const string ConfidedByAnotherMod = "mod_private";

        /// <summary>
        ///   What the character's meetings with the player left (Stage 2 of the group-scene plan, 28/11/2026): it holds what
        ///   the character keeps from the player, so it is never handed to a mod either (the conservative choice, Gabriel's
        ///   to revisit). New with 2.8.22, so no bridge already shipped loses anything.
        /// </summary>
        public const string KeptFromThePlayer = "meeting_memory";

        /// <summary>
        ///   Who the character is beneath the sheet (Stage 4 of the group-scene plan, 28/11/2026): it holds a secret known to the
        ///   character alone, so it is never handed to a mod either (the conservative choice, Gabriel's to revisit). New with
        ///   2.8.22, so no bridge already shipped loses anything.
        /// </summary>
        public const string KnownOnlyToThem = "generated_depth";

        /// <summary>Whether a prompt section belongs in a character's persona.</summary>
        public static bool IsPersona(string sectionKey)
            => !string.IsNullOrWhiteSpace(sectionKey) && Keys.Contains(sectionKey.Trim());

        /// <summary>
        ///   Whether a section's text may be handed to a mod at all (every section but <see cref="ConfidedByAnotherMod" />,
        ///   <see cref="KeptFromThePlayer" /> and <see cref="KnownOnlyToThem" />).
        /// </summary>
        public static bool MayLeaveCr(string sectionKey)
            => !string.Equals(sectionKey?.Trim(), ConfidedByAnotherMod, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(sectionKey?.Trim(), KeptFromThePlayer, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(sectionKey?.Trim(), KnownOnlyToThem, StringComparison.OrdinalIgnoreCase);
    }
}
