// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   Everything a conversation verb needs to decide eligibility, compose its teaching text, or
    ///   execute: the NPC and their profile, the live game-state bridge, the player's current message
    ///   (when teaching mid-conversation), and the per-exchange <see cref="GameState.ActionContext" />
    ///   (only populated at execution time, null while merely teaching). Not every verb reads every
    ///   field; it is a general-purpose carrier so a future verb never needs a new plumbing seam.
    /// </summary>
    public sealed class VerbContext
    {
        public VerbContext(Hero npc, NpcProfile profile, IGameStateBridge bridge, string playerMessage, ActionContext actionContext)
        {
            Npc = npc;
            Profile = profile;
            Bridge = bridge;
            PlayerMessage = playerMessage;
            ActionContext = actionContext;
        }

        /// <summary>The NPC the player is conversing with.</summary>
        public Hero Npc { get; }

        /// <summary>The NPC's persisted memory profile.</summary>
        public NpcProfile Profile { get; }

        /// <summary>The live game-state bridge, for verbs that need to read or mutate campaign state beyond their own Hero.</summary>
        public IGameStateBridge Bridge { get; }

        /// <summary>The player's current message, when teaching mid-conversation; null at execution time.</summary>
        public string PlayerMessage { get; }

        /// <summary>The per-exchange action context (emitted event, offered item id); null while merely teaching.</summary>
        public ActionContext ActionContext { get; }

        /// <summary>
        ///   While teaching, the StringIds of the others present in the scene (witnesses, the host); null when the speaker
        ///   is alone with the player or the path knows no scene. At execution, read ActionContext.PresentHeroIds.
        /// </summary>
        public IReadOnlyList<string> OthersPresentIds { get; set; }
    }
}
