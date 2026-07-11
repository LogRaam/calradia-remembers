// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      #region private

      /// <summary>
      ///   On a failed LLM call, surface a ONE-TIME-per-session actionable hint to the player. The
      ///   in-character "(My words escape me...)" line alone hides that this is an LLM SETUP issue (a
      ///   failed request — bad endpoint, a model that returned nothing, or a small local model whose
      ///   context is too short for the prompt). The exact error is in the log; this points a player who
      ///   has not enabled logging at the likely cause without spamming them every failure.
      /// </summary>
      private static void NotifyLlmFailure()
      {
         if (_llmFailureHintShown) return;
         _llmFailureHintShown = true;
         InformationManager.DisplayMessage(new InformationMessage(
            "Calradia Remembers: the AI did not respond — an LLM call failed. Check your model and endpoint; " +
            "for a small local model, raise its context length and max output tokens. Enable Logging " +
            "(Mod Options → Calradia Remembers) for the exact error.",
            Colors.Red));
      }

      private static string Pick(string[] pool)
         => pool[MBRandom.RandomInt(pool.Length)];

      // ── Captive escape resolution ──────────────────────────────────────────


      private void ApplyEscapeOutcomeFallback(ChatSpeaker speaker, bool freed, NpcProfile profile)
      {
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         ReplacePlaceholder(placeholder, freed
            ? Pick(EscapeSuccessFallbacks)
            : Pick(EscapeFailureFallbacks), speaker);
         if (freed) EndSceneAfterEscape(profile);
         else IsSendEnabled = true;
      }

      /// <summary>
      ///   Applies the standing penalty for being abased before witnesses and surfaces it
      ///   in the chat. The bridge only mutates the real campaign when the player is truly
      ///   a prisoner; console test scenes still show the message for verification.
      /// </summary>
      private void ApplyPublicHumiliationPenalty()
      {
         // Only the captor's MEN form the shaming audience; the bound victim companion does not count.
         int witnessCount = CountAggressorWitnesses();

         if (witnessCount <= 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.GameStateBridge == null) return;

         (int renownLoss, int influenceLoss) = behavior.GameStateBridge.ApplyPlayerPublicHumiliation(witnessCount);

         if (renownLoss <= 0 && influenceLoss <= 0) return;

         AppendSystemMessage(
            $"Word of this will spread. Your standing suffers — renown -{renownLoss}, influence -{influenceLoss}.",
            false);
      }

      /// <summary>
      ///   Gives each heroless (synthetic bandit) witness a distinct bandit-troop portrait so the
      ///   chief's men are not blank avatars. No-op for lord scenes (their witnesses are real
      ///   heroes that already have portraits).
      /// </summary>
      private void AssignBanditWitnessPortraits(bool captorIsBandit)
      {
         _witnessPortraits.Clear();

         if (!captorIsBandit || _witnesses == null || _witnesses.Count == 0) return;

         List<WitnessEntry> heroless = _witnesses.Where(w => string.IsNullOrEmpty(w.HeroStringId)).ToList();

         if (heroless.Count == 0) return;

         List<CharacterObject> faces =
            CaptiveEncounterService.ResolveBanditWitnessFaces(null, heroless.Count);

         for (var i = 0; i < heroless.Count && i < faces.Count; i++)
            if (faces[i] != null)
               _witnessPortraits[heroless[i].Name] = faces[i];
      }

      private string BuildCaptiveBaseSummary(string intentVerb)
      {
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";
         string audience = _captiveEncounterIsCollective && CountAggressorWitnesses() > 0
            ? ", before my soldiers"
            : ", in private";

         return $"Held {player} as my captive and {intentVerb} them{audience}.";
      }

      /// <summary>
      ///   Builds the witness list to inject into <paramref name="speaker" />'s prompt:
      ///   every other participant. When a witness holds the floor, the main NPC is
      ///   added as a witness so the speaker knows whose audience they share.
      /// </summary>
      private IReadOnlyList<WitnessEntry> BuildContextWitnesses(ChatSpeaker speaker)
      {
         if (speaker.IsMain)
            return _witnesses.Count > 0
               ? _witnesses.AsReadOnly()
               : null;

         var list = new List<WitnessEntry>();
         foreach (WitnessEntry w in _witnesses)
            if (w.HeroStringId != speaker.Hero?.StringId)
               list.Add(w);

         if (_npcHero != null)
            list.Add(new WitnessEntry {
               Name = _npcName,
               RelationToNpc = "the host of this conversation, standing beside you",
               HeroStringId = _npcHero.StringId,
               IsPlayerCompanion = false,
               Persona = null
            });

         return list.Count > 0
            ? list
            : null;
      }

      /// <summary>
      ///   Renders the attributed transcript as plain text for a summarizer.
      ///   <paramref name="playerLabel" /> names the player's lines ("Prisoner" in
      ///   captive scenes, their actual name in ordinary conversations).
      /// </summary>
      private string BuildSceneTranscriptText(string playerLabel)
      {
         if (_transcript == null || _transcript.Count == 0) return "";

         var sb = new StringBuilder();
         foreach (TranscriptEntry e in _transcript)
         {
            if (string.IsNullOrWhiteSpace(e.Text)) continue;
            string who = e.IsPlayer
               ? playerLabel
               : e.SpeakerName;
            sb.AppendLine($"{who}: {e.Text}");
         }

         return sb.ToString();
      }

      /// <param name="speaker"></param>
      /// <param name="excludeLastN">
      ///   When non-zero, skips the last N entries of the transcript.
      ///   Used by Sprint 15C to build the NPC's session before the witness reactions
      ///   are committed, so those reactions appear only as the stimulus playerMessage
      ///   and are not double-counted in the session history.
      /// </param>
      private ChatSession BuildSessionFor(ChatSpeaker speaker, int excludeLastN = 0)
      {
         var session = new ChatSession();
         string speakerId = speaker.Hero?.StringId;
         int limit = Math.Max(0, _transcript.Count - excludeLastN);

         for (var i = 0; i < limit; i++)
         {
            TranscriptEntry e = _transcript[i];
            if (e.IsPlayer)
               session.AddPlayerMessage(e.Text);
            else if (!string.IsNullOrEmpty(speakerId) && e.SpeakerHeroId == speakerId)
               session.AddNpcMessage(e.Text); // this speaker's own prior line
            else
               session.AddWitnessStatement(e.SpeakerName, e.Text); // anyone else, attributed
         }

         return session;
      }

      /// <summary>
      ///   Extracts witness reactions from a <see cref="ParsedResponse" />, resolving
      ///   each witness's hero id from the current session's witness list and NPC hero.
      ///   Returns an empty list when there are no reactions.
      /// </summary>
      private List<(string name, string heroId, string text)>
         CollectWitnessReactions(ParsedResponse response)
      {
         var result = new List<(string name, string heroId, string text)>();

         if (response?.WitnessReactions == null) return result;

         // No one is present to react — ignore any reactions the model invents. This is the
         // "the NPC talks to themselves" bug, most visible right after a privacy request clears
         // the room: with the witnesses gone, a smaller model may still emit witness lines.
         if (_witnesses == null || _witnesses.Count == 0) return result;

         foreach (WitnessReaction wr in response.WitnessReactions)
         {
            if (string.IsNullOrWhiteSpace(wr.Text)) continue;

            // The LLM sometimes copies the whole prompt label ("A soldier (one of your
            // men…)") into the name. Strip any parenthetical role so we match — and
            // DISPLAY — only the canonical short name, never the POV-NPC role text.
            string emitted = DialogueHeuristics.StripWitnessRole(wr.Name);

            // A "reaction" attributed to the MAIN NPC is the NPC reacting to itself — drop it.
            // Their own words are the [DIALOGUE]; they are never one of their own witnesses.
            if (string.Equals(emitted, _npcName, StringComparison.OrdinalIgnoreCase))
            {
               ModLog.Info($"[WITNESS] dropped a self-attributed reaction from {_npcName}.");

               continue;
            }

            string heroId = null;
            string canonicalName = emitted;
            foreach (WitnessEntry we in _witnesses)
               if (string.Equals(we.Name, emitted, StringComparison.OrdinalIgnoreCase))
               {
                  heroId = we.HeroStringId;
                  canonicalName = we.Name; // canonical casing/spelling

                  break;
               }

            // Mechanical anti-repeat: drop a witness's line when it matches the last one we
            // showed for them (trimmed, case-insensitive). Kills the verbatim looping the
            // model otherwise produces despite the prompt's "do not repeat" rule.
            if (_lastWitnessReaction.TryGetValue(canonicalName, out string previous) && string.Equals(previous.Trim(), wr.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
               ModLog.Info($"[WITNESS] dropped a repeated line from {canonicalName}.");

               continue;
            }

            _lastWitnessReaction[canonicalName] = wr.Text;

            result.Add((canonicalName, heroId, wr.Text));
         }

         return result;
      }

      private string ColorForWitness(WitnessEntry entry)
      {
         string key = entry.HeroStringId ?? entry.Name ?? "";
         int hash = Math.Abs(key.GetHashCode());

         return WitnessPalette[hash % WitnessPalette.Length];
      }

      /// <summary>
      ///   Closes a captive scene gracefully after a model refusal: replaces the would-be
      ///   refusal line with a terse, non-graphic in-character ending, then ends the scene so
      ///   the player never sees the guardrail. The end-of-scene toll and memory still run at
      ///   window close. Gender-neutral wording so it fits a male or female prisoner.
      /// </summary>
      private void ConcludeCaptiveSceneAfterRefusal(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile)
      {
         const string closingLine = "Enough. Throw the prisoner back with the rest — we are done here.";
         const string closingNarration =
            "When they have finally had their fill, you are hauled up by the arms and dragged, " + "bound and spent, back into the dark. The ordeal is over — for now.";

         ReplacePlaceholder(placeholder, closingLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, closingLine);
         StoryLog.WriteNpcLine(speaker.Name, closingLine);
         AppendNarration(closingNarration);

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage($"{profile?.Name} has ended the conversation.", false);
         ModLog.Info($"[SCENE] model refusal intercepted — scene concluded in character for {profile?.Name}.");
      }

      // ── Address detection ─────────────────────────────────────────────────
      // Heuristics live in AddressedSpeakerDetector (pure, testable); this method
      // only assembles the current participants (main NPC + hero witnesses).

      /// <summary>
      ///   Returns the hero id of the participant the player is addressing, or null when
      ///   none is explicitly addressed (caller keeps the sticky addressee). A bare
      ///   mention is NOT an address — only a stage direction or a vocative counts.
      /// </summary>
      private string DetectAddressedParticipant(string text)
      {
         var participants = new List<AddressedSpeakerDetector.Participant>();
         if (_npcHero != null)
            participants.Add(new AddressedSpeakerDetector.Participant(
               _npcHero.StringId, AddressedSpeakerDetector.DistinctiveNameTokens(_npcName)));
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               participants.Add(new AddressedSpeakerDetector.Participant(
                  w.HeroStringId, AddressedSpeakerDetector.DistinctiveNameTokens(w.Name)));

         return AddressedSpeakerDetector.Detect(text, participants);
      }

      /// <summary>
      ///   Ends a captive scene cleanly once the director's final Conclude beat has played: the
      ///   captor already wrote its own ending, so we only lock input to a Leave button — no extra
      ///   summary narration on top of it.
      /// </summary>
      private void EndCaptiveSceneSoftly(NpcProfile profile)
      {
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         ModLog.Info($"[SCENE] director reached Conclude — scene ended after {_captiveBeatCount} beat(s).");
      }

      /// <summary>Ends the scene after a successful escape — input locks to a Leave button.</summary>
      private void EndSceneAfterEscape(NpcProfile profile)
      {
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage("You tear free and escape into the night. (The actual release applies as you leave.)", true);
         ModLog.Info($"[ESCAPE] scene ended — player escaped from {profile?.Name}.");
      }

      // ── Story Log helpers ─────────────────────────────────────────────────

      // Writes the conversation intro once — deferred until the first player
      // message so _witnesses is already populated by SetWitnesses.
      private void EnsureStoryIntro()
      {
         if (_storyIntroWritten) return;
         _storyIntroWritten = true;

         if (!StoryLog.IsEnabled) return;
         var day = (int) CampaignTime.Now.ToDays;
         string[] witnesses = _witnesses.Select(w => w.Name).ToArray();
         StoryLog.WriteConversationIntro(_npcName, witnesses, _storyLocation, day);
      }

      /// <summary>
      ///   The current NPC's outstanding item-barter bargain (deliver goods for a favor),
      ///   or null when there is none. Read from the live conversation profile.
      /// </summary>
      private InformalQuest FindOutstandingItemBargain()
      {
         try
         {
            NpcProfile giver = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile;

            if (giver == null || _npcHero == null) return null;

            return giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverItems && q.IsOutstanding);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>The outstanding ProvideGoods supply task of the current conversation partner, or null.</summary>
      private InformalQuest FindOutstandingSupplyQuest()
      {
         try
         {
            NpcProfile giver = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile;

            if (giver == null || _npcHero == null) return null;

            return giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.ProvideGoods && q.IsOutstanding);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   Hands the chosen goods to the bridge for a ProvideGoods supply task. The bridge tallies the count,
      ///   and on a full delivery completes the quest (reward paid, journal closed) there; here we only surface
      ///   the outcome line and refresh the bag's visibility.
      /// </summary>
      private void SettleSupply(InformalQuest supply, List<ItemOffer> offers)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile giver = behavior?.DialogFlow?.Profile;

            if (behavior?.GameStateBridge == null || giver == null) return;

            ActionOutcome outcome = behavior.GameStateBridge.SettleSupplyQuest(giver, _npcHero, supply, offers);
            if (outcome == null) return;

            AppendSystemMessage(outcome.Message, outcome.IsPositive);
            if (outcome.IsPositive) OnPropertyChanged(nameof(IsOfferItemVisible));
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] SettleSupply failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Ends a captive scene that has run past its hard cap with a neutral time-skip summary,
      ///   so an eager model that keeps deferring its climax cannot drag the scene on forever.
      ///   Gender-neutral wording so it fits any prisoner.
      /// </summary>
      private void ForceConcludeLongScene(NpcProfile profile)
      {
         const string summary =
            "Time slips away. Whatever your captors mean to do, they tire of it eventually — " + "the threats, the handling, the long dragging hours — and at last they leave you " + "bound and spent, the ordeal blurring into exhaustion behind you, over for now.";
         AppendNarration(summary);

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage($"{profile?.Name} has ended the conversation.", false);
         ModLog.Info($"[SCENE] safety cap reached ({_captiveBeatCount} beats) — scene force-concluded with a summary.");
      }

      /// <summary>
      ///   Honors witness_leaves actions: the player asked one of THEIR OWN companions
      ///   to leave (e.g. "*turning to Olaf* Leave us, I'll catch up with you later"),
      ///   and the LLM acknowledged with the action. Only witnesses marked
      ///   IsPlayerCompanion can leave this way — the NPC's side of the room is
      ///   cleared through request_privacy, where the NPC decides. Name matching is
      ///   tolerant (the LLM may echo a partial or full name).
      /// </summary>
      private void HandleWitnessLeaves(ParsedResponse response)
      {
         if (response?.Actions == null || response.Actions.Count == 0) return;

         foreach (GameAction action in response.Actions)
         {
            if (!string.Equals(action.Type, "witness_leaves",
                   StringComparison.OrdinalIgnoreCase)) continue;

            action.Parameters.TryGetValue("name", out string name);

            if (string.IsNullOrWhiteSpace(name)) continue;

            WitnessEntry match = _witnesses.FirstOrDefault(w =>
               w.IsPlayerCompanion && (string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase) || w.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(w.Name, StringComparison.OrdinalIgnoreCase) >= 0));

            if (match == null)
            {
               ModLog.Info($"[WITNESSES] witness_leaves '{name}' ignored — no matching player companion.");

               continue;
            }

            _witnesses.Remove(match);
            _witnessColors.Remove(match.Name);
            if (!string.IsNullOrEmpty(match.HeroStringId) && _currentSpeakerHeroId == match.HeroStringId)
               _currentSpeakerHeroId = null; // they left — turns route back to the main NPC
            RefreshWitnessUi();

            AppendSystemMessage($"{match.Name} leaves the conversation.", true);
            // Keep the departure in the transcript so every later prompt rebuild
            // knows they are gone and the NPC doesn't keep addressing them.
            RecordTranscript(false, match.Name, match.HeroStringId,
               "*leaves the conversation at the player's request*");
            StoryLog.WriteNpcLine(match.Name, "*leaves the conversation*");
            ModLog.Info($"[WITNESSES] companion '{match.Name}' left (witness_leaves).");
         }
      }

      private ChatSpeaker MainSpeaker()
      {
         // The speaker's profile must ALWAYS follow the speaker's hero (_npcHero), so a captor never wears
         // another hero's memories and a scene's events are never written to the wrong person. Resolve it live
         // from the store by the current hero's id; the mode/last-conversation caches are only a fallback for a
         // heroless speaker (a synthetic bandit captor) or before the hero's profile is persisted. The old code
         // consulted the session-lifetime DialogFlow.Profile FIRST, which stayed pinned to the last companion
         // chatted with and so bled into captive scenes (the "Garios thinks he's Fercread" report).
         NpcProfile heroOwn = _npcHero != null
            ? NpcMemoryCampaignBehavior.Instance?.Store?.Get(_npcHero.StringId)
            : null;
         NpcProfile profile = SpeakerProfileSelector.Select(
            heroOwn,
            _audienceReason != CompanionAudienceReason.None, _audienceProfile,
            IsCaptiveEncounter, _captiveEncounterProfile,
            _captorScene, _captorSceneProfile,
            NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile);

         return new ChatSpeaker {
            IsMain = true,
            Hero = _npcHero,
            Name = _npcName,
            Profile = profile,
            Color = null
         };
      }

      /// <summary>
      ///   Logs the current GPU-memory usage next to how many chat render-targets are live (message bubbles and,
      ///   of those, the ones still holding a character tableau), so a VRAM climb can be lined up against the
      ///   resources the chat is actually holding. Diagnostic only — read once per LLM turn.
      /// </summary>
      private void LogVramSample(string tag)
      {
         int livePortraits = 0;
         for (int i = 0; i < _messages.Count; i++)
            if (_messages[i]?.HasLivePortrait == true) livePortraits++;

         ModLog.Info($"[VRAM] {tag} | {VramProbe.Describe()} | bubbles={_messages.Count} livePortraits={livePortraits}");
      }

      private ChatMessageViewModel MakeSpeakerMessage(ChatSpeaker speaker, string text)
      {
         // A heroless main speaker (synthesized bandit captor) renders from the fallback troop face.
         if (speaker.IsMain && speaker.Hero?.CharacterObject == null && _npcPortraitCharacter != null)
            return new ChatMessageViewModel(_npcPortraitCharacter, speaker.Name, text, false);

         ChatMessageViewModel msg = speaker.IsMain || string.IsNullOrEmpty(speaker.Color)
            ? new ChatMessageViewModel(speaker.Hero, speaker.Name, text, false)
            : new ChatMessageViewModel(speaker.Hero, speaker.Name, text, speaker.Color);

         // A freshly-minted nemesis Hero's tableau may not be renderable yet on the first encounter; fall back
         // to the band's troop face so his line still carries an avatar (a player-reported "no portrait" bug).
         if (speaker.IsMain && !msg.HasLivePortrait && _npcPortraitCharacter != null)
            return new ChatMessageViewModel(_npcPortraitCharacter, speaker.Name, text, false);

         return msg;
      }

      // ── Sprint 15B helpers ────────────────────────────────────────────────

      private void RecordTranscript(bool isPlayer, string name, string heroId, string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         _transcript.Add(new TranscriptEntry {
            IsPlayer = isPlayer,
            SpeakerName = name,
            SpeakerHeroId = heroId,
            Text = text
         });
      }

      private void RefreshWitnessUi()
      {
         bool any = _witnesses.Count > 0;
         string baseList = any
            ? "Present: " + string.Join(" · ", _witnesses.Select(w => w.Name))
            : "";

         // Sprint 15B: when addressing a witness, surface it so a misfire is visible.
         if (!string.IsNullOrEmpty(_currentSpeakerHeroId))
         {
            WitnessEntry cur = _witnesses.FirstOrDefault(w => w.HeroStringId == _currentSpeakerHeroId);
            WitnessesText = cur == null
               ? baseList
               : $"▸ Speaking to {cur.Name}      {baseList}";
         }
         else
         {
            WitnessesText = baseList;
         }

         HasWitnesses = any;
         IsPrivateRequestVisible = any && !_privacyRequestUsed && AllowsPrivacyRequest;
      }

      /// <summary>Removes a placeholder bubble entirely (used for a narration-only turn).</summary>
      private void RemovePlaceholder(ChatMessageViewModel placeholder)
      {
         int idx = _messages.IndexOf(placeholder);
         if (idx >= 0) _messages.RemoveAt(idx);
      }

      /// <summary>
      ///   Renders the NPC/captor turn into its placeholder and returns the spoken line used for
      ///   downstream logic. When the model produced only [NARRATION] and no spoken line (common
      ///   with weaker models that ignore the tags), the empty dialogue bubble is dropped — the
      ///   narration line (appended separately) carries the beat — and an empty string is returned.
      ///   Only when there is neither dialogue nor narration do we fall back to "(silence...)".
      /// </summary>
      private string RenderNpcTurn(ChatMessageViewModel placeholder, ChatSpeaker speaker, ParsedResponse response)
      {
         if (!string.IsNullOrWhiteSpace(response?.Dialogue))
         {
            ReplacePlaceholder(placeholder, response.Dialogue, speaker);

            return response.Dialogue;
         }

         if (!string.IsNullOrWhiteSpace(response?.Narration))
         {
            RemovePlaceholder(placeholder); // narration-only turn — no empty dialogue bubble

            return string.Empty;
         }

         ReplacePlaceholder(placeholder, "(silence...)", speaker);

         return "(silence...)";
      }

      private void ReplacePlaceholder(ChatMessageViewModel placeholder, string newText, ChatSpeaker speaker)
      {
         int idx = _messages.IndexOf(placeholder);

         if (idx < 0) return;
         _messages.RemoveAt(idx);
         _messages.Insert(idx, MakeSpeakerMessage(speaker, newText));
         ChatManager.ScrollToBottom();
      }

      /// <summary>
      ///   Resolves who the player is addressing this turn and updates the sticky
      ///   <see cref="_currentSpeakerHeroId" />. Detection prefers a participant named
      ///   inside a *stage direction*, then a vocative; otherwise the previous addressee
      ///   sticks. Falls back to the main NPC when the addressee can't be resolved.
      /// </summary>
      private ChatSpeaker ResolveAddressedSpeaker(string text)
      {
         string detectedId = DetectAddressedParticipant(text);

         if (detectedId == null) return SpeakerForCurrent();
         string resolvedId = detectedId == _npcHero?.StringId
            ? null
            : detectedId;

         if (resolvedId == _currentSpeakerHeroId) return SpeakerForCurrent();
         string previousName = string.IsNullOrEmpty(_currentSpeakerHeroId)
            ? _npcName
            : SpeakerForCurrent().Name;
         _currentSpeakerHeroId = resolvedId;
         string newName = string.IsNullOrEmpty(resolvedId)
            ? _npcName
            : SpeakerForCurrent().Name;
         ModLog.Info($"[ADDRESS] turn routed to {newName} (was {previousName})");

         return SpeakerForCurrent();
      }

      /// <summary>
      ///   Resolves a captive escape attempt. The captor's already-written line shows the START
      ///   of the scuffle; the OUTCOME is fate's: the bridge rolls and (on success) commits the
      ///   freeing, then a tightly-constrained beat narrates the captor losing the prisoner (or,
      ///   on failure, reasserting). The LLM can never reverse a committed escape.
      /// </summary>
      private void ResolveEscapeAttempt(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile,
         string npcLine,
         ParsedResponse response)
      {
         // Show the captor's start-of-struggle reaction (it was told to stop before the outcome).
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         bool freed = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.ResolvePlayerEscapeAttempt(false) ?? false;
         ModLog.Info($"[ESCAPE] attempt resolved: {(freed ? "SUCCESS — player freed" : "failure — captor holds")}.");

         IsSendEnabled = false;
         _ = RunEscapeOutcomeAsync(speaker, freed);
      }

      /// <summary>
      ///   Resolves a captor's decision to free the player. Unlike escape there is no roll: the captor chose
      ///   this, so their parting line stands. The bridge re-validates the release gate and commits the
      ///   freeing (deferred to scene close so the window is not disrupted, like escape); the scene then ends.
      /// </summary>
      private void ResolveCaptorRelease(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile,
         string npcLine,
         ParsedResponse response)
      {
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         bool freed = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.CommitCaptorRelease(speaker.Hero) ?? false;
         ModLog.Info($"[RELEASE] captor {speaker.Name} release {(freed ? "committed — player freed at scene close" : "refused by the gate")}.");

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage(freed ? $"{profile?.Name} has set you free." : $"{profile?.Name} has ended the conversation.", false);
      }

      /// <summary>
      ///   Resolves the reacting party for a witness reaction: the main NPC when the name matches it,
      ///   otherwise the matching listed witness. Returns the portrait hero (null when heroless), the
      ///   display colour, and the hero StringId. Extracted from AppendWitnessReaction to keep it simple.
      /// </summary>
      private (Hero hero, string color, string heroId) ResolveWitnessRender(string witnessName)
      {
         if (string.Equals(witnessName, _npcName, StringComparison.OrdinalIgnoreCase))
            return (_npcHero, NpcCreamColor, _npcHero?.StringId);

         foreach (WitnessEntry w in _witnesses)
         {
            if (!string.Equals(w.Name, witnessName, StringComparison.OrdinalIgnoreCase)) continue;

            Hero hero = string.IsNullOrEmpty(w.HeroStringId)
               ? null
               : Hero.FindFirst(h => h.StringId == w.HeroStringId);
            string color = _witnessColors.TryGetValue(w.Name, out string c)
               ? c
               : WitnessPalette[0];

            return (hero, color, w.HeroStringId);
         }

         return (null, WitnessPalette[0], null);
      }


      /// <summary>
      ///   Hands the chosen goods to the bridge, which enforces the value floor and — if met —
      ///   removes them and honors the bargain's favor. On success the quest is closed and the
      ///   NPC accepts the goods in their own voice (one LLM turn, written to the story log)
      ///   rather than a bare mechanical line — falling back to the plain confirmation when the
      ///   LLM is unavailable. A short offer keeps the plain refusal line.
      /// </summary>
      private void SettleItemBargain(InformalQuest bargain, List<ItemOffer> offers)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile giver = behavior?.DialogFlow?.Profile;

            if (behavior?.GameStateBridge == null || giver == null) return;

            // Resolve the goods' display names BEFORE the bridge removes them from the
            // roster, so the NPC's acceptance line can name what it was paid with.
            string goods = ItemBargainText.DescribeOfferedGoods(offers);

            ActionOutcome outcome = behavior.GameStateBridge
                                            .SettleItemBargain(giver, _npcHero, bargain, offers);

            if (outcome == null) return;

            // Short offer — the deal didn't close; nothing was removed. Keep the plain line.
            if (!outcome.IsPositive)
            {
               AppendSystemMessage(outcome.Message, false);

               return;
            }

            // Deal closed: stamp the bargain settled and drop the bag button.
            int day = behavior.GameStateBridge.GetCurrentGameDay();
            QuestService.MarkBargainSettled(giver, bargain, _npcHero, day, behavior.Store);
            OnPropertyChanged(nameof(IsOfferItemVisible));

            // Narrative beat instead of a bare green line: the NPC accepts the goods in
            // their own voice (shown under their portrait, written to the story log) via
            // one LLM turn driven by a synthetic stage cue. Fall back to the mechanical
            // confirmation when the LLM is unavailable so the player still sees the result.
            if (behavior.LlmServices is not {IsAvailable: true})
            {
               AppendSystemMessage(outcome.Message, true);

               return;
            }

            ChatSpeaker speaker = MainSpeaker();
            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            IsSendEnabled = false;
            _ = RunLlmExchangeAsync(
               ItemBargainText.BuildBargainAcceptanceStimulus(goods), speaker, placeholder, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[OFFER] SettleItemBargain failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private ChatSpeaker SpeakerForCurrent()
      {
         if (string.IsNullOrEmpty(_currentSpeakerHeroId)) return MainSpeaker();

         WitnessEntry w = _witnesses.FirstOrDefault(x => x.HeroStringId == _currentSpeakerHeroId);
         Hero h = w?.HeroStringId != null
            ? Hero.FindFirst(x => x.StringId == w.HeroStringId)
            : null;
         NpcProfile p = w?.HeroStringId != null
            ? NpcMemoryCampaignBehavior.Instance?.Store?.Get(w.HeroStringId)
            : null;

         if (w == null || h == null || p == null)
         {
            _currentSpeakerHeroId = null; // addressee gone — revert to main NPC

            return MainSpeaker();
         }

         _witnessColors.TryGetValue(w.Name, out string color);

         return new ChatSpeaker {
            IsMain = false,
            Hero = h,
            Name = w.Name,
            Profile = p,
            Color = color ?? WitnessPalette[0]
         };
      }

      #endregion
   }
}