// Code written by Gabriel Mailhot, 26/10/2026.
// fearente (Nexus, 26/10/2026): the story a wanderer tells at the first meeting was never remembered, and a companion hired
// a week before spoke of a childhood shared with the player. The recorder hands each line of that story here as the game
// says it (rendered, its variables already filled), and it is kept as ONE memory of the wanderer's own life, the part of
// the prompt that says "your own life, apart from this player". Which lines, and how kept: WandererBackstoryPolicy.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Dialogue
{
   /// <summary>Keeps a wanderer's own story, as told at the first meeting, as one memory of their own life.</summary>
   internal static class WandererBackstoryKeeper
   {
      /// <summary>Adds <paramref name="line" /> (a line of <paramref name="wanderer" />'s story) to the one memory that keeps it.</summary>
      public static void Keep(Hero wanderer, string line)
      {
         try
         {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (wanderer == null || store == null || string.IsNullOrWhiteSpace(line)) return;

            NpcProfile profile = store.Get(wanderer.StringId) ?? HeroProfileMapper.FromHero(wanderer);
            int at = profile.Events.FindIndex(e => WandererBackstoryPolicy.IsTheKeptStory(e.summary));
            string before = at >= 0 ? profile.Events[at].summary : null;
            string kept = WandererBackstoryPolicy.Keep(before, line);
            if (kept == before) return;

            int day = at >= 0 ? profile.Events[at].gameDay : (int) CampaignTime.Now.ToDays;
            var memory = new NotableEvent(day, NotableEventType.OwnLife, kept);
            if (at >= 0) profile.Events[at] = memory;
            else profile.Events.Add(memory);
            store.Set(profile);
            ModLog.Info($"[WANDERER] {wanderer.Name} keeps a line of the story told at your first meeting.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WANDERER] keeping the story failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
