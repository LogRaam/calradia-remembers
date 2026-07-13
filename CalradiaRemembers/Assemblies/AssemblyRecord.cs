// Code written by Gabriel Mailhot, 12/07/2026.
// ASSEMBLY pillar, Inc 0: the persisted shape of one convened assembly. Plain data (no engine references) so
// it round-trips through JSON SyncData safely; heroes and settlements are held by their stable StringId.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>One convened assembly: where, who called it, its deadline, and the invited roster.</summary>
    public sealed class AssemblyRecord
    {
        /// <summary>StringId of the host town or castle.</summary>
        public string HostSettlementId { get; set; }

        /// <summary>StringId of the hero who summoned it (the player, or a lord for the reverse case).</summary>
        public string InitiatorId { get; set; }

        /// <summary>Game day the summons went out.</summary>
        public int SummonedDay { get; set; }

        /// <summary>Game day the arrival window closes (accepted invitees not yet present become ambassadors).</summary>
        public int Deadline { get; set; }

        /// <summary>One seat per invited hero.</summary>
        public List<AssemblyInviteeRecord> Invitees { get; set; } = new();
    }

    /// <summary>One invited seat: the hero and their current lifecycle status.</summary>
    public sealed class AssemblyInviteeRecord
    {
        public string HeroId { get; set; }

        public AssemblyInviteeStatus Status { get; set; }
    }
}
