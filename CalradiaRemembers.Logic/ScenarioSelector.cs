// Code written by Gabriel Mailhot, 24/06/2026.
// The heart of the scripted-scenarios engine: given a fired event, pick the ONE rule that applies. Several
// rules may share a trigger (the four Rhagaea-marriage outcomes are four rules on one trigger); the eligible
// ones — trigger matched, not a spent one-shot, guards passed — form a pool from which exactly one is drawn
// at random. That single mechanism gives replayability with no authored branching. Pure: the RNG and the
// guard/fired-once facts are injected, so every path is deterministically testable.
// 11/10/2026: an event no longer plays a single rule (ScenarioPass plays every ready one, Gabriel's ruling); Draw is
// what remains of the one-of-many pick, used for the alternatives of a group.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Selects the single scenario rule to fire for an event, or null when none apply.</summary>
    public static class ScenarioSelector
    {
        /// <summary>
        ///   True when a rule's trigger matches a fired event: same verb (case-insensitive) and either the
        ///   rule's payload is a wildcard (matches any) or it equals the event's payload (case-insensitive).
        /// </summary>
        public static bool TriggerMatches(KeywordToken ruleTrigger, KeywordToken firedEvent)
        {
            if (ruleTrigger == null || firedEvent == null) return false;
            if (!string.Equals(ruleTrigger.Verb, firedEvent.Verb, StringComparison.OrdinalIgnoreCase)) return false;
            if (ruleTrigger.IsWildcardPayload) return true;

            // A payload is a path (ON_TALE:<tale>:<stage>, Gabriel 12/10/2026): a rule naming its first parts answers
            // everything under them, never a sibling whose name merely begins the same.
            return string.Equals(ruleTrigger.Payload, firedEvent.Payload, StringComparison.OrdinalIgnoreCase)
                   || firedEvent.Payload.StartsWith(ruleTrigger.Payload + ":", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///   Returns the rules eligible for <paramref name="firedEvent" />: trigger matched, not a spent
        ///   one-shot (in <paramref name="firedRuleIds" />), and all guards passing via
        ///   <paramref name="guardResolver" />. Order is preserved; never null.
        /// </summary>
        public static IReadOnlyList<ScenarioRule> Eligible(
            KeywordToken firedEvent,
            IEnumerable<ScenarioRule> rules,
            Func<KeywordToken, bool> guardResolver,
            ISet<string> firedRuleIds)
        {
            var eligible = new List<ScenarioRule>();
            if (rules == null) return eligible;

            foreach (ScenarioRule rule in rules)
            {
                if (rule == null) continue;
                if (!TriggerMatches(rule.Trigger, firedEvent)) continue;
                if (rule.FireOnce && firedRuleIds != null && firedRuleIds.Contains(rule.Id)) continue;
                if (!GuardEvaluator.AllPass(rule.Guards, guardResolver ?? (_ => true))) continue;
                eligible.Add(rule);
            }

            return eligible;
        }

        /// <summary>
        ///   Picks the one rule to fire for <paramref name="firedEvent" />, or null when none are eligible.
        ///   With a single eligible rule it is returned outright; with several, one is drawn by weight using
        ///   <paramref name="nextBelow" /> (an injected RNG returning a value in <c>[0, upperExclusive)</c>).
        /// </summary>
        public static ScenarioRule Select(
            KeywordToken firedEvent,
            IEnumerable<ScenarioRule> rules,
            Func<KeywordToken, bool> guardResolver,
            ISet<string> firedRuleIds,
            Func<int, int> nextBelow)
        {
            return Draw(Eligible(firedEvent, rules, guardResolver, firedRuleIds), nextBelow);
        }

        /// <summary>
        ///   One rule drawn by weight among <paramref name="eligible" /> (the alternatives of a group), or null when there
        ///   are none; a single one is returned outright. <paramref name="nextBelow" /> returns a value in [0, upper).
        /// </summary>
        public static ScenarioRule Draw(IReadOnlyList<ScenarioRule> eligible, Func<int, int> nextBelow)
        {
            if (eligible == null || eligible.Count == 0) return null;
            if (eligible.Count == 1) return eligible[0];

            int totalWeight = 0;
            foreach (ScenarioRule rule in eligible) totalWeight += rule.Weight;

            int roll = nextBelow != null ? nextBelow(totalWeight) : 0;
            if (roll < 0) roll = 0;
            if (roll >= totalWeight) roll = totalWeight - 1;

            int cumulative = 0;
            foreach (ScenarioRule rule in eligible)
            {
                cumulative += rule.Weight;
                if (roll < cumulative) return rule;
            }

            return eligible[eligible.Count - 1]; // unreachable when weights are positive; a defensive fallback
        }
    }
}
