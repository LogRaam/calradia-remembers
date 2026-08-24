// Code written by Gabriel Mailhot, 23/08/2026.
// The council bar item: a real MapNavigationItemVM (so it lengthens the bar and clicks/hovers like a native
// button, its click routing through CouncilNavigationElement.OpenView to convene). Its surface is pure brush
// work: the CRMapBar.xml override adds the "cr_council" layer (named after the ItemId) to the native
// MapBar.Left.Button.Backgrounds brush, and CouncilBarIconSprite fills that layer's sprite from
// cr_council_button.png at runtime, so the council picture covers the whole button.

#region

using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>A genuine nav-bar item that convenes a council.</summary>
   internal sealed class CouncilNavigationItemVM : MapNavigationItemVM
   {
      public CouncilNavigationItemVM() : base(new CouncilNavigationElement()) { }
   }
}
