// Code written by Gabriel Mailhot, 17/11/2026.
// Goeth (Discord, 17/11/2026): long memory, the actual words of earlier talks. Gabriel (17/11/2026): the story archive is
// written always; the MCM switch "Long Memory" (off by default) governs only what reaches the prompt, "pour epargner des
// tokens". This reads the loaded save's branch of the archive, asks LongMemoryRecall which passages bear on this turn for
// this character, and labels them for the prompt: [YOU] for the character, [PLAYER], or the speaker's name as it is now (a
// name learned later shows in old words too). Full prompt only.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.StoryArchive;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Behaviors
{
   /// <summary>Finds, for one character's turn, the words of earlier conversations worth recalling, ready for the prompt.</summary>
   public static class LongMemoryRecaller
   {
      /// <summary>The passages for <paramref name="characterId" />'s turn, or null when the switch is off, the prompt is compact, or nothing bears on it.</summary>
      public static IReadOnlyList<RecalledExchange> For(string characterId, string query, LeanPromptLevel lean, StoryArchiveStore archive = null)
      {
         try
         {
            if (!ModSettings.LongMemory || lean != LeanPromptLevel.Full || string.IsNullOrEmpty(characterId) || string.IsNullOrWhiteSpace(query)) return null;

            StoryArchiveStore store = archive ?? NpcMemoryCampaignBehavior.Instance?.StoryArchive;
            if (store == null || Campaign.Current == null) return null;

            HashSet<string> already = new HashSet<string>((ChatManager.ActiveViewModel?.Transcript?.Lines ?? Enumerable.Empty<Logic.TranscriptLine>())
                                                          .Select(l => l?.Text).Where(t => !string.IsNullOrEmpty(t)));
            IReadOnlyList<StoryArchiveRecord> branch = store.ReadBranch();
            string current = ChatManager.ActiveViewModel?.ArchiveEpisodeForRecall;
            // The threads this turn is on before any search: what the player's line names, and what this talk is already about.
            Hero speaker = Hero.FindFirst(h => h != null && h.StringId == characterId);
            var queryThreads = new HashSet<string>(Mapping.StoryThreadKeys.For(speaker, query));
            if (current != null)
               foreach (StoryArchiveRecord r in branch)
                  if (r?.EpisodeId == current && r.Threads != null)
                     queryThreads.UnionWith(r.Threads);
            IReadOnlyList<LongMemoryPassage> passages = LongMemoryRecall.Find(new LongMemoryRecallFacts {
               Records = branch, CharacterId = characterId, Query = query, CurrentEpisodeId = current, QueryThreads = queryThreads,
               AlreadyInContext = already, BudgetChars = ModSettings.LongMemoryBudget
            });
            if (passages.Count == 0) return null;

            double now = CampaignTime.Now.ToDays;
            List<RecalledExchange> recalled = passages.Select(p => new RecalledExchange {
               When = Elapsed(now - p.Time), Place = p.PlaceName,
               About = About(p), Latest = p.IsLatestOnThread,
               AlsoPresent = p.AlsoPresentIds.Select(NameOf).Where(n => !string.IsNullOrWhiteSpace(n)).ToList(),
               Lines = p.Lines.Select(l => new RecalledLine {
                  Speaker = l.IsPlayer ? "PLAYER" : l.SpeakerId == characterId ? "YOU" : NameOf(l.SpeakerId) ?? l.SpeakerName ?? "someone",
                  Text = l.Text
               }).ToList()
            }).ToList();
            ModLog.Info($"[LONG MEMORY] {characterId}: {recalled.Count} passage(s), {recalled.Sum(r => r.Lines.Count)} line(s) recalled.");
            // The conversation log (Gabriel, 28/11/2026): which passages this turn was given, never their words.
            ChatManager.ActiveViewModel?.NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Recall,
               $"{NameOf(characterId) ?? characterId} recalled {recalled.Count} passage(s): "
               + string.Join("; ", recalled.Select(r => $"{r.When}{(string.IsNullOrWhiteSpace(r.Place) ? "" : " at " + r.Place)}"
                                                        + $"{(string.IsNullOrWhiteSpace(r.About) ? "" : ", about " + r.About)}, {r.Lines.Count} line(s){(r.Latest ? ", latest on its thread" : "")}")));

            return recalled;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LONG MEMORY] recall failed, the turn goes without it: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static string About(LongMemoryPassage p)
      {
         List<string> labels = (p.ThreadKeys ?? new List<string>()).Select(Mapping.StoryThreadKeys.LabelOf).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().Take(3).ToList();

         return labels.Count == 0 ? null : string.Join(", ", labels);
      }

      private static string NameOf(string heroId)
      {
         if (string.IsNullOrEmpty(heroId)) return null;

         return Hero.FindFirst(h => h != null && h.StringId == heroId)?.Name?.ToString();
      }

      private static string Elapsed(double days)
      {
         int d = (int) Math.Floor(Math.Max(0, days));

         return d == 0 ? "earlier today" : d == 1 ? "yesterday" : d < 14 ? $"{d} days ago" : d < 7 * 6 ? $"{d / 7} weeks ago" : $"{d / NpcMemoryService.Core.Calendar.WorldCalendar.DaysPerSeason} seasons ago";
      }
   }
}
