// Code written by Gabriel Mailhot, 05/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Generates letter content via the LLM and queues a <see cref="PendingLetter" />
   ///   on the sending NPC's profile. Reuses the existing <see cref="NpcChatService" />
   ///   pipeline so the NPC's full personality, history, and romantic context are
   ///   automatically included. Only <c>response.Dialogue</c> is used — no profile
   ///   mutations, no action dispatch, no quest events.
   /// </summary>
   public static class LetterGenerationService
   {
      /// <summary>
      ///   A first-person phrase for where the letter's writer currently is, or null when it cannot be
      ///   read with confidence (the prompt then falls back to its stale-location guard rather than have
      ///   the model guess). Grounds the letter so an NPC on the march no longer writes as if still in the
      ///   town where the player last met them (player-reported: a correspondent kept placing herself in a
      ///   settlement both parties had long since left for a campaign).
      /// </summary>
      internal static string WhereaboutsFor(NpcProfile profile)
      {
         if (profile == null) return null;
         try
         {
            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);

            return WhereaboutsForHero(sender);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   A first-person phrase for where a hero currently is, or null when it cannot be read with confidence.
      ///   Handles a hero IN a settlement (visiting or residing), one travelling with a party or army, and a
      ///   settled notable at their own home (a lord's lady with no party, previously read as "uncertain", so
      ///   her letters carried no place at all, which a player noticed as the location grounding "not working").
      /// </summary>
      private static string WhereaboutsForHero(Hero hero) => hero == null ? null : LetterWhereaboutsPolicy.ForWriter(WhereaboutsOf(hero));

      /// <summary>Test seam (cr.test all): the phrase a letter places the player with right now.</summary>
      internal static string DebugPlayerWhereabouts() => Hero.MainHero == null ? null : LetterWhereaboutsPolicy.ForPlayer(WhereaboutsOf(Hero.MainHero));

      /// <summary>Where a hero is, as facts for LetterWhereaboutsPolicy (the writer's phrase and the player's differ).</summary>
      private static WhereaboutsFacts WhereaboutsOf(Hero hero)
      {
         var settlement = hero.CurrentSettlement ?? hero.StayingInSettlement;
         MobileParty party = hero.PartyBelongedTo;

         return new WhereaboutsFacts {
            SettlementName = settlement?.Name?.ToString(),
            InParty = party != null,
            InArmy = party?.Army != null,
            MenInParty = party?.MemberRoster?.TotalManCount ?? 0,
            // No settlement and no party: a settled notable or lady is at her own hall.
            HomeName = hero.HomeSettlement?.Name?.ToString()
         };
      }

      /// <summary>
      ///   A line placing the PLAYER for the letter's writer, so a spouse or partner does not write "come home,
      ///   you have been gone" while the player is standing in the same hall, nor speak as though the player is
      ///   somewhere they are not. Says the player is HERE WITH the writer when they share a settlement (and
      ///   forbids summoning them home in that case), else where the player is, else null (say nothing).
      /// </summary>
      private static string PlayerSituationFor(NpcProfile profile)
      {
         try
         {
            Hero player = Hero.MainHero;
            if (player == null || profile == null) return null;

            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
            var senderSettlement = sender?.CurrentSettlement ?? sender?.StayingInSettlement;
            var playerSettlement = player.CurrentSettlement ?? player.StayingInSettlement;

            if (senderSettlement != null && senderSettlement == playerSettlement)
               return $"The player is HERE WITH YOU, within {playerSettlement.Name}: they are present, not away. Do " +
                      "NOT tell them to come home, do not ask them to write to you, and do not speak as though they " +
                      "are far off. Speak to their being at your side.";

            // The PLAYER's own phrase, never the writer's (Lordfadooboo, 27/09/2026: "on the march with your own
            // retinue" reused for the player told a wanderer the player rode with the wanderer's retinue).
            string playerWhere = LetterWhereaboutsPolicy.ForPlayer(WhereaboutsOf(player));
            if (!string.IsNullOrWhiteSpace(playerWhere))
               return $"The player is presently {playerWhere}, apart from you.";

            return null;
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   Diagnostic (console <c>cr.letter_where</c>): generates ONE initial letter and hands the writer's
      ///   resolved whereabouts and the letter body to <paramref name="onWritten" /> for logging, WITHOUT
      ///   queuing a courier or mutating the profile. Used to verify a letter grounds to the writer's REAL
      ///   location rather than a stale town planted in their memory. The callback fires on a background
      ///   thread once the LLM returns.
      /// </summary>
      public static void GenerateForDiagnostic(
         NpcProfile profile, LetterReason reason, string triggerContext,
         LlmServices llmServices, Action<string, string> onWritten)
      {
         if (profile == null || llmServices?.ChatService == null)
         {
            onWritten?.Invoke(null, null);

            return;
         }

         _ = RunDiagnosticAsync(profile, reason, triggerContext, llmServices, onWritten);
      }

      private static async Task RunDiagnosticAsync(
         NpcProfile profile, LetterReason reason, string triggerContext,
         LlmServices llmServices, Action<string, string> onWritten)
      {
         string whereabouts = WhereaboutsFor(profile);
         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string message = LetterPromptBuilder.BuildInitialLetterMessage(profile, reason, triggerContext, playerName, whereabouts);

         string content = await CallLlmForLetterAsync(llmServices, profile, message).ConfigureAwait(false);
         onWritten?.Invoke(whereabouts, content);
      }

      /// <summary>
      ///   Entry point for an NPC-initiated letter: seals an envelope on <paramref name="profile" /> (its text is written
      ///   when the player first opens it) and persists via <paramref name="store" />.
      /// </summary>
      public static void QueueInitialLetter(
         NpcProfile profile,
         Hero giverHero,
         LetterReason reason,
         string triggerContext,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today,
         string offerTargetId = null,
         int offerAmount = 0)
      {
         // Only ADULTS initiate correspondence: a child hero with a profile (the player talked to them once)
         // must never be swept up by a trigger loop into writing tournament congratulations, let alone anything
         // warmer. A child may still REPLY to a letter the player chooses to send them (QueueReplyLetter).
         if (giverHero != null && !Mapping.ActionGate.Allows(giverHero, Logic.ActionKind.Letter, log: true))
         {
            ModLog.Info($"[LETTER] refused: {profile?.Name} may not write first ({reason}).");

            return;
         }

         // The letter box (Gabriel, 30/10/2026): a subject the player threw away from this sender is not written, so it
         // costs no model call; the arrival check stays as the backstop for letters written without this door.
         if (LetterBox.IsSilenced(profile, reason))
         {
            ModLog.Info($"[LETTERBOX] not written: {profile?.Name} would write about '{LetterBoxPolicy.SubjectOf(reason)}', which the player silenced.");

            return;
         }

         // The letter box (Gabriel, 31/10/2026: "on peut juste garder la boite et enlever l'ancien mode"): every letter an
         // NPC starts travels as an envelope, written when the player first opens it. No model, no letter: one that could
         // never be opened is not sent.
         if (llmServices?.ChatService == null) return;

         // Gabriel (28/09/2026): a greater lord does not write in their own hand to one far below them.
         string writtenBy = CorrespondenceRank.WrittenByFor(giverHero, profile, reason);
         SealEnvelope(profile, giverHero, reason, triggerContext, store, today, writtenBy, offerTargetId, offerAmount);
      }

      /// <summary>
      ///   Fire-and-forget entry point for the NPC's reply to a player response.
      /// </summary>
      public static void QueueReplyLetter(
         NpcProfile profile,
         Hero giverHero,
         PendingLetter originalLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         // THE ANSWER IS FILED BEFORE IT IS ANSWERED. Zivkyn (Nexus, 18/09/2026): "Love interest sends me four
         // letters, i write back to each one, she responds to each one. I return to talk to her and shes mad i
         // didnt write back... all four letters."
         //
         // He was right and her ledger agreed with him. An NPC-authored letter files a memory when it reaches
         // the player (LetterPopupManager -> LetterMemoryFinalizer), and an UNPROMPTED player letter files one
         // when it reaches the NPC (PlayerLetterService.RecordReceipt, added for an earlier report in this same
         // family). A REPLY to her letter went through neither: it set PlayerReply, queued her answer, and left
         // nothing whatever on her profile. So she held four memories of writing and none of being answered,
         // and silence is what she spoke from.
         //
         // LetterMemoryFinalizer has carried the npcIsSender:false direction since it was written; its own
         // summary says that direction was never wired. It is wired here. Recorded BEFORE the generation and
         // not inside it, for the reason that class already states about its base event: a guaranteed capture
         // must not depend on an LLM call returning.
         RecordThatThePlayerWroteBack(profile, originalLetter, today);

         _ = GenerateReplyAsync(profile, giverHero, originalLetter, llmServices, store, today)
            .ContinueWith(t =>
            {
               if (t.Exception != null)
                  ModLog.Error($"[LETTER] reply generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
            }, TaskScheduler.Default);
      }

      /// <summary>
      ///   Fire-and-forget entry point triggered when a player letter is delivered.
      ///   The LLM decides whether to reply; if yes, it picks a delay (1-7 days) and
      ///   writes the letter body. A <see cref="PendingLetter" /> is stored with
      ///   <see cref="PendingLetter.DispatchDay" /> set; the reply courier is spawned
      ///   on the main-thread daily tick once that day arrives.
      /// </summary>
      public static void QueueReplyToPlayerLetter(
         NpcProfile profile,
         Hero senderHero,
         PlayerLetter playerLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         _ = GeneratePlayerLetterReplyAsync(profile, senderHero, playerLetter, llmServices, store, today)
            .ContinueWith(t =>
            {
               if (t.Exception != null)
                  ModLog.Error($"[LETTER] player-letter reply generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
            }, TaskScheduler.Default);
      }

      #region private

      /// <summary>
      ///   Files, on the NPC's own profile, the fact that the player answered their letter. Fail-soft: a
      ///   correspondence that cannot be remembered must still be sent.
      /// </summary>
      private static void RecordThatThePlayerWroteBack(NpcProfile profile, PendingLetter originalLetter, int today)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            string reply = originalLetter?.PlayerReply;

            if (profile == null || behavior == null || string.IsNullOrWhiteSpace(reply)) return;

            LetterMemoryFinalizer.RecordLetterMemory(
               profile, CalradiaRemembers.Logic.Letters.LetterReplyReason.Of(originalLetter!.Reason), reply, false, today, behavior);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LETTER] could not file the player's reply on {profile?.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }


      /// <summary>
      ///   Calls the LLM via the existing ChatService pipeline with a fresh empty
      ///   session so the letter-generation exchange is never mixed into the NPC's
      ///   real conversation history. Returns only <c>response.Dialogue</c>.
      /// </summary>
      private static async Task<string> CallLlmForLetterAsync(
         LlmServices llmServices,
         NpcProfile profile,
         string generationMessage,
         int? asOfDay = null,
         int? historyKnownThrough = null)
      {
         try
         {
            // Build the prompt from a SNAPSHOT taken here on the calling thread. The off-thread
            // ChatAsync below enumerates the profile's lists (events, letters, relationships); the
            // main thread mutates those same lists from ticks, world-event feeders and ProfileMutator
            // (e.g. right after a letter reply is queued). Enumerating a live list while another thread
            // mutates it is what threw "Collection was modified" during letter reply — the snapshot is
            // never mutated, so the race is gone.
            NpcProfile promptProfile = CloneForPrompt(profile);

            // A letter used to be written from EncounterContext.Empty, which means the writer was told
            // nothing about what the player IS to them. zhenja1zhenja1 (17/09/2026) was written to as a man
            // riding in a lord's retinue by a lord he had merely beaten in a tournament. The standing is the
            // one fact a letter cannot do without, and it is read on THIS thread with the rest of the
            // snapshot, because the bridge reads live campaign state.
            EncounterContext standing = EncounterContextBuilder.StandingContextFor(profile, historyKnownThrough);

            var emptySession = new ChatSession();
            int letterDay = asOfDay ?? (int) CampaignTime.Now.ToDays;
            var world = new WorldState {
               // An envelope is written as of the day it was sent, not the day it is opened (31/10/2026).
               CurrentDay = letterDay,
               // bdionysis80 (Nexus, 12/11/2026): "current date Summer 9 1084 while receiving letters with timestamp
               // summer 1085". Given the day count alone, the writer worked out a year one too many; it gets the date.
               Season = ChronicleCalendar.GameDateOf(letterDay),
               ActiveConflicts = ""
            };

            // fkasad (15/10/2026): on a local server a letter waits for the open conversation to close, before its
            // timeout starts. The prompt above is already built, so nothing here reads the game off its thread.
            await BackgroundCallHold.WhileConversationOpenAsync(LlmServices.MainClientRunsLocally, $"letter from {profile.Name}");

            NpcChatResult result;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(LetterLlmTimeoutSeconds)))
            {
               CancellationToken token = cts.Token;
               result = await Task.Run(
                  () => llmServices.ChatService.ChatAsync(
                     promptProfile, world, emptySession, generationMessage,
                     standing, token),
                  token).ConfigureAwait(false);
            }

            // WHAT A LETTER COSTS, which nothing reported until now: the path logged its failures and never
            // its price, so "is a dedicated letter path worth building?" had no evidence behind it either way.
            // Measured 17/09/2026: a letter prompt is 32,461 chars against a conversation's 33,539 — 97%, not
            // the fraction EncounterContext.Empty suggests, because the format contract, the action catalogue,
            // the persona and the prose craft are all unconditional. It carried no CURRENT ENCOUNTER marker
            // either, so PromptCacheSplit found no prefix and the whole prompt was written as one cached
            // block that a one-shot call would never read back; the marker is emitted unconditionally since
            // 18/09/2026, so a letter now has a prefix like any other prompt.
            //
            // The frequency is the other half and only play can give it. This line is what makes both
            // countable in a real session before any content decision is taken.
            if (result.Usage != null)
               ModLog.Info($"  [LETTER TOKENS] prompt={result.Usage.PromptTokens} "
                         + $"(cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}");

            if (result.IsSuccess) return result.Response?.Dialogue;

            ModLog.Error($"[LETTER] LLM call failed: {result.ErrorMessage}");

            return null;
         }
         catch (System.OperationCanceledException)
         {
            // A slow model/provider that exceeds the timeout, not a fault: the letter is simply skipped, so this
            // is INFO, not a red ERROR. (TaskCanceledException derives from OperationCanceledException.)
            ModLog.Info($"[LETTER] generation timed out after {LetterLlmTimeoutSeconds}s (slow LLM response); letter skipped.");

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LETTER] CallLlmForLetterAsync threw: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   How long a single letter-generation LLM call may run before it is cancelled and the letter skipped: the
      ///   player's "LLM Response Timeout", never under a minute (CarolusIV, 05/10/2026: it was a fixed 60 seconds).
      /// </summary>
      private static int LetterLlmTimeoutSeconds => Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds);

      /// <summary>
      ///   A deep copy of the profile for read-only prompt building, so the off-thread LLM call never
      ///   enumerates lists the main thread is mutating. Falls back to the live profile if the clone
      ///   fails (no worse than the previous behaviour).
      /// </summary>
      private static NpcProfile CloneForPrompt(NpcProfile profile)
      {
         try { return JsonConvert.DeserializeObject<NpcProfile>(JsonConvert.SerializeObject(profile)) ?? profile; }
         catch { return profile; }
      }

      /// <summary>
      ///   Delivery delay in days, based on travel distance between the NPC and the
      ///   player. Minimum 1 day, maximum 12 days.
      /// </summary>
      /// <summary>Map distance a courier covers in one day — the single distance-to-days conversion (Tuning).</summary>
      private const float DistancePerTravelDay = 20f;

      /// <summary>
      ///   Raw courier travel time in DAYS from <paramref name="sender" /> to the player, or -1 when a position
      ///   cannot be read. The one place distance becomes days: <see cref="ComputeDeliveryDays" /> clamps it for
      ///   a courier's journey, and the letter-presence gate compares it to a day-count buffer (a time-in-days
      ///   is a far more meaningful distance measure than raw map units).
      /// </summary>
      internal static float TravelDaysTo(Hero sender)
      {
         try
         {
            MobileParty playerParty = MobileParty.MainParty;
            if (sender == null || playerParty == null) return -1f;

            Vec2 playerPos = playerParty.GetPosition2D;

            MobileParty senderParty = sender.PartyBelongedTo;
            if (senderParty != null) return senderParty.GetPosition2D.Distance(playerPos) / DistancePerTravelDay;

            var at = sender.CurrentSettlement ?? sender.StayingInSettlement;
            if (at != null) return at.GetPosition2D.Distance(playerPos) / DistancePerTravelDay;

            return -1f;
         }
         catch
         {
            return -1f;
         }
      }

      internal static int ComputeDeliveryDays(Hero giver)
      {
         float days = TravelDaysTo(giver);

         return days < 0f ? 3 : Math.Max(1, Math.Min(12, (int) days));
      }

      /// <summary>
      ///   Sends an envelope: everything the text will be written from is composed NOW, on the day it is sent (the occasion,
      ///   where the sender and the player are, the sender's mood), with the sender's memories counted so the text never
      ///   knows what came after. The sender remembers writing it from today, as every letter's sender does since 26/09.
      /// </summary>
      private static void SealEnvelope(NpcProfile profile, Hero giverHero, LetterReason reason, string triggerContext,
         BannerlordNpcMemoryStore store, int today, string writtenBy, string offerTargetId, int offerAmount)
      {
         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         var envelope = new PendingLetter {
            WrittenBy = writtenBy,
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = reason,
            Content = null,
            WrittenOnOpening = true,
            EnvelopeMessage = LetterPromptBuilder.BuildInitialLetterMessage(
               profile, reason, triggerContext, playerName, WhereaboutsFor(profile), PlayerSituationFor(profile), writtenBy,
               Mapping.DayMoodReader.Draw(giverHero).Mood),
            EnvelopeKnownThrough = profile.Events?.Count,
            SentOnDay = today,
            DispatchDay = today,
            DeliveryDay = today + ComputeDeliveryDays(giverHero),
            IsDelivered = false,
            OfferTargetId = offerTargetId,
            OfferAmount = offerAmount
         };
         profile.SentLetters.Add(envelope);
         LetterMemoryFinalizer.RememberSending(profile, envelope, today, NpcMemoryCampaignBehavior.Instance);
         store.Set(profile);
         ModLog.Info($"[LETTER] envelope sealed by {profile.Name} ({reason}); its text is written when opened.");
      }

      /// <summary>
      ///   Writes an envelope's text, as of the day it was sent and from what its sender knew then. Starts on the game thread
      ///   (the prompt is read there); the text comes back off it, or null when the model did not answer.
      /// </summary>
      public static Task<string> WriteEnvelopeAsync(PendingLetter letter, NpcProfile profile)
      {
         LlmServices llm = NpcMemoryCampaignBehavior.Instance?.LlmServices;
         if (letter == null || profile == null || llm?.ChatService == null || string.IsNullOrWhiteSpace(letter.EnvelopeMessage))
            return Task.FromResult<string>(null);

         return CallLlmForLetterAsync(llm, profile, letter.EnvelopeMessage, letter.SentOnDay, letter.EnvelopeKnownThrough);
      }

      /// <summary>
      ///   Translates a fixed English letter into the reply language (RaveN017Mod, 29/10/2026). Starts on the game thread, where
      ///   the writer and the reader are read; the text comes back off it, or null when the model did not answer. The prose
      ///   client, as the chronicle: the player reads this. A self-test hands in its own client.
      /// </summary>
      public static Task<string> TranslateFixedLetterAsync(PendingLetter letter, NpcProfile profile, NpcMemoryService.Core.LlmClient.ILlmClient client = null)
      {
         NpcMemoryService.Core.LlmClient.ILlmClient translatorClient = client ?? NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
         if (letter == null || profile == null || translatorClient == null) return Task.FromResult<string>(null);

         var request = new FixedLetterTranslation {
            Text = letter.Content,
            Language = ModSettings.ReplyLanguage,
            WriterName = letter.SenderName ?? profile.Name,
            WriterIsFemale = Hero.FindFirst(h => h.StringId == profile.Id)?.IsFemale,
            ReaderIsFemale = Hero.MainHero?.IsFemale
         };

         return TranslateOffThreadAsync(new FixedLetterTranslator(translatorClient), request, waitForConversation: client == null);
      }

      private static async Task<string> TranslateOffThreadAsync(FixedLetterTranslator translator, FixedLetterTranslation request, bool waitForConversation)
      {
         try
         {
            if (waitForConversation) await BackgroundCallHold.WhileConversationOpenAsync(LlmServices.MainClientRunsLocally, $"translating a letter from {request.WriterName}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(LetterLlmTimeoutSeconds));
            CancellationToken token = cts.Token;

            return await Task.Run(() => translator.TranslateAsync(request, token), token).ConfigureAwait(false);
         }
         catch (System.OperationCanceledException)
         {
            ModLog.Info($"[LETTER] translation timed out after {LetterLlmTimeoutSeconds}s; the letter stays in English.");

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LETTER] translation failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static async Task GeneratePlayerLetterReplyAsync(
         NpcProfile profile,
         Hero senderHero,
         PlayerLetter playerLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         if (llmServices?.ChatService == null) return;
         if (string.IsNullOrWhiteSpace(playerLetter.Content)) return;

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string writtenBy = CorrespondenceRank.WrittenByFor(senderHero, profile, LetterReason.PlayerLetterReply);
         // Gabriel (16/10/2026): a patrolling companion is stopped by letter; read on the game thread, before any await.
         bool onPatrol = Missions.CompanionPatrolBehavior.Instance?.IsPatrolling(profile.Id) == true;
         string message = LetterPromptBuilder.BuildPlayerLetterReplyDecisionMessage(
            profile, playerLetter.Content ?? "", playerName, WhereaboutsFor(profile), PlayerSituationFor(profile), writtenBy,
            Mapping.DayMoodReader.Draw(senderHero).Mood, onPatrol);

         string rawDecision = await CallLlmForLetterAsync(llmServices, profile, message);

         if (string.IsNullOrWhiteSpace(rawDecision)) return;

         // The letter has reached them: a stop order is obeyed now, whether or not they write back.
         if (onPatrol && PatrolLetterText.OrdersStop(rawDecision))
            LetterMutationQueue.Post(() => Missions.CompanionPatrolBehavior.Instance?.ObeyLetterToStop(profile.Id));
         rawDecision = PatrolLetterText.StripOrderLine(rawDecision);

         // Lordfadooboo (28/09/2026): the reader's judgement of the letter moves their regard, reply or not (an
         // insult is the letter most often passed over, and the one most worth judging).
         LetterTone tone = LetterRegardPolicy.ParseTone(rawDecision);
         LetterMutationQueue.Post(() =>
         {
            LetterRegardService.Apply(profile, tone, today);
            store.Set(profile);
         });

         // 5.1: PASS-vs-DELAY:N parsing lives in the pure, unit-tested LetterReplyDecisionParser now.
         LetterReplyDecision decision = LetterReplyDecisionParser.Parse(rawDecision);
         if (decision.Kind == LetterReplyKind.Pass)
         {
            string reasonSuffix = string.IsNullOrEmpty(decision.PassReason)
               ? ""
               : $", {decision.PassReason}";
            ModLog.Info($"[LETTER] {profile.Name} chose not to reply{reasonSuffix}");

            return;
         }

         // B1: the marker (if any) rides the SAME raw text as the DELAY/body parse above, so it must be stripped
         // out of the body before it is ever sent: it is metadata for the courier system, never letter prose.
         string letterBody = LetterRegardPolicy.StripToneLine(StripCorrespondencePreferenceLine(decision.Body));
         if (string.IsNullOrWhiteSpace(letterBody)) return;

         // B1: an explicit "write me less/more" stated in this reply. Parsed from the FULL raw text (not just
         // decision.Body) since the marker sits on its own trailing line within it.
         CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(rawDecision);

         int dispatchDay = today + decision.DelayDays;
         int travelDays = ComputeDeliveryDays(senderHero);

         var reply = new PendingLetter {
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = LetterReason.PlayerLetterReply,
            Content = letterBody,
            WrittenBy = writtenBy,
            SentOnDay = today,
            DispatchDay = dispatchDay,
            DeliveryDay = dispatchDay + travelDays, // fallback if courier lost before spawning
            IsDelivered = false
         };

         // 3.1: thread-pool continuation; apply the list/store mutation on the main thread.
         LetterMutationQueue.Post(() =>
         {
            profile.SentLetters.Add(reply);

            // B1: this answers a letter the PLAYER composed (PlayerLetter carries no LetterReason of its own),
            // so there is no NPC-authored "original letter" to attribute the subject nudge to; PlayerLetterReply
            // itself is the only reachable subject bucket here. Routed through CorrespondenceSignalService, the
            // single seam this reply path shares with the letters_mailbox self-test.
            bool applied = CorrespondenceSignalService.ApplyPreference(
               profile, NpcMemoryCampaignBehavior.Instance?.LetterEvaluator, pref, LetterReason.PlayerLetterReply);
            if (applied)
               ModLog.Info($"[LETTER] {profile.Name} stated a correspondence preference: scope={pref.Scope} direction={pref.Direction} strength={pref.Strength}.");

            store.Set(profile);
            ModLog.Info($"[LETTER] {profile.Name} will reply to player's letter, courier departs day {dispatchDay} ({decision.DelayDays}d delay).");
         });
      }

      /// <summary>
      ///   B1: strips a trailing <see cref="CorrespondencePreferenceParser.Marker" /> control line out of a
      ///   reply's letter body, so metadata meant for the courier system never leaks into the delivered letter
      ///   the player actually reads.
      /// </summary>
      private static string StripCorrespondencePreferenceLine(string text)
      {
         if (string.IsNullOrEmpty(text)) return text;

         string[] lines = text.Split('\n');
         var kept = new List<string>(lines.Length);
         foreach (string line in lines)
            if (!line.TrimStart().StartsWith(CorrespondencePreferenceParser.Marker, StringComparison.OrdinalIgnoreCase))
               kept.Add(line);

         return string.Join("\n", kept).TrimEnd();
      }

      private static async Task GenerateReplyAsync(
         NpcProfile profile,
         Hero giverHero,
         PendingLetter originalLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         if (llmServices?.ChatService == null) return;

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         // IdentityCrysys (08/10/2026): a reply to the "why no reply?" follow-up is about the thread, not the silence the
         // player has just broken; with the follow-up's reason, every reply after it reproached the player again.
         LetterReason replyReason = CalradiaRemembers.Logic.Letters.LetterReplyReason.Of(originalLetter.Reason);
         string writtenBy = CorrespondenceRank.WrittenByFor(giverHero, profile, replyReason);
         bool onPatrol = Missions.CompanionPatrolBehavior.Instance?.IsPatrolling(profile.Id) == true;
         string message = LetterPromptBuilder.BuildReplyLetterMessage(
            profile, originalLetter.PlayerReply ?? "", replyReason, playerName, WhereaboutsFor(profile), PlayerSituationFor(profile), writtenBy,
            Mapping.DayMoodReader.Draw(giverHero).Mood, onPatrol,
            originalLetter.Reason == LetterReason.FromAnotherMod ? originalLetter.Content : null);

         string raw = await CallLlmForLetterAsync(llmServices, profile, message);

         if (string.IsNullOrWhiteSpace(raw)) return;

         // The player answered the monthly letter: "come back" rides the courier (Gabriel, 16/10/2026).
         if (onPatrol && PatrolLetterText.OrdersStop(raw))
            LetterMutationQueue.Post(() =>
            {
               Hero companion = Hero.FindFirst(h => h.StringId == profile.Id);
               if (companion != null) Missions.CompanionPatrolBehavior.Instance?.OrderRecallByLetter(companion);
            });
         raw = PatrolLetterText.StripOrderLine(raw);

         // Lordfadooboo (28/09/2026): the player's answer to this character's letter is judged too.
         LetterTone tone = LetterRegardPolicy.ParseTone(raw);
         string content = LetterRegardPolicy.StripToneLine(raw);
         if (string.IsNullOrWhiteSpace(content)) return;

         int deliveryDays = ComputeDeliveryDays(giverHero);
         var reply = new PendingLetter {
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = replyReason,
            Content = content,
            WrittenBy = writtenBy,
            SentOnDay = today,
            DispatchDay = today,
            DeliveryDay = today + deliveryDays,
            IsDelivered = false
         };

         // 3.1: thread-pool continuation; apply the list/store mutation on the main thread.
         LetterMutationQueue.Post(() =>
         {
            profile.SentLetters.Add(reply);
            LetterRegardService.Apply(profile, tone, today);
            store.Set(profile);
            ModLog.Info($"[LETTER] reply queued from {profile.Name}, courier dispatches day {today}.");
         });
      }

      #endregion
   }
}