// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Lore
{
   /// <summary>
   ///   Loads and caches <c>character_overrides.json</c> (an array of per-NPC entries) into a
   ///   <see cref="CharacterOverrideSet" />. Safe to (re)load on session launch or on demand.
   /// </summary>
   public static class CharacterOverrideLoader
   {
      /// <summary>The current authored overrides; never null once <see cref="Load" /> has run.</summary>
      public static CharacterOverrideSet Set { get; private set; } = new(null);

      /// <summary>The path the last <see cref="Load" /> looked at, for the diagnostic console command.</summary>
      public static string LastLoadPath { get; private set; }

      /// <summary>Whether character_overrides.json existed at the last <see cref="Load" />.</summary>
      public static bool LastLoadFileExisted { get; private set; }

      /// <summary>How many overrides the last <see cref="Load" /> produced.</summary>
      public static int LastLoadCount { get; private set; }

      /// <summary>
      ///   Null when the last <see cref="Load" /> was clean (file missing, or parsed OK); set to the
      ///   parse exception's message when the file exists but is malformed, so a player-facing diagnostic
      ///   (cr.lore_status) can report the exact reason without needing the logging toggle on.
      /// </summary>
      public static string LastLoadError { get; private set; }

      /// <summary>
      ///   The authored backstory for <paramref name="hero" /> when custom data is enabled, else null.
      ///   Used to sync <see cref="NpcMemoryService.Core.Models.NpcProfile.AuthoredBackstory" /> before
      ///   each prompt, so the flavor is never stored stale.
      /// </summary>
      public static string BackstoryFor(Hero hero)
      {
         if (!ModSettings.UseCustomTraits) return null;
         CharacterOverride ov = For(hero);

         return ov is {HasBackstory: true}
            ? ov.Backstory
            : null;
      }

      /// <summary>
      ///   The authored CONVICTION for this hero: what they hold to be true, and therefore what they pursue.
      ///   Null when unset or when custom traits are off, exactly like the backstory it sits beside. Unlike the
      ///   backstory (voice only), this one reaches the prompt as a MOTIVE, so it is synced per turn the same way.
      /// </summary>
      public static string ConvictionFor(Hero hero)
      {
         if (!ModSettings.UseCustomTraits) return null;
         CharacterOverride ov = For(hero);

         return ov is {HasConviction: true}
            ? ov.Conviction
            : null;
      }

      /// <summary>
      ///   Copies the two authored PROSE fields (voice, and what the hero holds true) onto the profile that is
      ///   about to be sent to the model. Called on EVERY turn, never persisted deliberately: a stale copy would
      ///   survive a file edit or the MCM toggle being turned off, and the NPC would keep an authored persona the
      ///   player had already withdrawn. Both fields go to null when custom data is off, which erases any copy an
      ///   earlier turn left on the profile.
      ///   Shared with the self-test harness on purpose: <c>cr.test conviction</c> exercises THIS method, so the
      ///   test fails if the chat's per-turn sync is ever dropped, instead of quietly passing on a parallel path.
      /// </summary>
      public static void SyncAuthoredProse(NpcMemoryService.Core.Models.NpcProfile profile, Hero hero)
      {
         if (profile == null || hero == null) return;

         profile.AuthoredBackstory = BackstoryFor(hero);
         profile.AuthoredConviction = ConvictionFor(hero);
      }

      /// <summary>
      ///   The authored sexual orientation for <paramref name="hero" /> when custom data is enabled and set,
      ///   else null. Synced onto the NPC's RomanticProfile before each prompt so an edit takes effect live.
      /// </summary>
      public static NpcMemoryService.Core.Models.SexualOrientation? OrientationFor(Hero hero)
         => ModSettings.UseCustomTraits ? For(hero)?.ParsedOrientation : null;

      /// <summary>
      ///   The authored, LOCKED kinks for <paramref name="hero" /> when custom data is enabled and the
      ///   override sets them, else null. Unlike <see cref="OrientationFor" />, an override may lock kinks
      ///   to an EMPTY list (no kinks at all): null here means "no lock", not "no kinks". Synced onto the
      ///   NPC's RomanticProfile both live (per turn) and at the lore-reload seam, so an authored lock
      ///   survives cr.reload_lore instead of being overwritten by the usual trait-derived re-roll.
      /// </summary>
      public static IReadOnlyList<NpcMemoryService.Core.Models.Kink> KinksFor(Hero hero)
         => ModSettings.UseCustomTraits ? For(hero)?.ParsedKinks : null;

      /// <summary>
      ///   The authored romantic-preference lock for this hero, or null when unset or the custom-traits toggle
      ///   is off. Mirrors <see cref="KinksFor" />, and like it survives cr.reload_lore, which otherwise
      ///   re-derives a profile from current traits and would wipe the author's choice.
      /// </summary>
      public static IReadOnlyList<NpcMemoryService.Core.Models.RomanticPreference> PreferencesFor(Hero hero)
         => ModSettings.UseCustomTraits ? For(hero)?.ParsedPreferences : null;

      /// <summary>The authored loved and disliked gestures for this hero (each null when left out), or nulls when the toggle is off.</summary>
      public static (List<NpcMemoryService.Core.Models.IntimateAct> Loves, List<NpcMemoryService.Core.Models.IntimateAct> LessFond, List<NpcMemoryService.Core.Models.IntimateAct> Dislikes) ActsFor(Hero hero)
      {
         CharacterOverride o = ModSettings.UseCustomTraits ? For(hero) : null;

         return (o?.ParsedLoves, o?.ParsedLessFond, o?.ParsedDislikes);
      }

      /// <summary>
      ///   Lays the authored gesture lock ("loves", "lessFond", "dislikes") onto the profile, the ONE place it is written
      ///   (Tene21, Nexus, 03/11/2026: cr.acts still showed the roll after cr.reload_lore, because the lock was copied only
      ///   when a chat message was sent). Called by <see cref="HeroProfileMapper.UpgradeIfNeeded" />, so every reader that
      ///   brings a profile up to date sees it, and by <see cref="CharacterLoreBehavior.ApplyAll" /> for the stored
      ///   profiles the encyclopedia reads as they are. Does nothing while custom traits are off or no list is written.
      ///   Returns true when the tastes changed.
      /// </summary>
      public static bool SyncAuthoredTastes(NpcProfile profile, Hero hero)
      {
         RomanticProfile r = profile?.Romantic;
         if (r == null || hero == null) return false;

         bool preferencesChanged = SyncAuthoredPreferences(profile, hero);

         IntimateTastes now = TastesOf(r);
         var acts = ActsFor(hero);
         IntimateTastes locked = AuthoredTastesPolicy.Apply(new AuthoredTastesFacts {Current = now, Loves = acts.Loves, LessFond = acts.LessFond, Dislikes = acts.Dislikes});
         if (locked == null || Same(locked, now)) return preferencesChanged;

         r.LovedActs = locked.Loved;
         r.LessFondActs = locked.LessFond;
         r.DislikedActs = locked.Disliked;
         ModLog.Info($"[OVERRIDE] {profile.Name} gestures locked by authored char data: loves [{string.Join(", ", locked.Loved)}], less fond [{string.Join(", ", locked.LessFond)}], dislikes [{string.Join(", ", locked.Disliked)}].");

         return true;
      }

      /// <summary>
      ///   Lays the authored romantic-preference lock on the profile, wherever the gestures are laid (a profile brought up
      ///   to date, a reload, each turn). EvilPoptart1987 (Nexus, 12/11/2026): "some edits work, but 'preferences' do not
      ///   update". Returns true when the preferences changed.
      /// </summary>
      private static bool SyncAuthoredPreferences(NpcProfile profile, Hero hero)
      {
         IReadOnlyList<RomanticPreference> authored = PreferencesFor(hero);
         List<RomanticPreference> current = profile.Romantic?.Preferences;
         if (authored == null || current == null || current.SequenceEqual(authored)) return false;

         current.Clear();
         current.AddRange(authored);
         ModLog.Info($"[OVERRIDE] {profile.Name} romantic preferences locked to [{string.Join(", ", authored)}] by authored char data.");

         return true;
      }

      /// <summary>
      ///   Lifts a gesture lock that had landed on the profile (custom traits turned off): the three lists go back to null so
      ///   the next <see cref="HeroProfileMapper.UpgradeIfNeeded" /> rolls the character's own tastes again from their id.
      ///   A profile whose tastes are still the roll is left alone. Returns true when the lock was lifted.
      /// </summary>
      public static bool LiftAuthoredTastes(NpcProfile profile, Hero hero)
      {
         RomanticProfile r = profile?.Romantic;
         CharacterOverride o = For(hero);
         if (r == null || o == null) return false;

         var f = new AuthoredTastesFacts {Current = TastesOf(r), Loves = o.ParsedLoves, LessFond = o.ParsedLessFond, Dislikes = o.ParsedDislikes};
         if (!AuthoredTastesPolicy.LockIsInPlace(f)) return false;

         r.LovedActs = null;
         r.LessFondActs = null;
         r.DislikedActs = null;
         ModLog.Info($"[OVERRIDE] {profile.Name} gesture lock lifted (custom traits off): their own tastes are rolled again.");

         return true;
      }

      private static IntimateTastes TastesOf(RomanticProfile r) => new() {
         Loved = r.LovedActs ?? new List<IntimateAct>(), LessFond = r.LessFondActs ?? new List<IntimateAct>(), Disliked = r.DislikedActs ?? new List<IntimateAct>()
      };

      private static bool Same(IntimateTastes a, IntimateTastes b)
         => a.Loved.SequenceEqual(b.Loved) && a.LessFond.SequenceEqual(b.LessFond) && a.Disliked.SequenceEqual(b.Disliked);

      /// <summary>The override that applies to <paramref name="hero" />, or null.</summary>
      public static CharacterOverride For(Hero hero)
         => hero == null
            ? null
            : Set.Resolve(hero.StringId, hero.Name?.ToString(), TemplateKeyOf(hero));

      /// <summary>
      ///   The template id <paramref name="hero" /> may be matched on: none for a hero born of parents, whose template
      ///   is a parent's own character (see <see cref="CharacterOverrideSet.TemplateKey" />).
      /// </summary>
      public static string TemplateKeyOf(Hero hero)
         => hero == null ? null : CharacterOverrideSet.TemplateKey(hero.Template?.StringId, hero.Mother != null || hero.Father != null);

      /// <summary>
      ///   Adds (or replaces) a single override at runtime, keyed by hero id/name, without touching
      ///   character_overrides.json. Used by the console to inject a test backstory into the live prompt
      ///   pipeline (the per-turn sync reads <see cref="BackstoryFor" /> from this set). A session reload
      ///   restores the file-backed set. Callers must also ensure custom traits are enabled for it to apply.
      /// </summary>
      public static void InjectRuntimeOverride(CharacterOverride ov)
      {
         if (ov != null) Set = Set.With(ov);
      }

      public static void Load()
      {
         // Custom-first (player report): character_overrides.json was the ONE lore file not read from
         // ModuleData/Custom, so a copy there was ignored and only the shipped ModuleData copy worked, which is
         // exactly the one a mod update overwrites. Route it through the same shared resolver every other lore
         // file (world.txt, the guideline files, cultural_traditions.json) already uses, so an authored file in
         // Custom is honoured AND survives updates.
         string path = NarrativeLoader.ResolveCustomFirst("character_overrides.json");

         LastLoadPath = path;

         if (!File.Exists(path))
         {
            Set = new CharacterOverrideSet(null);
            LastLoadFileExisted = false;
            LastLoadCount = 0;
            LastLoadError = null;
            ModLog.Info($"  character_overrides.json not found — no authored characters. (looked in: {path})");

            return;
         }

         LastLoadFileExisted = true;

         try
         {
            string json = File.ReadAllText(path);
            List<CharacterOverride> entries =
               JsonConvert.DeserializeObject<List<CharacterOverride>>(json) ?? new List<CharacterOverride>();
            Set = new CharacterOverrideSet(entries);
            LastLoadCount = Set.Count;
            LastLoadError = null;
            ModLog.Info($"  Loaded {Set.Count} authored character override(s) from ModuleData.");
         }
         catch (Exception ex)
         {
            Set = new CharacterOverrideSet(null);
            LastLoadCount = 0;
            LastLoadError = ex.Message;
            ModLog.Error($"  character_overrides.json failed to parse — authored characters disabled: {ex.Message}");
            ModLog.ConfigError($"character_overrides.json failed to parse ({ex.Message}). Authored characters are disabled until it is fixed.");
         }
      }
   }
}