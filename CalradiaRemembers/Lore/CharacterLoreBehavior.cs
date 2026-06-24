// Code written by Gabriel Mailhot, 22/06/2026.
// Applies player-authored personalities. Authored trait values REPLACE the vanilla traits via
// Hero.SetTraitLevel at session launch, so the prompt's derived persona, the trait-derived mod features,
// and the campaign AI all read ONE source of truth. Turning the MCM toggle off restores the vanilla
// values, read back from the hero's character template (Hero stores its own traits separately).

#region

using System.Collections.Generic;
using System.ComponentModel;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Settings;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Lore
{
    /// <summary>Loads and applies/restores the authored character traits across the campaign.</summary>
    public sealed class CharacterLoreBehavior : CampaignBehaviorBase
    {
        public static CharacterLoreBehavior Instance { get; private set; }

        private static bool _subscribedToSettings;

        public CharacterLoreBehavior() => Instance = this;

        public override void RegisterEvents()
            => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SettingProfileLoader.LoadAndApply(); // world name/adjective + patriarchal cultures → PromptLore
            CharacterOverrideLoader.Load();
            SubscribeToToggle();
            if (ModSettings.UseCustomTraits) ApplyAll();
        }

        /// <summary>Reloads the files and re-applies (or restores) per the current toggle. For cr.reload_lore.</summary>
        public void ReloadAndReapply()
        {
            SettingProfileLoader.LoadAndApply();
            CharacterOverrideLoader.Load();
            if (ModSettings.UseCustomTraits) ApplyAll();
            else RestoreAll();
        }

        /// <summary>Sets every authored trait onto its hero. Returns how many heroes were touched.</summary>
        public int ApplyAll()
        {
            int n = 0;
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                CharacterOverride ov = CharacterOverrideLoader.Set.Resolve(hero.StringId, hero.Name?.ToString());
                if (ov == null || !ov.HasTraits) continue;

                foreach ((AuthoredTrait trait, int level) in ov.ClampedTraitLevels())
                {
                    TraitObject to = Map(trait);
                    if (to != null) hero.SetTraitLevel(to, level);
                }
                RederiveKinks(hero); // frozen kinks on an already-met NPC follow the new personality
                n++;
            }
            ModLog.Info($"[LORE] applied authored traits to {n} hero(es).");
            return n;
        }

        /// <summary>Restores the vanilla value (from the hero's template) of every authored trait.</summary>
        public int RestoreAll()
        {
            int n = 0;
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                CharacterOverride ov = CharacterOverrideLoader.Set.Resolve(hero.StringId, hero.Name?.ToString());
                if (ov == null || !ov.HasTraits) continue;

                foreach ((AuthoredTrait trait, int _) in ov.ClampedTraitLevels())
                {
                    TraitObject to = Map(trait);
                    // The template (CharacterObject) keeps the original level — Hero.SetTraitLevel only
                    // ever wrote the hero's own trait store, never the template.
                    if (to != null) hero.SetTraitLevel(to, hero.CharacterObject.GetTraitLevel(to));
                }
                RederiveKinks(hero); // back to the kinks the vanilla traits imply
                n++;
            }
            ModLog.Info($"[LORE] restored vanilla traits on {n} authored hero(es).");
            return n;
        }

        // ── Live MCM toggle ─────────────────────────────────────────────────

        private void SubscribeToToggle()
        {
            if (_subscribedToSettings) return;
            if (CrSettings.Instance is INotifyPropertyChanged settings)
            {
                settings.PropertyChanged += OnSettingChanged;
                _subscribedToSettings = true;
            }
        }

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
        /// </summary>
        private static void RederiveKinks(Hero hero)
        {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(hero.StringId);
            if (profile?.Romantic == null) return;

            List<Kink> fresh = RomanticProfileBuilder.DeriveKinks(hero, profile.Romantic.ArchetypeName);
            profile.Romantic.Kinks.Clear();          // init-only property, but the list itself is mutable
            profile.Romantic.Kinks.AddRange(fresh);
            store.Set(profile);
        }

        private static TraitObject Map(AuthoredTrait trait) => trait switch
        {
            AuthoredTrait.Valor       => DefaultTraits.Valor,
            AuthoredTrait.Honor       => DefaultTraits.Honor,
            AuthoredTrait.Mercy       => DefaultTraits.Mercy,
            AuthoredTrait.Generosity  => DefaultTraits.Generosity,
            AuthoredTrait.Calculating => DefaultTraits.Calculating,
            _                         => null
        };
    }
}
