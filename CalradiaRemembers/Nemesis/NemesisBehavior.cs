// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar increment 1 (persistence layer): owns the persisted roster of NemesisIdentity records
// (the persistent named bandit bosses), save-safe like GrudgeBehavior. A corrupt blob degrades to an empty
// roster rather than blocking the load; the SyncData key is additive so old saves load as an empty roster.
// The pure decisions (which recurring nemesis to reuse) live in NemesisRosterPolicy; this class only stores
// and queries. The bandit-capture hook that mints/attaches a Hero as captor is a later increment-1 piece.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Nemesis
{
   /// <summary>
   ///   The save-persisted store of nemesis identities. Additive and save-safe: absent key or corrupt blob
   ///   yields an empty roster, never a broken load. Query via <see cref="FindRecurring" /> (delegates to the
   ///   pure <see cref="NemesisRosterPolicy" />); mint and add via <see cref="Add" />.
   /// </summary>
   public sealed class NemesisBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_Nemesis_v1bytes";

      public static NemesisBehavior Instance;

      private NemesisRoster _roster = new NemesisRoster();

      // Transient (non-serialized) active-capture mapping: which bandit party currently holds the player and
      // the nemesis Hero who is their captor. Lost on a save/load mid-captivity (degrades to the vanilla
      // bandit scene), re-established on the next capture; the Hero itself persists via the roster.
      private MobileParty _activeCaptorParty;
      private Hero _activeNemesisHero;

      // Transient (non-serialized) ACTIVE-HUNT mapping: the nemesis Hero materialized into a bandit party the
      // player just engaged, and that party. Cleared when the engagement ends (he is detached back to latent
      // unless the player killed or captured him). Lost on a save/load mid-battle (a rare edge: he would stay
      // in the band), the same degradation the capture layer accepts.
      private Hero _materializedHero;
      private MobileParty _materializedParty;

      public NemesisBehavior()
      {
         Instance = this;
      }

      /// <summary>Every nemesis on record, living and dead.</summary>
      public IReadOnlyList<NemesisIdentity> All => _roster.Nemeses;

      public override void RegisterEvents()
      {
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
         // Active hunt: bracket a player-vs-bandit engagement to maybe materialize the nemesis into the
         // band (start) and to detach him back to latent when the fight ends without his death/capture (end).
         CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      /// <summary>
      ///   The bandit-capture hook: when a bandit party takes the PLAYER prisoner, reuse-or-mint the regional
      ///   nemesis for that band, mint/reuse his persistent Hero (with a memory profile so cr.memory/cr.info
      ///   work on him), and record him as the active captor. <see cref="CaptiveEncounterService" /> then runs
      ///   the captive scene with HIM. Defensive throughout: any failure degrades to the vanilla bandit scene.
      /// </summary>
      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         try
         {
            if (prisoner != Hero.MainHero) return; // only the PLAYER becoming a captive spawns a nemesis
            MobileParty band = capturer?.MobileParty;
            if (band == null || !band.IsBandit) return;

            string bandKey = ResolveBandCultureKey(band);
            string regionId = ResolveRegionId(band);
            var today = (int) CampaignTime.Now.ToDays;

            NemesisIdentity existing = FindRecurring(bandKey, regionId);
            NemesisIdentity nem = FindOrMint(bandKey, regionId, today); // reuses `existing` if present
            Hero hero = ResolveOrMintHero(nem, today);
            if (hero == null) return;

            _activeCaptorParty = band;
            _activeNemesisHero = hero;
            nem.TimesEncountered++;
            if (existing != null) nem.RecaptureCount++; // he has caught the player before, and now again
            ModLog.Info($"[NEMESIS] {hero.Name} now holds the player captive (band {bandKey}, region {regionId}, recaptures {nem.RecaptureCount}).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NEMESIS] capture hook failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The player has been let out of captivity. If the party that held them was the ACTIVE nemesis's band
      ///   (an escape, or any non-death release), the nemesis REMEMBERS it: a notable event lands on his memory
      ///   profile ("they slipped my grasp"), so a later re-crossing reflects that the player got away from him
      ///   (a player-reported gap: escaping recorded nothing). Records for a spontaneous escape AND whatever
      ///   release path a third-party escape mod uses, since both raise this event. Transient by design: if the
      ///   captivity spanned a save/load the active mapping is already gone and nothing is recorded, the same
      ///   degradation the rest of the capture layer accepts.
      /// </summary>
      private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
      {
         try
         {
            if (prisoner != Hero.MainHero) return;
            if (detail == EndCaptivityDetail.Death) return; // died a prisoner: nothing to remember him escaping
            if (_activeNemesisHero == null || _activeCaptorParty == null) return;
            if (party?.MobileParty != _activeCaptorParty) return; // freed from some other captor, not the nemesis

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(_activeNemesisHero.StringId);
            if (store != null && profile != null)
            {
               var day = (int) CampaignTime.Now.ToDays;
               string playerName = Hero.MainHero?.Name?.ToString() ?? "the captive";
               profile.Events.Add(new NotableEvent(day, NotableEventType.Conflict,
                  $"{playerName} slipped my grasp and escaped my hold. A debt I mean to collect."));
               store.Set(profile);
               ModLog.Info($"[NEMESIS] {_activeNemesisHero.Name} remembers the player escaping his hold (release detail {detail}).");
            }

            // Captivity under this nemesis is over; drop the transient active mapping.
            _activeNemesisHero = null;
            _activeCaptorParty = null;
         }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] prisoner-released hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Active hunt: the nemesis materializes only in a fight the player chose to start ──

      /// <summary>
      ///   Start of a player battle: if the other side is a bandit band of a living, latent nemesis's culture,
      ///   near the region he haunts, a chance roll may turn that band out to be led by him. Never while the
      ///   player is a prisoner or a capture scene is live.
      /// </summary>
      private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
      {
         try
         {
            if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
            if (Hero.MainHero?.IsPrisoner == true) return;
            if (_activeNemesisHero != null || _materializedHero != null) return;

            MobileParty band = EngagedBanditParty(attacker, defender);
            if (band == null) return;

            Hero hero = TryMaterializeInto(band, forceRoll: false);
            if (hero == null) return;

            _materializedHero = hero;
            _materializedParty = band;
         }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] hunt start hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   End of the engagement: if the materialized nemesis survived UN-captured, detach him back to latent so
      ///   he keeps no standing party on the map (killed or captured, the resolution layer owns the aftermath).
      /// </summary>
      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (_materializedHero == null) return;

            Hero hero = _materializedHero;
            if (hero.IsAlive && !hero.IsPrisoner && hero.PartyBelongedTo != null)
            {
               DetachToLatent(hero);
               ModLog.Info($"[NEMESIS] {hero.Name} slips the fight and fades back to latent.");
            }

            _materializedHero = null;
            _materializedParty = null;
         }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] hunt end hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Maybe materialize a living, latent nemesis of this band's culture into it: gated by
      ///   <see cref="NemesisMaterializePolicy" /> (region proximity + a chance roll, or forced for the
      ///   self-test). Ensures his Hero (minting it if latent), detaches him from anywhere, and adds him to the
      ///   band. Returns the Hero, or null when none materialized. Public so the self-test can drive it without
      ///   a real battle; the transient hunt mapping is owned by the event path, not set here.
      /// </summary>
      public Hero TryMaterializeInto(MobileParty band, bool forceRoll)
      {
         if (band == null || !band.IsBandit || _materializedHero != null) return null;

         NemesisIdentity nem = FindHuntableNemesisNear(band);
         if (nem == null || !NemesisIsLatent(nem)) return null;

         // A qualifying encounter: the ratified pity-timer reveal roll (rises to a certainty by
         // NemesisRevealPolicy.GuaranteeAfterEncounters, so the hunt is emergent but never endless). A miss
         // builds pity credit; forceRoll (0.0) always reveals, for the self-test.
         double roll = forceRoll ? 0.0 : MBRandom.RandomFloat;
         if (!NemesisRevealPolicy.ShouldReveal(nem.EncountersSinceReveal, roll))
         {
            nem.EncountersSinceReveal++;
            ModLog.Info($"[NEMESIS] {nem.Name} did not reveal this engagement (pity {nem.EncountersSinceReveal}/{NemesisRevealPolicy.GuaranteeAfterEncounters}).");

            return null;
         }

         var today = (int) CampaignTime.Now.ToDays;
         Hero hero = MintCaptorFor(nem.BandCultureKey, nem.RegionId, today);
         if (hero == null) return null;

         if (hero.PartyBelongedTo != null && hero.PartyBelongedTo != band) DetachToLatent(hero);
         AddHeroToPartyAction.Apply(hero, band, false);

         nem.EncountersSinceReveal = 0; // revealed: the pity counter resets
         nem.TimesEncountered++;
         ModLog.Info($"[NEMESIS] materialized {hero.Name} into a {nem.BandCultureKey} band the player engaged (region {nem.RegionId}).");
         InformationManager.DisplayMessage(new InformationMessage($"{hero.Name} leads this band. Your hunt has found him."));

         return hero;
      }

      /// <summary>The bandit party in a player map event (the side that is not the player's main party), or null.</summary>
      private static MobileParty EngagedBanditParty(PartyBase attacker, PartyBase defender)
      {
         MobileParty a = attacker?.MobileParty, d = defender?.MobileParty;

         if (a != null && a.IsBandit && a != MobileParty.MainParty) return a;
         if (d != null && d.IsBandit && d != MobileParty.MainParty) return d;

         return null;
      }

      /// <summary>The most-established living, non-turned nemesis of this band's culture whose region anchor is near it (or region-agnostic when his anchor is not a real settlement), or null.</summary>
      private NemesisIdentity FindHuntableNemesisNear(MobileParty band)
      {
         string culture = ResolveBandCultureKey(band);
         Vec2 pos = band.GetPosition2D;
         NemesisIdentity best = null;
         int bestEncounters = -1;

         foreach (NemesisIdentity n in _roster.Nemeses)
         {
            if (n == null || !n.IsAlive || n.IsTurned || n.BandCultureKey != culture) continue;

            Settlement anchor = ResolveRegionSettlement(n.RegionId);
            if (anchor != null && !NemesisMaterializePolicy.WithinRegion(anchor.GetPosition2D.x, anchor.GetPosition2D.y, pos.x, pos.y, NemesisMaterializePolicy.RegionMatchRadius))
               continue; // he haunts a real region and this band is outside it

            if (n.TimesEncountered > bestEncounters)
            {
               best = n;
               bestEncounters = n.TimesEncountered;
            }
         }

         return best;
      }

      /// <summary>Removes the nemesis Hero from whatever party holds him, back to a latent floating Hero with no map presence. Best-effort; never throws.</summary>
      public static void DetachToLatent(Hero hero)
      {
         MobileParty party = hero?.PartyBelongedTo;
         if (party == null) return;

         try { party.MemberRoster?.RemoveTroop(hero.CharacterObject); }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] detach failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The settlement a region key names, or null when the key is a culture/"wilds" fallback (then match on culture alone).</summary>
      private static Settlement ResolveRegionSettlement(string regionId)
         => string.IsNullOrEmpty(regionId) ? null : Settlement.All?.FirstOrDefault(s => s != null && s.StringId == regionId);

      /// <summary>True when the nemesis's Hero keeps no map party and is no prisoner (latent, ready to materialize); a Hero not yet minted is latent by construction.</summary>
      private static bool NemesisIsLatent(NemesisIdentity nem)
      {
         if (string.IsNullOrEmpty(nem.HeroStringId)) return true;

         Hero hero = Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);
         if (hero == null || !hero.IsAlive) return true;

         return !hero.IsPrisoner && hero.PartyBelongedTo == null;
      }

      /// <summary>
      ///   Consulted by <see cref="CaptiveEncounterService" /> in place of the band's (nonexistent) hero
      ///   leader: the nemesis Hero holding the player in THIS party, or null. Clears the stale mapping once
      ///   the player is no longer this party's prisoner.
      /// </summary>
      public Hero NemesisCaptorFor(MobileParty captorParty)
      {
         if (_activeNemesisHero == null || _activeCaptorParty == null) return null;
         if (captorParty != _activeCaptorParty) return null;

         if (Hero.MainHero?.IsPrisoner != true)
         {
            _activeNemesisHero = null;
            _activeCaptorParty = null;

            return null;
         }

         return _activeNemesisHero;
      }

      /// <summary>
      ///   The scene-escape chance against the ACTIVE nemesis captor, decaying with each prior recapture
      ///   (<see cref="NemesisEscapeChancePolicy" />), or null when the player's captor is not a nemesis (so
      ///   the caller keeps its ordinary flat chance). A nemesis is never a perfect cage, but he learns.
      /// </summary>
      public double? ActiveNemesisEscapeChance()
      {
         if (_activeNemesisHero == null || Hero.MainHero?.IsPrisoner != true) return null;

         NemesisIdentity nem = GetByHero(_activeNemesisHero.StringId);
         if (nem == null) return null;

         return NemesisEscapeChancePolicy.EscapeChance(nem.RecaptureCount);
      }

      /// <summary>
      ///   The fraction of vanilla spontaneous-escape rolls to suppress against the ACTIVE nemesis captor,
      ///   rising with each prior recapture (<see cref="NemesisEscapeChancePolicy" />), or null when the
      ///   player's captor is not a nemesis (the caller then keeps its ordinary flat suppress). A nemesis is
      ///   harder to slip than an ordinary captor, yet never impossible: a slim passive chance always remains.
      /// </summary>
      public double? ActiveNemesisVanillaSuppressChance()
      {
         if (_activeNemesisHero == null || Hero.MainHero?.IsPrisoner != true) return null;

         NemesisIdentity nem = GetByHero(_activeNemesisHero.StringId);
         if (nem == null) return null;

         return NemesisEscapeChancePolicy.VanillaDripSuppressChance(nem.RecaptureCount);
      }

      /// <summary>
      ///   True when the player is CURRENTLY held prisoner by the recorded nemesis party. The vanilla
      ///   spontaneous-escape drip is then fully suppressed, so the only luck exit is the decaying
      ///   scene-escape (you cannot simply wait a nemesis out).
      /// </summary>
      public bool PlayerHeldByNemesis()
      {
         if (_activeNemesisHero == null || _activeCaptorParty == null) return false;
         if (Hero.MainHero?.IsPrisoner != true) return false;

         return Hero.MainHero.PartyBelongedToAsPrisoner?.MobileParty == _activeCaptorParty;
      }

      /// <summary>The recapture count of the nemesis embodied by <paramref name="hero" />, or 0 when he is not a tracked nemesis.</summary>
      public int RecaptureCountFor(Hero hero)
      {
         if (hero == null) return 0;
         NemesisIdentity nem = GetByHero(hero.StringId);

         return nem?.RecaptureCount ?? 0;
      }

      /// <summary>
      ///   Nemesis pillar increment 2 (R2 execute / R3 torture, chat-driven entry point): the tracked,
      ///   LIVING nemesis Hero the PLAYER currently holds prisoner (the inversion of the usual captivity),
      ///   or null when no such nemesis is held. Reuses the exact "held by the player" check the rest of
      ///   the mod already relies on for a prisoner in the player's own party (<see cref="Hero.PartyBelongedToAsPrisoner" />'s
      ///   leader). Consulted by <see cref="NemesisPrisonerDialogFlow" />'s hero_main_options gate and by
      ///   its console test harnesses, so both prove the exact same detection.
      /// </summary>
      public Hero PlayerHeldNemesisPrisoner()
      {
         foreach (NemesisIdentity nem in _roster.Nemeses)
         {
            if (!nem.IsAlive || string.IsNullOrEmpty(nem.HeroStringId)) continue;

            Hero hero = Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);
            if (hero is {IsAlive: true, IsPrisoner: true} && hero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
               return hero;
         }

         return null;
      }

      /// <summary>
      ///   Reuse-or-mint the nemesis for this band culture + region AND ensure his persistent Hero exists
      ///   (name bound, memory profile registered). Returns the Hero, or null if minting failed. Shared by
      ///   the capture hook and the self-test harness so the mint path can be validated without a real capture.
      /// </summary>
      /// <param name="ignoreRespawnCooldown">Diagnostics only (the self-test harness); see <see cref="FindOrMint" />. Never set in play.</param>
      public Hero MintCaptorFor(string bandCultureKey, string regionId, int today, bool ignoreRespawnCooldown = false)
      {
         NemesisIdentity nem = FindOrMint(bandCultureKey, regionId, today, ignoreRespawnCooldown);

         return ResolveOrMintHero(nem, today);
      }

      #region private

      /// <summary>The vanilla bandit-clan StringId of the capturing band (looters / forest_bandits / ...), or a fallback.</summary>
      private static string ResolveBandCultureKey(MobileParty band)
      {
         string clanId = band?.ActualClan?.StringId;

         return string.IsNullOrEmpty(clanId) ? "looters" : clanId;
      }

      /// <summary>A coarse, stable region key for the capture (the band's home hideout, else its culture). TUNE: refine to the nearest settlement/region.</summary>
      private static string ResolveRegionId(MobileParty band)
      {
         string key = band?.HomeSettlement?.StringId ?? band?.Party?.Culture?.StringId;

         return string.IsNullOrEmpty(key) ? "wilds" : key;
      }

      /// <summary>
      ///   Reuse the identity's persistent Hero if he still lives, else mint a new bandit-faction Hero (the
      ///   proven SpawnCaptors pattern MINUS the throwaway kill), name him after the identity, bind his
      ///   StringId, and register a memory profile so he remembers the player across encounters.
      /// </summary>
      private static Hero ResolveOrMintHero(NemesisIdentity nem, int today)
      {
         if (nem == null) return null; // cooldown-blocked mint, or a bad call: no Hero to resolve

         try
         {
            if (!string.IsNullOrEmpty(nem.HeroStringId))
            {
               Hero existing = Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);
               if (existing != null && existing.IsAlive) return existing;
            }

            Clan bandits = Clan.BanditFactions?.FirstOrDefault(c => c.StringId == nem.BandCultureKey)
                           ?? Clan.BanditFactions?.FirstOrDefault();
            if (bandits == null) return null;

            CharacterObject template = CharacterObject.All?.FirstOrDefault(c => c != null && c.StringId == "bandit_chief")
                                       ?? CharacterObject.All?.FirstOrDefault(c => c != null && c.Occupation == Occupation.Wanderer);
            if (template == null) return null;

            Hero hero = HeroCreator.CreateSpecialHero(template, null, bandits, null, 35);
            if (hero == null) return null;

            try { hero.SetName(new TextObject(nem.Name + " " + nem.Epithet), new TextObject(nem.Name)); }
            catch (Exception ex) { ModLog.Error($"[NEMESIS] SetName failed: {ex.GetType().Name}: {ex.Message}"); }

            nem.HeroStringId = hero.StringId;

            try
            {
               NpcProfile profile = HeroProfileMapper.FromHero(hero);
               NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);
            }
            catch (Exception ex) { ModLog.Error($"[NEMESIS] profile register failed: {ex.GetType().Name}: {ex.Message}"); }

            ModLog.Info($"[NEMESIS] minted persistent Hero for {nem.Name} {nem.Epithet}.");

            return hero;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NEMESIS] ResolveOrMintHero failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion

      /// <summary>The living nemesis of this band culture anchored to this region, or null (mint a new one).</summary>
      public NemesisIdentity FindRecurring(string bandCultureKey, string regionId)
         => NemesisRosterPolicy.FindRecurring(_roster.Nemeses, bandCultureKey, regionId);

      /// <summary>Adds a freshly-minted nemesis identity to the roster.</summary>
      public void Add(NemesisIdentity identity)
      {
         if (identity != null && !string.IsNullOrEmpty(identity.Id)) _roster.Nemeses.Add(identity);
      }

      /// <summary>The nemesis embodied by this Hero StringId, or null.</summary>
      public NemesisIdentity GetByHero(string heroStringId)
         => string.IsNullOrEmpty(heroStringId) ? null : _roster.Nemeses.FirstOrDefault(n => n.HeroStringId == heroStringId);

      /// <summary>
      ///   The regional recurring roster's core: reuse the LIVING nemesis of this band culture in this region
      ///   if one exists, else MINT a new latent identity (name/epithet via <see cref="NemesisNameGenerator" />,
      ///   no Hero yet; the bandit-capture hook attaches a real Hero to it). The seed is a RANDOM roll at
      ///   mint: the roster count seeded it before, which made the FIRST nemesis of every campaign the same
      ///   man ("Wick the Cutthroat", a player report). Randomness is safe because the minted name/epithet
      ///   are PERSISTED on the identity and never re-derived from the seed.
      /// </summary>
      /// <param name="ignoreRespawnCooldown">
      ///   Diagnostics only (the self-test harness). The respawn cooldown is a rule about the PACE of the game,
      ///   not about correctness, and it made every nemesis self-test after the first one fail: killing the test
      ///   nemesis bought the usual quiet years, and the next test could then mint nobody. Never set in play.
      /// </param>
      public NemesisIdentity FindOrMint(string bandCultureKey, string regionId, int today, bool ignoreRespawnCooldown = false)
      {
         NemesisIdentity existing = FindRecurring(bandCultureKey, regionId);
         if (existing != null) return existing;

         // Respawn cooldown: a nemesis of this culture died recently, so no new one rises yet (the capture
         // then degrades to a faceless bandit scene). Gabriel 2026-07-08: killing him buys 1..3 quiet years.
         if (!ignoreRespawnCooldown && NemesisRosterPolicy.IsCultureRespawnBlocked(_roster.Nemeses, bandCultureKey, today))
         {
            ModLog.Info($"[NEMESIS] no new {bandCultureKey} nemesis: the last one's respawn cooldown has not passed.");

            return null;
         }

         NemesisNameResult name = NemesisNameGenerator.Generate(bandCultureKey, MBRandom.RandomInt(int.MaxValue));
         var minted = new NemesisIdentity {
            Id = "nemesis_" + Guid.NewGuid().ToString("N"),
            Name = name.Name,
            Epithet = name.Epithet,
            BandCultureKey = bandCultureKey,
            RegionId = regionId,
            IsAlive = true,
            MintedDay = today
         };

         Add(minted);
         ModLog.Info($"[NEMESIS] minted {minted.Name} {minted.Epithet} ({bandCultureKey}) in region {regionId}.");

         return minted;
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list, so an autosave racing a main-thread mutation
               // never throws mid-serialize and drops the whole blob (the GrudgeBehavior/SchemeStore pattern).
               var snapshot = new NemesisRoster {
                  Nemeses = new List<NemesisIdentity>(_roster.Nemeses),
                  MigrationVersion = _roster.MigrationVersion
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[NEMESIS] serialize failed, skipping this save: {ex.GetType().Name}: {ex.Message}");

               return;
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[NEMESIS] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch { bytes = null; }

            if (bytes is {Length: > 0})
               try { _roster = JsonConvert.DeserializeObject<NemesisRoster>(Encoding.UTF8.GetString(bytes)) ?? new NemesisRoster(); }
               catch (Exception ex)
               {
                  _roster = new NemesisRoster();
                  ModLog.Error($"[NEMESIS] load failed — roster reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _roster = new NemesisRoster();

            ModLog.Info($"[NEMESIS] loaded {_roster.Nemeses.Count} nemesis identity(ies).");
         }
      }
   }
}
