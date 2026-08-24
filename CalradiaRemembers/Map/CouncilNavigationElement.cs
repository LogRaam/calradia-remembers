// Code written by Gabriel Mailhot, 23/08/2026.
// The navigation "element" behind the council button in the map's bottom bar. A real MapNavigationItemVM is built
// from an INavigationElement, and its click calls element.OpenView(); implementing that interface is what lets a
// GENUINE bar item convene the council (element.OpenView -> CouncilEntry), which in turn is what makes the bar
// LENGTHEN to hold the button and push its end decoration along, instead of an absolute overlay landing under it.
// Permission drives the item's enabled state, so the button greys out (with a reason) when a council cannot open.

#region

using CalradiaRemembers.Chat;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>A bar navigation element whose "open" convenes a council; its permission mirrors <see cref="CouncilEntry.CanConveneNow" />.</summary>
   internal sealed class CouncilNavigationElement : INavigationElement
   {
      public string StringId => "cr_council";

      // Enabled when a council can be convened right now; otherwise greyed, with the reason as its tooltip.
      public NavigationPermissionItem Permission =>
         new NavigationPermissionItem(CouncilEntry.CanConveneNow(), new TextObject("You cannot convene a council here."));

      public bool IsLockingNavigation => false;
      public bool IsActive => false;
      public TextObject Tooltip => new TextObject("Hold a council");
      public bool HasAlert => false;
      public TextObject AlertTooltip => new TextObject(string.Empty);

      public void OpenView() => CouncilEntry.PromptAndConvene();

      public void OpenView(params object[] parameters) => CouncilEntry.PromptAndConvene();

      public void GoToLink() { }
   }
}
