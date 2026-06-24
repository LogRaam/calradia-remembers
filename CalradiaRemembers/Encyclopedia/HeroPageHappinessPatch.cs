// Code written by Gabriel Mailhot, 23/06/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Inserts a single line into the hero encyclopedia page, after the vanilla Info block, showing a
    ///   companion's contentment in your service. The content lives in GUI/Prefabs/CR_HeroContentment.xml
    ///   and binds to <c>@CalradiaContentment</c> provided by
    ///   <see cref="EncyclopediaHeroPageHappinessMixin"/>.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageHappinessPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;
        public override int Index => 0;

        [PrefabExtensionInsertPatch.PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroContentment";
    }
}
