// Code written by Gabriel Mailhot, 07/08/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Inserts the per-contact "Letters" button into the hero encyclopedia page's info block. Content lives
   ///   in GUI/Prefabs/CR_HeroLettersButton.xml and binds to <c>@HasLetters</c> / <c>@ExecuteOpenLetters</c>
   ///   from <see cref="EncyclopediaHeroPageLettersMixin" />.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageLettersPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroLettersButton";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}
