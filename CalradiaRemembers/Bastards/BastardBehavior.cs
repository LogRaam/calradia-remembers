// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards consequence layer. State kept is minimal: which children are the player's bastards (id → discovered,
// one-way) and, per bastard, the mother's recurring-blackmail clock (demand season / paid-silence season). Age
// is read live. Threads: (1) each SEASON, an age-keyed discovery roll may bring a hidden one to light — a
// scandal into the world-event store + a standing hit; (2) the same season, the still-leveraged mother may
// raise a blackmail demand (unmet past its deadline → she leaks it; paid → silence for a grace period, then she
// returns); (3) at coming of age, a player-sired bastard of a lesser maternal house presses a claim. See the
// ROADMAP "Bastards" pillar. Player-facing surfaces for blackmail (demand letter, conversation pressing) are
// later increments; the mechanical spine + console commands are here.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Bastards
{
   /// <summary>Per-bastard blackmail clock: the season a demand was raised (-1 none) and the season paid silence runs to (-1 none).</summary>
   internal sealed class BlackmailState
   {
      public int DemandSeason { get; set; } = -1;
      public int SilentUntil { get; set; } = -1;
   }

   /// <summary>
   ///   Tracks the player's bastards and drives their consequences each season: discovery, the mother's
   ///   recurring blackmail, and (at coming of age) a prestige-gated claim. Minimal persisted state; age and
   ///   the seasonal odds are read live from the heroes.
   /// </summary>
   public sealed class BastardBehavior : CampaignBehaviorBase
   {
      // Tuning (flagged for in-game ratification).
      private const int ScandalMagnitude = 40;     // how far the gossip carries (0..100)
      private const int ScandalRenownLoss = 5;     // the dishonour to the player's name
      private const int ScandalInfluenceLoss = 10; // the political cost at court
      private const int ClaimHostility = 40;       // the grown bastard turns a clear enemy of the player's house
      private const int ClaimMagnitude = 60;       // a dynastic claim travels far

      private const string SaveKey = "CalradiaRemembers_Bastards_v1bytes";
      private const string SaveKeyBlackmail = "CalradiaRemembers_BastardsBlackmail_v1bytes";

      public static BastardBehavior Instance;

      // childId -> discovered (one-way). Age is read live from the hero.
      private Dictionary<string, bool> _bastards = new();

      // childId -> the mother's blackmail clock.
      private Dictionary<string, BlackmailState> _blackmail = new();
      private int _lastEvaluatedSeason = -1;

      public BastardBehavior() => Instance = this;

      /// <summary>The tracked bastards (child id → discovered) — for the cr.bastards diagnostic.</summary>
      public IReadOnlyDictionary<string, bool> Bastards => _bastards;

      private static int CurrentSeason => (int) CampaignTime.Now.ToSeasons;

      public bool IsTracked(string childId) => childId != null && _bastards.ContainsKey(childId);

      public bool IsDiscovered(string childId)
         => childId != null && _bastards.TryGetValue(childId, out bool discovered) && discovered;

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
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
            byte[] a = null, b = null;
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
      }

      // ── Birth: tag the player's non-marital children ──────────────────────

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         if (aliveChildren == null || Hero.MainHero == null) return;

         foreach (Hero child in aliveChildren)
         {
            if (child == null || !IsPlayersBastard(child) || _bastards.ContainsKey(child.StringId)) continue;

            _bastards[child.StringId] = false; // concealed
            ModLog.Info($"[BASTARD] a hidden child is born: {child.Name} (parents {child.Mother?.Name}/{child.Father?.Name}).");
         }
      }

      /// <summary>A player-parented child whose OTHER parent is not the player's spouse — born outside the marriage.</summary>
      private static bool IsPlayersBastard(Hero child)
      {
         Hero player = Hero.MainHero;
         bool playerIsParent = child.Father == player || child.Mother == player;
         if (!playerIsParent) return false;

         Hero otherParent = child.Father == player
            ? child.Mother
            : child.Father;

         return otherParent != null && otherParent != player.Spouse;
      }

      // ── Seasonal evaluation: discovery, then the mother's blackmail ────────

      private void OnWeeklyTick()
      {
         int season = CurrentSeason;
         if (season <= _lastEvaluatedSeason) return;

         _lastEvaluatedSeason = season;
         EvaluateSeason();
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

            // (a) the ambient, age-keyed risk the secret simply surfaces.
            if (MBRandom.RandomInt(100) < BastardDiscoveryPolicy.DiscoveryChancePercent((int) child.Age))
            {
               Discover(child);

               continue;
            }

            // (b) the mother's active blackmail (a separate, deliberate vector).
            EvaluateBlackmail(child, season);
         }
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

         if (Hero.MainHero?.Clan != null)
         {
            GainRenownAction.Apply(Hero.MainHero, -ScandalRenownLoss, true);
            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -ScandalInfluenceLoss);
         }

         ModLog.Info($"[BASTARD] discovered: {child.Name} — scandal recorded, renown-{ScandalRenownLoss} influence-{ScandalInfluenceLoss}.");

         return true;
      }

      private static void RecordScandal(Hero child)
      {
         WorldEventStore store = WorldEventStore.Instance;
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
            Magnitude = ScandalMagnitude,
            InvolvesPlayer = true,
            ActorIds = actors,
            FactionIds = factions,
            Summary = $"Word spreads that {child.Name} is the natural child of {fatherName} and {motherName}.",
            ChronicleLine = $"a whispered scandal: {child.Name}, born of {motherName} out of wedlock, was named {fatherName}'s own"
         });
      }

      // ── Blackmail: the mother's recurring leverage while the secret hides ──

      private void EvaluateBlackmail(Hero child, int season)
      {
         Hero mother = child.Mother;
         if (mother == null) return;
         if (!BastardBlackmailPolicy.HoldsLeverage(true, mother.IsAlive, mother == Hero.MainHero?.Spouse)) return;

         BlackmailState bs = GetBlackmail(child.StringId);

         // An unmet demand past its deadline → she carries out her threat and leaks the secret.
         if (BastardBlackmailPolicy.DemandHasLapsed(season, bs.DemandSeason))
         {
            bs.DemandSeason = -1;
            ModLog.Info($"[BASTARD] {mother.Name}'s blackmail over {child.Name} went unmet — she leaks the secret.");
            Discover(child);

            return;
         }

         // Otherwise she may raise a fresh demand (none outstanding, paid silence expired).
         if (BastardBlackmailPolicy.CanRaiseDemand(season, bs.SilentUntil, bs.DemandSeason >= 0))
         {
            bs.DemandSeason = season;
            int demand = BastardBlackmailPolicy.DemandAmount(Hero.MainHero?.Gold ?? 0);
            ModLog.Info($"[BASTARD] {mother.Name} raises a blackmail demand over {child.Name}: ~{demand} denars for silence " +
                        $"(pay within {BastardBlackmailPolicy.DemandDeadlineSeasons} seasons). [letter — later increment]");
         }
      }

      /// <summary>
      ///   The player meets the demands an NPC holds over them: pays each (gold to her) and buys a season of
      ///   silence (she returns when it lapses). Returns how many demands were settled and the total paid.
      /// </summary>
      public (int settled, int goldPaid) Pay(Hero mother)
      {
         if (mother == null || Hero.MainHero == null) return (0, 0);

         int season = CurrentSeason;
         int settled = 0, gold = 0;

         foreach (string childId in _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList())
         {
            Hero child = FindHero(childId);
            if (child?.Mother != mother) continue;
            if (!BastardBlackmailPolicy.HoldsLeverage(true, mother.IsAlive, mother == Hero.MainHero.Spouse)) continue;

            int amount = BastardBlackmailPolicy.DemandAmount(Hero.MainHero.Gold);
            if (Hero.MainHero.Gold >= amount)
            {
               GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, mother, amount, false);
               gold += amount;
            }

            BlackmailState bs = GetBlackmail(childId);
            bs.DemandSeason = -1;
            bs.SilentUntil = BastardBlackmailPolicy.SilenceUntil(season);
            settled++;
         }

         if (settled > 0)
            ModLog.Info($"[BASTARD] player paid {mother.Name} {gold} denars for silence over {settled} bastard(s) until season {BastardBlackmailPolicy.SilenceUntil(season)}.");

         return (settled, gold);
      }

      /// <summary>
      ///   The gold an NPC could demand for her silence right now, or 0 — she is the mother of a still-concealed
      ///   bastard of the player's and holds leverage (pure <see cref="BastardBlackmailPolicy" />). A query.
      /// </summary>
      public int BlackmailDemandOver(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return 0;

         string childId = _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).FirstOrDefault(id => FindHero(id)?.Mother == npc);
         if (childId == null) return 0;
         if (!BastardBlackmailPolicy.HoldsLeverage(true, npc.IsAlive, npc == Hero.MainHero.Spouse)) return 0;

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

      private BlackmailState GetBlackmail(string childId)
      {
         if (!_blackmail.TryGetValue(childId, out BlackmailState s))
         {
            s = new BlackmailState();
            _blackmail[childId] = s;
         }

         return s;
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

      /// <summary>
      ///   The prestige-gate verdict for a grown bastard's claim on the FATHER's (player's) clan: a child of a
      ///   grander maternal house follows that line; an equal-or-lesser house breeds a claimant.
      /// </summary>
      public BastardClaimVerdict ResolveClaimVerdict(Hero bastard)
         => BastardPrestigePolicy.Resolve(ClanPrestige(bastard?.Clan), ClanPrestige(Hero.MainHero?.Clan));

      private void ApplyClaim(Hero bastard)
      {
         if (bastard == null) return;

         if (Hero.MainHero != null)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(bastard, Hero.MainHero, -ClaimHostility, false);

         RecordClaim(bastard);
         ModLog.Info($"[BASTARD] {bastard.Name} came of age and presses a claim against the player's house (relation -{ClaimHostility}).");
      }

      private static void RecordClaim(Hero bastard)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null || Hero.MainHero == null) return;

         string parent = Hero.MainHero.Name?.ToString() ?? "the lord";
         var actors = new List<string> {bastard.StringId, Hero.MainHero.StringId};

         var factions = new List<string>();
         if (Hero.MainHero.MapFaction != null) factions.Add(Hero.MainHero.MapFaction.StringId);
         if (bastard.MapFaction != null) factions.Add(bastard.MapFaction.StringId);

         store.Record(new WorldEvent {
            Type = WorldEventType.Scandal,
            Day = (int) CampaignTime.Now.ToDays,
            Magnitude = ClaimMagnitude,
            InvolvesPlayer = true,
            ActorIds = actors,
            FactionIds = factions,
            Summary = $"{bastard.Name}, natural-born child of {parent}, has come of age and presses a claim against their house.",
            ChronicleLine = $"{bastard.Name}, my own natural child, came of age and turned against my house, pressing a claim upon it"
         });
      }

      private static int ClanPrestige(Clan clan)
      {
         if (clan == null) return 0;
         bool ruling = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;

         return BastardPrestigePolicy.PrestigeScore(clan.Tier, (int) clan.Renown, ruling);
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null) return;
         _bastards.Remove(victim.StringId);
         _blackmail.Remove(victim.StringId);
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      private static byte[] ToBytes(object value)
      {
         try { return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None)); }
         catch (Exception ex)
         {
            ModLog.Error($"[BASTARD] serialize failed — persisting empty. {ex.GetType().Name}: {ex.Message}");

            return Encoding.UTF8.GetBytes("{}");
         }
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

      // ── Debug seams (console tests / diagnostics) ─────────────────────────

      /// <summary>Force-tag a hero as a concealed player bastard (console testing, no real birth needed).</summary>
      public void DebugTrack(Hero child)
      {
         if (child != null && !_bastards.ContainsKey(child.StringId)) _bastards[child.StringId] = false;
      }

      /// <summary>Run the seasonal evaluation (discovery + blackmail) immediately (console testing).</summary>
      public void DebugEvaluateSeason() => EvaluateSeason();

      /// <summary>Force-raise a blackmail demand on a tracked bastard NOW (console testing, bypasses the leverage gate).</summary>
      public void DebugRaiseDemand(Hero child)
      {
         if (child == null) return;
         DebugTrack(child);
         GetBlackmail(child.StringId).DemandSeason = CurrentSeason;
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
   }
}
