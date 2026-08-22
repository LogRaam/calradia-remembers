// Code written by Gabriel Mailhot, 21/08/2026.

#region

using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The exact set of view-model operations the manual action-injection menu
   ///   (<see cref="ManualActionMenuController" />) drives, so the controller depends on this seam rather than
   ///   the whole ChatViewModel, mirroring <see cref="IOfferHost" /> and <see cref="IActionConfirmationHost" />.
   ///   Extends <see cref="IActionConfirmationHost" /> so a confirmable pick (take_gold, join_party,
   ///   free_prisoner) can be handed straight to the SAME <see cref="ActionConfirmationController" /> the LLM
   ///   path uses, with no separate confirmation flow to keep in sync. ChatViewModel implements it with thin
   ///   forwards to its existing state and rendering helpers.
   /// </summary>
   internal interface IManualActionMenuHost : IActionConfirmationHost
   {
      /// <summary>The current conversation partner (the target of any manually-injected action).</summary>
      Hero Npc { get; }

      /// <summary>This NPC's stored profile, when one exists yet (mirrors NpcMemoryCampaignBehavior.Instance.DialogFlow.Profile).</summary>
      NpcProfile Profile { get; }

      /// <summary>True while the menu may be opened: a plain live conversation that has not yet ended.</summary>
      bool CanOpenActionMenu { get; }
   }
}
