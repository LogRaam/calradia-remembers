// Code written by Gabriel Mailhot, 24/06/2026.
// Ties the pure core to the engine: on a fired trigger, the pure ScenarioSelector picks the one rule (guards
// resolved against real facts, RNG = MBRandom), then this runtime EXECUTES its consequences, drops its message
// into the World-Event store (told in-game by the existing rumor/chronicle channels), and seals a one-shot.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Holds the loaded rules + the persisted fired-once set, and runs a fired trigger end to end.</summary>
   public sealed class ScenarioRuntime
   {
      private readonly HashSet<string> _fired;
      private List<ScenarioRule> _rules = new();

      // Reentrancy guard: a consequence (e.g. DECLARE_WAR) raises its own campaign event (WarDeclared),
      // which our feeders catch and would route back into Fire — a badly-authored pack could cascade or
      // loop. While a scenario is firing, nested triggers are ignored; the next real event fires normally.
      private bool _isFiring;

      public ScenarioRuntime(HashSet<string> firedRuleIds)
      {
         _fired = firedRuleIds ?? new HashSet<string>();
      }

      /// <summary>The rules currently loaded (for diagnostics).</summary>
      public IReadOnlyList<ScenarioRule> Rules => _rules;

      /// <summary>The ids of one-shot rules that have already fired (persisted by the behavior).</summary>
      public ISet<string> FiredRuleIds => _fired;

      /// <summary>Replaces the loaded rule set (called on session launch from the JSON packs).</summary>
      public void Load(IEnumerable<ScenarioRule> rules)
      {
         _rules = rules != null ? new List<ScenarioRule>(rules) : new List<ScenarioRule>();
      }

      /// <summary>
      ///   Picks the one rule for this event (pure selection: trigger + one-shot + guards, then a weighted
      ///   random draw), applies its consequences, records its message, and seals it if one-shot. Returns the
      ///   rule that fired, or null when none applied.
      /// </summary>
      public ScenarioRule Fire(KeywordToken firedEvent, ScenarioContext context)
      {
         if (_isFiring)
         {
            ModLog.Info($"[SCENARIO] re-entrant trigger {firedEvent} during a firing scenario — skipped (no cascade).");
            return null;
         }

         _isFiring = true;
         try
         {
            ScenarioRule rule = ScenarioSelector.Select(
               firedEvent,
               _rules,
               guard => ScenarioGuardResolver.Resolve(guard, context),
               _fired,
               upper => MBRandom.RandomInt(upper));

            if (rule == null) return null;

            foreach (KeywordToken consequence in rule.Consequences)
               ScenarioConsequenceExecutor.Execute(consequence, context);

            RecordMessage(rule, context);

            if (rule.FireOnce) _fired.Add(rule.Id);
            ModLog.Info($"[SCENARIO] fired '{rule.Id}' on {firedEvent}.");

            return rule;
         }
         finally
         {
            _isFiring = false;
         }
      }

      private static void RecordMessage(ScenarioRule rule, ScenarioContext context)
      {
         if (string.IsNullOrWhiteSpace(rule.Message)) return;

         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return;

         try
         {
            store.Record(new WorldEvent
            {
               Type = WorldEventType.Other,
               Day = (int) CampaignTime.Now.ToDays,
               Magnitude = 60,
               Summary = rule.Message,
               FactionIds = FactionIds(context)
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENARIO] message record failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static List<string> FactionIds(ScenarioContext context)
      {
         var ids = new List<string>();

         foreach (IFaction f in context.Factions)
            if (f?.StringId != null && !ids.Contains(f.StringId))
               ids.Add(f.StringId);

         foreach (Hero h in context.Heroes)
         {
            string id = h?.MapFaction?.StringId;
            if (id != null && !ids.Contains(id)) ids.Add(id);
         }

         return ids;
      }
   }
}
