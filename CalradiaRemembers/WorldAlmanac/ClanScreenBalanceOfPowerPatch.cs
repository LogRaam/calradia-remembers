// Code written by Gabriel Mailhot, 04/09/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Inserts the "Balance of Power" button into the Clan screen, appended into the Finance panel and lifted just
   ///   above it (the same append-into-a-Widget-by-Id shape as the proven encyclopedia Letters button). Content lives
   ///   in GUI/Prefabs/CR_ClanBalanceOfPowerButton.xml and binds to <c>@BalanceOfPowerButtonText</c> /
   ///   <c>@ExecuteOpenBalanceOfPower</c> from <see cref="ClanScreenBalanceOfPowerMixin" />.
   /// </summary>
   [PrefabExtension("ClanScreen", "descendant::Widget[@Id='FinancePanelWidget']")]
   internal sealed class ClanScreenBalanceOfPowerPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_ClanBalanceOfPowerButton";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}
