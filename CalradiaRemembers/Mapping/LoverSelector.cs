// Code written by Gabriel Mailhot, 01/11/2026.
// Who a lover would be, moved whole out of OpenPartnerLoverBehavior so the hidden affairs between lords (Gabriel's "go",
// 01/11/2026) pick by the same scan as the player's open partner, and that behaviour need not know them.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Finds the lover a character would take.</summary>
   internal static class LoverSelector
   {
      /// <summary>
      ///   Scans the living heroes for the best-acquainted plausible lover for <paramref name="partner" />,
      ///   gating each candidate through the pure <see cref="OpenPartnerLoverPolicy.IsPlausibleLover" /> and
      ///   inferring compatibility the way CR infers a match elsewhere (unwed adult of a sex she is drawn to,
      ///   an associate, never the player or the player's own household). Prefers the highest relation to her,
      ///   with an <see cref="MBRandom" /> tie-break so equal ties do not always pick the same hero.
      /// </summary>
      internal static Hero Best(Hero partner, NpcProfile profile)
      {
         var player = Hero.MainHero;
         Clan playerClan = Clan.PlayerClan;

         Hero best = null;
         int bestRel = int.MinValue;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == null || h == partner) continue;

            int rel = h.GetRelation(partner);
            bool acquainted = rel > 0 || (h.MapFaction != null && h.MapFaction == partner.MapFaction);
            // The partner's own orientation against the candidate's sex: the single authoritative compatibility
            // rule (RomanticProfile.IsCompatibleWith), reused for a female-partner-seeking-a-male-lover direction.
            bool orientationCompatible = profile.Romantic.IsCompatibleWith(h.IsFemale);

            bool ok = OpenPartnerLoverPolicy.IsPlausibleLover(new OpenPartnerLoverPolicy.LoverCandidateFacts {
               IsAlive = h.IsAlive,
               IsHero = h.IsLord, // a proper lord/lady, not a wanderer or notable
               OrientationCompatible = orientationCompatible,
               IsUnwed = h.Spouse == null,
               IsAdult = !h.IsChild && h.Age >= MarriageEligibility.MinMarriageAge,
               Acquainted = acquainted,
               IsThePlayer = h == player,
               IsOfPlayerHousehold = playerClan != null && h.Clan == playerClan
            });

            if (!ok) continue;

            if (rel > bestRel || (rel == bestRel && MBRandom.RandomInt(2) == 0))
            {
               bestRel = rel;
               best = h;
            }
         }

         return best;
      }
   }
}
