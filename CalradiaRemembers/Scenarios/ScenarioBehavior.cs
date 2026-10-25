// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

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

      /// <summary>The triggers this behavior feeds; a pack naming any other never fires (checked by cr.testall scenario_packs).</summary>
      public static readonly IReadOnlyCollection<string> KnownTriggers = new[] {"ON_DEATH", "ON_MARRIAGE", "ON_WAR_DECLARED", "ON_PEACE", "ON_TALE", "ON_CAPTIVE_NIGHT"};

      /// <summary>The hour of the night ON_CAPTIVE_NIGHT is fired at, once a night.</summary>
      public const int CaptiveNightHour = 23;

      // The day ON_CAPTIVE_NIGHT last fired (transient: a load fires that night again at most).
      private int _captiveNightDay = -1;

      /// <summary>
      ///   A tale reached a stage, or an ending ("end:&lt;id&gt;"): ON_TALE:&lt;tale&gt;:&lt;where&gt;, carrying the tale's roles
      ///   (the tale-scenario bridge, Gabriel 12/10/2026: the player leaves with the queen, and her husband's realm
      ///   answers). The giver is the event's subject; every bound hero is among its heroes.
      /// </summary>
      public void FireTale(string taleId, string where, IReadOnlyDictionary<string, string> roles)
      {
         if (string.IsNullOrWhiteSpace(taleId) || string.IsNullOrWhiteSpace(where)) return;

         Fire(KeywordToken.Parse($"ON_TALE:{taleId}:{where}"), ForTale(roles));
      }

      /// <summary>What a tale's event is about, exactly as FireTale hands it to the rules (shared with cr.testall).</summary>
      public static ScenarioContext ForTale(IReadOnlyDictionary<string, string> roles)
      {
         roles ??= new Dictionary<string, string>();
         var heroes = new List<Hero>();
         Hero giver = null;
         foreach (KeyValuePair<string, string> r in roles)
         {
            Hero h = Hero.FindFirst(x => x != null && x.StringId == r.Value);
            if (h == null || heroes.Contains(h)) continue;
            if (r.Key == "giver") giver = h;
            heroes.Add(h);
         }

         if (giver != null)
         {
            heroes.Remove(giver);
            heroes.Insert(0, giver);
         }

         return new ScenarioContext(heroes, FactionsOf(heroes.ToArray()), giver != null ? new List<Hero> {giver} : heroes, roles);
      }

      /// <summary>
      ///   What a death is about, exactly as the feeder hands it to the rules: the one who died first, then the one who
      ///   killed them; only the one who died is its subject (IF_HERO_IS), so a rule on a lord's death never answers a
      ///   death they dealt. Shared with cr.testall so the self-test asks the rules what a real death asks them.
      /// </summary>
      public static ScenarioContext ForDeath(Hero victim, Hero killer)
         => new(HeroList(victim, killer), FactionsOf(victim, killer), HeroList(victim));

      /// <summary>What a marriage is about, exactly as the feeder hands it to the rules: the two who wed.</summary>
      public static ScenarioContext ForMarriage(Hero a, Hero b)
         => new(HeroList(a, b), FactionsOf(a, b));

      /// <summary>The runtime (loaded rules + fired set), exposed for the console diagnostics/test-fire.</summary>
      public ScenarioRuntime Runtime { get; private set; }

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
         CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
      }

      /// <summary>
      ///   What a night in captivity is about (Gabriel, 24/10/2026: the night Rhagaea sends for her prisoner): the player, held
      ///   by the realm that holds him. Shared with cr.testall.
      /// </summary>
      public static ScenarioContext ForCaptiveNight()
      {
         PartyBase holder = Hero.MainHero?.PartyBelongedToAsPrisoner;

         return new ScenarioContext(HeroList(Hero.MainHero), FactionList(holder?.MapFaction));
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
         Fire(KeywordToken.Parse("ON_MARRIAGE"), ForMarriage(a, b));
      }

      // ── Trigger feeders ─────────────────────────────────────────────────────

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null) return;
         Fire(KeywordToken.Parse("ON_DEATH"), ForDeath(victim, killer));
      }

      // ON_CAPTIVE_NIGHT: once a night, at the hour the castle sleeps, while the player is a prisoner.
      private void OnHourlyTick()
      {
         // From the hour on, not at it exactly: an hourly tick may read 22.9999, and the day guards it to once a night.
         if (Hero.MainHero?.IsPrisoner != true || !CampaignTime.Now.IsNightTime || CampaignTime.Now.CurrentHourInDay < CaptiveNightHour) return;

         var today = (int) CampaignTime.Now.ToDays;
         if (today == _captiveNightDay) return;
         _captiveNightDay = today;
         Fire(KeywordToken.Parse("ON_CAPTIVE_NIGHT"), ForCaptiveNight());
      }

      private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
      {
         Fire(KeywordToken.Parse("ON_PEACE"), new ScenarioContext(null, FactionList(faction1, faction2)));
      }

      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         ScenarioMergeResult packs = ScenarioPackLoader.LoadAll();
         Runtime.Load(packs.Rules, packs.SourceOf);
      }

      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
      {
         Fire(KeywordToken.Parse("ON_WAR_DECLARED"), new ScenarioContext(null, FactionList(faction1, faction2)));
      }

      #endregion
   }
}