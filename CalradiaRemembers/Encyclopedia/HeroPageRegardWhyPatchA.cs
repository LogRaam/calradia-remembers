// Code written by Gabriel Mailhot, 17/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the friends-and-enemies reasons to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageRegardWhyPatchB" />,
    ///   so items appear below the "Why:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageRegardWhyPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroRegardWhyItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
