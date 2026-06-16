// Code written by Gabriel Mailhot, 14/06/2026.
// Sprint 21: captive pregnancy — a forced vaginal act may leave a female player with child.

#region

using System;
using System.Collections;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Conception consequence for a captive scene. When the captor's narration flags a
   ///   vaginal act carried to completion (the <c>impregnation_risk</c> action), a female
   ///   player of fertile age has a small chance to fall pregnant.
   ///
   ///   The father is the real captor when one exists (a lord); for a faceless bandit the
   ///   child is attributed to a single shared, hidden "Unknown" hero — reused for every
   ///   anonymous conception so saves never bloat. Because the MOTHER is the player
   ///   (MainHero), the engine's own <c>DeliverOffSpring</c> places the child in the
   ///   player's clan and culture regardless of who the father is, so the child is a true
   ///   heir of the player's house.
   ///
   ///   All engine reach-through is reflective (the <c>Pregnancy</c> record and the
   ///   behavior's pregnancy list are non-public) and fully guarded: any failure logs and
   ///   leaves the campaign untouched rather than risking a crash.
   /// </summary>
   public static class CaptivePregnancyService
   {
      private const float  ConceptionChance      = 0.15f;
      private const float  MinFertileAge         = 18f;
      private const float  MaxFertileAge         = 45f;
      private const string UnknownFatherStringId = "cr_unknown_father";

      /// <summary>
      ///   Rolls for conception and, on success, creates the pregnancy. Returns a short
      ///   log-only note describing the outcome, or null when nothing happened (ineligible
      ///   player, failed roll, or an engine error). Pass <paramref name="force" /> true to
      ///   skip the chance roll — used by the console test command only.
      /// </summary>
      public static string TryConceive(Hero father, bool force = false)
      {
         try
         {
            Hero mother = Hero.MainHero;
            if (!IsEligible(mother)) return null;
            if (!force && MBRandom.RandomFloat >= ConceptionChance) return null;

            Hero effectiveFather = father ?? GetOrCreateUnknownFather();
            if (effectiveFather == null)
            {
               ModLog.Error("[PREGNANCY] no father resolved — conception aborted.");
               return null;
            }

            if (!CreatePregnancy(mother, effectiveFather)) return null;

            string fatherName = father?.Name?.ToString() ?? "an unknown man";
            ModLog.Info($"[PREGNANCY] {mother.Name} conceived a child (father: {fatherName}).");
            return $"A child has been conceived (father: {fatherName}).";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PREGNANCY] conception failed: {ex.Message}");
            return null;
         }
      }

      #region private

      private static bool IsEligible(Hero mother)
      {
         if (mother == null || !mother.IsFemale || !mother.IsAlive) return false;
         if (mother.Age < MinFertileAge || mother.Age > MaxFertileAge) return false;
         if (IsAlreadyPregnant(mother)) return false;
         return true;
      }

      // ── Engine reach-through (reflective, cached) ──────────────────────────

      private static Type      _pregnancyType;
      private static FieldInfo _motherField;
      private static FieldInfo _listField;

      private static bool ResolveReflection()
      {
         if (_pregnancyType != null && _motherField != null && _listField != null) return true;

         _pregnancyType = typeof(PregnancyCampaignBehavior)
            .GetNestedType("Pregnancy", BindingFlags.NonPublic);
         _motherField = _pregnancyType?.GetField("Mother",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
         _listField = typeof(PregnancyCampaignBehavior)
            .GetField("_heroPregnancies", BindingFlags.NonPublic | BindingFlags.Instance);

         if (_pregnancyType == null || _motherField == null || _listField == null)
         {
            ModLog.Error("[PREGNANCY] could not resolve engine pregnancy internals — feature disabled this build.");
            return false;
         }

         return true;
      }

      private static IList PregnancyList()
      {
         PregnancyCampaignBehavior behavior =
            Campaign.Current?.GetCampaignBehavior<PregnancyCampaignBehavior>();
         if (behavior == null) return null;
         return _listField.GetValue(behavior) as IList;
      }

      private static bool IsAlreadyPregnant(Hero mother)
      {
         if (!ResolveReflection()) return false; // cannot tell → let the create-path guard instead
         IList list = PregnancyList();
         if (list == null) return false;

         foreach (object pregnancy in list)
            if (_motherField.GetValue(pregnancy) as Hero == mother)
               return true;

         return false;
      }

      private static bool CreatePregnancy(Hero mother, Hero father)
      {
         if (!ResolveReflection()) return false;

         IList list = PregnancyList();
         if (list == null)
         {
            ModLog.Error("[PREGNANCY] pregnancy list unavailable — conception aborted.");
            return false;
         }

         float days = Campaign.Current.Models.PregnancyModel.PregnancyDurationInDays;
         CampaignTime due = CampaignTime.DaysFromNow(days);

         // Pregnancy(Hero pregnantHero, Hero father, CampaignTime dueDate) — non-public ctor.
         object pregnancy = Activator.CreateInstance(
            _pregnancyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { mother, father, due },
            culture: null);

         list.Add(pregnancy);
         return true;
      }

      // ── Shared "Unknown" father ────────────────────────────────────────────

      /// <summary>
      ///   The single hidden hero who fathers every anonymous (bandit) conception. Looked up
      ///   by a fixed string id so it survives save/load without a static cache, and created
      ///   once — dead and clanless — so it never wanders the map or appears as a living lord.
      /// </summary>
      private static Hero GetOrCreateUnknownFather()
      {
         Hero existing = MBObjectManager.Instance?.GetObject<Hero>(UnknownFatherStringId);
         if (existing != null) return existing;

         CharacterObject template = MBObjectManager.Instance?.GetObject<CharacterObject>("looter");
         if (template == null)
         {
            ModLog.Error("[PREGNANCY] no template for the Unknown father — conception aborted.");
            return null;
         }

         // isAlive: false — a deceased, inert record. Bannerlord routinely references dead
         // parents, so this is a valid father for the child without an active hero loose in
         // the world.
         HeroCreator.CreateBasicHero(UnknownFatherStringId, template, out Hero father, false);
         if (father == null) return null;

         try { father.SetName(new TextObject("Unknown"), new TextObject("Unknown")); }
         catch { /* a generated name is acceptable if SetName is unavailable */ }

         return father;
      }

      #endregion
   }
}
