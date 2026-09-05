// Code written by Gabriel Mailhot, 04/09/2026.
// A player entry point for the Balance of Power panel: a button on the Clan screen (Gabriel's chosen spot, above
// the Finance panel). Until now the panel opened only from the dev console (cr.balanceofpower); this gives the
// player a real way in. The button binds to this mixin via CR_ClanBalanceOfPowerButton.xml.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   UIExtenderEx mixin adding a "Balance of Power" button to the Clan screen. Bound by the prefab patch via
   ///   <c>@BalanceOfPowerButtonText</c> / <c>@ExecuteOpenBalanceOfPower</c>; opens the World Almanac dashboard.
   /// </summary>
   [ViewModelMixin]
   public sealed class ClanScreenBalanceOfPowerMixin : BaseViewModelMixin<ClanManagementVM>
   {
      public ClanScreenBalanceOfPowerMixin(ClanManagementVM vm) : base(vm) { }

      [DataSourceProperty]
      public string BalanceOfPowerButtonText => "Balance of Power";

      [DataSourceMethod]
      public void ExecuteOpenBalanceOfPower() => BalanceOfPowerManager.Open();
   }
}
