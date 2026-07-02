// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   Drives companion↔companion standing from trait compatibility and weds the well-matched. Native
   ///   relations and marriages are vanilla-serialized, so this stores nothing of its own.
   /// </summary>
   public sealed class CompanionRapportBehavior : CampaignBehaviorBase
   {
      private const int MarriageChancePercent = 25; // weekly chance an eligible pair actually weds

      // Tuning (flagged).
      private const int RelationSoftCap = 40; // don't let the weekly nudge push a pair past ±this
      public static CompanionRapportBehavior Instance;

      public CompanionRapportBehavior()
      {
         Instance = this;
      }

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>Runs a camp-life tick on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore) { } // native relations + marriages persist themselves

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      private static int Compatibility(Hero a, Hero b)
         => CompanionRapport.Compatibility(
            a.GetTraitLevel(DefaultTraits.Valor), a.GetTraitLevel(DefaultTraits.Honor),
            a.GetTraitLevel(DefaultTraits.Mercy), a.GetTraitLevel(DefaultTraits.Generosity),
            a.GetTraitLevel(DefaultTraits.Calculating),
            b.GetTraitLevel(DefaultTraits.Valor), b.GetTraitLevel(DefaultTraits.Honor),
            b.GetTraitLevel(DefaultTraits.Mercy), b.GetTraitLevel(DefaultTraits.Generosity),
            b.GetTraitLevel(DefaultTraits.Calculating));

      private static void NudgeRelation(Hero a, Hero b)
      {
         int compat = Compatibility(a, b);
         int dir = Math.Sign(compat);

         if (dir == 0) return;

         int rel = a.GetRelation(b);

         if (dir > 0 && rel >= RelationSoftCap) return;
         if (dir < 0 && rel <= -RelationSoftCap) return;

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(a, b, dir, false);
      }

      private static void TryMarry(Hero a, Hero b)
      {
         if (a.Spouse != null || b.Spouse != null) return;
         bool opposite = a.IsFemale != b.IsFemale;
         bool adult = !a.IsChild && !b.IsChild;

         if (!CompanionRapport.CanMarry(a.GetRelation(b), true, opposite, adult)) return;
         if (MBRandom.RandomInt(100) >= MarriageChancePercent) return;

         try
         {
            MarriageAction.Apply(a, b, false);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{a.Name} and {b.Name} have wed — your clan grows."), 0, a.CharacterObject);
            ModLog.Info($"[CAMP] {a.Name} and {b.Name} married (companions, kept in your clan).");
         }
         catch (Exception ex) { ModLog.Error($"[CAMP] marriage of {a.Name} & {b.Name} failed: {ex.Message}"); }
      }

      private void OnWeeklyTick()
      {
         List<Hero> companions = Companions().ToList();
         for (var i = 0; i < companions.Count; i++)
            for (int j = i + 1; j < companions.Count; j++)
            {
               Hero a = companions[i],
                    b = companions[j];
               // One pair's rapport pass throwing must not skip every other pair this week.
               SafeRun.Step($"CompanionRapportBehavior.OnWeeklyTick({a.Name}, {b.Name})", () =>
               {
                  NudgeRelation(a, b);
                  TryMarry(a, b);
               });
            }
      }

      #endregion
   }
}