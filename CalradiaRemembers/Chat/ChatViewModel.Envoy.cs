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
               RansomCeiling = _envoy.Mandate.RansomCeiling,
               ExchangeCaptiveName = string.IsNullOrWhiteSpace(_envoy.Mandate.ExchangeCaptiveName) ? null : _envoy.Mandate.ExchangeCaptiveName,
               ExchangeGoldToPlayer = _envoy.ExchangeGoldToPlayer,
               LordRegardWords = EnvoyMandatePolicy.RegardWords(RegardBands.Describe(lordProfile?.ReputationWithPlayer ?? 0)),
               IsOpener = isOpener,
               TranscriptSoFar = EnvoyTranscriptLines()
            };

            var request = new LlmRequest {
               SystemPrompt = EnvoyPromptBuilder.Build(input),
               Messages = new[] {new LlmMessage(MessageRole.User, isOpener ? "(The envoy is admitted, and speaks first.)" : playerMessage)},
               Parameters = new LlmParameters {MaxTokens = 700, Creativity = 0.7f}
            };

            string raw = null;
            string error = null;
            using (var cts = new CancellationTokenSource())
            {
               cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 10));
               try
               {
                  LlmResponse resp = await Task.Run(() => behavior.LlmServices.LlmClient.CompleteAsync(request, cts.Token)).ConfigureAwait(false);
                  if (resp is {IsSuccess: true}) raw = resp.Content;
                  else error = resp?.ErrorMessage ?? "no result";
               }
               catch (Exception ex) { error = $"{ex.GetType().Name}: {ex.Message}"; }
            }

            // Back on the game's own thread: the deal below moves gold and prisoners through the engine.
            await MainThreadDispatcher.SwitchToMainThread();
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return; // the audience was closed mid-call

            if (string.IsNullOrWhiteSpace(raw))
            {
               ModLog.Error($"[ENVOY] turn failed: {error}");
               ReplacePlaceholder(placeholder, "(The envoy hesitates, and waits for you to speak.)", EnvoySpeaker());

               return;
            }

            ParsedResponse parsed = new SectionResponseParser().Parse(raw);
            string line = string.IsNullOrWhiteSpace(parsed.Dialogue) ? "(The envoy inclines their head.)" : parsed.Dialogue;

            _messages.Remove(placeholder);
            if (!string.IsNullOrWhiteSpace(parsed.Narration)) AppendNarration(parsed.Narration);
            _messages.Add(MakeSpeakerMessage(EnvoySpeaker(), line));
            RecordTranscript(false, _npcName, null, line);
            StoryLog.WriteNpcLine(_npcName, line);

            SettleEnvoyDeal(parsed.Actions, lord, lordProfile, behavior);

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
      private void SettleEnvoyDeal(IReadOnlyList<GameAction> actions, Hero lord, NpcProfile lordProfile, NpcMemoryCampaignBehavior behavior)
      {
         if (actions == null || _envoy.Settled || behavior?.GameStateBridge == null) return;

         if (lordProfile == null)
         {
            lordProfile = HeroProfileMapperShim(lord);
            if (lordProfile == null) return;
         }

         foreach (GameAction written in actions)
         {
            EnvoyDeal deal = EnvoyMandatePolicy.Bind(written?.Type, written?.Parameters, _envoy.Mandate);
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
