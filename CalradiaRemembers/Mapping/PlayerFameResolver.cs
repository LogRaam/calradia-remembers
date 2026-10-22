// Code written by Gabriel Mailhot, 22/10/2026.
// Gabriel (21/10/2026), after reading another mod's notoriety per culture: "2 et 3 m'interesse". This gathers what the
// pure PlayerFamePolicy reads (the player's renown, crown and traits; the speaker's station, realm and culture; the
// player's deeds done in that realm; whether a deed of his already reached them) and returns the one line that tells
// a character how far the player's name has reached them. Someone who has met the player (any shared event, regard,
// relation), the player's own clan and household get no line: they know him, not his name.

#region

using System;
using System.Linq;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Builds the "what you know of their name" line for a character who does not know the player in person, or null.</summary>
   public static class PlayerFameResolver
   {
      /// <summary>The line for this speaker, or null when none is due.</summary>
      public static string Resolve(Hero npc, NpcProfile profile, IGameStateBridge bridge)
      {
         try
         {
            PlayerFameFacts facts = Facts(npc, profile, bridge);

            return facts == null ? null : PlayerFamePolicy.Line(PlayerFamePolicy.Read(facts), Hero.MainHero.Name?.ToString());
         }
         catch (Exception ex)
         {
            Foundation.ModLog.Error($"[FAME] reading the player's name for {npc?.Name} failed: {ex.Message}");

            return null;
         }
      }

      /// <summary>What the policy reads for this speaker; null when they know the player in person (no line).</summary>
      internal static PlayerFameFacts Facts(Hero npc, NpcProfile profile, IGameStateBridge bridge)
      {
         Hero player = Hero.MainHero;
         if (npc == null || player == null || npc == player) return null;
         if (npc.Clan != null && npc.Clan == Clan.PlayerClan) return null;
         if (EncounterContextBuilder.IsPlayerHousehold(npc, profile) || FirstImpressionResolver.IsAlreadyKnown(npc, profile, bridge)) return null;
         if (profile?.Events?.Count > 0) return null; // they have met: the line would say they never did

         IFaction realm = npc.MapFaction;
         Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
         var today = (int) CampaignTime.Now.ToDays;
         WorldEventStore store = WorldEventStore.Instance;

         return new PlayerFameFacts {
            PlayerRenown = Clan.PlayerClan?.Renown ?? 0f,
            PlayerRulesARealm = playerKingdom != null && playerKingdom.Leader == player,
            Observer = npc.IsLord ? FameObserver.Noble : npc.IsNotable ? FameObserver.Notable : FameObserver.Commoner,
            SameRealm = realm != null && realm.IsKingdomFaction && realm == player.MapFaction,
            SameCulture = npc.Culture != null && npc.Culture == player.Culture,
            DeedsInObserversRealm = realm == null || store == null ? 0 : store.All().Count(e => e.InvolvesPlayer && InRealm(e.PlaceId, realm)),
            HeardADeed = store?.GetKnownEventsRated(npc, today, 8)?.Any(r => r.Event.InvolvesPlayer) == true,
            PlayerMercy = player.GetTraitLevel(DefaultTraits.Mercy),
            PlayerHonor = player.GetTraitLevel(DefaultTraits.Honor)
         };
      }

      private static bool InRealm(string placeId, IFaction realm)
         => !string.IsNullOrEmpty(placeId) && Settlement.Find(placeId)?.MapFaction == realm;
   }
}
