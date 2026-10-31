// Code written by Gabriel Mailhot, 31/10/2026.
// The killing attempt, played inside the chat (zhenja1, Nexus, 31/10/2026: he confirmed a killing twice and the man
// lived, because the roll had failed and nothing on screen said there was one). Gabriel (31/10/2026): the window names
// the chance; striking plays a roll, "only the number changing quickly until it stops on the score"; then the model
// writes the scene, the kill or the survival, and it goes into the conversation. After a kill only leaving remains; a
// captive who kills the captor escapes; after a failure the character answers it.
//
// The overlay lives in the chat's own prefab (CalradiaRemembersKillRoll, in both chat skins), not on a layer of its
// own: a layer above the chat would make the focus arbiter slide the chat away. The deed is applied the moment the
// player strikes, before any animation or narration, so closing the chat mid-roll neither loses the deed nor repeats
// it; the roll and the scene only show what already happened.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Violence;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      /// <summary>How long the landed number and its verdict stay before the overlay gives way to the scene. Tuning.</summary>
      private const float KillRollLandedSeconds = 1.8f;

      private sealed class KillAttemptState
      {
         public Hero Target;
         public ViolenceFacts Facts;
         public int Chance;
         public Func<int, KillAttemptResult> Strike;
         public IReadOnlyList<KillRollFrame> Frames;
         public int FrameIndex;
         public float FrameElapsed;
         public float LandedElapsed;
         public bool Landed;
         public bool OverlayClosed;
         public KillAttemptResult Result;
         public bool PlayerIsCaptive;
         public string Narration;
         public ChatMessageViewModel Placeholder;
      }

      private KillAttemptState _killAttempt;
      private bool _isKillRollVisible, _isKillRollConfirming, _isKillRollLanded, _isKillRollSucceeded, _killRollCanStrike;
      private string _killRollTitle = "", _killRollChanceText = "", _killRollReasons = "", _killRollShownText = "";

      [DataSourceProperty] public bool IsKillRollVisible { get => _isKillRollVisible; set { if (_isKillRollVisible == value) return; _isKillRollVisible = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsKillRollShown)); } }

      /// <summary>What the overlay binds: open, and the chat not slid aside for a screen above it (the pause menu, the encyclopedia).</summary>
      [DataSourceProperty] public bool IsKillRollShown => _isKillRollVisible && ChatVisualOffsetX < 100f;

      /// <summary>The window asks before anything happens: Strike or Stay your hand.</summary>
      [DataSourceProperty] public bool IsKillRollConfirming { get => _isKillRollConfirming; set { if (_isKillRollConfirming == value) return; _isKillRollConfirming = value; OnPropertyChangedWithValue(value); } }

      /// <summary>The number has stopped: the verdict shows under it.</summary>
      [DataSourceProperty] public bool IsKillRollLanded { get => _isKillRollLanded; set { if (_isKillRollLanded == value) return; _isKillRollLanded = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsKillRollFailed)); } }

      [DataSourceProperty] public bool IsKillRollSucceeded { get => _isKillRollSucceeded; set { if (_isKillRollSucceeded == value) return; _isKillRollSucceeded = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsKillRollFailed)); } }

      /// <summary>Shown only once the number has landed, like its twin <see cref="IsKillRollSucceeded" />.</summary>
      [DataSourceProperty] public bool IsKillRollFailed => _isKillRollLanded && !_isKillRollSucceeded;

      /// <summary>Strike waits for the turn that raised the blade to finish, so the scene never interleaves with it.</summary>
      [DataSourceProperty] public bool KillRollCanStrike { get => _killRollCanStrike; set { if (_killRollCanStrike == value) return; _killRollCanStrike = value; OnPropertyChangedWithValue(value); } }

      [DataSourceProperty] public string KillRollTitle { get => _killRollTitle; set { if (_killRollTitle == value) return; _killRollTitle = value; OnPropertyChangedWithValue(value); } }

      [DataSourceProperty] public string KillRollChanceText { get => _killRollChanceText; set { if (_killRollChanceText == value) return; _killRollChanceText = value; OnPropertyChangedWithValue(value); } }

      [DataSourceProperty] public string KillRollReasons { get => _killRollReasons; set { if (_killRollReasons == value) return; _killRollReasons = value; OnPropertyChangedWithValue(value); } }

      [DataSourceProperty] public string KillRollShownText { get => _killRollShownText; set { if (_killRollShownText == value) return; _killRollShownText = value; OnPropertyChangedWithValue(value); } }

      /// <summary>True while a killing attempt is open in this chat, from the window to the end of its scene.</summary>
      public bool IsKillAttemptOpen => _killAttempt != null;

      /// <summary>Test seam (cr.test kill_roll, cr.kill_roll): the engine roll to use for the next strike instead of a random one.</summary>
      internal int? KillRollForcedRoll { get; set; }

      /// <summary>Test seam (cr.test kill_roll): tell the plain outcome instead of calling the model, and start no reaction turn.</summary>
      internal bool KillNarrationOffline { get; set; }

      /// <summary>What followed the last attempt told in this chat, for cr.test kill_roll to read.</summary>
      internal KillAftermath? LastKillAftermath { get; private set; }

      /// <summary>The character this chat speaks with, for cr.kill_roll.</summary>
      internal Hero KillRollPartner => _npcHero;

      /// <summary>
      ///   Opens the killing window over this chat, naming the chance. False when this chat cannot hold it (another
      ///   chat mode, the council's window, the conversation over, another attempt open, or a target who is not the
      ///   one being spoken to): the caller then asks the old way. <paramref name="strike" /> applies the deed for an
      ///   engine roll in [0, 100) and says how it ended.
      /// </summary>
      public bool OfferKillAttempt(Hero target, ViolenceFacts facts, int chance, Func<int, KillAttemptResult> strike)
      {
         if (target == null || strike == null || _killAttempt != null || _conversationEnded) return false;
         if (_mode != ChatMode.Conversation && _mode != ChatMode.CaptiveEncounter) return false;
         if (!ReferenceEquals(target, _npcHero) || !ChatManager.ActiveSkinHostsKillRoll) return false;

         _killAttempt = new KillAttemptState {
            Target = target, Facts = facts, Chance = chance, Strike = strike,
            PlayerIsCaptive = IsCaptiveEncounter && Hero.MainHero?.IsPrisoner == true
         };
         KillRollTitle = $"Take the life of {target.Name}?";
         KillRollChanceText = $"Chance: {chance} in 100";
         KillRollReasons = KillAttemptPolicy.ChanceReasons(facts) + " This cannot be undone, and the realm will answer for it.";
         KillRollShownText = "";
         IsKillRollLanded = false;
         IsKillRollSucceeded = false;
         IsKillRollConfirming = true;
         KillRollCanStrike = IsKillRollStrikeReady();
         IsKillRollVisible = true;
         ModLog.Info($"[VIOLENCE] kill_npc on {target.Name}: the window asks the player (chance {chance}%, exposure {facts?.Exposure}).");

         return true;
      }

      // The turn that raised the blade is over: input is back with the player, a captive scene waits on Continue, or it ended.
      private bool IsKillRollStrikeReady() => IsSendEnabled || _isScenePaused || _conversationEnded;

      /// <summary>The player keeps their hand: nothing happened at all.</summary>
      public void ExecuteKillRollStay()
      {
         KillAttemptState attempt = _killAttempt;
         if (attempt == null || !IsKillRollConfirming) return;

         ModLog.Info($"[VIOLENCE] kill_npc declined by the player; {attempt.Target?.Name} lives and nothing was recorded.");
         CloseKillRollOverlay();
         _killAttempt = null;
         AppendSystemNotice($"You stay your hand. {attempt.Target?.Name} lives.");
      }

      /// <summary>The player strikes: the game rolls and the deed is done now; the number then plays out to it.</summary>
      public void ExecuteKillRollStrike()
      {
         KillAttemptState attempt = _killAttempt;
         if (attempt == null || !IsKillRollConfirming || !IsKillRollStrikeReady()) return;

         IsKillRollConfirming = false;
         IsSendEnabled = false;
         if (_isScenePaused) IsScenePaused = false;

         int roll = KillRollForcedRoll ?? MBRandom.RandomInt(100);
         KillRollForcedRoll = null; // one strike only: a later real attempt in this chat rolls for real
         try { attempt.Result = attempt.Strike(roll); }
         catch (Exception ex) { ModLog.Error($"[VIOLENCE] the strike failed: {ex.GetType().Name}: {ex.Message}"); }

         if (attempt.Result == null)
         {
            // The deed could not be carried out (the target gone, the setting turned off): nothing happened.
            CloseKillRollOverlay();
            _killAttempt = null;
            IsSendEnabled = !_conversationEnded;

            return;
         }

         // A captive who killed the captor is free: committed now, applied by the game as the scene closes.
         if (attempt.Result.Killed && attempt.PlayerIsCaptive)
            NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.ResolvePlayerEscapeAttempt(true);

         int shown = KillAttemptPolicy.ShownRoll(roll);
         attempt.Frames = KillAttemptPolicy.Frames(shown, MBRandom.RandomInt(int.MaxValue));
         attempt.FrameIndex = 0;
         attempt.FrameElapsed = 0f;
         KillRollShownText = attempt.Frames[0].Value.ToString();
         ModLog.Info($"[VIOLENCE] kill_npc rolled {shown} against {attempt.Chance} (killed={attempt.Result.Killed}, captive={attempt.PlayerIsCaptive}).");

         if (KillNarrationOffline)
         {
            attempt.Narration = KillAttemptPolicy.FallbackNarration(NarrationFactsFor(attempt));

            return;
         }

         attempt.Placeholder = MakeSpeakerMessage(NarratorSpeaker(), PlaceholderText);
         _messages.Add(attempt.Placeholder);
         _ = NarrateKillAttemptAsync(attempt);
      }

      /// <summary>Called every frame by the chat panel: moves the rolling number, then hands over to the scene.</summary>
      public void TickKillRoll(float dt)
      {
         KillAttemptState attempt = _killAttempt;
         if (attempt == null) return;

         if (IsKillRollConfirming)
         {
            KillRollCanStrike = IsKillRollStrikeReady();

            return;
         }

         if (attempt.Frames == null || attempt.OverlayClosed) return;

         if (!attempt.Landed)
         {
            attempt.FrameElapsed += dt;
            while (!attempt.Landed && attempt.FrameElapsed >= attempt.Frames[attempt.FrameIndex].Seconds)
            {
               attempt.FrameElapsed -= attempt.Frames[attempt.FrameIndex].Seconds;
               attempt.FrameIndex++;
               KillRollShownText = attempt.Frames[attempt.FrameIndex].Value.ToString();
               if (attempt.FrameIndex == attempt.Frames.Count - 1)
               {
                  attempt.Landed = true;
                  IsKillRollSucceeded = attempt.Result.Killed;
                  IsKillRollLanded = true;
               }
            }

            return;
         }

         attempt.LandedElapsed += dt;
         if (attempt.LandedElapsed < KillRollLandedSeconds) return;

         attempt.OverlayClosed = true;
         CloseKillRollOverlay();
         if (attempt.Narration != null) FinishKillAttempt(attempt);
      }

      private void CloseKillRollOverlay()
      {
         IsKillRollVisible = false;
         IsKillRollConfirming = false;
      }

      // The scene, written by the model from the outcome the game decided; the plain outcome when it gives nothing.
      private KillNarrationFacts NarrationFactsFor(KillAttemptState attempt)
         => new KillNarrationFacts {
            PlayerName = Hero.MainHero?.Name?.ToString(),
            VictimName = attempt.Target?.Name?.ToString(),
            VictimLabel = attempt.Target == null ? null : HeroLabel.Describe(new HeroLabel.HeroNaming {
               Name = attempt.Target.Name?.ToString(), ClanName = attempt.Target.Clan?.Name?.ToString(), CultureName = attempt.Target.Culture?.Name?.ToString()
            }),
            VictimIsFemale = attempt.Target?.IsFemale == true,
            Killed = attempt.Result.Killed,
            Exposure = attempt.Facts?.Exposure ?? ViolenceExposure.Alone,
            PlayerIsCaptive = attempt.PlayerIsCaptive,
            RecentLines = _transcript.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Text))
               .Select(l => $"{(l.IsNarration ? "(scene)" : l.SpeakerName)}: {l.Text}").ToList()
         };

      private async Task NarrateKillAttemptAsync(KillAttemptState attempt)
      {
         KillNarrationFacts facts = NarrationFactsFor(attempt);
         string narration = null;
         try
         {
            ILlmClient client = NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
            if (client != null)
            {
               var request = new LlmRequest {
                  SystemPrompt = KillAttemptPolicy.NarratorInstructions,
                  Messages = new[] {new LlmMessage(MessageRole.User, KillAttemptPolicy.NarratorTask(facts))},
                  Parameters = new LlmParameters {MaxTokens = 3000, Creativity = 0.7f}
               };
               (string raw, string _, string error) = await CallNarratorAsync(client, request).ConfigureAwait(false);
               if (string.IsNullOrWhiteSpace(raw)) ModLog.Error($"[VIOLENCE] the attempt's scene was not written: {error ?? "empty reply"}");
               else if (DialogueHeuristics.LooksLikeRefusal(raw)) ModLog.Info("[VIOLENCE] the narrator refused the attempt's scene; the plain outcome is shown.");
               else narration = raw.Trim();
            }
         }
         catch (Exception ex) { ModLog.Error($"[VIOLENCE] the attempt's scene failed: {ex.GetType().Name}: {ex.Message}"); }

         await MainThreadDispatcher.SwitchToMainThread();
         if (!ReferenceEquals(ChatManager.ActiveViewModel, this) || !ReferenceEquals(_killAttempt, attempt))
         {
            // The chat closed during the roll: the deed stands, so at least say how it ended.
            if (!string.IsNullOrEmpty(attempt.Result?.Message)) InformationManager.DisplayMessage(new InformationMessage(attempt.Result.Message));

            return;
         }

         attempt.Narration = narration ?? KillAttemptPolicy.FallbackNarration(facts);
         if (attempt.OverlayClosed) FinishKillAttempt(attempt);
      }

      // The scene goes into the conversation, then what follows: the end, the escape, or the character's answer.
      private void FinishKillAttempt(KillAttemptState attempt)
      {
         if (attempt.Placeholder != null) _messages.Remove(attempt.Placeholder);
         // Under the test seam the scene is shown but never recorded: no transcript, story log or extension line
         // may say that a living lord was killed by a diagnostic.
         if (KillNarrationOffline) _messages.Add(new ChatMessageViewModel(attempt.Narration, SceneNarrationColor));
         else AppendNarration(attempt.Narration);
         _killAttempt = null;

         KillAftermath next = KillAttemptPolicy.Decide(new KillAftermathFacts {Killed = attempt.Result.Killed, PlayerIsCaptive = attempt.PlayerIsCaptive});
         ModLog.Info($"[VIOLENCE] kill_npc scene told; next: {next}.");
         LastKillAftermath = next;

         switch (next)
         {
            case KillAftermath.PlayerEscapes:
               EndSceneAfterEscape(null);

               return;
            case KillAftermath.ConversationEnds:
               _conversationEnded = true;
               IsSendEnabled = false;
               IsPrivateRequestVisible = false;
               CloseButtonText = "Leave";
               NotifyConversationEndedChanged();
               AppendSystemNotice($"{attempt.Target?.Name} is dead. There is no one left to speak with.");

               return;
            default:
               if (!BeginKillAttemptReaction(KillAttemptPolicy.ReactionHint(NarrationFactsFor(attempt)))) IsSendEnabled = !_conversationEnded;

               return;
         }
      }

      // The character answers the failed attempt. Unlike BeginOutcomeReactionTurn, a captive scene is allowed: the
      // case that prompted all this was a captor (zhenja1).
      private bool BeginKillAttemptReaction(string hint)
      {
         if (_conversationEnded || KillNarrationOffline || (_mode != ChatMode.Conversation && _mode != ChatMode.CaptiveEncounter)) return false;

         ChatSpeaker speaker = MainSpeaker();
         if (speaker?.Hero == null || speaker.Hero.IsDead) return false;

         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunLlmExchangeAsync(OutcomeReactionStimulus(hint), speaker, placeholder, suppressPlayerLine: true, isOutcomeReaction: true);

         return true;
      }
   }
}
