// Code written by Gabriel Mailhot, 08/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Inserts the appearance panel (composed look + inline editable note) into the hero encyclopedia page's info
   ///   block. Content lives in GUI/Prefabs/CR_HeroAppearanceButton.xml and binds to <c>@IsAppearanceEditable</c> /
   ///   <c>@AppearanceHeader</c> / <c>@AppearanceText</c> / <c>@AppearanceProse</c> / <c>@ExecuteSaveAppearance</c>
   ///   from <see cref="EncyclopediaHeroPageAppearanceMixin" />.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageAppearancePatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroAppearanceButton";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}
