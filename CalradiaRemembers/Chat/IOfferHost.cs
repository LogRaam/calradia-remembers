// Code written by Gabriel Mailhot, 11/07/2026.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The exact set of view-model operations the offer ("give") flow (<see cref="OfferController" />) drives, so
   ///   the controller depends on this seam rather than the whole ChatViewModel, mirroring
   ///   <see cref="IRoundTableHost" /> and <see cref="IActionConfirmationHost" />. ChatViewModel implements it with
   ///   thin forwards to its existing state and rendering helpers.
   /// </summary>
   internal interface IOfferHost
   {
      /// <summary>The current conversation partner (the former ChatViewModel._npcHero), the recipient of the offer.</summary>
      Hero Npc { get; }

      /// <summary>
      ///   The current conversation partner's own profile (the speaker's, as the chat resolves it), where their bargains
      ///   and supply tasks live. Never the session-lifetime DialogFlow.Profile: that is the LAST person reached through
      ///   the game's dialogue menu, and a companion reached any other way sent the bag to the wrong ledger
      ///   (Lordfadooboo, 02/10/2026: twenty sacks of grain the companion had asked for, refused as "no task").
      /// </summary>
      NpcMemoryService.Core.Models.NpcProfile Profile { get; }

      /// <summary>True while an offer may be made: a plain live conversation that has not yet ended.</summary>
      bool CanOffer { get; }

      /// <summary>Pre-fills the chat input with the composed offer line for the player to edit and send.</summary>
      void SetInputText(string text);

      /// <summary>Adds a system notification line (a settle outcome) to the chat, colour-coded by whether it favors the player.</summary>
      void AppendSystemMessageLine(string text, bool positive);

      /// <summary>Notifies that the offer button's visibility may have changed (a settled bargain/supply drops the bag).</summary>
      void NotifyOfferItemVisibilityChanged();

      /// <summary>
      ///   Starts one NPC acceptance turn for a just-closed item bargain: the NPC accepts the goods in their own
      ///   voice (shown under their portrait, written to the story log) driven by the given synthetic stage cue.
      /// </summary>
      void BeginBargainAcceptanceTurn(string stimulus);
   }
}
