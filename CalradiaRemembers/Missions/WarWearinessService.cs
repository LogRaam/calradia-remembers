// Code written by Gabriel Mailhot, 30/06/2026.
// Engine glue around the pure WarWeariness model: reads/writes a companion's war-weariness on their stored
// NpcProfile and applies the tolls. The bands, tolls, tolerance and rest are pure and tested in
// CalradiaRemembers.Logic; this only moves the score between the game and that logic.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>The single place a companion's war-weariness is read from / written to their profile.</summary>
   public static class WarWearinessService
   {
      /// <summary>The companion's current war-weariness, or 0 (fresh) when they have no profile yet.</summary>
      public static int Get(Hero hero) => ProfileOf(hero)?.WarWeariness ?? WarWeariness.Min;

      /// <summary>Adds the toll of one battle wound to the companion's stored score (no-op without a profile).</summary>
      public static void AddWound(Hero hero) => Move(hero, WarWeariness.ApplyWound, "wound");

      /// <summary>Adds the toll of one capture to the companion's stored score (no-op without a profile).</summary>
      public static void AddCapture(Hero hero) => Move(hero, WarWeariness.ApplyCapture, "capture");

      /// <summary>Eases the companion's war-weariness toward zero by one rest step (a no-op when already rested).</summary>
      public static void Rest(Hero hero, int step)
      {
         NpcProfile p = ProfileOf(hero);
         if (p == null) return;

         int after = WarWeariness.Rest(p.WarWeariness, step);
         if (after == p.WarWeariness) return;
         p.WarWeariness = after;
         Store?.Set(p);
      }

      /// <summary>Sets the score directly (console test path), clamped.</summary>
      public static void Set(Hero hero, int value)
      {
         NpcProfile p = ProfileOf(hero);
         if (p == null) return;
         p.WarWeariness = WarWeariness.Clamp(value);
         Store?.Set(p);
      }

      /// <summary>Reads the "stepped back from the field" flag (a landed veteran who withdrew to their fief).</summary>
      public static bool HasSteppedBack(Hero hero) => ProfileOf(hero)?.SteppedBackFromWar ?? false;

      /// <summary>Sets the "stepped back from the field" flag on the companion's profile.</summary>
      public static void SetSteppedBack(Hero hero, bool value)
      {
         NpcProfile p = ProfileOf(hero);
         if (p == null || p.SteppedBackFromWar == value) return;
         p.SteppedBackFromWar = value;
         Store?.Set(p);
      }

      private static void Move(Hero hero, System.Func<int, int> op, string reason)
      {
         NpcProfile p = ProfileOf(hero);
         if (p == null) return;

         int before = p.WarWeariness;
         p.WarWeariness = op(before);
         if (p.WarWeariness == before) return;

         Store?.Set(p);
         ModLog.Info($"[WEARINESS] {hero.Name}: {before}→{p.WarWeariness} ({reason}).");
      }

      private static BannerlordNpcMemoryStore Store => NpcMemoryCampaignBehavior.Instance?.Store;

      private static NpcProfile ProfileOf(Hero hero) => hero == null ? null : Store?.Get(hero.StringId);
   }
}
