// Code written by Gabriel Mailhot, 04/09/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Inserts the "Balance of Power" button into the Clan screen, as a sibling of the Finance panel. Content lives
   ///   in GUI/Prefabs/CR_ClanBalanceOfPowerButton.xml and binds to <c>@BalanceOfPowerButtonText</c> /
   ///   <c>@ExecuteOpenBalanceOfPower</c> from <see cref="ClanScreenBalanceOfPowerMixin" />.
   /// </summary>
   [PrefabExtension("ClanScreen", "descendant::ClanScreenWidget[@Id='ClanScreenWidget']")]
   internal sealed class ClanScreenBalanceOfPowerPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_ClanBalanceOfPowerButton";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}
