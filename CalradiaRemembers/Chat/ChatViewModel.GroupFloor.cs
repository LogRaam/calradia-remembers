// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 7 of the group-scene plan, the group engine in the Dramaturge mode (docs/design/group-chat/
// integration-plan-2026-11-28.md). Gabriel, 26/11/2026: one thread per character; after every line a fast, cheap judge says,
// for each listener, whether they react and how much they want to speak; "le juge ne fait pas parler"; the referee
// (TurnArbiter) picks one speaker. Written then and "not wired yet"; wired here. NOT A/B-validated: it runs only in the
// Dramaturge mode (GroupFloorPolicy.Runs); in every other mode the ensemble turn keeps its fixed order and EnsembleFloorPolicy.
//
// Where it runs: the ensemble turn (RunEnsembleTurnAsync), the spotlight scene with two or more active voices, which is also
// what a companion briefing becomes once a second companion is drawn in. NOT the council or the assembly: those are one call
// that writes the whole table (CouncilPromptBuilder, with its resolutions and regard shifts parsed from one reply), so there
// is no "who speaks next" to decide there; arbitrating them means rewriting that pipeline, judged too invasive for tonight.
//
// A beat here: the player's line, then, round after round, every present listener but the last speaker is judged on the
// INTERPRET client (in parallel, at most TurnArbiter.JudgedPerRound, a short limit each), the bids are built from what the
// game knows (GroupFloorPolicy.BuildBids), TurnArbiter decides, and the one it picks speaks through the same per-voice
// exchange as before (RunLlmExchangeAsync, group tail suppressed). It stops when the referee hands the floor back, nobody
// speaks, a voice ends the meeting, or the cap is reached (one line plus NPC Cross-Talk rounds, never past the arbiter's
// ceiling). If every judge call fails on the first round, the beat runs the old fixed order instead; later, it closes with
// EnsembleFloorPolicy's one answer to a hanging question. One reaction at most a line is narrated (GroupFloorPolicy.
// PickReaction), written short on the prose client, and skipped with Scene Narration off. The conversation log gets "judge"
// and "referee" lines.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // The most lines of the scene the judge and the reaction writer read.
      private const int GroupSceneLines = 30;

      private readonly Random _groupTieBreak = new();
      private readonly HashSet<string> _groupShownEver = new();
      private int _groupLinesSinceReaction = 1;

      /// <summary>cr.test group_floor: the stand-in for the judge's client (the interpreter). Null in play.</summary>
      internal static ILlmClient FloorJudgeForDiagnostics;

      /// <summary>True when this chat's ensemble turns are decided by the judge and the referee (Dramaturge mode, switch on it).</summary>
      internal bool GroupFloorRunsNow => GroupFloorPolicy.Runs(new GroupFloorGateFacts {
         DramaturgeMode = ModSettings.DramaturgeMode, SwitchOnTheMcmMode = LlmServices.ActiveModelMode == ManualModelMode.Enhanced
      });

      private static ILlmClient FloorJudgeClient => FloorJudgeForDiagnostics ?? NpcMemoryCampaignBehavior.Instance?.LlmServices?.InterpreterClient;

      /// <summary>
      ///   The arbitrated beat. Returns false only when the judge failed on the first round before anyone spoke: the caller then
      ///   runs the fixed order, which commits the player's line itself. Otherwise the beat is played and the line committed.
      /// </summary>
      private async Task<bool> RunArbitratedBeatAsync(string text, ChatSpeaker primary, int beatStart)
      {
         int cap = GroupFloorPolicy.LineCap(ModSettings.NpcCrossTalkRounds);
         var shownThisBeat = new Dictionary<string, string>();
         var npcLines = 0;
         var playerCommitted = false;
         string cutOff = null;
         string lastSpeakerId = null; // null: the player
         string lastText = text;

         while (npcLines < cap)
         {
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return true;
            if (_conversationEnded) break;

            (FloorDecision decision, List<FloorBid> bids, bool judged) = await DecideGroupRoundAsync(
               lastText, lastSpeakerId, playerCommitted ? null : text, primary?.Hero?.StringId, cutOff, npcLines, shownThisBeat);
            await MainThreadDispatcher.SwitchToMainThread();
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return true;

            if (!judged)
            {
               if (!playerCommitted)
               {
                  NoteGroup(ChatLogWhyKind.Judge, "the floor judge failed on every listener; this beat keeps the fixed order");

                  return false;
               }

               NoteGroup(ChatLogWhyKind.Judge, "the floor judge failed on every listener; the beat closes on any hanging question (EnsembleFloorPolicy)");
               await AnswerAnyHangingQuestionAsync(beatStart);

               return true;
            }

            NoteGroup(ChatLogWhyKind.Referee, GroupFloorPolicy.Describe(bids, decision, NameOfActive));
            cutOff = decision.CutOffId;

            string reactor = GroupFloorPolicy.PickReaction(new GroupReactionFacts {
               Decision = decision, LinesSinceReaction = _groupLinesSinceReaction, ShownEver = _groupShownEver.ToList()
            });
            if (reactor != null)
            {
               string reaction = bids.FirstOrDefault(b => b.Id == reactor)?.Reaction;
               _groupShownEver.Add(reactor);
               shownThisBeat[reactor] = reaction;
               _groupLinesSinceReaction = 0;
               await NarrateReactionAsync(reactor, reaction, playerCommitted ? null : text);
               if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return true;
            }
            else _groupLinesSinceReaction++;

            if (decision.Outcome != FloorOutcome.Speak) break;

            ChatSpeaker s = SpeakerForActive(decision.SpeakerId);
            if (s == null) break;

            ChatMessageViewModel placeholder = MakeSpeakerMessage(s, PlaceholderText);
            _messages.Add(placeholder);
            // The first voice answers the player's own line and commits it; every later voice answers the room as it stands
            // (the 13/09/2026 voice hijack: a co-voice handed the player's line wrote another character's answer).
            string stimulus = playerCommitted ? LastThingSaidInTheRoom() ?? text : text;
            int before = _transcript.Count;
            await RunLlmExchangeAsync(stimulus, s, placeholder, suppressPlayerLine: playerCommitted, suppressGroupTail: true);
            playerCommitted = true;
            npcLines++;

            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return true;
            if (_conversationEnded) break;

            TranscriptLine said = LastLineOf(s.Hero?.StringId, before);
            if (said == null) break; // the voice fell silent: the floor goes back to the player
            lastSpeakerId = s.Hero?.StringId;
            lastText = said.Text;
         }

         // Nobody spoke at all: the player's line still belongs to the scene.
         if (!playerCommitted && !_conversationEnded) RecordTranscript(true, "You", Hero.MainHero?.StringId, text);

         return true;
      }

      /// <summary>
      ///   One round (main thread in): reads who is present and what the game knows of them, judges them off the main thread,
      ///   and lets the referee decide. <paramref name="pendingPlayerLine" /> is the player's line not yet in the transcript, or
      ///   null. judged is false when every judge call failed (or nobody could be judged).
      /// </summary>
      private async Task<(FloorDecision Decision, List<FloorBid> Bids, bool Judged)> DecideGroupRoundAsync(
         string lastText, string lastSpeakerId, string pendingPlayerLine, string addressedByPlayerId, string cutOffId, int npcLines,
         IReadOnlyDictionary<string, string> shownThisBeat)
      {
         bool playerSpoke = lastSpeakerId == null;
         List<ChatSpeaker> present = (_cast?.ActiveIds ?? new List<string>()).Select(SpeakerForActive).Where(sp => sp?.Hero != null).ToList();
         List<ChatSpeaker> listeners = present.Where(sp => sp.Hero.StringId != lastSpeakerId).ToList();
         if (listeners.Count == 0) return (new FloorDecision {Outcome = FloorOutcome.BackToPlayer}, new List<FloorBid>(), true);

         // The scene so far, as the judge reads it, and who spoke each line (for the silences).
         string playerName = Hero.MainHero?.Name?.ToString() ?? "The visitor";
         var lines = new List<FloorJudgeLine>();
         var speakers = new List<string>();
         foreach (TranscriptLine l in _transcript.Lines.Where(l => l != null && !l.IsNarration && !string.IsNullOrWhiteSpace(l.Text)))
         {
            lines.Add(new FloorJudgeLine {Speaker = l.IsPlayer ? playerName : l.SpeakerName, Text = l.Text.Trim()});
            speakers.Add(l.IsPlayer ? null : l.SpeakerHeroId);
         }

         if (!string.IsNullOrWhiteSpace(pendingPlayerLine))
         {
            lines.Add(new FloorJudgeLine {Speaker = playerName, Text = pendingPlayerLine.Trim()});
            speakers.Add(null);
         }

         lines = lines.Skip(Math.Max(0, lines.Count - GroupSceneLines)).ToList();

         // Who the line calls on by name, among those who heard it; for the player's line, the one the chat resolved leads.
         var participants = listeners.Select(sp => new AddressedSpeakerDetector.Participant(sp.Hero.StringId, AddressedSpeakerDetector.DistinctiveNameTokens(sp.Name))).ToList();
         string calledOn = AddressedSpeakerDetector.Detect(lastText, participants);
         string addressed = playerSpoke ? calledOn ?? addressedByPlayerId : calledOn;
         int playerStanding = GroupFloorPolicy.StandingOf(Mapping.StatureReader.Player, false);
         int StandingOfSpeaker(ChatSpeaker sp) => GroupFloorPolicy.StandingOf(Mapping.StatureReader.Of(sp.Hero), sp.Hero.IsPlayerCompanion);
         int lastStanding = playerSpoke ? playerStanding : present.Where(sp => sp.Hero.StringId == lastSpeakerId).Select(StandingOfSpeaker).DefaultIfEmpty(-1).First();

         var room = new List<FloorJudgePresence> {new() {Name = playerName, Station = StationWords(playerStanding, false), IsFemale = Hero.MainHero?.IsFemale ?? false}};
         room.AddRange(present.Select(sp => new FloorJudgePresence {Name = sp.Name, Station = StationWords(StandingOfSpeaker(sp), sp.Hero.IsPlayerCompanion), IsFemale = sp.Hero.IsFemale}));

         string lower = (lastText ?? "").ToLowerInvariant();
         List<GroupFloorListener> unjudged = listeners.Select(sp => new GroupFloorListener {
            Id = sp.Hero.StringId, Standing = StandingOfSpeaker(sp), TurnsSilent = GroupFloorPolicy.TurnsSilent(speakers, sp.Hero.StringId),
            Named = AddressedSpeakerDetector.DistinctiveNameTokens(sp.Name).Any(tok => lower.Contains(tok)),
            LastShownReaction = shownThisBeat != null && shownThisBeat.TryGetValue(sp.Hero.StringId, out string seen) ? seen : null,
            TieBreak = _groupTieBreak.Next(1000)
         }).ToList();

         // A large room is not judged whole every line: those the line concerns first, then the longest silent.
         var round = new GroupFloorRound {
            Listeners = unjudged, LastSpeakerId = lastSpeakerId, PlayerSpokeLast = playerSpoke, AddressedId = addressed, CalledOnId = calledOn,
            LastSpeakerStanding = lastStanding, CutOffId = cutOffId
         };
         HashSet<string> toJudge = new(TurnArbiter.WhoToJudge(GroupFloorPolicy.BuildBids(round)));
         var asks = listeners.Where(sp => toJudge.Contains(sp.Hero.StringId)).Select(sp => (
            Id: sp.Hero.StringId,
            Request: new LlmRequest {
               SystemPrompt = FloorJudge.StablePrompt(new FloorJudgeListener {Name = sp.Name, Persona = sp.Profile?.Trait ?? sp.Profile?.Personality ?? "", Room = room}),
               Messages = new[] {new LlmMessage(MessageRole.User, FloorJudge.TurnPrompt(sp.Name, lines))},
               // Reasoning off, as every short reading of the scene engine (GLM 4.6 spent the dramaturge's budget thinking, 28/11/2026).
               Parameters = new LlmParameters {MaxTokens = 60, Creativity = 0.3f, AllowTruncationRetry = false, ReasoningOverride = "off"},
               Subject = "floor judge"
            })).ToList();

         // Read here, on the main thread: the note below is written after the calls, off it.
         Dictionary<string, string> names = present.GroupBy(sp => sp.Hero.StringId).ToDictionary(g => g.Key, g => g.First().Name);
         ILlmClient judge = FloorJudgeClient;
         CancellationToken token = _windowClosing.Token;
         TimeSpan limit = TimeSpan.FromSeconds(GroupFloorPolicy.JudgeTimeoutSeconds);
         (string Id, string Content)[] replies = await Task.Run(() => Task.WhenAll(asks.Select(async a => (a.Id, await AskJudgeAsync(judge, a.Request, limit, token).ConfigureAwait(false))))).ConfigureAwait(false);
         if (replies.Length == 0 || replies.All(r => r.Content == null)) return (null, null, false);

         Dictionary<string, FloorJudgeVerdict> verdicts = replies.Where(r => r.Content != null).ToDictionary(r => r.Id, r => FloorJudge.Parse(r.Content));
         var judgedRound = new GroupFloorRound {
            Listeners = unjudged.Select(l => new GroupFloorListener {
               Id = l.Id, Standing = l.Standing, TurnsSilent = l.TurnsSilent, Named = l.Named, LastShownReaction = l.LastShownReaction, TieBreak = l.TieBreak,
               Verdict = verdicts.TryGetValue(l.Id, out FloorJudgeVerdict v) ? v : null
            }).ToList(),
            LastSpeakerId = lastSpeakerId, PlayerSpokeLast = playerSpoke, AddressedId = addressed, CalledOnId = calledOn,
            LastSpeakerStanding = lastStanding, CutOffId = cutOffId
         };
         List<FloorBid> bids = GroupFloorPolicy.BuildBids(judgedRound);
         NoteGroup(ChatLogWhyKind.Judge, $"judged {replies.Count(r => r.Content != null)} of {asks.Count} listener(s): "
                                         + string.Join("; ", verdicts.Select(kv => $"{(names.TryGetValue(kv.Key, out string nm) ? nm : kv.Key)} {kv.Value.Intent.ToString().ToLowerInvariant()}{(kv.Value.Reaction != null ? " (" + kv.Value.Reaction + ")" : "")}")));

         return (TurnArbiter.Decide(new FloorRound {Bids = bids, NpcLinesSincePlayer = npcLines, PlayerSpokeLast = playerSpoke}), bids, true);
      }

      // One judge call within its limit: the reply's content, or null on any failure, timeout or cancel.
      private static async Task<string> AskJudgeAsync(ILlmClient client, LlmRequest request, TimeSpan limit, CancellationToken ct)
      {
         if (client == null) return null;

         try
         {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(limit);
               Task<LlmResponse> call = client.CompleteAsync(request, cts.Token);
               Task done = await Task.WhenAny(call, Task.Delay(limit, ct)).ConfigureAwait(false);
               if (done != call) return null;

               LlmResponse r = await call.ConfigureAwait(false);

               return r != null && r.IsSuccess ? r.Content ?? "" : null;
            }
         }
         catch (Exception)
         {
            return null;
         }
      }

      /// <summary>
      ///   Narrates one reaction (main thread in and out): a short line on the prose client, shown as the narrator's line. Skipped
      ///   with Scene Narration off (nothing would show, so nothing is paid), on a failure, a timeout, a refusal or an echo.
      /// </summary>
      private async Task NarrateReactionAsync(string reactorId, string reaction, string pendingPlayerLine)
      {
         ChatSpeaker who = SpeakerForActive(reactorId);
         if (who == null || string.IsNullOrWhiteSpace(reaction)) return;
         if (!ModSettings.SceneNarration)
         {
            NoteGroup(ChatLogWhyKind.Referee, $"{who.Name}'s reaction ({reaction}) not narrated: Scene Narration is off");

            return;
         }

         List<string> recent = _transcript.Lines.Where(l => l != null && !string.IsNullOrWhiteSpace(l.Text))
                                          .Select(l => l.IsNarration ? l.Text.Trim() : $"{(l.IsPlayer ? Hero.MainHero?.Name?.ToString() ?? "The visitor" : l.SpeakerName)}: {l.Text.Trim()}")
                                          .ToList();
         if (!string.IsNullOrWhiteSpace(pendingPlayerLine)) recent.Add($"{Hero.MainHero?.Name?.ToString() ?? "The visitor"}: {pendingPlayerLine.Trim()}");
         string scene = string.Join("\n", recent.Skip(Math.Max(0, recent.Count - GroupSceneLines)));
         List<string> sceneTexts = _transcript.Lines.Where(l => l != null).Select(l => l.Text).ToList();

         ILlmClient prose = FloorJudgeForDiagnostics ?? NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
         var request = new LlmRequest {
            SystemPrompt = GroupReactionPrompt.System,
            Messages = new[] {new LlmMessage(MessageRole.User, GroupReactionPrompt.User(scene, who.Name, who.Hero?.IsFemale ?? false, reaction))},
            Parameters = new LlmParameters {MaxTokens = 150, Creativity = 0.7f, AllowTruncationRetry = false, ReasoningOverride = "off"},
            Subject = "reaction"
         };
         string content = await Task.Run(() => AskJudgeAsync(prose, request, TimeSpan.FromSeconds(GroupFloorPolicy.ReactionTimeoutSeconds), _windowClosing.Token)).ConfigureAwait(false);
         await MainThreadDispatcher.SwitchToMainThread();
         if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

         string line = GroupReactionPrompt.Read(content, sceneTexts);
         if (line == null)
         {
            NoteGroup(ChatLogWhyKind.Referee, $"{who.Name}'s reaction ({reaction}) not narrated: no usable line came");

            return;
         }

         AppendModelNarration(line, line);
         NoteGroup(ChatLogWhyKind.Referee, $"{who.Name}'s reaction narrated ({reaction})");
      }

      // The first line the speaker said at or after transcript index <paramref name="from" />, or null.
      private TranscriptLine LastLineOf(string heroId, int from)
      {
         for (int i = _transcript.Count - 1; i >= Math.Max(0, from); i--)
         {
            TranscriptLine l = _transcript.Lines[i];
            if (l != null && !l.IsPlayer && !l.IsNarration && l.SpeakerHeroId == heroId && !string.IsNullOrWhiteSpace(l.Text)) return l;
         }

         return null;
      }

      private string NameOfActive(string id) => id == "_player" ? Hero.MainHero?.Name?.ToString() ?? "the player" : SpeakerForActive(id)?.Name ?? id;

      // The station the judge is told, from the standing the referee weighs.
      private static string StationWords(int standing, bool servesThePlayer)
      {
         if (servesThePlayer) return "a companion in the player's service";

         switch (standing)
         {
            case 4: return "rules a realm";
            case 3: return "head or kin of a great house";
            case 2: return "landed nobility";
            case 1: return "of an established house";
            default: return "of no great standing";
         }
      }

      private void NoteGroup(string kind, string text)
      {
         ModLog.Info($"[GROUP FLOOR] {kind}: {text}");
         NoteWhy(kind, $"{(kind == ChatLogWhyKind.Judge ? "Floor judge" : "Referee")}: {text}.");
      }

      /// <summary>
      ///   cr.test group_floor: one round after <paramref name="playerLine" /> through the real path (the cast, the bids, the stand-in
      ///   judge, the referee), without anyone speaking. Main thread in.
      /// </summary>
      internal async Task<(FloorDecision Decision, List<FloorBid> Bids, bool Judged)> DecideFirstRoundForDiagnosticsAsync(string playerLine)
      {
         ChatSpeaker primary = ResolveAddressedSpeaker(playerLine);
         // Without ConfigureAwait(false) this await resumes through the game thread's synchronization context, which only the game loop
         // drains: cr.test group_floor, waiting on this task while it pumps the dispatcher, then waited on itself (Gabriel's run, 28/11/2026).
         var result = await DecideGroupRoundAsync(playerLine, null, playerLine, primary?.Hero?.StringId, null, 0, new Dictionary<string, string>()).ConfigureAwait(false);
         await MainThreadDispatcher.SwitchToMainThread();

         return result;
      }
   }
}
