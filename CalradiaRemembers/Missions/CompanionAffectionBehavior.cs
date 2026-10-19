// Code written by Gabriel Mailhot, 10/07/2026.
// A faithful, content companion who has served the player through war may, now and then, ask for a private word
// to confide genuine affection and become the player's discreet SECRET LOVER (never a bargain, kept hidden to
// avoid jealousy). The positive counterpart to CompanionHappinessBehavior's grievance/retirement audiences: it
// fires only for a HAPPY companion, gated by orientation compatibility, a deep bond, and the adult-content level.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   Fires the occasional secret-affection audience for a happy, deeply-bonded companion. Persists a per
   ///   companion cooldown so a declined advance is not re-raised for a good while.
   /// </summary>
   public sealed class CompanionAffectionBehavior : CampaignBehaviorBase
   {
      private const string CooldownSaveKey = "CalradiaRemembers_CompanionAffectionAsked_v1";
      public static CompanionAffectionBehavior Instance;
      private Dictionary<string, int> _lastAskedDay = new(); // companion id → day last asked (cooldown anchor)

      public CompanionAffectionBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>Runs the weekly pass on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      /// <summary>Forces the audience request for one companion now, ignoring the roll (console test path).</summary>
      public string TryRequestFor(Hero h)
      {
         if (h == null) return "No hero.";

         (bool eligible, string why) = EligibilityOf(h);
         if (!eligible) return $"{h.Name} is not eligible for a secret-affection audience: {why}.";

         RequestAudience(h);

         return $"{h.Name} asks for a private word.";
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_lastAskedDay, Formatting.None)); }
            catch { bytes = Encoding.UTF8.GetBytes("{}"); }

            try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[AFFECTION] save failed: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is not {Length: > 0}) return;
            try { _lastAskedDay = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>(); }
            catch { _lastAskedDay = new Dictionary<string, int>(); }
         }
      }

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      /// <summary>Whether this companion could confide, plus a human reason when not (for the console).</summary>
      private (bool eligible, string why) EligibilityOf(Hero h)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile?.Romantic == null) return (false, "no romantic profile");

         bool adult = ModSettings.AdultContentLevel != AdultContentLevel.Off;
         bool compatible = Hero.MainHero != null && profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale);
         bool content = !CompanionHappiness.WantsToLeave(CompanionHappinessService.Get(h));
         bool attached = profile.Romantic.IsConsort || profile.Romantic.IsSecretLover
                         || profile.Romantic.Status == RomanticStatus.Committed || h == Hero.MainHero?.Spouse;

         if (CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = compatible, AdultEnabled = adult, IsContent = content, AlreadyAttached = attached, Regard = profile.ReputationWithPlayer, Warmth = ModSettings.RelationWarmth}))
            return (true, "");

         if (!adult) return (false, "adult content is Off");
         if (!compatible) return (false, "orientation not compatible");
         if (!content) return (false, "the companion is not content");
         if (attached) return (false, "already bound to you");

         return (false, $"regard {profile.ReputationWithPlayer} is below {CompanionSecretAffectionPolicy.MinRegardFor(ModSettings.RelationWarmth)} at the {ModSettings.RelationWarmth} pacing");
      }

      private void GrantAudience(Hero h)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile == null) return;

         ChatManager.OpenForCompanionAudience(h, profile, CompanionAudienceReason.SecretAffection, false);
         ModLog.Info($"[AFFECTION] {h.Name}'s secret-affection audience opened.");
      }

      private bool InCooldown(Hero h)
         => _lastAskedDay.TryGetValue(h.StringId, out int d) && Today - d < CompanionSecretAffectionPolicy.RefusalCooldownDays;

      private void OnWeeklyTick()
      {
         if (ModSettings.AdultContentLevel == AdultContentLevel.Off) return;

         foreach (Hero h in Companions())
         {
            Hero companion = h;
            // One companion's pass throwing must not skip the rest of the roster.
            SafeRun.Step($"CompanionAffectionBehavior.OnWeeklyTick({companion.Name})", () =>
            {
               if (InCooldown(companion)) return;

               (bool eligible, string _) = EligibilityOf(companion);
               if (!eligible) return;

               NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion.StringId);
               if (profile == null) return;

               if (!CompanionSecretAffectionPolicy.ShouldConfideThisWeek(new NpcMemoryService.Core.Standing.Regard(profile.ReputationWithPlayer), MBRandom.RandomInt(100), ModSettings.RelationWarmth))
                  return;

               RequestAudience(companion);
            });
         }
      }

      private void RequestAudience(Hero h)
      {
         _lastAskedDay[h.StringId] = Today; // asked now: the cooldown starts whether the player hears them or not

         var title = new TextObject($"{h.Name} asks for a private word");
         var body = new TextObject(
            $"{h.Name} seems to have something personal on their mind, and asks for a quiet moment alone with you.");

         // Neutral wording on purpose: the reveal belongs in the conversation, not the popup.
         // A companion is known: no "Who:" line, but the "Who is this?" button to their page (Lordfadooboo, 13/10/2026).
         Interception.AudienceInquiry.Show(h, title.ToString(), body.ToString(), "Speak with them", "Not now",
            () => GrantAudience(h),
            () => ModLog.Info($"[AFFECTION] {h.Name}'s private word was deferred."), sayWho: false);
      }

      #endregion
   }
}
