// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Collections.Generic;
using System.ComponentModel;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Lore
{
   /// <summary>Loads and applies/restores the authored character traits across the campaign.</summary>
   public sealed class CharacterLoreBehavior : CampaignBehaviorBase
   {
      private static bool _subscribedToSettings;

      public CharacterLoreBehavior()
      {
         Instance = this;
         HeroProfileMapper.AuthoredTastesSync = (profile, hero) => CharacterOverrideLoader.SyncAuthoredTastes(profile, hero);
         EncounterContextBuilder.PastBondSource = CharacterOverrideLoader.PastBonds;
         MattersMostReader.AuthoredSource = CharacterOverrideLoader.MattersMostFor;
      }

      public static CharacterLoreBehavior Instance { get; private set; }

      /// <summary>Sets every authored trait onto its hero. Returns how many heroes were touched.</summary>
      public int ApplyAll()
      {
         var n = 0;
         foreach (Hero hero in Hero.AllAliveHeroes)
         {
            CharacterOverride ov = CharacterOverrideLoader.Set.Resolve(new HeroOverrideLookup {HeroId = hero.StringId, HeroName = hero.Name?.ToString(), TemplateId = CharacterOverrideLoader.TemplateKeyOf(hero)});

            // Skip only a truly-empty entry: ANY authored content (traits, a kink lock, a backstory, a conviction,
            // or orientation text) makes this a character cr.reload_lore should process. Earlier this counted only
            // traits and kinks as content, so a backstory/conviction/orientation-only character was skipped here
            // (player report: no "kinks" entry meant the character was not processed until kinks was added).
            if (ov == null) continue;

            if (!ov.HasAnyAuthoredContent)
            {
               // Never skip in SILENCE: the original report read as "my character is ignored" precisely because
               // nothing said so. Name the entry, so cr.lore_status / the log tell the author what was passed over.
               ModLog.Info($"[LORE] entry for '{ov.Hero ?? ov.Id ?? hero.Name?.ToString()}' has no authored field to apply, skipped.");

               continue;
            }

            // A value the author WROTE but nothing recognizes (a misspelled orientation, a mistyped kink) is
            // applied as nothing. Say so rather than letting the edit vanish without a word.
            foreach (string problem in ov.AuthoringProblems())
               ModLog.Info($"[LORE] {hero.Name}: {problem}");

            if (ov.HasTraits)
               foreach ((AuthoredTrait trait, int level) in ov.ClampedTraitLevels())
               {
                  TraitObject to = Map(trait);
                  if (to != null) hero.SetTraitLevel(to, level);
               }

            // Re-derive kinks only when this override actually drives them (authored traits, or an authored kink
            // lock); a backstory/conviction/orientation-only override must not re-roll and wipe an NPC's kinks.
            if (ov.HasTraits || ov.ParsedKinks != null)
               RederiveKinks(hero); // frozen kinks on an already-met NPC follow the new personality, or an authored lock
            // The gesture and preference locks land on the STORED profile here, after the kinks they are weighed against:
            // the Heart line of the encyclopedia reads the store as it is (Tene21, 03/11/2026: cr.acts showed the roll after
            // cr.reload_lore). Preferences joined 12/11/2026 (EvilPoptart1987: "'preferences' do not update"): an entry
            // with no traits or kinks never reached the kink re-derivation that laid them.
            if (ov.LocksStoredTastes)
               UpdateStoredTastes(hero, false);
            n++;
         }

         ModLog.Info($"[LORE] applied authored traits to {n} hero(es).");

         return n;
      }

      public override void RegisterEvents()
         => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

      /// <summary>Reloads the files and re-applies (or restores) per the current toggle. For cr.reload_lore.</summary>
      public void ReloadAndReapply()
      {
         SettingProfileLoader.LoadAndApply();
         CharacterOverrideLoader.Load();
         if (ModSettings.UseCustomTraits) ApplyAll();
         else RestoreAll();
      }

      /// <summary>Restores the vanilla value (from the hero's template) of every authored trait.</summary>
      public int RestoreAll()
      {
         var n = 0;
         foreach (Hero hero in Hero.AllAliveHeroes)
         {
            CharacterOverride ov = CharacterOverrideLoader.Set.Resolve(new HeroOverrideLookup {HeroId = hero.StringId, HeroName = hero.Name?.ToString(), TemplateId = CharacterOverrideLoader.TemplateKeyOf(hero)});

            // Same content gate as ApplyAll: visit any authored character. A traits/kink-lock override must be
            // visited so turning "Use custom traits" OFF actually releases its kink lock (RederiveKinks below
            // checks the toggle itself via CharacterOverrideLoader.KinksFor and no-ops the lock while it is off).
            if (ov == null || !ov.HasAnyAuthoredContent) continue;

            if (ov.HasTraits)
               foreach ((AuthoredTrait trait, int _) in ov.ClampedTraitLevels())
               {
                  TraitObject to = Map(trait);
                  // The template (CharacterObject) keeps the original level; Hero.SetTraitLevel only
                  // ever wrote the hero's own trait store, never the template.
                  if (to != null) hero.SetTraitLevel(to, hero.CharacterObject.GetTraitLevel(to));
               }

            if (ov.HasTraits || ov.ParsedKinks != null)
               RederiveKinks(hero); // back to the kinks the vanilla traits imply (lock is inert while the toggle is off)
            if (ov.Loves != null || ov.LessFond != null || ov.Dislikes != null)
               UpdateStoredTastes(hero, true); // the character's own roll back, re-rolled from their id after their kinks
            n++;
         }

         ModLog.Info($"[LORE] restored vanilla traits on {n} authored hero(es).");

         return n;
      }

      public override void SyncData(IDataStore dataStore) { }

      #region private

      private static TraitObject Map(AuthoredTrait trait) => trait switch {
         AuthoredTrait.Valor => DefaultTraits.Valor,
         AuthoredTrait.Honor => DefaultTraits.Honor,
         AuthoredTrait.Mercy => DefaultTraits.Mercy,
         AuthoredTrait.Generosity => DefaultTraits.Generosity,
         AuthoredTrait.Calculating => DefaultTraits.Calculating,
         _ => null
      };

      private static void OnSettingChanged(object sender, PropertyChangedEventArgs e)
      {
         if (e.PropertyName != nameof(CrSettings.UseCustomTraits)) return;
         if (Campaign.Current == null) return; // only meaningful inside a running campaign
         if (ModSettings.UseCustomTraits) Instance?.ApplyAll();
         else Instance?.RestoreAll();
      }

      /// <summary>
      ///   Re-derives the kinks of an ALREADY-MET NPC (one with a stored profile) from their current
      ///   traits, so a personality override doesn't leave kinks frozen against the old traits. A
      ///   never-met NPC has no profile yet and derives its kinks correctly when first built — nothing
      ///   to do here. The derivation is deterministic, so this is stable, not a reroll.
      ///   Then applies an authored KINKS LOCK on top when one exists (player request: a kink added at the
      ///   table with cr.add_kink was silently wiped by this very re-derivation on the next cr.reload_lore;
      ///   authoring the lock in character_overrides.json makes it survive instead).
      /// </summary>
      private static void RederiveKinks(Hero hero)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(hero.StringId);

         if (profile?.Romantic == null) return;

         List<Kink> fresh = RomanticProfileBuilder.DeriveKinks(hero, profile.Romantic.ArchetypeName);
         profile.Romantic.Kinks.Clear(); // init-only property, but the list itself is mutable
         profile.Romantic.Kinks.AddRange(fresh);

         IReadOnlyList<Kink> authoredKinks = CharacterOverrideLoader.KinksFor(hero);
         if (authoredKinks != null)
         {
            profile.Romantic.Kinks.Clear();
            profile.Romantic.Kinks.AddRange(authoredKinks);
            ModLog.Info($"[OVERRIDE] {profile.Name} kinks locked to [{string.Join(", ", authoredKinks)}] by authored char data.");
         }

         // Same lock, same reason as the kinks above: the reload re-derives the profile from current traits, so
         // an authored preference has to be re-applied here or it is wiped on the next cr.reload_lore.
         IReadOnlyList<NpcMemoryService.Core.Models.RomanticPreference> authoredPreferences =
            CharacterOverrideLoader.PreferencesFor(hero);
         if (authoredPreferences != null)
         {
            profile.Romantic.Preferences.Clear();
            profile.Romantic.Preferences.AddRange(authoredPreferences);
            ModLog.Info($"[OVERRIDE] {profile.Name} romantic preferences locked to [{string.Join(", ", authoredPreferences)}] by authored char data.");
         }

         store.Set(profile);
      }

      /// <summary>
      ///   Brings the stored profile of an already-met character up to date, which lays the authored gesture lock (or, with
      ///   <paramref name="lift" />, lifts it first so their own roll comes back). A never-met character has no profile yet
      ///   and is given the lock when first built and brought up to date.
      /// </summary>
      private static void UpdateStoredTastes(Hero hero, bool lift)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(hero.StringId);
         if (profile?.Romantic == null) return;

         if (lift) CharacterOverrideLoader.LiftAuthoredTastes(profile, hero);
         HeroProfileMapper.UpgradeIfNeeded(profile, hero);
         store.Set(profile);
      }

      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         SettingProfileLoader.LoadAndApply(); // world name/adjective + patriarchal cultures → PromptLore
         CharacterOverrideLoader.Load();
         Portraits.ChatPortraitResolver.CustomBaseFolder = NarrativeLoader.CustomBaseFolder; // portraits in Custom first (10/11/2026)
         Portraits.ChatPortraitResolver.EnsureFolder(); // ready place for the player to drop custom chat portraits
         SubscribeToToggle();
         if (ModSettings.UseCustomTraits) ApplyAll();
      }

      // ── Live MCM toggle ─────────────────────────────────────────────────

      private void SubscribeToToggle()
      {
         if (_subscribedToSettings) return;
         if (CrSettings.Instance is not INotifyPropertyChanged settings) return;

         settings.PropertyChanged += OnSettingChanged;
         _subscribedToSettings = true;
      }

      #endregion
   }
}