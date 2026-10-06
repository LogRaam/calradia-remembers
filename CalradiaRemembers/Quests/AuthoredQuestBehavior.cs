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
         // fkasad (06/10/2026): also Quests under the player's Custom folder (the MCM path when set), like every other file.
         foreach (string dir in AuthoredQuestFolders.List(root, Mapping.NarrativeLoader.CustomBaseFolder()))
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
            whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, hero, today));
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
      private AuthoredOfferFacts FactsFor(AuthoredQuest q, Hero giver, int today) => new() {
         Today = today, StoryUnderWay = _run is {Status: AuthoredQuestStatus.Active}, Unique = q.Unique, AlreadyPlayed = _done.Contains(q.Id),
         GiverHasVanillaQuest = HasATaskOfTheirOwn(giver),
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
         whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, hero, (int) CampaignTime.Now.ToDays));
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

         // Branching tales (smoke_over_the_hills): the head of another house of the giver's realm, chosen by
         // Logic.AuthoredQuests.AuthoredRivalPolicy among the lords read here.
         if (q.Requires?.Rival != null)
         {
            Kingdom realm = giver.Clan?.Kingdom;
            IEnumerable<RivalCandidate> lords = (realm?.Clans ?? Enumerable.Empty<Clan>())
               .Where(c => c != null && !c.IsEliminated && c.Leader != null)
               .Select(c => new RivalCandidate {
                  HeroId = c.Leader.StringId, LeadsHisClan = true, OfTheGiversRealm = c.Kingdom == realm,
                  OfTheGiversHouse = c == giver.Clan, OfThePlayersHouse = c == Clan.PlayerClan || c.Leader == Hero.MainHero,
                  InTheWorld = Mapping.HeroPresence.IsInTheWorld(c.Leader)
               });
            string rivalId = AuthoredRivalPolicy.Choose(lords, MBRandom.RandomFloat, out string noRival);
            Hero rival = Find(rivalId);
            if (rival == null)
            {
               whyNot = noRival ?? AuthoredRivalPolicy.NoneEligible;

               return false;
            }

            ids["rival"] = rival.StringId;
            names["rival"] = rival.Name?.ToString();
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
         // The tale's place is marked on the map, as a vanilla quest marks its destination (fkasad, 06/10/2026).
         journal.Track(Settlement.Find(AuthoredQuestEngine.Bound(_run, "{village}") ?? ""));
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
      ///   Hourly: arriving at a be_at place (the stage's own, or a choice's), the bands the stage hunts, keeping watch
      ///   (Gabriel: nights spent waiting, no moon), and a scene that is due but not open (after a load, or when the
      ///   stage before it ended in a conversation). A night counts once, read deep in it (an hour past midnight) with
      ///   the player's party within the stage's radius.
      /// </summary>
      private void OnHourlyTick()
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;
         AuthoredStage s = AuthoredQuestEngine.Current(_run, q);
         var today = (int) CampaignTime.Now.ToDays;

         foreach (AuthoredWay band in AuthoredQuestEngine.BandsToHunt(_run, q)) EnsureBand(q, band);

         foreach (AuthoredObjective place in AuthoredQuestEngine.PlacesToReach(_run, q))
            if (Near(place.Place, place.Radius))
            {
               Apply(q, AuthoredQuestEngine.Arrived(_run, q, AuthoredQuestEngine.Bound(_run, place.Place), today));

               return;
            }

         switch (s?.Objective?.Type)
         {
            case "keep_watch":
               if (!CampaignTime.Now.IsNightTime || (int) CampaignTime.Now.CurrentHourInDay != 1) return;
               Apply(q, AuthoredQuestEngine.NightPassed(_run, q, Near(s.Objective.Place, s.Objective.Radius), today));
               break;
            case "explore":
               if (!ChatManager.IsOpen && !_sceneWaiting) OpenScene(q);
               break;
         }
      }

      // ── The quest's band ─────────────────────────────────────────────────

      private const string BandRole = "tale_band";

      /// <summary>
      ///   The band a defeat_party stage (or one of its choices) hunts, on the map near its place: spawned when the
      ///   stage begins, again after a load (a transient party is never saved), and again if others destroyed it (the
      ///   survivors regroup, and the deed stays the player's to do). A band warned in the scene (alert) is larger by a
      ///   third. The stage's own band keeps the tag it always had; a choice's band is tagged with the choice.
      /// </summary>
      private void EnsureBand(AuthoredQuest q, AuthoredWay way)
      {
         AuthoredSpawn spawn = way.Objective.Spawn;
         if (spawn == null) return;
         string tag = way.Choice == null ? q.Id : q.Id + "/" + way.Choice.Id;
         MobileParty live = TransientPartyRegistry.Instance?.WithTag(BandRole, tag);
         if (live != null && live.IsActive)
         {
            Remember(way, live.StringId);

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

         Remember(way, band.StringId);
         TransientPartyRegistry.Instance?.Track(band, BandRole, tag,
            onDestroyed: (party, by) => OnBandDestroyed(q, party, by),
            onRestore: pos => SpawnBand(pos, near, name, spawn.Template, size));
         ModLog.Info($"[TALE] '{q.Id}': {name} ({size}) near {near.Name}.");
      }

      /// <summary>Which band belongs to which way out: the stage's own, or a choice's.</summary>
      private void Remember(AuthoredWay way, string partyId)
      {
         if (way.Choice == null) _run.SpawnedPartyId = partyId;
         else _run.ChoiceBands[way.Choice.Id] = partyId;
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

      /// <summary>The band was destroyed: by the player, its way out is taken; by anyone else, it regroups on the next hour.</summary>
      private void OnBandDestroyed(AuthoredQuest q, MobileParty band, PartyBase by)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || band == null) return;
         string choiceOf = _run.ChoiceBands.FirstOrDefault(kv => kv.Value == band.StringId).Key;
         if (band.StringId != _run.SpawnedPartyId && choiceOf == null) return;

         if (by == PartyBase.MainParty || by?.MobileParty?.Army != null && by.MobileParty.Army.LeaderParty == MobileParty.MainParty)
         {
            ModLog.Info($"[TALE] '{q.Id}': the player destroyed {band.Name}.");
            Apply(q, AuthoredQuestEngine.PartyDefeated(_run, q, band.StringId, (int) CampaignTime.Now.ToDays));

            return;
         }

         ModLog.Info($"[TALE] '{q.Id}': {band.Name} was destroyed by {by?.Name}; the survivors will regroup.");
         if (choiceOf != null) _run.ChoiceBands.Remove(choiceOf);
         else _run.SpawnedPartyId = null;
      }

      /// <summary>
      ///   Takes the tale's bands off the map when the stage they belong to is left, or the tale ends, however it ends.
      ///   One tale runs at a time, so every band of the role is this tale's.
      /// </summary>
      private static void ReleaseBands() => TransientPartyRegistry.Instance?.ReleaseRole(BandRole);

      private bool Near(string placeRole, float radius)
      {
         Settlement s = Settlement.Find(AuthoredQuestEngine.Bound(_run, placeRole) ?? "");

         return s != null && MobileParty.MainParty != null && MobileParty.MainParty.GetPosition2D.Distance(s.GetPosition2D) <= Math.Max(1f, radius);
      }

      // ── The exploration scene ────────────────────────────────────────────

      /// <summary>Opens the current explore stage as a scene in the chat: a narrator, the file's findings, dangers and people.</summary>
      internal void OpenScene(AuthoredQuest q)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || AuthoredQuestEngine.Current(_run, q)?.Objective?.Type != "explore") return;
         if (ChatManager.IsOpen)
         {
            ModLog.Notice("a scene of your tale waits; it will open when the conversation ends.");
            _sceneWaiting = true;

            return;
         }

         _sceneWaiting = false;
         string place = _run.BoundNames.TryGetValue("village", out string v) ? v : null;
         string title = AuthoredQuestEngine.Current(_run, q)?.Objective?.Scene?.Title;
         var over = false;
         ChatManager.OpenForExploration(new ExplorationSession {
            Title = !string.IsNullOrWhiteSpace(title) ? AuthoredQuestEngine.Fill(_run, title) : place != null ? $"The wood by {place}" : q.Title,
            BuildPrompt = () => AuthoredQuestPrompt.Narrator(_run, q),
            Apply = markers => ApplyMarkers(q, markers, () => over = true),
            IsOver = () => over,
            OnClose = () => CloseScene(q),
            Ways = () => SceneWays(q)
         });
         ModLog.Info($"[TALE] '{q.Id}': the scene opens.");
      }

      private bool _sceneWaiting;

      /// <summary>The words of the scene's open ways out, names filled: what the chat lists under the narration.</summary>
      private IReadOnlyList<string> SceneWays(AuthoredQuest q)
         => _run is {Status: AuthoredQuestStatus.Active}
            ? AuthoredQuestEngine.SceneChoices(_run, q).Select(c => AuthoredQuestEngine.Fill(_run, c.Label)).ToList()
            : new List<string>();

      /// <summary>
      ///   What the narration's markers do: findings recorded, a hazard's effect carried out, someone met, and a way out
      ///   taken (the first open one), which ends the scene after this turn; the story follows it when the scene closes.
      /// </summary>
      private IReadOnlyList<string> ApplyMarkers(AuthoredQuest q, SceneMarkers m, Action endTheScene)
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
                  endTheScene();
                  notes.Add("The night is lost; dawn finds you no further along.");
                  break;
            }
         }

         foreach (string id in m.Met)
            if (AuthoredQuestEngine.Meet(_run, q, id))
            {
               string who = AuthoredQuestEngine.Current(_run, q)?.Objective?.Scene?.Encounters.FirstOrDefault(x => x.Id == id)?.Name;
               notes.Add($"{AuthoredQuestEngine.Fill(_run, who) ?? "Someone"} is with you now.");
            }

         foreach (string c in m.Choices)
            if (AuthoredQuestEngine.ChooseInScene(_run, q, c))
            {
               ModLog.Info($"[TALE] scene choice '{c}'.");
               endTheScene();
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
         AuthoredScene scene = AuthoredQuestEngine.Current(_run, q)?.Objective?.Scene;
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

      /// <summary>
      ///   Carries out what a step decided: the journal lines of the next stage (its own, then each open choice's), the
      ///   ending reached (the reward, or a named one), or the failure. A stage left behind takes its bands with it, and
      ///   a stage that is a scene opens (now, or when the conversation that led to it ends).
      /// </summary>
      internal void Apply(AuthoredQuest q, AuthoredStep step, AuthoredStoryQuest journal = null)
      {
         if (step == null || step.Kind == AuthoredStepKind.None) return;
         journal ??= Journal(q.Id);
         string taken = step.ChoiceId != null ? $" by choice '{step.ChoiceId}'" : "";

         switch (step.Kind)
         {
            case AuthoredStepKind.Advanced:
            case AuthoredStepKind.OpenScene:
               ReleaseBands();
               foreach (string line in AuthoredQuestEngine.JournalLines(_run, q)) journal?.Log(AuthoredQuestEngine.Fill(_run, line));
               AnnounceStage(AuthoredStageNotice.Text(q.Title, AuthoredQuestEngine.Fill(_run, AuthoredQuestEngine.Current(_run, q)?.Journal)));
               ModLog.Info($"[TALE] '{q.Id}': {step.FromStage} -> {step.ToStage}{taken}.");
               if (AuthoredQuestEngine.Current(_run, q)?.Objective?.Type == "explore") MainThreadDispatcher.Enqueue(() => OpenScene(q));

               break;
            case AuthoredStepKind.Completed:
               ReleaseBands();
               AuthoredOutcome ending = AuthoredQuestEngine.Outcome(_run, q);
               CarryOut(q, ending);
               if (!string.IsNullOrWhiteSpace(ending?.Journal)) journal?.Log(AuthoredQuestEngine.Fill(_run, ending.Journal));
               journal?.CloseAsCompleted();
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' completed{taken}, ending '{step.EndingId ?? "reward"}'.");

               break;
            case AuthoredStepKind.Failed:
               ReleaseBands();
               CarryOut(q, q.Failure);
               if (!string.IsNullOrWhiteSpace(q.Failure?.Journal)) journal?.Log(AuthoredQuestEngine.Fill(_run, q.Failure.Journal));
               journal?.CloseAsFailed("The time to act has passed.");
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' failed at stage {step.FromStage}.");

               break;
         }
      }

      /// <summary>The last stage notice shown (cr.testall reads it back).</summary>
      internal string LastStageNotice { get; private set; }

      /// <summary>
      ///   Tells the player the tale moved on (Gabriel, 04/10/2026: a stage complete and a new one begun should be
      ///   said, not only written into the journal): on screen, and in the chat when one is open, since a stage
      ///   often moves in a conversation or as a scene closes.
      /// </summary>
      private void AnnounceStage(string notice)
      {
         LastStageNotice = notice;
         try
         {
            MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(notice));
            ChatManager.ActiveViewModel?.AppendSystemMessage(notice, true);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] stage notice failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Regard and a memory for the giver, then the outcome's consequences (memory, world_event), each only when its
      ///   flags hold on the path the player walked.
      /// </summary>
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

         foreach (AuthoredConsequence c in AuthoredQuestEngine.Consequences(_run, outcome))
         {
            try
            {
               if (c.Type == "memory")
                  foreach (string id in Who(c.Who)) Remember(id, AuthoredQuestEngine.Fill(_run, c.Text));
               else if (c.Type == "grudge")
               {
                  // A tale may set two lords at odds (Gabriel, 06/10/2026); the grudge ledger fades it with time.
                  TaleGrudge g = AuthoredQuestEngine.GrudgeOf(_run, c);
                  Hero holder = Find(g?.HolderId), target = Find(g?.TargetId);
                  if (holder != null && target != null)
                     Grudges.GrudgeBehavior.Instance?.TryPlant(holder, target, Logic.Grudges.GrudgeSource.Tale, g.Cause, g.Magnitude,
                        knownToPlayer: true, today: (int) CampaignTime.Now.ToDays);
               }
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

      /// <summary>
      ///   The authored quest block for this character's prompt, or null. The character whose conversation ended the tale
      ///   (the lord hearing the last report) keeps speaking from it until that conversation closes: before branches the
      ///   block vanished the moment the run completed, so the final stage's canvas never reached the model.
      /// </summary>
      public string PromptBlockFor(Hero hero)
      {
         if (hero == null) return null;
         try
         {
            bool underWay = _run is {Status: AuthoredQuestStatus.Active} || _run != null && _run.SpokenHeroId == hero.StringId;
            if (underWay && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
               return AuthoredQuestPrompt.ForCharacter(_run, q, hero.StringId, LocalOf(hero), offering: false);

            if (_offer != null && _offerHeroId == hero.StringId && _quests.TryGetValue(_offer.QuestId, out AuthoredQuest o))
               return AuthoredQuestPrompt.ForCharacter(_offer, o, hero.StringId, null, offering: true);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] prompt block failed: {ex.GetType().Name}: {ex.Message}"); }

         return null;
      }

      /// <summary>
      ///   The written tale's lines for an ordinary villager of <paramref name="place" />, as a notable of it gets them
      ///   (fkasad, 06/10/2026: the peasants in the street knew nothing of the beast). Null when no tale concerns it.
      /// </summary>
      public string PromptBlockForLocalsOf(Settlement place)
      {
         if (place == null) return null;
         try
         {
            if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
               return AuthoredQuestPrompt.ForCharacter(_run, q, "commoner_of_" + place.StringId, place.StringId, offering: false);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] villager prompt block failed: {ex.GetType().Name}: {ex.Message}"); }

         return null;
      }

      /// <summary>
      ///   The choices by words open in a conversation with this character, written for the ACTION INTERPRETER only
      ///   (EncounterContext.StoryChoicesForInterpreter); null when none is open. The character is never told them.
      /// </summary>
      public string InterpreterChoicesFor(Hero hero)
      {
         if (hero == null || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return null;
         try { return AuthoredQuestPrompt.ChoicesForInterpreter(_run, q, hero.StringId); }
         catch (Exception ex)
         {
            ModLog.Error($"[TALE] interpreter choices failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The action interpreter reported a choice by words (the story_choice verb) in a conversation with this
      ///   character. Taken only when the current stage declares it, open, said to this very character; the step is
      ///   then carried out like any other. Returns the choice's words when taken, null when the game refused it.
      /// </summary>
      internal string TakeSaidChoice(Hero hero, string choiceId)
      {
         if (hero == null || string.IsNullOrWhiteSpace(choiceId) || _run is not {Status: AuthoredQuestStatus.Active}
             || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return null;

         AuthoredChoice c = AuthoredQuestEngine.SaidChoicesFor(_run, q, hero.StringId).FirstOrDefault(x => x.Id == choiceId.Trim());
         AuthoredStep step = AuthoredQuestEngine.Said(_run, q, hero.StringId, choiceId.Trim(), (int) CampaignTime.Now.ToDays);
         if (c == null || step.Kind == AuthoredStepKind.None)
         {
            ModLog.Info($"[TALE] choice by words '{choiceId}' with {hero.Name} refused: not a declared, open choice said to them.");

            return null;
         }

         ModLog.Info($"[TALE] choice by words '{c.Id}' taken with {hero.Name}.");
         Apply(q, step);

         return AuthoredQuestEngine.Fill(_run, string.IsNullOrWhiteSpace(c.Label) ? c.Id : c.Label);
      }

      // ── Helpers ──────────────────────────────────────────────────────────

      /// <summary>
      ///   The giver already has a task for the player: a vanilla issue on offer, or a quest of theirs under way (any but a
      ///   tale). fkasad (06/10/2026): two errands from one mouth tangle the conversation.
      /// </summary>
      private static bool HasATaskOfTheirOwn(Hero giver)
      {
         if (giver == null) return false;
         try
         {
            if (giver.Issue != null) return true;

            return Campaign.Current?.QuestManager?.Quests?.Any(x => x != null && x.IsOngoing && x.QuestGiver == giver && x is not AuthoredStoryQuest) == true;
         }
         catch
         {
            return false;
         }
      }

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
         ReleaseBands();
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
         AuthoredWay own = AuthoredQuestEngine.BandsToHunt(_run, q).FirstOrDefault(w => w.Choice == null);
         if (own == null) return null;
         EnsureBand(q, own);

         return TransientPartyRegistry.Instance?.WithTag(BandRole, q.Id);
      }

      /// <summary>Self-test seam: the band destroyed by this party, through the real handling.</summary>
      internal void DebugBandDestroyed(MobileParty band, PartyBase by)
      {
         if (_run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) OnBandDestroyed(q, band, by);
      }

      /// <summary>Self-test seam: the ways out the chat would list under the narration now, through the real handling.</summary>
      internal IReadOnlyList<string> DebugSceneWays()
         => _run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q) ? SceneWays(q) : new List<string>();

      /// <summary>
      ///   Self-test seam: reads a tale written into the test itself through the real loader and adds it to the tales in
      ///   play (never a shipped file); the loader's problems are returned. <see cref="DebugForgetTale" /> takes it out.
      /// </summary>
      internal IReadOnlyList<string> DebugLoadTale(string json, string source)
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(json, source);
         foreach (AuthoredQuest q in r.Quests) _quests[q.Id] = q;

         return r.Problems;
      }

      /// <summary>Self-test seam: takes a tale the test loaded out of the tales in play.</summary>
      internal void DebugForgetTale(string questId) => _quests.Remove(questId);

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
         AuthoredStep step = s?.Objective?.Type == "explore"
            ? StepAfterScene(q, today)
            : AuthoredQuestEngine.ForceAdvance(_run, q, today);
         Apply(q, step);

         return Describe(q, step);
      }

      /// <summary>cr.story_choose: takes this choice of the current stage, if it is open, however it would be taken (a test lever).</summary>
      internal string DebugChoose(string choiceId)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return "No tale is under way.";
         IReadOnlyList<AuthoredChoice> open = AuthoredQuestEngine.OpenChoices(_run, q);
         if (open.All(c => c.Id != choiceId))
            return $"No open choice '{choiceId}' at stage '{_run.StageId}'. Open: {(open.Count == 0 ? "none" : string.Join(", ", open.Select(c => c.Id)))}.";

         AuthoredStep step = AuthoredQuestEngine.ForceChoice(_run, q, choiceId, (int) CampaignTime.Now.ToDays);
         Apply(q, step);

         return Describe(q, step);
      }

      private static string Describe(AuthoredQuest q, AuthoredStep step)
         => step.Kind == AuthoredStepKind.None
            ? $"'{q.Id}': nothing to step."
            : $"'{q.Id}': {step.FromStage} -> {(step.Kind == AuthoredStepKind.Completed ? "ending '" + (step.EndingId ?? "reward") + "'" : step.ToStage)}"
              + (step.ChoiceId != null ? $" (choice '{step.ChoiceId}')." : ".");

      private AuthoredStep StepAfterScene(AuthoredQuest q, int today)
      {
         AuthoredQuestEngine.SceneClosed(_run, q, today, out AuthoredStep step);

         return step;
      }
   }
}
