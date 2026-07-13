// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Social;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Jealousy
{
   /// <summary>
   ///   Owns the jealousy mechanic: it is told when the player performs a romantic act
   ///   (<see cref="OnRomanticAct" />, plus the marriage campaign event), infers at most one
   ///   plausible jealous party, and — gated by witnesses, probability and a per-pair cooldown —
   ///   schedules a delayed reaction so the grudge lands after "word travels". Reactions apply a
   ///   relation hit and a grievance memory, and may dispatch a threatening letter by courier.
   ///   Persisted so locks, cooldowns and in-flight reactions survive save/load.
   /// </summary>
   public sealed class JealousyBehavior : CampaignBehaviorBase
   {
      // An unwitnessed liaison rumour that never reaches the jealous party within this window is
      // forgotten (the awareness watch is dropped).
      private const int AwarenessMaxDays = 30;

      // ── Competition loop (v1.5): a polygamous household whose partners are unevenly attended ──
      // The neglect/inequity thresholds live in CalradiaRemembers.Logic.CompetitionSelector (tested).
      private const int CompetitionCooldownDays = 15; // between competition reactions per partner
      private const float CompetitionLetterChance = 0.6f;
      private const int CompetitionRelationDecay = 2; // mild cooling each reaction
      private const int EstrangeNeglectDays = 30; // a high-hierarchy senior withdraws after this
      private const int EstrangeRelationHit = 10;
      private const string LegacySaveKey = "CalradiaRemembers_Jealousy_v1";

      // Don't let the same party stew over the same courted NPC more than once per this window.
      private const int ReactionCooldownDays = 20;

      // Current key: JSON state stored as a UTF-8 byte[] (JsonCampaignState), which has no size
      // limit. LegacySaveKey is the old STRING key, kept only to migrate a save written before
      // this behavior switched over (Bannerlord's native string serializer corrupts a save once
      // the blob grows past roughly 65,535 characters).
      private const string SaveKey = "CalradiaRemembers_Jealousy_v2bytes";

      // A witnessed act is telegraphed — high chance the jealous party hears and reacts.
      private const float WitnessedReactionChance = 0.75f;
      public static JealousyBehavior Instance;

      private JealousyState _state = new();

      public JealousyBehavior()
      {
         Instance = this;
      }

      /// <summary>
      ///   Test hook (cr.jealousy_comp): reports the player's current partner household and forces
      ///   one competition pass (ignoring cooldowns) so the v1.5 loop can be validated on demand.
      /// </summary>
      public string DebugCompetition()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return "No store.";
         var today = (int) CampaignTime.Now.ToDays;

         Hero spouse = Hero.MainHero?.Spouse;
         var partners = new List<Hero>();
         if (spouse is {IsAlive: true}) partners.Add(spouse);
         foreach (string id in behavior.Store.ListIds())
         {
            NpcProfile p = behavior.Store.Get(id);

            if (p?.Romantic?.IsConsort != true || p.Romantic.Status != RomanticStatus.Committed) continue;
            Hero h = Hero.FindFirst(x => x.StringId == id);
            if (h is {IsAlive: true} && h != spouse) partners.Add(h);
         }

         if (partners.Count < 2)
            return $"Competition needs >=2 active partners (native spouse + committed consorts). Found {partners.Count}.";

         var sb = new StringBuilder();
         sb.AppendLine($"Partner household ({partners.Count}):");
         foreach (Hero h in partners)
         {
            JealousyCulture c = JealousyCultureLoader.GetCulture(h.Culture?.Name?.ToString());
            sb.AppendLine($"  {h.Name} [{h.Culture?.Name}] tolerant={c.ToleratesSharedPartners(h.Clan?.Tier ?? 0)} " +
                          $"neglect={NeglectDays(h, behavior, today)}d");
            _state.CompetitionCooldown.Remove(h.StringId); // clear cooldown so the forced pass can react
         }

         EvaluateCompetition(today);
         sb.Append("Competition pass run — check the log and the partners' relation / encyclopedia for any reaction.");

         return sb.ToString();
      }

      /// <summary>
      ///   Test hook (cr.jealousy): resolve the jealous party for an act toward
      ///   <paramref name="courted" /> and fire the reaction immediately, bypassing the witness,
      ///   probability and cooldown gates. Returns a one-line description for the console.
      /// </summary>
      public string DebugForce(Hero courted, RomanticActTier tier)
      {
         if (courted == null) return "No courted NPC.";
         var today = (int) CampaignTime.Now.ToDays;

         string lockedRival = Lookup(_state.RivalLocks, courted.StringId);
         JealousResolution res = JealousPartyResolver.Resolve(
            courted, id => NpcMemoryCampaignBehavior.Instance?.Store?.Get(id), lockedRival);

         if (res?.Party == null)
            return $"No plausible jealous party for an act toward {courted.Name}.";

         if (res.Stake == JealousStake.RivalSuitor)
            _state.RivalLocks[courted.StringId] = res.Party.StringId;

         FireReaction(new PendingJealousy {
            PartyId = res.Party.StringId,
            Stake = (int) res.Stake,
            CourtedId = courted.StringId,
            CourtedName = courted.Name?.ToString() ?? "another",
            OtherId = res.Other?.StringId,
            Tier = (int) tier,
            ReactDay = today,
            WithLetter = true
         }, today);

         return $"Jealous party: {res.Party.Name} ({res.Stake}) over {courted.Name} — reaction fired " +
                "(grudge + grievance memory + threat letter queued).";
      }

      // ── Triggers ──────────────────────────────────────────────────────────

      /// <summary>
      ///   The player performed a romantic act toward <paramref name="courted" />. Resolves a
      ///   jealous party and, if the gates pass, schedules a delayed reaction. Safe to call with
      ///   any act; it no-ops when no plausible jealous party exists.
      /// </summary>
      public void OnRomanticAct(Hero courted, RomanticActTier tier, bool hadWitnesses, int today)
      {
         if (courted == null || Hero.MainHero == null || courted == Hero.MainHero) return;

         try
         {
            string lockedRival = Lookup(_state.RivalLocks, courted.StringId);
            JealousResolution res = JealousPartyResolver.Resolve(
               courted, id => NpcMemoryCampaignBehavior.Instance?.Store?.Get(id), lockedRival);

            if (res?.Party == null) return;

            string pairKey = res.Party.StringId + "|" + courted.StringId;

            if (_state.LastReactionDay.TryGetValue(pairKey, out int last) && today - last < ReactionCooldownDays)
               return; // already stewing over this pairing

            if (res.Stake == JealousStake.RivalSuitor)
               _state.RivalLocks[courted.StringId] = res.Party.StringId; // keep the same rival across the arc
            _state.LastReactionDay[pairKey] = today;

            if (hadWitnesses)
            {
               // Telegraphed: word travels fast. React soon (probabilistic — not every flirt).
               if (MBRandom.RandomFloat > WitnessedReactionChance) return;
               SchedulePendingReaction(res, courted, tier, 1 + MBRandom.RandomInt(3), today);
               ModLog.Info($"[JEALOUSY] scheduled (witnessed): {res.Party.Name} ({res.Stake}) over {courted.Name}.");
            }
            else
            {
               // No witnesses: the act is private. Lay down a faint liaison rumour and let it
               // travel — the jealous party reacts only if/when word actually reaches them
               // (proximity, faction, recency), not on a flat dice roll. Closes the v1 stub.
               string liaisonId = RecordLiaisonRumor(courted, today);
               _state.PendingAwareness.Add(new PendingAwareness {
                  PartyId = res.Party.StringId,
                  Stake = (int) res.Stake,
                  CourtedId = courted.StringId,
                  CourtedName = courted.Name?.ToString() ?? "another",
                  OtherId = res.Other?.StringId,
                  Tier = (int) tier,
                  LiaisonEventId = liaisonId,
                  CreatedDay = today
               });
               ModLog.Info($"[JEALOUSY] {res.Party.Name} ({res.Stake}) may grow jealous over {courted.Name} — awaiting word to travel.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[JEALOUSY] OnRomanticAct failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      public override void RegisterEvents()
      {
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
            JsonCampaignState.Save(dataStore, SaveKey, _state);
         else
            _state = JsonCampaignState.Load(dataStore, SaveKey, LegacySaveKey, () => new JealousyState());
      }

      // ── Persisted state ───────────────────────────────────────────────────

      private sealed class JealousyState
      {
         /// <summary>partnerId → last day a competition reaction fired (v1.5 ongoing loop cooldown).</summary>
         public Dictionary<string, int> CompetitionCooldown { get; } = new();

         /// <summary>"partyId|courtedId" → last day a reaction was scheduled, enforcing the cooldown.</summary>
         public Dictionary<string, int> LastReactionDay { get; } = new();

         /// <summary>Scheduled reactions waiting for word to travel (fired on the daily tick).</summary>
         public List<PendingJealousy> Pending { get; } = new();

         /// <summary>Unwitnessed acts whose liaison rumour has not yet reached the jealous party (v1.2 propagation).</summary>
         public List<PendingAwareness> PendingAwareness { get; } = new();

         /// <summary>Courted-NPC id → the rival suitor locked in on the first jealous act, for consistency.</summary>
         public Dictionary<string, string> RivalLocks { get; } = new();
      }

      private sealed class PendingAwareness
      {
         public string CourtedId { get; set; }
         public string CourtedName { get; set; }
         public int CreatedDay { get; set; }
         public string LiaisonEventId { get; set; }
         public string OtherId { get; set; }
         public string PartyId { get; set; }
         public int Stake { get; set; }
         public int Tier { get; set; }
      }

      private sealed class PendingJealousy
      {
         public string CourtedId { get; set; }
         public string CourtedName { get; set; }
         public string OtherId { get; set; }
         public string PartyId { get; set; }
         public int ReactDay { get; set; }
         public int Stake { get; set; }
         public int Tier { get; set; }
         public bool WithLetter { get; set; }
      }

      #region private

      // ── Reaction ──────────────────────────────────────────────────────────

      private static void FireReaction(PendingJealousy pj, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;

         Hero party = Hero.FindFirst(h => h.StringId == pj.PartyId);

         if (party is not {IsAlive: true}) return;

         var stake = (JealousStake) pj.Stake;
         var tier = (RomanticActTier) pj.Tier;
         JealousyCulture culture = JealousyCultureLoader.GetCulture(party.Culture?.Name?.ToString());
         bool competition = stake == JealousStake.PlayerPartner && culture.ToleratesSharedPartners(party.Clan?.Tier ?? 0);

         NpcProfile profile = behavior.Store.Get(pj.PartyId) ?? HeroProfileMapper.FromHero(party);

         // A wound of the heart, not clan politics — routed to this NPC's PERSONAL regard
         // (ReputationWithPlayer), not the vanilla clan-relation ledger.
         int delta = -JealousyReaction.Severity(stake, tier, competition, culture);
         if (delta < 0)
            ProfileMutator.ApplyReputationDelta(profile, delta);

         string grievance = JealousyReaction.BuildGrievance(stake, competition, pj.CourtedName);
         profile.Events.Add(new NotableEvent(today, NotableEventType.Jealousy, grievance));
         behavior.Store.Set(profile);

         ModLog.Info($"[JEALOUSY] {party.Name} ({stake}{(competition ? "/competition" : "")}) turns on the player " +
                     $"over {pj.CourtedName} (personal regard {delta}).");

         // Grudges pillar: the reference tier-1 source. Adds RESOLVABILITY on top of the relation hit
         // already applied above — never a second penalty (the relation delta is untouched).
         // EXCEPT the secret lover: their hurt stays private and the bond absorbs it (quiet regard loss and
         // a discreet letter, never a grudge that hardens into a grievance arc).
         if (stake != JealousStake.SecretLover)
            GrudgeBehavior.Instance?.TryPlant(party, Hero.MainHero, GrudgeSource.Jealousy, grievance,
               GrudgeBackfillRules.JealousyMagnitude, true, today);

         // A threatening/confronting letter, unless they are beside the player (same party, army, or
         // settlement), in which case they would say it to the player's face rather than send a courier.
         bool withPlayer = PlayerPresenceResolver.IsBeside(party);

         if (!pj.WithLetter || withPlayer) return;
         string context = JealousyReaction.BuildLetterContext(
            stake, competition, party.Name?.ToString(), pj.CourtedName, culture);
         LetterGenerationService.QueueInitialLetter(
            profile, party, LetterReason.JealousThreat, context,
            behavior.LlmServices, behavior.Store, today);
      }

      /// <summary>
      ///   Whether a partner is beside the player right now (party, army, or settlement), via the single
      ///   presence authority. A partner riding with the player is never neglected. Widened from the old
      ///   party-only check so the jealousy system reads the same truth as the letter and chat systems.
      /// </summary>
      private static bool IsWithPlayer(Hero hero) => PlayerPresenceResolver.IsBeside(hero);

      private static string Lookup(Dictionary<string, string> map, string key)
         => map != null && key != null && map.TryGetValue(key, out string v)
            ? v
            : null;

      /// <summary>Days since this partner was last attended to — a partner riding with the player is never neglected.</summary>
      private static int NeglectDays(Hero h, NpcMemoryCampaignBehavior behavior, int today)
      {
         if (IsWithPlayer(h)) return 0;
         NpcProfile p = behavior.Store.Get(h.StringId);

         if (p == null || p.Events.Count == 0) return CompetitionSelector.NeglectThresholdDays; // present but unrecorded — don't over-punish

         return today - p.Events.Max(e => e.gameDay);
      }

      private static void QueueCompetitionLetter(
         NpcProfile profile,
         Hero partner,
         JealousyCulture culture,
         bool estranged,
         NpcMemoryCampaignBehavior behavior,
         int today)
      {
         string hint = string.IsNullOrWhiteSpace(culture.PromptHint)
            ? ""
            : " " + culture.PromptHint;
         string context = estranged
            ? $"You are the player's senior partner, neglected too long while another was favoured.{hint} Write coldly to tell them you are withdrawing from the bond — wounded pride, not pleading; you will not be set aside in your own house."
            : culture.EquityExpectation
               ? $"You are one of the player's partners. By your customs you accept sharing them, but they favour another and slight you.{hint} Write to demand to be treated with equal honour — firm, not pleading."
               : $"You are one of the player's partners, kept beneath the one they favour.{hint} Write to demand your due share of their attention and your rightful standing — you will not be set aside.";
         LetterGenerationService.QueueInitialLetter(
            profile, partner, LetterReason.JealousThreat, context, behavior.LlmServices, behavior.Store, today);
      }

      /// <summary>
      ///   Lays down a faint, private "they have grown close" rumour at the act's location and
      ///   returns its id. Low magnitude — it reaches only those near or of the right faction, and
      ///   ages out. Never the heroic chronicle (InvolvesPlayer false).
      /// </summary>
      private static string RecordLiaisonRumor(Hero courted, int today)
      {
         var store = WorldEventStore.Instance;

         if (store == null) return null;

         var player = Hero.MainHero;
         Settlement place = player?.CurrentSettlement ?? courted.CurrentSettlement;
         var id = Guid.NewGuid().ToString("N");

         var factions = new List<string>();
         if (player?.MapFaction != null) factions.Add(player.MapFaction.StringId);
         if (courted.MapFaction != null && !factions.Contains(courted.MapFaction.StringId))
            factions.Add(courted.MapFaction.StringId);

         store.Record(new WorldEvent {
            Id = id,
            Type = WorldEventType.Other,
            Day = today,
            PlaceId = place?.StringId,
            InvolvesPlayer = false,
            Magnitude = 18,
            Summary = $"{player?.Name?.ToString() ?? "the player"} has been keeping close company with {courted.Name}.",
            ActorIds = new List<string> {player?.StringId, courted.StringId}.Where(s => s != null).ToList(),
            FactionIds = factions
         });

         return id;
      }

      /// <summary>
      ///   Walks the unwitnessed-act watch list: when the liaison rumour has by now reached the
      ///   jealous party (proximity/faction/recency), schedule their reaction; drop watches whose
      ///   rumour has expired or whose party/event is gone.
      /// </summary>
      private void EvaluateAwareness(int today)
      {
         if (_state.PendingAwareness.Count == 0) return;
         var store = WorldEventStore.Instance;

         var ready = new List<PendingAwareness>();
         var drop = new List<PendingAwareness>();
         foreach (PendingAwareness pa in _state.PendingAwareness)
         {
            if (today - pa.CreatedDay > AwarenessMaxDays)
            {
               drop.Add(pa);

               continue;
            }

            Hero party = Hero.FindFirst(h => h.StringId == pa.PartyId);
            WorldEvent liaison = store?.GetById(pa.LiaisonEventId);
            if (party is not {IsAlive: true} || liaison == null)
            {
               drop.Add(pa);

               continue;
            }

            if (store.IsAwareOf(party, liaison, today)) ready.Add(pa);
         }

         foreach (PendingAwareness pa in ready)
         {
            _state.PendingAwareness.Remove(pa);
            _state.Pending.Add(new PendingJealousy {
               PartyId = pa.PartyId,
               Stake = pa.Stake,
               CourtedId = pa.CourtedId,
               CourtedName = pa.CourtedName,
               OtherId = pa.OtherId,
               Tier = pa.Tier,
               ReactDay = today + 1 + MBRandom.RandomInt(2),
               WithLetter = MBRandom.RandomFloat < JealousyReaction.LetterChance((JealousStake) pa.Stake)
            });
            ModLog.Info($"[JEALOUSY] word of {pa.CourtedName}'s liaison with the player has reached the jealous party — it stirs.");
         }

         foreach (PendingAwareness pa in drop) _state.PendingAwareness.Remove(pa);
      }

      // ── Competition loop (v1.5) ─────────────────────────────────────────────

      /// <summary>
      ///   Once the player keeps more than one partner, those from cultures that accept shared
      ///   partners compete for attention and standing. A partner left unseen while another is
      ///   favoured cools toward the player and may write demanding their due — and a long-
      ///   neglected senior of a high-hierarchy culture can withdraw from the bond entirely.
      ///   Purely a function of how the PLAYER divides their attention; no NPC2NPC simulation.
      /// </summary>
      private void EvaluateCompetition(int today)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;

            if (behavior?.Store == null) return;

            Hero spouse = Hero.MainHero?.Spouse;
            var partners = new List<Hero>();
            if (spouse is {IsAlive: true}) partners.Add(spouse);
            foreach (string id in behavior.Store.ListIds())
            {
               NpcProfile p = behavior.Store.Get(id);

               if (p?.Romantic is not {IsConsort: true} || p.Romantic.Status != RomanticStatus.Committed) continue;
               Hero h = Hero.FindFirst(x => x.StringId == id);
               if (h is {IsAlive: true} && h != spouse) partners.Add(h);
            }

            if (partners.Count < 2) return; // no rivalry without a household of several

            // Senior = the native spouse, else the highest-regarded consort.
            Hero senior = spouse is {IsAlive: true}
               ? spouse
               : partners.OrderByDescending(h => h.GetRelationWithPlayer()).FirstOrDefault();

            // Gather each partner's standing, then let the pure selector pick the single most-
            // aggrieved sharing-culture partner with a real, relative slight (respecting cooldown).
            var standings = new List<PartnerStanding>(partners.Count);
            var byId = new Dictionary<string, Hero>(partners.Count);
            foreach (Hero h in partners)
            {
               JealousyCulture c = JealousyCultureLoader.GetCulture(h.Culture?.Name?.ToString());
               bool onCooldown = _state.CompetitionCooldown.TryGetValue(h.StringId, out int last) && today - last < CompetitionCooldownDays;
               standings.Add(new PartnerStanding {
                  PartnerId = h.StringId,
                  ToleratesSharing = c.ToleratesSharedPartners(h.Clan?.Tier ?? 0),
                  NeglectDays = NeglectDays(h, behavior, today),
                  OnCooldown = onCooldown
               });
               byId[h.StringId] = h;
            }

            string worstId = CompetitionSelector.SelectAggrieved(standings);

            if (worstId == null) return;

            Hero worst = byId[worstId];
            int worstNeglect = standings.First(s => s.PartnerId == worstId).NeglectDays;
            FireCompetition(worst, ReferenceEquals(worst, senior), worstNeglect, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[JEALOUSY] competition pass failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void FireCompetition(Hero partner, bool isSenior, int neglect, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;

         JealousyCulture culture = JealousyCultureLoader.GetCulture(partner.Culture?.Name?.ToString());
         NpcProfile profile = behavior.Store.Get(partner.StringId) ?? HeroProfileMapper.FromHero(partner);

         _state.CompetitionCooldown[partner.StringId] = today;

         // Senior recourse (the Battanian cetmuintir / a steppe senior wife): a long-neglected
         // senior of a high-hierarchy sharing culture WITHDRAWS rather than merely sulking.
         bool estrange = isSenior && culture.Hierarchy == JealousyHierarchy.High && neglect >= EstrangeNeglectDays && profile.Romantic is {Status: RomanticStatus.Committed};

         if (estrange)
         {
            profile.Romantic.Status = RomanticStatus.Estranged;
            ProfileMutator.ApplyReputationDelta(profile, -EstrangeRelationHit); // a wound of the heart, not clan politics
            profile.Events.Add(new NotableEvent(today, NotableEventType.Jealousy,
               "I am the first of this house, and you left me to wither while you doted on another. I withdraw what I gave you."));
            behavior.Store.Set(profile);
            ModLog.Info($"[JEALOUSY] {partner.Name} (senior, {partner.Culture?.Name}) estranges over sustained neglect ({neglect}d).");
            QueueCompetitionLetter(profile, partner, culture, true, behavior, today);

            return;
         }

         ProfileMutator.ApplyReputationDelta(profile, -CompetitionRelationDecay); // a wound of the heart, not clan politics
         profile.Events.Add(new NotableEvent(today, NotableEventType.Jealousy,
            culture.EquityExpectation
               ? "I am one of several, and I accept that — but I will not be treated as less. You favour another and slight me."
               : "You keep me beneath the one you favour and seldom turn your eyes to me. I deserve my place at your side."));
         behavior.Store.Set(profile);
         ModLog.Info($"[JEALOUSY] {partner.Name} ({partner.Culture?.Name}, {(culture.EquityExpectation ? "equity" : "rank")}) competes for attention (neglect {neglect}d).");

         if (MBRandom.RandomFloat < CompetitionLetterChance && !IsWithPlayer(partner))
            QueueCompetitionLetter(profile, partner, culture, false, behavior, today);
      }

      private void FireDuePending(int today)
      {
         if (_state.Pending.Count == 0) return;

         List<PendingJealousy> due = _state.Pending.Where(p => today >= p.ReactDay).ToList();
         foreach (PendingJealousy pj in due)
         {
            _state.Pending.Remove(pj);
            try { FireReaction(pj, today); }
            catch (Exception ex)
            {
               ModLog.Error($"[JEALOUSY] reaction failed for {pj?.PartyId}: {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      private void OnBeforeHeroesMarried(Hero firstHero, Hero secondHero, bool showNotification)
      {
         var player = Hero.MainHero;

         if (player == null) return;
         Hero spouse = firstHero == player ? secondHero : secondHero == player ? firstHero : null;

         if (spouse == null) return;

         // A wedding is public — treat it as witnessed and at the highest severity tier.
         OnRomanticAct(spouse, RomanticActTier.Marriage, true, (int) CampaignTime.Now.ToDays);
      }

      private void OnDailyTick()
      {
         var today = (int) CampaignTime.Now.ToDays;
         // Each pass runs behind its own guard: one throwing must not skip the others this tick.
         SafeRun.Step("JealousyBehavior.EvaluateAwareness", () => EvaluateAwareness(today)); // unwitnessed rumours that have now reached the jealous party
         SafeRun.Step("JealousyBehavior.FireDuePending", () => FireDuePending(today));
         SafeRun.Step("JealousyBehavior.EvaluateCompetition", () => EvaluateCompetition(today));
      }

      /// <summary>Schedules a jealousy reaction for a resolved party after a short delay.</summary>
      private void SchedulePendingReaction(JealousResolution res, Hero courted, RomanticActTier tier, int delay, int today)
      {
         _state.Pending.Add(new PendingJealousy {
            PartyId = res.Party.StringId,
            Stake = (int) res.Stake,
            CourtedId = courted.StringId,
            CourtedName = courted.Name?.ToString() ?? "another",
            OtherId = res.Other?.StringId,
            Tier = (int) tier,
            ReactDay = today + delay,
            WithLetter = MBRandom.RandomFloat < JealousyReaction.LetterChance(res.Stake)
         });
      }

      #endregion
   }
}