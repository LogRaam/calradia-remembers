// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether the "talk to the locals" idle-chat option should be offered. The mod gathers
    ///   the game facts and hands them here, so the rule — which has regressed before (the tavern
    ///   keeper and other place-bound NPCs getting hijacked) — is pure and unit-testable. The decision
    ///   lives with the data (Tell-Don't-Ask): construct with the facts, then ask
    ///   <see cref="ShouldOfferIdleChat"/>.
    /// </summary>
    public sealed class CommonerChatContext
    {
        // Only a GENUINELY anonymous townsperson/villager gets idle chat — never a functional NPC that
        // owns its own dialogue (tavern keeper, a captured bandit, a merchant, an artisan, an arena
        // master, …). Whitelisting these two occupations, rather than blacklisting roles one at a time,
        // is what stops the recurring "X got hijacked" regressions.
        private static readonly HashSet<string> GenericTownsperson =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Townsfolk", "Villager" };

        private readonly bool   _isHeroConversation;
        private readonly bool   _isCivilianSettlement;
        private readonly bool   _hasConversationPartner;
        private readonly string _partnerOccupation;
        private readonly bool   _llmAvailable;

        public CommonerChatContext(
            bool   isHeroConversation,
            bool   isCivilianSettlement,
            bool   hasConversationPartner,
            string partnerOccupation,
            bool   llmAvailable)
        {
            _isHeroConversation     = isHeroConversation;
            _isCivilianSettlement   = isCivilianSettlement;
            _hasConversationPartner = hasConversationPartner;
            _partnerOccupation      = partnerOccupation;
            _llmAvailable           = llmAvailable;
        }

        /// <summary>
        ///   True only when ALL hold: it is not a named-Hero conversation, the place is a civilian
        ///   settlement (town/village/castle, never a hideout), there is a conversation partner, that
        ///   partner is an anonymous townsperson/villager, and the AI is available.
        /// </summary>
        public bool ShouldOfferIdleChat()
            => !_isHeroConversation
               && _isCivilianSettlement
               && _hasConversationPartner
               && GenericTownsperson.Contains(_partnerOccupation ?? string.Empty)
               && _llmAvailable;
    }
}
