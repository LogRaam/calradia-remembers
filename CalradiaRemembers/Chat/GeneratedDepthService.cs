// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 4 of the group-scene plan, "generated depth" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain"; on the secret (27/11/2026): "Il peut y avoir des secrets
// personnels complémentaires au jeu.. si ça ne touche pas à une mécanique du jeu ... Le problème arrive lorsque le LLM invente
// quelque chose que le jeu peut contredire."
//
// The engine half. With "Deeper characters" on, the first model turn of an ordinary one-to-one conversation with a character
// that has no depth yet (ChatViewModel.GeneratedDepth.cs, GeneratedDepthPolicy) reads the character's sheet here, on the main
// thread, from the very sections its prompt shows, and starts one background call to the Background model (the housekeeping
// client, behind the same hold and time limit as the summary), then the secret's check on the interpreter model. The chat
// never waits: the depth lands on the profile in one reference write and shows from the next conversation on. Generated once,
// never regenerated (cr.depth_clear is the one way back). With "Lasting memory of meetings" on, the secret is also seeded into
// the meeting record as still kept from the player, so its told and untold state is tracked from then on.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Character;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Generates and keeps a character's depth, once, in the background.</summary>
   public static class GeneratedDepthService
   {
      // The stock sections of the character's own prompt the generator reads: who they are, their ties, their past with the
      // player. mod_private is left out on purpose: what another mod confided is not sent to one more call.
      private static readonly string[] SheetSectionKeys = {
         "identity", "archetype", "personality", "authored_backstory", "authored_conviction", "mod_backstory", "character_voice", "relationships",
         "family_compact", "matters_most", "fear_and_superstition", "background_context", "history", "meeting_memory"
      };

      private static readonly object Gate = new();
      private static readonly HashSet<string> InFlight = new(StringComparer.Ordinal);
      private static readonly HashSet<string> FailedThisSession = new(StringComparer.Ordinal);

      /// <summary>Whether a generation for this profile is running.</summary>
      internal static bool IsInFlight(string profileId)
      {
         if (string.IsNullOrEmpty(profileId)) return false;
         lock (Gate) return InFlight.Contains(profileId);
      }

      /// <summary>Whether a generation for this profile already came back with nothing this session.</summary>
      internal static bool FailedBefore(string profileId)
      {
         if (string.IsNullOrEmpty(profileId)) return false;
         lock (Gate) return FailedThisSession.Contains(profileId);
      }

      /// <summary>cr.depth_clear and cr.test generated_depth: forget that a profile failed this session.</summary>
      internal static void ForgetFailure(string profileId)
      {
         if (string.IsNullOrEmpty(profileId)) return;
         lock (Gate) FailedThisSession.Remove(profileId);
      }

      /// <summary>
      ///   Reads the sheet now (main thread) and starts the generation in the background. Returns at once; the caller has already
      ///   asked <see cref="GeneratedDepthPolicy.ShouldGenerate" />. Never throws.
      /// </summary>
      internal static void Start(NpcMemoryCampaignBehavior behavior, Hero hero, NpcProfile profile)
      {
         try
         {
            if (behavior == null || hero == null || profile?.Id == null) return;
            lock (Gate)
               if (!InFlight.Add(profile.Id)) return;

            GeneratedDepthSheet sheet = ReadSheet(behavior, hero, profile);
            int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;
            string player = Hero.MainHero?.Name?.ToString() ?? "";
            ILlmClient generator = behavior.LlmServices?.HousekeepingClient;
            ILlmClient checker = behavior.LlmServices?.InterpreterClient;
            ModLog.Info($"[DEPTH] {profile.Name}: generating a depth in the background (voice {(sheet.AskVoice ? "asked" : "authored")}, fear {(sheet.AskFear ? "asked" : "the game's")}).");
            _ = Task.Run(() => GenerateAndKeepAsync(behavior, profile, sheet, generator, checker, day, player));
         }
         catch (Exception ex)
         {
            lock (Gate) InFlight.Remove(profile?.Id ?? "");
            ModLog.Error($"[DEPTH] could not start: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Runs the two calls and keeps the depth on <paramref name="profile" /> (one reference write, then the store). Returns the
      ///   result; never throws. The seam cr.test generated_depth drives with stand-in clients. Drops the result when the profile is
      ///   no longer the one the store holds (another save was loaded meanwhile) or already has a depth.
      /// </summary>
      internal static async Task<GeneratedDepthResult> GenerateAndKeepAsync(NpcMemoryCampaignBehavior behavior, NpcProfile profile, GeneratedDepthSheet sheet,
                                                                         ILlmClient generator, ILlmClient checker, int day, string playerName)
      {
         GeneratedDepthResult result = null;
         try
         {
            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"generated depth for {profile.Name}");

            TimeSpan generatorLimit = TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds));
            TimeSpan checkLimit = TimeSpan.FromSeconds(GeneratedDepthPolicy.CheckTimeoutSeconds);
            result = await GeneratedDepthGenerator.GenerateAsync(sheet, generator, checker, day, generatorLimit, checkLimit, CancellationToken.None).ConfigureAwait(false);

            if (result.Depth == null)
            {
               lock (Gate) FailedThisSession.Add(profile.Id);
               ModLog.Info($"[DEPTH] {profile.Name}: nothing kept ({result.Why}); not asked again this session.");

               return result;
            }

            if (!ReferenceEquals(NpcMemoryCampaignBehavior.Instance, behavior) || !ReferenceEquals(behavior.Store?.Get(profile.Id), profile))
            {
               ModLog.Info($"[DEPTH] {profile.Name}: the campaign changed while it was written; dropped.");

               return new GeneratedDepthResult {Why = "the campaign changed"};
            }

            if (profile.GeneratedDepth != null) return new GeneratedDepthResult {Depth = profile.GeneratedDepth, Why = "a depth was already kept"};

            profile.GeneratedDepth = result.Depth;
            if (ModSettings.MeetingMemory)
            {
               MeetingMemory seeded = GeneratedDepthPolicy.SeedSecret(profile.MeetingMemory, result.Depth.Secret, playerName, day);
               if (seeded != null) profile.MeetingMemory = seeded;
            }

            behavior.Store.Set(profile);
            GeneratedDepth d = result.Depth;
            ModLog.Info($"[DEPTH] {profile.Name}: kept for good ({result.Why}). voice: {d.Voice} | mask: {d.Mask} | fear: {d.Fear} | limit: {d.Limit} | "
                      + $"secret: {(d.Secret.Length > 0 ? d.Secret : result.SecretDropped ? "(dropped)" : "(none)")}");

            return result;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DEPTH] {profile?.Name}: failed: {ex.GetType().Name}: {ex.Message}");

            return result ?? new GeneratedDepthResult {Why = ex.GetType().Name};
         }
         finally
         {
            lock (Gate) InFlight.Remove(profile?.Id ?? "");
         }
      }

      /// <summary>
      ///   The character's sheet as its prompt shows it (main thread): the stock sections of the live prompt builder, rendered with
      ///   a context built as a turn builds it. What the game already gives decides what is asked: no voice beside an authored
      ///   backstory, no fear beside the game's own.
      /// </summary>
      internal static GeneratedDepthSheet ReadSheet(NpcMemoryCampaignBehavior behavior, Hero hero, NpcProfile profile)
      {
         var sections = new Dictionary<string, string>();
         PromptBuilder builder = behavior?.LlmServices?.DebugBuildPromptBuilder();
         if (builder != null && behavior.GameStateBridge != null)
         {
            EncounterContext context = EncounterContextBuilder.Build(hero, profile, behavior.GameStateBridge, null);
            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetCalendarDate(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Realms = behavior.GameStateBridge.GetRealmStanding(),
               Rumors = ""
            };
            foreach (PromptSectionRendering s in builder.RenderStockSections(profile, world, context))
               if (!string.IsNullOrWhiteSpace(s.Text)) sections[s.Key] = s.Text.Trim();
         }

         return new GeneratedDepthSheet {
            NpcName = profile?.Name,
            Sheet = string.Join("\n\n", SheetSectionKeys.Where(sections.ContainsKey).Select(k => sections[k])),
            AskVoice = string.IsNullOrWhiteSpace(profile?.AuthoredBackstory),
            AskFear = profile == null || profile.Fear == Fear.None
         };
      }
   }
}
