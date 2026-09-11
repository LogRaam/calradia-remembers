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
   ///   <see cref="ShouldOfferIdleChat" />.
   /// </summary>
   public sealed class CommonerChatContext
   {
      // Only a GENUINELY anonymous townsperson/villager gets idle chat — never a functional NPC that
      // owns its own dialogue (tavern keeper, a captured bandit, a merchant, an artisan, an arena
      // master, …). Whitelisting these two occupations, rather than blacklisting roles one at a time,
      // is what stops the recurring "X got hijacked" regressions.
      private static readonly HashSet<string> GenericTownsperson = new(StringComparer.OrdinalIgnoreCase) {"Townsfolk", "Villager"};
      public bool HasConversationPartner { get; init; }
      public bool IsCivilianSettlement { get; init; }
      public bool IsHeroConversation { get; init; }
      public bool LlmAvailable { get; init; }
      public string PartnerOccupation { get; init; }
      public bool TownsfolkQuestActive { get; init; }
      /// <summary>
      ///   True only when ALL hold: it is not a named-Hero conversation, the place is a civilian
      ///   settlement (town/village/castle, never a hideout), there is a conversation partner, that
      ///   partner is an anonymous townsperson/villager, the AI is available, and no active quest
      ///   relies on talking to townsfolk for its own clues (idle chat is withheld while such a quest
      ///   is ongoing so it does not divert vanilla quest dialogue).
      /// </summary>
      public bool ShouldOfferIdleChat()
         => !IsHeroConversation && IsCivilianSettlement && HasConversationPartner && GenericTownsperson.Contains(PartnerOccupation ?? string.Empty) && LlmAvailable && !TownsfolkQuestActive;
   }
}