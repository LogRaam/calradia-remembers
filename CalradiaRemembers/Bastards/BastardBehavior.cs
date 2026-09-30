// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Bastards
{
   /// <summary>
   ///   Per-bastard blackmail clock: the season a demand was raised (-1 none), the gold it asks, and the season paid
   ///   silence runs to.
   /// </summary>
   internal sealed class BlackmailState
   {
      public int DemandAmount { get; set; }
      public int DemandSeason { get; set; } = -1;
      public int SilentUntil { get; set; } = -1;

      /// <summary>Day the current demand's letter was queued (romance audit M-G6b): the clock starts only once a
      /// Blackmail letter sent on or after this day has actually reached the player. -1 when no demand is in flight.</summary>
      public int DemandLetterSentDay { get; set; } = -1;
   }

   /// <summary>
   ///   Tracks the player's bastards and drives their consequences each season: discovery, the mother's
   ///   recurring blackmail, and (at coming of age) a prestige-gated claim. Minimal persisted state; age and
   ///   the seasonal odds are read live from the heroes.
   /// </summary>
   public sealed class BastardBehavior : CampaignBehaviorBase
   {
      private const int ClaimHostility = 40; // the grown bastard turns a clear enemy of the player's house
      private const int ClaimMagnitude = 60; // a dynastic claim travels far
      private const int DemandLetterDeliveryDays = 3; // courier time for a blackmail demand letter

      private const string SaveKey = "CalradiaRemembers_Bastards_v1bytes";
      private const string SaveKeyBlackmail = "CalradiaRemembers_BastardsBlackmail_v1bytes";
      private const string LastSeasonSaveKey = "CalradiaRemembers_BastardLastSeason_v1"; // minor: persist to avoid a double seasonal roll on reload

      private const int ScandalInfluenceLoss = 10; // the political cost at court

      // Tuning (flagged for in-game ratification).
      private const int ScandalMagnitude = 40; // how far the gossip carries (0..100)
      private const int ScandalRenownLoss = 5; // the dishonour to the player's name

      public static BastardBehavior Instance;

      // childId -> discovered (one-way). Age is read live from the hero.
      private Dictionary<string, bool> _bastards = new();

      // childId -> the mother's blackmail clock.
      private Dictionary<string, BlackmailState> _blackmail = new();
      private int _lastEvaluatedSeason = -1;

      public BastardBehavior()
      {
         Instance = this;
      }

      /// <summary>The tracked bastards (child id → discovered) — for the cr.bastards diagnostic.</summary>
      public IReadOnlyDictionary<string, bool> Bastards => _bastards;

      private static int CurrentSeason => (int) CampaignTime.Now.ToSeasons;

      /// <summary>
      ///   The gold an NPC could demand for her silence right now, or 0 — she is the mother of a still-concealed
      ///   bastard of the player's and holds leverage (pure <see cref="BastardBlackmailPolicy" />). A query.
      /// </summary>
      public int BlackmailDemandOver(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return 0;

         string childId = _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).FirstOrDefault(id => FindHero(id)?.Mother == npc);

         if (childId == null) return 0;
         if (!BastardBlackmailPolicy.HoldsLeverage(new BastardBlackmailPolicy.LeverageFacts {BastardConcealed = true, MotherAlive = npc.IsAlive, MotherIsPlayerSpouse = npc == Hero.MainHero.Spouse})) return 0;
         if (MotherDisposition(npc) != BastardMotherDisposition.Mercenary) return 0; // only a scheming mother extorts

         return BastardBlackmailPolicy.DemandAmount(Hero.MainHero.Gold);
      }

      /// <summary>A short blackmail-state label for the cr.bastards diagnostic.</summary>
      public string BlackmailStatusOf(string childId)
      {
         if (childId == null || !_blackmail.TryGetValue(childId, out BlackmailState s)) return "no demand";
         if (s.DemandSeason >= 0) return $"DEMAND@S{s.DemandSeason}";
         if (s.SilentUntil > CurrentSeason) return $"paid-silence→S{s.SilentUntil}";

         return "no demand";
      }

      /// <summary>
      ///   Force the coming-of-age claim path for the given verdict (console testing): track the hero, out the
      ///   secret, and apply the claim (hostility + world-event) when it presses.
      /// </summary>
      public void DebugClaim(Hero bastard, bool pressesClaim)
      {
         if (bastard == null) return;

         DebugTrack(bastard);
         _bastards[bastard.StringId] = true;
         if (pressesClaim) ApplyClaim(bastard);
      }

      /// <summary>Run the seasonal evaluation (discovery + blackmail) immediately (console testing).</summary>
      public void DebugEvaluateSeason() => EvaluateSeason();

      /// <summary>Force-raise a blackmail demand on a tracked bastard NOW + send the demand letter (console testing).</summary>
      public void DebugRaiseDemand(Hero child)
      {
         if (child == null) return;
         DebugTrack(child);

         BlackmailState bs = GetBlackmail(child.StringId);
         bs.DemandSeason = CurrentSeason;
         bs.DemandAmount = BastardBlackmailPolicy.DemandAmount(Hero.MainHero?.Gold ?? 0);

         if (child.Mother != null) SendDemandLetter(child.Mother, child, bs.DemandAmount);
      }

      /// <summary>The culture-scaled scandal magnitude, exposed for the console self-test.</summary>
      public int DebugScaledScandalMagnitude() => ScaledScandalMagnitude();

      // ── Debug seams (console tests / diagnostics) ─────────────────────────

      /// <summary>Force-tag a hero as a concealed player bastard (console testing, no real birth needed).</summary>
      public void DebugTrack(Hero child)
      {
         if (child != null && !_bastards.ContainsKey(child.StringId)) _bastards[child.StringId] = false;
      }

      /// <summary>
      ///   Brings a hidden bastard to light: marks it a known fact, records the scandal into the world-event
      ///   store (rumour + chronicle), and dents the player's standing. Returns false for an untracked hero.
      /// </summary>
      public bool Discover(Hero child)
      {
         if (child == null || !_bastards.ContainsKey(child.StringId)) return false;

         _bastards[child.StringId] = true;
         _blackmail.Remove(child.StringId); // once it is out, there is nothing left to extort
         RecordScandal(child);

         // The dishonour cuts as deep as the player's CULTURE makes it (monogamous → grave, permissive → mild).
         JealousyOutrage outrage = PlayerCultureOutrage();
         int renown = BastardScandalPolicy.Scale(ScandalRenownLoss, outrage);
         int influence = BastardScandalPolicy.Scale(ScandalInfluenceLoss, outrage);

         if (Hero.MainHero?.Clan != null)
         {
            GameState.RenownPenalty.Apply(Hero.MainHero, renown); // a negative GainRenownAction is a no-op
            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -influence);
         }

         ModLog.Info($"[BASTARD] discovered: {child.Name} — scandal recorded ({outrage} culture), renown-{renown} influence-{influence}.");

         return true;
      }

      /// <summary>
      ///   True when this NPC is the mother of a still-concealed bastard of the player's (gates the conversation
      ///   teaching).
      /// </summary>
      public bool IsBastardMotherOfHidden(Hero npc)
         => npc != null && _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).Any(id => FindHero(id)?.Mother == npc);

      public bool IsDiscovered(string childId)
         => childId != null && _bastards.TryGetValue(childId, out bool discovered) && discovered;

      public bool IsTracked(string childId) => childId != null && _bastards.ContainsKey(childId);

      /// <summary>
      ///   The mother's disposition toward the player (trait- and regard-driven; pure <see cref="BastardMotherPolicy" />
      ///   ).
      /// </summary>
      public BastardMotherDisposition MotherDisposition(Hero mother)
      {
         int relation = mother != null && Hero.MainHero != null
            ? mother.GetRelation(Hero.MainHero)
            : 0;

         return BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = TraitLevel(mother, DefaultTraits.Honor), Calculating = TraitLevel(mother, DefaultTraits.Calculating), RelationWithPlayer = relation});
      }

      /// <summary>
      ///   The player meets the demands an NPC holds over them: pays each (gold to her) and buys a season of
      ///   silence (she returns when it lapses). Returns how many demands were settled and the total paid.
      /// </summary>
      public (int settled, int goldPaid) Pay(Hero mother)
      {
         if (mother == null || Hero.MainHero == null) return (0, 0);
         if (MotherDisposition(mother) != BastardMotherDisposition.Mercenary) return (0, 0); // a kinder mother has nothing to buy off

         int season = CurrentSeason;
         int settled = 0,
             gold = 0;

         foreach (string childId in _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList())
         {
            Hero child = FindHero(childId);

            if (child?.Mother != mother) continue;

            BlackmailState bs = GetBlackmail(childId);

            if (bs.DemandSeason < 0) continue; // only an active demand can be paid off

            int amount = bs.DemandAmount > 0
               ? bs.DemandAmount
               : BastardBlackmailPolicy.DemandAmount(Hero.MainHero.Gold);
            // Minor 13 (romance audit): only a demand actually PAID buys silence. The settlement used to fire even
            // when the player could not afford it, clearing the demand for free; now an unaffordable demand simply
            // stays outstanding.
            if (Hero.MainHero.Gold < amount) continue;

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, mother, amount);
            gold += amount;
            bs.DemandSeason = -1;
            bs.DemandAmount = 0;
            bs.SilentUntil = BastardBlackmailPolicy.SilenceUntil(season);
            settled++;
         }

         if (settled > 0)
            ModLog.Info($"[BASTARD] player paid {mother.Name} {gold} denars for silence over {settled} bastard(s) until season {BastardBlackmailPolicy.SilenceUntil(season)}.");

         return (settled, gold);
      }

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
      }

      /// <summary>
      ///   M-G4: when the player marries the other parent of a still-concealed bastard, that child is legitimated
      ///   by the marriage, so it is no longer a secret to be discovered nor leverage to be sold. Drops it from
      ///   the bastard register and clears any blackmail over it. Fires while the spouse is still resolvable as
      ///   the parent (BeforeHeroesMarried); only the player's own marriages matter here.
      /// </summary>
      private void OnBeforeHeroesMarried(Hero firstHero, Hero secondHero, bool showNotification)
      {
         Hero player = Hero.MainHero;
         if (player == null) return;

         Hero newSpouse = firstHero == player
            ? secondHero
            : secondHero == player
               ? firstHero
               : null;

         if (newSpouse == null) return; // not the player's own marriage

         foreach (string childId in _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList())
         {
            Hero child = FindHero(childId);
            if (child == null) continue;

            Hero otherParent = child.Father == player
               ? child.Mother
               : child.Mother == player
                  ? child.Father
                  : null;

            if (otherParent == null || otherParent != newSpouse) continue; // the parent being wed is not this child's other parent

            _bastards.Remove(childId);
            _blackmail.Remove(childId);
            ModLog.Info($"[BASTARD] {child.Name} is legitimated by the player's marriage to {newSpouse.Name}; no longer a concealed bastard (M-G4).");
         }
      }

      /// <summary>
      ///   The prestige-gate verdict for a grown bastard's claim on the FATHER's (player's) clan: a child of a
      ///   grander maternal house follows that line; an equal-or-lesser house breeds a claimant.
      /// </summary>
      public BastardClaimVerdict ResolveClaimVerdict(Hero bastard)
         => BastardPrestigePolicy.Resolve(ClanPrestige(bastard?.Clan), ClanPrestige(Hero.MainHero?.Clan));

      /// <summary>
      ///   The largest OUTSTANDING blackmail demand this mother currently holds against the player (0 when she
      ///   holds none). Romance audit C4: a payment that falls short of an active demand still moves the gold
      ///   but buys nothing, so the caller must be able to tell the player how much short they were rather than
      ///   silently pocketing the coin. Reads the same demand state <see cref="SettleByPayment" /> settles on.
      /// </summary>
      public int OutstandingDemand(Hero mother)
      {
         if (mother == null || Hero.MainHero == null) return 0;

         var max = 0;
         foreach (string childId in _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList())
         {
            Hero child = FindHero(childId);

            if (child?.Mother != mother) continue;

            BlackmailState bs = GetBlackmail(childId);

            if (bs.DemandSeason >= 0 && bs.DemandAmount > max) max = bs.DemandAmount;
         }

         return max;
      }

      /// <summary>
      ///   When the player gives a mercenary bastard-mother at least her standing demand (e.g. via the Give Gold
      ///   button), buy her silence for the grace period — no extra transfer, the gold already moved. Returns
      ///   true when a demand was settled. Called from the gold-payment path so paying her IS settling the secret.
      /// </summary>
      public bool SettleByPayment(Hero mother, int amountPaid)
      {
         if (mother == null || Hero.MainHero == null || amountPaid <= 0) return false;
         if (MotherDisposition(mother) != BastardMotherDisposition.Mercenary) return false;

         int season = CurrentSeason;
         var settled = false;

         foreach (string childId in _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList())
         {
            Hero child = FindHero(childId);

            if (child?.Mother != mother) continue;

            BlackmailState bs = GetBlackmail(childId);

            if (bs.DemandSeason < 0 || amountPaid < bs.DemandAmount) continue;

            bs.DemandSeason = -1;
            bs.DemandAmount = 0;
            bs.SilentUntil = BastardBlackmailPolicy.SilenceUntil(season);
            settled = true;
         }

         if (settled) ModLog.Info($"[BASTARD] payment of {amountPaid} to {mother.Name} bought her silence.");

         return settled;
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] a = ToBytes(_bastards);
            byte[] b = ToBytes(_blackmail);
            try { dataStore.SyncData(SaveKey, ref a); }
            catch (Exception ex) { ModLog.Error($"[BASTARD] save failed: {ex.GetType().Name}: {ex.Message}"); }

            try { dataStore.SyncData(SaveKeyBlackmail, ref b); }
            catch (Exception ex) { ModLog.Error($"[BASTARD] blackmail save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] a = null,
                   b = null;
            try { dataStore.SyncData(SaveKey, ref a); }
            catch
            {
               /* key absent in older saves */
            }

            try { dataStore.SyncData(SaveKeyBlackmail, ref b); }
            catch
            {
               /* key absent in older saves */
            }

            _bastards = FromBytes<Dictionary<string, bool>>(a);
            _blackmail = FromBytes<Dictionary<string, BlackmailState>>(b);
         }

         // Minor (romance audit): persist the last-evaluated season so a save/load does not reset it to -1 and
         // re-run the same season's discovery/blackmail pass (a double roll on reload). Additive key: an older
         // save simply lacks it and keeps -1 (one harmless re-eval on its first load after this fix).
         try { dataStore.SyncData(LastSeasonSaveKey, ref _lastEvaluatedSeason); }
         catch { /* key absent in older saves */ }
      }

      #region private

      private static int ClanPrestige(Clan clan)
      {
         if (clan == null) return 0;
         Logic.Stature house = Mapping.StatureReader.OfClan(clan);

         return BastardPrestigePolicy.PrestigeScore(house.ClanTier, (int) clan.Renown, house.IsRuler);
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      /// <summary>
      ///   True once a blackmail-demand letter from <paramref name="mother" /> sent on or after
      ///   <paramref name="sinceDay" /> (this demand's own letter) has actually reached the player, and was not
      ///   lost (romance audit M-G6b). The sinceDay filter is the C2 lesson: a stale delivered letter from a
      ///   previous demand cycle must not count as the current one arriving.
      /// </summary>
      private static bool BlackmailLetterDelivered(Hero mother, int sinceDay)
      {
         if (mother == null) return false;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(mother.StringId);
         if (profile?.SentLetters == null) return false;

         return profile.SentLetters.Any(l => l.Reason == LetterReason.Blackmail
                                              && l.SentOnDay >= sinceDay
                                              && l.IsDelivered
                                              && !l.IsLost);
      }

      private static T FromBytes<T>(byte[] bytes) where T : new()
      {
         if (bytes is not {Length: > 0}) return new T();

         try { return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes)) ?? new T(); }
         catch (Exception ex)
         {
            ModLog.Error($"[BASTARD] load failed — dropped for this load. {ex.GetType().Name}: {ex.Message}");

            return new T();
         }
      }

      /// <summary>A player-parented child born outside any marriage (romance audit M-G2: a posthumous heir of a
      /// marriage the spouse died out of is NOT a bastard, even though the player's Spouse now reads null).</summary>
      private static bool IsPlayersBastard(Hero child)
      {
         var player = Hero.MainHero;
         bool playerIsParent = child.Father == player || child.Mother == player;

         if (!playerIsParent) return false;

         Hero otherParent = child.Father == player
            ? child.Mother
            : child.Father;

         bool formerSpouse = otherParent != null && (player.ExSpouses?.Contains(otherParent) ?? false);

         return BastardLegitimacyPolicy.IsPlayersBastard(new BastardLegitimacyPolicy.ParentageFacts {PlayerIsParent = true, OtherParentExists = otherParent != null, OtherParentIsCurrentSpouse = otherParent == player.Spouse, OtherParentIsFormerSpouse = formerSpouse, OtherParentAlive = otherParent != null && otherParent.IsAlive});
      }

      private static string MotherNoteContent(Hero child, BastardMotherDisposition disposition)
      {
         string c = child.Name?.ToString() ?? "our child";

         return disposition switch {
            BastardMotherDisposition.Longing =>
               $"Not a day passes that I do not think of you. {c} grows quickly and asks after a father I cannot name. " +
               "Come to us — I would have you know your child, and I confess I would have far more than that, if you would let me.",
            BastardMotherDisposition.Resentful =>
               $"You have a child, {c}, and a duty you pretend not to see. I ask nothing of your heart — you have shown me its " +
               "measure — only that you not forget the one thing of yours I am left to raise alone.",
            _ =>
               $"I write to tell you of {c}, who is well, and to ask what little you can spare for the child's keep. I make no " +
               "demands and keep your name to myself; I only thought you should know."
         };
      }

      /// <summary>The player culture's outrage at infidelity, which scales how grave a discovered bastard is.</summary>
      private static JealousyOutrage PlayerCultureOutrage()
         => JealousyCultureLoader.GetCulture(Hero.MainHero?.Culture?.Name?.ToString()).Outrage;

      private static void RecordClaim(Hero bastard)
      {
         var store = WorldEventStore.Instance;

         if (store == null || Hero.MainHero == null) return;

         string parent = Hero.MainHero.Name?.ToString() ?? "the lord";
         var actors = new List<string> {bastard.StringId, Hero.MainHero.StringId};

         var factions = new List<string>();
         if (Hero.MainHero.MapFaction != null) factions.Add(Hero.MainHero.MapFaction.StringId);
         if (bastard.MapFaction != null) factions.Add(bastard.MapFaction.StringId);

         var today = (int) CampaignTime.Now.ToDays;
         store.Record(new WorldEvent {
            Type = WorldEventType.Scandal,
            Day = today,
            Magnitude = ClaimMagnitude,
            InvolvesPlayer = true,
            ActorIds = actors,
            FactionIds = factions,
            Summary = $"{bastard.Name}, natural-born child of {parent}, has come of age and presses a claim against their house.",
            ChronicleLine = $"{bastard.Name}, my own natural child, came of age and turned against my house, pressing a claim upon it"
         });

         // M-G5 (regard audit): plant the BastardClaim grudge LIVE too. Before, only claims recovered by the
         // one-shot backfill ever formed one, so a claim pressed TODAY left the bastard with no grudge, the system
         // blind to one of its most charged events. Anchored on the SAME day as the WorldEvent above, so the
         // backfill (which reads that event) cannot double-count it (holder+source+day dedup).
         GrudgeBehavior.Instance?.TryPlant(bastard, Hero.MainHero, GrudgeSource.BastardClaim,
            $"{bastard.Name}, natural-born child of {parent}, pressed a claim against their house.",
            GrudgeBackfillRules.BastardClaimMagnitude, knownToPlayer: true, today);
      }

      private static void RecordScandal(Hero child)
      {
         var store = WorldEventStore.Instance;

         if (store == null) return;

         string motherName = child.Mother?.Name?.ToString() ?? "an unknown woman";
         string fatherName = child.Father?.Name?.ToString() ?? "an unknown man";

         var actors = new List<string> {child.StringId};
         if (child.Mother != null) actors.Add(child.Mother.StringId);
         if (child.Father != null) actors.Add(child.Father.StringId);

         var factions = new List<string>();
         if (Hero.MainHero?.MapFaction != null) factions.Add(Hero.MainHero.MapFaction.StringId);
         if (child.Mother?.MapFaction != null) factions.Add(child.Mother.MapFaction.StringId);

         store.Record(new WorldEvent {
            Type = WorldEventType.Scandal,
            Day = (int) CampaignTime.Now.ToDays,
            Magnitude = ScaledScandalMagnitude(),
            InvolvesPlayer = true,
            ActorIds = actors,
            FactionIds = factions,
            Summary = $"Word spreads that {child.Name} is the natural child of {fatherName} and {motherName}.",
            ChronicleLine = $"a whispered scandal: {child.Name}, born of {motherName} out of wedlock, was named {fatherName}'s own"
         });
      }

      /// <summary>The scandal's reach (magnitude), scaled by the player culture's outrage at infidelity.</summary>
      private static int ScaledScandalMagnitude()
         => BastardScandalPolicy.Scale(ScandalMagnitude, PlayerCultureOutrage());

      /// <summary>Queues a blackmail demand letter from the mother to the player (delivered via the normal courier path).</summary>
      private static void SendDemandLetter(Hero mother, Hero child, int demand)
      {
         if (mother == null || child == null) return;
         if (!Mapping.InitiativeGate.Allows(mother, Logic.InitiativeKind.Letter, log: true)) return;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null) return;

         NpcProfile profile = store.Get(mother.StringId) ?? HeroProfileMapper.FromHero(mother);
         var today = (int) CampaignTime.Now.ToDays;

         profile.SentLetters.Add(new PendingLetter {
            SenderId = mother.StringId,
            SenderName = mother.Name?.ToString(),
            Reason = LetterReason.Blackmail,
            // M-G6: the deadline in the prose must match the real mechanic (DemandDeadlineSeasons), not a vague
            // "by season's end" that undersold the actual window and blindsided the player when it did not lapse then.
            Content = $"You have not forgotten {child.Name}, I think — the child we made between us. The realm does not yet know whose " +
                      $"it is, and need not, so long as you are generous. Send me {demand} denars and my tongue stays still. " +
                      $"Disappoint me, and before {BastardBlackmailPolicy.DemandDeadlineSeasons} seasons are out, all of Calradia will know the truth.",
            SentOnDay = today,
            DeliveryDay = today + DemandLetterDeliveryDays,
            IsDelivered = false
         });

         store.Set(profile);
         ModLog.Info($"[BASTARD] blackmail demand letter queued: {mother.Name} → player (~{demand} denars, due day {today + DemandLetterDeliveryDays}).");
      }

      private static void SendMotherNote(Hero mother, Hero child, BastardMotherDisposition disposition)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null || mother == null) return;
         if (!Mapping.InitiativeGate.Allows(mother, Logic.InitiativeKind.Letter, log: true)) return;

         NpcProfile profile = store.Get(mother.StringId) ?? HeroProfileMapper.FromHero(mother);
         var today = (int) CampaignTime.Now.ToDays;

         profile.SentLetters.Add(new PendingLetter {
            SenderId = mother.StringId,
            SenderName = mother.Name?.ToString(),
            Reason = LetterReason.BastardMotherNote,
            Content = MotherNoteContent(child, disposition),
            SentOnDay = today,
            DeliveryDay = today + DemandLetterDeliveryDays,
            IsDelivered = false
         });

         store.Set(profile);
         ModLog.Info($"[BASTARD] {disposition} note queued: {mother.Name} → player about {child.Name}.");
      }

      private static byte[] ToBytes(object value)
      {
         try { return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None)); }
         catch (Exception ex)
         {
            ModLog.Error($"[BASTARD] serialize failed — persisting empty. {ex.GetType().Name}: {ex.Message}");

            return Encoding.UTF8.GetBytes("{}");
         }
      }

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      private void ApplyClaim(Hero bastard)
      {
         if (bastard == null) return;

         if (Hero.MainHero != null)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(bastard, Hero.MainHero, -ClaimHostility, false);

         RecordClaim(bastard);
         ModLog.Info($"[BASTARD] {bastard.Name} came of age and presses a claim against the player's house (relation -{ClaimHostility}).");
      }

      // ── The mother's initiative: blackmail OR a warmer letter, by disposition ──

      private void EvaluateMother(Hero child, int season)
      {
         Hero mother = child.Mother;

         // M-G3: the player can never blackmail themselves. A player-mother is normally caught by the MainParty
         // guard below, but a CAPTIVE player-mother is not in MainParty, so she slipped through and "sent herself"
         // a blackmail letter revealing her own secret. Guard the player's own identity directly, captive or not.
         if (mother is not {IsAlive: true} || mother == Hero.MainHero || mother == Hero.MainHero?.Spouse) return;

         // A mother who is in the player's OWN party is at his side, not apart: the whole bastard thread
         // (a secret held from a distance, "come to us", "you are never here", a blackmail by courier) makes
         // no sense while she travels with him. A player reported exactly this, a courted, party-bound mother
         // writing that he was never there for the children. She is present; anything owed is spoken in person.
         if (mother.PartyBelongedTo == MobileParty.MainParty) return;

         BastardMotherDisposition disposition = MotherDisposition(mother);
         BlackmailState bs = GetBlackmail(child.StringId);

         // A kinder mother never leaks the secret herself; she only writes, now and then, in her own register.
         if (disposition != BastardMotherDisposition.Mercenary)
         {
            if (!BastardBlackmailPolicy.CanRaiseDemand(season, bs.SilentUntil, false)) return;

            bs.SilentUntil = BastardBlackmailPolicy.SilenceUntil(season);
            SendMotherNote(mother, child, disposition);

            return;
         }

         // Mercenary: an unmet demand past its deadline → she carries out her threat and leaks the secret. M-G6b:
         // but only if the demand LETTER actually reached the player. If the courier was intercepted and it never
         // arrived, the player was never given the threat, so the secret does NOT leak (no blindside); the demand
         // is dropped so she may raise a fresh one instead of a threat they never saw ticking down in silence.
         if (BastardBlackmailPolicy.DemandHasLapsed(season, bs.DemandSeason))
         {
            bool wasDelivered = BlackmailLetterDelivered(mother, bs.DemandLetterSentDay);
            bs.DemandSeason = -1;
            bs.DemandLetterSentDay = -1;

            if (!wasDelivered)
            {
               ModLog.Info($"[BASTARD] {mother.Name}'s blackmail over {child.Name} lapsed but never reached the player (courier lost); the secret holds, she may demand again.");

               return;
            }

            ModLog.Info($"[BASTARD] {mother.Name}'s blackmail over {child.Name} went unmet — she leaks the secret.");
            Discover(child);

            return;
         }

         // Otherwise she may raise a fresh demand (none outstanding, paid silence expired) — and write to extort.
         if (!BastardBlackmailPolicy.CanRaiseDemand(season, bs.SilentUntil, bs.DemandSeason >= 0)) return;

         bs.DemandSeason = season;
         bs.DemandLetterSentDay = (int) CampaignTime.Now.ToDays; // M-G6b: anchor delivery detection to this demand's letter
         bs.DemandAmount = BastardBlackmailPolicy.DemandAmount(Hero.MainHero?.Gold ?? 0);
         ModLog.Info($"[BASTARD] {mother.Name} raises a blackmail demand over {child.Name}: ~{bs.DemandAmount} denars for silence " +
                     $"(pay within {BastardBlackmailPolicy.DemandDeadlineSeasons} seasons).");
         SendDemandLetter(mother, child, bs.DemandAmount);
      }

      private void EvaluateSeason()
      {
         if (_bastards.Count == 0) return;

         int season = CurrentSeason;

         // Snapshot the concealed ids first — Discover mutates the dictionary.
         List<string> concealed = _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

         foreach (string id in concealed)
         {
            Hero child = FindHero(id);

            if (child is not {IsAlive: true}) continue;

            // Minor 16 (romance audit): after the MainHero dies and an heir takes over, `Hero.MainHero` is the
            // heir, who did NOT father/bear these children. Their scandal and blackmail belong to the dead parent,
            // not the heir, so a bastard no longer parented by the CURRENT player is left alone (never "inherited").
            if (!IsPlayersBastard(child)) continue;

            // (a) the ambient, age-keyed risk the secret simply surfaces.
            if (MBRandom.RandomInt(100) < BastardDiscoveryPolicy.DiscoveryChancePercent((int) child.Age))
            {
               Discover(child);

               continue;
            }

            // (b) the mother's own initiative — blackmail, longing, reproach, or a plea for support.
            EvaluateMother(child, season);
         }
      }

      private BlackmailState GetBlackmail(string childId)
      {
         if (_blackmail.TryGetValue(childId, out BlackmailState s)) return s;

         s = new BlackmailState();
         _blackmail[childId] = s;

         return s;
      }

      // ── Birth: tag the player's non-marital children ──────────────────────

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         if (aliveChildren == null || Hero.MainHero == null) return;

         foreach (Hero child in aliveChildren)
         {
            if (child == null || !IsPlayersBastard(child) || _bastards.ContainsKey(child.StringId)) continue;

            // Minor 14 (romance audit): a child of a clanless wanderer mother has no house to hide a secret in, to
            // scandalise, or to press a dynastic claim from. The whole concealed-bastard arc presumes a maternal
            // house, so a clanless child is simply not tracked (no crash, no phantom claimant).
            if (child.Clan == null)
            {
               ModLog.Info($"[BASTARD] {child.Name} is born to a clanless mother; no maternal house to conceal a bastard in, not tracked.");

               continue;
            }

            // M-G4: a bastard born into the player's OWN clan (a female player's child, or a companion-mother's
            // child raised in the household) lives publicly from birth, so there is NO secret to "discover" later.
            // It is never a concealed bastard: no seasonal discovery roll, no scandal, no blackmail leverage. Its
            // standing is an open matter of the house, not a hidden one, and the coming-of-age path already
            // refuses a claim to a child of the player's own clan.
            if (child.Clan == Hero.MainHero.Clan)
            {
               ModLog.Info($"[BASTARD] {child.Name} is born into the player's own house: an open child of the family, not a concealed bastard.");

               continue;
            }

            _bastards[child.StringId] = false; // concealed (raised in the mother's external house)
            ModLog.Info($"[BASTARD] a hidden child is born: {child.Name} (parents {child.Mother?.Name}/{child.Father?.Name}).");
         }
      }

      // ── Coming-of-age: the grown bastard's claim ──────────────────────────

      private void OnHeroComesOfAge(Hero hero)
      {
         if (hero == null || !_bastards.ContainsKey(hero.StringId) || Hero.MainHero?.Clan == null) return;

         // Only a player-SIRED bastard raised in an external (the mother's) house is a claimant on the player's
         // clan. A female player's bastard is already in her own clan — a later, different nuance.
         if (hero.Father != Hero.MainHero || hero.Clan == Hero.MainHero.Clan) return;

         BastardClaimVerdict verdict = ResolveClaimVerdict(hero);
         _bastards[hero.StringId] = true; // coming of age outs the secret regardless
         _blackmail.Remove(hero.StringId);

         if (verdict == BastardClaimVerdict.FollowsMotherDynasty)
         {
            ModLog.Info($"[BASTARD] {hero.Name} came of age in the grander house of {hero.Clan?.Name} — follows the mother's dynasty, no claim.");

            return;
         }

         ApplyClaim(hero);
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null) return;
         _bastards.Remove(victim.StringId);
         _blackmail.Remove(victim.StringId);
      }

      // ── Seasonal evaluation: discovery, then the mother's blackmail ────────

      private void OnWeeklyTick()
      {
         int season = CurrentSeason;

         if (season <= _lastEvaluatedSeason) return;

         _lastEvaluatedSeason = season;
         EvaluateSeason();
      }

      #endregion
   }
}