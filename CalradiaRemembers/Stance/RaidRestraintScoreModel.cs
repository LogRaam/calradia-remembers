// Code written by Gabriel Mailhot, 01/11/2026.
// The game's own say in where a lord raids. Every lord scores every target through this model, and the base game already lowers a
// village by the relation between the two clans, yet at 100 a fifth of the score remains, and a village next door still wins:
// DepaHN (27/10/2026), "a close friend was the first to burn my villages". This takes the player's villages off a lord's list
// while he spares them; he then goes for the next best target. It wraps whatever model was in effect, as the settlement access
// model does, so another mod's scoring survives. An army takes its target from its leader alone: a friend riding in another
// lord's army follows him. Sieges are left alone (Gabriel, 01/11/2026).

#region

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Forwards every score to the model in effect, and scores nothing for a village its would-be raider spares.</summary>
   public sealed class RaidRestraintScoreModel : TargetScoreCalculatingModel
   {
      public override float TravelingToAssignmentFactor => BaseModel.TravelingToAssignmentFactor;

      public override float BesiegingFactor => BaseModel.BesiegingFactor;

      public override float AssaultingTownFactor => BaseModel.AssaultingTownFactor;

      public override float RaidingFactor => BaseModel.RaidingFactor;

      public override float DefendingFactor => BaseModel.DefendingFactor;

      public override float GetDefensivePatrollingFactor(bool isNavalPatrolling) => BaseModel.GetDefensivePatrollingFactor(isNavalPatrolling);

      public override float GetOffensivePatrollingFactor(bool isNavalPatrolling) => BaseModel.GetOffensivePatrollingFactor(isNavalPatrolling);

      public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
         => BaseModel.CalculateDefensivePatrollingScoreForSettlement(settlement, isTargetingPort, mobileParty);

      public override float CalculateOffensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
         => BaseModel.CalculateOffensivePatrollingScoreForSettlement(settlement, isTargetingPort, mobileParty);

      public override float GetTargetScoreForFaction(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
      {
         if (missionType == Army.ArmyTypes.Raider && RaidRestraintBehavior.Spares(mobileParty?.LeaderHero, targetSettlement)) return 0f;

         return BaseModel.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);
      }

      // A lord who set out before he drew turns back: the base model scores his objective itself, without coming through here.
      public override float CurrentObjectiveValue(MobileParty mobileParty)
      {
         if (mobileParty?.DefaultBehavior == AiBehavior.RaidSettlement && RaidRestraintBehavior.Spares(mobileParty.LeaderHero, mobileParty.TargetSettlement)) return 0f;

         return BaseModel.CurrentObjectiveValue(mobileParty);
      }
   }
}
