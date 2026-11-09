// Code written by Gabriel Mailhot, 09/11/2026.
// Whether an extort read this turn will really be carried out, so the chat knows if it charges its own regard cost
// (TurnRegardPolicy). Here rather than in the chat: the chat already knows the verbs, and the verbs already know the
// violence behaviors; asking ExtortionBehavior from the chat would tie those two together for one question.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Violence;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>Whether an extort toward a character would be carried out today.</summary>
    internal static class ExtortReadiness
    {
        /// <summary>True when the player holds something over this character today. False on any fault.</summary>
        internal static bool WillRun(Hero npc)
        {
            try { return ExtortionBehavior.Instance?.HoldOver(npc) != null; }
            catch (Exception ex)
            {
                ModLog.Error($"[EXTORT] hold check failed (non-fatal): {ex.GetType().Name}: {ex.Message}");

                return false;
            }
        }
    }
}
