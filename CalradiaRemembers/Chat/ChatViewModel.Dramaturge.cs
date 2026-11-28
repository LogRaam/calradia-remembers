// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 6 of the group-scene plan, the per-turn machinery of the Dramaturge mode (docs/design/group-chat/
// integration-plan-2026-11-28.md). Gabriel, 28/11/2026: three modes, "le mode classique avec un seul modele+NSFW, le mode
// Enhanced avec deux modeles PROSE+INTERPRET+NSFW, et le mode dramaturge avec DRAMA+PROSE+INTERPRET+NSFW". NOT A/B-validated:
// in the simulator a same-batch baseline with no inner lives and no keeper equalled the full system on the drama, so this lives
// ONLY under the Dramaturge mode (DramaturgeModePolicy) and Standard and Enhanced run exactly as before.
//
// The engine half, in the ordinary 1:1 chat:
// - the inner life: after the player's line (in parallel with the prose call) and after the character's reply (in the
//   background), one cheap call on the interpreter client updates where the character stands; the next turn's prompt carries
//   it ("Where you stand now", EncounterContext.InnerLifeNote). One chained task per chat, so two updates never race; a turn
//   waits for the last one a few seconds at most. The state lives for this chat only and is never saved; the bond starts from
//   the meeting record of stage 2 when there is one, and is never written back (prompt texture, never a regard ledger).
// - the keeper: before the reply is shown, one cheap call on the interpreter client reads it for a lie against what the
//   character knows (the mod's rule: NPCs never lie), a repeat within this chat, a contradiction or an invention; on a problem,
//   one rewrite on the prose model with the reason (EncounterContext.DraftRefusedNote), read once more, and the rewrite is shown.
//   It keeps the record of this chat (what was established, where things are).
// - the narrated reaction chosen in code: a 1:1 chat has no other listener, so there is none here; stage 7 takes it for groups.
// Every cheap call has a short limit; on failure or timeout the turn carries on without it. The conversation log (stage 1)
// gets the inner-life reading each time and the keeper's verdict and rewrite.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // The most lines of this chat the cheap readers are shown (the repeat check reads this chat only).
      private const int DramaturgeSceneLines = 30;

      private volatile DramaturgeInnerState _dramaState = new();
      private readonly DramaturgeRecord _dramaRecord = new();
      private Task _dramaMind = Task.CompletedTask;
      private DramaturgeSheet _dramaSheet;

      /// <summary>cr.test dramaturge_mode: the stand-in for the interpreter client (inner life and keeper). Null in play.</summary>
      internal static ILlmClient DramaturgeReaderForDiagnostics;

      /// <summary>cr.test dramaturge_mode: the stand-in for the prose call of the keeper's rewrite (its text, or null). Null in play.</summary>
      internal static Func<string, Task<string>> DramaturgeRewriterForDiagnostics;

      /// <summary>cr.test dramaturge_mode: the inner life as it stands now.</summary>
      internal DramaturgeInnerState DramaturgeStateForDiagnostics => _dramaState;

      /// <summary>cr.test dramaturge_mode: the inner-life update in flight (or done).</summary>
      internal Task DramaturgeMindForDiagnostics => _dramaMind;

      /// <summary>cr.test dramaturge_mode: whether an ordinary player turn of the main character would run the machinery now.</summary>
      internal bool DramaturgeRunsForMainForDiagnostics => DramaturgeModePolicy.Runs(DramaturgeFactsFor(MainSpeaker(), false, false));

      /// <summary>The facts the Dramaturge machinery is decided on for a turn, read live.</summary>
      internal DramaturgeTurnFacts DramaturgeFactsFor(ChatSpeaker speaker, bool suppressPlayerLine, bool suppressGroupTail)
         => new() {
            DramaturgeMode = ModSettings.DramaturgeMode,
            SwitchOnTheMcmMode = LlmServices.ActiveModelMode == ManualModelMode.Enhanced,
            IsMainSpeaker = speaker?.IsMain == true,
            IsSyntheticPlayerLine = suppressPlayerLine,
            IsCaptiveEncounter = IsCaptiveEncounter,
            IsCaptorScene = _captorScene,
            IsGroupVoice = suppressGroupTail || ChatManager.ActiveSkin == ChatManager.CouncilSkin
         };

      private static ILlmClient DramaturgeReader => DramaturgeReaderForDiagnostics ?? NpcMemoryCampaignBehavior.Instance?.LlmServices?.InterpreterClient;

      private static string PlayerNameNow => Hero.MainHero?.Name?.ToString();

      /// <summary>
      ///   Who the character is and what they know, for the cheap readers: the stock sections the composed opening already
      ///   reads (ReadComposedOpeningSheet: identity, personality, authored backstory, what matters most, history, the meeting
      ///   record), the talk of the realm and their last two days. Read once per chat, on the main thread. Also seeds the bond.
      /// </summary>
      private DramaturgeSheet DramaturgeSheetFor(ChatSpeaker speaker)
      {
         if (_dramaSheet != null) return _dramaSheet;

         ComposedOpeningSheet facts = null;
         try { facts = ReadComposedOpeningSheet(speaker); }
         catch (Exception ex) { ModLog.Error($"[DRAMATURGE] the character's facts could not be read: {ex.GetType().Name}: {ex.Message}"); }

         string knows = string.Join("\n\n", new[] {facts?.Character, facts?.History}.Where(s => !string.IsNullOrWhiteSpace(s)));
         var world = new List<string>();
         if (facts?.RealmNews != null) world.AddRange(facts.RealmNews);
         if (facts?.LivedLately != null) world.AddRange(facts.LivedLately);
         _dramaSheet = new DramaturgeSheet {
            NpcName = speaker?.Profile?.Name ?? speaker?.Name, PlayerName = PlayerNameNow, Knows = knows, WorldFacts = world
         };

         // The meeting record of stage 2 left a bond toward the player: the inner life starts from it (read, never written back).
         if (speaker?.Profile != null && !_dramaState.HasContent)
         {
            string bond = DramaturgeInnerLife.SeedBond(MeetingMemory.About(MeetingMemoryAtOpening(speaker.Profile), PlayerNameNow)?.Bond, PlayerNameNow);
            if (bond.Length > 0) _dramaState = new DramaturgeInnerState {Bond = bond};
         }

         return _dramaSheet;
      }

      /// <summary>This chat's last lines, as "Name: text" (narration as it is), plus the player's line not yet recorded.</summary>
      private string DramaturgeSceneText(string pendingPlayerLine)
      {
         var lines = _transcript.Lines
                                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Text))
                                .Select(l => l.IsNarration ? l.Text.Trim() : $"{(l.IsPlayer ? PlayerNameNow ?? "The visitor" : l.SpeakerName)}: {l.Text.Trim()}")
                                .ToList();
         if (!string.IsNullOrWhiteSpace(pendingPlayerLine)) lines.Add($"{PlayerNameNow ?? "The visitor"}: {pendingPlayerLine.Trim()}");

         return string.Join("\n", lines.Skip(Math.Max(0, lines.Count - DramaturgeSceneLines)));
      }

      /// <summary>
      ///   "Where you stand now" for this turn's prompt: waits a few seconds at most for the update still running from the last
      ///   line, then writes the state as it stands. Null before any reading. The caller returns to the main thread after.
      /// </summary>
      private async Task<string> DramaturgeNoteForTurnAsync()
      {
         Task pending = _dramaMind;
         if (!pending.IsCompleted)
            await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(DramaturgeModePolicy.InnerLifeWaitSeconds))).ConfigureAwait(false);

         return DramaturgeInnerLife.Note(_dramaState, _dramaSheet?.PlayerName ?? PlayerNameNow);
      }

      /// <summary>
      ///   Starts the inner-life update after the last line (main thread: the facts and the scene are read here), chained after
      ///   the one before it so two updates never race. Never waits; its reading goes to the conversation log.
      /// </summary>
      private void StartDramaturgeMindUpdate(ChatSpeaker speaker, string pendingPlayerLine, string after)
      {
         try
         {
            DramaturgeSheet sheet = DramaturgeSheetFor(speaker);
            string scene = DramaturgeSceneText(pendingPlayerLine);
            ILlmClient reader = DramaturgeReader;
            ChatLogCollector log = _chatLog;
            CancellationToken token = _windowClosing.Token;
            Task previous = _dramaMind;
            _dramaMind = Task.Run(async () =>
            {
               try { await previous.ConfigureAwait(false); }
               catch { /* the one before failed: this one still runs */ }

               DramaturgeMindResult r = await DramaturgeTurnComposer.UpdateMindAsync(reader, sheet, _dramaState, scene, DramaturgeModePolicy.CheapCallLimit, token).ConfigureAwait(false);
               _dramaState = r.State;
               log?.AddWhy(ChatLogWhyKind.Dramaturge, $"Inner life after {after}: {r.Why}.");
               if (ModSettings.VerboseLogging) ModLog.Info($"[DRAMATURGE] inner life after {after}: {r.Why}");
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DRAMATURGE] the inner life could not be updated: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The keeper (main thread in, off it for the calls): reads the reply; on a problem, one rewrite on the prose model with
      ///   the reason, the session rolled back and restored exactly as the prose fallback does (ProseFallbackComposer), read once
      ///   more; the rewrite is shown unless it failed, came back empty or reads as a refusal. Returns the result to show and the
      ///   keeper's lines (the caller writes them to the logs back on the main thread).
      /// </summary>
      private async Task<(NpcChatResult Result, DramaturgeKeeperResult Kept)> KeepDramaturgeReplyAsync(
         ChatSpeaker speaker, NpcProfile profile, WorldState world, ChatSession session, string playerMessage, EncounterContext context,
         NpcChatResult result, bool allowTruncationRetry)
      {
         DramaturgeSheet sheet = DramaturgeSheetFor(speaker);
         string scene = DramaturgeSceneText(playerMessage);
         string reply = ActionInterpreterComposer.CombineProseForInterpretation(result.Response?.Dialogue, result.Response?.Narration);
         string original = result.Response?.Dialogue ?? "";
         NpcChatService chat = NpcMemoryCampaignBehavior.Instance?.LlmServices?.ChatService;
         Func<string, Task<string>> stand = DramaturgeRewriterForDiagnostics;
         CancellationToken token = _windowClosing.Token;
         NpcChatResult rewritten = null;

         async Task<string> Rewrite(string reason)
         {
            NpcChatResult r;
            session?.RollbackLastMessage(); // the reply just written
            session?.RollbackLastMessage(); // the player's line, added again by the call below (or restored on failure)
            context.DraftRefusedNote = reason;
            try
            {
               if (stand != null)
               {
                  string text = await stand(reason).ConfigureAwait(false);
                  r = string.IsNullOrWhiteSpace(text) ? NpcChatResult.Failure("no rewrite") : NpcChatResult.Success(new ParsedResponse {Dialogue = text}, null, "stop");
                  if (r.IsSuccess)
                  {
                     session?.AddPlayerMessage(playerMessage);
                     session?.AddNpcMessage(text);
                  }
               }
               else if (chat == null) r = NpcChatResult.Failure("no chat service");
               else r = await chat.ChatAsync(profile, world, session, playerMessage, context, token, allowTruncationRetry: allowTruncationRetry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
               r = NpcChatResult.Failure(ex.Message);
            }
            finally
            {
               context.DraftRefusedNote = null;
            }

            string shown = ActionInterpreterComposer.CombineProseForInterpretation(r.Response?.Dialogue, r.Response?.Narration);
            bool show = DramaturgeRewritePolicy.ShowsRewrite(new KeeperRewriteFacts {
               RewriteAnswered = r.IsSuccess, RewriteHasText = !string.IsNullOrWhiteSpace(shown), RewriteLooksLikeRefusal = DialogueHeuristics.LooksLikeRefusal(shown)
            });
            if (!show)
            {
               // The first reply stands: the session holds it again, exactly as before the rewrite.
               if (r.IsSuccess)
               {
                  session?.RollbackLastMessage();
                  session?.RollbackLastMessage();
               }

               session?.AddPlayerMessage(playerMessage);
               session?.AddNpcMessage(original);

               return null;
            }

            rewritten = r;

            return shown;
         }

         DramaturgeKeeperResult kept = await Task.Run(() => DramaturgeTurnComposer.KeepAsync(DramaturgeReader, sheet, _dramaState, _dramaRecord, scene, reply, Rewrite,
                                                                                              DramaturgeModePolicy.CheapCallLimit, token)).ConfigureAwait(false);

         return (kept.Rewritten && rewritten != null ? rewritten : result, kept);
      }

      /// <summary>The keeper's lines, to the log and the conversation log (main thread).</summary>
      private void NoteDramaturgeKeeper(DramaturgeKeeperResult kept)
      {
         if (kept == null) return;

         foreach (string line in kept.Why)
         {
            ModLog.Info($"[DRAMATURGE] keeper: {line}");
            NoteWhy(ChatLogWhyKind.Keeper, $"Keeper: {line}.");
         }
      }

      /// <summary>
      ///   cr.test dramaturge_mode: the keeper over a fixed reply of the main character, through the real method (session
      ///   rollback and restore included); only the network calls are stand-ins. Main thread in.
      /// </summary>
      internal async Task<(string Shown, DramaturgeKeeperResult Kept, ChatSession Session)> KeepReplyForDiagnosticsAsync(string playerLine, string reply)
      {
         ChatSpeaker speaker = MainSpeaker();
         ChatSession session = BuildSessionFor(speaker);
         session.AddPlayerMessage(playerLine);
         session.AddNpcMessage(reply);
         NpcChatResult first = NpcChatResult.Success(new ParsedResponse {Dialogue = reply}, null, "stop");
         EncounterContext context = new();
         (NpcChatResult result, DramaturgeKeeperResult kept) = await KeepDramaturgeReplyAsync(speaker, speaker?.Profile, new WorldState(), session, playerLine, context, first, false).ConfigureAwait(false);

         return (result.Response?.Dialogue, kept, session);
      }

      /// <summary>cr.test dramaturge_mode: starts the inner-life update after a player line, as a turn does.</summary>
      internal void StartMindUpdateForDiagnostics(string playerLine) => StartDramaturgeMindUpdate(MainSpeaker(), playerLine, "the player's line");
   }
}
