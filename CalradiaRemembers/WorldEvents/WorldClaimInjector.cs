// Code written by Gabriel Mailhot, 12/09/2026.
// Invented facts, increment 3: the moment a thing a character made up becomes a thing that is true.
//
// Gabriel, 2026-09-12, watching a governor invent a blight in his own fields: "si un NPC invente un FAIT...
// et que le joueur en parle à un autre NPC, ce dernier ne connaîtra pas ce fait. Ça peut créer de
// l'incohérence." And his design: "lorsqu'un nouveau fait est déclaré par un NPC et jugé crédible, alors il
// peut être injecté comme fait divers dans le World Events ou le pipeline de rumeurs."
//
// This file is deliberately thin. Every judgement was made in pure code that can be argued with on its own
// (WorldClaimPolicy); what is left here is looking up a place, asking the store whether the world already
// carries it, and writing the row. The one thing it adds is ATTRIBUTION: the speaker rides along in ActorIds,
// so a later listener can say "Arion told the player that" rather than knowing it from nowhere.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using NpcMemoryService.Core.Knowledge;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>
   ///   Adopts the claims an NPC made in conversation into the world, as low-weight rumours that travel the
   ///   same road every other piece of news travels.
   /// </summary>
   public static class WorldClaimInjector
   {
      /// <summary>
      ///   Tags every row this feature writes, so they can be found, counted and removed as a group. A
      ///   language model writing to a saved world is a thing that must stay identifiable.
      /// </summary>
      public const string Source = "CalradiaRemembers.NpcClaim";

      /// <summary>
      ///   Records what may be recorded and reports what happened, verdict by verdict, so a refusal is
      ///   visible in a log rather than a silence.
      /// </summary>
      /// <returns>How many claims entered the world.</returns>
      public static int Adopt(IReadOnlyList<WorldClaim> claims, Hero speaker, int today)
      {
         if (claims == null || claims.Count == 0 || WorldEventStore.Instance == null) return 0;

         var adopted = 0;

         foreach (WorldClaim claim in claims)
         {
            try
            {
               // The player never authors the world (Gabriel's ruling), and the check is by IDENTITY rather
               // than by comparing an id string, because this is the one rule that must never be lost to a
               // formatting difference.
               bool fromThePlayer = speaker == null || speaker == Hero.MainHero;

               ClaimVerdict verdict = WorldClaimPolicy.Judge(claim, fromThePlayer, AlreadyKnown(claim, today));

               if (verdict != ClaimVerdict.Accepted)
               {
                  ModLog.Info($"[CLAIM] refused ({verdict}): {Shorten(claim.Text)}");

                  continue;
               }

               WorldEventStore.Instance.Record(Build(claim, speaker, today));
               adopted++;

               ModLog.Info($"[CLAIM] adopted from {speaker.Name} ({claim.Subject}"
                           + $"{(claim.PlaceName == null ? "" : ", " + claim.PlaceName)}): {Shorten(claim.Text)}");
            }
            catch (Exception ex)
            {
               // A rumour is never worth a crash on a background path.
               ModLog.Error($"[CLAIM] adoption failed: {ex.GetType().Name}: {ex.Message}");
            }
         }

         return adopted;
      }

      #region private

      private static WorldEvent Build(WorldClaim claim, Hero speaker, int today)
      {
         Settlement place = PlaceNamed(claim.PlaceName);

         return new WorldEvent {
            Id = Guid.NewGuid().ToString("N"),
            Type = WorldEventType.HeardTell,
            Day = today,
            Summary = claim.Text.Trim(),

            // Attribution is the point. A second character who has heard this can name who it came from,
            // which is a better scene than knowing it from nowhere - and it leaves the claim deniable.
            ActorIds = speaker?.StringId == null ? new List<string>() : new List<string> {speaker.StringId},
            PlaceId = place?.StringId,

            // Never the player's chronicle: the player's own deeds have their own road into the world, and two
            // authors on one record is how two records disagree.
            InvolvesPlayer = false,
            IsPersistentDeed = false,

            // Capped low by policy so an invented rumour never outranks a battle or a siege in what a
            // character brings up.
            Magnitude = WorldClaimPolicy.ReachOf(claim),
            SourceMod = Source
         };
      }

      /// <summary>
      ///   Whether the world already carries this. Coarse on purpose: a false match loses a duplicate, a false
      ///   miss accumulates for ever, and only one of those can be undone.
      /// </summary>
      private static bool AlreadyKnown(WorldClaim claim, int today)
      {
         string key = WorldClaimPolicy.DedupKey(claim);

         return WorldEventStore.Instance.MostRecentMatching(
            WorldEventType.HeardTell,
            e => e != null
                 && WorldClaimPolicy.DedupKey(new WorldClaim {
                    Subject = claim.Subject,
                    PlaceName = NameOfPlace(e.PlaceId) ?? claim.PlaceName,
                    Text = e.Summary
                 }) == key) != null;
      }

      private static Settlement PlaceNamed(string name)
      {
         if (string.IsNullOrWhiteSpace(name) || Settlement.All == null) return null;

         string wanted = name.Trim();

         foreach (Settlement candidate in Settlement.All)
            if (string.Equals(candidate?.Name?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
               return candidate;

         // A place the model named that Calradia does not have is not a reason to drop the news: plenty of
         // real talk is about somewhere nobody can point to. It simply has no map pin.
         return null;
      }

      private static string NameOfPlace(string settlementId)
         => string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId)?.Name?.ToString();

      private static string Shorten(string text)
      {
         string trimmed = (text ?? "").Trim();

         return trimmed.Length <= 90 ? trimmed : trimmed.Substring(0, 87) + "...";
      }

      #endregion
   }
}
