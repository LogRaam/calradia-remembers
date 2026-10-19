// Code written by Gabriel Mailhot, 30/09/2026.
// THE PERCEIVED IMAGE, increment P2 (ROADMAP, SELF-ESTEEM): the live side of PerceivedImagePolicy (Logic, tested), for
// the character dossier in the chat. Reads his self-esteem, his day as the player has moved it in this conversation,
// whether the player knows him well (personal regard at friendly or better), and whether he keeps face (a calculating
// mind, or the bearing of a great house or a crown).

#region

using System;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Mood;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Standing
{
   /// <summary>How a character seems to the player, in one sentence.</summary>
   public static class PerceivedImageService
   {
      /// <summary>The sentence for the dossier, or null when there is nothing to say.</summary>
      public static string For(Hero hero, NpcProfile profile)
      {
         if (hero == null || hero == Hero.MainHero || !HeroPresence.IsInTheWorld(hero)) return null;

         try
         {
            DayMood? day = DayMoodReader.Draw(hero).Mood;

            return PerceivedImagePolicy.Compose(new PerceivedImageFacts {
               SelfEsteem = SelfEsteemService.BandFor(hero, profile),
               Mood = ConversationMood.NowFor(hero.StringId, day) ?? day,
               IsFemale = hero.IsFemale,
               KnowsHimWell = (profile?.ReputationWithPlayer ?? 0) >= Regard.FriendlyTier,
               KeepsFace = hero.GetTraitLevel(DefaultTraits.Calculating) > 0 || StatureReader.Of(hero).Station >= 2
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[IMAGE] could not compose how {hero.Name} seems: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
