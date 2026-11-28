// Code written by Gabriel Mailhot, 13/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The UI-agnostic core of the PROSE + INTERPRETER composition, shared by BOTH the live chat
   ///   (<see cref="ChatViewModel" />) and the cr.rp_bench roleplay bench so the two feed the ACTION INTERPRETER
   ///   through ONE code path (same prompt, same context projection, same client/model, same failure = prose with
   ///   no tags). It deliberately owns NO UI concern: the caller decides when to resume the main thread and how to
   ///   consume the merged <see cref="ParsedResponse" />.
   /// </summary>
   public static class ActionInterpreterComposer
   {
      /// <summary>The interpreter's reply budget (see the request below for why 900).</summary>
      private const int InterpreterReplyTokens = 900;

      private static bool FitsWindow(int window, string prompt)
         => ContextFitPolicy.Fit(new ContextFitFacts {
            WindowTokens = window, PromptChars = prompt?.Length ?? 0, ReplyTokens = InterpreterReplyTokens,
            Messages = new[] {new ContextFitMessage {IsUser = true, Chars = ActionInterpreterPromptBuilder.FinalInstruction.Length}}
         }).Fits;

      /// <summary>
      ///   Runs the MCM-configured ACTION INTERPRETER over the already-produced <paramref name="proseResponse" /> ONE
      ///   shot and returns a <see cref="ParsedResponse" /> that keeps every prose field but REPLACES the machine tags
      ///   (<see cref="ParsedResponse.Actions" /> and <see cref="ParsedResponse.NewEventData" />) with what the
      ///   interpreter read, for the caller to feed into the SAME consumption pipeline. On ANY failure (empty prose,
      ///   prompt build, call, or parse) it returns the prose with NO tags: a missed tag is applied as nothing rather
      ///   than disturbing the scene, and v1 never falls back to the prose model's own tags (the two modes stay
      ///   cleanly separated). It does NOT switch threads: the caller resumes the main thread before touching UI or
      ///   game state. The interpreter is routed to <paramref name="services" />'s dedicated InterpreterClient (its
      ///   own provider, key, base URL AND model) and named by <paramref name="playerLabel" />.
      ///   <paramref name="conversationInProgress" /> is the hard fact the interpreter otherwise has no way to
      ///   read (a stranger scene reads "regard +0, never met before" on EVERY turn): the CALLER knows the turn
      ///   index / session state, so it computes this and passes it through to
      ///   <see cref="ActionInterpreterContextBuilder.Project(EncounterContext, NpcProfile, string, bool)" />,
      ///   fixing the rp_bench scene-1 bug where every turn of an ongoing exchange kept being tagged first_meeting.
      /// </summary>
      public static async Task<ParsedResponse> InterpretAsync(
         ParsedResponse proseResponse, EncounterContext context, NpcProfile profile,
         string playerLabel, LlmServices services, CancellationToken ct, bool conversationInProgress, string tradeBackground = null)
      {
         string prose = CombineProseForInterpretation(proseResponse.Dialogue, proseResponse.Narration);
         if (string.IsNullOrWhiteSpace(prose))
            return WithInterpretedTags(proseResponse, null); // nothing to read -> apply no tags

         string prompt;
         // From the player's own toggle, not the chat's session compact: the interpreter may run on a cloud endpoint
         // while the chat is local, and it fits its own window below.
         LeanPromptLevel level = ModSettings.LeanPromptChosen ? LeanPromptLevel.Lean : LeanPromptLevel.Full;
         try
         {
            string contextFacts = ActionInterpreterContextBuilder.Project(context, profile, playerLabel, conversationInProgress);
            // On a turn about captives only: the names a buy_prisoner target is one of, and the exchange before this reply.
            if (!string.IsNullOrWhiteSpace(tradeBackground)) contextFacts += Environment.NewLine + Environment.NewLine + tradeBackground;
            // Stage 5 (28/11/2026): a reply in the novel style names the NPC in the third person; the interpreter is told who
            // that is. In the facts, not the cached prefix, so the default prompt is unchanged.
            if (services?.ChatService?.PromptBuilder is NpcMemoryService.Core.Prompts.PromptBuilder builder && builder.NovelStyleApplies(context))
               contextFacts += Environment.NewLine + Environment.NewLine + NovelStylePolicy.InterpreterNote;
            // Compact reaches the interpreter too, at last. This prompt had no notion of a prompt level: at
            // ~17.6k tokens it is more than twice the whole context of the 8,192-token models the setting is
            // named for, so for those players this call could never once succeed - and the failure below is
            // swallowed, so they played on with no deed ever recorded and nothing saying why.
            prompt = ActionInterpreterPromptBuilder.Build(prose, contextFacts, level);

            // The interpreter's own fit (fkasad, 13/10/2026): on a local endpoint with a declared window, its full
            // prompt (~17.6k tokens) is replaced by the compact one when it would not fit with its reply, rather than
            // being cut by the server (its action catalogue is exactly what a truncation removes).
            int window = LlmServices.ContextWindowFor(CalradiaRemembers.Logic.Providers.ServiceRole.Interpret);
            if (window > 0 && level == LeanPromptLevel.Full && !FitsWindow(window, prompt))
            {
               level = LeanPromptLevel.Lean;
               prompt = ActionInterpreterPromptBuilder.Build(prose, contextFacts, level);
               ModLog.Info($"[CONTEXT] interpreter: the full reading prompt does not fit the local window ({window}); compact used, fits: {FitsWindow(window, prompt)}.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROSE-INTERPRET] interpreter prompt build failed; applying no tags: {ex.GetType().Name}: {ex.Message}");

            return WithInterpretedTags(proseResponse, null);
         }

         var request = new LlmRequest {
            SystemPrompt = prompt,
            // The interpreter prompt has ALWAYS been built prefix-first so its invariant head — the role, the
            // tag vocabulary, the output format and the whole action catalogue — could be cached across calls.
            // Being cacheable and being CACHED are not the same thing: nothing ever asked for the breakpoint,
            // so every turn re-sent that block at full price, on top of the prose call's own prompt. Claimed
            // here. The prompt's text and order are untouched; only where cache_control falls changes.
            StableSystemPrompt = ActionInterpreterPromptBuilder.StablePrefixFor(level),
            // Diagnostic only: which character this reading is FOR, so a wire observer compares the
            // interpreter's prefix against its own and never against the prose call's (see LlmRequest.Subject).
            // Prefixed (28/11/2026): the prose call carries the bare id too, so with one model for both the conversation log called
            // every reply an interpreter call, and the wire recorder compared the two prefixes against each other.
            Subject = CalradiaRemembers.Logic.Diagnostics.ChatLogRoles.InterpreterSubject(profile?.Id),
            Messages = new[] {new LlmMessage(MessageRole.User, ActionInterpreterPromptBuilder.FinalInstruction)},
            // 900, not 600: the two-step FinalInstruction makes the model write a CHECK grounding line per
            // candidate action BEFORE the tag block. A reply carrying several deeds can spend 600 on the CHECK
            // preamble and truncate the tags (bench 2026-08-19 16:06: empty emits + a "no content" call). Even
            // with reasoning pinned off (LlmServices.InterpreterReasoningEffort) some models emit stray reasoning
            // tokens too, so the headroom is doubly earned. Must match the bench (ActionBenchRunner) so the bench
            // measures what the game does.
            Parameters = new LlmParameters {MaxTokens = InterpreterReplyTokens, Creativity = 0.2f},
            // The dedicated InterpreterClient below owns the interpreter's provider, key, base URL AND model
            // (ResolveInterpreterModel: the configured Actions Interpreter model, or the scene's model when blank),
            // so no ModelOverride is needed here. Its ModelProvider still honours the TestModelOverride bench seam.
            ModelOverride = null
         };

         // Gabriel (25/10/2026), on a reading that brought back nothing usable: "reessaie une fois l'interpreteur en ajoutant
         // un log qui le specifie" (InterpreterRetryPolicy). A reading that parsed is an answer, even with no deed in it.
         ParsedResponse interpreted = null;
         string error = null;
         InterpreterAttemptOutcome lastOutcome = InterpreterAttemptOutcome.Empty;
         for (int attempt = 1; ; attempt++)
         {
            (InterpreterAttemptOutcome outcome, ParsedResponse read, string why) = await AttemptAsync(request, services, ct).ConfigureAwait(false);
            if (outcome == InterpreterAttemptOutcome.Read)
            {
               interpreted = read;
               if (attempt > 1) ModLog.Compat($"[PROSE-INTERPRET] interpreter retry {attempt} of {InterpreterRetryPolicy.MaxAttempts} succeeded; the reply's deeds are recorded.");

               break;
            }

            error = why;
            lastOutcome = outcome;
            if (!InterpreterRetryPolicy.ShouldRetry(outcome, attempt)) break;

            ModLog.Compat($"[PROSE-INTERPRET] interpreter attempt {attempt} of {InterpreterRetryPolicy.MaxAttempts} brought back nothing usable "
                        + $"({outcome}: {why}); asking the interpreter once more.");
         }

         if (interpreted == null)
         {
            // Compat, not Error: this channel is on even when logging is off (the Nexus default), because a
            // silent failure here is INVISIBLE in play - the prose still shows, the scene continues, and only
            // the deeds quietly stop being recorded. A player on a small local model could run a whole campaign
            // in that state, as DuskSymphony did, with nothing anywhere to explain it.
            ModLog.Compat($"[PROSE-INTERPRET] interpreter produced no tags after {InterpreterRetryPolicy.MaxAttempts} attempts; applying none: {error ?? "empty reply"}. "
                        + "Deeds from this reply are NOT recorded. " + InterpreterRetryPolicy.FailureAdvice(lastOutcome, error));

            return WithInterpretedTags(proseResponse, null);
         }

         ModLog.Info($"[PROSE-INTERPRET] interpreter supplied {interpreted.Actions?.Count ?? 0} action(s)"
                     + (interpreted.NewEventData != null ? $" + 1 event ({interpreted.NewEventData.Type})" : "")
                     + $" (model: {services.ResolveInterpreterModel() ?? "resolved"}).");

         return WithInterpretedTags(proseResponse, interpreted);
      }

      /// <summary>Test seam (cr.test interpreter_retry): stands in for the interpreter client's call when set.</summary>
      internal static Func<LlmRequest, CancellationToken, Task<LlmResponse>> CallOverride;

      /// <summary>
      ///   One interpreter call, on its own time budget: what it brought back, the reading when it parsed, and why not
      ///   otherwise. Never throws.
      /// </summary>
      private static async Task<(InterpreterAttemptOutcome Outcome, ParsedResponse Read, string Why)> AttemptAsync(
         LlmRequest request, LlmServices services, CancellationToken ct)
      {
         string content = null;
         string error = null;
         int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
         using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
         {
            cts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
            try
            {
               // Route to the interpreter's OWN client: its base URL / key / model are the interpreter provider's,
               // which may differ from the scene (prose) provider. Never null (falls back to the main client).
               Func<LlmRequest, CancellationToken, Task<LlmResponse>> call = CallOverride ?? services.InterpreterClient.CompleteAsync;
               LlmResponse resp = await Task.Run(() => call(request, cts.Token)).ConfigureAwait(false);

               // The interpreter runs once per turn on a ~70k-char prompt, which makes it the larger half of
               // what a turn costs; its usage reaches the log so a missed cache breakpoint can be seen.
               if (resp?.Usage != null)
                  ModLog.Info($"  [INTERPRETER TOKENS] prompt={resp.Usage.PromptTokens} "
                            + $"(cached:{resp.Usage.CachedPromptTokens ?? 0}) | completion={resp.Usage.CompletionTokens}");

               if (resp is {IsSuccess: true}) content = resp.Content;
               else error = resp?.ErrorMessage ?? "no result";
            }
            catch (Exception ex)
            {
               error = $"{ex.GetType().Name}: {ex.Message}";
            }
         }

         if (error != null) return (InterpreterAttemptOutcome.CallFailed, null, error);
         if (string.IsNullOrWhiteSpace(content)) return (InterpreterAttemptOutcome.Empty, null, "empty reply");

         try
         {
            return (InterpreterAttemptOutcome.Read, new SectionResponseParser().Parse(content), null);
         }
         catch (Exception ex)
         {
            return (InterpreterAttemptOutcome.Unparsable, null, $"{ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Splices <paramref name="interpreted" />'s machine tags (<see cref="ParsedResponse.Actions" /> and
      ///   <see cref="ParsedResponse.NewEventData" />) onto the prose turn, keeping every OTHER field of
      ///   <paramref name="prose" /> (dialogue, narration, stance, discovery, quests, witness reactions) exactly
      ///   as the prose model wrote it. A null <paramref name="interpreted" /> means "apply no tags": Actions empty
      ///   and NewEventData null, so the turn runs the prose with no ACTION/EVENT effect. Mirrors the field list
      ///   of <c>ChatViewModel.WithoutEventData</c> (plus Resolutions), so a field ADDED to
      ///   <see cref="ParsedResponse" /> must be added here too, or this path would silently drop it.
      /// </summary>
      internal static ParsedResponse WithInterpretedTags(ParsedResponse prose, ParsedResponse interpreted) => new() {
         Dialogue = prose.Dialogue,
         Narration = prose.Narration,
         NarrationBeforeDialogue = prose.NarrationBeforeDialogue,
         Memory = prose.Memory,
         // The writer's: only its prompt carries the memory codes (kurushimi00, 01/11/2026).
         DiscussedMemories = prose.DiscussedMemories,
         NewEventData = interpreted?.NewEventData,
         Reputation = prose.Reputation,
         StanceShift = prose.StanceShift,
         // The character's own decision about the scene (a private audience granted or refused) survives when the
         // interpreter recorded none (Lufaine, 30/09/2026: the room never cleared); deeds stay the interpreter's.
         Actions = Logic.SceneDecisionMerge.Merge(interpreted?.Actions, prose.Actions),
         Discovery = prose.Discovery,
         // avalanche24 (03/11/2026): a task the interpreter found written as [ACTION] type: quest is the writer's offer too.
         QuestGiven = prose.QuestGiven ?? interpreted?.QuestGiven,
         QuestBlockMalformed = prose.QuestBlockMalformed
                               || (prose.QuestGiven == null && interpreted?.QuestGiven == null && interpreted?.QuestBlockMalformed == true),
         QuestCompleted = prose.QuestCompleted,
         QuestAbandoned = prose.QuestAbandoned,
         WitnessReactions = prose.WitnessReactions,
         Resolutions = prose.Resolutions
      };

      /// <summary>
      ///   Combines the NPC's narration (physical action) then dialogue into one prose block for the interpreter to
      ///   read, exactly as the cr.prose_interpret_test bench's CombineProse does, so the live path and the bench
      ///   feed the interpreter identically.
      /// </summary>
      internal static string CombineProseForInterpretation(string dialogue, string narration)
         => CombineProse(dialogue, narration);

      /// <summary>
      ///   What the interpreter reads about a trade in captives this turn (RaveN017Mod, Nexus, 05/11/2026: several sales in a row
      ///   were misread, the interpreter seeing one reply alone): the captives the player holds and the exchange just before the
      ///   reply, or null when the turn is not about captives. <paramref name="history" /> is the session after this turn was
      ///   added (its last messages are the player's line and the reply).
      /// </summary>
      internal static string TradeBackgroundFor(IReadOnlyList<LlmMessage> history, string playerLine, string reply)
      {
         List<Hero> held = Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners();
         if (held.Count == 0) return null;

         return Logic.Prisoners.InterpreterTradeBackground.Block(new Logic.Prisoners.InterpreterTradeFacts {
            HeldCaptives = held.Select(h => h.Name?.ToString()).ToList(), PlayerLine = playerLine,
            PreviousReply = PreviousReplyOf(history), Reply = reply
         });
      }

      /// <summary>The character's reply before the player's last line, or null on a first turn.</summary>
      internal static string PreviousReplyOf(IReadOnlyList<LlmMessage> history)
      {
         if (history == null) return null;

         int lastPlayer = -1;
         for (int i = history.Count - 1; i >= 0; i--)
            if (history[i]?.Role == MessageRole.User) { lastPlayer = i; break; }

         for (int i = lastPlayer - 1; i >= 0; i--)
            if (history[i]?.Role == MessageRole.Assistant) return history[i].Content;

         return null;
      }

      private static string CombineProse(string dialogue, string narration)
      {
         var sb = new StringBuilder();
         if (!string.IsNullOrWhiteSpace(narration)) sb.AppendLine(narration.Trim());
         if (!string.IsNullOrWhiteSpace(dialogue)) sb.Append(dialogue.Trim());

         return sb.ToString().Trim();
      }

      /// <summary>
      ///   The prompt-safe player label the interpreter names the player by, resolved exactly as
      ///   <see cref="LlmServices" /> feeds <c>PromptBuilder.PlayerName</c> (the test override when set, else the
      ///   live hero name), so the interpreted memory names the player the same way the prose prompt did. This also
      ///   honours the bench's forced player name (LlmServices.TestPlayerNameOverride).
      /// </summary>
      public static string ResolveDefaultPlayerLabel()
         => string.IsNullOrWhiteSpace(LlmServices.TestPlayerNameOverride)
            ? Hero.MainHero?.Name?.ToString() ?? ""
            : LlmServices.TestPlayerNameOverride;
   }
}
