// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 2: reads how EXPOSED a victim actually is, from the live scene rather than from anything the
// model claims. Gabriel's ruling is that violence is possible everywhere and that company only makes it harder, so
// this never bars an attempt: it only reports who else is at hand, and ViolencePolicy turns that into odds.
//
// The reporter's own case is the yardstick: a village notable alone in his hut, no guards, the player having got
// the drop on him. That must read as Alone. The same man in a town's streets, or a lord with his soldiers at his
// back, must not.

#region

using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Logic.Violence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>Reads the live scene for the facts an act of violence is judged on.</summary>
   public static class ViolenceSceneReader
   {
      /// <summary>
      ///   Who is at hand to intervene. Soldiers at the victim's back, or the garrison of a town or castle, make
      ///   them GUARDED; witnesses in the conversation make them merely watched; otherwise they are alone with the
      ///   player. A village has no garrison, which is exactly why the reporter's notable was reachable.
      /// </summary>
      public static ViolenceExposure ExposureOf(Hero target)
      {
         if (target == null) return ViolenceExposure.Guarded; // unknown company is treated as the hardest case

         if (LeadsArmedFollowers(target)) return ViolenceExposure.Guarded;

         Settlement where = target.CurrentSettlement;
         if (where != null && (where.IsTown || where.IsCastle)) return ViolenceExposure.Guarded;

         return HasOnlookers() ? ViolenceExposure.Onlookers : ViolenceExposure.Alone;
      }

      /// <summary>The facts of a violent attempt: both fighters measured the same way the battle pillar measures them, plus who is watching.</summary>
      public static ViolenceFacts FactsFor(Hero target)
      {
         CombatantFacts player = Battles.BattleFeatFacts.For(Hero.MainHero);
         CombatantFacts foe = Battles.BattleFeatFacts.For(target);

         return new ViolenceFacts(
            CombatProwessPolicy.Prowess(player),
            CombatProwessPolicy.Prowess(foe),
            ExposureOf(target),
            Hero.MainHero?.GetSkillValue(TaleWorlds.Core.DefaultSkills.Roguery) ?? 0);
      }

      // Soldiers of their own at their back: a lord leading a party is never alone, whatever the room looks like.
      private static bool LeadsArmedFollowers(Hero target)
         => target.PartyBelongedTo != null
            && target.PartyBelongedTo != TaleWorlds.CampaignSystem.Party.MobileParty.MainParty
            && target.PartyBelongedTo.MemberRoster != null
            && target.PartyBelongedTo.MemberRoster.TotalManCount > 1;

      // The conversation's own witnesses: people the mod already knows are standing there.
      private static bool HasOnlookers()
      {
         try { return ChatManager.ActiveViewModel?.HasWitnesses == true; }
         catch { return false; }
      }
   }
}
