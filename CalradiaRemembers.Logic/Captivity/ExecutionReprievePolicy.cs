// Code written by Gabriel Mailhot, 26/11/2026.
// froggyluv (Nexus, 26/11/2026): "Alot of enemy Lords talk arrogantly and smarmy until you order the 'Execute Them' button.
// Then they have the stock 'What?! No! You cant do that Im royalty and a Lord..'. I generally never go thru with it but
// when i talked to one particularly cocky lord after i rescinded the order, he denied that -that had never happened."
// Gabriel (26/11/2026): build it. The game's own threat and reprieve never reached the lord's memory. Read in the 1.5.7
// binaries (LordConversationsCampaignBehavior, HeroExecutionSceneNotificationData): after a battle the player may say
// "Off with your head!" (sentence talk_lord_defeat_to_lord_capture_and_kill), the lord protests, and the player either
// takes it back ("Fine then. You are my prisoner now.", ..._player_answer_1) or orders it (..._player_answer_2), which
// opens the execution scene whose "cancel" takes him prisoner after all. The same scene, opened from the party screen,
// can be cancelled too. Each reprieve gives the spared lord the memory of it.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
    /// <summary>How a condemned lord was spared.</summary>
    public enum ReprieveWay
    {
        /// <summary>No reprieve happened.</summary>
        None,

        /// <summary>Sentenced to death after the battle, face to face, then taken prisoner instead when he protested.</summary>
        RevokedInTalk,

        /// <summary>Sentenced after the battle, brought to the block, and spared there at the last moment.</summary>
        StayedAtTheBlockAfterBattle,

        /// <summary>Brought to the block from the player's prisoners and spared there, with no sentence spoken to his face.</summary>
        StayedAtTheBlock
    }

    /// <summary>
    ///   Follows the game's execution dialogue for one conversation partner and tells when a lord was spared. One instance
    ///   per campaign; pure (sentence ids and hero ids in, a reprieve out).
    /// </summary>
    public sealed class ExecutionReprieveTracker
    {
        /// <summary>The player's "Off with your head!" after a battle.</summary>
        public const string ThreatSentence = "talk_lord_defeat_to_lord_capture_and_kill";

        /// <summary>The player takes it back: "Fine then. You are my prisoner now."</summary>
        public const string RevokeSentence = "talk_lord_defeat_to_lord_capture_and_kill_player_answer_1";

        /// <summary>The player holds to it: "I care not. Prepare to die!", which opens the execution scene.</summary>
        public const string OrderSentence = "talk_lord_defeat_to_lord_capture_and_kill_player_answer_2";

        private string _threatened;
        private string _sentToTheBlock;

        /// <summary>A sentence of the game's dialogue was spoken with <paramref name="partnerId" />. Returns a reprieve when this sentence was one.</summary>
        public ReprieveWay OnSentence(string sentenceId, string partnerId)
        {
            if (string.IsNullOrEmpty(sentenceId) || string.IsNullOrEmpty(partnerId)) return ReprieveWay.None;

            if (sentenceId == ThreatSentence)
            {
                _threatened = partnerId;

                return ReprieveWay.None;
            }

            if (_threatened != partnerId) return ReprieveWay.None;

            if (sentenceId == RevokeSentence)
            {
                _threatened = null;

                return ReprieveWay.RevokedInTalk;
            }

            if (sentenceId == OrderSentence)
            {
                _threatened = null;
                _sentToTheBlock = partnerId;
            }

            return ReprieveWay.None;
        }

        /// <summary>The player cancelled the execution scene of <paramref name="victimId" />: after the battle's sentence, or from the party screen.</summary>
        public ReprieveWay OnExecutionCancelled(string victimId)
        {
            if (string.IsNullOrEmpty(victimId)) return ReprieveWay.None;

            bool afterSentence = _sentToTheBlock == victimId;
            _sentToTheBlock = null;

            return afterSentence ? ReprieveWay.StayedAtTheBlockAfterBattle : ReprieveWay.StayedAtTheBlock;
        }

        /// <summary>The execution scene of <paramref name="victimId" /> went ahead: nothing is left to follow for him.</summary>
        public void OnExecutionCarriedOut(string victimId)
        {
            if (_sentToTheBlock == victimId) _sentToTheBlock = null;
        }
    }

    /// <summary>The memory a spared lord keeps. Pure.</summary>
    public static class ExecutionReprievePolicy
    {
        /// <summary>
        ///   The line remembered by the spared lord, in his own voice, or null for no reprieve. Gender-neutral for the player
        ///   ("they"): the line is kept as written.
        /// </summary>
        public static string Memory(ReprieveWay way, string playerName)
        {
            string who = string.IsNullOrWhiteSpace(playerName) ? "My captor" : playerName.Trim();

            switch (way)
            {
                case ReprieveWay.RevokedInTalk:
                    return $"After the battle, {who} sentenced me to death to my face. When I protested, they changed their mind and took me prisoner instead.";
                case ReprieveWay.StayedAtTheBlockAfterBattle:
                    return $"After the battle, {who} sentenced me to death and had me brought to the block. At the last moment they spared me and took me prisoner instead.";
                case ReprieveWay.StayedAtTheBlock:
                    return $"While I was their prisoner, {who} had me brought to the block to be executed, then spared me at the last moment.";
                default:
                    return null;
            }
        }
    }
}
