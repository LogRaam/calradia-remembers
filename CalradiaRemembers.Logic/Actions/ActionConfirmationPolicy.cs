// Code written by Gabriel Mailhot, 12/07/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Captivity;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>
   ///   How the player answered a pending in-chat confirmation prompt.
   /// </summary>
   public enum ConfirmationAnswer
   {
      /// <summary>The player agreed (explicit "y"/"yes", or just pressed Enter, since Y is the default).</summary>
      Confirm,

      /// <summary>The player refused ("n"/"no").</summary>
      Decline,

      /// <summary>
      ///   The player typed something other than a yes/no answer, which reads as moving on to a fresh
      ///   message instead of settling the prompt. The pending action is declined so it never applies
      ///   unconfirmed.
      /// </summary>
      Recompose
   }

   /// <summary>What the arrange_match confirmation names (BuildHandQuestion).</summary>
   public sealed class HandConfirmationFacts
   {
      /// <summary>The woman of the player's house whose hand is asked.</summary>
      public string BrideName { get; init; }

      /// <summary>The man who would wed her.</summary>
      public string SuitorName { get; init; }
   }

   /// <summary>What the question about an item leaving the player's hands names.</summary>
   public sealed class ItemConfirmationFacts
   {
      /// <summary>"give_item" or "sell_item".</summary>
      public string ActionType { get; init; }

      /// <summary>The real roster item that would leave (resolved, not the model's word for it).</summary>
      public string ItemName { get; init; }

      /// <summary>The other party's display name.</summary>
      public string NpcName { get; init; }

      /// <summary>The price a sale pays (ItemSalePolicy), 0 for a gift.</summary>
      public int Price { get; init; }

      /// <summary>The price was brought down to what the buyer can pay.</summary>
      public bool ClampedToPurse { get; init; }

      /// <summary>The count a tale asks of this person when the gift is its goods (ItemName then names the goods); 0 otherwise.</summary>
      public int TaleCount { get; init; }
   }

   /// <summary>What the question about an edict the player decrees names (enact_edict, Gabriel 21/10/2026).</summary>
   public sealed class EdictConfirmationFacts
   {
      /// <summary>The name the player gave it, or null.</summary>
      public string EdictName { get; init; }

      /// <summary>The catalogue's title ("Heavy levy").</summary>
      public string Title { get; init; }

      /// <summary>Where it holds ("Pravend", "all the towns of Vlandia").</summary>
      public string Where { get; init; }

      /// <summary>What it does, in words (EdictEffect.Describe).</summary>
      public string Effects { get; init; }

      /// <summary>Its term, in days.</summary>
      public int Days { get; init; }

      /// <summary>The influence it costs the player.</summary>
      public int Influence { get; init; }
   }

   /// <summary>What the question about a captive's counter_offer names.</summary>
   public sealed class OfferConfirmationFacts
   {
      /// <summary>The captive's display name.</summary>
      public string NpcName { get; init; }

      /// <summary>Her pronoun ("her"/"him", "she"/"he"): a held prisoner is not always female.</summary>
      public bool IsFemale { get; init; }

      /// <summary>The terms she struck, in the order she (or the model) named them.</summary>
      public IReadOnlyList<CaptiveTerm> Terms { get; init; } = new List<CaptiveTerm>();

      /// <summary>The ransom in denars, already brought down to what the payer can pay; 0 when ransom is no term.</summary>
      public int Ransom { get; init; }

      /// <summary>The ransom named above was brought down to what the payer can pay.</summary>
      public bool RansomClampedToPurse { get; init; }

      /// <summary>The realm she would swear her house to, with fealty among her terms.</summary>
      public string FealtyRealm { get; init; }
   }

   /// <summary>
   ///   Pure policy for the terminal-style "confirm this action?" prompt shown in chat before a
   ///   consequential LLM action (moving coin or committing to a service) is applied. Decides which
   ///   action types need confirming, reads the player's typed answer, and builds the plain-language
   ///   question shown to them.
   /// </summary>
   public static class ActionConfirmationPolicy
   {
      /// <summary>
      ///   The plain-language confirmation question shown in chat for the pending action.
      ///   <paramref name="amount" /> is the coin count (ignored for actions that carry no amount, such as
      ///   "free_prisoner"); <paramref name="npcName" /> is the NPC's display name. Case-insensitive on
      ///   <paramref name="actionType" />; anything unrecognised falls back to a generic confirmation line.
      /// </summary>
      public static string BuildQuestion(string actionType, string npcName, int amount)
      {
         if (string.Equals(actionType, "give_gold", StringComparison.OrdinalIgnoreCase)) return $"Accept {amount} coins from {npcName}?";

         if (string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase)) return $"Give {npcName} {amount} coins?";

         if (string.Equals(actionType, "join_party", StringComparison.OrdinalIgnoreCase)) return $"Let {npcName} take service in your party for {amount} coins?";

         if (string.Equals(actionType, "free_prisoner", StringComparison.OrdinalIgnoreCase)) return $"Set {npcName} free?";

         return $"Confirm this with {npcName}?";
      }

      /// <summary>
      ///   Interprets the player's answer while a confirmation is pending. Trimmed, case-insensitive:
      ///   null or blank (just pressed Enter) and "y"/"yes" both count as <see cref="ConfirmationAnswer.Confirm" />
      ///   (Y is the default); "n"/"no" is <see cref="ConfirmationAnswer.Decline" />; anything else is
      ///   <see cref="ConfirmationAnswer.Recompose" />, since the player is typing a new message instead.
      /// </summary>
      public static ConfirmationAnswer ParseAnswer(string input)
      {
         string trimmed = input == null
            ? ""
            : input.Trim();

         if (trimmed.Length == 0 || string.Equals(trimmed, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)) return ConfirmationAnswer.Confirm;

         if (string.Equals(trimmed, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)) return ConfirmationAnswer.Decline;

         return ConfirmationAnswer.Recompose;
      }

      /// <summary>
      ///   True only for the action types the PLAYER spends or commits on, so those alone are confirmed:
      ///   "take_gold" (the player pays), "join_party" (the player hires and pays), and "free_prisoner"
      ///   (the player gives up a prisoner they hold, an irreversible commitment). Case-insensitive.
      ///   False for everything else, including "give_gold" (the NPC gives the player coin, which is the
      ///   NPC's initiative, not the player's, so it is never gated), null, and empty. Consequences the
      ///   player must not be able to veto (relation changes, grudges, and so on) are never routed here.
      /// </summary>
      public static bool RequiresConfirmation(string actionType)
      {
         if (string.IsNullOrEmpty(actionType)) return false;

         return string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase) || string.Equals(actionType, "join_party", StringComparison.OrdinalIgnoreCase) || string.Equals(actionType, "free_prisoner", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "sell_item", StringComparison.OrdinalIgnoreCase) || string.Equals(actionType, "counter_offer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "enact_edict", StringComparison.OrdinalIgnoreCase);
      }

      /// <summary>
      ///   A gift written in words is confirmed; one picked from the bag is not (the player chose it item by item).
      ///   3chmidt (Nexus, 13/10/2026): "the game just takes another sword out of my inventory" - a gift resolved from a
      ///   word picks among several matching items, so the player sees which one leaves before it does.
      /// </summary>
      public static bool GiveItemNeedsConfirmation(bool pickedFromBag) => !pickedFromBag;

      /// <summary>
      ///   "Sell Crafted Two Handed Sword to Poison Menja for 9000 coins?" or "Give Crafted Two Handed Sword to Poison
      ///   Menja?": the real item and, for a sale, the real price, said as all the buyer has when it was brought down.
      /// </summary>
      public static string BuildItemQuestion(ItemConfirmationFacts f)
      {
         string item = string.IsNullOrWhiteSpace(f?.ItemName) ? "this" : f.ItemName.Trim();
         string npc = string.IsNullOrWhiteSpace(f?.NpcName) ? "them" : f.NpcName.Trim();

         if (string.Equals(f?.ActionType, "sell_item", StringComparison.OrdinalIgnoreCase))
            return $"Sell {item} to {npc} for {f.Price} coins{(f.ClampedToPurse ? " (all they have)" : "")}?";

         if (f?.TaleCount > 0) return $"Hand {f.TaleCount} {item} to {npc}, as they asked?";

         return $"Give {item} to {npc}?";
      }

      /// <summary>
      ///   "Give Ylva's hand to Erek?": the player's own yes to a match the groom's side asked of him, for a woman of his
      ///   house (tashmetu, 20/10/2026). Asked in the chat whatever the model read in his words: a daughter is never wed on
      ///   the reading of a sentence.
      /// </summary>
      public static string BuildHandQuestion(HandConfirmationFacts f)
      {
         string bride = string.IsNullOrWhiteSpace(f?.BrideName) ? "her" : f.BrideName.Trim() + "'s";
         string suitor = string.IsNullOrWhiteSpace(f?.SuitorName) ? "him" : f.SuitorName.Trim();

         return $"Give {bride} hand to {suitor}?";
      }

      /// <summary>
      ///   "Decree the Bread Edict (a grain requisition) for Pravend: loyalty -1 a day, food +4 a day, for 60 days, at 25
      ///   influence?": the player's own yes to an edict, naming what it really does, its term and its price, before the
      ///   one carrying it proclaims anything (Gabriel, 21/10/2026).
      /// </summary>
      public static string BuildEdictQuestion(EdictConfirmationFacts f)
      {
         string title = string.IsNullOrWhiteSpace(f?.Title) ? "the edict" : f.Title.Trim().ToLowerInvariant();
         string name = string.IsNullOrWhiteSpace(f?.EdictName) ? $"a {title}" : $"{f.EdictName.Trim()} (a {title})";

         return $"Decree {name} for {f?.Where ?? "your town"}: {f?.Effects}, for {f?.Days} days, at {f?.Influence} influence?";
      }

      /// <summary>
      ///   "Accept Adrichea's offer (swear her house to the Northern Empire, 3000 coins) and set her free?": the
      ///   counter_offer question names every term she struck, in the order she named them, and the ransom brought
      ///   down to what the payer can pay when it was clamped.
      /// </summary>
      public static string BuildOfferQuestion(OfferConfirmationFacts f)
      {
         string npc = string.IsNullOrWhiteSpace(f?.NpcName) ? "them" : f.NpcName.Trim();
         string possessive = f?.IsFemale == true ? "her" : "his";
         string objective = f?.IsFemale == true ? "her" : "him";

         var parts = new List<string>();
         foreach (CaptiveTerm term in f?.Terms ?? new List<CaptiveTerm>())
            switch (term)
            {
               case CaptiveTerm.Ransom:
                  parts.Add($"{f.Ransom}{(f.RansomClampedToPurse ? " coins (all they have)" : " coins")}");
                  break;
               case CaptiveTerm.Fealty:
                  parts.Add($"swear {possessive} house to {(string.IsNullOrWhiteSpace(f.FealtyRealm) ? "your realm" : f.FealtyRealm.Trim())}");
                  break;
               case CaptiveTerm.Service:
                  parts.Add("take service in your clan");
                  break;
               case CaptiveTerm.Intimacy:
                  parts.Add("a night together");
                  break;
               case CaptiveTerm.Goodwill:
                  parts.Add($"{possessive} lasting goodwill");
                  break;
            }

         string termsText = parts.Count > 0 ? string.Join(", ", parts) : $"{possessive} word alone";

         return $"Accept {npc}'s offer ({termsText}) and set {objective} free?";
      }
   }
}