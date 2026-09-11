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
         => CompanionRapport.Compatibility(new RomanticTraits {Valor = a.GetTraitLevel(DefaultTraits.Valor), Honor = a.GetTraitLevel(DefaultTraits.Honor), Mercy = a.GetTraitLevel(DefaultTraits.Mercy), Generosity = a.GetTraitLevel(DefaultTraits.Generosity), Calculating = a.GetTraitLevel(DefaultTraits.Calculating)}, new RomanticTraits {Valor = b.GetTraitLevel(DefaultTraits.Valor), Honor = b.GetTraitLevel(DefaultTraits.Honor), Mercy = b.GetTraitLevel(DefaultTraits.Mercy), Generosity = b.GetTraitLevel(DefaultTraits.Generosity), Calculating = b.GetTraitLevel(DefaultTraits.Calculating)});

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

         // Vanilla has the final say: MarriageAction.Apply silently no-ops unless the marriage model deems the
         // couple suitable (extra checks our own guards do not make, e.g. for a same-clan companion pair). Ask it
         // up front rather than roll, "wed", and announce a marriage that never took (the phantom in the report).
         if (Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(a, b) != true) return;

         if (MBRandom.RandomInt(100) >= MarriageChancePercent) return;

         try
         {
            MarriageAction.Apply(a, b, false);

            // Only announce a wedding that ACTUALLY took. MarriageAction can quietly no-op on an edge the guards
            // above miss (a same-clan companion pair is not the cross-clan case vanilla marriage assumes), and a
            // player who is told two companions wed but can then find no marriage anywhere is exactly the
            // unverifiable claim this mod refuses to make (player report). If it did not take, log it and stay
            // silent rather than announce a phantom.
            if (a.Spouse != b || b.Spouse != a)
            {
               ModLog.Error($"[CAMP] marriage of {a.Name} & {b.Name} did not take (MarriageAction.Apply no-op); not announced.");

               return;
            }

            MBInformationManager.AddQuickInformation(
               new TextObject($"{a.Name} and {b.Name} have wed, your clan grows."), 0, a.CharacterObject);
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