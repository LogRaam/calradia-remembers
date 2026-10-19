// Code written by Gabriel Mailhot, 11/10/2026.
// Gabriel (11/10/2026): "Si plusieurs regles existent pour la mort de Rhagaea, tous devraient etre joues. Et des guards
// devraient proteger le jeu en verifiant que les conditions du jeu sont favorables a l'execution du scenario." Until
// then ONE rule was drawn among every eligible rule of an event (ScenarioSelector.Select), so a war and a pretender on
// the same death could not both happen, and a mod's broad rule could take the place of CR's. Now every eligible rule
// plays, in reading order (CR's packs, the player's, then the mods'), each checked at its own turn so it sees what the
// rules before it changed. Rules sharing a GROUP are alternatives (the four outcomes of marrying Rhagaea): one drawn by
// weight among those eligible at the group's turn. And a rule plays only when every one of its consequences can apply:
// a message that told only part of what happened would lie to the player.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Scenarios
{
    /// <summary>What a fired event asks of the world while it plays its rules, and how it plays one.</summary>
    public sealed class ScenarioChecks
    {
        /// <summary>The one-shot rules already played in this save (a sealed one never plays again).</summary>
        public ISet<string> Sealed { get; init; }

        /// <summary>Whether a guard holds in the world as it is NOW.</summary>
        public Func<KeywordToken, bool> GuardPasses { get; init; }

        /// <summary>Whether a consequence can really apply NOW (a war between two realms already at war cannot).</summary>
        public Func<KeywordToken, bool> ConsequenceApplies { get; init; }

        /// <summary>A random value in [0, upper), for drawing among a group's alternatives.</summary>
        public Func<int, int> NextBelow { get; init; }

        /// <summary>Plays the rule: its consequences, its message, its seal.</summary>
        public Action<ScenarioRule> Play { get; init; }
    }

    /// <summary>Plays every rule a fired event calls for. Pure: the world is asked and changed through the checks.</summary>
    public static class ScenarioPass
    {
        /// <summary>
        ///   Plays, in reading order, every rule whose trigger matches and which is ready at its turn; at the place of a
        ///   group's first rule, one of the group's ready rules drawn by weight. Returns the rules played, in order.
        /// </summary>
        public static IReadOnlyList<ScenarioRule> Play(KeywordToken firedEvent, IEnumerable<ScenarioRule> rules, ScenarioChecks checks)
        {
            var played = new List<ScenarioRule>();
            if (rules == null || checks?.Play == null) return played;

            var matching = new List<ScenarioRule>();
            foreach (ScenarioRule rule in rules)
                if (rule != null && ScenarioSelector.TriggerMatches(rule.Trigger, firedEvent))
                    matching.Add(rule);

            var groupsDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ScenarioRule rule in matching)
            {
                ScenarioRule chosen;
                if (rule.Group == null)
                {
                    chosen = IsReady(rule, checks) ? rule : null;
                }
                else
                {
                    if (!groupsDone.Add(rule.Group)) continue;
                    var ready = new List<ScenarioRule>();
                    foreach (ScenarioRule member in matching)
                        if (string.Equals(member.Group, rule.Group, StringComparison.OrdinalIgnoreCase) && IsReady(member, checks))
                            ready.Add(member);
                    chosen = ScenarioSelector.Draw(ready, checks.NextBelow);
                }

                if (chosen == null) continue;
                checks.Play(chosen);
                played.Add(chosen);
            }

            return played;
        }

        /// <summary>Not a sealed one-shot, every guard holds, and every consequence can apply, all as the world is now.</summary>
        public static bool IsReady(ScenarioRule rule, ScenarioChecks checks)
        {
            if (rule == null) return false;
            if (rule.FireOnce && checks?.Sealed != null && checks.Sealed.Contains(rule.Id)) return false;
            if (!GuardEvaluator.AllPass(rule.Guards, checks?.GuardPasses ?? (_ => true))) return false;

            Func<KeywordToken, bool> applies = checks?.ConsequenceApplies ?? (_ => true);
            foreach (KeywordToken consequence in rule.Consequences)
                if (!applies(consequence))
                    return false;

            return true;
        }
    }
}
