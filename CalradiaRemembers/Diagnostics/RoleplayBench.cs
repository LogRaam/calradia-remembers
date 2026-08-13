// Code written by Gabriel Mailhot, 12/08/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Settings;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>
   ///   The cr.rp_bench roleplay model bench. Plays TWO fixed, scripted scenes through whatever LLM the user has
   ///   configured in MCM and writes a readable transcript plus deterministic structural signals to
   ///   CalradiaRemembers_rp_bench.log, so the user can swap the roleplay model in MCM, re-run, and RANK models
   ///   BY READING. There is deliberately NO automated LLM judge: the user is the judge; the
   ///   <see cref="RoleplayBenchChecks" /> signals only point the eye at the turns worth reading.
   ///
   ///   This is a REAL-LLM, in-game tool the user runs live with their own key. It is NOT part of cr.testall and
   ///   has no cr.testall self-test: its pure checks are covered by RoleplayBenchChecksTests, and the runner
   ///   itself is validated by the user running cr.rp_bench and reading the output.
   /// </summary>
   public static class RoleplayBench
   {
      private const int MaxPasses = 5;

      /// <summary>A clearly masculine, culture-neutral name forced on the player for both scenes, so a bench that
      /// forces a MALE player (scene 2 is a male-player romance with a female partner) reads coherently instead of
      /// keeping the real character's possibly-female name.</summary>
      private const string MalePlayerName = "Aldric";

      /// <summary>The bench's own log file, written next to the main CalradiaRemembers.log so runs can be diffed across models.</summary>
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_rp_bench.log");

      private static readonly string[] Scene1Lines = {
         "Well met. It has been too long since Pendraic.",
         "I have come to ask a favor: lend your voice at the high council to back my claim.",
         "Bah. Truth be told, your clan has always been a pack of bog-crawling cowards.",
         "Forgive me, that was the drink and my temper. You have been a truer ally than most, and I spoke wrongly.",
         "Let us part as friends, then, and speak no more of it."
      };

      private static readonly string[] Scene2Lines = {
         "At last, alone. I have thought of little else but you since we last parted.",
         "*steps close and brushes a strand of hair from her face* You are even lovelier than I remembered.",
         "*draws her into a slow, deep kiss, one hand at the small of her back*",
         "*lifts her and carries her to the bed, working the laces of her gown loose*",
         "*lays her down and follows*"
      };

      /// <summary>
      ///   Plays both scenes through the configured model, once per pass (default one pass), and writes the report.
      ///   Returns a one-line confirmation pointing the user to the report file. Restores all borrowed/forced
      ///   state (player gender override, the seeded profiles, the forced adult level, the model mode) in a finally.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior, int passes = 1)
      {
         if (behavior == null) return "Calradia Remembers behavior not active. Start a campaign first.";
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         if (passes < 1) passes = 1;
         if (passes > MaxPasses) passes = MaxPasses;

         string roleplayModel = behavior.LlmServices.ModelId ?? "unknown";
         var report = new StringBuilder();

         report.AppendLine();
         report.AppendLine("========================================================================");
         report.AppendLine($"  cr.rp_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
         report.AppendLine($"  Passes: {passes}");
         report.AppendLine($"  Player: forced MALE, named \"{MalePlayerName}\" (so the staged identity reads coherently).");
         report.AppendLine("  MODELS: scene 1 uses your ROLEPLAY model; scene 2 FORCES the NSFW model, and Hardcore.");
         report.AppendLine($"    Scene 1 (roleplay model): {roleplayModel}");
         report.AppendLine("    Scene 2 (NSFW model): shown in the SCENE 2 header below; it MAY DIFFER from scene 1.");
         report.AppendLine("  Signals are ADVISORY (verbatimRepeats / refusal / earthRef). You are the judge - read the transcripts.");
         report.AppendLine("========================================================================");

         // Force a MALE player for both scenes so the scripted register (scene 1) and the male-player romance
         // (scene 2) are deterministic regardless of who the user actually plays. Restored in the finally.
         bool? priorGender = LlmServices.TestPlayerGenderOverride;
         string priorName = LlmServices.TestPlayerNameOverride;
         LlmServices.TestPlayerGenderOverride = false;
         LlmServices.TestPlayerNameOverride = MalePlayerName;
         behavior.LlmServices.Invalidate();

         try
         {
            for (var pass = 1; pass <= passes; pass++)
            {
               if (passes > 1)
               {
                  report.AppendLine();
                  report.AppendLine($"################################  PASS {pass} / {passes}  ################################");
               }

               report.AppendLine();
               report.Append(RunScene1(behavior));
               report.AppendLine();
               report.Append(RunScene2(behavior));
            }
         }
         catch (Exception ex)
         {
            report.AppendLine();
            report.AppendLine($"[BENCH ABORTED] {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            LlmServices.TestPlayerGenderOverride = priorGender;
            LlmServices.TestPlayerNameOverride = priorName;
            behavior.LlmServices.Invalidate();
         }

         string text = report.ToString();
         WriteReportFile(text);
         foreach (string line in text.Split('\n'))
            ModLog.Info($"[rp_bench] {line.TrimEnd()}");

         return $"cr.rp_bench done ({passes} pass(es), model {roleplayModel}). Full transcript + signals: {ReportPath}";
      }

      #region private

      // ── Scene 1: the lord and the affront ──────────────────────────────────

      /// <summary>
      ///   Scene 1: a Battanian lord with warm regard is asked a favour, gravely insulted, then apologised to.
      ///   Tests structure (does [DIALOGUE] hold), register, memory recall, and the "right action at the right
      ///   moment": a proportionate negative change_relation on the insult (turn 3).
      /// </summary>
      private static string RunScene1(NpcMemoryCampaignBehavior behavior)
      {
         Hero hero = SelectScene1Hero();

         if (hero == null)
            return "SCENE 1 - The lord and the affront\n  SKIPPED: no Battanian (or any) male lord available to stage the scene.\n";

         string id = hero.StringId;
         NpcProfile original = behavior.Store.Get(id);

         try
         {
            NpcProfile seed = BuildScene1Profile(hero);
            behavior.Store.Set(seed);

            string sceneModel = behavior.LlmServices.ModelId ?? "unknown"; // roleplay model (no mode forced here)
            var session = new ChatSession();
            var turns = new List<TurnResult>();
            foreach (string line in Scene1Lines)
               turns.Add(AskTurn(behavior, hero, seed, session, line));

            return FormatScene1(hero, turns, sceneModel);
         }
         finally
         {
            if (original != null) behavior.Store.Set(original);
            else behavior.Store.Remove(id);
         }
      }

      /// <summary>
      ///   Seeds the scene-1 lord's throwaway profile: warm personal regard (+12) and two prior memories, a
      ///   positive rescue at Pendraic (~40 days ago) and a small unmet favour, so the model can show recall.
      /// </summary>
      private static NpcProfile BuildScene1Profile(Hero hero)
      {
         NpcProfile p = HeroProfileMapper.FromHero(hero);
         var today = (int) CampaignTime.Now.ToDays;

         p.ReputationWithPlayer = 12;
         p.Events.Clear();
         p.Events.Add(new NotableEvent(Math.Max(1, today - 40), NotableEventType.Collaboration,
            "You pulled him from the press at Pendraic when his horse fell beneath him, and he has not forgotten it."));
         p.Events.Add(new NotableEvent(Math.Max(1, today - 55), NotableEventType.Agreement,
            "He still owes you for grain you loaned him in a hard winter and never called in."));
         p.Events.Sort((a, b) => a.gameDay.CompareTo(b.gameDay));

         return p;
      }

      private static string FormatScene1(Hero hero, List<TurnResult> turns, string sceneModel)
      {
         int formatHeld = turns.Count(t => t.FormatHeld);
         List<string> npcLines = turns.Select(t => t.Dialogue).ToList();
         int repeats = RoleplayBenchChecks.HasVerbatimRepeat(npcLines);
         bool refusal = turns.Any(t => t.LooksLikeRefusal);
         bool earthRef = turns.Any(t => t.MentionsEarthReference);

         string actionAtThree = "no";
         if (turns.Count >= 3)
         {
            GameAction rel = turns[2].Actions?.FirstOrDefault(a =>
               string.Equals(a.Type, "change_relation", StringComparison.OrdinalIgnoreCase));
            int? delta = ReadRelationDelta(rel);
            if (delta.HasValue && delta.Value < 0)
            {
               string band = delta.Value >= -15
                  ? "within a sane conversational band"
                  : "outside a sane band (absurdly harsh)";
               actionAtThree = $"yes (delta {delta.Value:+#;-#;0}, {band})";
            }
            else if (delta.HasValue)
               actionAtThree = $"emitted but NOT negative (delta {delta.Value:+#;-#;0})";
         }

         var sb = new StringBuilder();
         sb.AppendLine("SCENE 1 - The lord and the affront  (structure / register / memory)");
         sb.AppendLine($"  MODEL: {sceneModel}  (your ROLEPLAY model)");
         sb.AppendLine($"  NPC: {HeroLabelSafe(hero)} | seeded regard +12, two prior memories (Pendraic rescue, unpaid grain favour)");
         sb.AppendLine($"  SIGNALS: formatHeld={formatHeld}/{turns.Count}, change_relation@turn3={actionAtThree}, " +
                       $"verbatimRepeats={repeats}, refusal={YesNo(refusal)}, earthRef={YesNo(earthRef)}");
         AppendTranscript(sb, hero, turns);

         return sb.ToString();
      }

      // ── Scene 2: intimacy ──────────────────────────────────────────────────

      /// <summary>
      ///   Scene 2: an established, willing female partner met alone, at Hardcore. Tests romantic roleplay
      ///   quality and structure ([NARRATION] for physical action, an intimacy [EVENT] so the scene is
      ///   remembered) and, above all, whether the model REFUSES or censors explicit consensual content.
      ///   Forces AdultLevel = Hardcore and the NSFW model mode (mirroring a real explicit scene) for the
      ///   duration, restoring both in the finally.
      /// </summary>
      private static string RunScene2(NpcMemoryCampaignBehavior behavior)
      {
         Hero hero = SelectScene2Hero();

         if (hero == null)
            return "SCENE 2 - Intimacy\n  SKIPPED: no adult female lady available to stage the scene.\n";

         string id = hero.StringId;
         NpcProfile original = behavior.Store.Get(id);

         // Force Hardcore for this scene only. If MCM has not loaded, the dropdown is null and we cannot force
         // it: the scene then runs at the fallback level (Mature) and the report says so.
         ForcedAdultLevel adult = ForceHardcore(behavior, out string adultNote);
         ModelMode priorMode = LlmServices.ActiveModelMode;
         LlmServices.ActiveModelMode = ModelMode.Nsfw; // a real explicit scene uses the NSFW model if one is set
         behavior.LlmServices.Invalidate();
         string sceneModel = behavior.LlmServices.ModelId ?? "unknown";

         try
         {
            NpcProfile seed = BuildScene2Profile(hero);
            behavior.Store.Set(seed);

            var session = new ChatSession();
            var turns = new List<TurnResult>();
            foreach (string line in Scene2Lines)
               turns.Add(AskTurn(behavior, hero, seed, session, line));

            return FormatScene2(hero, turns, sceneModel, adultNote);
         }
         finally
         {
            if (original != null) behavior.Store.Set(original);
            else behavior.Store.Remove(id);

            adult?.Restore();
            LlmServices.ActiveModelMode = priorMode;
            behavior.LlmServices.Invalidate();
         }
      }

      /// <summary>
      ///   Seeds the scene-2 partner's throwaway profile: high regard (+45), a romantic profile oriented toward
      ///   the male player (heterosexual, committed, attracted), a couple of preferences and kinks, and a prior
      ///   intimate memory, so an intimate scene is warranted rather than sprung cold.
      /// </summary>
      private static NpcProfile BuildScene2Profile(Hero hero)
      {
         NpcProfile p = HeroProfileMapper.FromHero(hero);
         var today = (int) CampaignTime.Now.ToDays;

         p.ReputationWithPlayer = 45;
         p.Events.Clear();
         p.Events.Add(new NotableEvent(Math.Max(1, today - 20), NotableEventType.Intimacy,
            "A night shared after the harvest feast, unhurried and unspoken of since."));

         RomanticProfile r = p.Romantic ?? RomanticProfileBuilder.Build(hero, p.Trait ?? "");
         r.Orientation = SexualOrientation.Heterosexual; // she is female, so this is attraction to men
         r.Status = RomanticStatus.Committed;
         r.AttractionToPlayer = 60;
         EnsurePreference(r, RomanticPreference.Devoted);
         EnsurePreference(r, RomanticPreference.Intense);
         EnsureKink(r, Kink.Praise);
         EnsureKink(r, Kink.Submission);
         p.Romantic = r;

         return p;
      }

      private static string FormatScene2(Hero hero, List<TurnResult> turns, string sceneModel, string adultNote)
      {
         int replied = turns.Count(t => t.HasDialogueOrNarration);
         int narrationUsed = turns.Count(t => !string.IsNullOrWhiteSpace(t.Narration));
         List<string> npcLines = turns.Select(t => t.Dialogue).ToList();
         int repeats = RoleplayBenchChecks.HasVerbatimRepeat(npcLines);
         bool intimacyEvent = turns.Any(t => t.HasIntimacyEvent);
         // The key Hardcore signal: an out-of-fiction refusal OR an empty/errored response either way reads as
         // the model balking at explicit consensual content.
         bool refusalOrCensor = turns.Any(t => t.LooksLikeRefusal || t.IsEmptyOrErrored);

         var sb = new StringBuilder();
         sb.AppendLine("SCENE 2 - Intimacy  (romantic roleplay quality / romantic structure)  [Hardcore]");
         sb.AppendLine($"  MODEL: {sceneModel}  (your NSFW model, NOT the roleplay model; set the NSFW slot to compare a model here){(string.IsNullOrEmpty(adultNote) ? "" : " | " + adultNote)}");
         sb.AppendLine($"  NPC: {HeroLabelSafe(hero)} | seeded regard +45, filled romantic profile (hetero, committed), prior intimate memory");
         sb.AppendLine($"  SIGNALS: dialogueOrNarration={replied}/{turns.Count}, narrationUsed={narrationUsed}/{turns.Count}, " +
                       $"intimacyEvent={YesNo(intimacyEvent)}, refusal/censor={YesNo(refusalOrCensor)}, verbatimRepeats={repeats}");
         AppendTranscript(sb, hero, turns);

         return sb.ToString();
      }

      // ── Shared turn driving + rendering ────────────────────────────────────

      /// <summary>
      ///   Drives ONE scripted player turn through the true prompt pipeline (EncounterContextBuilder.Build ->
      ///   ChatService.ChatAsync -> parse), the same seam CrSelfTest.AskLlmParsed uses, accumulating the shared
      ///   <paramref name="session" /> so the scene carries forward. Any throw or timeout (a hard API refusal
      ///   included) is caught and recorded on the turn, so one bad turn never crashes the bench.
      /// </summary>
      private static TurnResult AskTurn(NpcMemoryCampaignBehavior behavior, Hero npc, NpcProfile profile, ChatSession session, string playerLine)
      {
         var turn = new TurnResult {PlayerLine = playerLine};

         try
         {
            LlmServices services = behavior.LlmServices;

            if (services is not {IsAvailable: true})
            {
               turn.Error = "LLM not configured";

               return turn;
            }

            EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, playerLine);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};

            int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(budgetSeconds));
            CancellationToken ct = cts.Token;

            Task<NpcChatResult> task = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, playerLine, ctx, ct), ct);
            task.Wait(ct);
            NpcChatResult result = task.GetAwaiter().GetResult();

            if (result is not {IsSuccess: true})
            {
               turn.Error = result?.ErrorMessage ?? "no result";

               return turn;
            }

            turn.Response = result.Response ?? new ParsedResponse {Dialogue = ""};
         }
         catch (OperationCanceledException)
         {
            turn.Error = "model refused or errored (timed out)";
         }
         catch (AggregateException ex)
         {
            Exception inner = ex.InnerException ?? ex;
            turn.Error = $"model refused or errored ({inner.GetType().Name}: {inner.Message})";
         }
         catch (Exception ex)
         {
            turn.Error = $"model refused or errored ({ex.GetType().Name}: {ex.Message})";
         }

         return turn;
      }

      /// <summary>Renders each turn as a clear, readable block: the player line, the NPC reply, then any narration / action / event / error.</summary>
      private static void AppendTranscript(StringBuilder sb, Hero hero, List<TurnResult> turns)
      {
         string npcName = hero?.Name?.ToString() ?? "NPC";

         for (var i = 0; i < turns.Count; i++)
         {
            TurnResult t = turns[i];
            sb.AppendLine();
            sb.AppendLine($"  --- Turn {i + 1} ---");
            sb.AppendLine($"  YOU: {t.PlayerLine}");

            if (t.Error != null)
            {
               sb.AppendLine($"  [ERROR at turn {i + 1}] {t.Error}");

               continue;
            }

            sb.AppendLine($"  {npcName}: {(string.IsNullOrWhiteSpace(t.Dialogue) ? "(no spoken line)" : t.Dialogue.Trim())}");

            if (!string.IsNullOrWhiteSpace(t.Narration))
               sb.AppendLine($"  [NARRATION] {t.Narration.Trim()}");

            if (t.Actions != null)
               foreach (GameAction a in t.Actions)
                  sb.AppendLine($"  [ACTION] {a.Type}{FormatParams(a)}");

            if (t.Response?.NewEventData != null)
               sb.AppendLine($"  [EVENT] {t.Response.NewEventData.Type}: {t.Response.NewEventData.Summary}");
         }
      }

      // ── Hero selection ─────────────────────────────────────────────────────

      /// <summary>A living, free, adult MALE Battanian lord with Honor and Valor, relaxing each criterion in turn so the scene can always stage.</summary>
      private static Hero SelectScene1Hero()
      {
         List<Hero> pool = Hero.AllAliveHeroes
            .Where(h => h != null && h != Hero.MainHero && h.IsLord && !h.IsFemale && !h.IsPrisoner && h.IsAlive && !h.IsChild)
            .ToList();

         bool IsBattanian(Hero h) => string.Equals(h.Culture?.StringId, "battania", StringComparison.OrdinalIgnoreCase);
         bool HonorValor(Hero h) => h.GetTraitLevel(DefaultTraits.Honor) > 0 && h.GetTraitLevel(DefaultTraits.Valor) > 0;

         return pool.FirstOrDefault(h => IsBattanian(h) && HonorValor(h))
                ?? pool.FirstOrDefault(IsBattanian)
                ?? pool.FirstOrDefault(HonorValor)
                ?? pool.FirstOrDefault();
      }

      /// <summary>A living, free, adult FEMALE noble (a lady lord preferred, else any adult woman) for the intimacy scene.</summary>
      private static Hero SelectScene2Hero()
      {
         return Hero.AllAliveHeroes.FirstOrDefault(h =>
                   h != null && h != Hero.MainHero && h.IsFemale && h.IsLord && !h.IsPrisoner && h.IsAlive && !h.IsChild)
                ?? Hero.AllAliveHeroes.FirstOrDefault(h =>
                   h != null && h != Hero.MainHero && h.IsFemale && !h.IsPrisoner && h.IsAlive && !h.IsChild);
      }

      // ── Helpers ────────────────────────────────────────────────────────────

      private static void EnsurePreference(RomanticProfile r, RomanticPreference pref)
      {
         if (r.Preferences != null && !r.Preferences.Contains(pref)) r.Preferences.Add(pref);
      }

      private static void EnsureKink(RomanticProfile r, Kink kink)
      {
         if (r.Kinks != null && !r.Kinks.Contains(kink)) r.Kinks.Add(kink);
      }

      /// <summary>Reads the proposed integer delta of a change_relation action, or null when absent/unparseable.</summary>
      private static int? ReadRelationDelta(GameAction action)
      {
         if (action?.Parameters == null) return null;
         if (action.Parameters.TryGetValue("delta", out string raw) && int.TryParse(raw, out int d)) return d;

         return null;
      }

      /// <summary>The prompt-safe hero label (name / house / culture), falling back to the bare name if the pure labeler is unavailable.</summary>
      private static string HeroLabelSafe(Hero hero)
      {
         try { return HeroLabel.Describe(hero?.Name?.ToString(), hero?.Clan?.Name?.ToString(), hero?.Culture?.Name?.ToString()); }
         catch { return hero?.Name?.ToString() ?? "NPC"; }
      }

      private static string FormatParams(GameAction a)
      {
         if (a.Parameters == null || a.Parameters.Count == 0) return "";

         return " (" + string.Join(", ", a.Parameters.Select(kv => $"{kv.Key}={kv.Value}")) + ")";
      }

      private static string YesNo(bool value) => value ? "yes" : "no";

      /// <summary>
      ///   Forces the MCM adult-content level to Hardcore for the duration of scene 2, returning a token that
      ///   restores the prior value. When MCM has not loaded (the dropdown is null), returns null and sets
      ///   <paramref name="note" /> so the report is honest that the scene could not be forced to Hardcore.
      /// </summary>
      private static ForcedAdultLevel ForceHardcore(NpcMemoryCampaignBehavior behavior, out string note)
      {
         var dropdown = CrSettings.Instance?.AdultContentLevel;

         if (dropdown == null)
         {
            note = "WARNING: MCM settings not loaded, could NOT force Hardcore (scene ran at the fallback level).";

            return null;
         }

         note = "";
         int priorIndex = dropdown.SelectedIndex;
         dropdown.SelectedValue = "Hardcore";
         behavior.LlmServices.Invalidate();

         return new ForcedAdultLevel(dropdown, priorIndex, behavior);
      }

      #endregion

      /// <summary>Restore token for the forced adult-content dropdown: puts the selected index back and rebuilds the chat service.</summary>
      private sealed class ForcedAdultLevel
      {
         private readonly NpcMemoryCampaignBehavior _behavior;
         private readonly MCM.Common.Dropdown<string> _dropdown;
         private readonly int _priorIndex;

         public ForcedAdultLevel(MCM.Common.Dropdown<string> dropdown, int priorIndex, NpcMemoryCampaignBehavior behavior)
         {
            _dropdown = dropdown;
            _priorIndex = priorIndex;
            _behavior = behavior;
         }

         public void Restore()
         {
            _dropdown.SelectedIndex = _priorIndex;
            _behavior.LlmServices.Invalidate();
         }
      }

      /// <summary>One scripted turn's parsed outcome plus the derived, per-turn advisory signals the report reads.</summary>
      private sealed class TurnResult
      {
         public string PlayerLine { get; set; }
         public ParsedResponse Response { get; set; }
         public string Error { get; set; }

         public IReadOnlyList<GameAction> Actions => Response?.Actions;
         public string Dialogue => Response?.Dialogue ?? "";
         public string Narration => Response?.Narration ?? "";

         public bool FormatHeld => Error == null && !string.IsNullOrWhiteSpace(Dialogue);
         public bool HasDialogueOrNarration => Error == null && (!string.IsNullOrWhiteSpace(Dialogue) || !string.IsNullOrWhiteSpace(Narration));
         public bool IsEmptyOrErrored => Error != null || (string.IsNullOrWhiteSpace(Dialogue) && string.IsNullOrWhiteSpace(Narration));

         public bool HasIntimacyEvent => Response?.NewEventData != null
                                         && (Response.NewEventData.Type == NotableEventType.Intimacy
                                             || Response.NewEventData.Type == NotableEventType.Flirt);

         public bool LooksLikeRefusal => RoleplayBenchChecks.LooksLikeRefusal($"{Dialogue} {Narration}");
         public bool MentionsEarthReference => RoleplayBenchChecks.MentionsEarthReference($"{Dialogue} {Narration}");
      }

      private static void WriteReportFile(string content)
      {
         try
         {
            string dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(ReportPath, content);
         }
         catch (Exception ex)
         {
            ModLog.Error($"cr.rp_bench could not write {ReportPath}: {ex.Message}");
         }
      }
   }
}
