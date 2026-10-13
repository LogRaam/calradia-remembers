// Code written by Gabriel Mailhot, 13/10/2026.
// The "Who is this?" button on an audience invitation (Lordfadooboo, Nexus, 13/10/2026: "an ability to open their
// encyclopedia page from that screen would be helpful"). The game's popup has room for two buttons only (512 wide, two
// 251-wide buttons), so this one sits on its own line above them. It is added to the game's single popup, so it is
// hidden by default and shown only for an invitation raised through AudienceInquiry: the popup patch below tells
// AudienceInquiry which data the popup shows, and AudienceInquiry decides.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;

#endregion

namespace CalradiaRemembers.Interception
{
   /// <summary>Inserts the button (GUI/Prefabs/CR_AudienceWhoButton.xml) above the popup's two buttons.</summary>
   [PrefabExtension("SingleQueryPopup", "descendant::ListPanel[@Id='ControlButtons']")]
   internal sealed class AudienceWhoButtonPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_AudienceWhoButton";

      public override InsertType Type => InsertType.Prepend;
   }

   /// <summary>The button's data on the game's popup: hidden unless the popup shows one of CR's invitations.</summary>
   [ViewModelMixin]
   public sealed class AudienceWhoButtonMixin : BaseViewModelMixin<SingleQueryPopUpVM>
   {
      private bool _isCrWhoShown;
      private string _crWhoText = "";

      /// <summary>The popup's mixin (the game keeps one popup and reuses it).</summary>
      internal static AudienceWhoButtonMixin Current { get; private set; }

      public AudienceWhoButtonMixin(SingleQueryPopUpVM vm) : base(vm)
      {
         Current = this;
      }

      [DataSourceProperty]
      public bool IsCrWhoShown
      {
         get => _isCrWhoShown;
         set
         {
            if (_isCrWhoShown == value) return;
            _isCrWhoShown = value;
            ViewModel?.OnPropertyChanged();
         }
      }

      // Empty while hidden: the label hides its button when empty, so a mixin that failed to bind (no text at all)
      // leaves no stray button on the game's other popups.
      [DataSourceProperty]
      public string CrWhoText
      {
         get => _crWhoText;
         set
         {
            if (_crWhoText == value) return;
            _crWhoText = value;
            ViewModel?.OnPropertyChanged();
         }
      }

      [DataSourceMethod]
      public void ExecuteCrWho() => AudienceInquiry.OnWhoClicked();

      /// <summary>Shows or hides the button for the popup now shown.</summary>
      internal void Refresh(bool shown)
      {
         CrWhoText = shown ? "Who is this?" : "";
         IsCrWhoShown = shown;
      }
   }

   /// <summary>Tells AudienceInquiry which invitation the popup shows.</summary>
   [HarmonyPatch(typeof(SingleQueryPopUpVM), nameof(SingleQueryPopUpVM.SetData))]
   internal static class AudienceWhoSetDataPatch
   {
      public static void Postfix(InquiryData data) => AudienceInquiry.OnPopupShown(data);
   }

   /// <summary>The popup closed: no invitation shown.</summary>
   [HarmonyPatch(typeof(SingleQueryPopUpVM), nameof(SingleQueryPopUpVM.OnClearData))]
   internal static class AudienceWhoClearPatch
   {
      public static void Postfix() => AudienceInquiry.OnPopupShown(null);
   }
}
