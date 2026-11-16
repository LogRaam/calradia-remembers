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
      /// <summary>
      ///   The line for this speaker, or null when none is due. <paramref name="memoriesAtOpening" /> is how many memories
      ///   they held when this conversation opened: what the first exchange records must not make them forget, from the
      ///   second turn on, that they had never met the player.
      /// </summary>
      public static string Resolve(Hero npc, NpcProfile profile, IGameStateBridge bridge, int? memoriesAtOpening = null)
      {
         try
         {
            PlayerFameFacts facts = Facts(npc, profile, bridge, memoriesAtOpening);

            return facts == null ? null : PlayerFamePolicy.Line(PlayerFamePolicy.Read(facts), Hero.MainHero.Name?.ToString());
         }
         catch (Exception ex)
         {
            Foundation.ModLog.Error($"[FAME] reading the player's name for {npc?.Name} failed: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Whether the player's name is news to this speaker (PlayerFamePolicy.NameIsNews): a stranger who never heard it,
      ///   or heard it only in passing. False for anyone who knows the player in person.
      /// </summary>
      public static bool NameIsNewsTo(Hero npc, NpcProfile profile, IGameStateBridge bridge, int? memoriesAtOpening = null)
      {
         try
         {
            PlayerFameFacts facts = Facts(npc, profile, bridge, memoriesAtOpening);

            return facts != null && PlayerFamePolicy.NameIsNews(PlayerFamePolicy.Read(facts));
         }
         catch (Exception ex)
         {
            Foundation.ModLog.Error($"[FAME] reading whether the player's name is news to {npc?.Name} failed: {ex.Message}");

            return false;
         }
      }

      /// <summary>What the policy reads for this speaker; null when they know the player in person (no line).</summary>
      internal static PlayerFameFacts Facts(Hero npc, NpcProfile profile, IGameStateBridge bridge, int? memoriesAtOpening = null)
      {
         Hero player = Hero.MainHero;
         if (npc == null || player == null || npc == player) return null;
         if (npc.Clan != null && npc.Clan == Clan.PlayerClan) return null;
         if (EncounterContextBuilder.IsPlayerHousehold(npc, profile)) return null;
         // They had met before this conversation (the line would say they never did), or a bond already stands.
         if ((memoriesAtOpening ?? profile?.Events?.Count ?? 0) > 0) return null;
         if (FirstImpressionResolver.HasStandingBond(npc, profile, bridge)) return null;

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
            LivesUnderThePlayer = npc.HomeSettlement?.OwnerClan == Clan.PlayerClan || (playerKingdom != null && playerKingdom.Leader == player && realm == playerKingdom),
            PlayerMercy = player.GetTraitLevel(DefaultTraits.Mercy),
            PlayerHonor = player.GetTraitLevel(DefaultTraits.Honor)
         };
      }

      private static bool InRealm(string placeId, IFaction realm)
         => !string.IsNullOrEmpty(placeId) && Settlement.Find(placeId)?.MapFaction == realm;
   }
}
