// Code written by Gabriel Mailhot, 28/06/2026.
// "Stance with teeth", increment 3.2 — the re-entry gate. After the player LOSES a town ambush and is auto-walked
// out, the capture takes a moment to spring on the open map; without this, the player could re-click the town and
// slip back inside, undoing the capture. This bars town entry only while a capture is PENDING.
//
// BUG FIX 2026-06-29: this is now a DECORATOR, not a bare DefaultSettlementAccessModel. Adding a plain
// DefaultSettlementAccessModel REPLACED whatever model was already in effect — and for a player running War Sails
// (which registers its own model so "port" locations are reachable), CR loading last downgraded every port to the
// vanilla default, whose CanMainHeroAccessLocation hard-codes "port" => "Door is locked!". So we now WRAP the
// model that was already registered and forward EVERY decision to it (preserving other mods' location rules),
// adding only the capture-pending bar on settlement entry. When nothing was registered before us, we fall back to
// the vanilla default behaviour via base.

#region

using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>
   ///   Decorates the settlement-access model already in effect: forwards every decision to it (so other mods'
   ///   location rules, e.g. War Sails' ports, are preserved) and only bars town entry while a lost-ambush
   ///   capture is pending, so the player cannot slip the capture by re-clicking the town.
   /// </summary>
   public sealed class AmbushSettlementAccessModel : DefaultSettlementAccessModel
   {
      private readonly SettlementAccessModel _inner; // the model we replaced; null => fall back to the vanilla default

      public AmbushSettlementAccessModel(SettlementAccessModel inner)
      {
         // Never wrap ourselves (would recurse); only a genuinely different prior model is kept.
         _inner = inner is AmbushSettlementAccessModel ? null : inner;
      }

      public override void CanMainHeroEnterSettlement(Settlement settlement, out AccessDetails accessDetails)
      {
         if (_inner != null) _inner.CanMainHeroEnterSettlement(settlement, out accessDetails);
         else base.CanMainHeroEnterSettlement(settlement, out accessDetails);

         if (settlement != null && AmbushCaptivityBehavior.Instance?.IsCapturePending == true)
            accessDetails.AccessLevel = AccessLevel.NoAccess;
      }

      public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
      {
         if (_inner != null) _inner.CanMainHeroEnterLordsHall(settlement, out accessDetails);
         else base.CanMainHeroEnterLordsHall(settlement, out accessDetails);
      }

      public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails)
      {
         if (_inner != null) _inner.CanMainHeroEnterDungeon(settlement, out accessDetails);
         else base.CanMainHeroEnterDungeon(settlement, out accessDetails);
      }

      public override bool CanMainHeroAccessLocation(Settlement settlement, string locationId, out bool disableOption, out TextObject disabledText)
         => _inner != null
            ? _inner.CanMainHeroAccessLocation(settlement, locationId, out disableOption, out disabledText)
            : base.CanMainHeroAccessLocation(settlement, locationId, out disableOption, out disabledText);

      public override bool CanMainHeroDoSettlementAction(Settlement settlement, SettlementAction settlementAction, out bool disableOption, out TextObject disabledText)
         => _inner != null
            ? _inner.CanMainHeroDoSettlementAction(settlement, settlementAction, out disableOption, out disabledText)
            : base.CanMainHeroDoSettlementAction(settlement, settlementAction, out disableOption, out disabledText);

      public override bool IsRequestMeetingOptionAvailable(Settlement settlement, out bool disableOption, out TextObject disabledText)
         => _inner != null
            ? _inner.IsRequestMeetingOptionAvailable(settlement, out disableOption, out disabledText)
            : base.IsRequestMeetingOptionAvailable(settlement, out disableOption, out disabledText);
   }
}
