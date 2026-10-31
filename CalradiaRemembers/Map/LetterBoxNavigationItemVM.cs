// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box bar item: a real MapNavigationItemVM whose badge (the bar's "unread" badge, drawn by the native MapBar
// prefab from HasAlert and AlertText) shows how many letters wait. The native item sets AlertText to a fixed mark in
// RefreshValues; this one writes the count there instead, and again whenever the box changes. Its surface is the
// "cr_letterbox" layer of MapBar.Left.Button.Backgrounds (CRMapBar.xml), its sprite filled at runtime by
// LetterBoxBarIconSprite, exactly as the council's.

#region

using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Letters;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>A genuine nav-bar item that opens the letter box, its badge counting the letters waiting.</summary>
   internal sealed class LetterBoxNavigationItemVM : MapNavigationItemVM
   {
      internal const string ItemIdValue = "cr_letterbox";

      public LetterBoxNavigationItemVM() : base(new LetterBoxNavigationElement())
      {
         LetterBox.Changed += WriteCount;
         WriteCount();
      }

      public override void RefreshValues()
      {
         base.RefreshValues();
         WriteCount();
      }

      public override void OnFinalize()
      {
         LetterBox.Changed -= WriteCount;
         base.OnFinalize();
      }

      private void WriteCount() => AlertText = LetterBoxPolicy.BadgeText(LetterBox.WaitingCountCached());
   }
}
