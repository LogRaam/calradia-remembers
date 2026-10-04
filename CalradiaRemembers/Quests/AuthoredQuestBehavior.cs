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
using TaleWorlds.CampaignSystem.Party;
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

      private Dictionary<string, AuthoredQuest> _quests = new(StringComparer.Ordinal);
      private List<string> _problems = new();
      private AuthoredQuestRun _run;
      private HashSet<string> _done = new(StringComparer.Ordinal);
      private Dictionary<string, int> _declined = new(StringComparer.Ordinal);

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
         string declined = string.Join("\n", _declined.Select(kv => kv.Key + "=" + kv.Value));
         dataStore.SyncData(RunKey, ref run);
         dataStore.SyncData(DoneKey, ref done);
         dataStore.SyncData(DeclinedKey, ref declined);

         if (!dataStore.IsLoading) return;

         _run = AuthoredQuestRun.Deserialize(run);
         _done = new HashSet<string>((done ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
         _declined = new Dictionary<string, int>(StringComparer.Ordinal);
         foreach (string line in (declined ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            int eq = line.LastIndexOf('=');
            if (eq > 0 && int.TryParse(line.Substring(eq + 1), out int day)) _declined[line.Substring(0, eq)] = day;
         }
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
         string root = Path.Combine(BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "Quests");
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

            TryOffer(hero, today, MBRandom.RandomFloat);
         });
      }

      /// <summary>Considers every quest this character could give; the first that qualifies and passes the offer rule is raised.</summary>
      internal bool TryOffer(Hero hero, int today, double roll, string onlyQuestId = null)
      {
         _offer = null;
         _offerHeroId = null;

         foreach (AuthoredQuest q in _quests.Values.Where(q => onlyQuestId == null || q.Id == onlyQuestId))
         {
            if (!TryBind(q, hero, out Dictionary<string, string> ids, out Dictionary<string, string> names)) continue;

            bool offer = AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {
               Today = today, StoryUnderWay = _run is {Status: AuthoredQuestStatus.Active}, Unique = q.Unique, AlreadyPlayed = _done.Contains(q.Id),
               DeclinedOnDay = _declined.TryGetValue(q.Id, out int d) ? d : (int?) null, Roll = roll, Chance = AuthoredQuestOfferPolicy.OfferChance
            });
            if (!offer) continue;

            _offer = AuthoredQuestEngine.Start(q, ids, names, today);
            _offerHeroId = hero.StringId;
            ModLog.Info($"[TALE] {hero.Name} raises '{q.Id}' with the player ({string.Join(", ", names.Select(kv => kv.Key + "=" + kv.Value))}).");

            return true;
         }

         return false;
      }

      /// <summary>
      ///   Binds the quest's roles around this giver, or false when they do not qualify. Increment 1 reads the giver
      ///   (role, regard on the mod's ledger, not at war) and one village (held by the giver's house, by a forest when
      ///   asked).
      /// </summary>
      private static bool TryBind(AuthoredQuest q, Hero giver, out Dictionary<string, string> ids, out Dictionary<string, string> names)
      {
         ids = new Dictionary<string, string>();
         names = new Dictionary<string, string>();
         if (giver == null || giver == Hero.MainHero || !Mapping.ActionGate.Allows(giver, Logic.ActionKind.Witness)) return false;

         GiverRequirement g = q.Requires?.Giver ?? new GiverRequirement();
         bool roleOk = g.Role switch {
            "lord" => giver.IsLord,
            "notable" => giver.IsNotable,
            "gang_leader" => giver.IsGangLeader,
            _ => true
         };
         if (!roleOk) return false;
         if (g.NotAtWarWithPlayer && giver.MapFaction != null && Hero.MainHero?.MapFaction != null
             && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, Hero.MainHero.MapFaction)) return false;
         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(giver.StringId)?.ReputationWithPlayer ?? 0;
         if (regard < g.MinRegard) return false;
         if ((Clan.PlayerClan?.Tier ?? 0) < (q.Requires?.Player?.MinClanTier ?? 0)) return false;

         ids["giver"] = giver.StringId;
         names["giver"] = giver.Name?.ToString();

         PlaceRequirement v = q.Requires?.Village;
         if (v != null)
         {
            IEnumerable<Settlement> candidates = g.Holds == "{village}"
               ? giver.Clan?.Settlements?.Where(s => s.IsVillage) ?? Enumerable.Empty<Settlement>()
               : new[] {giver.HomeSettlement}.Where(s => s != null && s.IsVillage);
            Settlement village = candidates.FirstOrDefault(s => v.Near != "forest" || NearForest(s));
            if (village == null) return false;

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
               CarryOut(q, q.Reward);
               journal?.CloseAsCompleted();
               _done.Add(q.Id);
               ModLog.Info($"[TALE] '{q.Id}' completed.");

               break;
            case AuthoredStepKind.Failed:
               CarryOut(q, q.Failure);
               journal?.CloseAsFailed("The time to act has passed.");
               _done.Add(q.Id);
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
         Journal(_run.QuestId)?.CloseAsCancelled(reason);
         ModLog.Info($"[TALE] '{_run.QuestId}' abandoned: {reason}");
         _run = null;
      }

      /// <summary>Self-test only: forgets that a quest was played or declined, so a test leaves no trace.</summary>
      internal void ForgetPlayed(string questId)
      {
         _done.Remove(questId);
         _declined.Remove(questId);
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
