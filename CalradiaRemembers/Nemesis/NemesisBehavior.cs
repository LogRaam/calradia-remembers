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
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Nemesis;
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
      // the nemesis Hero who is their captor. The Hero/party references are process-only; the PERSISTED anchor
      // is _activeCaptorIdentityId below, which is what survives a save/load and a prisoner transfer.
      private MobileParty _activeCaptorParty;
      private Hero _activeNemesisHero;

      // The PERSISTED identity of the nemesis holding the player captive right now (nemesis audit RC1). The
      // reported "different nemesis share the same memory" bug came from re-resolving the captor by
      // (culture, region) at scene time (TryAttachCaptorFor -> FindOrMint): region degenerates to culture for a
      // wandering band, so a prisoner transferred to a hideout-anchored party, or a save/load, minted a SECOND
      // nemesis who then inherited the first one's captivity memory. Anchoring the captivity to THIS serialized
      // id, restored by id on load and reused (never re-minted) while it is set, is what keeps one captivity to
      // one captor. Empty/null = no nemesis currently holds the player.
      private string _activeCaptorIdentityId;

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
         CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, ReestablishActiveCaptorOnLoad);
      }

      /// <summary>
      ///   Rebuilds the process-only captor mapping (<see cref="_activeNemesisHero" /> /
      ///   <see cref="_activeCaptorParty" />) after a load, from the PERSISTED
      ///   <see cref="_activeCaptorIdentityId" /> (nemesis audit RC1). The old version keyed on the holding
      ///   party's <c>LeaderHero</c>, which the virtual-override design leaves null, so it always degraded to a
      ///   faceless bandit (Gabriel, 2026-07-19: saved captive of Harker, reloaded a generic looter). Keying on
      ///   the serialized identity restores the SAME captor by id, whatever party now holds the player, and is
      ///   what makes a save/load mid-captivity keep one captor instead of minting a second. A stale id (the
      ///   player is no longer a prisoner, or the identity/hero is gone) simply clears, degrading safely.
      /// </summary>
      private void ReestablishActiveCaptorOnLoad()
      {
         try
         {
            if (Hero.MainHero?.IsPrisoner != true)
            {
               _activeCaptorIdentityId = null; // freed while saved: no captor to restore
               _activeNemesisHero = null;
               _activeCaptorParty = null;

               return;
            }

            MobileParty captorParty = Hero.MainHero.PartyBelongedToAsPrisoner?.MobileParty;

            // Preferred path: the serialized identity of the captor. Resolve his living Hero and bind the party
            // that currently holds the player, whatever its leader (the band has no LeaderHero by design).
            if (!string.IsNullOrEmpty(_activeCaptorIdentityId))
            {
               NemesisIdentity nem = _roster.Nemeses.FirstOrDefault(n => n.Id == _activeCaptorIdentityId);
               Hero hero = nem == null || string.IsNullOrEmpty(nem.HeroStringId)
                  ? null
                  : Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);

               if (hero is {IsAlive: true} && captorParty != null)
               {
                  _activeCaptorParty = captorParty;
                  _activeNemesisHero = hero;
                  ModLog.Info($"[NEMESIS] on load, restored {hero.Name} (id {_activeCaptorIdentityId}) as the captor by persisted identity.");

                  return;
               }

               // The persisted id no longer resolves to a living captor; drop it rather than trust it.
               ModLog.Info($"[NEMESIS] on load, persisted captor id {_activeCaptorIdentityId} no longer resolves; clearing.");
               _activeCaptorIdentityId = null;
            }

            // Legacy fallback for a save made before RC1 (no persisted id): the old LeaderHero path. Harmless
            // when the band has no leader; it just finds nothing and the scene resolves a captor lazily.
            Hero leader = captorParty?.LeaderHero;
            if (leader == null || GetByHero(leader.StringId) == null) return;

            _activeCaptorParty = captorParty;
            _activeNemesisHero = leader;
            _activeCaptorIdentityId = GetByHero(leader.StringId)?.Id;
            ModLog.Info($"[NEMESIS] on load, restored {leader.Name} as the captor via the legacy leader path.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NEMESIS] ReestablishActiveCaptorOnLoad failed: {ex.GetType().Name}: {ex.Message}");
         }
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
            _activeCaptorIdentityId = nem.Id; // RC1: the captivity is anchored to THIS identity from here on
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
      ///   The player has been let out of captivity. When it was the ACTIVE nemesis's own band that held them,
      ///   the nemesis remembers HOW it ended, honestly (nemesis audit M7): a real ESCAPE is a grievance he
      ///   means to collect, a release he CHOSE is his own magnanimity, and a ransom / peace / battle handover
      ///   is neither, so it leaves no memory rather than a false one (the old code wrote "they slipped my
      ///   grasp" for every non-death release, contradicting a scene where he had just let the player go).
      ///   Whatever the detail, the captivity anchor is dropped UNCONDITIONALLY here (nemesis audit RC1/M4):
      ///   the player is free now, so a stale mapping must never survive to be inherited by, or to block, a
      ///   later captor.
      /// </summary>
      private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
      {
         try
         {
            if (prisoner != Hero.MainHero) return;

            // Memory: only when the nemesis's OWN band held the player, and never on death.
            if (detail != EndCaptivityDetail.Death
                && _activeNemesisHero != null && _activeCaptorParty != null
                && party?.MobileParty == _activeCaptorParty)
               RecordCaptivityEndMemory(detail);

            // The captivity is over however it ended: drop the anchor unconditionally.
            _activeNemesisHero = null;
            _activeCaptorParty = null;
            _activeCaptorIdentityId = null;
         }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] prisoner-released hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Writes the active nemesis's honest memory of how the captivity ended (nemesis audit M7). An ESCAPE
      ///   is a debt he means to collect; a release he CHOSE is his own doing; every other ending
      ///   (ransom, peace, battle, compensation) is not a story he tells himself as either escape or mercy, so
      ///   it records nothing.
      /// </summary>
      private void RecordCaptivityEndMemory(EndCaptivityDetail detail)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(_activeNemesisHero.StringId);
         if (store == null || profile == null) return;

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the captive";
         string summary;
         NotableEventType type;

         switch (detail)
         {
            case EndCaptivityDetail.ReleasedAfterEscape:
               summary = $"{playerName} slipped my grasp and escaped my hold. A debt I mean to collect.";
               type = NotableEventType.Conflict;

               break;
            case EndCaptivityDetail.ReleasedByChoice:
               summary = $"I let {playerName} walk free, of my own will. That was my choice to make, not theirs.";
               type = NotableEventType.Other;

               break;
            default: // Ransom, ReleasedAfterPeace, ReleasedAfterBattle, ReleasedByCompensation: no false memory
               return;
         }

         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, type, summary));
         store.Set(profile);
         ModLog.Info($"[NEMESIS] {_activeNemesisHero.Name} remembers the captivity ending (detail {detail}).");
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
            if (anchor != null && !NemesisMaterializePolicy.WithinRegion(new MapPoint(anchor.GetPosition2D.x, anchor.GetPosition2D.y), new MapPoint(pos.x, pos.y), NemesisMaterializePolicy.RegionMatchRadius))
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
      ///   LAZY captor attach (player report 2026-07-23: six days, three captor scenes, no memory between
      ///   them). The capture-time hook (<see cref="OnHeroPrisonerTaken" />) attaches unconditionally, but its
      ///   mapping is PROCESS-ONLY and does not survive the two things a multi-day captivity is full of:
      ///   a save/load (the virtual override means the band has no LeaderHero for
      ///   <see cref="ReestablishActiveCaptorOnLoad" /> to key on, so the restore always degrades) and a
      ///   prisoner transfer between bands (reference equality on the recorded party). Every scene after
      ///   either fell back to the ephemeral synthetic chief: fresh name-seed per band, never persisted, no
      ///   memory, no encyclopedia, unreachable by character_overrides. So the captor is now (re)attached at
      ///   SCENE time, the moment the identity starts to matter ("produce it when the player looks").
      ///   Reuses the exact mint-or-reuse path of the capture hook, INCLUDING the respawn cooldown (a slain
      ///   nemesis's quiet years stay quiet: during them this returns null and the faceless chief remains
      ///   correct). Deliberately does NOT bump TimesEncountered/RecaptureCount: this is the same captivity
      ///   resuming, not a new capture, and counting reloads would inflate the escape-decay.
      /// </summary>
      public Hero TryAttachCaptorFor(MobileParty band)
      {
         try
         {
            if (band == null || !band.IsBandit) return null;
            if (Hero.MainHero?.IsPrisoner != true) return null;
            if (Hero.MainHero.PartyBelongedToAsPrisoner?.MobileParty != band) return null;

            var today = (int) CampaignTime.Now.ToDays;

            // RC1: if THIS captivity already has a captor identity (set at capture, or restored on load), REUSE
            // him, whatever party now holds the player. Re-resolving by (culture, region) here was the reported
            // bug: a transfer to a hideout-anchored party made FindOrMint mint a SECOND nemesis who then
            // inherited the first one's captivity memory. One captivity keeps one captor.
            if (!string.IsNullOrEmpty(_activeCaptorIdentityId))
            {
               NemesisIdentity kept = _roster.Nemeses.FirstOrDefault(n => n.Id == _activeCaptorIdentityId);
               Hero keptHero = kept == null ? null : ResolveOrMintHero(kept, today);

               if (keptHero != null)
               {
                  _activeCaptorParty = band;
                  _activeNemesisHero = keptHero;
                  ModLog.Info($"[NEMESIS] scene time: reattached the SAME captor {keptHero.Name} (id {_activeCaptorIdentityId}) to the current party; not re-resolved by region.");

                  return keptHero;
               }

               // The remembered identity no longer resolves; fall through and resolve a fresh one below.
               _activeCaptorIdentityId = null;
            }

            // No captor identity yet for this captivity (the capture hook did not run, or a genuinely new
            // holder): resolve one now, and ANCHOR it so every later scene of this captivity reuses it.
            string bandKey = ResolveBandCultureKey(band);
            string regionId = ResolveRegionId(band);

            NemesisIdentity nem = FindOrMint(bandKey, regionId, today);
            Hero hero = ResolveOrMintHero(nem, today);

            if (hero == null) return null;

            _activeCaptorParty = band;
            _activeNemesisHero = hero;
            _activeCaptorIdentityId = nem.Id;
            ModLog.Info($"[NEMESIS] {hero.Name} attached at scene time as the captor (band {bandKey}, region {regionId}) and anchored (id {nem.Id}): the capture-time mapping was lost or never made.");

            return hero;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NEMESIS] lazy captor attach failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
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

      // REMOVED 2026-07-24 (AUDIT-NEMESIS M1): ResolveGearHolderForBand minted a nemesis UNCONDITIONALLY
      // (ignoreRespawnCooldown: true) to hold a fled bandit escape's loot, which resurrected a slain boss
      // during his own quiet years and inflated his counters. Bandit escape loot is now SCATTERED by culture
      // and recovered from defeated bands (ScatteredLootBehavior), so no nemesis is minted for loot at all.

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

         // RC3: never mint a homonym of a LIVING nemesis (two identical names amplified the "shared memory"
         // perception even when the profiles were distinct). GenerateDistinct re-rolls against the living set.
         NemesisNameResult name = NemesisNameGenerator.GenerateDistinct(
            bandCultureKey, MBRandom.RandomInt(int.MaxValue),
            (n, e) => _roster.Nemeses.Any(x =>
               x.IsAlive
               && string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Epithet, e, StringComparison.OrdinalIgnoreCase)));
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
         // RC1: the active captor's persisted identity id, additive key (absent on old saves reads as null =
         // no captor, exactly today's degradation). Bidirectional and simple: unlike the roster it needs no
         // frozen-copy race guard (a lone string, not a mutating list). Wrapped so a key fault never blocks
         // the roster load below.
         try { dataStore.SyncData("CR_NemesisActiveCaptorId", ref _activeCaptorIdentityId); }
         catch (Exception ex) { ModLog.Error($"[NEMESIS] active-captor id sync failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

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
