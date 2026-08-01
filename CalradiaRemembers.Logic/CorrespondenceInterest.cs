// Code written by Gabriel Mailhot, 01/08/2026.
// A2: the mailbox learns from the player. LetterWeights scores a letter's SUBJECT in isolation, but a 100h
// player who keeps skipping the same lord's letters (or the same topic) is telling us something the subject
// tier alone cannot capture: THEY, specifically, are tired of hearing from THAT sender, or about THAT
// subject, or from correspondence in general right now. This layer turns that signal into a set of damper
// deltas that LetterWeights.WeightWithInterest folds on top of the base weight, and lets the player state an
// explicit preference too, so the adaptation is never a black box the player cannot see or override.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How broadly a stated correspondence preference applies.</summary>
    public enum PreferenceScope
    {
        /// <summary>No preference was stated (or none could be parsed); nothing is dampened by it.</summary>
        None,

        /// <summary>Applies to one specific sender only.</summary>
        Sender,

        /// <summary>Applies to one specific subject (a <see cref="NpcMemoryService.Core.Models.LetterReason" />) only.</summary>
        Subject,

        /// <summary>Applies across all unprompted correspondence, regardless of sender or subject.</summary>
        All
    }

    /// <summary>Which way a stated correspondence preference pushes interest.</summary>
    public enum PreferenceDirection
    {
        /// <summary>The player wants less of this: interest is pushed further from zero (more negative).</summary>
        Less,

        /// <summary>The player wants more of this: interest is pushed back toward zero.</summary>
        More
    }

    /// <summary>How firmly a stated correspondence preference should move the interest dial.</summary>
    public enum PreferenceStrength
    {
        /// <summary>A gentle nudge.</summary>
        Mild,

        /// <summary>A firm, unmistakable ask.</summary>
        Strong
    }

    /// <summary>
    ///   A single correspondence preference the player has stated (typically parsed by
    ///   <see cref="CorrespondencePreferenceParser" /> from a reply's control line). A value type so it is
    ///   trivially testable and safe to pass around without allocation concerns.
    /// </summary>
    public readonly struct CorrespondencePreference
    {
        /// <summary>Constructs a stated preference with an explicit scope, direction and strength.</summary>
        public CorrespondencePreference(PreferenceScope scope, PreferenceDirection direction, PreferenceStrength strength)
        {
            Scope = scope;
            Direction = direction;
            Strength = strength;
        }

        /// <summary>How broadly this preference applies.</summary>
        public PreferenceScope Scope { get; }

        /// <summary>Which way this preference pushes interest.</summary>
        public PreferenceDirection Direction { get; }

        /// <summary>How firmly this preference should move the interest dial.</summary>
        public PreferenceStrength Strength { get; }

        /// <summary>
        ///   The absence of any stated preference: <see cref="Scope" /> is <see cref="PreferenceScope.None" />,
        ///   which <see cref="CorrespondenceInterestPolicy.DeltasFor" /> always turns into an all-zero delta.
        /// </summary>
        public static CorrespondencePreference None { get; } =
            new CorrespondencePreference(PreferenceScope.None, PreferenceDirection.Less, PreferenceStrength.Mild);
    }

    /// <summary>
    ///   The three interest dampers <see cref="LetterWeights.WeightWithInterest" /> folds onto a base weight,
    ///   as a single delta to apply to an NPC's running interest state. A value type so a caller can compute
    ///   one from an event (<see cref="CorrespondenceInterestPolicy.OnAcknowledge" />,
    ///   <see cref="CorrespondenceInterestPolicy.OnReply" />, or a stated preference via
    ///   <see cref="CorrespondenceInterestPolicy.DeltasFor" />) and add it straight onto the running totals.
    /// </summary>
    public readonly struct InterestDeltas
    {
        /// <summary>Constructs a delta for all three interest channels.</summary>
        public InterestDeltas(int senderDelta, int subjectDelta, int globalDelta)
        {
            SenderDelta = senderDelta;
            SubjectDelta = subjectDelta;
            GlobalDelta = globalDelta;
        }

        /// <summary>The change to apply to this one sender's running interest.</summary>
        public int SenderDelta { get; }

        /// <summary>The change to apply to this one subject's running interest.</summary>
        public int SubjectDelta { get; }

        /// <summary>The change to apply to the player's overall correspondence interest.</summary>
        public int GlobalDelta { get; }
    }

    /// <summary>
    ///   Turns the player's correspondence behaviour, explicit or implicit, into the <see cref="InterestDeltas" />
    ///   that a caller accumulates per sender/subject/globally and then clamps (<see cref="ClampSender" />,
    ///   <see cref="ClampSubject" />, <see cref="ClampGlobal" />) before handing the running totals to
    ///   <see cref="LetterWeights.WeightWithInterest" />. Every interest value lives in [floor, 0]: zero is
    ///   neutral (never a bonus), and the floor is how dampened a channel can ever become. Purely arithmetic;
    ///   the caller owns where the running totals are stored and how long they persist.
    /// </summary>
    public static class CorrespondenceInterestPolicy
    {
        // ── Floors: how far each channel can be dampened before clamping holds it ───────────────────────

        /// <summary>The floor for a single sender's interest: the most one correspondent can be tuned out.</summary>
        public const int SenderFloor = -40;

        /// <summary>The floor for a single subject's interest: shallower than sender, since a subject is shared across many senders.</summary>
        public const int SubjectFloor = -25;

        /// <summary>The floor for the player's overall correspondence interest.</summary>
        public const int GlobalFloor = -30;

        /// <summary>
        ///   The ceiling shared by all three channels. Interest is a damper, never a bonus: "More" only ever
        ///   pushes a channel back up toward this neutral ceiling, never past it.
        /// </summary>
        public const int InterestCeiling = 0;

        // ── Implicit signal: the player read the letter but did not answer ──────────────────────────────

        /// <summary>The sender-interest penalty for an acknowledged-but-unanswered letter.</summary>
        public const int AcknowledgeSenderPenalty = -8;

        /// <summary>The subject-interest penalty for an acknowledged-but-unanswered letter.</summary>
        public const int AcknowledgeSubjectPenalty = -5;

        /// <summary>
        ///   The delta for a letter the player acknowledged (read) but never replied to: a mild, repeatable
        ///   nudge that the sender and subject are wearing thin, without touching global interest at all
        ///   (one quiet correspondent should not dampen everybody else's mail).
        /// </summary>
        public static InterestDeltas OnAcknowledge()
        {
            return new InterestDeltas(AcknowledgeSenderPenalty, AcknowledgeSubjectPenalty, 0);
        }

        // ── Implicit signal: the player actually wrote back ─────────────────────────────────────────────

        /// <summary>The sender-interest bonus for a genuine reply: a strong recovery, not a slow crawl.</summary>
        public const int ReplySenderBonus = 40;

        /// <summary>The subject-interest bonus for a genuine reply.</summary>
        public const int ReplySubjectBonus = 8;

        /// <summary>
        ///   The delta for a letter the player actually answered: the clearest possible signal of interest,
        ///   so it lifts sender interest sharply (able to undo several Acknowledge penalties in one reply)
        ///   and subject interest more modestly. Global interest is untouched, for the same reason as
        ///   <see cref="OnAcknowledge" />: one reply should not itself brighten every other correspondent.
        /// </summary>
        public static InterestDeltas OnReply()
        {
            return new InterestDeltas(ReplySenderBonus, ReplySubjectBonus, 0);
        }

        // ── Explicit signal: the player stated a preference outright ────────────────────────────────────

        /// <summary>The magnitude a Strong, Sender-scoped preference moves sender interest.</summary>
        public const int SenderStrongMagnitude = 40;

        /// <summary>The magnitude a Mild, Sender-scoped preference moves sender interest.</summary>
        public const int SenderMildMagnitude = 20;

        /// <summary>The magnitude a Strong, Subject-scoped preference moves subject interest.</summary>
        public const int SubjectStrongMagnitude = 25;

        /// <summary>The magnitude a Mild, Subject-scoped preference moves subject interest.</summary>
        public const int SubjectMildMagnitude = 12;

        /// <summary>The magnitude a Strong, All-scoped preference moves global interest.</summary>
        public const int AllStrongMagnitude = 30;

        /// <summary>The magnitude a Mild, All-scoped preference moves global interest.</summary>
        public const int AllMildMagnitude = 15;

        /// <summary>
        ///   Turns a stated <see cref="CorrespondencePreference" /> into the one-time <see cref="InterestDeltas" />
        ///   it should apply. Only the channel named by <see cref="CorrespondencePreference.Scope" /> ever
        ///   moves; the other two stay exactly zero, so a "less from this lord" preference never touches
        ///   subject or global interest by accident. <see cref="PreferenceScope.None" /> (including the
        ///   default <see cref="CorrespondencePreference.None" />) always yields an all-zero delta.
        /// </summary>
        public static InterestDeltas DeltasFor(CorrespondencePreference pref)
        {
            int sign = pref.Direction == PreferenceDirection.More ? 1 : -1;

            switch (pref.Scope)
            {
                case PreferenceScope.Sender:
                    int senderMagnitude = pref.Strength == PreferenceStrength.Strong ? SenderStrongMagnitude : SenderMildMagnitude;
                    return new InterestDeltas(sign * senderMagnitude, 0, 0);

                case PreferenceScope.Subject:
                    int subjectMagnitude = pref.Strength == PreferenceStrength.Strong ? SubjectStrongMagnitude : SubjectMildMagnitude;
                    return new InterestDeltas(0, sign * subjectMagnitude, 0);

                case PreferenceScope.All:
                    int allMagnitude = pref.Strength == PreferenceStrength.Strong ? AllStrongMagnitude : AllMildMagnitude;
                    return new InterestDeltas(0, 0, sign * allMagnitude);

                default: // None
                    return new InterestDeltas(0, 0, 0);
            }
        }

        // ── Clamps: hold every running total inside [floor, InterestCeiling] ────────────────────────────

        /// <summary>Clamps a running sender-interest total to [<see cref="SenderFloor" />, <see cref="InterestCeiling" />].</summary>
        public static int ClampSender(int v)
        {
            return Clamp(v, SenderFloor);
        }

        /// <summary>Clamps a running subject-interest total to [<see cref="SubjectFloor" />, <see cref="InterestCeiling" />].</summary>
        public static int ClampSubject(int v)
        {
            return Clamp(v, SubjectFloor);
        }

        /// <summary>Clamps a running global-interest total to [<see cref="GlobalFloor" />, <see cref="InterestCeiling" />].</summary>
        public static int ClampGlobal(int v)
        {
            return Clamp(v, GlobalFloor);
        }

        private static int Clamp(int v, int floor)
        {
            return Math.Max(floor, Math.Min(InterestCeiling, v));
        }
    }
}
