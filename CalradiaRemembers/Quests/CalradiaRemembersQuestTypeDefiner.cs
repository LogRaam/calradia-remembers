// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 11.7: quest journal — SaveableTypeDefiner registration.

#region

using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Quests
{
    /// <summary>
    ///   Registers <see cref="CalradiaRemembersQuest" /> with Bannerlord's save system.
    ///   The base ID 300_000_000 is chosen based on a scan of all loaded modules:
    ///   vanilla TaleWorlds max ~4_140_000; Bannerlord.ButterLib uses ~222_444_700;
    ///   300_000_000 is safely above both. Do NOT change it after shipping — it is
    ///   baked into every save that contains a CalradiaRemembers quest entry.
    /// </summary>
    public sealed class CalradiaRemembersQuestTypeDefiner : SaveableTypeDefiner
    {
        public CalradiaRemembersQuestTypeDefiner()
            : base(300_000_000)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CalradiaRemembersQuest), 1);
        }
    }
}
