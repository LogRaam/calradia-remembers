// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (ProudSeal 2026-07-10): nobles open too neutral and reasonable, a rivalry needs the player to
// offend first. This gathers the engine facts (the NPC's traits, the player's standing, the faction context)
// and asks the pure FirstImpressionPolicy how a barely-known noble opens toward the player, then renders it as
// prompt guidance. It colours the OPENING TONE only, never the relation numbers. A lord in the player's OWN
// clan opens loyal; an already-known lord keeps his existing stance; a spouse, consort, or close kin draws no
// first impression at all (an established bond is never "a person you barely know").

#region

using System;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Builds the ready-to-inject "first impression" line for a barely-known noble, or null.</summary>
   public static class FirstImpressionResolver
   {
      // Beyond this relation, or with more than this many recorded events, the NPC is no stranger: their
      // existing stance governs, and no cold first impression is drawn.
      private const int KnownRelationThreshold = 10;
      private const int KnownEventThreshold = 2;

      /// <summary>The first-impression prompt line for this NPC, or null when none applies.</summary>
      public static string Resolve(Hero npc, NpcProfile profile, IGameStateBridge bridge)
      {
         if (npc == null || Hero.MainHero == null) return null;
         if (!npc.IsLord) return null; // the report is about nobles; notables/wanderers are out of scope for now
         if (npc == Hero.MainHero) return null;

         // A spouse, consort, or close kin is never "a person you barely know" (field report, in-progress-save
         // spouse, 2026-08-27): without this exemption an own-clan spouse still drew the clan-Loyal line under a
         // header that frames her as a stranger, contradicting the marital bond the rest of the prompt renders.
         if (EncounterContextBuilder.IsPlayerHousehold(npc, profile)) return null;

         var inputs = new FirstImpressionInputs {
            Mercy = npc.GetTraitLevel(DefaultTraits.Mercy),
            Honor = npc.GetTraitLevel(DefaultTraits.Honor),
            Calculating = npc.GetTraitLevel(DefaultTraits.Calculating),
            Generosity = npc.GetTraitLevel(DefaultTraits.Generosity),
            Valor = npc.GetTraitLevel(DefaultTraits.Valor),
            PlayerClanTier = StatureReader.Player.ClanTier,
            PlayerRenownBand = RenownBand(Clan.PlayerClan?.Renown ?? 0f),
            PlayerFiefCount = Clan.PlayerClan?.Fiefs?.Count ?? 0,
            SameFactionRival = IsSameFactionRival(npc),
            IsOwnClanMember = npc.Clan != null && npc.Clan == Clan.PlayerClan,
            IsAlreadyKnown = IsAlreadyKnown(npc, profile, bridge),
            Roll = FirstImpressionPolicy.StableRoll(npc.StringId)
         };

         FirstImpressionDisposition disposition = FirstImpressionPolicy.Resolve(inputs);

         return FirstImpressionDescriber.Describe(disposition);
      }

      #region private

      /// <summary>Coarse renown bands: 0 an unknown upstart, 3 famous across Calradia.</summary>
      private static int RenownBand(float renown)
      {
         if (renown < 50f) return 0;
         if (renown < 150f) return 1;
         if (renown < 400f) return 2;

         return 3;
      }

      /// <summary>
      ///   The player's clan is a DIFFERENT clan sworn to the same kingdom as this NPC, so a status-minded or
      ///   ungenerous lord may resent one more house at the table when the spoils are shared.
      /// </summary>
      private static bool IsSameFactionRival(Hero npc)
      {
         IFaction npcFaction = npc.MapFaction;

         return npcFaction != null
                && npcFaction.IsKingdomFaction
                && npcFaction == Hero.MainHero.MapFaction
                && npc.Clan != Clan.PlayerClan;
      }

      /// <summary>
      ///   The NPC is no stranger: a real relationship (friend or enemy) exists, or they have a history of
      ///   recorded dealings with the player. First impressions are only for the barely-known.
      /// </summary>
      /// <summary>A real relationship stands (friend or enemy, by the game's relation or the mod's own regard), events aside.</summary>
      internal static bool HasStandingBond(Hero npc, NpcProfile profile, IGameStateBridge bridge)
         => Math.Abs(bridge?.GetPlayerRelation(npc) ?? 0) >= KnownRelationThreshold || Math.Abs(profile?.ReputationWithPlayer ?? 0) >= KnownRelationThreshold;

      internal static bool IsAlreadyKnown(Hero npc, NpcProfile profile, IGameStateBridge bridge)
      {
         int relation = bridge?.GetPlayerRelation(npc) ?? 0;
         // Memories LIVED with the player only: three rumours heard about the world do not make the player known to them
         // (VonLemberg, source audit, 15/11/2026).
         int events = NpcMemoryService.Core.Models.HistoryWindowPolicy.LivedCount(profile?.Events);
         // M-R4 (regard audit): the mod's OWN personal regard also proves they are no stranger. Without this a run
         // of hostile chats (regard sunk to -30) with no [EVENT] recorded still read as "they have done nothing to
         // wrong you", an opening tone that flatly contradicts the CURRENT STANCE line built from the same score.
         int regard = profile?.ReputationWithPlayer ?? 0;

         return Math.Abs(relation) >= KnownRelationThreshold
                || Math.Abs(regard) >= KnownRelationThreshold
                || events > KnownEventThreshold;
      }

      #endregion
   }
}
