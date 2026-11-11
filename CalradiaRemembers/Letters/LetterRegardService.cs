// Code written by Gabriel Mailhot, 28/09/2026.
// Regard earned by letter (Lordfadooboo, Nexus, 28/09/2026). The rules are LetterRegardPolicy's (pure, tested); this
// applies the reader's judgement to their profile, on the game thread.

#region

using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Letters;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Moves a letter reader's regard by their judgement of the player's letter.</summary>
   public static class LetterRegardService
   {
      /// <summary>
      ///   Applies <paramref name="tone" /> to <paramref name="profile" />'s regard for the player and returns the shift
      ///   applied (0 when nothing moved). Call on the game thread; the caller persists the profile.
      /// </summary>
      public static int Apply(NpcProfile profile, LetterTone tone, int today)
      {
         if (profile == null) return 0;

         int shift = LetterRegardPolicy.Shift(new LetterRegardFacts {
            Tone = tone,
            CurrentRegard = profile.ReputationWithPlayer,
            LastGainDay = profile.LastLetterRegardGainDay,
            Today = today,
            Warmth = ModSettings.RelationWarmth
         });

         if (shift != 0)
         {
            ProfileMutator.ApplyReputationDelta(profile, shift, NpcMemoryService.Core.Standing.RegardCause.Letter);
            if (shift > 0) profile.LastLetterRegardGainDay = today;
            Announce(profile, shift);
         }

         ModLog.Info($"[LETTER] {profile.Name} judged the player's letter {tone}: regard {shift:+#;-#;0} (now {profile.ReputationWithPlayer}).");

         return shift;
      }

      // Schmidt (Discord, 11/11/2026): the same banner and sound a word face to face raises, so a letter's cost is seen.
      private static void Announce(NpcProfile profile, int shift)
      {
         string notice = LetterRegardPolicy.Notice(profile.Name, shift);
         if (notice == null) return;

         try
         {
            Hero reader = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
            MBInformationManager.AddQuickInformation(new TextObject(notice), 0, reader?.CharacterObject);
            string sound = shift > 0 ? ModSettings.RelationIncreaseSound : ModSettings.RelationDecreaseSound;
            if (!string.IsNullOrWhiteSpace(sound)) SoundEvent.PlaySound2D(sound);
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[LETTER] regard banner failed for {profile.Name}: {ex.Message}");
         }
      }
   }
}
