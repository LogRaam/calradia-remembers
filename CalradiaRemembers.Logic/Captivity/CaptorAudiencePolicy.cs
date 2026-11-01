// Code written by Gabriel Mailhot, 01/11/2026.
// Windmolen (Nexus, 26/10/2026): "My captor always seems to want to play some perverse game with me [...] The NPC's never seem
// to be capable of just doing it! Give in, or just execute you or let you free or ransom you." Gabriel the same day: "il faudra
// d'autres scenarios que de la maltraitance". Below Hardcore a lord's captive saw no scene at all; at Hardcore every scene was a
// mistreatment that ended in the cell. This decides when a lord sends for his prisoner to talk, and what kind of talk it is,
// from who he is. Gabriel, 01/11/2026 ("go"): a first audience around the second day, at most one a week after that.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>Who holds the player, and how things stand between them.</summary>
   public sealed class CaptorAudienceFacts
   {
      /// <summary>The captor's Mercy trait.</summary>
      public int Mercy { get; init; }

      /// <summary>The captor's Honor trait.</summary>
      public int Honor { get; init; }

      /// <summary>The captor's Calculating trait.</summary>
      public int Calculating { get; init; }

      /// <summary>The captor's regard for the player.</summary>
      public int Regard { get; init; }

      /// <summary>The heaviest grudge the captor holds against the player.</summary>
      public int HeaviestGrudge { get; init; }

      /// <summary>The captor is of the player's blood.</summary>
      public bool IsKin { get; init; }

      /// <summary>The tier of the player's clan: a prisoner of rank is a guest of rank.</summary>
      public int PlayerClanTier { get; init; }

      /// <summary>The player has the ransom on them: nobody opens a bargain with an empty purse.</summary>
      public bool CanPayTheRansom { get; init; }
   }

   /// <summary>When a captor sends for the player, and what he wants of the talk.</summary>
   public static class CaptorAudiencePolicy
   {
      /// <summary>The day of captivity on which a lord first sends for his prisoner.</summary>
      public const int FirstAudienceDay = 2;

      /// <summary>The days between two audiences: a prisoner is not a daily visitor.</summary>
      public const int DaysBetweenAudiences = 7;

      /// <summary>The clan tier from which a captor treats the prisoner as a guest of rank.</summary>
      public const int GuestOfRankTier = 2;

      /// <summary>The days an oath of parole binds the player (Claude's default, Gabriel's "go", 01/11/2026).</summary>
      public const int ParoleDays = 60;

      // Below this no lord, however cruel, always keeps to the old scenes; above it the worst still happens at Hardcore.
      private const int MinHardcoreShare = 10;
      private const int MaxHardcoreShare = 80;

      /// <summary>True when an audience is due on this day of captivity.</summary>
      public static bool IsDue(int daysHeld, int? daysSinceLast)
         => daysHeld >= FirstAudienceDay && (!daysSinceLast.HasValue || daysSinceLast.Value >= DaysBetweenAudiences);

      /// <summary>
      ///   At Hardcore the chance out of 100 that a due audience takes the place of the old scenes: an honourable, merciful
      ///   lord talks, a cruel one seldom does. Below Hardcore the audience is the only scene there is.
      /// </summary>
      public static int HardcoreShare(CaptorAudienceFacts f)
      {
         int share = 30 + 15 * ((f?.Mercy ?? 0) + (f?.Honor ?? 0));

         return Math.Max(MinHardcoreShare, Math.Min(MaxHardcoreShare, share));
      }

      /// <summary>
      ///   What this captor wants of the talk, or null when he simply keeps the prisoner and waits. A vendetta opens no door:
      ///   <see cref="CaptorReleasePolicy.VendettaGrudgeFloor" /> refuses every release, so a talk would promise what cannot be.
      /// </summary>
      public static CaptiveSceneIntent? Select(CaptorAudienceFacts f, Func<int, int> rollPercent)
      {
         if (f == null || rollPercent == null || f.HeaviestGrudge >= CaptorReleasePolicy.VendettaGrudgeFloor) return null;

         var weights = new List<(CaptiveSceneIntent? Intent, int Weight)> {
            (null, 25),
            (CaptiveSceneIntent.TornFriend, f.Regard >= Regard.WarmTier || f.IsKin ? 60 : 0),
            (CaptiveSceneIntent.Courtesy, f.PlayerClanTier >= GuestOfRankTier ? Math.Max(0, 10 + 10 * (f.Honor + f.Mercy)) : 0),
            (CaptiveSceneIntent.Bargain, f.CanPayTheRansom ? Math.Max(5, 20 + 15 * f.Calculating) : 0),
            (CaptiveSceneIntent.Parole, f.Honor >= 1 ? 20 + 10 * f.Honor : 0)
         };

         int total = 0;
         foreach ((CaptiveSceneIntent? _, int weight) in weights) total += weight;

         int roll = rollPercent(total);
         foreach ((CaptiveSceneIntent? intent, int weight) in weights)
         {
            if (roll < weight) return intent;
            roll -= weight;
         }

         return null;
      }

      /// <summary>True when an oath of parole is on offer in this kind of talk.</summary>
      public static bool OffersParole(CaptiveSceneIntent intent)
         => intent is CaptiveSceneIntent.Parole or CaptiveSceneIntent.Courtesy or CaptiveSceneIntent.TornFriend;

      /// <summary>
      ///   True when the player breaks their oath: they ATTACKED the realm they swore not to fight, within the term. Attacked by
      ///   it, they only defended themselves. Claude's default, Gabriel's "go", 01/11/2026.
      /// </summary>
      public static bool BreaksParole(ParoleFightFacts f)
         => f != null && f.PlayerAttacked && f.Today <= f.UntilDay && !string.IsNullOrEmpty(f.SwornFactionId) && f.SwornFactionId == f.EnemyFactionId;
   }

   /// <summary>A fight the player took part in, against an oath of parole.</summary>
   public sealed class ParoleFightFacts
   {
      /// <summary>The player's side attacked.</summary>
      public bool PlayerAttacked { get; init; }

      /// <summary>The faction fought.</summary>
      public string EnemyFactionId { get; init; }

      /// <summary>The faction the player swore not to fight.</summary>
      public string SwornFactionId { get; init; }

      /// <summary>Today, in campaign days.</summary>
      public int Today { get; init; }

      /// <summary>The last day of the oath.</summary>
      public int UntilDay { get; init; }
   }
}
