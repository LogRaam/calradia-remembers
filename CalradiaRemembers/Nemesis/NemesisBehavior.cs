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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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

      public NemesisBehavior()
      {
         Instance = this;
      }

      /// <summary>Every nemesis on record, living and dead.</summary>
      public IReadOnlyList<NemesisIdentity> All => _roster.Nemeses;

      public override void RegisterEvents()
      {
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
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
      ///   Reuse-or-mint the nemesis for this band culture + region AND ensure his persistent Hero exists
      ///   (name bound, memory profile registered). Returns the Hero, or null if minting failed. Shared by
      ///   the capture hook and the self-test harness so the mint path can be validated without a real capture.
      /// </summary>
      public Hero MintCaptorFor(string bandCultureKey, string regionId, int today)
      {
         NemesisIdentity nem = FindOrMint(bandCultureKey, regionId, today);

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
      ///   no Hero yet — the bandit-capture hook attaches a real Hero to it). The seed varies by roster size so
      ///   fresh bosses do not all share a name.
      /// </summary>
      public NemesisIdentity FindOrMint(string bandCultureKey, string regionId, int today)
      {
         NemesisIdentity existing = FindRecurring(bandCultureKey, regionId);
         if (existing != null) return existing;

         NemesisNameResult name = NemesisNameGenerator.Generate(bandCultureKey, _roster.Nemeses.Count);
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
