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
      private IReadOnlyDictionary<string, string> _sources = new Dictionary<string, string>();

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

      /// <summary>Replaces the loaded rule set (called on session launch from the JSON packs), with whose pack each rule came from.</summary>
      public void Load(IEnumerable<ScenarioRule> rules, IReadOnlyDictionary<string, string> sources = null)
      {
         _rules = rules != null ? new List<ScenarioRule>(rules) : new List<ScenarioRule>();
         _sources = sources ?? new Dictionary<string, string>();
      }

      /// <summary>Whose pack a loaded rule came from ("CalradiaRemembers", "Custom" or a mod's id), or "?" when unknown.</summary>
      public string SourceOf(string ruleId)
         => ruleId != null && _sources.TryGetValue(ruleId, out string source) ? source : "?";

      /// <summary>
      ///   Plays every rule this event calls for (ScenarioPass, Gabriel 11/10/2026): in reading order, each ready at its
      ///   turn (not sealed, guards hold, every consequence can apply), one alternative per group; each applies its
      ///   consequences, records its message, and seals if one-shot. Returns the rules played, never null.
      /// </summary>
      public IReadOnlyList<ScenarioRule> Fire(KeywordToken firedEvent, ScenarioContext context)
      {
         if (_isFiring)
         {
            ModLog.Info($"[SCENARIO] re-entrant trigger {firedEvent} during a firing scenario: skipped (no cascade).");
            return new List<ScenarioRule>();
         }

         _isFiring = true;
         try
         {
            return ScenarioPass.Play(firedEvent, _rules, new ScenarioChecks
            {
               Sealed = _fired,
               GuardPasses = guard => ScenarioGuardResolver.Resolve(guard, context),
               ConsequenceApplies = consequence => ScenarioConsequenceExecutor.CanApply(consequence, context),
               NextBelow = upper => MBRandom.RandomInt(upper),
               Play = rule =>
               {
                  foreach (KeywordToken consequence in rule.Consequences)
                     ScenarioConsequenceExecutor.Execute(consequence, context);

                  RecordMessage(rule, context);

                  if (rule.FireOnce) _fired.Add(rule.Id);
                  ModLog.Info($"[SCENARIO] fired '{rule.Id}' ({SourceOf(rule.Id)}) on {firedEvent}.");
               }
            });
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
               Summary = ScenarioRoleText.Fill(rule.Message, context.RoleNames()), // a tale's {husband} named (Gabriel, 12/10/2026)
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
