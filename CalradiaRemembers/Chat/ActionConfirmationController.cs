// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
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
            int.TryParse(a, out amount);

         return amount;
      }

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
