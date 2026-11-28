// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 3 of the group-scene plan, the composed intro (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "au début de chaque CHAT on ajoute l'intro", fed with the encounter: the place, the hour, the season, how the
// player comes (armour, companions), and above all a battle the two just fought together. His rule: the composer "chooses
// among true facts and stages them; it invents nothing the game tracks".
//
// The engine half. It IS the scene opener (ChatViewModel.SceneOpener.cs), entered from OpenWithScene when "Composed opening
// scene" is on (ComposedOpeningPolicy): same greeting on screen meanwhile, same open input, same set-aside when the player
// speaks first. The opener's call becomes the composer's, plus one cheap check (ComposedOpeningComposer). Delivered, the
// paragraph is shown in the narrator's purple above the greeting (which stands as the first words), and the concern and the
// tension go to the main character's prompt for this conversation only (EncounterContext.ComposedOpening). Anything else
// falls back to today's opener turn, silently for the player, with a line in the log and the conversation log.
//
// The facts are read here, on the main thread, from the readers the turn already uses: EncounterContextBuilder (where you
// are, the hour, the season and the sky, what the player wears, the talk of the realm, who sits in), the stock sections of
// the character's own prompt (who they are, their history with the player), and the profile's last days (a battle fought
// beside the player is written there by WorldEventFeeders). Nothing is read twice by another road.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // The stock sections of the character's own prompt the composer reads: who they are, then their past with the player.
      private static readonly string[] CharacterSectionKeys = {"identity", "personality", "authored_backstory", "matters_most"};
      private static readonly string[] HistorySectionKeys = {"history", "meeting_memory"};

      private ComposedOpening _composedOpening;
      private string _composedOpeningWhy;

      /// <summary>cr.test composed_intro: stand-ins for the composer and the check. Null in play (the live clients are used).</summary>
      internal static ILlmClient ComposerForDiagnostics;

      /// <summary>cr.test composed_intro: the check's stand-in.</summary>
      internal static ILlmClient CheckerForDiagnostics;

      /// <summary>cr.test composed_intro: on a fall back, end the opening instead of running today's opener (which calls the model).</summary>
      internal static bool StopAtFallbackForDiagnostics;

      /// <summary>cr.test composed_intro: what the composed opening left the character, or null.</summary>
      internal ComposedOpening ComposedOpeningForDiagnostics => _composedOpening;

      /// <summary>cr.test composed_intro: the last line written about the composed opening, or null.</summary>
      internal string ComposedOpeningWhyForDiagnostics => _composedOpeningWhy;

      /// <summary>cr.test composed_intro: whether an opening of this conversation would be composed now.</summary>
      internal bool ComposesOpeningForDiagnostics => ComposedOpeningPolicy.ShouldCompose(ComposedOpeningFactsFor(MainSpeaker()));

      /// <summary>cr.test composed_intro: what the composed opening left the main character, as the turn hands it on.</summary>
      internal ComposedOpening ComposedOpeningForMainForDiagnostics => ComposedOpeningFor(MainSpeaker());

      /// <summary>What the composed opening left <paramref name="speaker" />: the main character only, null for anyone else.</summary>
      internal ComposedOpening ComposedOpeningFor(ChatSpeaker speaker) => speaker?.IsMain == true ? _composedOpening : null;

      /// <summary>The facts the composed opening is decided on, read live.</summary>
      internal ComposedOpeningFacts ComposedOpeningFactsFor(ChatSpeaker speaker)
         => new() {
            SwitchOn = ModSettings.ComposedOpening,
            SceneNarrationOn = ModSettings.SceneNarration,
            EnvoyAudience = IsEnvoyAudience,
            CaptiveScene = IsCaptiveEncounter || _captorScene,
            CompanionBriefing = IsCompanionBriefingChat,
            Council = ChatManager.ActiveSkin == ChatManager.CouncilSkin,
            SpokeToday = SpokeWithThePlayerToday(speaker?.Profile)
         };

      // A talk, a council or a captive scene leaves an identified memory dated the day it closed (the conversation recap),
      // so one dated today means they already met the player in a talk today. The current talk writes its own at the close.
      private static bool SpokeWithThePlayerToday(NpcProfile profile)
      {
         int? today = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.GetCurrentGameDay();
         if (profile?.Events == null || today == null) return false;

         return profile.Events.Any(e => e != null && e.gameDay == today.Value && !string.IsNullOrEmpty(e.Id) && !e.IsPlayerWritten);
      }

      /// <summary>
      ///   The composed opening: reads the facts (main thread), runs the composer and the check off it, and applies the result
      ///   back on it. Falls back to today's opener turn on anything short of a checked paragraph.
      /// </summary>
      private async Task RunComposedOpeningAsync(ChatSpeaker speaker)
      {
         ComposedOpeningResult result;
         try
         {
            ComposedOpeningSheet sheet = ReadComposedOpeningSheet(speaker);
            var services = NpcMemoryCampaignBehavior.Instance?.LlmServices;
            ILlmClient composer = ComposerForDiagnostics ?? services?.LlmClient;
            ILlmClient checker = CheckerForDiagnostics ?? services?.InterpreterClient;
            TimeSpan composerLimit = TimeSpan.FromSeconds(ComposedOpeningPolicy.ComposerTimeoutSeconds(ModSettings.LlmResponseTimeoutSeconds));
            TimeSpan checkLimit = TimeSpan.FromSeconds(ComposedOpeningPolicy.CheckTimeoutSeconds);
            CancellationToken token = SceneOpenerToken;
            result = await Task.Run(() => ComposedOpeningComposer.ComposeAsync(sheet, composer, checker, composerLimit, checkLimit, token)).ConfigureAwait(false);
         }
         catch (Exception ex)
         {
            result = new ComposedOpeningResult {Why = $"the facts could not be read ({ex.GetType().Name}: {ex.Message})"};
         }

         await MainThreadDispatcher.SwitchToMainThread();

         // The chat closed, or another replaced it, while the calls were out: nothing of this belongs to it.
         if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

         try
         {
            // The player spoke first: the greeting already stands and his turn owns the scene; nothing of this is applied.
            if (_sceneOpenerAbandoned || result.Cancelled)
            {
               NoteComposedOpening("set aside: the player spoke before it came.");
               EndSceneOpenerFlight();

               return;
            }

            if (result.Delivered && result.Reply != null)
            {
               ApplyComposedOpening(result.Reply);
               NoteComposedOpening($"composed and shown ({result.Why}).");
               EndSceneOpenerFlight();

               return;
            }

            NoteComposedOpening($"fell back to the ordinary opener: {result.Why}.");
            if (StopAtFallbackForDiagnostics)
            {
               SettleSceneOpener(SceneOpenerOutcome.Failed);
               EndSceneOpenerFlight();

               return;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENE] the composed opening could not be applied: {ex.GetType().Name}: {ex.Message}");
            SettleSceneOpener(SceneOpenerOutcome.Failed);
            EndSceneOpenerFlight();

            return;
         }

         // Today's opener, as if the switch were off: the flight stays open, the greeting still stands meanwhile.
         _ = RunLlmExchangeAsync(SceneOpenerPolicy.Cue, speaker, _sceneOpenerGreeting, true, isSceneOpener: true);
      }

      /// <summary>
      ///   Shows the paragraph in the narrator's purple above the greeting, which stands as the first words and is remembered
      ///   after it, and keeps the concern and the tension for the main character's prompt. Scene Narration still decides the
      ///   display (the greeting is passed as the spoken line, so the policy treats it as an ordinary turn).
      /// </summary>
      private void ApplyComposedOpening(ComposedOpeningReply reply)
      {
         _sceneOpenerCommitted = true;
         AppendModelNarration(reply.Setting, _sceneOpenerGreetingText ?? "", _sceneOpenerGreeting);
         var opening = new ComposedOpening {
            Concern = string.IsNullOrWhiteSpace(reply.Concern) ? null : reply.Concern,
            Tension = string.IsNullOrWhiteSpace(reply.Tension) ? null : reply.Tension
         };
         _composedOpening = opening.HasContent ? opening : null;
         SettleSceneOpener(SceneOpenerOutcome.Composed);
         // The scene is set: the first player turn must not ask for an opening tableau again (the opener turn that would have
         // consumed this never ran).
         _openingNarrationPending = false;
         if (IsLiveConversation) EnsureStoryIntro();
      }

      private void NoteComposedOpening(string text)
      {
         _composedOpeningWhy = text;
         ModLog.Info($"[SCENE] composed opening: {text}");
         NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Intro, $"Composed opening: {text}");
      }

      /// <summary>
      ///   What the game knows, for the composer and the check: read on the main thread from the readers a turn uses. The
      ///   context is built as the turn builds it (the same history cut, the same meeting record frozen at the opening).
      /// </summary>
      internal ComposedOpeningSheet ReadComposedOpeningSheet(ChatSpeaker speaker)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = speaker?.Profile;
         if (behavior?.GameStateBridge == null || profile == null) return null;

         EncounterContext context = EncounterContextBuilder.Build(
            speaker.Hero, profile, behavior.GameStateBridge, null, BuildContextWitnesses(speaker),
            historyKnownThrough: HistoryKnownThrough(profile),
            questsKnownThrough: QuestsKnownThrough(profile),
            memoriesDiscussedAtOpening: MemoriesDiscussedAtOpening(profile),
            freezeMeetingMemory: true,
            meetingMemoryAtOpening: MeetingMemoryAtOpening(profile));
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         var world = new WorldState {
            CurrentDay = today,
            Season = behavior.GameStateBridge.GetCalendarDate(),
            TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
            ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
            Realms = behavior.GameStateBridge.GetRealmStanding(),
            Rumors = ""
         };

         Dictionary<string, string> sections = new();
         PromptBuilder builder = behavior.LlmServices?.DebugBuildPromptBuilder();
         if (builder != null)
            foreach (PromptSectionRendering s in builder.RenderStockSections(profile, world, context))
               if (!string.IsNullOrWhiteSpace(s.Text)) sections[s.Key] = s.Text.Trim();

         string Join(IEnumerable<string> keys) => string.Join("\n\n", keys.Where(sections.ContainsKey).Select(k => sections[k]));

         HereAndNowFacts now = context.HereAndNow;
         return new ComposedOpeningSheet {
            NpcName = profile.Name,
            PlayerName = Hero.MainHero?.Name?.ToString(),
            Character = Join(CharacterSectionKeys),
            History = Join(HistorySectionKeys),
            LivedLately = LivedLately(profile, today),
            RealmNews = RealmTalk(context),
            Place = PlaceOf(context),
            Season = now == null || now.Season == Season.Unknown ? null : now.Season.ToString().ToLowerInvariant(),
            TimeOfDay = now == null ? null : ComposedOpeningPolicy.TimeOfDay(now.HourOfDay, now.IsNight),
            Weather = WeatherWords(now?.Weather ?? Weather.Unknown),
            Activity = context.NpcCurrentActivity,
            Presence = context.PresenceNote,
            PlayerGear = context.PlayerGear,
            Present = (context.Witnesses ?? new List<WitnessEntry>()).Select(w => w?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList(),
            Greeting = _sceneOpenerGreetingText,
            Language = ModSettings.ReplyLanguage
         };
      }

      // The last two days of the character's own record, oldest first, dated in words: a battle fought beside the player is
      // among them (WorldEventFeeders writes it), with whatever else they lived.
      private static IReadOnlyList<string> LivedLately(NpcProfile profile, int today)
         => (profile.Events ?? new List<NotableEvent>())
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.summary) && e.gameDay <= today && e.gameDay >= today - 2)
            .OrderBy(e => e.gameDay)
            .Select(e => $"{(today - e.gameDay == 0 ? "today" : today - e.gameDay == 1 ? "yesterday" : "two days ago")}: {e.summary.Trim()}")
            .Reverse().Take(ComposedOpeningPrompt.MaxListLines).Reverse()
            .ToList();

      private static IReadOnlyList<string> RealmTalk(EncounterContext context)
      {
         var lines = new List<string>();
         RealmNewsFacts news = context.RealmNews;
         if (!string.IsNullOrWhiteSpace(news?.Headline)) lines.Add(news.Headline);
         if (news?.Tidings != null) lines.AddRange(news.Tidings.Select(t => t?.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
         if (lines.Count == 0 && !string.IsNullOrWhiteSpace(context.RealmNewsLine)) lines.Add(context.RealmNewsLine);

         return lines;
      }

      // The WHERE YOU ARE note when they stand in a place; else the nearest place, how it lies, and whose lands these are.
      private static string PlaceOf(EncounterContext context)
      {
         if (!string.IsNullOrWhiteSpace(context.CurrentLocationNote)) return context.CurrentLocationNote;

         HereAndNowFacts now = context.HereAndNow;
         if (string.IsNullOrWhiteSpace(now?.NearestPlace)) return null;
         if (now.IsInsideThatPlace) return "in " + now.NearestPlace;

         string lies = string.IsNullOrWhiteSpace(now.BearingToPlace) ? "near" : now.BearingToPlace;
         string realm = string.IsNullOrWhiteSpace(now.RealmName) ? "" : $", in the lands of {now.RealmName}";

         return $"out in the country; {now.NearestPlace} lies {lies}{realm}";
      }

      private static string WeatherWords(Weather weather)
      {
         switch (weather)
         {
            case Weather.Clear: return "clear skies";
            case Weather.LightRain: return "light rain";
            case Weather.HeavyRain: return "heavy rain";
            case Weather.Snow: return "snow falling";
            case Weather.Blizzard: return "a blizzard";
            case Weather.Storm: return "a storm";
            default: return null;
         }
      }
   }
}
