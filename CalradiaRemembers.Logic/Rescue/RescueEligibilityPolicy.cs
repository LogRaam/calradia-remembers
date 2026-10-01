// Code written by Gabriel Mailhot, 11/07/2026.

#region

#endregion

namespace CalradiaRemembers.Logic.Rescue
{
    /// <summary>
    ///   Pure gate deciding whether a prisoner release should be credited to the player as a RESCUE worth
    ///   consequences (gratitude from the freed lord and their circle, and a possible grudge from the
    ///   captor who lost them). Only a genuine, player-caused freeing of a non-bandit lord who is not in
    ///   the player's own clan counts; releasing a bandit, a clanmate, or the player themselves is not a
    ///   rescue in this sense.
    /// </summary>
    public static class RescueEligibilityPolicy
    {
        /// <summary>
        ///   What is known about one prisoner release, read off the campaign by the caller. SIX booleans
        ///   in a row, and three of them are exclusions rather than requirements, so a swap between a
        ///   "must" and a "must not" inverts the verdict without changing a character of the call.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ReleaseFacts
        {
           /// <summary>The player won the battle this release came out of.</summary>
           public bool PlayerWonBattle { get; set; }

           /// <summary>The prisoner went free as a direct result of that battle, not by some unrelated route.</summary>
           public bool ReleasedAfterBattle { get; set; }

           /// <summary>The freed prisoner is a lord, which is who a rescue is owed to and remembered by.</summary>
           public bool PrisonerIsLord { get; set; }

           /// <summary>The freed prisoner is a bandit. Turning one loose is no rescue.</summary>
           public bool PrisonerIsBandit { get; set; }

           /// <summary>They are of the player's own house, so their freedom is family business, not a debt.</summary>
           public bool PrisonerInPlayerClan { get; set; }

           /// <summary>The freed prisoner IS the player. Nobody is rescued by themselves.</summary>
           public bool PrisonerIsPlayer { get; set; }

           /// <summary>
           ///   The player broke them out of a prison: the game's own prison break, which ends the captivity as an
           ///   ESCAPE with the player named as the one who made it possible (01/10/2026). As much a rescue as a won
           ///   battle, and the freed lord says so in vanilla ("I will remember your help").
           /// </summary>
           public bool BrokenOutByPlayer { get; set; }
        }

        /// <summary>
        ///   How a captivity ended, as the engine tells it (one value per
        ///   <c>TaleWorlds.CampaignSystem.Actions.EndCaptivityDetail</c>). Mapped by the host, so this pure rule
        ///   can say which endings are a RESCUE without the Logic layer knowing the engine.
        /// </summary>
        public enum ReleaseKind
        {
            /// <summary>The engine freed them itself when their captor's party was beaten: nobody chose it.</summary>
            AfterBattle,

            /// <summary>
            ///   Someone CHOSE to let them go. After a battle the player won, that someone is the player, in the
            ///   very conversation vanilla opens to ask (<c>ConversationContext.FreeOrCapturePrisonerHero</c>):
            ///   the freed lord walks up and thanks them. Lordfadooboo (Nexus, 20/09/2026) rescued Lady Mesui from
            ///   deserters exactly this way, "the vanilla encounter where the lord approaches you and thanks you
            ///   happened and i got the clan relation points", and she remembered none of it, because only
            ///   <see cref="AfterBattle" /> was listened for. The battle guard below is what keeps this honest:
            ///   releasing someone the player has held in their own dungeon is a pardon, not a rescue.
            /// </summary>
            ByChoice,

            /// <summary>Ransomed, freed at a peace, escaped, dead, or bought out: none of them a rescue by the player.</summary>
            Other
        }

        /// <summary>Whether this ending can be a rescue at all, before the battle and prisoner facts are weighed.</summary>
        public static bool CanBeARescue(ReleaseKind kind) => kind is ReleaseKind.AfterBattle or ReleaseKind.ByChoice;

        /// <summary>
        ///   True only when the player won the battle (<c>PlayerWonBattle</c>) and the prisoner was
        ///   released as a direct result of it (<c>ReleasedAfterBattle</c>), or the player broke them out of a
        ///   prison (<c>BrokenOutByPlayer</c>); and the freed prisoner is
        ///   a lord (<c>PrisonerIsLord</c>), and the prisoner is neither a bandit
        ///   (<c>PrisonerIsBandit</c>), a member of the player's own clan
        ///   (<c>PrisonerInPlayerClan</c>), nor the player (<c>PrisonerIsPlayer</c>).
        /// </summary>
        public static bool IsPlayerRescue(ReleaseFacts facts)
        {
           if (facts == null) return false;

           bool playerWonBattle = facts.PlayerWonBattle;
           bool releasedAfterBattle = facts.ReleasedAfterBattle;
           bool prisonerIsLord = facts.PrisonerIsLord;
           bool prisonerIsBandit = facts.PrisonerIsBandit;
           bool prisonerInPlayerClan = facts.PrisonerInPlayerClan;
           bool prisonerIsPlayer = facts.PrisonerIsPlayer;

            return (playerWonBattle && releasedAfterBattle || facts.BrokenOutByPlayer)
                && prisonerIsLord
                && !prisonerIsBandit
                && !prisonerInPlayerClan
                && !prisonerIsPlayer;
        }
    }
}
