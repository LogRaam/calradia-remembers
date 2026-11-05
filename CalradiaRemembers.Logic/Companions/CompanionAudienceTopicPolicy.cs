// Code written by Gabriel Mailhot, 22/07/2026.
// Player feedback (Nexus): companions travelling in the party "had a lot to say, things they should have told
// [the player] long before" but had no way to start that conversation. The ratified design rule is that an
// audience must carry something the player can ACT on, DECIDE, or LEARN, never just a mood check-in, so the
// mod must FIND the thing worth saying before it ever asks for one ("no topic found means no audience"). This
// is the pure selection only: given the candidate topics the engine has already checked live state for, which
// single one (if any) earns today's audience. Engine glue (reading missions/grudges/party state/world events,
// opening the popup, the free refusal) lives in the mod.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>
    ///   One topic under consideration for a found-topic companion audience: whether its precondition (read
    ///   from live state by the caller) actually holds, and how strong/interesting it is (0..100, comparable
    ///   across topics) when it does. <see cref="Strength" /> on an ineligible candidate is never read.
    /// </summary>
    public sealed class CompanionAudienceTopicCandidate
    {
        public CompanionAudienceTopicCandidate(CompanionAudienceReason topic, bool preconditionHolds, int strength)
        {
            Topic = topic;
            PreconditionHolds = preconditionHolds;
            Strength = strength;
        }

        /// <summary>Which of the found-topics this candidate represents.</summary>
        public CompanionAudienceReason Topic { get; }

        /// <summary>Whether the real-world condition behind this topic is actually true right now.</summary>
        public bool PreconditionHolds { get; }

        /// <summary>How strong/interesting this topic is (0..100), only meaningful when <see cref="PreconditionHolds" />.</summary>
        public int Strength { get; }
    }

    /// <summary>
    ///   Picks the single strongest ELIGIBLE topic among the candidates the caller offers, or
    ///   <see cref="CompanionAudienceReason.None" /> when nothing qualifies, silence is the default, never a
    ///   fallback "just to say something".
    /// </summary>
    public static class CompanionAudienceTopicPolicy
    {
        // A GLOBAL cooldown across ALL companions, not per companion. A party of 8 companions each running
        // their own timer would still produce a popup every few days from SOMEONE, exactly the "oh no, not
        // him again" failure mode the design rule exists to avoid. One shared clock means the roster as a
        // whole asks for a private word at a human pace, however many companions are in the party.
        // 08/09/2026: this is now also the "Occasionally" value CompanionAudiencePacingRules.CooldownDays
        // returns, and the source CompanionAudiencePacingRules.DefaultCooldownDays reads from, so the player's
        // pacing dial scales this number rather than replacing it with an unrelated one.
        public const int GlobalCooldownDays = 14;

        /// <summary>
        ///   The strongest topic whose precondition holds, or <see cref="CompanionAudienceReason.None" /> when
        ///   none do. Ties (equal strength) resolve by the topic's own enum ordinal, so the SAME set of
        ///   candidates always yields the SAME answer no matter what order the caller built the list in.
        /// </summary>
        public static CompanionAudienceReason SelectTopic(IReadOnlyList<CompanionAudienceTopicCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0) return CompanionAudienceReason.None;

            CompanionAudienceTopicCandidate best = candidates
                .Where(c => c != null && c.PreconditionHolds)
                .OrderByDescending(c => c.Strength)
                .ThenBy(c => (int) c.Topic)
                .FirstOrDefault();

            return best?.Topic ?? CompanionAudienceReason.None;
        }

        /// <summary>
        ///   The topic actually raised: a draw among those that hold, each as likely as it is strong, so the strongest is
        ///   the likeliest but never the only one. Lufaine (Nexus, 04/11/2026): "the dialogue always seems to be either an
        ///   update about a war declaration or a memory of a previous scene. Makes the interaction seem quite robotic and
        ///   predictable." A scheme warning is never left to chance: it is raised whenever it holds.
        ///   <paramref name="roll" /> is in [0, 1); outside it reads as the edge.
        /// </summary>
        public static CompanionAudienceReason DrawTopic(IReadOnlyList<CompanionAudienceTopicCandidate> candidates, double roll)
        {
            List<CompanionAudienceTopicCandidate> holding = (candidates ?? new List<CompanionAudienceTopicCandidate>())
                .Where(c => c != null && c.PreconditionHolds && c.Strength > 0)
                .OrderBy(c => (int) c.Topic)
                .ToList();
            if (holding.Count == 0) return CompanionAudienceReason.None;
            if (holding.Any(c => c.Topic == CompanionAudienceReason.SchemeWarning)) return CompanionAudienceReason.SchemeWarning;

            int total = holding.Sum(c => c.Strength);
            double at = Math.Max(0, Math.Min(0.999999, roll)) * total;
            foreach (CompanionAudienceTopicCandidate c in holding)
            {
                if (at < c.Strength) return c.Topic;
                at -= c.Strength;
            }

            return holding[holding.Count - 1].Topic;
        }
    }
}
