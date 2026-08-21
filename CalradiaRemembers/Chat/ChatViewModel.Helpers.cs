// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Duels;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Witnesses;
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
      private static void NotifyLlmFailure(string reason = null)
      {
         if (_llmFailureHintShown) return;
         _llmFailureHintShown = true;
         // Surface the client's OWN reason when it has one (a timeout, a context-length or rate-limit error):
         // it is already player-facing and tells them WHICH knob to turn, where the generic guidance alone left
         // a player guessing "check your model and endpoint" at what was really a timeout on a long prompt.
         string detail = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason.Trim()})";
         InformationManager.DisplayMessage(new InformationMessage(
            "Calradia Remembers: the AI did not respond, an LLM call failed." + detail + " Check your model and " +
            "endpoint; for a small local model, raise its context length and max output tokens. Enable Logging " +
            "(Mod Options -> Calradia Remembers) for the exact error.",
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

         // Round-table audit D2: entries were built once from the HOST's angle and handed unchanged to every
         // other speaker, so a participant read the room through someone else's eyes ("Aldric (your vassal)"
         // in the prompt of a lord who is not Aldric's liege). Each speaker now gets the room described from
         // THEIR side, which is what WitnessEntry.RelationToNpc always claimed to be.
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         var list = new List<WitnessEntry>();

         foreach (WitnessEntry w in _witnesses)
            if (w.HeroStringId != speaker.Hero?.StringId)
               list.Add(WitnessResolver.ForObserver(w, speaker.Hero, store));

         WitnessEntry host = WitnessResolver.HostForObserver(_npcHero, speaker.Hero, store);
         if (host != null) list.Add(host);

         return list.Count > 0
            ? list
            : null;
      }

      /// <summary>
      ///   Bring-participants-to-a-captor-scene, increment 1: true when THIS turn's speaker is a brought
      ///   companion the player has addressed or handed the prisoner to (the sticky addressed-speaker routing
      ///   already resolved in <paramref name="speaker" />), inside a player-as-captor scene. Feeds
      ///   <see cref="NpcMemoryService.Core.Models.EncounterContext.CompanionActingOnCaptive" />. Excludes the
      ///   main NPC (the prisoner's own turn already has its own framing, <c>AppendPlayerCaptorSceneRules</c>),
      ///   the bound companion-victim entry (present as the VICTIM, not as one of the captors), and (increment 2)
      ///   any <see cref="WitnessEntry.IsBroughtCaptive" /> entry: a brought captive is never an acting captor,
      ///   even if the player addresses them, so only a real <see cref="WitnessEntry.IsPlayerCompanion" />
      ///   qualifies.
      /// </summary>
      private bool IsCompanionActingOnCaptiveTurn(ChatSpeaker speaker)
      {
         if (!_captorScene || speaker.IsMain || speaker.Hero == null) return false;

         return _witnesses.Any(w =>
            w.HeroStringId == speaker.Hero.StringId && w.IsPlayerCompanion && !w.IsCaptiveVictim && !w.IsBroughtCaptive);
      }

      /// <summary>
      ///   Bring-participants-to-a-captor-scene, increment 2 (2026-08-21): the mirror of
      ///   <see cref="IsCompanionActingOnCaptiveTurn" /> for a BROUGHT CAPTIVE, another of the player's own
      ///   prisoners brought into the scene as leverage (<see cref="WitnessEntry.IsBroughtCaptive" />). True when
      ///   the player has just addressed that captive, inside a player-as-captor scene. Feeds
      ///   <see cref="NpcMemoryService.Core.Models.EncounterContext.IsBroughtCaptiveTurn" />, which drives the
      ///   coerced-captive framing rather than the acting-companion or free-witness teachings.
      /// </summary>
      private bool IsBroughtCaptiveTurn(ChatSpeaker speaker)
      {
         if (!_captorScene || speaker.IsMain || speaker.Hero == null) return false;

         return _witnesses.Any(w => w.HeroStringId == speaker.Hero.StringId && w.IsBroughtCaptive);
      }

      /// <summary>Diagnostics (cr.roundtable): the names currently in the room, in display order.</summary>
      internal IReadOnlyList<string> DebugWitnessNames()
         => _witnesses.Select(w => w.Name).ToList();

      /// <summary>Diagnostics (cr.roundtable): who hosts this conversation.</summary>
      internal string DebugMainSpeakerName() => _npcName ?? "(unknown)";

      /// <summary>
      ///   Renders the attributed transcript as plain text for a summarizer.
      ///   <paramref name="playerLabel" /> names the player's lines ("Prisoner" in
      ///   captive scenes, their actual name in ordinary conversations).
      /// </summary>
      private string BuildSceneTranscriptText(string playerLabel) => _transcript.ToPlainText(playerLabel);

      /// <param name="speaker"></param>
      /// <param name="excludeLastN">
      ///   When non-zero, skips the last N entries of the transcript.
      ///   Used by Sprint 15C to build the NPC's session before the witness reactions
      ///   are committed, so those reactions appear only as the stimulus playerMessage
      ///   and are not double-counted in the session history.
      /// </param>
      private ChatSession BuildSessionFor(ChatSpeaker speaker, int excludeLastN = 0)
      {
         // What each entry becomes (audit C4, including the narration-bearing run rebuilt as one tagged
         // assistant turn) is decided by the transcript itself, where a net10 test can reach it. All that is
         // left here is replaying the result onto the engine's ChatSession.
         IReadOnlyList<SessionTurn> turns = _transcript.ComposeSessionFor(speaker.Hero?.StringId, excludeLastN);

         var session = new ChatSession();

         foreach (SessionTurn turn in turns)
            switch (turn.Role)
            {
               case SessionTurnRole.Player:
                  session.AddPlayerMessage(turn.Text);

                  break;
               case SessionTurnRole.Witness:
                  session.AddWitnessStatement(turn.SpeakerName, turn.Text); // anyone else, attributed

                  break;
               default:
                  session.AddNpcMessage(turn.Text); // this speaker's own prior output

                  break;
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

         // Which lines may be voiced is a decision, and it now lives in WitnessReactionGate where a net10
         // test can reach it: everything below is engine plumbing (who is present, what each was last heard
         // saying, resolving the hero id) around that one call.
         List<string> presentNames = _witnesses.Select(w => w.Name).ToList();

         foreach (WitnessReaction wr in response.WitnessReactions)
         {
            WitnessReactionDecision decision =
               WitnessReactionGate.Decide(wr.Name, wr.Text, _npcName, presentNames, _lastWitnessReaction);

            if (!decision.IsKept)
            {
               if (decision.Verdict != WitnessReactionVerdict.DropBlank)
                  ModLog.Info($"[WITNESS] dropped a reaction from '{decision.CanonicalName}': {decision.Verdict}.");

               continue;
            }

            string canonicalName = decision.CanonicalName;
            string heroId = _witnesses.FirstOrDefault(w => string.Equals(w.Name, canonicalName, StringComparison.OrdinalIgnoreCase))?.HeroStringId;

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
         NotifyConversationEndedChanged();
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
         NotifyConversationEndedChanged();
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
         NotifyConversationEndedChanged();
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
         NotifyConversationEndedChanged();
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

      private void RecordTranscript(bool isPlayer, string name, string heroId, string text, bool isNarration = false)
         => _transcript.Record(isPlayer, name, heroId, text, isNarration);

      /// <summary>
      ///   A brigand captor learns the player's name only if the prisoner gives it up (a brigand has no
      ///   register). When the player's line names them, record it on the captor's profile so the scene, and
      ///   a recurring nemesis across later captures, may use it (Gabriel 2026-07-19). Only for a brigand
      ///   captor: a lord already knows the name, and only once (idempotent). The Store.Set persists it for a
      ///   nemesis; an ephemeral synthesized bandit is never saved, which is fine (the name lives only for
      ///   this scene, where the conversation history carries it anyway).
      /// </summary>
      private void MaybeLearnPlayerNameFromCaptive(string playerMessage)
      {
         if (!IsCaptiveEncounter || !_captiveEncounterIsBandit) return;
         if (_captiveEncounterProfile is not {CaptorLearnedPlayerName: false}) return;
         if (!PlayerNameRevealPolicy.MentionsOwnName(playerMessage, Hero.MainHero?.Name?.ToString())) return;

         _captiveEncounterProfile.CaptorLearnedPlayerName = true;
         NpcMemoryCampaignBehavior.Instance?.Store?.Set(_captiveEncounterProfile);
         ModLog.Info($"[CAPTIVE] {_captiveEncounterProfile.Name} learned the player's name from the prisoner.");
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

         // The round-table button is offered whenever there is a group to go round (witnesses present),
         // outside captive scenes and once the conversation has not been ended.
         IsRoundTableVisible = _roundTable.ComputeVisible(_witnesses.Count, IsCaptiveEncounter, _conversationEnded);
      }

      /// <summary>A ChatSpeaker for a present witness (resolved Hero + stored profile + colour), or null if gone.</summary>
      private ChatSpeaker SpeakerForWitness(WitnessEntry w)
      {
         if (string.IsNullOrEmpty(w?.HeroStringId)) return null;

         Hero h = Hero.FindFirst(x => x.StringId == w.HeroStringId);
         NpcProfile p = NpcMemoryCampaignBehavior.Instance?.Store?.Get(w.HeroStringId);

         if (h == null || p == null) return null;

         _witnessColors.TryGetValue(w.Name, out string color);

         return new ChatSpeaker {
            IsMain = false,
            Hero = h,
            Name = w.Name,
            Profile = p,
            Color = color ?? WitnessPalette[0]
         };
      }

      /// <summary>Every present witness that resolves to a live hero with a profile, as ChatSpeakers.</summary>
      private List<ChatSpeaker> PresentWitnessSpeakers()
      {
         var list = new List<ChatSpeaker>();
         foreach (WitnessEntry w in _witnesses)
         {
            ChatSpeaker s = SpeakerForWitness(w);
            if (s != null) list.Add(s);
         }

         return list;
      }

      /// <summary>The main NPC followed by every present witness, as ChatSpeakers (for the button round-table).</summary>
      private List<ChatSpeaker> AllPresentSpeakers()
      {
         var list = new List<ChatSpeaker> {MainSpeaker()};
         list.AddRange(PresentWitnessSpeakers());

         return list;
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
      /// <param name="allowSilentSpeaker">
      ///   Council narrator model: true for a real council, where <paramref name="speaker" /> is the anchor
      ///   seated like any other member, not privileged to reply every turn. When true, a response carrying
      ///   [WITNESS_REACTION] blocks (other seated members spoke, even if the anchor and the narrator both
      ///   stayed silent) also drops the placeholder cleanly instead of falling back to "(silence...)" under
      ///   the anchor's own name for a turn they simply chose not to speak on.
      /// </param>
      private string RenderNpcTurn(ChatMessageViewModel placeholder, ChatSpeaker speaker, ParsedResponse response, bool allowSilentSpeaker = false)
      {
         if (!string.IsNullOrWhiteSpace(response?.Dialogue))
         {
            ReplacePlaceholder(placeholder, response.Dialogue, speaker);

            return response.Dialogue;
         }

         if (!string.IsNullOrWhiteSpace(response?.Narration)
             || (allowSilentSpeaker && response?.WitnessReactions is {Count: > 0}))
         {
            RemovePlaceholder(placeholder); // narration-only (or council: other members carried it), no empty bubble

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
      ///   Draws THIS turn's verdict for whatever the captive player physically attempts. The model is told
      ///   the answer and narrates it; it never gets to decide, because a model judging the struggle it is
      ///   also writing always sides with the scene it meant to tell. Null outside a captive scene, and on a
      ///   synthetic scene cue (no player line, so nothing to adjudicate).
      /// </summary>
      private bool? RollCaptiveAttemptVerdict(bool suppressPlayerLine)
      {
         if (!IsCaptiveEncounter || suppressPlayerLine) return null;

         Hero player = Hero.MainHero;
         if (player == null) return null;

         int health = player.MaxHitPoints > 0
            ? player.HitPoints * 100 / player.MaxHitPoints
            : 100;
         int roll = MBRandom.RandomInt(100);
         bool succeeds = CaptiveActionOutcomePolicy.Succeeds(roll, health);
         ModLog.Info($"[AGENCY] captive attempt verdict: roll {roll} vs {CaptiveActionOutcomePolicy.SuccessChance(health)}% "
                   + $"(health {health}%) -> {(succeeds ? "SUCCEEDS" : "FAILS")}");

         return succeeds;
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
         // A narration-led turn keeps its emitted order: narration before the spoken line, in the
         // bubbles as in the transcript/story entries AppendNarration writes.
         bool narrationFirst = response?.NarrationBeforeDialogue == true && !string.IsNullOrWhiteSpace(response.Narration);
         if (narrationFirst)
            AppendNarration(response.Narration, placeholder);
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!narrationFirst && !string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         // ONE escape attempt per captivity event (Gabriel 2026-07-20). Once this event's attempt is spent, a
         // further bid is absorbed by the fiction: the captor's line above already answers it, and we arm
         // NOTHING, so the scene carries on instead of looping back into the fight.
         if (CaptiveEscapeCoordinator.EscapeAlreadySpent)
         {
            IsSendEnabled = !_conversationEnded;
            ModLog.Info("[ESCAPE] a further break was refused: this captivity event's one attempt is already spent.");

            return;
         }

         // 2026-07-20: the escape is no longer a narrated roll. It becomes a REAL fight-or-flee encounter, staged
         // after this chat closes (see CaptiveEscapeCoordinator). Build the roster: the real captor FIRST so the
         // player faces HIM in the flesh, then a bandit filler per present witness, capped at three. The OUTCOME
         // of the fight, not the LLM, decides freedom; nothing is freed here.
         var roster = new List<CharacterObject> {speaker.Hero?.CharacterObject};
         foreach (WitnessEntry w in _witnesses)
         {
            if (roster.Count >= 3) break;             // captor + up to two others
            if (w.IsPlayerCompanion) continue;         // your own people never fight you
            Hero wh = string.IsNullOrEmpty(w.HeroStringId)
               ? null
               : Hero.FindFirst(h => h != null && h.StringId == w.HeroStringId);
            roster.Add(wh?.CharacterObject);           // a real hero as himself, a synthetic man as a bandit filler
         }

         // If the player LOSES the fight, the scene RESUMES: reopen the captive chat with the captor reacting to
         // the beaten-back prisoner. Capture the scene's data now, as values (the VM is torn down at chat close).
         Hero captorHero = speaker.Hero;
         NpcProfile captorProfile = _captiveEncounterProfile;
         var witnessesForResume = new List<WitnessEntry>(_witnesses);
         CaptiveSceneIntent resumeIntent = _captiveEncounterIntent;
         bool resumeCollective = _captiveEncounterIsCollective;

         void ResumeOnLoss() => ChatManager.OpenForCaptiveEncounter(
            captorHero, captorProfile, witnessesForResume, resumeCollective, resumeIntent,
            openingOverride: CaptiveEscapeCoordinator.ResumeAfterLostEscapeCue);

         CaptiveEscapeCoordinator.Arm(roster, speaker.Hero, _captiveEncounterIsBandit, ResumeOnLoss);

         // Deliberate transition: lock input and leave only the close button. Clicking it closes the chat, and the
         // coordinator stages the encounter on the next tick, on the live map (we never return to the chat).
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         IsPleaseContinueVisible = false;
         IsSendEnabled = false;
         CloseButtonText = "Break for it";
         NotifyConversationEndedChanged();
         AppendSystemMessage("You steel yourself, and make your break for it...", false);
         ModLog.Info($"[ESCAPE] armed from chat; captor={speaker.Name}, {roster.Count} aggressor(s). Awaiting the 'Break for it' click.");
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
         bool narrationFirst = response?.NarrationBeforeDialogue == true && !string.IsNullOrWhiteSpace(response.Narration);
         if (narrationFirst)
            AppendNarration(response.Narration, placeholder);
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!narrationFirst && !string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         bool freed = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.CommitCaptorRelease(speaker.Hero) ?? false;
         ModLog.Info($"[RELEASE] captor {speaker.Name} release {(freed ? "committed — player freed at scene close" : "refused by the gate")}.");

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         NotifyConversationEndedChanged();
         AppendSystemMessage(freed ? $"{profile?.Name} has set you free." : $"{profile?.Name} has ended the conversation.", false);
      }

      /// <summary>
      ///   Resolves a duel called for in conversation. The prompt forbids the NPC from answering its own
      ///   challenge (it stops at the words), so everything after them is settled here: eligibility is
      ///   re-validated against the LIVE facts, and when the PLAYER is the one calling the NPC out,
      ///   <see cref="DuelAcceptancePolicy" /> weighs whether they take the field, from their honour, valour,
      ///   fear and grudges. Nothing is staged from inside the chat window: a mission cannot open over it, so
      ///   the fight is ARMED and <see cref="DuelCoordinator" /> opens it once the chat has closed, exactly as
      ///   the captive escape does.
      /// </summary>
      private void ResolveDuelChallenge(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile,
         string npcLine,
         ParsedResponse response)
      {
         // The challenge itself was spoken: show it whatever comes of it. Narration keeps its emitted order,
         // as in every other resolver here.
         bool narrationFirst = response?.NarrationBeforeDialogue == true && !string.IsNullOrWhiteSpace(response.Narration);
         if (narrationFirst)
            AppendNarration(response.Narration, placeholder);
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!narrationFirst && !string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         Hero opponent = speaker.Hero;

         // A witness present in the room but not this turn's interlocutor is speaking on their OWN turn: a
         // quarrel with someone else in the room, not the player (challenge_duel carries no target parameter,
         // so whoever speaks becomes "opponent" no matter who the fiction actually names). Player report
         // 2026-08-14: with player+A+B+C present, the quarrel was written A-vs-C, but the mod armed C-vs-player.
         // Absorb it into the fiction instead: the line already stands, nothing is armed. TRUE NPC-vs-NPC
         // duels (A challenges C, player spectates) are a deliberately-separate FUTURE increment of the Duels
         // pillar; this guard only prevents the player being dragged into a fight that was never theirs.
         if (!DuelInterlocutorPolicy.IsDirectInterlocutor(speaker.IsMain, speaker.Hero?.StringId, _currentSpeakerHeroId))
         {
            IsSendEnabled = !_conversationEnded;
            ModLog.Info($"[DUEL] {speaker.Name} called for steel on their own turn (not the player's interlocutor), absorbed into fiction, nothing armed.");

            return;
         }

         // The prompt is advice, the bridge is law. The verb was taught from a context built at the START of
         // the turn, and a model can reach for it several turns later, by which time the two may have moved
         // apart, one may have been taken prisoner, or the cooldown may have closed. An inadmissible challenge
         // is ABSORBED by the fiction (the words stand, nothing is armed, the conversation carries on) rather
         // than contradicted with a system line the player would read as a bug.
         if (!DuelEligibilityResolver.CanDuelNow(opponent, profile, out DuelVenue venue))
         {
            IsSendEnabled = !_conversationEnded;
            ModLog.Info($"[DUEL] {speaker.Name} called for steel, but the duel is no longer admissible at commit time; nothing armed.");

            return;
         }

         bool playerIssued = ChallengerIsPlayer(response);

         // The player called them out, so the NPC's answer is the policy's, not the model's. An NPC who balks
         // does not simply go quiet: the refusal is a deed, it costs them (contempt, and renown to the player
         // who called a bluff), and the conversation carries on from there.
         if (playerIssued
             && DuelAcceptancePolicy.Decide(new DuelAcceptanceContext {
                Traits = GrudgeBridge.BuildTraitFacts(opponent),
                Stance = StanceService.ToStance(profile),
                AGrudgeDrivesIt = HasLiveGrudge(opponent),
                ChallengedBeforeWitnesses = _witnesses.Count > 0
             }) == DuelResponse.Refuse)
         {
            // Recorded as scene narration rather than a bare system line, so it enters the transcript and the
            // NPC's very next reply is spoken by someone who knows they refused.
            AppendNarration($"{profile?.Name ?? speaker.Name} lets the challenge hang, and will not take it up.");
            DuelOutcomeApplier.Apply(DuelReason.HonourAffront, DuelResolution.NpcForfeited, opponent, null, _witnesses.Count);
            IsSendEnabled = !_conversationEnded;
            ModLog.Info($"[DUEL] {speaker.Name} refused the player's challenge (acceptance score below the threshold).");

            return;
         }

         DuelCoordinator.Arm(opponent, DuelReason.HonourAffront, venue, !playerIssued, _witnesses.Count, null);

         // Same deliberate transition as the escape: lock the input and leave only the close button. Clicking
         // it closes the chat, and the coordinator stages the duel on the next tick, on the live map.
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         IsPleaseContinueVisible = false;
         IsSendEnabled = false;
         CloseButtonText = playerIssued ? "Take the field" : "Answer him";
         NotifyConversationEndedChanged();
         AppendSystemMessage(playerIssued
            ? $"{profile?.Name ?? speaker.Name} takes up your challenge. Close this conversation to take the field."
            : $"{profile?.Name ?? speaker.Name} demands satisfaction of you. Close this conversation to take the field.", false);
         ModLog.Info($"[DUEL] armed from chat against {speaker.Name}: venue={venue}, "
                   + $"challenger={(playerIssued ? "player" : "npc")}, {_witnesses.Count} witness(es).");
      }

      /// <summary>
      ///   Who called for the duel, read from the action's "challenger" field. A missing or unrecognised value
      ///   must fail SAFE, and the safe side is the NPC: treating an unknown challenger as the NPC's own demand
      ///   only costs the player an answer they were going to give anyway, whereas guessing "player" would run
      ///   the acceptance policy against an NPC who has ALREADY demanded satisfaction, and could have them balk
      ///   at their own challenge.
      /// </summary>
      private static bool ChallengerIsPlayer(ParsedResponse response)
      {
         if (response?.Actions == null) return false;

         foreach (GameAction action in response.Actions)
         {
            if (!string.Equals(action?.Type, "challenge_duel", StringComparison.OrdinalIgnoreCase)) continue;
            if (action.Parameters == null
                || !action.Parameters.TryGetValue("challenger", out string who)
                || string.IsNullOrWhiteSpace(who))
               return false;

            return who.Trim().StartsWith("player", StringComparison.OrdinalIgnoreCase)
                || who.Trim().StartsWith("them", StringComparison.OrdinalIgnoreCase);
         }

         return false;
      }

      /// <summary>
      ///   True when this hero carries a live grudge against the player, the single strongest pull toward the
      ///   field in <see cref="DuelAcceptancePolicy" /> (an honourable outlet for a grievance words have not
      ///   settled). Same ranked source the prompt's own grudge note reads.
      /// </summary>
      private static bool HasLiveGrudge(Hero hero)
      {
         if (hero == null) return false;

         var today = (int) CampaignTime.Now.ToDays;

         return (GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today)?.Count ?? 0) > 0;
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
      ///   Resolves which seated council member an [ACTION]'s optional "actor" parameter names, so a deed a
      ///   NON-anchor member commits to (a gift, a task, a vow) executes against THEM rather than always the
      ///   anchor/routed speaker who happens to carry the turn's spoken reply. No "actor" parameter keeps
      ///   today's behaviour exactly: <paramref name="fallbackHero" />/<paramref name="fallbackProfile" /> pass
      ///   through unchanged, so an ordinary (non-council) conversation is never affected: the prompt only
      ///   ever teaches "actor:" on a council/round-table turn. An actor that resolves to nobody seated, or
      ///   ambiguously to more than one seat, returns false: the caller must DROP the action rather than
      ///   misattribute it (silently taking the wrong hero's gold, or marrying the wrong person, is worse than
      ///   the deed never landing).
      /// </summary>
      /// <summary>
      ///   The seated members of the CURRENT council/round-table turn: the anchor (this window's main NPC)
      ///   first, then every present witness carrying a real hero (stable regardless of who is currently
      ///   carrying THIS turn's spoken reply). Shared by <see cref="TryResolveActionActor" /> (an [ACTION]'s
      ///   "actor" field) and the [RESOLUTION] actor attribution (<see cref="ProcessCouncilResolutions" />), so
      ///   both channels resolve a name against the exact same seat list.
      /// </summary>
      private (List<string> Names, List<string> HeroIds) SeatedMembers()
      {
         var names = new List<string> {_npcName};
         var heroIds = new List<string> {_npcHero?.StringId};

         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
            {
               names.Add(w.Name);
               heroIds.Add(w.HeroStringId);
            }

         return (names, heroIds);
      }

      private bool TryResolveActionActor(
         GameAction action,
         Hero fallbackHero,
         NpcProfile fallbackProfile,
         NpcMemoryCampaignBehavior behavior,
         out Hero actorHero,
         out NpcProfile actorProfile)
      {
         actorHero = fallbackHero;
         actorProfile = fallbackProfile;

         // m10: "actor:" attribution is a COUNCIL concept only. In an ordinary conversation (even one with
         // witnesses present) a hallucinated actor: must never redirect the action to a witness or drop it; it
         // stays with this turn's speaker. Only a real council resolves an actor against the seated members.
         if (_councilScope == null) return true;

         if (action?.Parameters == null
             || !action.Parameters.TryGetValue("actor", out string actorName)
             || string.IsNullOrWhiteSpace(actorName))
            return true; // no actor named: today's behaviour, unchanged

         (List<string> seatedNames, List<string> seatedHeroIds) = SeatedMembers();

         int idx = CouncilActorPolicy.ResolveActor(actorName, seatedNames);

         if (idx < 0)
         {
            ModLog.Error($"[ACTION] '{action.Type}' named actor '{actorName}' who could not be resolved among "
                       + $"the seated members ({string.Join(", ", seatedNames)}), dropped rather than misattributed.");
            actorHero = null;
            actorProfile = null;

            return false;
         }

         string heroId = seatedHeroIds[idx];

         if (heroId == fallbackHero?.StringId)
            return true; // the named actor IS whoever this turn's dispatch already targets

         Hero hero = Hero.FindFirst(h => h.StringId == heroId);
         NpcProfile prof = behavior?.Store?.Get(heroId);

         if (hero == null || prof == null)
         {
            ModLog.Error($"[ACTION] '{action.Type}' named actor '{actorName}' resolved to seated member "
                       + $"'{seatedNames[idx]}' whose hero/profile could not be loaded, dropped.");
            actorHero = null;
            actorProfile = null;

            return false;
         }

         actorHero = hero;
         actorProfile = prof;

         return true;
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