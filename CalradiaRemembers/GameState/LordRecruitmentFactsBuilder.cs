// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Reads the live game state into a pure <see cref="LordRecruitmentFacts" /> for
   ///   <see cref="LordRecruitment" />. Engine-coupled on purpose; the decision itself stays pure.
   /// </summary>
   public static class LordRecruitmentFactsBuilder
   {
      /// <summary>
      ///   Gathers the recruitment facts for a lord, or null when the hero could never be a
      ///   candidate at all (not a real, free lord of another clan) — the caller then skips.
      /// </summary>
      public static LordRecruitmentFacts Build(Hero lord)
      {
         if (lord == null || Hero.MainHero == null) return null;
         if (!lord.IsLord || !lord.IsAlive || lord.IsPrisoner) return null;
         if (lord == Hero.MainHero) return null;
         if (lord.Clan == Clan.PlayerClan) return null; // already ours

         Clan clan = lord.Clan;
         Kingdom kingdom = clan?.Kingdom;
         Hero liege = kingdom?.Leader;
         bool hasLiege = liege != null && liege != lord;

         return new LordRecruitmentFacts {
            // THE MOD'S OWN LEDGER, per the two-ledger rule and Gabriel's ruling of 21/09/2026. This read was
            // vanilla `GetRelation` while its two sibling policies (NotableRecruitmentPolicy, PrisonerRecruitmentPolicy)
            // gated on `ReputationWithPlayer` at the same "deep bond" mark of 60, and the notable policy's own doc
            // claimed all three matched. They did not: a lord you had spent a campaign befriending IN CHAT could sit
            // at 80 of the regard this mod builds and still be refused, because vanilla had never heard of any of it.
            // Found by reading docs/maps/constants.md (21/09/2026). Vanilla relation stays the FALLBACK for a lord
            // with no profile yet, the same shape CourtActionResolver already uses for arranged-match consent.
            Relation = NpcMemoryCampaignBehavior.Instance?.Store?.Get(lord.StringId)?.ReputationWithPlayer
                       ?? lord.GetRelation(Hero.MainHero),
            PlayerClanTier = Mapping.StatureReader.Player.ClanTier,
            LordClanTier = Mapping.StatureReader.OfClan(clan).ClanTier,
            LordIsLandless = clan?.Settlements == null || !clan.Settlements.Any(s => s.IsTown || s.IsCastle),
            LordClanWeakOrDestroyed = clan == null || clan.IsEliminated || clan.Heroes.Count(h => h is {IsAlive: true, IsLord: true}) <= 1,
            HasLiege = hasLiege,
            RelationWithLiege = hasLiege
               ? lord.GetRelation(liege)
               : 0,
            IsClanLeader = clan != null && clan.Leader == lord
         };
      }
   }
}