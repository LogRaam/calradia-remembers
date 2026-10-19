// Code written by Gabriel Mailhot, 30/09/2026.
// SELF-ESTEEM, increments E2/E3 (ROADMAP, ruled 30/09/2026): the live side of SelfEsteemPolicy (Logic, tested). Reads
// a character's baseline and temper off the game, keeps what has befallen him on his profile, and answers the band.
// Ruling 3: only characters the player has met (a CR profile) keep one; nobody keeps the self-regard of all Calradia.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Mood;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Standing
{
   /// <summary>Records what moves a character's self-esteem, and reads it back as a band.</summary>
   public static class SelfEsteemService
   {
      /// <summary>How long a blow or a gladness still weighs on the day's mood.</summary>
      public const int PersonalMoodDays = 7;

      /// <summary>
      ///   Records one event for <paramref name="hero" /> when the player has met him; nothing otherwise. Game thread.
      ///   Also marks the day's personal blow or gladness for the mood of the day, with what it was and
      ///   <paramref name="subject" /> (whom or what it concerned), so the day can name its cause.
      /// </summary>
      public static void Record(Hero hero, SelfEsteemEvent e, string subject = null)
      {
         try
         {
            if (!HeroPresence.IsInTheWorld(hero) || hero == Hero.MainHero) return;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(hero.StringId);
            if (profile == null) return;

            var today = (int) CampaignTime.Now.ToDays;
            // A gain shrinks as he climbs (fkasad, 01/10/2026), by the MCM's Self-Esteem Climb.
            var climb = new SelfEsteemClimb {Baseline = BaselineOf(hero), Exponent = ModSettings.SelfEsteemClimb};
            SelfEsteemState after = SelfEsteemPolicy.Record(StateOf(profile), e, TemperOf(hero), today, climb);

            profile.SelfEsteemShift = after.Shift;
            profile.SelfEsteemShiftDay = after.ShiftDay;
            profile.SelfEsteemCapturedOnDays = after.CapturedOnDays ?? new List<int>();
            if (IsBlow(e))
            {
               profile.LastPersonalBlowDay = today;
               profile.LastPersonalBlowKind = e.ToString();
               profile.LastPersonalBlowSubject = subject;
            }

            if (IsGladness(e))
            {
               profile.LastPersonalGladnessDay = today;
               profile.LastPersonalGladnessKind = e.ToString();
               profile.LastPersonalGladnessSubject = subject;
            }
            store.Set(profile);

            ModLog.Info($"[SELF-ESTEEM] {hero.Name}: {e}, shift now {after.Shift:+#;-#;0} ({BandFor(hero, profile)}).");
         }
         catch (Exception ex) { ModLog.Error($"[SELF-ESTEEM] could not record {e} for {hero?.Name}: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>How <paramref name="hero" /> regards himself today, or null for the player or no one.</summary>
      public static SelfEsteemBand? BandFor(Hero hero, NpcProfile profile)
      {
         if (hero == null || hero == Hero.MainHero) return null;

         var today = (int) CampaignTime.Now.ToDays;
         int baseline = BaselineOf(hero);

         return SelfEsteemPolicy.BandOf(SelfEsteemPolicy.Current(baseline, profile == null ? null : StateOf(profile), today));
      }

      /// <summary>Where a man starts: his station, his valour, his own draw.</summary>
      private static int BaselineOf(Hero hero)
         => SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {
            Station = StatureReader.Of(hero).Station,
            Valor = Trait(hero, DefaultTraits.Valor),
            PersonalDraw = SelfEsteemPolicy.PersonalDraw(hero.StringId)
         });

      /// <summary>Whether a blow still weighs on his day.</summary>
      public static bool HasRecentBlow(NpcProfile profile)
         => profile?.LastPersonalBlowDay is { } d && (int) CampaignTime.Now.ToDays - d <= PersonalMoodDays;

      /// <summary>Whether a gladness still lifts his day.</summary>
      public static bool HasRecentGladness(NpcProfile profile)
         => profile?.LastPersonalGladnessDay is { } d && (int) CampaignTime.Now.ToDays - d <= PersonalMoodDays;

      private static SelfEsteemState StateOf(NpcProfile p)
         => new SelfEsteemState {Shift = p.SelfEsteemShift, ShiftDay = p.SelfEsteemShiftDay, CapturedOnDays = p.SelfEsteemCapturedOnDays ?? new List<int>()};

      private static SelfEsteemTemper TemperOf(Hero hero)
         => new SelfEsteemTemper {Valor = Trait(hero, DefaultTraits.Valor), Calculating = Trait(hero, DefaultTraits.Calculating)};

      private static int Trait(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      private static bool IsBlow(SelfEsteemEvent e)
         => e == SelfEsteemEvent.FiefLost || e == SelfEsteemEvent.KinDied || e == SelfEsteemEvent.Captured || e == SelfEsteemEvent.BattleLost;

      private static bool IsGladness(SelfEsteemEvent e)
         => e == SelfEsteemEvent.FiefGained || e == SelfEsteemEvent.TournamentWon || e == SelfEsteemEvent.BattleWon
            || e == SelfEsteemEvent.BattleWonAgainstOdds;
   }
}
