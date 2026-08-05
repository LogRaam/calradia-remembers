// Code written by Gabriel Mailhot, 25/06/2026.
// Birth-time clan correction for CR-created pregnancies. A child of a CR conception ALWAYS belongs to the
// MOTHER's clan: player-mother / legitimate-wife → the player's clan (they are already in it), another woman →
// her own house (a bastard). Vanilla DeliverOffSpring routes a player-FATHERED child into the player's clan,
// so we reassign it at birth — but ONLY for pregnancies CR itself created, leaving vanilla NPC patrilineality
// untouched. See PregnancyService for the conception side.
//
// 05/08/2026 addition, stuck-pregnancy cleanup (player report): a campaign with the Life/Death Cycle option
// disabled (no aging, no death) never runs vanilla's own offspring-delivery check (decompiled:
// PregnancyCampaignBehavior.DailyTickHero gates the whole thing behind !IsLifeDeathCycleDisabled), so a CR
// conception made under that setting sat "pregnant" forever and the NPC never stopped mentioning it in chat.
// The daily tick below sweeps every CR-tracked mother and, only when births are disabled AND her due date is
// well past, ends the pregnancy CHILDLESS (no child is born, which would override the player's own no-birth
// choice). This is a deliberate compromise: unsticking the conversation without silently reversing a campaign
// setting the player deliberately chose.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Tracks the mothers CR has made pregnant and, at birth, places their offspring in the MOTHER's clan.
   ///   The tracked set is persisted (a pregnancy spans ~84 game-days, often across saves). Also runs a
   ///   daily sweep that ends a pregnancy childless when the campaign's Life/Death Cycle option is disabled
   ///   and her due date has gone stuck well past due (see the 05/08/2026 file-header note).
   /// </summary>
   public sealed class PregnancyBirthBehavior : CampaignBehaviorBase
   {
      /// <summary>
      ///   Tuning: how many days past her due date a mother must be, WITH births disabled, before the
      ///   daily sweep ends her pregnancy. Gives vanilla's own check (and ordinary day-boundary jitter
      ///   right at the due date) a fair window first. See <see cref="CalradiaRemembers.Logic.PregnancyPolicy" />.
      /// </summary>
      private const int OverdueMarginDays = 5;

      private const string SaveKey = "CalradiaRemembers_CrExpectingMothers_v1";

      public static PregnancyBirthBehavior Instance { get; private set; }

      private List<string> _crMotherIds = new();

      public PregnancyBirthBehavior() => Instance = this;

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
         => dataStore.SyncData(SaveKey, ref _crMotherIds);

      /// <summary>
      ///   Registers a mother CR has just made pregnant, so her child is reassigned to her clan at birth.
      ///   Called by <see cref="PregnancyService" /> after a successful conception.
      /// </summary>
      public void TrackConception(Hero mother)
      {
         if (mother == null) return;
         if (!_crMotherIds.Contains(mother.StringId)) _crMotherIds.Add(mother.StringId);
      }

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         // Only correct CR's own pregnancies; vanilla NPC births keep their (patrilineal) clan.
         if (mother == null || !_crMotherIds.Remove(mother.StringId) || aliveChildren == null) return;

         Clan motherClan = mother.Clan;
         foreach (Hero child in aliveChildren)
         {
            if (child == null || child.Clan == motherClan) continue;
            child.Clan = motherClan;
            ModLog.Info($"[PREGNANCY] CR birth: moved {child.Name} into the mother's clan ({motherClan?.Name?.ToString() ?? "—"}).");
         }
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         // A mother who dies while pregnant will never give birth — drop her so the set can't leak.
         if (victim != null) _crMotherIds.Remove(victim.StringId);
      }

      /// <summary>
      ///   Daily sweep for the stuck-pregnancy bug (see file header): while births are enabled this does
      ///   nothing at all, since vanilla's own daily check delivers on schedule and CR must never step on
      ///   that. Only when the campaign's Life/Death Cycle option is disabled does it check every
      ///   CR-tracked mother's due date and, once she is overdue past <see cref="OverdueMarginDays" />,
      ///   end her pregnancy childless via <see cref="PregnancyService.TryEndStuckPregnancy" />, the
      ///   reflective remove symmetric to the reflective add that created it. Guarded per-mother so one
      ///   failure never stops the sweep for the rest; never throws out of this method.
      /// </summary>
      private void OnDailyTick()
      {
         if (_crMotherIds.Count == 0) return;

         bool birthsDisabled = CampaignOptions.IsLifeDeathCycleDisabled;
         if (!birthsDisabled) return; // vanilla's own daily check handles normal delivery, nothing to do here.

         foreach (string motherId in _crMotherIds.ToList())
         {
            try
            {
               Hero mother = Hero.FindFirst(h => h.StringId == motherId);
               if (mother == null)
               {
                  _crMotherIds.Remove(motherId);

                  continue;
               }

               if (!PregnancyService.TryGetDaysOverdue(mother, out int daysOverdue))
               {
                  // No tracked pregnancy left in the engine's own list (already delivered or cleared some
                  // other way), so nothing left for us to track either.
                  _crMotherIds.Remove(motherId);

                  continue;
               }

               if (!PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled, daysOverdue, OverdueMarginDays)) continue;

               if (!PregnancyService.TryEndStuckPregnancy(mother)) continue;

               _crMotherIds.Remove(motherId);
               ModLog.Info($"[PREGNANCY] ended a stuck pregnancy for {mother.Name}: this campaign's Life/Death " +
                           $"Cycle option is disabled, so vanilla would never have delivered it (due {daysOverdue} " +
                           "day(s) ago). No child is born, this respects the player's own no-birth setting rather " +
                           "than overriding it.");
            }
            catch (Exception ex)
            {
               ModLog.Error($"[PREGNANCY] stuck-pregnancy cleanup failed for hero {motherId}: {ex.GetType().Name}: {ex.Message}");
            }
         }
      }
   }
}
