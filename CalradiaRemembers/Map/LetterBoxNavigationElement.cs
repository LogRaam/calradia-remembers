// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box's button in the map's bottom bar (Gabriel, 30/10/2026: "un icone de lettre dans le UI MAP. Et qu'un
// chiffre apparait pour montrer le nombre de lettres accumulees pretes a lire"). Built like the council's: a genuine
// INavigationElement, so the bar lengthens for it and its click opens the box. HasAlert drives the bar's own badge,
// which shows the count (LetterBoxNavigationItemVM writes it); its tooltip says when an urgent letter waits.

#region

using CalradiaRemembers.Letters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>A bar navigation element whose "open" opens the letter box; its alert is "a letter waits".</summary>
   internal sealed class LetterBoxNavigationElement : INavigationElement
   {
      public string StringId => LetterBoxNavigationItemVM.ItemIdValue;

      public NavigationPermissionItem Permission => new NavigationPermissionItem(true, new TextObject(string.Empty));

      public bool IsLockingNavigation => false;
      public bool IsActive => LetterBoxWindow.IsOpen;
      public TextObject Tooltip => new TextObject("Letter box");
      public bool HasAlert => LetterBox.WaitingCountCached() > 0;

      public TextObject AlertTooltip
      {
         get
         {
            int n = LetterBox.WaitingCountCached();
            string urgent = LetterBox.UrgentWaitsCached() ? " One looks urgent." : "";

            return new TextObject(n == 1 ? "A letter waits." + urgent : $"{n} letters wait.{urgent}");
         }
      }

      public void OpenView() => LetterBoxWindow.Open();

      public void OpenView(params object[] parameters) => LetterBoxWindow.Open();

      public void GoToLink() { }
   }
}
