// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Wires triggers → the scripted-scenarios runtime, and persists which one-shots have fired.</summary>
   public sealed class ScenarioBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_Scenarios_v1";
      public static ScenarioBehavior Instance;

      private HashSet<string> _fired = new();

      public ScenarioBehavior()
      {
         Instance = this;
         Runtime = new ScenarioRuntime(_fired);
      }

      /// <summary>The runtime (loaded rules + fired set), exposed for the console diagnostics/test-fire.</summary>
      public ScenarioRuntime Runtime { get; private set; }

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
         CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
      }

      public override void SyncData(IDataStore dataStore)
      {
         string json = null;
         if (dataStore.IsSaving)
            try { json = JsonConvert.SerializeObject(new List<string>(_fired), Formatting.None); }
            catch (Exception ex)
            {
               ModLog.Error($"[SCENARIO] fired-set serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               json = "[]";
            }

         dataStore.SyncData(SaveKey, ref json);

         if (dataStore.IsSaving || string.IsNullOrEmpty(json)) return;
         {
            try
            {
               List<string> list = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
               _fired = new HashSet<string>(list);
               Runtime = new ScenarioRuntime(_fired); // OnSessionLaunched (fired after this) loads the rules
            }
            catch (Exception ex)
            {
               _fired = new HashSet<string>();
               Runtime = new ScenarioRuntime(_fired);
               ModLog.Error($"[SCENARIO] fired-set load failed — scenarios reset for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      #region private

      private static List<IFaction> FactionList(params IFaction[] factions)
      {
         var list = new List<IFaction>();
         foreach (IFaction f in factions)
            if (f != null && !list.Contains(f))
               list.Add(f);

         return list;
      }

      private static List<IFaction> FactionsOf(params Hero[] heroes)
      {
         var list = new List<IFaction>();
         foreach (Hero h in heroes)
         {
            IFaction f = h?.MapFaction;
            if (f != null && !list.Contains(f)) list.Add(f);
         }

         return list;
      }

      private static List<Hero> HeroList(params Hero[] heroes)
      {
         var list = new List<Hero>();
         foreach (Hero h in heroes)
            if (h != null)
               list.Add(h);

         return list;
      }

      private void Fire(KeywordToken firedEvent, ScenarioContext context)
      {
         try { Runtime.Fire(firedEvent, context); }
         catch (Exception ex) { ModLog.Error($"[SCENARIO] fire {firedEvent} failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnHeroesMarried(Hero a, Hero b, bool showNotification)
      {
         Fire(KeywordToken.Parse("ON_MARRIAGE"), new ScenarioContext(HeroList(a, b), FactionsOf(a, b)));
      }

      // ── Trigger feeders ─────────────────────────────────────────────────────

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null) return;
         Fire(KeywordToken.Parse("ON_DEATH"), new ScenarioContext(HeroList(victim, killer), FactionsOf(victim, killer)));
      }

      private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
      {
         Fire(KeywordToken.Parse("ON_PEACE"), new ScenarioContext(null, FactionList(faction1, faction2)));
      }

      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         Runtime.Load(ScenarioPackLoader.LoadAll());
      }

      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
      {
         Fire(KeywordToken.Parse("ON_WAR_DECLARED"), new ScenarioContext(null, FactionList(faction1, faction2)));
      }

      #endregion
   }
}