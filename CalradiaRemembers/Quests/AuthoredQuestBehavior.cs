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
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.WorldEvents;
using Helpers;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using CalradiaRemembers.Travel;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

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

      // A queen's return that had to wait (her husband away on campaign, or a captivity): "herId|himId|questId" to the day
      // it was first asked. Additive key: an older save simply has none waiting.
      private const string PendingReturnsKey = "CalradiaRemembers_TaleReturnsWaiting_v1";

      // The pictures of each stage's image_pool already shown, by "tale/stage" (Gabriel, 24/10/2026: a new video each time
      // the tale comes back). Additive key: an older save shows from a fresh round.
      private const string PoolShownKey = "CalradiaRemembers_TaleImagePoolShown_v1";

      // The tale set aside while a scenario's tale plays (TalePause, Gabriel, 25/10/2026). Absent in older saves.
      private const string PausedRunKey = "CalradiaRemembers_TalePausedRun_v1";

      /// <summary>How long a waiting return is tried before it is given up (and said so in the log).</summary>
      private const int ReturnWaitDays = 60;

      private Dictionary<string, AuthoredQuest> _quests = new(StringComparer.Ordinal);
      private List<string> _problems = new();
      private AuthoredQuestRun _run;

      // The tale set aside for a scenario's tale (one level only); taken up again once that is over and the player is free.
      private AuthoredQuestRun _paused;
      private HashSet<string> _done = new(StringComparer.Ordinal);
      private Dictionary<string, int> _declined = new(StringComparer.Ordinal);
      private Dictionary<string, int> _playedOn = new(StringComparer.Ordinal);
      private Dictionary<string, int> _returnsWaiting = new(StringComparer.Ordinal);
      private Dictionary<string, List<string>> _poolShown = new(StringComparer.Ordinal);

      // The offer being made in the conversation now open (transient: a conversation does not survive a save).
      private AuthoredQuestRun _offer;
      private string _offerHeroId;

      // A tale that begins on the map (Gabriel, 10/10/2026): where the player's party stood last hour, how many hours it
      // has stood there, and the day the dice were last cast (transient: after a load the party counts its hours again).
      private Vec2 _lastPartyPosition;
      private int _stillHours;
      private int _mapRolledOn = -1;

      public AuthoredQuestBehavior()
      {
         Instance = this;
         // A choice leading to an ending the player pays for is offered only while the purse holds it (tashmetu, 20/10/2026).
         AuthoredQuestEngine.PlayerGold = () => Hero.MainHero?.Gold ?? 0;
         // OPEN_TALE (Gabriel, 24/10/2026): scenarios open a tale through this door, without knowing this behaviour.
         Scenarios.ScenarioConsequenceExecutor.TaleOpener = (questId, giver, dryRun) => Instance?.OpenFromScenario(questId, giver, dryRun) ?? "authored quests are not active";
      }

      public static AuthoredQuestBehavior Instance { get; private set; }

      /// <summary>Every problem found in the quest files at load (cr.story_check).</summary>
      public IReadOnlyList<string> Problems => _problems;

      public IReadOnlyCollection<AuthoredQuest> Quests => _quests.Values;
      public AuthoredQuestRun ActiveRun => _run;

      /// <summary>
      ///   The hero holds a role in the tale under way, which is then their errand (fkasad, 07/10/2026), or is raising a
      ///   tale with the player now (fkasad, 18/10/2026).
      /// </summary>
      public bool HoldsARole(string heroId) => AuthoredTaleRoles.HoldsARole(_run, heroId, _offer != null ? _offerHeroId : null)
                                               || AuthoredTaleRoles.HoldsARole(_paused, heroId);

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => SafeRun.Step("AuthoredQuests.Load", OnSessionLaunched));
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("AuthoredQuests.Daily", OnDailyTick));
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("AuthoredQuests.Hourly", OnHourlyTick));
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, e => SafeRun.Step("AuthoredQuests.MapEventEnded", () => OnMapEventEnded(e)));
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
         string returnsWaiting = DayList(_returnsWaiting);
         string poolShown = string.Join("\n", _poolShown.Select(kv => kv.Key + "=" + string.Join("|", kv.Value)));
         dataStore.SyncData(RunKey, ref run);
         dataStore.SyncData(DoneKey, ref done);
         dataStore.SyncData(DeclinedKey, ref declined);
         dataStore.SyncData(PlayedOnKey, ref playedOn);
         try { dataStore.SyncData(PendingReturnsKey, ref returnsWaiting); }
         catch
         {
            returnsWaiting = ""; // an older save: no return waiting
         }

         try { dataStore.SyncData(PoolShownKey, ref poolShown); }
         catch
         {
            poolShown = ""; // an older save: every pool starts a fresh round
         }

         string paused = _paused?.Serialize() ?? "";
         try { dataStore.SyncData(PausedRunKey, ref paused); }
         catch
         {
            paused = ""; // an older save: nothing was set aside
         }

         if (!dataStore.IsLoading) return;

         _run = AuthoredQuestRun.Deserialize(run);
         _paused = AuthoredQuestRun.Deserialize(paused);
         _done = new HashSet<string>((done ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
         _declined = ReadDayList(declined);
         _playedOn = ReadDayList(playedOn);
         _returnsWaiting = ReadDayList(returnsWaiting);
         _poolShown = new Dictionary<string, List<string>>(StringComparer.Ordinal);
         foreach (string line in (poolShown ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            int eq = line.IndexOf('=');
            if (eq > 0) _poolShown[line.Substring(0, eq)] = line.Substring(eq + 1).Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries).ToList();
         }
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
         _folders.Clear();
         // fkasad (06/10/2026): also Quests under the player's Custom folder (the MCM path when set), like every other file.
         // Gabriel (08/10/2026): a tale may be a folder of its own, its JSON beside its images; one level down is read too.
         foreach (string dir in AuthoredQuestFolders.List(root, Mapping.NarrativeLoader.CustomBaseFolder()))
            ReadTalesFolder(files, dir, rel => rel);
         // Gabriel (09/10/2026), for tashmetu: another enabled mod's ModuleData/cr_quests, after CR's own and the player's,
         // so a tale already defined there wins and the mod's copy is reported by cr.story_check.
         foreach (TaleModuleFolder mod in ModuleTaleFolders())
         {
            if (Directory.Exists(mod.Folder)) ModLog.Info($"[TALE] reading {mod.ModuleId}'s tales ({AuthoredQuestFolders.ModuleTalesFolder}).");
            ReadTalesFolder(files, mod.Folder, mod.SourceOf);
         }

         AuthoredQuestLoadResult r = AuthoredQuestLoader.ParseAll(files);
         _quests = r.Quests.ToDictionary(q => q.Id, StringComparer.Ordinal);
         _problems = r.Problems;
         foreach (AuthoredQuest q in _quests.Values)
         {
            string folder = FolderOf(q);
            _problems.AddRange(AuthoredTaleImages.Problems(q, name => folder != null && File.Exists(AuthoredTaleImages.PathIn(folder, name))));
            _problems.AddRange(TaleItemPolicy.UnknownItems(q, IsCategory, IsItem));
         }

         ModLog.Info($"[TALE] {_quests.Count} authored quest(s) loaded from {files.Count} file(s); {_problems.Count} problem(s).");
         foreach (string p in _problems) ModLog.Info("[TALE] " + p);
         if (_problems.Count > 0)
            ModLog.Notice($"{_problems.Count} problem(s) in the quest files (ModuleData/Quests). cr.story_check lists them.");
      }

      /// <summary>A tales folder and one level of sub-folders (a tale may be a folder of its own, its JSON beside its images).</summary>
      private void ReadTalesFolder(List<(string, string)> files, string dir, Func<string, string> sourceOf)
      {
         if (!Directory.Exists(dir)) return;
         foreach (string f in Directory.GetFiles(dir, "*.json")) AddFile(files, sourceOf(Path.GetFileName(f)), f);
         foreach (string sub in Directory.GetDirectories(dir))
            if (!string.Equals(Path.GetFileName(sub), "Custom", StringComparison.OrdinalIgnoreCase))
               foreach (string f in Directory.GetFiles(sub, "*.json")) AddFile(files, sourceOf(Path.GetFileName(sub) + "/" + Path.GetFileName(f)), f);
      }

      /// <summary>The tales folders of the other enabled mods, in launcher order (the same enabled list cr_patch reads).</summary>
      internal static IReadOnlyList<TaleModuleFolder> ModuleTaleFolders()
      {
         try
         {
            var modules = new List<(string, string)>();
            foreach (string id in Storage.NarrativePatchScanner.LoadedModuleIds())
            {
               string moduleRoot;
               try { moduleRoot = TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath(id); }
               catch { continue; }
               modules.Add((id, moduleRoot));
            }

            return AuthoredQuestFolders.FromModules(modules, "CalradiaRemembers");
         }
         catch (Exception ex)
         {
            ModLog.Fault("listing other mods' tales folders", ex);

            return new List<TaleModuleFolder>();
         }
      }

      /// <summary>How many of the tales loaded came from another mod's cr_quests (cr.testall tale_mod_folders).</summary>
      internal int TalesFromOtherMods => _quests.Values.Count(q => q.Source != null && q.Source.Contains(": "));

      private readonly Dictionary<string, string> _folders = new(StringComparer.OrdinalIgnoreCase);

      private void AddFile(List<(string, string)> files, string source, string path)
      {
         files.Add((source, File.ReadAllText(path)));
         _folders[source] = Path.GetDirectoryName(path);
      }

      /// <summary>The folder a tale was read from (its images sit beside it), or null.</summary>
      private string FolderOf(AuthoredQuest q) => q?.Source != null && _folders.TryGetValue(q.Source, out string f) ? f : null;

      /// <summary>
      ///   Only the tale under way keeps one open journal entry (TaleJournalPolicy). tashmetu (07/10/2026): "a finished tale
      ///   stays listed twice, under Old Quests and still under Active Quests": a second open entry of the same tale
      ///   outlived the close of the first until the next load. Run at load, every day, and when a tale ends.
      /// </summary>
      private void ReconcileJournal()
      {
         List<AuthoredStoryQuest> open = Campaign.Current?.QuestManager?.Quests?.OfType<AuthoredStoryQuest>().Where(q => q.IsOngoing).ToList()
                                         ?? new List<AuthoredStoryQuest>();
         foreach (AuthoredStoryQuest q in open)
         {
            AuthoredStoryQuest newest = open.Last(o => o.StoryId == q.StoryId); // the quest list keeps the order quests began in
            bool finished = _run == null && _done.Contains(q.StoryId ?? "");
            TaleJournalVerdict verdict = TaleJournalPolicy.Verdict(new TaleJournalFacts {
               EntryStoryId = q.StoryId, IsNewestOfItsStory = ReferenceEquals(newest, q),
               RunQuestId = finished ? q.StoryId : _run?.QuestId, RunStatus = finished ? AuthoredQuestStatus.Completed : _run?.Status,
               PausedQuestId = _paused?.QuestId
            });
            if (verdict == TaleJournalVerdict.Keep) continue;

            ModLog.Info($"[TALE] journal entry of '{q.StoryId}' closed ({verdict}).");
            if (verdict == TaleJournalVerdict.CloseCompleted) q.CloseAsCompleted();
            else if (verdict == TaleJournalVerdict.CloseFailed) q.CloseAsFailed("The time to act has passed.");
            else q.CloseAsCancelled("This tale has ended.");
         }
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
               // Items in tales (tashmetu, 20/10/2026): the goods are handed over with the bag, never taken on a greeting;
               // the player is told where, once the chat is up.
               if (TryDeliveryFor(hero, out string key, out int count))
               {
                  string hint = $"{hero.Name} awaits {count} {Items.SupplyItemMatcher.Label(key)}: hand them over with the bag once you carry them.";
                  MainThreadDispatcher.Enqueue(() => ChatManager.ActiveViewModel?.AppendSystemMessage(hint, true));
               }

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
            if (q.OnMap != null)
            {
               whyNot = "it begins by itself on the map, never in a conversation";

               continue;
            }

            if (q.OnScenario != null)
            {
               whyNot = "it is opened only by a scenario (OPEN_TALE), never in a conversation";

               continue;
            }

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
      // A scenario's summons is not an errand raised in conversation: nothing of the giver's own bars it, neither an errand on
      // offer nor a quest of theirs under way (Gabriel, 26/10/2026: the night was still refused with the wolf's tale under way).
      private AuthoredOfferFacts FactsFor(AuthoredQuest q, Hero giver, int today, bool bySummons = false) => new() {
         Today = today, StoryUnderWay = TalePause.StoryUnderWay(_run is {Status: AuthoredQuestStatus.Active}, _paused != null, bySummons),
         Unique = q.Unique, AlreadyPlayed = _done.Contains(q.Id),
         GiverHasVanillaQuest = !bySummons && HasATaskOfTheirOwn(giver),
         LastPlayedOnDay = _playedOn.TryGetValue(q.Id, out int p) ? p : (int?) null, RepeatCooldownDays = q.RepeatCooldownDays,
         DeclinedOnDay = _declined.TryGetValue(q.Id, out int d) ? d : (int?) null, Chance = AuthoredQuestOfferPolicy.OfferChance,
         ContentAllowed = AuthoredContentPolicy.Allows(q.Requires?.Content, ModSettings.AdultContentLevel), Content = q.Requires?.Content
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
         if (q.OnScenario != null)
            return $"yes, when a scenario opens it (OPEN_TALE) ({string.Join(", ", names.Select(kv => kv.Key + "=" + kv.Value))})";
         if (q.OnMap != null)
         {
            whyNot = AuthoredMapTriggerPolicy.WhyNot(q.OnMap, MapFactsFor(hero));
            if (whyNot != null) return "not now: " + whyNot;

            return $"yes, on the map, on a {q.OnMap.ChancePerDay:P0} chance today ({string.Join(", ", names.Select(kv => kv.Key + "=" + kv.Value))})";
         }

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
         // Items in tales (tashmetu, 20/10/2026): goods this game does not know could never be brought nor given.
         string unknownGoods = TaleItemPolicy.UnknownItems(q, IsCategory, IsItem).FirstOrDefault();
         if (unknownGoods != null)
         {
            whyNot = "it names goods this game does not know (cr.story_check lists them)";

            return false;
         }

         if (giver == null || giver == Hero.MainHero || !Mapping.ActionGate.Allows(giver, Logic.ActionKind.Witness))
         {
            whyNot = "they cannot take part right now (not free, not of age, or not in the world)";

            return false;
         }

         GiverRequirement g = q.Requires?.Giver ?? new GiverRequirement();
         whyNot = AuthoredGiverPolicy.WhyNot(g, new GiverFacts {
            IsLord = giver.IsLord, IsNotable = giver.IsNotable, IsGangLeader = giver.IsGangLeader,
            IsPlayersSpouse = Hero.MainHero?.Spouse == giver, IsPlayersCompanion = giver.IsPlayerCompanion, IsFemale = giver.IsFemale,
            InPlayersParty = giver.PartyBelongedTo != null && giver.PartyBelongedTo == MobileParty.MainParty, IsPregnant = giver.IsPregnant,
            // The Troy-inspired tale (Gabriel, 12/10/2026): a married giver of another realm who could leave her house.
            IsMarriedToAnother = giver.Spouse != null && giver.Spouse != Hero.MainHero && Mapping.HeroPresence.IsInTheWorld(giver.Spouse),
            IsClanLeader = giver.Clan != null && giver.Clan.Leader == giver,
            IsOfPlayersRealm = giver.MapFaction != null && giver.MapFaction == Hero.MainHero?.MapFaction,
            Trade = giver.IsNotable ? Logic.StationGuidelinesPolicy.TradeKeyOf(giver.Occupation.ToString()) : null
         });
         if (whyNot != null) return false;

         if (!AuthoredCulturePolicy.Matches(g.Culture, giver.Culture?.StringId, giver.Culture?.Name?.ToString()))
         {
            whyNot = $"the quest wants a giver of {AuthoredCulturePolicy.Describe(g.Culture)}, and they are of {giver.Culture?.Name}";

            return false;
         }

         Kingdom giversRealm = giver.Clan?.Kingdom;
         if (!AuthoredCulturePolicy.Matches(g.Realm, giversRealm?.StringId ?? "", giversRealm?.Name?.ToString() ?? ""))
         {
            whyNot = $"the quest wants a giver of the realm {AuthoredCulturePolicy.Describe(g.Realm)}, and they are of {(giversRealm != null ? $"{giversRealm.Name} ({giversRealm.StringId})" : "no realm")}";

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

         whyNot = AuthoredPlayerPolicy.WhyNot(q.Requires?.Player, new AuthoredPlayerFacts {ClanTier = Clan.PlayerClan?.Tier ?? 0, IsFemale = Hero.MainHero?.IsFemale == true});
         if (whyNot != null) return false;

         ids["giver"] = giver.StringId;
         names["giver"] = giver.Name?.ToString();
         // tashmetu (11/10/2026): {player} names the player in a tale's journal, canvases, memories and consequences
         // (AuthoredQuestLoader.GameBoundRoles). A name only: no hero id, so no objective or knower can target the player.
         names["player"] = Hero.MainHero?.Name?.ToString();
         // Gabriel (12/10/2026): one queen per realm, each with her picture: "queen_{giver_realm}.jpg" names the realm's
         // own file, and {giver_realm} in a line names the realm.
         if (giver.MapFaction != null)
         {
            ids["giver_realm"] = giver.MapFaction.StringId;
            names["giver_realm"] = giver.MapFaction.Name?.ToString();
         }

         PlaceRequirement v = q.Requires?.Village;
         if (v != null)
         {
            // tashmetu (20/10/2026): a town as the bound place, for tales from town merchants.
            bool town = string.Equals(v.Type?.Trim(), "town", StringComparison.OrdinalIgnoreCase);
            bool OfKind(Settlement s) => s != null && (town ? s.IsTown : s.IsVillage);
            IEnumerable<Settlement> candidates = g.Holds == "{village}"
               ? giver.Clan?.Settlements?.Where(OfKind) ?? Enumerable.Empty<Settlement>()
               : new[] {giver.HomeSettlement}.Where(OfKind);
            List<Settlement> owned = candidates.ToList();
            List<Settlement> ofCulture = owned.Where(s => AuthoredCulturePolicy.Matches(v.Culture, s.Culture?.StringId, s.Culture?.Name?.ToString())).ToList();
            List<Settlement> placed = ofCulture.Where(s => v.Near != "forest" || NearForest(s)).ToList();
            // fkasad (18/10/2026): never a raided or looted village, nor one whose fortress is besieged (TaleVillagePolicy).
            List<TaleVillageFacts> states = placed.Select(VillageFacts).ToList();
            TaleVillageFacts quiet = TaleVillagePolicy.Pick(states);
            Settlement village = quiet == null ? null : placed[states.IndexOf(quiet)];
            if (village == null)
            {
               whyNot = owned.Count == 0 ? $"no {(town ? "town" : "village")} is theirs to give the quest about"
                  : ofCulture.Count == 0
                     ? $"none of their villages ({string.Join(", ", owned.Select(s => s.Name))}) is of {AuthoredCulturePolicy.Describe(v.Culture)}"
                  : placed.Count == 0
                     ? $"none of their villages of that culture ({string.Join(", ", ofCulture.Select(s => s.Name))}) lies by a forest"
                     : $"none of their villages is quiet now ({TaleVillagePolicy.WhyNoneQuiet(states)})";

               return false;
            }

            ids["village"] = village.StringId;
            names["village"] = village.Name?.ToString();
         }

         // The giver's husband (the Troy-inspired tale, Gabriel 12/10/2026): her own spouse, bound as {husband}.
         if (q.Requires?.Husband != null)
         {
            Hero husband = giver.Spouse;
            whyNot = AuthoredHusbandPolicy.WhyNot(q.Requires.Husband, new HusbandFacts {
               Exists = husband != null, InTheWorld = husband != null && Mapping.HeroPresence.IsInTheWorld(husband),
               RulesARealm = husband?.Clan?.Kingdom != null && husband.Clan.Kingdom.Leader == husband
            });
            if (whyNot != null) return false;

            ids["husband"] = husband.StringId;
            names["husband"] = husband.Name?.ToString();
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
            // A talk_to that completes as the conversation ends (Gabriel, 24/10/2026: the night judged by what it left).
            if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest playing))
               Apply(playing, AuthoredQuestEngine.ConversationEnded(_run, playing, log?.NpcId, (int) CampaignTime.Now.ToDays));
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
         PauseUnderWay(offer);
         _run = offer;
         Hero giver = Find(AuthoredQuestEngine.Bound(_run, "{giver}"));
         var today = (int) CampaignTime.Now.ToDays;
         // Where the giver's regard stood as the tale began, read back as it ends (TaleRegardFlags).
         _run.GiverRegardAtStart = RegardOf(giver);
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
         RetryWaitingReturns();
         ReconcileJournal();
         if (_run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;
         if (StopIfContentLowered(q)) return;
         if (StopIfVillageLost(q)) return;
         Apply(q, AuthoredQuestEngine.Day(_run, q, (int) CampaignTime.Now.ToDays));
         if (_run is {Status: AuthoredQuestStatus.Active}) Apply(q, ReadWorld(q));
      }

      /// <summary>
      ///   Gabriel (18/10/2026, the Troy-inspired tale): the ways out the world takes rather than the player (the war
      ///   ends, someone dies, the player weds her), read once a day for the people the current stage names.
      /// </summary>
      private AuthoredStep ReadWorld(AuthoredQuest q)
      {
         var roles = new Dictionary<string, TaleRoleState>();
         foreach (string role in AuthoredQuestEngine.RolesWatched(_run, q)) roles[role] = RoleState(Find(AuthoredQuestEngine.Bound(_run, role)));

         return AuthoredQuestEngine.WorldNow(_run, q, roles, (int) CampaignTime.Now.ToDays);
      }

      /// <summary>What the world exits read of one of the tale's people. A captive is still in the world; the dead are not.</summary>
      private static TaleRoleState RoleState(Hero h)
         => new() {
            Gone = !Mapping.HeroPresence.IsInTheWorld(h),
            WedToPlayer = h != null && Hero.MainHero != null && h.Spouse == Hero.MainHero,
            AtWarWithPlayer = h?.MapFaction != null && Hero.MainHero?.MapFaction != null && h.MapFaction != Hero.MainHero.MapFaction
                              && FactionManager.IsAtWarAgainstFaction(h.MapFaction, Hero.MainHero.MapFaction)
         };

      /// <summary>cr.testall seam: what the world exits read of this hero.</summary>
      internal static TaleRoleState DebugRoleState(Hero h) => RoleState(h);

      /// <summary>
      ///   Gabriel (10/10/2026): a tale whose content level is now above the player's setting (they lowered it) stops,
      ///   without reward or penalty, rather than go on sending what they switched off. True when it stopped.
      /// </summary>
      internal bool StopIfContentLowered(AuthoredQuest q)
      {
         if (AuthoredContentPolicy.Allows(q?.Requires?.Content, ModSettings.AdultContentLevel)) return false;
         Abandon($"its content level ({q?.Requires?.Content}) is above the setting chosen in the mod options");

         return true;
      }

      /// <summary>
      ///   fkasad (18/10/2026): the tale's village fell with its town, and the tale asked him to retake it. Gabriel the same
      ///   day: when the giver no longer holds the village a tale is about, the tale ends without reward or penalty, its
      ///   journal saying who holds the village now. Only for a tale whose giver holds the village ("holds": "{village}").
      ///   True when it stopped.
      /// </summary>
      internal bool StopIfVillageLost(AuthoredQuest q)
      {
         if (q?.Requires?.Giver?.Holds != "{village}") return false;
         Hero giver = Find(AuthoredQuestEngine.Bound(_run, "{giver}"));
         Settlement village = Settlement.Find(AuthoredQuestEngine.Bound(_run, "{village}") ?? "");
         if (giver?.Clan == null || village == null) return false;
         if (!TaleVillagePolicy.GiverLostIt(giver.Clan.StringId, village.OwnerClan?.StringId)) return false;

         Abandon(TaleVillagePolicy.LostLine(village.Name?.ToString(), village.OwnerClan?.Name?.ToString()));

         return true;
      }

      /// <summary>The state of a village a tale could be about, as the game reports it.</summary>
      private static TaleVillageFacts VillageFacts(Settlement s)
         => new() {
            Name = s?.Name?.ToString(),
            BeingRaided = s?.Village?.VillageState == Village.VillageStates.BeingRaided,
            Looted = s?.Village?.VillageState == Village.VillageStates.Looted,
            // A town is its own fortress (2.7.19): besieged, it is no place for a tale either.
            FortressUnderSiege = s?.IsTown == true ? s.IsUnderSiege : s?.Village?.Bound?.IsUnderSiege == true
         };

      /// <summary>cr.testall seam: the state of a village as the tale's choice reads it.</summary>
      internal static TaleVillageFacts DebugVillageFacts(Settlement s) => VillageFacts(s);

      /// <summary>
      ///   Hourly: arriving at a be_at place (the stage's own, or a choice's), the bands the stage hunts, keeping watch
      ///   (Gabriel: nights spent waiting, no moon), and a scene that is due but not open (after a load, or when the
      ///   stage before it ended in a conversation). A night counts once, read deep in it (an hour past midnight) with
      ///   the player's party within the stage's radius.
      /// </summary>
      private void OnHourlyTick()
      {
         ResumeIfFree(Hero.MainHero?.IsPrisoner == true);
         SafeRun.Step("AuthoredQuests.MapTrigger", TickMapTrigger);
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

      // ── A tale that begins on the map ────────────────────────────────────

      /// <summary>
      ///   Gabriel (10/10/2026): the party waits on the map, by day, in the giver's own lands, and a tale may begin. Counts
      ///   the hours the party stands still, then, once a day when some tale could begin, casts the dice once for all of
      ///   them (one weighted pick, its own chance), and asks the player by a popup.
      /// </summary>
      private void TickMapTrigger()
      {
         MobileParty main = MobileParty.MainParty;
         if (main == null) return;
         Vec2 at = main.GetPosition2D;
         _stillHours = at.DistanceSquared(_lastPartyPosition) < 0.0001f ? _stillHours + 1 : 0;
         _lastPartyPosition = at;

         if (!ModSettings.AuthoredQuests || _run is {Status: AuthoredQuestStatus.Active} || ChatManager.IsOpen || Mission.Current != null) return;
         var today = (int) CampaignTime.Now.ToDays;
         if (_mapRolledOn == today) return;

         List<MapOpening> open = MapTalesOpenNow(today, null, out _);
         if (open.Count == 0) return;

         _mapRolledOn = today;
         int pick = AuthoredQuestOfferPolicy.PickByWeight(open.Select(o => o.Quest.Weight).ToArray(), open.Count == 1 ? 0 : MBRandom.RandomFloat);
         if (pick < 0) return;
         MapOpening chosen = open[pick];
         if (!AuthoredMapTriggerPolicy.Begins(chosen.Quest.OnMap.ChancePerDay, MBRandom.RandomFloat))
         {
            ModLog.Info($"[TALE] '{chosen.Quest.Id}' could begin on the map today; the dice said no.");

            return;
         }

         AuthoredQuestRun run = AuthoredQuestEngine.Start(chosen.Quest, chosen.Ids, chosen.Names, today);
         ModLog.Info($"[TALE] '{chosen.Quest.Id}' begins on the map with {chosen.Giver.Name} ({string.Join(", ", chosen.Names.Select(kv => kv.Key + "=" + kv.Value))}).");
         AskOnMap(chosen.Quest, run, chosen.Giver);
      }

      /// <summary>A tale that could begin on the map now, with whom, and the roles it bound.</summary>
      private sealed class MapOpening
      {
         public AuthoredQuest Quest;
         public Hero Giver;
         public Dictionary<string, string> Ids;
         public Dictionary<string, string> Names;
      }

      /// <summary>Every tale that begins on the map and could begin now with someone of the player's party; the last reason otherwise.</summary>
      private List<MapOpening> MapTalesOpenNow(int today, string onlyQuestId, out string whyNot, bool placeAndTime = true)
      {
         var open = new List<MapOpening>();
         whyNot = "no tale that begins on the map is loaded";
         List<Hero> party = MapGivers();
         foreach (AuthoredQuest q in _quests.Values.Where(q => q.OnMap != null && (onlyQuestId == null || q.Id == onlyQuestId)))
         {
            whyNot = "no one travelling with you could give it";
            foreach (Hero hero in party)
            {
               if (!TryBind(q, hero, out Dictionary<string, string> ids, out Dictionary<string, string> names, out string why))
               {
                  whyNot = why;

                  continue;
               }

               why = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, hero, today)) ?? (placeAndTime ? AuthoredMapTriggerPolicy.WhyNot(q.OnMap, MapFactsFor(hero)) : null);
               if (why != null)
               {
                  whyNot = why;

                  continue;
               }

               open.Add(new MapOpening {Quest = q, Giver = hero, Ids = ids, Names = names});
               break;
            }
         }

         return open;
      }

      /// <summary>The characters travelling in the player's party, the player's spouse first.</summary>
      private static List<Hero> MapGivers()
      {
         var heroes = new List<Hero>();
         if (Hero.MainHero?.Spouse is Hero spouse) heroes.Add(spouse);
         foreach (TroopRosterElement e in MobileParty.MainParty?.MemberRoster?.GetTroopRoster() ?? new List<TroopRosterElement>())
         {
            Hero h = e.Character?.HeroObject;
            if (h != null && h != Hero.MainHero && !heroes.Contains(h)) heroes.Add(h);
         }

         return heroes;
      }

      /// <summary>Where the player's party is now, read for a tale given by <paramref name="giver" />.</summary>
      private MapTriggerFacts MapFactsFor(Hero giver)
      {
         MobileParty main = MobileParty.MainParty;
         Vec2 at = main?.GetPosition2D ?? Vec2.Zero;
         Settlement near = Settlement.All.Where(s => s.IsTown || s.IsCastle || s.IsVillage).OrderBy(s => s.GetPosition2D.DistanceSquared(at)).FirstOrDefault();

         return new MapTriggerFacts {
            StillHours = _stillHours, IsDay = CampaignTime.Now.IsDayTime, InSettlement = main?.CurrentSettlement != null,
            InBattle = main?.MapEvent != null || main?.SiegeEvent != null || main?.BesiegedSettlement != null, AtSea = main?.IsCurrentlyAtSea == true,
            InGiversLands = near?.Culture != null && near.Culture == giver?.Culture, LandsCulture = near?.Culture?.Name?.ToString()
         };
      }

      /// <summary>The popup: yes begins the tale and opens the conversation with the giver; no costs nothing for a season.</summary>
      private void AskOnMap(AuthoredQuest q, AuthoredQuestRun run, Hero giver)
      {
         var today = (int) CampaignTime.Now.ToDays;
         InformationManager.ShowInquiry(new InquiryData(
            q.Title,
            AuthoredQuestEngine.Fill(run, q.OnMap.Invitation),
            true, true, AuthoredQuestEngine.Fill(run, q.OnMap.Accept), AuthoredQuestEngine.Fill(run, q.OnMap.Decline),
            () =>
            {
               Accept(q, run);
               ChatSceneStager.StageTaleMeeting(giver);
            },
            () =>
            {
               _declined[q.Id] = today;
               ModLog.Info($"[TALE] the player let '{q.Id}' pass on the map.");
            }), true);
      }

      /// <summary>Self-test seam: why this loaded tale would not begin on the map now, the dice aside; null when it could.</summary>
      internal string DebugMapWhyNot(string questId)
      {
         if (!_quests.TryGetValue(questId, out AuthoredQuest q)) return "not loaded";
         if (q.OnMap == null) return "it does not begin on the map";
         string whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, null, (int) CampaignTime.Now.ToDays));
         if (whyNot != null) return whyNot;

         return MapTalesOpenNow((int) CampaignTime.Now.ToDays, questId, out whyNot).Count > 0 ? null : whyNot;
      }

      /// <summary>
      ///   cr.tale_on_map (Gabriel, 11/10/2026: test a tale that begins on the map without waiting on a 15% day): the popup of
      ///   this tale now, every condition checked but the dice; with <paramref name="anyway" />, the place and the hour are
      ///   skipped too (the giver and the tale's own rules still hold).
      /// </summary>
      internal string DebugBeginOnMap(string questId, bool anyway)
      {
         if (_run is {Status: AuthoredQuestStatus.Active}) return $"A tale is under way ('{_run.QuestId}'): finish or abandon it first (cr.story_abandon).";
         AuthoredQuest q = _quests.Values.FirstOrDefault(x => x.Id == questId);
         if (q == null) return $"No tale '{questId}' is loaded (cr.story_check lists them).";
         if (q.OnMap == null) return $"'{questId}' does not begin on the map.";

         // A test lever: the tale may be played again, as if never played or declined.
         ForgetPlayed(questId);
         var today = (int) CampaignTime.Now.ToDays;
         List<MapOpening> open = MapTalesOpenNow(today, questId, out string whyNot, placeAndTime: !anyway);
         if (open.Count == 0) return $"'{questId}' cannot begin now: {whyNot}." + (anyway ? "" : " Add 'anyway' to skip the place and the hour.");

         MapOpening o = open[0];
         AuthoredQuestRun run = AuthoredQuestEngine.Start(q, o.Ids, o.Names, today);
         ModLog.Info($"[TALE] '{q.Id}' begun on the map by the console with {o.Giver.Name}.");
         AskOnMap(q, run, o.Giver);

         return $"'{questId}' begins with {o.Giver.Name}: the popup is open.";
      }

      /// <summary>Self-test seam: the hours the party has stood still, as the trigger counts them.</summary>
      internal int DebugStillHours => _stillHours;

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
         // fkasad (07/10/2026): a band that scales meets the player at their size, so the climax stays one.
         int size = AuthoredBandSizePolicy.Size(new AuthoredBandSizeFacts {
            Rolled = MBRandom.RandomInt(lo, hi + 1), ScalesWithPlayer = spawn.ScaleWithPlayer,
            PlayerFighters = MobileParty.MainParty?.MemberRoster?.TotalHealthyCount ?? 0
         });
         if (_run.BandAlerted) size += size / 3;

         // A point the game itself would pick (BanditSpawnCampaignBehavior's way), not raw arithmetic around the village:
         // a random offset can land in water or off the walkable map, and the native party creation then failed with an
         // AccessViolationException (cr.testall authored_quest_opening, Gabriel 19/10/2026, near Mecalovea).
         Vec2 at = NavigationHelper.FindPointAroundPosition(near.GatePosition, MobileParty.NavigationType.Default, 3f).ToVec2();
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

      /// <summary>The tale band's own spawn, for cr.tale_fight_spike (TaleCompanyFightSpike).</summary>
      internal static MobileParty SpawnBandForSpike(Vec2 at, Settlement home, string name, string template, int size)
         => SpawnBand(at, home, name, template, size);

      /// <summary>A bandit-owned band of the template's troops, patrolling around its place.</summary>
      private static MobileParty SpawnBand(Vec2 at, Settlement home, string name, string template, int size)
      {
         try
         {
            Clan bandits;
            CharacterObject troop;
            if (template == AuthoredBandMakeupPolicy.LocalTemplate) (bandits, troop) = RegionalBandits(home);
            else
            {
               bandits = Clan.BanditFactions?.FirstOrDefault(c => c.StringId == (template ?? "looters"));
               troop = CharacterObject.Find(template == "looters" || template == null ? "looter" : template);
            }

            bandits ??= Clan.BanditFactions?.FirstOrDefault(c => c.StringId == "looters");
            troop ??= CharacterObject.Find("looter");
            if (bandits == null || troop == null) return null;

            // fkasad (07/10/2026): 65 looters against 110 veterans was no fight. A share of the band is seasoned as the
            // player's own men are (AuthoredBandMakeupPolicy), along the troop's own upgrade line.
            var line = new List<CharacterObject> {troop};
            while (line.Count < 3 && line[line.Count - 1].UpgradeTargets is {Length: > 0} next && next[0] != null) line.Add(next[0]);
            int[] split = AuthoredBandMakeupPolicy.Split(new AuthoredBandMakeupFacts {
               Size = Math.Max(1, size), PlayerAverageTier = PlayerAverageTier(), UpgradeSteps = line.Count - 1
            });

            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            for (var i = 0; i < line.Count && i < split.Length; i++)
               if (split[i] > 0) roster.AddToCounts(line[i], split[i]);
            ModLog.Info($"[TALE] band of {bandits.Name}: {string.Join(", ", line.Select((t, i) => $"{(i < split.Length ? split[i] : 0)} {t.Name}"))}.");
            CampaignVec2? spot = Travel.MapSpot.Walkable(at, home, "tale band " + name);
            if (spot == null) return null;

            MobileParty band = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
               spot.Value, 1f, home, new TaleWorlds.Localization.TextObject(name), bandits, roster, TroopRoster.CreateDummyTroopRoster(), null);
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

      /// <summary>The bandits of the region around a place: those of the nearest hideout (fkasad, 07/10/2026).</summary>
      internal static (Clan Clan, CharacterObject Troop) RegionalBandits(Settlement near)
      {
         if (near == null) return (null, null);
         Vec2 at = near.GetPosition2D;
         Settlement hideout = Settlement.All.Where(s => s.IsHideout).OrderBy(s => s.GetPosition2D.DistanceSquared(at)).FirstOrDefault();
         CultureObject culture = hideout?.Culture;
         Clan clan = culture == null ? null : Clan.BanditFactions?.FirstOrDefault(c => c.Culture == culture);

         return (clan, clan?.BasicTroop ?? culture?.BasicTroop);
      }

      /// <summary>The average tier of the regular troops in the player's party (heroes left out); 1 when there are none.</summary>
      private static double PlayerAverageTier()
      {
         int men = 0, tiers = 0;
         foreach (TroopRosterElement e in MobileParty.MainParty?.MemberRoster?.GetTroopRoster() ?? new List<TroopRosterElement>())
         {
            if (e.Character == null || e.Character.IsHero) continue;
            men += e.Number;
            tiers += e.Number * e.Character.Tier;
         }

         return men == 0 ? 1 : (double) tiers / men;
      }

      /// <summary>
      ///   A battle ended: if the player fought the tale's band and lost, the tale hears of it (Gabriel, 10/10/2026: the
      ///   band's on_defeat). A battle the player won is OnBandDestroyed's; one the player was not in is no concern here.
      /// </summary>
      private void OnMapEventEnded(MapEvent e)
      {
         if (e == null || !e.HasWinner || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;

         var tale = new HashSet<string>(_run.ChoiceBands.Values) {_run.SpawnedPartyId ?? ""};
         string bandId = e.InvolvedParties.Select(p => p?.MobileParty?.StringId).FirstOrDefault(id => id != null && tale.Contains(id));
         if (bandId == null) return;

         BattleSideEnum? playerSide = e.PartiesOnSide(BattleSideEnum.Attacker).Any(p => p.Party == PartyBase.MainParty) ? BattleSideEnum.Attacker
            : e.PartiesOnSide(BattleSideEnum.Defender).Any(p => p.Party == PartyBase.MainParty) ? BattleSideEnum.Defender
            : (BattleSideEnum?) null;
         if (playerSide == null || e.DefeatedSide != playerSide) return;

         OnPlayerDefeated(q, bandId);
      }

      /// <summary>The player lost to the tale's band: its on_defeat is told once per stage, and may end the tale.</summary>
      private void OnPlayerDefeated(AuthoredQuest q, string bandId)
      {
         AuthoredDefeatResult lost = AuthoredQuestEngine.PlayerLostTo(_run, q, bandId, (int) CampaignTime.Now.ToDays);
         if (lost.OnDefeat == null)
         {
            ModLog.Info($"[TALE] '{q.Id}': the player lost to the band; the tale says nothing of a defeat, it waits.");

            return;
         }

         ModLog.Info($"[TALE] '{q.Id}': the player lost to the band ({(lost.Tell ? "told" : "already told on this stage")}; " +
                     $"{(lost.Step.Kind == AuthoredStepKind.Completed ? "ends: " + lost.Step.EndingId : "the tale goes on")}).");
         if (lost.Tell)
         {
            CarryOut(q, lost.OnDefeat.AsOutcome());
            if (!string.IsNullOrWhiteSpace(lost.OnDefeat.Journal)) Journal(q.Id)?.Log(AuthoredQuestEngine.Fill(_run, lost.OnDefeat.Journal));
         }

         Apply(q, lost.Step);
      }

      /// <summary>Self-test seam: a tale's "elope" or "return_spouse" between these two, through the real handling.</summary>
      internal void DebugElopement(string consequenceType, Hero her, Hero him)
      {
         AuthoredQuestRun keep = _run;
         try
         {
            _run = new AuthoredQuestRun {QuestId = "cr_selftest_elopement"};
            _run.BoundIds["giver"] = her?.StringId;
            _run.BoundIds["husband"] = him?.StringId;
            var c = new AuthoredConsequence {Type = consequenceType};
            if (consequenceType == "elope") Elope(c);
            else ReturnSpouse(c);
         }
         finally { _run = keep; }
      }

      /// <summary>Self-test seam: the player lost to the tale's own band, through the real handling after the battle.</summary>
      internal void DebugPlayerLostToBand()
      {
         if (_run != null && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q) && _run.SpawnedPartyId != null) OnPlayerDefeated(q, _run.SpawnedPartyId);
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
            BuildPrompt = () => AuthoredQuestPrompt.Narrator(_run, q, Surroundings()),
            Apply = markers => ApplyMarkers(q, markers, () => over = true),
            IsOver = () => over,
            OnClose = () => CloseScene(q),
            Ways = () => SceneWays(q),
            PrisonersWelcome = () => AuthoredQuestEngine.Current(_run, q)?.Objective?.Scene?.InvitePrisoners == true,
            MayEnd = () => _run is {Status: AuthoredQuestStatus.Active} && AuthoredQuestEngine.SceneMayEnd(_run, q),
            // Gabriel (08/10/2026): the stage's picture opens beside the chat with its scene.
            ImagePath = SceneImagePath(q),
            TakeImage = () =>
            {
               string path = _pendingSceneImage;
               _pendingSceneImage = null;

               return path;
            },
            FoundSoFar = () => (AuthoredQuestEngine.Current(_run, q)?.Objective?.Scene?.Findings ?? new List<AuthoredFinding>())
                               .Where(f => _run.Findings.Contains(f.Id)).Select(f => AuthoredQuestEngine.Fill(_run, f.Text)).ToList()
         });
         ModLog.Info($"[TALE] '{q.Id}': the scene opens.");
      }

      private bool _sceneWaiting;

      private string _pendingSceneImage;

      /// <summary>The full path of the image of the stage under way, when it names one present in the tale's folder.</summary>
      private string SceneImagePath(AuthoredQuest q) => ImagePath(q, AuthoredTaleImages.ImageFor(_run, q));

      /// <summary>The full path of one of the tale's images, when it is present in the tale's folder; null otherwise.</summary>
      private string ImagePath(AuthoredQuest q, string image)
      {
         string folder = FolderOf(q);
         if (!AuthoredTaleImages.IsAcceptedName(image) || folder == null) return null;
         string path = AuthoredTaleImages.PathIn(folder, image.Trim());

         return File.Exists(path) ? path : null;
      }

      /// <summary>
      ///   The picture of the stage a conversation with this character belongs to, and the tale's title (Gabriel, 08/10/2026:
      ///   the lord's hall when he asks, the tavern when the village is questioned): the stage under way for someone the tale
      ///   speaks to, the first stage for the one offering it. Null when the conversation is no part of a tale.
      /// </summary>
      public (string Path, string Title)? ConversationImageFor(Hero hero)
      {
         if (hero == null) return null;
         try
         {
            if (PromptBlockFor(hero) == null) return null;
            // Gabriel (11/10/2026): only the conversation that IS the step under way, never one with a character the tale
            // merely concerns (an "A word with you" with a notable opened the wolf's picture).
            if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
               return AuthoredTaleImages.ConversationIsTheStep(_run, q, hero.StringId, LocalOf(hero)) && SceneImagePath(q) is string p ? (p, q.Title) : null;
            if (_offer != null && _offerHeroId == hero.StringId && _quests.TryGetValue(_offer.QuestId, out AuthoredQuest o))
               return ImagePath(o, AuthoredTaleImages.Resolve(o.Stages.FirstOrDefault()?.Image, _offer.BoundIds)) is string first ? (first, o.Title) : null;
         }
         catch (Exception ex) { ModLog.Error($"[TALE] conversation image failed: {ex.GetType().Name}: {ex.Message}"); }

         return null;
      }

      /// <summary>The picture of the stage under way for a villager of <paramref name="place" /> the tale concerns, or null.</summary>
      public (string Path, string Title)? ConversationImageForLocalsOf(Settlement place)
      {
         if (place == null || PromptBlockForLocalsOf(place) == null) return null;
         if (_run is {Status: AuthoredQuestStatus.Active} && _quests.TryGetValue(_run.QuestId, out AuthoredQuest q))
            return AuthoredTaleImages.ConversationIsTheStep(_run, q, "commoner_of_" + place.StringId, place.StringId) && SceneImagePath(q) is string p
               ? (p, q.Title)
               : null;

         return null;
      }

      /// <summary>What the game knows of where the scene happens and who walks into it (fkasad, 07/10/2026).</summary>
      internal static SceneSurroundingsFacts Surroundings()
      {
         try
         {
            string terrain = Campaign.Current?.MapSceneWrapper != null && MobileParty.MainParty != null
               ? Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(MobileParty.MainParty.Position).ToString()
               : null;

            return new SceneSurroundingsFacts {
               EngineTerrain = terrain,
               Season = CampaignTime.Now.GetSeasonOfYear.ToString(),
               Hour = (int) CampaignTime.Now.CurrentHourInDay,
               PlayerCulture = Hero.MainHero?.Culture?.Name?.ToString()
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TALE] the scene's surroundings could not be read: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

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
         // Gabriel (08/10/2026): what this narration reveals, springs or brings in may carry a picture; read before the
         // markers move the tale on, and taken by the chat once the narration is shown.
         _pendingSceneImage = ImagePath(q, AuthoredTaleImages.ImageForMarkers(_run, q, m.Found, m.Hazards, m.Met));
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
               PickPoolImage(q);
               MeetIfSummoned(q);
               foreach (string line in AuthoredQuestEngine.JournalLines(_run, q)) journal?.Log(AuthoredQuestEngine.Fill(_run, line));
               AnnounceStage(AuthoredStageNotice.Text(q.Title, AuthoredQuestEngine.Fill(_run, AuthoredQuestEngine.Current(_run, q)?.Journal)));
               ModLog.Info($"[TALE] '{q.Id}': {step.FromStage} -> {step.ToStage}{taken}.");
               // tashmetu (15/10/2026): a stage's on_enter, carried out before the stage is announced to scenarios and mods,
               // so both see an elopement the stage carried out as done.
               List<CrTaleConsequence> entered = CarryOutConsequences(q, AuthoredQuestEngine.OnEnter(_run, q));
               FireTaleEvent(q, step.ToStage, entered);
               if (AuthoredQuestEngine.Current(_run, q)?.Objective?.Type == "explore") MainThreadDispatcher.Enqueue(() => OpenScene(q));

               break;
            case AuthoredStepKind.Completed:
               ReleaseBands();
               TaleRegardFlags.Mark(_run, RegardOf(Find(AuthoredQuestEngine.Bound(_run, "{giver}"))));
               AuthoredOutcome ending = AuthoredQuestEngine.Outcome(_run, q);
               List<CrTaleConsequence> endedWith = CarryOut(q, ending);
               if (!string.IsNullOrWhiteSpace(ending?.Journal)) journal?.Log(AuthoredQuestEngine.Fill(_run, ending.Journal));
               journal?.CloseAsCompleted();
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' completed{taken}, ending '{step.EndingId ?? "reward"}'.");
               FireTaleEvent(q, "end:" + (step.EndingId ?? "reward"), endedWith);
               ReconcileJournal();

               break;
            case AuthoredStepKind.Failed:
               ReleaseBands();
               TaleRegardFlags.Mark(_run, RegardOf(Find(AuthoredQuestEngine.Bound(_run, "{giver}"))));
               List<CrTaleConsequence> failedWith = CarryOut(q, q.Failure);
               if (!string.IsNullOrWhiteSpace(q.Failure?.Journal)) journal?.Log(AuthoredQuestEngine.Fill(_run, q.Failure.Journal));
               journal?.CloseAsFailed("The time to act has passed.");
               _done.Add(q.Id);
               _playedOn[q.Id] = (int) CampaignTime.Now.ToDays;
               ModLog.Info($"[TALE] '{q.Id}' failed at stage {step.FromStage}.");
               FireTaleEvent(q, "end:failure", failedWith);
               ReconcileJournal();

               break;
         }
      }

      /// <summary>
      ///   The tale-scenario bridge (Gabriel, 12/10/2026): every stage reached and every ending sets off the scenario rules
      ///   that answer ON_TALE:&lt;tale&gt;:&lt;stage&gt; or ON_TALE:&lt;tale&gt;:end:&lt;ending&gt;, with the tale's roles (the player
      ///   included) so a rule can name {husband}.
      /// </summary>
      private void FireTaleEvent(AuthoredQuest q, string where, IEnumerable<CrTaleConsequence> carriedOut)
      {
         if (_run == null) return;

         Dictionary<string, string> roles = TaleRoles();
         Scenarios.ScenarioBehavior.Instance?.FireTale(q.Id, where, roles);

         // tashmetu (15/10/2026): the same moment, for other mods' code (CrTales), with what was really carried out.
         var progress = new CrTaleProgress(q.Id, where, (int) CampaignTime.Now.ToDays, roles, _run.Flags, carriedOut);
         if (where.StartsWith("end:", StringComparison.Ordinal)) CrTales.RaiseEnded(progress);
         else CrTales.RaiseStageReached(progress);
      }

      /// <summary>The run's bound roles, the player included.</summary>
      private Dictionary<string, string> TaleRoles()
      {
         var roles = new Dictionary<string, string>(_run?.BoundIds ?? new Dictionary<string, string>());
         if (Hero.MainHero != null) roles["player"] = Hero.MainHero.StringId;

         return roles;
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
      private List<CrTaleConsequence> CarryOut(AuthoredQuest q, AuthoredOutcome outcome)
      {
         if (outcome == null) return new List<CrTaleConsequence>();
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
         PayGold(outcome.Gold, giver);

         return CarryOutConsequences(q, AuthoredQuestEngine.Consequences(_run, outcome), outcome);
      }

      /// <summary>
      ///   An ending's gold: paid to the player, or, negative, paid by the player to the giver (gone, to no one): tashmetu
      ///   (20/10/2026), a fine, a bribe, a child's keep. A choice leading here was offered only while the player had it
      ///   (AuthoredQuestEngine.CostOf); an ending reached otherwise takes what he has (the game's own transfer stops there).
      /// </summary>
      private static void PayGold(int gold, Hero giver)
      {
         if (gold > 0) TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, gold);
         if (gold < 0) TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Mapping.HeroPresence.IsInTheWorld(giver) ? giver : null, -gold);
      }

      /// <summary>cr.testall tale_keys: an ending's gold through the real payment.</summary>
      internal static void DebugPayGold(int gold, Hero giver) => PayGold(gold, giver);

      /// <summary>
      ///   Carries out consequences (an ending's, or a stage's on_enter) and says what became of each (tashmetu, 15/10/2026:
      ///   another mod must not answer an elopement that did not happen).
      /// </summary>
      private List<CrTaleConsequence> CarryOutConsequences(AuthoredQuest q, IEnumerable<AuthoredConsequence> consequences, AuthoredOutcome outcome = null)
      {
         var results = new List<CrTaleConsequence>();
         List<AuthoredConsequence> all = (consequences ?? Enumerable.Empty<AuthoredConsequence>()).ToList();
         ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(_run, outcome, all);
         Hero giver = Find(AuthoredQuestEngine.Bound(_run, "{giver}"));
         foreach (AuthoredConsequence c in all)
         {
            try
            {
               if (c.Type == "memory")
               {
                  List<string> ids = MemoryRecipients(c.Who, named);
                  foreach (string id in ids) Remember(id, AuthoredQuestEngine.Fill(_run, c.Text));
                  results.Add(ids.Count > 0
                     ? Result(c, ids[0], null, CrTaleConsequenceStatus.Done, null)
                     : Result(c, null, null, CrTaleConsequenceStatus.NotDone, "nobody to remember it"));
               }
               else if (c.Type == "grudge")
               {
                  // A tale may set two lords at odds (Gabriel, 06/10/2026); the grudge ledger fades it with time.
                  TaleGrudge g = AuthoredQuestEngine.GrudgeOf(_run, c);
                  Hero holder = Find(g?.HolderId), target = Find(g?.TargetId);
                  bool planted = holder != null && target != null && Grudges.GrudgeBehavior.Instance?.TryPlant(holder, target,
                     Logic.Grudges.GrudgeSource.Tale, g.Cause, g.Magnitude, knownToPlayer: true, today: (int) CampaignTime.Now.ToDays) == true;
                  results.Add(Result(c, g?.HolderId, g?.TargetId, planted ? CrTaleConsequenceStatus.Done : CrTaleConsequenceStatus.NotDone,
                     planted ? null : "the grudge could not be planted"));
               }
               else if (c.Type == "conception")
                  results.Add(Conceive(c));
               else if (c.Type == "elope")
                  results.Add(Elope(c));
               else if (c.Type == "return_spouse")
                  results.Add(ReturnSpouse(c));
               else if (c.Type == "item")
                  results.Add(GiveItem(c));
               else if (c.Type == "regard")
                  results.Add(MoveRegard(c));
               else if (c.Type == "world_event")
               {
                  bool told = WorldEventStore.Instance != null;
                  WorldEventStore.Instance?.Record(new WorldEvent {
                     Type = WorldEventType.Other, Day = (int) CampaignTime.Now.ToDays, Magnitude = c.Magnitude,
                     Summary = AuthoredQuestEngine.Fill(_run, c.Summary), PlaceId = AuthoredQuestEngine.Bound(_run, c.Place),
                     ActorIds = giver != null ? new List<string> {giver.StringId} : new List<string>(), InvolvesPlayer = true
                  });
                  results.Add(Result(c, null, null, told ? CrTaleConsequenceStatus.Done : CrTaleConsequenceStatus.NotDone, told ? null : "no news store"));
               }
            }
            catch (Exception ex)
            {
               ModLog.Error($"[TALE] consequence '{c?.Type}' of '{q.Id}' failed: {ex.GetType().Name}: {ex.Message}");
               results.Add(Result(c, null, null, CrTaleConsequenceStatus.NotDone, "it failed"));
            }
         }

         return results;
      }

      private static CrTaleConsequence Result(AuthoredConsequence c, string whoId, string otherId, CrTaleConsequenceStatus status, string reason)
         => new CrTaleConsequence(c?.Type, whoId, otherId, status, reason);

      /// <summary>
      ///   A regard consequence (Gabriel, 24/10/2026: a regard only the endings of some paths earn, under a flag): moves the
      ///   regard of who it names ({giver} when empty) on the mod's own ledger, a profile made when CR holds none yet.
      /// </summary>
      private CrTaleConsequence MoveRegard(AuthoredConsequence c)
      {
         string id = AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c.Who) ? "{giver}" : c.Who);
         Hero hero = Find(id);
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (hero == null || hero == Hero.MainHero || store == null || c.Amount == 0)
            return Result(c, id, null, CrTaleConsequenceStatus.NotDone, "no one whose regard could move");

         NpcProfile profile = store.Get(hero.StringId) ?? Mapping.HeroProfileMapper.FromHero(hero);
         ProfileMutator.ApplyReputationDelta(profile, c.Amount, NpcMemoryService.Core.Standing.RegardCause.Quest);
         store.Set(profile);

         return Result(c, hero.StringId, null, CrTaleConsequenceStatus.Done, null);
      }

      /// <summary>A character's regard for the player on CR's own ledger; 0 for someone CR has no profile of.</summary>
      private static int RegardOf(Hero hero)
         => hero == null ? 0 : NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.ReputationWithPlayer ?? 0;

      // Self-tests open a scenario tale without staging anything on screen.
      private bool _stagingHeld;

      /// <summary>
      ///   A scenario tale brings its people to the player (Gabriel, 24-25/10/2026: the prisoner is fetched to the Empress's
      ///   chamber; they cannot go to her): a stage that is a conversation with someone opens that conversation by itself,
      ///   once no other chat is open (a scene that led there closes first).
      /// </summary>
      private void MeetIfSummoned(AuthoredQuest q)
      {
         if (q.OnScenario == null || _stagingHeld) return;

         AuthoredObjective o = AuthoredQuestEngine.Current(_run, q)?.Objective;
         Hero who = o?.Type == "talk_to" ? Find(AuthoredQuestEngine.Bound(_run, o.Hero)) : null;
         if (who == null) return;

         StageWhenFree(q.Id, who, 1800);
      }

      // Waits frame by frame (at most a few seconds' worth) for the chat to be closed, then stages the meeting, if the tale
      // still waits for it.
      private void StageWhenFree(string questId, Hero who, int framesLeft)
      {
         MainThreadDispatcher.Enqueue(() =>
         {
            if (_run is not {Status: AuthoredQuestStatus.Active} || _run.QuestId != questId) return;
            // The scene that led here closes its backdrop on a later frame: the meeting waits for it to be gone.
            if (ChatManager.IsOpen || ExplorationBackdrop.IsOpen)
            {
               if (framesLeft > 0) StageWhenFree(questId, who, framesLeft - 1);
               else ModLog.Notice($"the conversation with {who.Name} of your tale could not open: another chat stayed open.");

               return;
            }

            // Gabriel (26/10/2026): on the scene's dark backdrop, with no vanilla conversation, and the character first.
            ModLog.Info($"[TALE] '{questId}': the conversation with {who.Name} opens by itself, on the scene's backdrop.");
            NpcMemoryCampaignBehavior.Instance?.DialogFlow?.OpenChatForSummonedMeeting(who);
         });
      }

      /// <summary>
      ///   The stage just entered shows one picture of its image_pool for this playing of the tale: one not shown before, the
      ///   pool starting over once every one was (TaleImagePool).
      /// </summary>
      private void PickPoolImage(AuthoredQuest q)
      {
         AuthoredStage stage = AuthoredQuestEngine.Current(_run, q);
         if (stage?.ImagePool == null || stage.ImagePool.Count == 0 || _run.ImagePicks.ContainsKey(stage.Id)) return;

         string key = q.Id + "/" + stage.Id;
         List<string> shown = _poolShown.TryGetValue(key, out List<string> s) ? s : new List<string>();
         bool atRandom = string.Equals(stage.ImagePoolPick?.Trim(), "random", StringComparison.OrdinalIgnoreCase);
         string picked = TaleImagePool.Pick(stage.ImagePool, shown, MBRandom.RandomFloat, atRandom);
         if (picked == null) return;

         _run.ImagePicks[stage.Id] = picked;
         _poolShown[key] = TaleImagePool.AfterShowing(stage.ImagePool, shown, picked);
         ModLog.Info($"[TALE] '{q.Id}' stage {stage.Id} shows {picked} from its pool ({_poolShown[key].Count} of {stage.ImagePool.Count} shown this round).");
      }

      /// <summary>
      ///   OPEN_TALE (Gabriel, 24/10/2026: the night Rhagaea sends for her prisoner): opens a tale that only a scenario opens,
      ///   with <paramref name="giver" />, by its popup. Null when it opened (or, <paramref name="dryRun" />, would open); else
      ///   why not, in plain words. The tale's own rules hold (its giver's conditions, its content level, once or again after
      ///   its cooldown, never while another tale is under way).
      /// </summary>
      internal string OpenFromScenario(string questId, Hero giver, bool dryRun)
      {
         if (_run is {Status: AuthoredQuestStatus.Active} && _paused != null) return $"a tale is under way ('{_run.QuestId}') and another waits ('{_paused.QuestId}')";
         if (!_quests.TryGetValue(questId ?? "", out AuthoredQuest q)) return $"no tale '{questId}' is loaded";
         if (q.OnScenario == null) return $"'{questId}' is not opened by a scenario (it has no on_scenario)";
         if (giver == null) return "no giver";
         if (!TryBind(q, giver, out Dictionary<string, string> ids, out Dictionary<string, string> names, out string whyNot)) return whyNot;

         var today = (int) CampaignTime.Now.ToDays;
         whyNot = AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, giver, today, bySummons: true));
         if (whyNot != null || dryRun) return whyNot;

         AuthoredQuestRun run = AuthoredQuestEngine.Start(q, ids, names, today);
         ModLog.Info($"[TALE] '{q.Id}' opened by a scenario with {giver.Name}.");
         // Gabriel (25/10/2026): the summons asked in role-play rather than by a popup: a tale with no invitation begins
         // at once, and its next stage (a scene, or a conversation) opens by itself.
         if (string.IsNullOrWhiteSpace(q.OnScenario.Invitation))
         {
            Accept(q, run);

            return null;
         }

         InformationManager.ShowInquiry(new InquiryData(
            q.Title,
            AuthoredQuestEngine.Fill(run, q.OnScenario.Invitation),
            true, true, AuthoredQuestEngine.Fill(run, q.OnScenario.Accept), AuthoredQuestEngine.Fill(run, q.OnScenario.Decline),
            () => Accept(q, run),
            () =>
            {
               if (string.IsNullOrWhiteSpace(q.OnScenario.DeclineEnding))
               {
                  _declined[q.Id] = today;
                  ModLog.Info($"[TALE] the player let '{q.Id}' pass.");

                  return;
               }

               // Gabriel (25/10/2026): a no that is a way the story goes; the giver is disappointed, and it is remembered.
               Decline(q, run, giver);
            }), true);

         return null;
      }

      /// <summary>
      ///   Gabriel (25/10/2026, "je devrais pouvoir vivre la nuit avec elle et reprendre ensuite la quete du loup lorsque je
      ///   suis libere de prison"): a scenario's tale beginning while another plays sets that one aside, its clock stopped,
      ///   its journal entry open. Its bands leave the map; they come back when it is taken up again.
      /// </summary>
      private void PauseUnderWay(AuthoredQuestRun incoming)
      {
         if (_run is not {Status: AuthoredQuestStatus.Active} || ReferenceEquals(_run, incoming) || _paused != null) return;
         ReleaseBands();
         TalePause.Pause(_run, (int) CampaignTime.Now.ToDays);
         _paused = _run;
         _run = null;
         Journal(_paused.QuestId)?.Log("This tale waits: other matters hold you for now.");
         ModLog.Info($"[TALE] '{_paused.QuestId}' set aside at stage {_paused.StageId} while another tale plays.");
      }

      /// <summary>The tale set aside takes up again where it stood, once nothing else plays and the player is free.</summary>
      private void ResumeIfFree(bool playerPrisoner)
      {
         if (!TalePause.ShouldResume(_paused != null, _run is {Status: AuthoredQuestStatus.Active}, playerPrisoner)) return;
         TalePause.Resume(_paused, (int) CampaignTime.Now.ToDays);
         _run = _paused;
         _paused = null;
         ModLog.Info($"[TALE] '{_run.QuestId}' taken up again at stage {_run.StageId}.");
         if (!_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return;
         Journal(q.Id)?.Log("You are free to take this tale up again.");
         AnnounceStage(AuthoredStageNotice.Text(q.Title, AuthoredQuestEngine.Fill(_run, AuthoredQuestEngine.Current(_run, q)?.Journal)));
      }

      /// <summary>The popup's no, when the tale makes it an ending: the tale is played to that ending at once.</summary>
      private void Decline(AuthoredQuest q, AuthoredQuestRun run, Hero giver)
      {
         PauseUnderWay(run);
         _run = run;
         _run.GiverRegardAtStart = RegardOf(giver);
         ModLog.Info($"[TALE] the player said no to '{q.Id}': ending '{q.OnScenario.DeclineEnding}'.");
         Apply(q, AuthoredQuestEngine.Declined(_run, q, (int) CampaignTime.Now.ToDays));
      }

      /// <summary>Self-test seam: a scenario tale opened and refused at its popup, through the real handling.</summary>
      internal string DebugDeclineFromScenario(string questId, Hero giver)
      {
         string whyNot = OpenFromScenario(questId, giver, dryRun: true);
         if (whyNot != null) return whyNot;

         AuthoredQuest q = _quests[questId];
         TryBind(q, giver, out Dictionary<string, string> ids, out Dictionary<string, string> names, out _);
         Decline(q, AuthoredQuestEngine.Start(q, ids, names, (int) CampaignTime.Now.ToDays), giver);

         return null;
      }

      /// <summary>
      ///   The Troy-inspired tale (Gabriel, 12/10/2026): she leaves her husband with the player. Her marriage ends through
      ///   the game's own dissolution (she goes back to her own house, single), she rides in the player's party as a guest
      ///   (the council hostages' way), may later be married as a Stolen Love divorcee is, and her husband holds a
      ///   StolenSpouse grudge sized as Stolen Love's. Nothing moves unless all of it can (TaleElopementPolicy).
      /// </summary>
      private CrTaleConsequence Elope(AuthoredConsequence c)
      {
         Hero her = Find(AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c.Who) ? "{giver}" : c.Who));
         Hero him = Find(AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c.Spouse) ? "{husband}" : c.Spouse));
         var facts = new ElopementFacts {
            SheIsMarriedToHim = her != null && him != null && her.Spouse == him && him.Spouse == her,
            BothInTheWorld = her != null && him != null && Mapping.HeroPresence.IsInTheWorld(her) && Mapping.HeroPresence.IsInTheWorld(him),
            EitherIsPrisoner = her?.IsPrisoner == true || him?.IsPrisoner == true, SheIsPregnant = her?.IsPregnant == true,
            SheLeadsHerHouse = her?.Clan != null && her.Clan.Leader == her,
            SheLeadsAParty = her?.PartyBelongedTo != null && her.PartyBelongedTo.LeaderHero == her, SheGoverns = her?.GovernorOf != null
         };
         string whyNot = TaleElopementPolicy.WhyNotElope(facts);
         if (whyNot != null)
         {
            ModLog.Info($"[TALE] '{_run?.QuestId}': {her?.Name} does not leave {him?.Name}: {whyNot}.");
            return Result(c, her?.StringId, him?.StringId, CrTaleConsequenceStatus.NotDone, whyNot);
         }

         if (TaleElopementPolicy.MustReleasePost(facts)) Marriage.MarriageDissolutionService.ReleaseStructuralRoles(her);
         if (!Marriage.MarriageDissolutionService.TryDissolve(her, him, out string reason))
         {
            ModLog.Info($"[TALE] '{_run?.QuestId}': {her.Name}'s marriage to {him.Name} could not end ({reason}); she stays.");
            return Result(c, her.StringId, him.StringId, CrTaleConsequenceStatus.NotDone, $"the marriage could not end ({reason})");
         }

         var today = (int) CampaignTime.Now.ToDays;
         if (MobileParty.MainParty != null) TaleWorlds.CampaignSystem.Actions.AddHeroToPartyAction.Apply(her, MobileParty.MainParty, false);
         int magnitude = Logic.StolenSpouseGrudgePolicy.Magnitude(him.GetRelation(her), him.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Mercy));
         Grudges.GrudgeBehavior.Instance?.TryPlant(him, Hero.MainHero, Logic.Grudges.GrudgeSource.StolenSpouse,
            $"the player took {her.Name}, my wife, from my own hall", magnitude, knownToPlayer: true, today: today);
         Mapping.MisdeedLedgerBehavior.Record(him.StringId, Logic.Trauma.MisdeedKind.StoleSpouse);
         Mapping.DeedRegardApplier.Apply(him, Logic.PlayerDeed.StoleSpouse); // Gabriel 23/10: every deed moves regard
         Marriage.NpcMarriageEstrangementBehavior.Instance?.RecordElopement(her.StringId, him.StringId, today);
         ModLog.Info($"[TALE] '{_run?.QuestId}': {her.Name} has left {him.Name} and rides with the player; {him.Name} holds a grudge ({magnitude}).");

         return Result(c, her.StringId, him.StringId, CrTaleConsequenceStatus.Done, null);
      }

      /// <summary>
      ///   The Troy-inspired tale (Gabriel, 12/10/2026: "Le retour de la Reine a son mari est la condition de paix"): she
      ///   goes back to her husband. The game's own marriage weds them again, and she leaves the player's party for his, or
      ///   for his house's home. Not when either is gone, captive, or wed to someone else (TaleElopementPolicy).
      /// </summary>
      private CrTaleConsequence ReturnSpouse(AuthoredConsequence c)
      {
         Hero her = Find(AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c.Who) ? "{giver}" : c.Who));
         Hero him = Find(AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c.Spouse) ? "{husband}" : c.Spouse));
         if (TryReturn(her, him, _run?.QuestId, firstAsked: true)) return Result(c, her?.StringId, him?.StringId, CrTaleConsequenceStatus.Done, null);

         bool waits = _returnsWaiting.ContainsKey($"{her?.StringId}|{him?.StringId}|{_run?.QuestId}");

         return Result(c, her?.StringId, him?.StringId, waits ? CrTaleConsequenceStatus.Waiting : CrTaleConsequenceStatus.NotDone, _lastReturnWhy);
      }

      /// <summary>Why the last return attempt did not happen (or waits), in plain words.</summary>
      private string _lastReturnWhy;

      /// <summary>
      ///   One attempt at her return (TaleElopementPolicy.DecideReturn): now, wait, or never. Gabriel's cr.testall on a new
      ///   game (13/10/2026): the game weds nobody whose party is in an army or a battle, and refused in silence while
      ///   the log claimed she had gone back. Now a passing reason sends her to his house's home and the wedding waits
      ///   (retried daily, RetryWaitingReturns); a refusal is said; success is checked, not assumed. True when done.
      /// </summary>
      private bool TryReturn(Hero her, Hero him, string questId, bool firstAsked)
      {
         ReturnDecision d = TaleElopementPolicy.DecideReturn(new ReturnFacts {
            BothInTheWorld = her != null && him != null && Mapping.HeroPresence.IsInTheWorld(her) && Mapping.HeroPresence.IsInTheWorld(him),
            SheIsSingle = her != null && her.Spouse == null, HeIsSingle = him != null && him.Spouse == null,
            EitherIsPrisoner = her?.IsPrisoner == true || him?.IsPrisoner == true,
            HeIsOnCampaign = him?.PartyBelongedTo?.Army != null || him?.PartyBelongedTo?.MapEvent != null,
            TheGameWouldWedThem = her != null && him != null && Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(her, him),
            SheLeadsHerHouse = her?.Clan != null && her.Clan.Leader == her
         });
         string key = $"{her?.StringId}|{him?.StringId}|{questId}";
         _lastReturnWhy = d.Why;

         if (d.Why != null)
         {
            if (d.WaitAndRetry)
            {
               if (firstAsked)
               {
                  _returnsWaiting[key] = (int) CampaignTime.Now.ToDays;
                  if (her != null && !her.IsPrisoner && her.PartyBelongedTo == MobileParty.MainParty && him?.Clan?.HomeSettlement != null)
                     TaleWorlds.CampaignSystem.Actions.TeleportHeroAction.ApplyImmediateTeleportToSettlement(her, him.Clan.HomeSettlement);
                  ModLog.Info($"[TALE] '{questId}': {her?.Name}'s return to {him?.Name} waits: {d.Why}. She goes to his house's home; tried again each day.");
               }

               return false;
            }

            _returnsWaiting.Remove(key);
            ModLog.Info($"[TALE] '{questId}': {her?.Name} is not returned to {him?.Name}: {d.Why}.");

            return false;
         }

         if (d.RestoreDirectly)
         {
            // The game's rule for NEW matches refused them (Gabriel's cr.testall, 13/10/2026: Susada and Arcor, no campaign
            // in the way). Its reasons are logged so the next report names the conjunct; the marriage the tale ended is
            // restored as MarriageAction would, she joining his house.
            var model = Campaign.Current.Models.MarriageModel;
            ModLog.Info($"[TALE] '{questId}': the game's match rule refused {her.Name} and {him.Name} (her clan {her.Clan?.Name}, leader {her.Clan?.Leader?.Name}; his {him.Clan?.Name}, leader {him.Clan?.Leader?.Name}; "
                      + $"she suitable {model.IsSuitableForMarriage(her)}, he suitable {model.IsSuitableForMarriage(him)}); the marriage is restored directly.");
            if (him.Clan != null && her.Clan != him.Clan) her.Clan = him.Clan;
            her.Spouse = him;
            TaleWorlds.CampaignSystem.Actions.ChangeRomanticStateAction.Apply(her, him, TaleWorlds.CampaignSystem.Romance.RomanceLevelEnum.Marriage);
         }
         else
            TaleWorlds.CampaignSystem.Actions.MarriageAction.Apply(her, him, false);

         if (her.Spouse != him)
         {
            _returnsWaiting.Remove(key);
            _lastReturnWhy = "the game did not wed them again";
            ModLog.Error($"[TALE] '{questId}': the game did not wed {her.Name} to {him.Name} again; she is not returned.");

            return false;
         }

         _returnsWaiting.Remove(key);
         if (him.PartyBelongedTo != null && him.PartyBelongedTo.LeaderHero == him)
            TaleWorlds.CampaignSystem.Actions.AddHeroToPartyAction.Apply(her, him.PartyBelongedTo, false);
         else if (him.Clan?.HomeSettlement != null)
            TaleWorlds.CampaignSystem.Actions.TeleportHeroAction.ApplyImmediateTeleportToSettlement(her, him.Clan.HomeSettlement);
         ModLog.Info($"[TALE] '{questId}': {her.Name} has gone back to {him.Name}.");

         return true;
      }

      /// <summary>Daily: each waiting return is tried again; one waiting past ReturnWaitDays is given up, in the log.</summary>
      private void RetryWaitingReturns()
      {
         int today = (int) CampaignTime.Now.ToDays;
         foreach (KeyValuePair<string, int> w in _returnsWaiting.ToList())
         {
            string[] p = w.Key.Split('|');
            if (p.Length < 3) { _returnsWaiting.Remove(w.Key); continue; }

            Hero her = Find(p[0]);
            Hero him = Find(p[1]);
            if (today - w.Value > ReturnWaitDays)
            {
               _returnsWaiting.Remove(w.Key);
               ModLog.Info($"[TALE] '{p[2]}': {her?.Name}'s return to {him?.Name} waited {ReturnWaitDays} days and is given up.");
               Settled(p, CrTaleConsequenceStatus.NotDone, $"waited {ReturnWaitDays} days and was given up");
               continue;
            }

            if (TryReturn(her, him, p[2], firstAsked: false)) Settled(p, CrTaleConsequenceStatus.Done, null);
            else if (!_returnsWaiting.ContainsKey(w.Key)) Settled(p, CrTaleConsequenceStatus.NotDone, _lastReturnWhy);
         }
      }

      /// <summary>tashmetu (15/10/2026): a return that waited has settled; mods hear it (CrTales.ConsequenceSettled).</summary>
      private static void Settled(string[] herHimQuest, CrTaleConsequenceStatus status, string reason)
      {
         // Only the player: the consequence names her and him (WhoId, OtherId); the tale's own role names are not kept.
         var roles = new Dictionary<string, string>();
         if (Hero.MainHero != null) roles["player"] = Hero.MainHero.StringId;
         CrTales.RaiseConsequenceSettled(new CrTaleProgress(herHimQuest[2], "settled", (int) CampaignTime.Now.ToDays, roles, null,
            new[] {new CrTaleConsequence("return_spouse", herHimQuest[0], herHimQuest[1], status, reason)}));
      }

      /// <summary>Self-test seam: one day passes for the tales, through the real daily handling.</summary>
      internal void DebugDailyTick() => OnDailyTick();

      /// <summary>Self-test seam: the returns waiting now ("herId|himId|questId").</summary>
      internal IReadOnlyCollection<string> DebugReturnsWaiting => _returnsWaiting.Keys.ToList();

      /// <summary>Self-test seam: one return attempt between these two, as the consequence makes it.</summary>
      internal bool DebugTryReturn(Hero her, Hero him) => TryReturn(her, him, "[testall]", firstAsked: true);

      /// <summary>
      ///   Gabriel (10/10/2026): a tale of intimacy ends on a chance of a child. The woman of the player and their partner
      ///   carries it, through the same conception as every intimate scene (PregnancyService: of age, not already with
      ///   child, Life and Death Cycle on); two women or two men conceive nothing.
      /// </summary>
      private CrTaleConsequence Conceive(AuthoredConsequence c)
      {
         (Hero mother, Hero father, string why) = ConceptionPlan(c, MBRandom.RandomFloat);
         if (mother == null)
         {
            ModLog.Info($"[TALE] no child from '{_run?.QuestId}': {why}.");

            return Result(c, AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c?.Who) ? "{giver}" : c.Who), Hero.MainHero?.StringId,
               CrTaleConsequenceStatus.NotDone, why);
         }

         string note = GameState.PregnancyService.TryConceive(mother, father, force: true);
         ModLog.Info($"[TALE] '{_run?.QuestId}' ends on a chance of a child: {note ?? $"{mother.Name} could not conceive now (age, a child already on the way, a recent birth, or the Life and Death Cycle off)"}.");

         return Result(c, mother.StringId, father?.StringId, note != null ? CrTaleConsequenceStatus.Done : CrTaleConsequenceStatus.NotDone,
            note != null ? null : "she could not conceive now");
      }

      /// <summary>Who would carry a child from this consequence on this roll, and who would father it; the reason otherwise.</summary>
      private (Hero Mother, Hero Father, string WhyNot) ConceptionPlan(AuthoredConsequence c, double roll)
      {
         Hero partner = Find(AuthoredQuestEngine.Bound(_run, string.IsNullOrWhiteSpace(c?.Who) ? "{giver}" : c.Who)), player = Hero.MainHero;
         if (partner == null || player == null) return (null, null, "the partner is not bound");

         ConceptionMother m = TaleConceptionPolicy.Mother(new ConceptionFacts {PlayerIsFemale = player.IsFemale, PartnerIsFemale = partner.IsFemale});
         if (m == ConceptionMother.None) return (null, null, "two women or two men");
         double chance = c?.Chance ?? TaleConceptionPolicy.DefaultChance;
         // Whole percent, culture-free: "P0" writes "50 %" on a French system (cr.testall tale_conception failed there, 12/10/2026).
         if (!TaleConceptionPolicy.Conceives(chance, roll)) return (null, null, $"the {(int) Math.Round(chance * 100)}% chance did not fall");

         return m == ConceptionMother.Partner ? (partner, player, null) : (player, partner, null);
      }

      /// <summary>Self-test seam: who would carry a child of this loaded tale's partner on this roll, without conceiving anything.</summary>
      internal string DebugConceptionPlan(string partnerId, double chance, double roll)
      {
         AuthoredQuestRun keep = _run;
         try
         {
            _run = new AuthoredQuestRun {QuestId = "cr_selftest_conception"};
            _run.BoundIds["giver"] = partnerId;
            (Hero mother, Hero father, string why) = ConceptionPlan(new AuthoredConsequence {Type = "conception", Chance = chance}, roll);

            return mother == null ? "none: " + why : $"mother {mother.StringId}, father {father.StringId}";
         }
         finally { _run = keep; }
      }

      /// <summary>The heroes a consequence names: a role, or "locals_of:{place}" (the notables of a bound place).</summary>
      private IEnumerable<string> Who(string who)
      {
         if (string.IsNullOrEmpty(who)) return Enumerable.Empty<string>();
         if (IsCrowd(who))
         {
            Settlement s = Settlement.Find(AuthoredQuestEngine.Bound(_run, who.Substring(AuthoredQuestEngine.LocalsOf.Length)) ?? "");

            return s?.Notables?.Where(n => n != null).Select(n => n.StringId).ToList() ?? new List<string>();
         }

         string id = AuthoredQuestEngine.Bound(_run, who);

         return id != null ? new[] {id} : Enumerable.Empty<string>();
      }

      /// <summary>Who a memory consequence reaches: its target, less anyone the same ending already names (a crowd only).</summary>
      private List<string> MemoryRecipients(string who, ISet<string> named)
         => Who(who).Where(id => !IsCrowd(who) || !named.Contains(id)).ToList();

      /// <summary>
      ///   Self-test seam (tashmetu, 20/10/2026): who an ending's memories reach, giver first, for a giver bound to a village,
      ///   when the ending gives the giver a memory of its own and speaks to the village's locals. Writes nothing.
      /// </summary>
      internal (List<string> giverMemory, List<string> locals) DebugEndingMemoryRecipients(Hero giver, Settlement village)
      {
         AuthoredQuestRun keep = _run;
         try
         {
            _run = new AuthoredQuestRun {QuestId = "cr_selftest_locals"};
            _run.BoundIds["giver"] = giver.StringId;
            _run.BoundIds["village"] = village.StringId;
            var outcome = new AuthoredOutcome {Memory = "the ending, as the giver saw it"};
            var crowd = new AuthoredConsequence {Type = "memory", Who = AuthoredQuestEngine.LocalsOf + "{village}", Text = "the ending, as the village heard it"};
            ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(_run, outcome, new[] {crowd});

            return (new List<string> {giver.StringId}, MemoryRecipients(crowd.Who, named));
         }
         finally { _run = keep; }
      }

      private static bool IsCrowd(string who) => who != null && who.StartsWith(AuthoredQuestEngine.LocalsOf);

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
      ///   The choices by words open in a conversation with this character (EncounterContext.StoryChoicesForInterpreter),
      ///   taught to whoever reads the turn's actions: the interpreter in Enhanced, the character's own call in Standard
      ///   (Gabriel, 25/10/2026), with the rule not to announce or steer toward them. Null when none is open.
      /// </summary>
      public string InterpreterChoicesFor(Hero hero)
      {
         if (hero == null || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return null;
         try { return AuthoredQuestPrompt.ForActionReader(_run, q, hero.StringId); }
         catch (Exception ex)
         {
            ModLog.Error($"[TALE] interpreter choices failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   One exchange of the conversation with this hero was played (their reply is in and its deeds applied): the tale's
      ///   arc counts it (TaleArc, Gabriel 25/10/2026). True when it was the arc's last: the conversation is to close now.
      /// </summary>
      internal bool ArcExchanged(Hero hero, Func<double> roll = null)
      {
         if (hero == null || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return false;
         try
         {
            string before = _run.ArcBeatId;
            bool closed = TaleArc.Exchanged(_run, q, hero.StringId, roll ?? (() => MBRandom.RandomFloat));
            if (_run.ArcDeclared != null) ModLog.Info($"[TALE] '{q.Id}': the arc's net decided '{_run.ArcDeclared}'; the next reply narrates it.");
            if (closed)
               ModLog.Info($"[TALE] '{q.Id}': the arc played its final beat '{_run.ArcBeatId}' after {_run.ArcExchanges} exchange(s); the conversation closes.");
            else if (_run.ArcBeatId != before)
               ModLog.Info($"[TALE] '{q.Id}': the arc moves to beat '{_run.ArcBeatId}' ({_run.ArcExchanges} exchange(s) so far).");

            return closed;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TALE] arc count failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The reader of the turn's actions reported a signal of the tale's arc (the tale_signal verb) in a conversation with
      ///   this character. Taken only while the arc plays and the signal is one it watches for now (TaleArc.SignalSeen).
      /// </summary>
      internal void ArcSignalsDropped() => _run?.ArcSignalsNow?.Clear();

      /// <summary>The reader of the turn's actions reported a signal of the arc; see TaleArc.SignalSeen.</summary>
      internal bool TakeSignal(Hero hero, string signalId)
      {
         if (hero == null || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return false;
         bool taken = TaleArc.SignalSeen(_run, q, hero.StringId, signalId);
         ModLog.Info(taken
            ? $"[TALE] '{q.Id}': the arc saw the signal '{signalId}' with {hero.Name}."
            : $"[TALE] '{q.Id}': signal '{signalId}' with {hero.Name} refused: not one the arc watches for now.");

         return taken;
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

         // tashmetu (07/10/2026): the interpreter wrote "choice_voice" for "voice"; the open choice it means is still taken.
         IReadOnlyList<AuthoredChoice> open = AuthoredQuestEngine.SaidChoicesFor(_run, q, hero.StringId);
         string id = SaidChoiceId.Match(choiceId, open.Select(x => x.Id).ToList()) ?? choiceId.Trim();
         AuthoredChoice c = open.FirstOrDefault(x => x.Id == id);
         string shown = AuthoredTaleImages.ImageFor(_run, q);
         AuthoredStep step = AuthoredQuestEngine.Said(_run, q, hero.StringId, id, (int) CampaignTime.Now.ToDays);
         if (c == null || step.Kind == AuthoredStepKind.None)
         {
            ModLog.Info($"[TALE] choice by words '{choiceId}' with {hero.Name} refused: not a declared, open choice said to them.");

            return null;
         }

         ModLog.Info($"[TALE] choice by words '{c.Id}' taken with {hero.Name}.");
         Apply(q, step);
         ShowStateImage(q, shown);

         return AuthoredQuestEngine.Fill(_run, string.IsNullOrWhiteSpace(c.Label) ? c.Id : c.Label);
      }

      /// <summary>
      ///   Gabriel (10/10/2026): stages that are states of one conversation, each with its picture. After a choice by words
      ///   moved the tale, the new stage's picture opens beside the chat still open, when it has one of its own.
      /// </summary>
      private void ShowStateImage(AuthoredQuest q, string shown)
      {
         string next = _run is {Status: AuthoredQuestStatus.Active} ? AuthoredTaleImages.ImageToShow(shown, AuthoredTaleImages.ImageFor(_run, q)) : null;
         if (next == null) return;
         _lastStateImage = next;
         string path = ImagePath(q, next);
         if (path == null) return;

         string title = q.Title;
         MainThreadDispatcher.Enqueue(() =>
         {
            if (ChatManager.IsOpen) ChatManager.ActiveViewModel?.ShowSceneImage(path, title);
            ModLog.Info($"[TALE] the state's picture {next} shown.");
         });
      }

      private string _lastStateImage;

      /// <summary>Self-test seam: the last picture a change of state called for (whether or not its file is there).</summary>
      internal string DebugLastStateImage => _lastStateImage;

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
            return giver.Issue != null
                   || Campaign.Current?.QuestManager?.Quests?.Any(x => x != null && x.IsOngoing && x.QuestGiver == giver && x is not AuthoredStoryQuest) == true;
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
         // Nothing plays but a tale waits: that is the one set aside.
         if (_run is not {Status: AuthoredQuestStatus.Active} && _paused != null)
         {
            _run = _paused;
            _paused = null;
         }

         if (_run == null) return;
         ReleaseBands();
         Journal(_run.QuestId)?.CloseAsCancelled(reason);
         ModLog.Info($"[TALE] '{_run.QuestId}' abandoned: {reason}");
         // tashmetu (15/10/2026): mods hear an abandoned tale too. Not the scenario rules: a rule naming the whole tale
         // would otherwise answer an abandon (a war pressed over a tale dropped from the console).
         if (_run.Status == AuthoredQuestStatus.Active)
            CrTales.RaiseEnded(new CrTaleProgress(_run.QuestId, "end:abandoned", (int) CampaignTime.Now.ToDays, TaleRoles(), _run.Flags, null));
         _run = null;
      }

      /// <summary>Self-test only: forgets that a quest was played or declined, so a test leaves no trace.</summary>
      // ── Goods (items in tales) ───────────────────────────────────────────

      private static bool IsItem(string id) => !string.IsNullOrEmpty(id) && MBObjectManager.Instance?.GetObject<ItemObject>(id) != null;

      private static bool IsCategory(string id) => !string.IsNullOrEmpty(id) && MBObjectManager.Instance?.GetObject<ItemCategory>(id) != null;

      /// <summary>
      ///   The goods the tale asks this person to be handed now (a `bring` open on the stage or a choice): the key the bag's
      ///   supply picker matches the party's goods against, and the count. False when the tale asks nothing of them.
      /// </summary>
      internal bool TryDeliveryFor(Hero hero, out string key, out int count)
      {
         key = null;
         count = 0;
         if (hero == null || _run is not {Status: AuthoredQuestStatus.Active} || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return false;
         AuthoredWay way = AuthoredQuestEngine.DeliveryFor(_run, q, hero.StringId);
         key = way == null ? null : TaleItemPolicy.BringKey(way.Objective.Item, IsCategory, IsItem);
         count = way?.Objective.Count ?? 0;

         return key != null && count > 0;
      }

      /// <summary>How many of the party's goods answer this key (a tale's `bring`): the bag offers the hand-over only when they reach the count.</summary>
      internal static int PartyHolds(string key)
         => MobileParty.MainParty?.ItemRoster?.Where(e => e.EquipmentElement.Item != null && e.Amount > 0 && Items.SupplyItemMatcher.Matches(e.EquipmentElement.Item, key))
                                             .Sum(e => e.Amount) ?? 0;

      /// <summary>
      ///   The goods the player chose in the bag's picker, handed to this person (tashmetu, 20/10/2026): exactly the count
      ///   asked leaves the party and the tale takes the way the `bring` leads, or, short of it, nothing leaves and the tale
      ///   waits (gating before declaration: the tale never says he brought what he did not). Says what happened.
      /// </summary>
      internal bool HandOver(Hero hero, IReadOnlyList<GameState.ItemOffer> offers, out string message)
      {
         message = null;
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;
         if (roster == null || !TryDeliveryFor(hero, out string key, out int asked) || !_quests.TryGetValue(_run.QuestId, out AuthoredQuest q)) return false;

         string label = Items.SupplyItemMatcher.Label(key);
         var held = new List<(ItemObject item, int count)>();
         foreach (GameState.ItemOffer offer in offers ?? new List<GameState.ItemOffer>())
         {
            ItemRosterElement el = roster.FirstOrDefault(e => e.EquipmentElement.Item?.StringId == offer.ItemId && e.Amount > 0);
            ItemObject item = el.EquipmentElement.Item;
            if (item == null || offer.Count <= 0 || !Items.SupplyItemMatcher.Matches(item, key) || held.Any(h => h.item == item)) continue;
            held.Add((item, Math.Min(offer.Count, el.Amount)));
         }

         int offered = held.Sum(h => h.count);
         if (TaleItemPolicy.Taken(offered, asked) == 0)
         {
            message = $"{hero.Name}: that is only {offered} of the {asked} {label} I asked for.";

            return false;
         }

         int remaining = asked;
         foreach ((ItemObject item, int c) in held)
         {
            if (remaining <= 0) break;
            int take = Math.Min(c, remaining);
            roster.AddToCounts(item, -take);
            remaining -= take;
         }

         message = $"You hand {asked} {label} to {hero.Name}.";
         ModLog.Info($"[TALE] '{q.Id}': {asked} {label} handed to {hero.Name} ({key}).");
         Apply(q, AuthoredQuestEngine.Delivered(_run, q, hero.StringId, (int) CampaignTime.Now.ToDays));

         return true;
      }

      /// <summary>
      ///   What the tale asks this person for when this item is among the goods (a give_item worded in the chat, Gabriel
      ///   21/10/2026): the count asked, how many of the goods the party carries and their name; 0 when it asks none of it.
      /// </summary>
      internal int AskedOfGift(Hero hero, string itemId, out int held, out string label)
      {
         held = 0;
         label = null;
         ItemObject item = string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
         if (item == null || !TryDeliveryFor(hero, out string key, out int asked) || !Items.SupplyItemMatcher.Matches(item, key)) return 0;
         held = PartyHolds(key);
         label = Items.SupplyItemMatcher.Label(key);

         return asked;
      }

      /// <summary>The tale's goods handed over from all the party carries of them, the item named first (a worded hand-over).</summary>
      internal bool HandOverHeld(Hero hero, string itemId, out string message)
      {
         message = null;
         if (!TryDeliveryFor(hero, out string key, out _)) return false;
         List<GameState.ItemOffer> offers = MobileParty.MainParty?.ItemRoster?
                                                       .Where(e => e.EquipmentElement.Item != null && e.Amount > 0 && Items.SupplyItemMatcher.Matches(e.EquipmentElement.Item, key))
                                                       .OrderByDescending(e => e.EquipmentElement.Item.StringId == itemId)
                                                       .Select(e => new GameState.ItemOffer(e.EquipmentElement.Item.StringId, e.Amount)).ToList();

         return HandOver(hero, offers, out message);
      }

      /// <summary>An `item` consequence: the item the file names, given to the player's party (tashmetu, 20/10/2026).</summary>
      private CrTaleConsequence GiveItem(AuthoredConsequence c)
      {
         string id = TaleItemPolicy.GiftItemId(c.Item, IsItem);
         ItemObject item = id == null ? null : MBObjectManager.Instance.GetObject<ItemObject>(id);
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;
         if (item == null || roster == null)
            return Result(c, Hero.MainHero?.StringId, null, CrTaleConsequenceStatus.NotDone, item == null ? $"no item '{c.Item}' in this game" : "the player has no party");

         int count = Math.Max(1, Math.Min(c.Count, TaleItemPolicy.MaxCount));
         roster.AddToCounts(item, count);
         string notice = count == 1 ? $"You receive {item.Name}." : $"You receive {item.Name} x{count}.";
         MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(notice));
         ChatManager.ActiveViewModel?.AppendSystemMessage(notice, true);
         ModLog.Info($"[TALE] '{_run?.QuestId}': the player is given {count} {id}.");

         return Result(c, Hero.MainHero?.StringId, null, CrTaleConsequenceStatus.Done, null);
      }

      /// <summary>cr.testall tale_items: an `item` consequence through the real giving.</summary>
      internal CrTaleConsequence DebugGiveItem(AuthoredConsequence c) => GiveItem(c);

      internal void ForgetPlayed(string questId)
      {
         _done.Remove(questId);
         _declined.Remove(questId);
         _playedOn.Remove(questId);
      }

      internal AuthoredQuestRun Offer => _offer;

      /// <summary>The tale set aside while a scenario's tale plays, or null.</summary>
      internal AuthoredQuestRun PausedRun => _paused;

      /// <summary>Self-test seam: the hourly check that takes a tale set aside up again, the player free or not.</summary>
      internal void DebugResume(bool playerFree) => ResumeIfFree(!playerFree);

      /// <summary>Self-test seam: the daily reconciling of the journal, and whether a tale's entry is open.</summary>
      internal void DebugReconcileJournal() => ReconcileJournal();

      internal bool DebugJournalOpen(string questId) => Journal(questId) != null;

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

      /// <summary>Self-test seam: could this character give this loaded tale now (cr.story_check's answer)?</summary>
      internal string DebugEligibility(Hero hero, string questId)
         => _quests.TryGetValue(questId, out AuthoredQuest q) ? Eligibility(hero, q) : "not loaded";

      /// <summary>Self-test seam: takes a tale the test loaded out of the tales in play.</summary>
      internal void DebugForgetTale(string questId) => _quests.Remove(questId);

      /// <summary>
      ///   Self-test seam (cr.testall tale_opened_by_scenario): opens a scenario tale as its popup's yes would, the tale's
      ///   own rules checked as OPEN_TALE checks them, without the popup and without staging the conversation.
      /// </summary>
      internal string DebugOpenFromScenario(string questId, Hero giver)
      {
         string whyNot = OpenFromScenario(questId, giver, dryRun: true);
         if (whyNot != null) return whyNot;

         AuthoredQuest q = _quests[questId];
         TryBind(q, giver, out Dictionary<string, string> ids, out Dictionary<string, string> names, out _);
         _stagingHeld = true;
         try { Accept(q, AuthoredQuestEngine.Start(q, ids, names, (int) CampaignTime.Now.ToDays)); }
         finally { _stagingHeld = false; }

         return null;
      }

      /// <summary>Self-test seam: the conversation with this hero ended, through the real handling.</summary>
      internal void DebugConversationEnded(Hero hero) => OnConversationEnded(new CrConversationLog(hero?.StringId, null, (int) CampaignTime.Now.ToDays, new List<CrTranscriptLine>(), null));

      /// <summary>Self-test seam: forgets which pictures of this tale's pools were shown, so a test leaves no trace.</summary>
      internal void DebugForgetPools(string questId)
      {
         foreach (string key in _poolShown.Keys.Where(k => k.StartsWith(questId + "/", StringComparison.Ordinal)).ToList()) _poolShown.Remove(key);
      }

      /// <summary>Self-test seam: why this loaded tale would not be offered now, the giver and the dice aside; null when nothing stands in the way.</summary>
      internal string DebugWhyNotOffered(string questId)
         => _quests.TryGetValue(questId, out AuthoredQuest q) ? AuthoredQuestOfferPolicy.WhyNot(FactsFor(q, null, (int) CampaignTime.Now.ToDays)) : "not loaded";

      /// <summary>Self-test seam: binds a loaded tale's roles around this giver, as an offer would, without offering it.</summary>
      internal string DebugBind(string questId, Hero giver, out Dictionary<string, string> ids)
      {
         ids = null;
         if (!_quests.TryGetValue(questId, out AuthoredQuest q)) return "not loaded";

         return TryBind(q, giver, out ids, out _, out string whyNot) ? null : whyNot;
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
