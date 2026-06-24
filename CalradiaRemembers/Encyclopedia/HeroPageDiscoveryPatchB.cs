// Code written by Gabriel Mailhot, 01/06/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Appends the "Discoveries:" header line to the hero encyclopedia page.
   ///   UIExtenderEx applies this patch BEFORE <see cref="HeroPageDiscoveryPatchA" />,
   ///   so the label appears above the bullet list in InfoContainer's vertical stack.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageDiscoveryPatchB : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroDiscoveryLabel";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}