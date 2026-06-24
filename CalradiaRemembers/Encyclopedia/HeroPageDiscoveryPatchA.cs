// Code written by Gabriel Mailhot, 01/06/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Appends the discovery bullet list to the hero encyclopedia page.
   ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageDiscoveryPatchB" />,
   ///   so items appear below the "Discoveries:" label in InfoContainer's vertical stack.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageDiscoveryPatchA : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroDiscoveryItems";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}