// Code written by Gabriel Mailhot, 19/06/2026.
// Sprint 24 (jealousy): a romantic act with one NPC can provoke a third party who has a real
// stake. Knowledge-gated (witnesses propagate it), probabilistic, delayed (word travels), and
// culturally shaded (exclusivity betrayal vs. shared-partner competition).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
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
        public static JealousyBehavior Instance;

        private const string SaveKey = "CalradiaRemembers_Jealousy_v1";

        // Don't let the same party stew over the same courted NPC more than once per this window.
        private const int ReactionCooldownDays = 20;

        private JealousyState _state = new JealousyState();

        public JealousyBehavior() => Instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            string json = null;
            if (dataStore.IsSaving)
            {
                try { json = JsonConvert.SerializeObject(_state, Formatting.None); }
                catch (Exception ex)
                {
                    ModLog.Error($"[JEALOUSY] state serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    json = "{}";
                }
            }

            dataStore.SyncData(SaveKey, ref json);

            if (!dataStore.IsSaving && !string.IsNullOrEmpty(json))
            {
                try { _state = JsonConvert.DeserializeObject<JealousyState>(json) ?? new JealousyState(); }
                catch (Exception ex)
                {
                    _state = new JealousyState();
                    ModLog.Error($"[JEALOUSY] state load failed — jealousy reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                }
            }
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

                // Knowledge gate: witnesses spread the word quickly; without them it rarely reaches
                // the jealous party at all (until the v1.2 rumor store gives word a real home).
                float chance = hadWitnesses ? 0.75f : 0.20f;
                if (MBRandom.RandomFloat > chance) return;

                if (res.Stake == JealousStake.RivalSuitor)
                    _state.RivalLocks[courted.StringId] = res.Party.StringId; // keep the same rival across the arc

                int delay = hadWitnesses ? 1 + MBRandom.RandomInt(3) : 4 + MBRandom.RandomInt(4);
                bool withLetter = MBRandom.RandomFloat < LetterChance(res.Stake);

                _state.LastReactionDay[pairKey] = today;
                _state.Pending.Add(new PendingJealousy {
                    PartyId     = res.Party.StringId,
                    Stake       = (int) res.Stake,
                    CourtedId   = courted.StringId,
                    CourtedName = courted.Name?.ToString() ?? "another",
                    OtherId     = res.Other?.StringId,
                    Tier        = (int) tier,
                    ReactDay    = today + delay,
                    WithLetter  = withLetter
                });

                ModLog.Info($"[JEALOUSY] scheduled: {res.Party.Name} ({res.Stake}) over {courted.Name} " +
                            $"in {delay}d (letter={withLetter}, witnessed={hadWitnesses}).");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[JEALOUSY] OnRomanticAct failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void OnBeforeHeroesMarried(Hero firstHero, Hero secondHero, bool showNotification)
        {
            Hero player = Hero.MainHero;
            if (player == null) return;
            Hero spouse = firstHero == player ? secondHero : secondHero == player ? firstHero : null;
            if (spouse == null) return;

            // A wedding is public — treat it as witnessed and at the highest severity tier.
            OnRomanticAct(spouse, RomanticActTier.Marriage, hadWitnesses: true, today: (int) CampaignTime.Now.ToDays);
        }

        private void OnDailyTick()
        {
            if (_state.Pending.Count == 0) return;
            int today = (int) CampaignTime.Now.ToDays;

            var due = _state.Pending.Where(p => today >= p.ReactDay).ToList();
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

        /// <summary>
        ///   Test hook (cr.jealousy): resolve the jealous party for an act toward
        ///   <paramref name="courted" /> and fire the reaction immediately, bypassing the witness,
        ///   probability and cooldown gates. Returns a one-line description for the console.
        /// </summary>
        public string DebugForce(Hero courted, RomanticActTier tier)
        {
            if (courted == null) return "No courted NPC.";
            int today = (int) CampaignTime.Now.ToDays;

            string lockedRival = Lookup(_state.RivalLocks, courted.StringId);
            JealousResolution res = JealousPartyResolver.Resolve(
                courted, id => NpcMemoryCampaignBehavior.Instance?.Store?.Get(id), lockedRival);
            if (res?.Party == null)
                return $"No plausible jealous party for an act toward {courted.Name}.";

            if (res.Stake == JealousStake.RivalSuitor)
                _state.RivalLocks[courted.StringId] = res.Party.StringId;

            FireReaction(new PendingJealousy {
                PartyId     = res.Party.StringId,
                Stake       = (int) res.Stake,
                CourtedId   = courted.StringId,
                CourtedName = courted.Name?.ToString() ?? "another",
                OtherId     = res.Other?.StringId,
                Tier        = (int) tier,
                ReactDay    = today,
                WithLetter  = true
            }, today);

            return $"Jealous party: {res.Party.Name} ({res.Stake}) over {courted.Name} — reaction fired " +
                   "(grudge + grievance memory + threat letter queued).";
        }

        // ── Reaction ──────────────────────────────────────────────────────────

        private static void FireReaction(PendingJealousy pj, int today)
        {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null) return;

            Hero party = Hero.FindFirst(h => h.StringId == pj.PartyId);
            if (party == null || !party.IsAlive) return;

            var stake = (JealousStake) pj.Stake;
            var tier = (RomanticActTier) pj.Tier;
            JealousyCultureProfile culture = JealousyCultureLoader.Get(party.Culture?.Name?.ToString());
            bool competition = stake == JealousStake.PlayerPartner
                               && culture.ToleratesSharedPartners(party.Clan?.Tier ?? 0);

            NpcProfile profile = behavior.Store.Get(pj.PartyId) ?? HeroProfileMapper.FromHero(party);

            int delta = -Severity(stake, tier, competition, culture);
            if (delta < 0)
                ChangeRelationAction.ApplyPlayerRelation(party, delta, affectRelatives: false, showQuickNotification: false);

            profile.Events.Add(new NotableEvent(today, NotableEventType.Jealousy,
                BuildGrievance(stake, competition, pj.CourtedName)));
            behavior.Store.Set(profile);

            ModLog.Info($"[JEALOUSY] {party.Name} ({stake}{(competition ? "/competition" : "")}) turns on the player " +
                        $"over {pj.CourtedName} (relation {delta}).");

            // A threatening/confronting letter — unless they ride with the player, in which case
            // they would say it to the player's face rather than send a courier.
            bool withPlayer = party.PartyBelongedTo == MobileParty.MainParty;
            if (pj.WithLetter && !withPlayer)
            {
                string context = BuildLetterContext(stake, competition, party, pj.CourtedName, culture);
                LetterGenerationService.QueueInitialLetter(
                    profile, party, LetterReason.JealousThreat, context,
                    behavior.LlmServices, behavior.Store, today);
            }
        }

        // ── Tuning ────────────────────────────────────────────────────────────

        private static int Severity(JealousStake stake, RomanticActTier tier, bool competition, JealousyCultureProfile culture)
        {
            int b;
            switch (stake)
            {
                case JealousStake.SpouseOfCourted:
                    b = tier == RomanticActTier.Flirt ? 8 : tier == RomanticActTier.Intimacy ? 20 : 22;
                    return Scale(b, culture);
                case JealousStake.PlayerPartner:
                    if (competition)
                        return tier == RomanticActTier.Flirt ? 3 : tier == RomanticActTier.Intimacy ? 6 : 8;
                    return tier == RomanticActTier.Flirt ? 10 : tier == RomanticActTier.Intimacy ? 25 : 40;
                case JealousStake.RivalSuitor:
                    b = tier == RomanticActTier.Flirt ? 6 : tier == RomanticActTier.Intimacy ? 15 : 18;
                    return Scale(b, culture);
                default: // SpurnedAdmirer
                    b = tier == RomanticActTier.Flirt ? 5 : tier == RomanticActTier.Intimacy ? 12 : 15;
                    return Scale(b, culture);
            }
        }

        /// <summary>Light cultural scaling for stakes where exclusivity stance modulates intensity (never to zero).</summary>
        private static int Scale(int baseValue, JealousyCultureProfile culture)
        {
            float f = culture.Outrage == JealousyOutrage.High ? 1.0f
                    : culture.Outrage == JealousyOutrage.Medium ? 0.85f
                    : 0.7f;
            return Math.Max(1, (int) Math.Round(baseValue * f));
        }

        private static float LetterChance(JealousStake stake) => stake switch
        {
            JealousStake.RivalSuitor     => 0.80f,
            JealousStake.SpouseOfCourted => 0.70f,
            JealousStake.PlayerPartner   => 0.65f,
            _                            => 0.50f
        };

        private static string BuildGrievance(JealousStake stake, bool competition, string courted) => stake switch
        {
            JealousStake.SpouseOfCourted => $"The player made romantic advances toward {courted}, my own spouse. I will not forget the affront.",
            JealousStake.PlayerPartner   => competition
                ? $"My partner has taken up with {courted} as well. I will not be made the lesser of their loves."
                : $"My partner gave their affection to {courted} while bound to me. The betrayal cuts deep.",
            JealousStake.RivalSuitor     => $"The player has been courting {courted}, whom I had hoped to win for myself. I mean to make them regret it.",
            _                            => $"I offered the player my heart, and they turned instead to {courted}."
        };

        private static string BuildLetterContext(
            JealousStake stake, bool competition, Hero party, string courted, JealousyCultureProfile culture)
        {
            string name = party.Name?.ToString() ?? "the writer";
            string hint = string.IsNullOrWhiteSpace(culture.PromptHint) ? "" : " " + culture.PromptHint;
            switch (stake)
            {
                case JealousStake.SpouseOfCourted:
                    return $"You are {name}, wed to {courted}. The player has made romantic advances toward your spouse." +
                           $"{hint} Confront or warn the player, and demand they keep away from {courted}.";
                case JealousStake.PlayerPartner:
                    return competition
                        ? $"You are one of the player's partners. They have now taken up with {courted} as well. By your customs " +
                          $"this sharing is permitted —{hint} — so your grievance is rank and attention: insist you not be set " +
                          $"beneath {courted}, and that the player honour what is owed to you."
                        : $"You are the player's own partner, bound to them. They have given their affection to {courted} while " +
                          $"sworn to you.{hint} Write as the betrayed — wounded, cold, or openly threatening.";
                case JealousStake.RivalSuitor:
                    return $"You are {name}. You had hoped to win {courted}'s hand yourself, and now the player courts them." +
                           $"{hint} Warn the player off — make plain that you intend to marry {courted}, and that you will not yield quietly.";
                default:
                    return $"You are {name}. You once felt deeply for the player, and they have turned instead to {courted}." +
                           $"{hint} Write as the spurned — bitter, reproachful, or vengeful.";
            }
        }

        private static string Lookup(Dictionary<string, string> map, string key)
            => map != null && key != null && map.TryGetValue(key, out string v) ? v : null;

        // ── Persisted state ───────────────────────────────────────────────────

        private sealed class JealousyState
        {
            /// <summary>Courted-NPC id → the rival suitor locked in on the first jealous act, for consistency.</summary>
            public Dictionary<string, string> RivalLocks { get; set; } = new Dictionary<string, string>();

            /// <summary>"partyId|courtedId" → last day a reaction was scheduled, enforcing the cooldown.</summary>
            public Dictionary<string, int> LastReactionDay { get; set; } = new Dictionary<string, int>();

            /// <summary>Scheduled reactions waiting for word to travel (fired on the daily tick).</summary>
            public List<PendingJealousy> Pending { get; set; } = new List<PendingJealousy>();
        }

        private sealed class PendingJealousy
        {
            public string PartyId { get; set; }
            public int Stake { get; set; }
            public string CourtedId { get; set; }
            public string CourtedName { get; set; }
            public string OtherId { get; set; }
            public int Tier { get; set; }
            public int ReactDay { get; set; }
            public bool WithLetter { get; set; }
        }
    }
}
