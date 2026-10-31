// Code written by Gabriel Mailhot, 31/10/2026.
// The killing attempt, played in front of the player. zhenja1 (Nexus, 31/10/2026) confirmed a killing twice and the
// man lived both times: the attempt is a roll (ViolencePolicy), but the window never named the odds and a failure
// showed only as a line in the message feed, while the character talked on as if nothing had happened. Gabriel
// (31/10/2026): the window names the chance, a roll plays out "a bit like Baldur's Gate's dice, but only the number
// changing quickly until it stops on the score", then the model writes the scene: how the player kills the
// character, or how the character survives. After a kill with nobody else there, only leaving remains; a captive
// who kills their captor escapes; after a failure the character answers it.
//
// The game rolls and decides; this file only shows the roll faithfully and says what follows. The model narrates an
// outcome it is given, never one it chooses.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Violence
{
    /// <summary>What follows a killing attempt once the scene has been told.</summary>
    public enum KillAftermath
    {
        /// <summary>The attempt failed: the character, alive, answers it in their next line.</summary>
        NpcReacts,

        /// <summary>The character is dead: the conversation is over and only leaving remains.</summary>
        ConversationEnds,

        /// <summary>The player was the character's captive and killed them: the player escapes as the scene closes.</summary>
        PlayerEscapes
    }

    /// <summary>The facts that decide what follows a killing attempt.</summary>
    public sealed class KillAftermathFacts
    {
        /// <summary>The roll came off and the game let the character die.</summary>
        public bool Killed { get; init; }

        /// <summary>The player is a prisoner in a captive scene with this character.</summary>
        public bool PlayerIsCaptive { get; init; }
    }

    /// <summary>How a killing attempt ended, once the game rolled and the deed was done.</summary>
    public sealed class KillAttemptResult
    {
        /// <summary>The roll's verdict, as the consequences were charged.</summary>
        public ViolenceVerdict Verdict { get; init; }

        /// <summary>The character is dead: the roll came off and the game let them die.</summary>
        public bool Killed { get; init; }

        /// <summary>The plain outcome, as the message feed shows it.</summary>
        public string Message { get; init; }
    }

    /// <summary>One step of the rolling number: the value shown and how long it stays before the next.</summary>
    public sealed class KillRollFrame
    {
        /// <summary>The number shown, 1 to 100.</summary>
        public int Value { get; init; }

        /// <summary>How long this number stays on screen, in seconds.</summary>
        public float Seconds { get; init; }
    }

    /// <summary>The facts the narrator is given to tell a killing attempt.</summary>
    public sealed class KillNarrationFacts
    {
        /// <summary>The player character's name.</summary>
        public string PlayerName { get; init; }

        /// <summary>The character the player tried to kill.</summary>
        public string VictimName { get; init; }

        /// <summary>The attempt came off and the character is dead.</summary>
        public bool Killed { get; init; }

        /// <summary>Who was at hand: guards and retinue, onlookers, or nobody.</summary>
        public ViolenceExposure Exposure { get; init; }

        /// <summary>The player was the character's prisoner.</summary>
        public bool PlayerIsCaptive { get; init; }

        /// <summary>The last lines of the conversation, oldest first, as "Name: text".</summary>
        public IReadOnlyList<string> RecentLines { get; init; }
    }

    /// <summary>Shows a killing attempt's roll faithfully and decides what follows it.</summary>
    public static class KillAttemptPolicy
    {
        /// <summary>How many numbers flick past before the roll lands. Tuning.</summary>
        public const int FrameCount = 26;

        /// <summary>How long the first numbers stay: a blur. Tuning.</summary>
        public const float FastestFrameSeconds = 0.04f;

        /// <summary>How long the last number before the result stays: the roll slowing to a stop. Tuning.</summary>
        public const float SlowestFrameSeconds = 0.26f;

        /// <summary>How many lines of the conversation the narrator reads, so the scene follows what was said. Tuning.</summary>
        public const int RecentLineCount = 6;

        /// <summary>
        ///   The number the player sees for an engine roll in [0, 100): 1 to 100. The policy succeeds on
        ///   roll &lt; chance, so the shown number succeeds exactly when it is at most the chance (see <see cref="Succeeds" />).
        /// </summary>
        public static int ShownRoll(int roll) => Math.Min(Math.Max(roll, 0), 99) + 1;

        /// <summary>Whether a shown number of <paramref name="shown" /> comes off against <paramref name="chance" /> in 100: at most the chance.</summary>
        public static bool Succeeds(int shown, int chance) => shown <= chance;

        /// <summary>
        ///   The rolling number: <see cref="FrameCount" /> numbers that slow down and land on <paramref name="shown" />.
        ///   The numbers before the last are random but never repeat the one before, so the roll visibly moves; the
        ///   same seed gives the same roll.
        /// </summary>
        public static IReadOnlyList<KillRollFrame> Frames(int shown, int seed)
        {
            var random = new Random(seed);
            var frames = new List<KillRollFrame>(FrameCount);
            int previous = 0;
            for (var i = 0; i < FrameCount - 1; i++)
            {
                int value;
                do value = random.Next(1, 101);
                while (value == previous || value == shown && i == FrameCount - 2);

                // Ease out: the blur slows to a stop, quadratically, so the last numbers can be read.
                float t = i / (float) (FrameCount - 2);
                frames.Add(new KillRollFrame {Value = value, Seconds = FastestFrameSeconds + (SlowestFrameSeconds - FastestFrameSeconds) * t * t});
                previous = value;
            }

            frames.Add(new KillRollFrame {Value = Math.Min(Math.Max(shown, 1), 100), Seconds = 0f});

            return frames;
        }

        /// <summary>The line under the chance that says where it comes from, in the player's words.</summary>
        public static string ChanceReasons(ViolenceFacts facts)
        {
            if (facts == null) return "";

            string arms = facts.PlayerProwess >= facts.TargetProwess * 1.2 ? "You are the better fighter"
                : facts.TargetProwess >= facts.PlayerProwess * 1.2 ? "They are the better fighter"
                : "You are evenly matched";
            string company = facts.Exposure == ViolenceExposure.Guarded ? "guards or their men are at hand"
                : facts.Exposure == ViolenceExposure.Onlookers ? "others are watching"
                : "you are alone with them";

            return $"{arms}, and {company}.";
        }

        /// <summary>What follows the attempt: a kill ends the conversation (and frees a captive), a failure is answered.</summary>
        public static KillAftermath Decide(KillAftermathFacts facts)
        {
            if (facts == null || !facts.Killed) return KillAftermath.NpcReacts;

            return facts.PlayerIsCaptive ? KillAftermath.PlayerEscapes : KillAftermath.ConversationEnds;
        }

        /// <summary>The narrator's instructions: tell the given outcome, never another.</summary>
        public const string NarratorInstructions =
            "You narrate one moment of a roleplay set in Calradia, a harsh world of late antiquity and the early Middle Ages. "
            + "The game has already decided what happened; you tell it, you never change it. "
            + "Write in the second person for the player (\"you\") and the third person for the other character, in the present tense. "
            + "Write one short paragraph of three to five sentences: the act itself, concrete and physical, without lingering on gore. "
            + "Whatever the conversation said before this moment, the outcome given to you is what happened. "
            + "Write no dialogue, no title, no choices, nothing outside the scene.";

        /// <summary>The narrator's task for this attempt: who, where, the outcome, and the conversation that led to it.</summary>
        public static string NarratorTask(KillNarrationFacts facts)
        {
            if (facts == null) return "";

            string player = string.IsNullOrWhiteSpace(facts.PlayerName) ? "the player" : facts.PlayerName;
            string victim = string.IsNullOrWhiteSpace(facts.VictimName) ? "the other character" : facts.VictimName;
            var b = new StringBuilder();

            if (facts.RecentLines != null && facts.RecentLines.Count > 0)
            {
                b.AppendLine("THE CONVERSATION SO FAR:");
                foreach (string line in facts.RecentLines.Skip(Math.Max(0, facts.RecentLines.Count - RecentLineCount)))
                    b.AppendLine(line);
                b.AppendLine();
            }

            b.Append($"WHAT HAPPENS NOW: you, {player}, try to kill {victim}");
            b.Append(facts.PlayerIsCaptive ? ", though you are their prisoner. " : ". ");
            b.Append(facts.Exposure == ViolenceExposure.Guarded ? "Guards or their own men are close at hand. "
                : facts.Exposure == ViolenceExposure.Onlookers ? "Others are there and see it. "
                : "Nobody else is there. ");

            if (facts.Killed)
            {
                b.Append($"OUTCOME: you succeed. {victim} dies here; tell how you kill them, and end on their death.");
                if (facts.PlayerIsCaptive) b.Append(" Then you slip away from your captivity before anyone stops you.");
            }
            else
            {
                b.Append($"OUTCOME: you fail. {victim} survives, unharmed or barely hurt; tell how the attempt goes wrong");
                b.Append(facts.Exposure == ViolenceExposure.Guarded ? " and how those at their side stop you"
                    : facts.Exposure == ViolenceExposure.Onlookers ? " under the eyes of the others"
                    : "");
                b.Append($". End the moment the attempt is over, before {victim} says or decides anything: they answer it themselves after you.");
            }

            return b.ToString();
        }

        /// <summary>The line shown when no narration could be written: the plain outcome, so the scene still says what happened.</summary>
        public static string FallbackNarration(KillNarrationFacts facts)
        {
            string victim = string.IsNullOrWhiteSpace(facts?.VictimName) ? "them" : facts.VictimName;
            if (facts?.Killed == true)
                return facts.PlayerIsCaptive ? $"You kill {victim}, and slip away from your captivity before anyone can stop you." : $"You kill {victim}.";

            return facts?.Exposure == ViolenceExposure.Guarded ? $"Your attempt on {victim} fails. Those at their side saw to that."
                : facts?.Exposure == ViolenceExposure.Onlookers ? $"Your attempt on {victim} fails. There were too many eyes on you."
                : $"Your attempt on {victim} fails.";
        }
    }
}
