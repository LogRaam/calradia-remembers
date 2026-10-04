// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1, the game side (docs/design/AUTHORED_QUESTS.md). Loads the quest files, offers a quest
// through a character who qualifies, plays the one active run on the facts the game reports, keeps the journal entry
// ("Tale:"), and carries out rewards and failures. The rules live in Logic.AuthoredQuests (pure, tested); this only
// reads the game and applies what they decide. Gabriel's rulings: one at a time, unique once per campaign, any
// character may offer one, a canvas the model rephrases, the game advances the quest and never the model.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using CalradiaRemembers.Travel;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>The authored quests in play: one active run at most, its journal entry, its rewards.</summary>
   public sealed class AuthoredQuestBehavior : CampaignBehaviorBase
   {
      private const string RunKey = "CalradiaRemembers_AuthoredQuestRun_v1";
      private const string DoneKey = "CalradiaRemembers_AuthoredQuestsDone_v1";
      private const string DeclinedKey = "CalradiaRemembers_AuthoredQuestsDeclined_v1";

      // Added after the first saves (additive): the day each quest was last played to its end, for repeat_cooldown_days.
      // An older save has none, so a repeatable quest it already played may come back at once: harmless.
      private const string PlayedOnKey = "CalradiaRemembers_AuthoredQuestsPlayedOn_v1";

      private Dictionary<string, AuthoredQuest> _quests = new(StringComparer.Ordinal);
      private List<string> _problems = new();
      private AuthoredQuestRun _run;
      private HashSet<string> _done = new(StringComparer.Ordinal);
      private Dictionary<string, int> _declined = new(StringComparer.Ordinal);
      private Dictionary<string, int> _playedOn = new(StringComparer.Ordinal);

      // The offer being made in the conversation now open (transient: a conversation does not survive a save).
      private AuthoredQuestRun _offer;
      private string _offerHeroId;

      public AuthoredQuestBehavior() => Instance = this;

      public static AuthoredQuestBehavior Instance { get; private set; }

      /// <summary>Every problem found in the quest files at load (cr.story_check).</summary>
      public IReadOnlyList<string> Problems => _problems;

      public IReadOnlyCollection<AuthoredQuest> Quests => _quests.Values;
      public AuthoredQuestRun ActiveRun => _run;

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => SafeRun.Step("AuthoredQuests.Load", OnSessionLaunched));
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("AuthoredQuests.Daily", OnDailyTick));
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("AuthoredQuests.Hourly", OnHourlyTick));
         // The conversation events are static and this behaviour is made anew for each campaign: hook once, and
         // forward to whichever instance is current, so a second loaded campaign does not answer twice.
         if (_hooked) return;
         _hooked = true;
         CrConversation.Started += info => Instance?.OnConversationStarted(info);
         CrConversation.Ended += log => Instance?.OnConversationEnded(log);
      }

      private static bool _hooked;

      public override void SyncData(IDataStore dataStore)
      {
         string run = _run?.Serialize() ?? "";
         string done = string.Join("\n", _done);
         string declined = DayList(_declined);
         string playedOn = DayList(_playedOn);
         dataStore.SyncData(RunKey, ref run);
         dataStore.SyncData(DoneKey, ref done);
         dataStore.SyncData(DeclinedKey, ref declined);
         dataStore.SyncData(PlayedOnKey, ref playedOn);

         if (!dataStore.IsLoading) return;

         _run = AuthoredQuestRun.Deserialize(run);
         _done = new HashSet<string>((done ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
         _declined = ReadDayList(declined);
         _playedOn = ReadDayList(playedOn);
      }

      private static string DayList(Dictionary<string, int> days) => string.Join("\n", days.Select(kv => kv.Key + "=" + kv.Value));

      private static Dictionary<string, int> ReadDayList(string text)
      {
         var days = new Dictionary<string, int>(StringComparer.Ordinal);
         foreach (string line in (text ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            int eq = line.LastIndexOf('=');
            if (eq > 0 && int.TryParse(line.Substring(eq + 1), out int day)) days[line.Substring(0, eq)] = day;
         }

         return days;
      }

      // ── Loading ──────────────────────────────────────────────────────────

      private void OnSessionLaunched()
      {
         LoadFiles();
         ReconcileJournal();
      }

      /// <summary>Reads ModuleData/Quests and its Custom folder (players' and modders' own, kept across updates).</summary>
      internal void LoadFiles()
      {
         string root = Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "ModuleData", "Quests");
         var files = new List<(string, string)>();
         foreach (string dir in new[] {root, Path.Combine(root, "Custom")})
            if (Directory.Exists(dir))
               foreach (string f in Directory.GetFiles(dir, "*.json"))
                  files.Add((Path.GetFileName(f), File.ReadAllText(f)));

         AuthoredQuestLoadResult r = AuthoredQuestLoader.ParseAll(files);
         _quests = r.Quests.ToDictionary(q => q.Id, StringComparer.Ordinal);
         _problems = r.Problems;

         ModLog.Info($"[TALE] {_quests.Count} authored quest(s) loaded from {files.Count} file(s); {_problems.Count} problem(s).");
         foreach (string p in _problems) ModLog.Info("[TALE] " + p);
         if (_problems.Count > 0)
            ModLog.Notice($"{_problems.Count} problem(s) in the quest files (ModuleData/Quests). cr.story_check lists them.");
      }

      /// <summary>A journal entry with no run behind it (the run ended, or a save from elsewhere) is closed.</summary>
      private void ReconcileJournal()
      {
         foreach (AuthoredStoryQuest q in Campaign.Current?.QuestManager?.Quests?.OfType<AuthoredStoryQuest>().Where(q => q.IsOngoing).ToList()
                                          ?? new List<AuthoredStoryQuest>())
            if (_run == null || _run.Status != AuthoredQuestStatus.Active || q.StoryId != _run.QuestId)
               q.CloseAsCancelled("This tale has ended.");
      }

      // ── Offering ─────────────────────────────────────────────────────────

      private void OnConversationStarted(CrConversationInfo info)
      {
         SafeRun.Step("AuthoredQuests.ConversationStarted", () =>
         {
            Hero hero = Find(info?.NpcId);
            if (hero == null) return;
            var today = (int) CampaignTime.Now.ToDays;

            if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
            {
               Apply(q, AuthoredQuestEngine.ConversationOpened(_run, q, hero.StringId, LocalOf(hero), today));

               return;
            }

            // MCM "Authored Quests": off, no new tale is offered (one under way goes on).
            if (ModSettings.AuthoredQuests) TryOffer(hero, today, MBRandom.RandomFloat);
         });
      }

      /// <summary>Considers every quest this character could give; the first that qualifies and passes the offer rule is raised.</summary>
      internal bool TryOffer(Hero hero, int today, double roll, string onlyQuestId = null) => TryOffer(hero, today, roll, onlyQuestId, out _);

      /// <summary>As TryOffer, saying why not when it is no: the first condition that failed, in plain words.</summary>
      internal bool TryOffer(Hero hero, int today, double roll, string onlyQuestId, out string whyNot)
      {
         _offer = null;
         _offerHeroId = null;
         whyNot = onlyQuestId == null ? "no quest qualifies" : $"there is no quest '{onlyQuestId}'";

         // Every quest this giver could raise now; the file weights choose among them, then one roll decides.
         var open = new List<(AuthoredQuest q, Dictionary<string, string> ids, Dictionary<string, string> names)>();
         foreach (AuthoredQuest q in _quests.Values.Where(q => onlyQuestId == null || q.Id == onlyQuestId))
         {
            if (!TryBind(q, hero, out Dictionary<string, string> ids, out Dictionary<string, string> names, out whyNot)) continue;
            whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, today));
            if (whyNot == null) open.Add((q, ids, names));
         }

         if (open.Count == 0) return false;
         int pick = AuthoredQuestOfferPolicy.PickByWeight(open.Select(o => o.q.Weight).ToArray(), open.Count == 1 ? 0 : MBRandom.RandomFloat);
         if (pick < 0)
         {
            whyNot = "every quest it could give has a weight of 0";

            return false;
         }

         if (roll >= AuthoredQuestOfferPolicy.OfferChance)
         {
            whyNot = "the dice said no this time";

            return false;
         }

         (AuthoredQuest chosen, Dictionary<string, string> chosenIds, Dictionary<string, string> chosenNames) = open[pick];
         _offer = AuthoredQuestEngine.Start(chosen, chosenIds, chosenNames, today);
         _offerHeroId = hero.StringId;
         ModLog.Info($"[TALE] {hero.Name} raises '{chosen.Id}' with the player ({string.Join(", ", chosenNames.Select(kv => kv.Key + "=" + kv.Value))}).");

         return true;
      }

      /// <summary>The offer facts for this quest now, the dice aside.</summary>
      private AuthoredOfferFacts FactsFor(AuthoredQuest q, int today) => new() {
         Today = today, StoryUnderWay = _run is {Status: AuthoredQuestStatus.Active}, Unique = q.Unique, AlreadyPlayed = _done.Contains(q.Id),
         LastPlayedOnDay = _playedOn.TryGetValue(q.Id, out int p) ? p : (int?) null, RepeatCooldownDays = q.RepeatCooldownDays,
         DeclinedOnDay = _declined.TryGetValue(q.Id, out int d) ? d : (int?) null, Chance = AuthoredQuestOfferPolicy.OfferChance
      };

      /// <summary>
      ///   For authors (cr.story_check name): could this character give this quest now, the dice aside? Nothing is
      ///   offered or changed; the answer names what it would bind, or the first condition that failed.
      /// </summary>
      internal string Eligibility(Hero hero, AuthoredQuest q)
      {
         if (!TryBind(q, hero, out _, out Dictionary<string, string> names, out string whyNot)) return "no: " + whyNot;
         whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, (int) CampaignTime.Now.ToDays));
         if (whyNot != null) return "not now: " + whyNot;

         return $"yes, on a {AuthoredQuestOfferPolicy.OfferChance:P0} chance per conversation ({string.Join(", ", names.Select(kv => kv.Key + "=" + kv.Value))})";
      }

      /// <summary>
      ///   Binds the quest's roles around this giver, or false when they do not qualify. Increment 1 reads the giver
      ///   (role, regard on the mod's ledger, not at war) and one village (held by the giver's house, by a forest when
      ///   asked).
      /// </summary>
      private static bool TryBind(AuthoredQuest q, Hero giver, out Dictionary<string, string> ids, out Dictionary<string, string> names, out string whyNot)
      {
         ids = new Dictionary<string, string>();
         names = new Dictionary<string, string>();
         whyNot = null;
         if (giver == null || giver == Hero.MainHero || !Mapping.ActionGate.Allows(giver, Logic.ActionKind.Witness))
         {
            whyNot = "they cannot take part right now (not free, not of age, or not in the world)";

            return false;
         }

         GiverRequirement g = q.Requires?.Giver ?? new GiverRequirement();
         bool roleOk = g.Role switch {
            "lord" => giver.IsLord,
            "notable" => giver.IsNotable,
            "gang_leader" => giver.IsGangLeader,
            _ => true
         };
         if (!roleOk)
         {
            whyNot = $"the quest wants a giver who is a {g.Role}";

            return false;
         }

         if (g.NotAtWarWithPlayer && giver.MapFaction != null && Hero.MainHero?.MapFaction != null
             && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, Hero.MainHero.MapFaction))
         {
            whyNot = "their realm is at war with you";

            return false;
         }

         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(giver.StringId)?.ReputationWithPlayer ?? 0;
         if (regard < g.MinRegard)
         {
            whyNot = $"their regard for you is {regard}, and the quest needs at least {g.MinRegard}";

            return false;
         }

         int tier = Clan.PlayerClan?.Tier ?? 0, minTier = q.Requires?.Player?.MinClanTier ?? 0;
         if (tier < minTier)
         {
            whyNot = $"your clan is tier {tier}, and the quest needs tier {minTier}";

            return false;
         }

         ids["giver"] = giver.StringId;
         names["giver"] = giver.Name?.ToString();

         PlaceRequirement v = q.Requires?.Village;
         if (v != null)
         {
            IEnumerable<Settlement> candidates = g.Holds == "{village}"
               ? giver.Clan?.Settlements?.Where(s => s.IsVillage) ?? Enumerable.Empty<Settlement>()
               : new[] {giver.HomeSettlement}.Where(s => s != null && s.IsVillage);
            List<Settlement> owned = candidates.ToList();
            Settlement village = owned.FirstOrDefault(s => v.Near != "forest" || NearForest(s));
            if (village == null)
            {
               whyNot = owned.Count == 0 ? "no village is theirs to give the quest about"
                  : $"none of their villages ({string.Join(", ", owned.Select(s => s.Name))}) lies by a forest";

               return false;
            }

            ids["village"] = village.StringId;
            names["village"] = village.Name?.ToString();
         }

         return true;
      }

      private static bool NearForest(Settlement s)
      {
         try { return Campaign.Current.MapSceneWrapper.GetEnvironmentTerrainTypes(s.Position).Contains(TerrainType.Forest); }
         catch { return false; }
      }

      private void OnConversationEnded(CrConversationLog log)
      {
         SafeRun.Step("AuthoredQuests.ConversationEnded", () =>
         {
            AuthoredQuestEngine.ConversationClosed(_run, log?.NpcId);
            // A scene that came due while another conversation was open opens now that it has ended.
            if (_sceneWaiting && _run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest waiting))
               MainThreadDispatcher.Enqueue(() => OpenScene(waiting));
            if (_offer == null || _offerHeroId != log?.NpcId) return;

            AuthoredQuestRun offer = _offer;
            _offer = null;
            _offerHeroId = null;
            if (!_quests.TryGetValue(offer.QuestId, out AuthoredQuest q)) return;

            AskToAccept(q, offer);
         });
      }

      /// <summary>The player answers in a plain choice: the story starts only on a yes they gave themselves.</summary>
      private void AskToAccept(AuthoredQuest q, AuthoredQuestRun offer)
      {
         string giver = offer.BoundNames.TryGetValue("giver", out string n) ? n : "Someone";
         InformationManager.ShowInquiry(new InquiryData(
            q.Title,
            $"{giver} has asked for your help: {AuthoredQuestEngine.Fill(offer, q.Stages[0].Journal)}\n\nWill you take it on?",
            true, true, "Take it on", "Not now",
            () => Accept(q, offer),
            () =>
            {
               _declined[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] the player declined '{q.Id}'.");
            }));
      }

      internal void Accept(AuthoredQuest q, AuthoredQuestRun offer)
      {
         _run = offer;
         Hero giver = Find(AuthoredQuestEngine.Bound(_run, "{giver}"));
         var today = (int) CampaignTime.Now.ToDays;
         AuthoredStep step = AuthoredQuestEngine.Accept(_run, q, today);

         var journal = new AuthoredStoryQuest(q.Id, q.Title, giver, AuthoredQuestEngine.Fill(_run, q.Stages[0].Journal));
         ModLog.Info($"[TALE] '{q.Id}' accepted; stage {_run.StageId}.");
         Apply(q, step, journal);
      }

      // ── Playing ──────────────────────────────────────────────────────────

      private void OnDailyTick()
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;
         Apply(q, AuthoredQuestEngine.Day(_run, q, (int) CampaignTime.Now.ToDays));
      }

      /// <summary>
      ///   Hourly: arriving at a be_at place, and keeping watch (Gabriel: nights spent waiting, no moon). A night counts
      ///   once, read deep in it (an hour past midnight) with the player's party within the stage's radius.
      /// </summary>
      private void OnHourlyTick()
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;
         AuthoredStage s = AuthoredQuestEngine.Current(_run, q);
         var today = (int) CampaignTime.Now.ToDays;

         switch (s?.Objective.Type)
         {
            case "defeat_party":
               EnsureBand(q, s);
               break;
            case "be_at":
               if (Near(s.Objective.Place, s.Objective.Radius)) Apply(q, AuthoredQuestEngine.Arrived(_run, q, AuthoredQuestEngine.Bound(_run, s.Objective.Place), today));
               break;
            case "keep_watch":
               if (!CampaignTime.Now.IsNightTime || (int) CampaignTime.Now.CurrentHourInDay != 1) return;
               AuthoredStep night = AuthoredQuestEngine.NightPassed(_run, q, Near(s.Objective.Place, s.Objective.Radius), today);
               Apply(q, night);
               if (night.Kind == AuthoredStepKind.OpenScene) MainThreadDispatcher.Enqueue(() => OpenScene(q));
               break;
         }
      }

      // ── The quest's band ─────────────────────────────────────────────────

      private const string BandRole = "tale_band";

      /// <summary>
      ///   The band a defeat_party stage hunts, on the map near its place: spawned when the stage begins, again after a
      ///   load (a transient party is never saved), and again if others destroyed it (the survivors regroup, and the
      ///   deed stays the player's to do). A band warned in the scene (alert) is larger by a third.
      /// </summary>
      private void EnsureBand(AuthoredQuest q, AuthoredStage s)
      {
         AuthoredSpawn spawn = s.Objective.Spawn;
         if (spawn == null) return;
         MobileParty live = TransientPartyRegistry.Instance?.WithTag(BandRole, q.Id);
         if (live != null && live.IsActive)
         {
            _run.SpawnedPartyId = live.StringId;

            return;
         }

         Settlement near = Settlement.Find(AuthoredQuestEngine.Bound(_run, spawn.Near) ?? "");
         if (near == null) return;

         int lo = spawn.Size?.Length > 0 ? spawn.Size[0] : 10, hi = spawn.Size?.Length > 1 ? spawn.Size[1] : lo;
         int size = MBRandom.RandomInt(lo, hi + 1);
         if (_run.BandAlerted) size += size / 3;

         Vec2 at = near.GetPosition2D + new Vec2(MBRandom.RandomFloatRanged(-3f, 3f), MBRandom.RandomFloatRanged(-3f, 3f));
         string name = AuthoredQuestEngine.Fill(_run, spawn.Name) ?? "Brigands";
         MobileParty band = SpawnBand(at, near, name, spawn.Template, size);
         if (band == null)
         {
            ModLog.Error($"[TALE] '{q.Id}': the band could not be spawned near {near.Name}.");

            return;
         }

         _run.SpawnedPartyId = band.StringId;
         TransientPartyRegistry.Instance?.Track(band, BandRole, q.Id,
            onDestroyed: (party, by) => OnBandDestroyed(q, party, by),
            onRestore: pos => SpawnBand(pos, near, name, spawn.Template, size));
         ModLog.Info($"[TALE] '{q.Id}': {name} ({size}) near {near.Name}.");
      }

      /// <summary>A bandit-owned band of the template's troops, patrolling around its place.</summary>
      private static MobileParty SpawnBand(Vec2 at, Settlement home, string name, string template, int size)
      {
         try
         {
            Clan bandits = Clan.BanditFactions?.FirstOrDefault(c => c.StringId == (template ?? "looters")) ?? Clan.BanditFactions?.FirstOrDefault(c => c.StringId == "looters");
            CharacterObject troop = CharacterObject.Find(template == "looters" || template == null ? "looter" : template) ?? CharacterObject.Find("looter");
            if (bandits == null || troop == null) return null;

            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(troop, Math.Max(1, size));
            MobileParty band = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
               new CampaignVec2(at, true), 1f, home, new TaleWorlds.Localization.TextObject(name), bandits, roster, TroopRoster.CreateDummyTroopRoster(), null);
            if (band == null) return null;

            var grain = TaleWorlds.Core.Game.Current.ObjectManager.GetObject<TaleWorlds.Core.ItemObject>("grain");
            if (grain != null) band.ItemRoster.AddToCounts(grain, 10);
            band.SetMovePatrolAroundSettlement(home, MobileParty.NavigationType.Default, false);
            band.Ai.SetDoNotMakeNewDecisions(true);

            return band;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TALE] band spawn failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The band was destroyed: by the player, the stage is done; by anyone else, it regroups on the next hour.</summary>
      private void OnBandDestroyed(AuthoredQuest q, MobileParty band, PartyBase by)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || band?.StringId != _run.SpawnedPartyId) return;

         if (by == PartyBase.MainParty || by?.MobileParty?.Army != null && by.MobileParty.Army.LeaderParty == MobileParty.MainParty)
         {
            ModLog.Info($"[TALE] '{q.Id}': the player destroyed {band.Name}.");
            Apply(q, AuthoredQuestEngine.PartyDefeated(_run, q, band.StringId, (int) CampaignTime.Now.ToDays));

            return;
         }

         ModLog.Info($"[TALE] '{q.Id}': {band.Name} was destroyed by {by?.Name}; the survivors will regroup.");
         _run.SpawnedPartyId = null;
      }

      /// <summary>Takes the band off the map when the tale ends, however it ends.</summary>
      private void ReleaseBand(string questId)
      {
         MobileParty band = TransientPartyRegistry.Instance?.WithTag(BandRole, questId);
         if (band != null) TransientPartyRegistry.Instance.Release(band);
      }

      private bool Near(string placeRole, float radius)
      {
         Settlement s = Settlement.Find(AuthoredQuestEngine.Bound(_run, placeRole) ?? "");

         return s != null && MobileParty.MainParty != null && MobileParty.MainParty.GetPosition2D.Distance(s.GetPosition2D) <= Math.Max(1f, radius);
      }

      // ── The exploration scene ────────────────────────────────────────────

      /// <summary>Opens the current explore stage as a scene in the chat: a narrator, the file's findings, dangers and people.</summary>
      internal void OpenScene(AuthoredQuest q)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || AuthoredQuestEngine.Current(_run, q)?.Objective.Type != "explore") return;
         if (ChatManager.IsOpen)
         {
            ModLog.Notice("a scene of your tale waits; it will open when the conversation ends.");
            _sceneWaiting = true;

            return;
         }

         _sceneWaiting = false;
         string place = _run.BoundNames.TryGetValue("village", out string v) ? v : null;
         var lostNight = false;
         ChatManager.OpenForExploration(new ExplorationSession {
            Title = place != null ? $"The wood by {place}" : q.Title,
            BuildPrompt = () => AuthoredQuestPrompt.Narrator(_run, q),
            Apply = markers => ApplyMarkers(q, markers, () => lostNight = true),
            IsOver = () => lostNight,
            OnClose = () => CloseScene(q)
         });
         ModLog.Info($"[TALE] '{q.Id}': the scene opens.");
      }

      private bool _sceneWaiting;

      /// <summary>What the narration's markers do: findings recorded, a hazard's effect carried out, someone met.</summary>
      private IReadOnlyList<string> ApplyMarkers(AuthoredQuest q, SceneMarkers m, Action loseTheNight)
      {
         var notes = new List<string>();
         foreach (string f in m.Found)
            if (AuthoredQuestEngine.Found(_run, q, f)) ModLog.Info($"[TALE] finding '{f}'.");

         foreach (string h in m.Hazards)
         {
            AuthoredHazardEffect e = AuthoredQuestEngine.Sprung(_run, q, h);
            if (e == null) continue;
            ModLog.Info($"[TALE] hazard '{h}' ({e.Type}).");
            switch (e.Type)
            {
               case "injure":
                  int lo = e.HitPoints?.Length > 0 ? e.HitPoints[0] : 10, hi = e.HitPoints?.Length > 1 ? e.HitPoints[1] : lo;
                  int hurt = MBRandom.RandomInt(lo, hi + 1);
                  Hero me = Hero.MainHero;
                  int before = me.HitPoints;
                  me.HitPoints = Math.Max(1, before - hurt);
                  notes.Add($"You are hurt ({before - me.HitPoints} hit points).");
                  break;
               case "alert":
                  _run.BandAlerted = true;
                  notes.Add("You have been noticed. Whoever is out there knows someone is looking.");
                  break;
               case "lose_time":
                  loseTheNight();
                  notes.Add("The night is lost; dawn finds you no further along.");
                  break;
            }
         }

         foreach (string id in m.Met)
            if (AuthoredQuestEngine.Meet(_run, q, id))
            {
               string who = AuthoredQuestEngine.Current(_run, q)?.Objective.Scene?.Encounters.FirstOrDefault(x => x.Id == id)?.Name;
               notes.Add($"{AuthoredQuestEngine.Fill(_run, who) ?? "Someone"} is with you now.");
            }

         return notes;
      }

      /// <summary>
      ///   The scene closed: the required findings never reached are said now, from the file (Gabriel: a clue is missed
      ///   only in its manner), and the stage that follows begins.
      /// </summary>
      private void CloseScene(AuthoredQuest q)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active}) return;
         AuthoredScene scene = AuthoredQuestEngine.Current(_run, q)?.Objective.Scene;
         IReadOnlyList<AuthoredFinding> missed = AuthoredQuestEngine.SceneClosed(_run, q, (int) CampaignTime.Now.ToDays, out AuthoredStep step);
         if (step.Kind == AuthoredStepKind.None) return;

         var text = new List<string>();
         foreach (AuthoredFinding f in missed) text.Add(AuthoredQuestEngine.Fill(_run, f.Text));
         if (!string.IsNullOrWhiteSpace(scene?.Closing)) text.Add(AuthoredQuestEngine.Fill(_run, scene.Closing));
         if (text.Count > 0)
         {
            string body = string.Join(Environment.NewLine + Environment.NewLine, text);
            MainThreadDispatcher.Enqueue(() => InformationManager.ShowInquiry(new InquiryData(q.Title, body, true, false, "Go on", "", null, null)));
         }

         Apply(q, step);
      }

      /// <summary>Carries out what a step decided: the journal line of the next stage, the reward, or the failure.</summary>
      internal void Apply(AuthoredQuest q, AuthoredStep step, AuthoredStoryQuest journal = null)
      {
         if (step == null || step.Kind == AuthoredStepKind.None) return;
         journal ??= Journal(q.Id);

         switch (step.Kind)
         {
            case AuthoredStepKind.Advanced:
            case AuthoredStepKind.OpenScene:
               AuthoredStage next = AuthoredQuestEngine.Current(_run, q);
               journal?.Log(AuthoredQuestEngine.Fill(_run, next?.Journal));
               ModLog.Info($"[TALE] '{q.Id}': {step.FromStage} -> {step.ToStage}.");

               break;
            case AuthoredStepKind.Completed:
               ReleaseBand(q.Id);
               CarryOut(q, q.Reward);
               journal?.CloseAsCompleted();
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' completed.");

               break;
            case AuthoredStepKind.Failed:
               ReleaseBand(q.Id);
               CarryOut(q, q.Failure);
               journal?.CloseAsFailed("The time to act has passed.");
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' failed at stage {step.FromStage}.");

               break;
         }
      }

      /// <summary>Regard and a memory for the giver, then the outcome's consequences (memory, world_event in increment 1).</summary>
      private void CarryOut(AuthoredQuest q, AuthoredOutcome outcome)
      {
         if (outcome == null) return;
         Hero giver = Find(AuthoredQuestEngine.Bound(_run, "{giver}"));

         if (giver != null)
         {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(giver.StringId);
            if (profile != null)
            {
               ProfileMutator.ApplyReputationDelta(profile, outcome.Regard, NpcMemoryService.Core.Standing.RegardCause.Quest);
               store.Set(profile);
            }
         }

         if (giver != null && !string.IsNullOrWhiteSpace(outcome.Memory)) Remember(giver.StringId, AuthoredQuestEngine.Fill(_run, outcome.Memory));
         if (outcome.Gold > 0) TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, outcome.Gold);

         foreach (AuthoredConsequence c in outcome.Consequences ?? new List<AuthoredConsequence>())
         {
            try
            {
               if (c.Type == "memory")
                  foreach (string id in Who(c.Who)) Remember(id, AuthoredQuestEngine.Fill(_run, c.Text));
               else if (c.Type == "world_event")
                  WorldEventStore.Instance?.Record(new WorldEvent {
                     Type = WorldEventType.Other, Day = (int) CampaignTime.Now.ToDays, Magnitude = c.Magnitude,
                     Summary = AuthoredQuestEngine.Fill(_run, c.Summary), PlaceId = AuthoredQuestEngine.Bound(_run, c.Place),
                     ActorIds = giver != null ? new List<string> {giver.StringId} : new List<string>(), InvolvesPlayer = true
                  });
            }
            catch (Exception ex) { ModLog.Error($"[TALE] consequence '{c?.Type}' of '{q.Id}' failed: {ex.GetType().Name}: {ex.Message}"); }
         }
      }

      /// <summary>The heroes a consequence names: a role, or "locals_of:{place}" (the notables of a bound place).</summary>
      private IEnumerable<string> Who(string who)
      {
         if (string.IsNullOrEmpty(who)) return Enumerable.Empty<string>();
         if (who.StartsWith("locals_of:"))
         {
            Settlement s = Settlement.Find(AuthoredQuestEngine.Bound(_run, who.Substring("locals_of:".Length)) ?? "");

            return s?.Notables?.Where(n => n != null).Select(n => n.StringId).ToList() ?? new List<string>();
         }

         string id = AuthoredQuestEngine.Bound(_run, who);

         return id != null ? new[] {id} : Enumerable.Empty<string>();
      }

      private static void Remember(string heroId, string line)
         => CrNpcMemory.AddMemory(new CrMemoryEntry {NpcId = heroId, Summary = line, Kind = "Other", DaysAgo = 0, SourceMod = "CalradiaRemembers"});

      // ── Prompt ───────────────────────────────────────────────────────────

      /// <summary>The authored quest block for this character's prompt, or null.</summary>
      public string PromptBlockFor(Hero hero)
      {
         if (hero == null) return null;
         try
         {
            if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
               return AuthoredQuestPrompt.ForCharacter(_run, q, hero.StringId, LocalOf(hero), offering: false);

            if (_offer != null && _offerHeroId == hero.StringId && _quests.TryGetValue(_offer.QuestId, out AuthoredQuest o))
               return AuthoredQuestPrompt.ForCharacter(_offer, o, hero.StringId, null, offering: true);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] prompt block failed: {ex.GetType().Name}: {ex.Message}"); }

         return null;
      }

      // ── Helpers ──────────────────────────────────────────────────────────

      /// <summary>The place a notable belongs to (their home village or town); null for anyone else.</summary>
      private static string LocalOf(Hero h) => h is {IsNotable: true} ? (h.HomeSettlement ?? h.CurrentSettlement)?.StringId : null;

      private static Hero Find(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);

      private static AuthoredStoryQuest Journal(string questId)
         => Campaign.Current?.QuestManager?.Quests?.OfType<AuthoredStoryQuest>().FirstOrDefault(q => q.IsOngoing && q.StoryId == questId);

      // ── Console and cr.testall seams ─────────────────────────────────────

      /// <summary>Ends the active run without reward or penalty (cr.story_abandon, and self-test cleanup).</summary>
      internal void Abandon(string reason)
      {
         _offer = null;
         _offerHeroId = null;
         if (_run == null) return;
         ReleaseBand(_run.QuestId);
         Journal(_run.QuestId)?.CloseAsCancelled(reason);
         ModLog.Info($"[TALE] '{_run.QuestId}' abandoned: {reason}");
         _run = null;
      }

      /// <summary>Self-test only: forgets that a quest was played or declined, so a test leaves no trace.</summary>
      internal void ForgetPlayed(string questId)
      {
         _done.Remove(questId);
         _declined.Remove(questId);
         _playedOn.Remove(questId);
      }

      internal AuthoredQuestRun Offer => _offer;

      /// <summary>Self-test seam: a CR conversation opened with this hero, as CrConversation.Started reports it.</summary>
      internal void DebugConversationOpened(Hero hero)
      {
         if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
            Apply(q, AuthoredQuestEngine.ConversationOpened(_run, q, hero.StringId, LocalOf(hero), (int) CampaignTime.Now.ToDays));
      }

      /// <summary>Self-test seam: the scene's markers, as a narration would carry them, through the real handling.</summary>
      internal IReadOnlyList<string> DebugSceneMarkers(string narration)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return new List<string>();

         return ApplyMarkers(q, SceneMarkers.Extract(narration), () => { });
      }

      /// <summary>Self-test seam: the band for the current defeat_party stage, spawned now; null if none could be.</summary>
      internal MobileParty DebugEnsureBand()
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return null;
         AuthoredStage s = AuthoredQuestEngine.Current(_run, q);
         if (s?.Objective.Type != "defeat_party") return null;
         EnsureBand(q, s);

         return TransientPartyRegistry.Instance?.WithTag(BandRole, q.Id);
      }

      /// <summary>Self-test seam: the band destroyed by this party, through the real handling.</summary>
      internal void DebugBandDestroyed(MobileParty band, PartyBase by)
      {
         if (_run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) OnBandDestroyed(q, band, by);
      }

      /// <summary>Self-test seam: the scene closes, through the real handling (no window was opened).</summary>
      internal void DebugCloseScene()
      {
         if (_run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) CloseScene(q);
      }

      /// <summary>cr.story_step: completes the current stage as though its objective were met (a test lever).</summary>
      internal string DebugStep()
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return "No tale is under way.";
         AuthoredStage s = AuthoredQuestEngine.Current(_run, q);
         var today = (int) CampaignTime.Now.ToDays;
         AuthoredStep step = s.Objective.Type == "explore"
            ? StepAfterScene(q, today)
            : AuthoredQuestEngine.ForceAdvance(_run, q, today);
         Apply(q, step);

         return $"'{q.Id}': {step.FromStage} -> {(step.Kind == AuthoredStepKind.Completed ? "completed" : step.ToStage)}.";
      }

      private AuthoredStep StepAfterScene(AuthoredQuest q, int today)
      {
         AuthoredQuestEngine.SceneClosed(_run, q, today, out AuthoredStep step);

         return step;
      }
   }
}
