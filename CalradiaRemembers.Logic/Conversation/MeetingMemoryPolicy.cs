// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 2 of the group-scene plan, "what a meeting leaves" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain". At a chat's close one background call updates what the
// character carries away from the player (SDK MeetingMemoryUpdater). It rides the end-of-chat summary job, so a chat too
// slight to earn a summary (ConversationMemoryRules) leaves no meeting memory either, and it runs once per conversation.
//
// If this is wrong: the switch off still spends a call per chat, a witness gets the addressed character's record written
// as his own (lpsalce, 26/11/2026, the same mix-up for memories), or an empty transcript is sent to the model.

namespace CalradiaRemembers.Logic.Conversation
{
    /// <summary>What decides whether a closed chat's summary job also updates the character's meeting memory.</summary>
    public sealed class MeetingMemoryFacts
    {
        /// <summary>MCM "Lasting memory of meetings" (off by default).</summary>
        public bool SwitchOn { get; init; }

        /// <summary>The job is a witness's (present, not the one the player came to see): PendingSummaryJob.HostName is set.</summary>
        public bool IsWitness { get; init; }

        /// <summary>The job carries the conversation's words.</summary>
        public bool HasTranscript { get; init; }
    }

    /// <summary>Whether a closed chat updates the character's meeting memory. Pure.</summary>
    public static class MeetingMemoryPolicy
    {
        /// <summary>
        ///   Only with the switch on, only for the one the player spoke with (a talk between two people: the record is about
        ///   the player, and a witness's place in a scene is Stage 7's), and only when there are words to read.
        /// </summary>
        public static bool ShouldUpdate(MeetingMemoryFacts facts)
            => facts != null && facts.SwitchOn && !facts.IsWitness && facts.HasTranscript;
    }
}
