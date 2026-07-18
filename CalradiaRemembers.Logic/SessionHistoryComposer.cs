// Code written by Gabriel Mailhot, 18/07/2026.
// How one participant's ChatSession is rebuilt from the attributed transcript before every LLM call
// (audit C4). Extracted from ChatViewModel.BuildSessionFor, where it sat in the controller and therefore
// had no test at all: the mod project is net472 and the test project is net10.
//
// What it decides matters because this IS the conversation as the model receives it. Each entry becomes a
// player message, somebody else's attributed statement, or the speaker's own assistant turn; and a run of
// the speaker's own consecutive entries that contains narration is replayed as ONE assistant turn rebuilt
// in the emitted [DIALOGUE]/[NARRATION] shape, so the model reads its own past output in the very format
// it is asked to produce. A run with no narration is left exactly as it was, one message per entry:
// ordinary conversations are the overwhelming majority and their history must not be reshaped.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Which role one rebuilt message plays in the session handed to the model.</summary>
    public enum SessionTurnRole
    {
        /// <summary>The player's own words.</summary>
        Player,

        /// <summary>Somebody other than this speaker, replayed attributed by name.</summary>
        Witness,

        /// <summary>This speaker's own prior output, replayed as their assistant turn.</summary>
        Speaker
    }

    /// <summary>One rebuilt message: what to add to the session, and under whose name.</summary>
    public sealed class SessionTurn
    {
        public SessionTurn(SessionTurnRole role, string speakerName, string text)
        {
            Role = role;
            SpeakerName = speakerName;
            Text = text;
        }

        public SessionTurnRole Role { get; }

        /// <summary>Only meaningful for <see cref="SessionTurnRole.Witness" />, which is replayed attributed.</summary>
        public string SpeakerName { get; }

        public string Text { get; }
    }

    /// <summary>
    ///   Rebuilds one participant's view of the conversation from the shared attributed transcript. Pure:
    ///   the host supplies the lines and who is asking, this decides what each becomes.
    /// </summary>
    public static class SessionHistoryComposer
    {
        /// <param name="transcript">The attributed history, oldest first.</param>
        /// <param name="speakerHeroId">
        ///   Whose session is being rebuilt. An empty id claims NOTHING as its own: a speaker the engine has
        ///   no hero for cannot be matched against the recorded ids, and guessing by name instead would let
        ///   one participant inherit another's lines as their own assistant turns.
        /// </param>
        /// <param name="excludeLastN">
        ///   Skips the last N entries. Sprint 15C builds the NPC's session before the witness reactions are
        ///   committed, so those reactions reach the model only as the stimulus and are not double-counted.
        /// </param>
        public static IReadOnlyList<SessionTurn> Compose(IReadOnlyList<TranscriptLine> transcript,
                                                         string speakerHeroId,
                                                         int excludeLastN = 0)
        {
            var turns = new List<SessionTurn>();
            if (transcript == null) return turns;

            int limit = Math.Max(0, transcript.Count - Math.Max(0, excludeLastN));

            for (var i = 0; i < limit; i++)
            {
                TranscriptLine e = transcript[i];

                if (e.IsPlayer)
                {
                    turns.Add(new SessionTurn(SessionTurnRole.Player, null, e.Text));

                    continue;
                }

                if (!IsOwnLine(e, speakerHeroId))
                {
                    turns.Add(new SessionTurn(SessionTurnRole.Witness, e.SpeakerName, e.Text));

                    continue;
                }

                // The run of this speaker's own consecutive entries. It is bounded by `limit`, so an entry
                // trimmed by excludeLastN is never pulled into the turn that precedes it.
                int runEnd = i;
                var hasNarration = false;

                while (runEnd < limit && IsOwnLine(transcript[runEnd], speakerHeroId))
                {
                    hasNarration |= transcript[runEnd].IsNarration;
                    runEnd++;
                }

                if (!hasNarration)
                {
                    turns.Add(new SessionTurn(SessionTurnRole.Speaker, e.SpeakerName, e.Text));

                    continue;
                }

                var turn = new StringBuilder();
                for (int k = i; k < runEnd; k++)
                    turn.Append(transcript[k].IsNarration
                        ? $"[NARRATION]{transcript[k].Text}[/NARRATION]"
                        : $"[DIALOGUE]{transcript[k].Text}[/DIALOGUE]");

                turns.Add(new SessionTurn(SessionTurnRole.Speaker, e.SpeakerName, turn.ToString()));
                i = runEnd - 1;
            }

            return turns;
        }

        private static bool IsOwnLine(TranscriptLine entry, string speakerHeroId)
            => !entry.IsPlayer
            && !string.IsNullOrEmpty(speakerHeroId)
            && entry.SpeakerHeroId == speakerHeroId;
    }
}
