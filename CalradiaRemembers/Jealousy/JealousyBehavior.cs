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
using TaleWorlds.Library;

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

      // A private flirt leaves no witnesses to carry the tale, so it does not always leave a rumour
      // behind at all — a coin toss whether word gets out (player report: the spouse reacted to EVERY
      // private conversation, however innocent).
      private const float PrivateReactionChance = 0.5f;

      // M-J6: how far/fast the liaison rumour spreads (WorldEventStore.IsAwareOf reads Magnitude). A PRIVATE act
      // leaves a faint rumour that travels slowly; a WITNESSED one spreads far faster and more surely, because
      // witnesses carry the tale. Neither is instant: word must still reach the jealous party (proximity/faction).
      private const int PrivateRumorMagnitude = 18;
      private const int WitnessedRumorMagnitude = 40;

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
            sb.AppendLine($"  {h.Name} [{h.Culture?.Name}] tolerant={c.ToleratesSharedPartners(Hero.MainHero?.Clan?.Tier ?? 0)} " +
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

      /// <summary>
      ///   Test seam (cr.testall open_relationship): runs the competition SELECTION only, firing no reaction,
      ///   and returns the StringId of the partner who would be chosen as aggrieved (null when none). Lets a
      ///   self-test prove a neglected NON-open partner is chosen while an open-arrangement partner is excluded.
      /// </summary>
      public string DebugSelectAggrieved(int today) => SelectAggrieved(today)?.worst?.StringId;

      /// <summary>
      ///   Public seam over the competition loop's OWN neglect measure (days since <paramref name="h" /> was
      ///   last genuinely attended; 0 for a partner riding with the player or held captive). Exposed so the
      ///   open-partner autonomy (Inc 3a) reads the SAME notion of neglect the jealousy competition uses,
      ///   rather than forking a second, drifting formula. Returns 0 when the store is unavailable.
      /// </summary>
      public static int NeglectDaysFor(Hero h, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         return h == null || behavior?.Store == null
            ? 0
            : NeglectDays(h, behavior, today);
      }

      /// <summary>Test seam (cr.testall): runs the REAL daily tick through its MCM gate, so the EnableJealousy toggle can be observed suppressing (OFF) or allowing (ON) every jealousy pass.</summary>
      public void DebugRunDailyTick() => OnDailyTick();

      /// <summary>Test seam (cr.testall): how many jealousy reactions are queued to fire, so the EnableJealousy gate self-test can watch one fire (ON) or stay pending (OFF).</summary>
      public int DebugPendingCount => _state.Pending.Count;

      /// <summary>Test seam (cr.testall): enqueue one due jealousy reaction against <paramref name="partyId" /> for the EnableJealousy gate self-test.</summary>
      public void DebugEnqueuePending(string partyId, string courtedName, int reactDay)
         => _state.Pending.Add(new PendingJealousy {
            PartyId = partyId,
            Stake = (int) JealousStake.PlayerPartner,
            CourtedId = null,
            CourtedName = courtedName ?? "another",
            OtherId = null,
            Tier = (int) RomanticActTier.Flirt,
            ReactDay = reactDay,
            WithLetter = false
         });

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

            // M-J2: a marriage-tier act (public wedding, consort openly taken) ALWAYS wakes the jealous party;
            // only lesser acts are probabilistic ("not every flirt travels") — witnessed or not. A witnessed act
            // is telegraphed (WitnessedReactionChance); a PRIVATE one has no witnesses to carry the tale, so it
            // gets its own, lower roll (PrivateReactionChance) instead of bypassing the probability gate entirely.
            // M-J1: consume the pairing cooldown ONLY when the act actually reacts, so a failed flirt roll no
            // longer swallows a real marriage that happens days later within the cooldown window.
            bool reacts = hadWitnesses
               ? JealousyReactionPolicy.WitnessedActReacts(tier, MBRandom.RandomFloat, WitnessedReactionChance)
               : JealousyReactionPolicy.PrivateActReacts(tier, MBRandom.RandomFloat, PrivateReactionChance);
            if (!reacts) return;

            _state.LastReactionDay[pairKey] = today;

            // M-J6: a witnessed act is NOT telepathy. Word must still TRAVEL to the jealous party (proximity,
            // faction, recency) through the awareness mechanism, exactly like a private act, so a jealous party on
            // the far side of the world does not learn of it instantly. A witnessed act only spreads FASTER and
            // more surely (a stronger rumour), because witnesses carry the tale.
            int rumorMagnitude = hadWitnesses
               ? WitnessedRumorMagnitude
               : PrivateRumorMagnitude;
            string liaisonId = RecordLiaisonRumor(courted, today, rumorMagnitude);

            if (liaisonId == null)
            {
               // No world-event store to carry the rumour: a witnessed act still reacts (fall back to a direct
               // schedule so it is never silently lost); a private act has no channel to travel without a rumour.
               if (hadWitnesses) SchedulePendingReaction(res, courted, tier, 1 + MBRandom.RandomInt(3), today);

               return;
            }

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
            ModLog.Info($"[JEALOUSY] {res.Party.Name} ({res.Stake}) over {courted.Name}: word {(hadWitnesses ? "spreads fast (witnessed)" : "must travel (private)")} to reach them.");
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
         // Minor (romance audit): polygamy is the PLAYER's elite privilege, so the sharing tolerance is gated on
         // the PLAYER's clan tier, not the jealous party's (a low-tier player is not "grand enough to keep consorts"
         // however high-born their partner, and vice versa).
         bool competition = stake == JealousStake.PlayerPartner && culture.ToleratesSharedPartners(Hero.MainHero?.Clan?.Tier ?? 0);

         NpcProfile profile = behavior.Store.Get(pj.PartyId) ?? HeroProfileMapper.FromHero(party);

         // Open relationship (Inc 2): a partner who holds reciprocal open terms with the player is not jealous
         // of the player's other loves. Suppress her OWN event-triggered reaction entirely (the competition
         // path excludes her separately, both via the one pure OpenRelationshipPolicy check so they cannot
         // drift). Only a player-partner stake carries such a bond; a third-party jealous party (courted's
         // spouse, a rival, a spurned admirer) is unaffected.
         if ((stake == JealousStake.PlayerPartner || stake == JealousStake.SecretLover)
             && OpenRelationshipPolicy.SuppressesJealousy(profile.Romantic?.IsOpenArrangement ?? false))
         {
            ModLog.Info($"[JEALOUSY] {party.Name} holds open terms with the player, no jealousy ({stake}).");

            return;
         }

         // A wound of the heart, not clan politics — routed to this NPC's PERSONAL regard
         // (ReputationWithPlayer), not the vanilla clan-relation ledger.
         int delta = -JealousyReaction.Severity(stake, tier, competition, culture);
         if (delta < 0)
            ProfileMutator.ApplyReputationDelta(profile, delta);

         // M-J5: a betrayal wounds the BOND, not just the regard. The player's own betrayed partner (their
         // primary partner, or their secret lover) sees their romantic status fall to Estranged (recoverable),
         // instead of staying "Intimate"/"Committed" as if nothing happened. A third-party jealousy (the courted
         // NPC's spouse, a rival, a spurned admirer) has no player-bond to wound, so it is left untouched.
         // But a reported FLIRT is not a betrayal: it makes the partner doubt (the regard hit and the grievance
         // memory above still apply at every tier) without breaking the bond — only an intimacy or a marriage
         // is the betrayal that wounds it (player report: the wife turned Estranged over mere small talk).
         if (tier >= RomanticActTier.Intimacy
             && (stake == JealousStake.PlayerPartner || stake == JealousStake.SecretLover) && profile.Romantic != null)
         {
            RomanticStatus woundedFrom = profile.Romantic.Status;
            profile.Romantic.Status = RomanticWoundPolicy.AfterJealousyWound(woundedFrom);
            if (profile.Romantic.Status != woundedFrom)
               ModLog.Info($"[JEALOUSY] {party.Name}'s bond wounded by betrayal: {woundedFrom} -> {profile.Romantic.Status}.");
         }

         string grievance = JealousyReaction.BuildGrievance(stake, competition, pj.CourtedName);
         profile.Events.Add(new NotableEvent(today, NotableEventType.Jealousy, grievance));
         behavior.Store.Set(profile);

         ModLog.Info($"[JEALOUSY] {party.Name} ({stake}{(competition ? "/competition" : "")}) turns on the player " +
                     $"over {pj.CourtedName} (personal regard {delta}).");

         // Grudges pillar: the reference tier-1 source. Adds RESOLVABILITY on top of the relation hit
         // already applied above — never a second penalty (the relation delta is untouched).
         // EXCEPT the secret lover: their hurt stays private and the bond absorbs it (quiet regard loss and
         // a discreet letter, never a grudge that hardens into a grievance arc). And no lasting grudge for a
         // mere reported FLIRT either: warm words breed doubt (regard + letter), not a grudge that hardens —
         // only an intimacy or a marriage is betrayal enough to be nursed.
         if (stake != JealousStake.SecretLover && tier >= RomanticActTier.Intimacy)
            GrudgeBehavior.Instance?.TryPlant(party, Hero.MainHero, GrudgeSource.Jealousy, grievance,
               GrudgeBackfillRules.JealousyMagnitude, true, today);

         // A threatening/confronting letter, unless they are beside the player (same party, army, or
         // settlement), in which case they would say it to the player's face rather than send a courier.
         // M-J7: a prisoner sends no couriers from a cell, so a captive jealous party never writes a threat.
         bool withPlayer = PlayerPresenceResolver.IsBeside(party);

         if (!pj.WithLetter || withPlayer || party.IsPrisoner) return;
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
         // M-J7: a captive partner is in irons, not being slighted; the competition loop must not read their
         // captivity as the player neglecting them (they cannot be "attended to" from a cell either way).
         if (h.IsPrisoner) return 0;
         NpcProfile p = behavior.Store.Get(h.StringId);

         if (p == null || p.Events.Count == 0) return CompetitionSelector.NeglectThresholdDays; // present but unrecorded — don't over-punish

         // M-J4: measure days since the last genuine ATTENTION, not the last event of any kind. Otherwise a
         // jealousy reaction the system itself planted, or a received letter, reset the neglect clock, so the
         // competition/estrangement loop could never actually reach a partner who was truly being ignored.
         int lastAttentionDay = int.MinValue;
         foreach (NotableEvent e in p.Events)
            if (JealousyAttentionPolicy.CountsAsAttention(e.type) && e.gameDay > lastAttentionDay)
               lastAttentionDay = e.gameDay;

         if (lastAttentionDay == int.MinValue) return CompetitionSelector.NeglectThresholdDays; // recorded, but never truly attended to

         return today - lastAttentionDay;
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
      private static string RecordLiaisonRumor(Hero courted, int today, int magnitude)
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
            Magnitude = magnitude,
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

            if (!store.IsAwareOf(party, liaison, today)) continue;

            // Anti-"psychic spouse" (player report): IsAwareOf's faction bonus can push a witnessed flirt rumour
            // over the awareness threshold at ANY map distance, so a same-faction wife used to learn of the
            // liaison INSTANTLY from the far end of Calradia. On top of the shared rumour scoring, jealousy adds
            // its own earshot gate: the jealous party must be close enough to the liaison's location to plausibly
            // have heard the tale (or be riding beside the player). The watch is NOT dropped when only the
            // distance fails — they may draw near before the rumour dies of old age (AwarenessMaxDays).
            Vec2? partyPos = party.PartyBelongedTo?.GetPosition2D ?? party.CurrentSettlement?.GetPosition2D;
            Vec2? rumorPos = string.IsNullOrEmpty(liaison.PlaceId)
               ? null
               : Settlement.Find(liaison.PlaceId)?.GetPosition2D;
            float? distance = partyPos.HasValue && rumorPos.HasValue
               ? partyPos.Value.Distance(rumorPos.Value)
               : (float?) null; // an unreadable position fails safe toward NOT hearing

            if (!JealousyHearingPolicy.WithinEarshot(distance, PlayerPresenceResolver.IsBeside(party)))
            {
               ModLog.Info($"[JEALOUSY] word of {pa.CourtedName}'s liaison reached {party.Name}'s faction, but they are too far to have heard it yet.");

               continue;
            }

            ready.Add(pa);
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
            (Hero worst, bool isSenior, int neglect)? pick = SelectAggrieved(today);

            if (pick == null) return;

            FireCompetition(pick.Value.worst, pick.Value.isSenior, pick.Value.neglect, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[JEALOUSY] competition pass failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The competition SELECTION, shared by the live daily pass (<see cref="EvaluateCompetition" />) and
      ///   the test seam (<see cref="DebugSelectAggrieved" />) so the two cannot drift: gathers the household's
      ///   standings, marks each open-arrangement partner (via the one pure
      ///   <see cref="OpenRelationshipPolicy.SuppressesJealousy" /> check) so she is never chosen, then asks
      ///   the pure <see cref="CompetitionSelector" /> for the single most-slighted partner. Returns null when
      ///   there is no rivalry or no real slight. Fires nothing itself.
      /// </summary>
      private (Hero worst, bool isSenior, int neglect)? SelectAggrieved(int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return null;

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

         if (partners.Count < 2) return null; // no rivalry without a household of several

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
            // Open relationship (Inc 2): an open partner still counts toward the household size and the
            // most-attended baseline, but is flagged so CompetitionSelector never chooses her as aggrieved.
            bool isOpen = OpenRelationshipPolicy.SuppressesJealousy(
               behavior.Store.Get(h.StringId)?.Romantic?.IsOpenArrangement ?? false);
            standings.Add(new PartnerStanding {
               PartnerId = h.StringId,
               ToleratesSharing = c.ToleratesSharedPartners(Hero.MainHero?.Clan?.Tier ?? 0),
               NeglectDays = NeglectDays(h, behavior, today),
               OnCooldown = onCooldown,
               IsOpenArrangement = isOpen
            });
            byId[h.StringId] = h;
         }

         string worstId = CompetitionSelector.SelectAggrieved(standings);

         if (worstId == null) return null;

         Hero worst = byId[worstId];
         int worstNeglect = standings.First(s => s.PartnerId == worstId).NeglectDays;

         return (worst, ReferenceEquals(worst, senior), worstNeglect);
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
         // Inc 1: the jealousy MCM toggle also gates the marriage hook, so an OFF household sees no wedding-day
         // jealousy either. Default-safe: treated as ON when settings are unavailable.
         if (!ModSettings.EnableJealousy) return;

         var player = Hero.MainHero;

         if (player == null) return;
         Hero spouse = firstHero == player ? secondHero : secondHero == player ? firstHero : null;

         if (spouse == null) return;

         // A wedding is public — treat it as witnessed and at the highest severity tier.
         OnRomanticAct(spouse, RomanticActTier.Marriage, true, (int) CampaignTime.Now.ToDays);
      }

      private void OnDailyTick()
      {
         // Inc 1: the whole jealousy system sits behind an MCM toggle. When it is OFF, NEITHER the competition
         // loop NOR any event-triggered reaction runs (a drama-free household). Default-safe: treated as ON
         // when settings are unavailable.
         if (!ModSettings.EnableJealousy) return;

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