// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner exchange, increment 3b: the ENVOY audience (fkasad, Nexus, 25/09/2026; Gabriel: always an envoy, never the
// lord in person; anonymous; announced by a popup). A pipeline of its own, as the council has, because the 1:1 chat
// prompt is built from a hero and would make the envoy speak as his lord. The chat has no hero here (_npcHero stays
// null, so no memory summary, no resume, no witnesses latch onto anyone); the envoy is a name and a troop's face. The
// LORD stays behind the scenes: a deal the envoy settles runs through the bridge in the lord's name, with the lord's
// gold and prisons, and the lord remembers it. Whatever the model writes is first rebound to the lord's mandate
// (EnvoyMandatePolicy), so the envoy can never settle more than he was sent to.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>What an envoy audience carries: whose envoy, and the mandate he was given.</summary>
   public sealed class EnvoyAudience
   {
      /// <summary>The lord who sent him.</summary>
      public string LordId { get; set; } = "";

      /// <summary>The lord's realm or house, for the envoy's introduction.</summary>
      public string LordRealm { get; set; }

      /// <summary>What the wanted captive is to the lord, or null.</summary>
      public string WantedStake { get; set; }

      /// <summary>True when the lord wants the captive back and free; false when he wants him in his own custody.</summary>
      public bool WantsCaptiveBack { get; set; } = true;

      /// <summary>An envoy about this captive was already sent away: this one brings a better offer (fkasad, 12/10/2026).</summary>
      public bool ComesAgainWithMore { get; set; }

      /// <summary>What the envoy offers first (half his ceiling, never below the honest floor), or 0 on an older save.</summary>
      public int OpeningOffer { get; set; }

      /// <summary>The gold that would even the offered exchange, seen from the player.</summary>
      public int ExchangeGoldToPlayer { get; set; }

      /// <summary>What the lord authorised.</summary>
      public EnvoyMandate Mandate { get; set; } = new();

      /// <summary>Set once a deal is settled; the envoy settles one and no more.</summary>
      public bool Settled { get; set; }
   }

   public sealed partial class ChatViewModel
   {
      private EnvoyAudience _envoy;

      /// <summary>
      ///   The last envoy prompt sent, for cr.prompt_dump: the envoy is no hero, so the dump has nobody to rebuild it
      ///   from (fkasad, 26/09/2026: "no such character, and nobody is being spoken to" mid-audience).
      /// </summary>
      internal static string LastEnvoyPrompt { get; private set; }

      /// <summary>True while this chat is an envoy's audience.</summary>
      internal bool IsEnvoyAudience => _envoy != null;

      /// <summary>
      ///   Opens the audience: the envoy asked for it, so he speaks first. The chat's name and troop face are set by
      ///   the opener (ChatManager.OpenForEnvoyAudience) before this.
      /// </summary>
      public void ActivateEnvoyAudience(EnvoyAudience envoy)
      {
         if (envoy == null) return;

         _mode = ChatMode.Conversation;
         _envoy = envoy;
         OnPropertyChanged(nameof(IsInviteVisible));

         ChatMessageViewModel placeholder = MakeSpeakerMessage(EnvoySpeaker(), PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunEnvoyTurnAsync(null, placeholder, isOpener: true);
      }

      private ChatSpeaker EnvoySpeaker() => new() {IsMain = true, Hero = null, Name = _npcName, Profile = null};

      /// <summary>The player spoke to the envoy.</summary>
      private void ExecuteSendEnvoyMessage(string text)
      {
         AppendPlayerMessage(text);
         IsSendEnabled = false;
         InputText = "";
         if (_isComposeOpen) IsComposeOpen = false;

         ChatMessageViewModel placeholder = MakeSpeakerMessage(EnvoySpeaker(), PlaceholderText);
         _messages.Add(placeholder);
         _ = RunEnvoyTurnAsync(text, placeholder, isOpener: false);
      }

      /// <summary>One envoy turn: his own prompt, one call, his line, and at most one deal run in his lord's name.</summary>
      private async Task RunEnvoyTurnAsync(string playerMessage, ChatMessageViewModel placeholder, bool isOpener)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         try
         {
            Hero lord = Hero.FindFirst(h => h != null && h.StringId == _envoy.LordId);
            if (behavior?.LlmServices?.LlmClient == null || lord == null)
            {
               ReplacePlaceholder(placeholder, "(The envoy bows, and says nothing more.)", EnvoySpeaker());

               return;
            }

            NpcProfile lordProfile = behavior.Store?.Get(lord.StringId);
            var input = new EnvoyPromptInput {
               EnvoyName = _npcName,
               LordName = lord.Name?.ToString() ?? "",
               LordRealm = _envoy.LordRealm,
               PlayerName = Hero.MainHero?.Name?.ToString() ?? "",
               WantedCaptiveName = _envoy.Mandate.WantedCaptiveName,
               WantedCaptiveStake = _envoy.WantedStake,
               WantsCaptiveBack = _envoy.WantsCaptiveBack,
               RansomCeiling = _envoy.Mandate.RansomCeiling,
               OpeningOffer = _envoy.OpeningOffer,
               ComesAgainWithMore = _envoy.ComesAgainWithMore,
               ExchangeCaptiveName = string.IsNullOrWhiteSpace(_envoy.Mandate.ExchangeCaptiveName) ? null : _envoy.Mandate.ExchangeCaptiveName,
               ExchangeGoldToPlayer = _envoy.ExchangeGoldToPlayer,
               LordRegardWords = EnvoyMandatePolicy.RegardWords(RegardBands.Describe(lordProfile?.ReputationWithPlayer ?? 0)),
               IsOpener = isOpener,
               TranscriptSoFar = EnvoyTranscriptLines()
            };

            string systemPrompt = EnvoyPromptBuilder.Build(input);
            LastEnvoyPrompt = systemPrompt;
            ModLog.Info($"[ENVOY] turn for {lord.Name}'s envoy ({(isOpener ? "opening" : "reply")}, prompt {systemPrompt.Length} chars).");
            if (ModSettings.VerboseLogging)
            {
               ModLog.Info("=== ENVOY PROMPT START ===");
               ModLog.Info(systemPrompt);
               ModLog.Info("=== ENVOY PROMPT END ===");
            }

            // The chat's own ceiling, not a small one: a reasoning model spends most of a small budget thinking and
            // returns no reply at all (fkasad, 26/09/2026, on a local reasoning model: 700 left "the envoy hesitates"
            // on every turn while the server showed a finished thought). A ceiling, not a target.
            var request = new LlmRequest {
               SystemPrompt = systemPrompt,
               Messages = new[] {new LlmMessage(MessageRole.User, isOpener ? "(The envoy is admitted, and speaks first.)" : playerMessage)},
               Parameters = new LlmParameters {MaxTokens = 3000, Creativity = ModSettings.ChatTemperature}
            };

            string raw = null;
            string error = null;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token))
            {
               cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 10));
               try
               {
                  LlmResponse resp = await Task.Run(() => behavior.LlmServices.LlmClient.CompleteAsync(request, cts.Token)).ConfigureAwait(false);
                  if (resp is {IsSuccess: true}) raw = resp.Content;
                  else error = resp?.ErrorMessage ?? "no result";
                  if (resp is {IsSuccess: true} && string.IsNullOrWhiteSpace(raw))
                     error = $"the model returned an empty reply (finish_reason: {resp.FinishReason ?? "unknown"}); a reasoning model may have spent its budget thinking";
               }
               catch (Exception ex) { error = $"{ex.GetType().Name}: {ex.Message}"; }
            }

            // Back on the game's own thread: the deal below moves gold and prisoners through the engine.
            await MainThreadDispatcher.SwitchToMainThread();
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return; // the audience was closed mid-call

            if (string.IsNullOrWhiteSpace(raw))
            {
               ModLog.Error($"[ENVOY] turn failed: {error ?? "empty reply"}");
               ReplacePlaceholder(placeholder, "(The envoy hesitates, and waits for you to speak.)", EnvoySpeaker());

               return;
            }

            ParsedResponse parsed = new SectionResponseParser().Parse(raw);
            string line = string.IsNullOrWhiteSpace(parsed.Dialogue) ? "(The envoy inclines their head.)" : parsed.Dialogue;

            _messages.Remove(placeholder);
            if (!string.IsNullOrWhiteSpace(parsed.Narration)) AppendModelNarration(parsed.Narration, line);
            _messages.Add(MakeSpeakerMessage(EnvoySpeaker(), line));
            RecordTranscript(false, _npcName, null, line);
            StoryLog.WriteNpcLine(_npcName, line);

            SettleEnvoyDeal(parsed.Actions, lord, lordProfile, behavior, isOpener ? null : playerMessage);

            ChatManager.ScrollToBottom();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ENVOY] RunEnvoyTurnAsync failed: {ex.GetType().Name}: {ex.Message}");
            try { ReplacePlaceholder(placeholder, "(The envoy falls silent.)", EnvoySpeaker()); }
            catch (Exception inner) { ModLog.Error($"[ENVOY] placeholder cleanup failed: {inner.Message}"); }
         }
         finally
         {
            IsSendEnabled = true;
         }
      }

      /// <summary>
      ///   Runs at most one deal, rebound to the mandate, through the bridge in the LORD's name (his gold, his prisons,
      ///   his profile for the memory the verb writes). Anything outside the mandate is logged and ignored.
      /// </summary>
      private void SettleEnvoyDeal(IReadOnlyList<GameAction> actions, Hero lord, NpcProfile lordProfile, NpcMemoryCampaignBehavior behavior,
                                   string playerLine)
      {
         if (actions == null || _envoy.Settled || behavior?.GameStateBridge == null) return;

         if (lordProfile == null)
         {
            lordProfile = HeroProfileMapperShim(lord);
            if (lordProfile == null) return;
         }

         foreach (GameAction written in actions)
         {
            // What this turn said (tashmetu, 27/09/2026): a gift is the player's to make in so many words, and a plain
            // acceptance takes the envoy's last offer.
            EnvoyDeal deal = EnvoyMandatePolicy.Bind(written?.Type, written?.Parameters, _envoy.Mandate,
               new EnvoyTurnFacts {PlayerLine = playerLine, LastOfferNamed = LastEnvoyOffer()});
            if (deal == null)
            {
               ModLog.Info($"[ENVOY] ignored '{written?.Type}': outside the envoy's mandate.");

               continue;
            }

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = deal.Type, Parameters = deal.Parameters}, lord, new ActionContext(lordProfile, null));

            if (outcome == null) continue;

            AppendSystemMessage(outcome.Message, outcome.IsPositive && !outcome.WasRefused);
            ModLog.Info($"[ENVOY] {deal.Type} for {lord.Name}: {(outcome.WasRefused ? "refused" : "settled")} ({outcome.Message}).");

            if (outcome.WasRefused) continue;

            _envoy.Settled = true;
            behavior.Store.Set(lordProfile);

            return;
         }
      }

      /// <summary>The lord's profile, created if the player has never met him (an envoy can come from a stranger).</summary>
      private static NpcProfile HeroProfileMapperShim(Hero lord)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile profile = behavior?.Store?.Get(lord.StringId);
            if (profile != null) return profile;

            profile = Mapping.HeroProfileMapper.FromHero(lord);
            behavior?.Store?.Set(profile);

            return profile;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ENVOY] could not create {lord?.Name}'s profile: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The last sum the envoy named in this parley (its own lines, newest first), or null.</summary>
      private int? LastEnvoyOffer()
      {
         for (int i = _transcript.Lines.Count - 1; i >= 0; i--)
         {
            var l = _transcript.Lines[i];
            if (l == null || l.IsPlayer || l.IsNarration || string.IsNullOrWhiteSpace(l.Text)) continue;

            int? figure = SpokenFigure.LastIn(l.Text, 100);
            if (figure != null) return figure;
         }

         return null;
      }

      /// <summary>The parley so far, "Name: line", bounded like the council's.</summary>
      private IReadOnlyList<string> EnvoyTranscriptLines()
      {
         const int maxLines = 20;
         var lines = _transcript.Lines
                                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Text))
                                .Select(l => $"{(l.IsNarration ? "(scene)" : l.IsPlayer ? "You" : l.SpeakerName ?? "The envoy")}: {l.Text}")
                                .ToList();

         return lines.Count > maxLines ? lines.GetRange(lines.Count - maxLines, maxLines) : lines;
      }

      /// <summary>
      ///   On close: a deal already left its memory on the lord (the verb wrote it). An audience that ended without one
      ///   still happened, and the lord knows his envoy came back empty-handed.
      /// </summary>
      private void FinalizeEnvoyAudience()
      {
         try
         {
            if (_envoy == null || _envoy.Settled) return;

            Hero lord = Hero.FindFirst(h => h != null && h.StringId == _envoy.LordId);
            NpcProfile profile = lord == null ? null : HeroProfileMapperShim(lord);
            if (profile == null) return;

            profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Other,
               $"Sent an envoy under a flag of truce to treat with {Hero.MainHero?.Name} for {_envoy.Mandate.WantedCaptiveName}; no bargain was struck."));
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ENVOY] closing memory failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
