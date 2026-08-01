// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 11.7: quest journal, SaveableTypeDefiner registration.

#region

using System;
using System.Linq;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Missions;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Quests
{
    /// <summary>
    ///   Registers the CR quest types with Bannerlord's save system. The base ID shipped at 300_000_000 (chosen
    ///   above vanilla ~4.14M and ButterLib ~222.44M). PROBLEM (confirmed 2026-07-31): Europe1100 / EOE 1100 (its
    ///   WhileThyCome assembly) registers a definer at the SAME base(300000000) with its first type at id 1, so
    ///   both mods claim save-id 300000001 and whichever loads SECOND hard-crashes the game at boot. We cannot
    ///   share the id, and we cannot renumber for everyone without moving the ids baked into existing CR-quest
    ///   saves. So the base is chosen at boot (<see cref="QuestSaveBasePolicy" />): keep 300_000_000 when no known
    ///   collider is loaded (existing saves untouched), and shift only when Europe1100 is present, so the two mods
    ///   coexist. NPC memories/relations are unaffected either way (separate byte[] store). Do NOT touch the ids
    ///   1/2/3 or the shipped 300_000_000 constant.
    /// </summary>
    public sealed class CalradiaRemembersQuestTypeDefiner : SaveableTypeDefiner
    {
        public CalradiaRemembersQuestTypeDefiner()
            : base(ResolveBaseId())
        {
        }

        /// <summary>
        ///   The base id: the shipped one, unless a known save-id collider (Europe1100) is loaded, in which case a
        ///   distinct base lets both mods boot. All mod assemblies are already loaded when the save system scans
        ///   for definers, so a loaded collider assembly is detectable here. Any failure defaults to the shipped
        ///   base, which is the safe fallback for the majority (an EOE player would simply see the pre-existing
        ///   crash, never a regression for a non-EOE player).
        /// </summary>
        private static int ResolveBaseId()
        {
            try
            {
                bool colliderPresent = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => QuestSaveBasePolicy.IsColliderAssembly(a.GetName().Name));

                return QuestSaveBasePolicy.Choose(colliderPresent);
            }
            catch
            {
                return QuestSaveBasePolicy.DefaultBase;
            }
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CalradiaRemembersQuest), 1);
            // 2026-07-02: companion-errand journal mirror (ROADMAP "A personal ledger of quests the
            // PLAYER gives out"). Additive id, save-safe: do NOT touch id 1 or the base number.
            AddClassDefinition(typeof(CompanionErrandQuest), 2);
            // 2026-07-03: RP with teeth, increment 2, Phase 2, the pledge (TrackedCommitment) journal
            // mirror. Additive id, save-safe: do NOT touch ids 1/2 or the base number.
            AddClassDefinition(typeof(CommitmentQuest), 3);
        }
    }
}
