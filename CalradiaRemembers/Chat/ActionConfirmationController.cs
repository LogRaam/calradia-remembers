// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Items;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Owns the in-chat action-confirmation feature so it no longer threads through the shared ChatViewModel
   ///   partials (which forced a surgical-commit dance every release), mirroring <see cref="RoundTableController" />.
   ///   A consequential LLM action (coin transfer / service commitment) is not applied on the spot: it is queued
   ///   and the player is asked to confirm IN CHAT, Y as default. This tames the LLM re-emitting the same action
   ///   every turn (the reported repeated charges), and a declined action is forgotten (never applied, and a
   ///   correction is fed back so neither the ongoing context nor the end-of-conversation memory records it). The
   ///   controller drives the view model through the <see cref="IActionConfirmationHost" /> seam; the moved bodies
   ///   are a straight lift of the former ChatViewModel members, so runtime behaviour is identical.
   /// </summary>
   internal sealed class ActionConfirmationController
   {
      private readonly Queue<PendingConfirmation> _pending = new Queue<PendingConfirmation>();

      // Idempotence: a coin/commit action already resolved this conversation (applied, declined, or a give
      // already paid) is not acted on again, so the LLM re-emitting the same transfer every turn does nothing.
      // Key is type:targetId:amount, so a genuinely different amount or NPC is still a distinct action.
      private readonly HashSet<string> _resolvedKeys = new HashSet<string>();

      private bool _awaiting;

      /// <summary>True while the chat is listening for a Y/n answer instead of sending to the LLM.</summary>
      public bool IsAwaiting => _awaiting;

      /// <summary>True while at least one action is queued and waiting for the player's confirmation.</summary>
      public bool HasPending => _pending.Count > 0;

      /// <summary>Self-test seam: the question the next confirmation would ask, or null.</summary>
      internal string DebugNextQuestion => _pending.Count > 0 ? _pending.Peek().Question : null;

      /// <summary>Self-test seam: the context the next confirmed action would run with (its pinned item), or null.</summary>
      internal ActionContext DebugNextContext => _pending.Count > 0 ? _pending.Peek().Context : null;

      /// <summary>
      ///   Preprocesses a coin/commit action before the loop would run it. An identical action already resolved
      ///   this conversation is dropped (<see cref="ActionDisposition.Skip" />), taming the LLM re-emitting the
      ///   same transfer every turn. A player-outgoing spend or commitment (take_gold, join_party, free_prisoner)
      ///   is queued for confirmation (<see cref="ActionDisposition.Queued" />). Everything else, including give_gold (the NPC's own gift to
      ///   the player) and consequences like change_relation, runs inline as before.
      /// </summary>
      public ActionDisposition Preprocess(GameAction action, Hero npc, NpcProfile profile, ActionContext context)
      {
         if (action == null) return ActionDisposition.ExecuteInline;

         string type = action.Type ?? "";

         // An item leaving the player's hands (3chmidt, 13/10/2026): a sale always, a gift written in words, is asked
         // first, naming the real item the game would take; the pending action then takes exactly that one.
         if (type.Equals("sell_item", StringComparison.OrdinalIgnoreCase) || type.Equals("give_item", StringComparison.OrdinalIgnoreCase))
            return PreprocessItem(action, npc, profile, context, type);

         // counter_offer (Maifailun and Gabriel, 14/10/2026): a captive bargains real terms for her own freedom;
         // always confirmed, naming every term struck, the same way a sale always is.
         if (type.Equals("counter_offer", StringComparison.OrdinalIgnoreCase))
            return PreprocessCounterOffer(action, npc, profile, context);

         bool tracked = type.Equals("give_gold", StringComparison.OrdinalIgnoreCase)
                        || type.Equals("take_gold", StringComparison.OrdinalIgnoreCase)
                        || type.Equals("join_party", StringComparison.OrdinalIgnoreCase)
                        || type.Equals("free_prisoner", StringComparison.OrdinalIgnoreCase);
         if (!tracked) return ActionDisposition.ExecuteInline; // change_relation, execute_prisoner, etc.: untouched

         int amount = ExtractAmount(action);
         string key = $"{type.ToLowerInvariant()}:{npc?.StringId}:{amount}";
         if (_resolvedKeys.Contains(key))
         {
            ModLog.Info($"[CONFIRM] repeat suppressed ({key}): the LLM re-emitted an already-resolved transfer.");

            return ActionDisposition.Skip;
         }

         _resolvedKeys.Add(key); // resolved for this conversation whatever happens next

         if (!ActionConfirmationPolicy.RequiresConfirmation(type)) return ActionDisposition.ExecuteInline; // give_gold

         string npcName = npc?.Name?.ToString() ?? profile?.Name ?? "them";
         _pending.Enqueue(new PendingConfirmation
         {
            Action = action,
            Npc = npc,
            Profile = profile,
            Context = context,
            Amount = amount,
            Question = ActionConfirmationPolicy.BuildQuestion(type, npcName, amount)
         });

         return ActionDisposition.Queued;
      }

      /// <summary>
      ///   give_item and sell_item. A gift picked from the bag runs as before; a gift written in words and every sale
      ///   are queued with the real item (BannerlordGameStateBridge.PreviewItem, the executor's own resolution) and, for a
      ///   sale, the price the buyer can pay (ItemSalePolicy). The pending context carries that item's id, so the item the
      ///   player says yes to is the item that leaves. Nothing resolvable or a refused sale runs inline, where the bridge
      ///   says why. A repeat of the same item (and price) this conversation is dropped.
      /// </summary>
      private ActionDisposition PreprocessItem(GameAction action, Hero npc, NpcProfile profile, ActionContext context, string type)
      {
         bool sale = type.Equals("sell_item", StringComparison.OrdinalIgnoreCase);
         bool pickedFromBag = !string.IsNullOrWhiteSpace(context?.OfferedItemId);
         if (!sale && !ActionConfirmationPolicy.GiveItemNeedsConfirmation(pickedFromBag)) return ActionDisposition.ExecuteInline;

         ItemPreview item = BannerlordGameStateBridge.PreviewItem(action, context);
         if (item == null) return ActionDisposition.ExecuteInline; // the bridge refuses it, saying why

         int agreed = ExtractAmount(action);
         ItemSaleDecision deal = sale
            ? ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = agreed, BuyerPurse = npc?.Gold ?? 0, PlayerStillHolds = true, ItemValue = item.Value})
            : null;
         if (sale && deal.Refusal != ItemSaleRefusal.None) return ActionDisposition.ExecuteInline; // the bridge refuses it, saying why

         string key = $"{type.ToLowerInvariant()}:{npc?.StringId}:{item.Id}:{agreed}";
         if (_resolvedKeys.Contains(key))
         {
            ModLog.Info($"[CONFIRM] repeat suppressed ({key}): the LLM re-emitted an already-resolved item deed.");

            return ActionDisposition.Skip;
         }

         _resolvedKeys.Add(key);

         var pinned = new ActionContext(context?.Profile ?? profile, context?.EmittedEvent) {
            OfferedItemId = item.Id, PlayerWordsThisConversation = context?.PlayerWordsThisConversation
         };
         _pending.Enqueue(new PendingConfirmation
         {
            Action = action,
            Npc = npc,
            Profile = profile,
            Context = pinned,
            Amount = deal?.Price ?? 0,
            Question = ActionConfirmationPolicy.BuildItemQuestion(new ItemConfirmationFacts {
               ActionType = type, ItemName = item.Name, NpcName = npc?.Name?.ToString() ?? profile?.Name ?? "them",
               Price = deal?.Price ?? 0, ClampedToPurse = deal?.ClampedToPurse == true
            })
         });

         return ActionDisposition.Queued;
      }

      /// <summary>
      ///   counter_offer. Shares <see cref="BannerlordGameStateBridge.PreviewCounterOffer" /> with the executor (the
      ///   term the player says yes to is the term that happens), the same way <see cref="PreprocessItem" /> shares
      ///   the bridge's own item resolution for a sale. A refusal from the preview runs inline, where the bridge
      ///   says why; otherwise queued with a question naming every term she struck. Repeat key includes the terms
      ///   and the clamped ransom, so a genuinely different bargain struck later in the same conversation is still
      ///   asked, never silently dropped as a repeat.
      /// </summary>
      private ActionDisposition PreprocessCounterOffer(GameAction action, Hero npc, NpcProfile profile, ActionContext context)
      {
         CounterOfferPreview preview = BannerlordGameStateBridge.PreviewCounterOffer(action, npc, context);
         if (preview == null || preview.Refusal != null) return ActionDisposition.ExecuteInline; // the bridge refuses it, saying why

         string key = $"counter_offer:{npc?.StringId}:{string.Join("+", preview.Terms)}:{preview.Ransom}";
         if (_resolvedKeys.Contains(key))
         {
            ModLog.Info($"[CONFIRM] repeat suppressed ({key}): the LLM re-emitted an already-resolved offer.");

            return ActionDisposition.Skip;
         }

         _resolvedKeys.Add(key);

         string npcName = npc?.Name?.ToString() ?? profile?.Name ?? "them";
         _pending.Enqueue(new PendingConfirmation {
            Action = action, Npc = npc, Profile = profile, Context = context, Amount = preview.Ransom,
            Question = ActionConfirmationPolicy.BuildOfferQuestion(new OfferConfirmationFacts {
               NpcName = npcName, IsFemale = npc?.IsFemale == true, Terms = preview.Terms,
               Ransom = preview.Ransom, RansomClampedToPurse = preview.RansomClampedToPurse, FealtyRealm = preview.FealtyRealm
            })
         });

         return ActionDisposition.Queued;
      }

      /// <summary>
      ///   Raises the next queued confirmation as an OOC chat prompt (Y default), putting the chat into listen
      ///   mode. If the conversation already ended (the player cannot answer), the queued spends are DROPPED, not
      ///   applied: a player-outgoing payment must never go through without the player's consent.
      /// </summary>
      public void PromptNext(IActionConfirmationHost host)
      {
         if (_pending.Count == 0)
         {
            _awaiting = false;

            return;
         }

         if (host.IsConversationEnded)
         {
            ModLog.Info($"[CONFIRM] conversation ended with {_pending.Count} unanswered confirmation(s), dropped, nothing charged.");
            _pending.Clear();
            _awaiting = false;

            return;
         }

         _awaiting = true;
         host.AppendOoc($"» {_pending.Peek().Question} [Y/n]");
      }

      /// <summary>
      ///   Routes a player input while a confirmation is pending. Enter/y/yes applies it; n/no declines it;
      ///   anything else is treated as the player recomposing: every pending action is declined and the typed
      ///   message is sent to the LLM as a fresh turn.
      /// </summary>
      public void HandleInput(IActionConfirmationHost host, string raw)
      {
         ConfirmationAnswer answer = ActionConfirmationPolicy.ParseAnswer(raw);

         if (answer == ConfirmationAnswer.Recompose)
         {
            while (_pending.Count > 0) DeclineAction(host, _pending.Dequeue());
            _awaiting = false;
            host.SendPlayerMessage(raw); // now that _awaiting is false, this sends normally

            return;
         }

         PendingConfirmation p = _pending.Dequeue();
         if (answer == ConfirmationAnswer.Confirm) ApplyConfirmedAction(host, p);
         else DeclineAction(host, p);

         if (_pending.Count > 0) PromptNext(host);
         else _awaiting = false;
      }

      /// <summary>The coin amount of a gold/hire action, from its "amount" or "price" parameter (0 if absent).</summary>
      private static int ExtractAmount(GameAction action)
      {
         int amount = 0;
         if (action.Parameters != null
             && (action.Parameters.TryGetValue("amount", out string a) || action.Parameters.TryGetValue("price", out a)))
            amount = ParseCoinAmount(a);

         return amount;
      }

      /// <summary>
      ///   Parses a coin amount tolerantly. A model (a small local one especially) may write "10,000", "10 000",
      ///   or "10000 denars"; a bare int.TryParse rejects the first two outright and reads a partial, wrong value
      ///   from the third (player report: giving 10,000 registered as a far smaller sum). Keep only the digit run,
      ///   so "10,000 denars" and "10 000" both parse to 10000. A leading minus is dropped: a transfer amount is
      ///   always non-negative here.
      /// </summary>
      private static int ParseCoinAmount(string raw) => CoinAmount.Parse(raw);

      /// <summary>Executes a confirmed action through the bridge and reports it, exactly as the inline path would have.</summary>
      private void ApplyConfirmedAction(IActionConfirmationHost host, PendingConfirmation p)
      {
         try
         {
            ActionOutcome outcome = NpcMemoryCampaignBehavior.Instance?.GameStateBridge
               .ExecuteAction(p.Action, p.Npc, p.Context);
            if (outcome != null) host.AppendSystemMessageLine(outcome.Message, outcome.IsPositive);

            // A CONFIRMED action can still be REFUSED at the bridge (an active war, insolvency, a gate the
            // prompt let through). Mirror the inline path (ChatViewModel action loop): the refusal must reach
            // the TRANSCRIPT as an out-of-character note, not only the UI line, or the model believes the
            // transfer it just confirmed happened and the next turns build on the lie.
            string refusalNote = ActionRefusalNote.For(outcome?.WasRefused == true, outcome?.Message);
            if (refusalNote != null)
            {
               try { host.RecordTranscriptLine(true, "You", Hero.MainHero?.StringId, refusalNote); }
               catch (Exception ex) { ModLog.Error($"[CONFIRM] refusal note failed: {ex.GetType().Name}: {ex.Message}"); }
            }

            // Story log: only when the coin ACTUALLY moved, and the amount the bridge APPLIED (clamped to the
            // real purse), never the raw proposed sum, so the journal agrees with the purse.
            if (outcome != null && !outcome.WasRefused && outcome.AppliedGoldAmount is int moved && moved > 0)
            {
               if (string.Equals(p.Action.Type, "give_gold", StringComparison.OrdinalIgnoreCase))
                  StoryLog.WriteGoldTransfer(p.Profile?.Name, moved, true);
               else if (string.Equals(p.Action.Type, "take_gold", StringComparison.OrdinalIgnoreCase))
                  StoryLog.WriteGoldTransfer(p.Profile?.Name, moved, false);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONFIRM] apply failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Drops a declined action: it is never executed, and a correction is recorded into the transcript so the
      ///   LLM stops treating it as done (both in the ongoing exchange and in the end-of-conversation summary).
      /// </summary>
      private void DeclineAction(IActionConfirmationHost host, PendingConfirmation p)
      {
         host.AppendOoc($"» Declined. \"{p.Question}\" was not carried out.");
         string note = $"(Out of character: I declined the offer \"{p.Question}\" It did not happen, nothing was carried out. Do not treat it as done.)";
         try { host.RecordTranscriptLine(true, "You", Hero.MainHero?.StringId, note); }
         catch (Exception ex) { ModLog.Error($"[CONFIRM] decline note failed: {ex.GetType().Name}: {ex.Message}"); }
         ModLog.Info($"[CONFIRM] player declined {p.Action?.Type} ({p.Amount}).");
      }

      private sealed class PendingConfirmation
      {
         public GameAction Action;
         public int Amount;
         public ActionContext Context;
         public Hero Npc;
         public NpcProfile Profile;
         public string Question;
      }
   }
}
