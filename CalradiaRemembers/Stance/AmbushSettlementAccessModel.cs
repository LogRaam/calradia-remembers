// Code written by Gabriel Mailhot, 28/06/2026.
// "Stance with teeth", increment 3.2 — the re-entry gate. After the player LOSES a town ambush and is auto-walked
// out, the capture takes a moment to spring on the open map; without this, the player could re-click the town and
// slip back inside, undoing the capture. This PASSIVE model bars town entry only while a capture is PENDING (NOT
// while an ambush is merely armed — the player must still be able to enter a town to trigger the fight). It only
// answers "can the player enter?", never touching the encounter or map, so it cannot crash the native pathfinding.

#region

using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Bars town entry while a lost-ambush capture is pending, so the player cannot slip the capture.</summary>
   public sealed class AmbushSettlementAccessModel : DefaultSettlementAccessModel
   {
      public override void CanMainHeroEnterSettlement(Settlement settlement, out AccessDetails accessDetails)
      {
         base.CanMainHeroEnterSettlement(settlement, out accessDetails);

         if (settlement != null && AmbushCaptivityBehavior.Instance?.IsCapturePending == true)
            accessDetails.AccessLevel = AccessLevel.NoAccess;
      }
   }
}
