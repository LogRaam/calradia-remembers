// Code written by Gabriel Mailhot, 25/06/2026.
// Birth-time clan correction for CR-created pregnancies. A child of a CR conception ALWAYS belongs to the
// MOTHER's clan: player-mother / legitimate-wife → the player's clan (they are already in it), another woman →
// her own house (a bastard). Vanilla DeliverOffSpring routes a player-FATHERED child into the player's clan,
// so we reassign it at birth — but ONLY for pregnancies CR itself created, leaving vanilla NPC patrilineality
// untouched. See PregnancyService for the conception side.
//
// 05/08/2026 addition, stuck-pregnancy cleanup (player report): a campaign with the Life/Death Cycle option
// disabled (no aging, no death) never runs vanilla's own offspring-delivery check (decompiled:
// PregnancyCampaignBehavior.DailyTickHero gates the whole thing behind !IsLifeDeathCycleDisabled), so a CR
// conception made under that setting sat "pregnant" forever and the NPC never stopped mentioning it in chat.
// The daily tick below sweeps every CR-tracked mother and, only when births are disabled AND her due date is
// well past, ends the pregnancy CHILDLESS (no child is born, which would override the player's own no-birth
// choice). This is a deliberate compromise: unsticking the conversation without silently reversing a campaign
// setting the player deliberately chose.
//
// 10/08/2026 addition, LOVER-CHILDREN (open relationship Inc 3c): a committed partner who takes a lover may
// conceive a child BY HIM on a tryst. That child is the LOVER's (its genetic father) and its mother's, and must
// NEVER land in the player-husband's clan. The ordinary rule above would misplace it: a wife married INTO the
// player's clan has mother.Clan == the player's clan, so the mother-clan reassignment would adopt her lover's
// child. A lover-child is therefore tracked separately (mother id -> lover id, an additive, save-safe list) and
// placed by LoverChildClanPolicy: the lover's line when he has one, else the mother's line only when it is not
// the player's, never the player's clan. Ordinary (player or normal NPC) conceptions are wholly untouched: they
// never carry a lover-child link, so they take the unchanged mother-clan path (or, for vanilla NPCs, no path).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Tracks the mothers CR has made pregnant and, at birth, places their offspring in the MOTHER's clan.
   ///   The tracked set is persisted (a pregnancy spans ~84 game-days, often across saves). Also runs a
   ///   daily sweep that ends a pregnancy childless when the campaign's Life/Death Cycle option is disabled
   ///   and her due date has gone stuck well past due (see the 05/08/2026 file-header note).
   /// </summary>
   public sealed class PregnancyBirthBehavior : CampaignBehaviorBase
   {
      /// <summary>
      ///   Tuning: how many days past her due date a mother must be, WITH births disabled, before the
      ///   daily sweep ends her pregnancy. Gives vanilla's own check (and ordinary day-boundary jitter
      ///   right at the due date) a fair window first. See <see cref="CalradiaRemembers.Logic.PregnancyPolicy" />.
      /// </summary>
      private const int OverdueMarginDays = 5;

      private const string SaveKey = "CalradiaRemembers_CrExpectingMothers_v1";

      // Inc 3c: lover-child links, one entry per pregnant mother, encoded "motherId|loverId". A separate,
      // additive save key so older saves simply lack it (the list stays empty, no migration). The "|" separator
      // never appears in a hero StringId.
      private const string LoverChildSaveKey = "CalradiaRemembers_CrLoverChildren_v1";

      public static PregnancyBirthBehavior Instance { get; private set; }

      private List<string> _crMotherIds = new();
      private List<string> _loverChildLinks = new();

      public PregnancyBirthBehavior() => Instance = this;

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         dataStore.SyncData(SaveKey, ref _crMotherIds);

         // Additive, save-safe: an older save lacks this key, so on load the list is simply left empty (no
         // migration). Mirrors the CrExpectingMothers key above. Never break deserialization: a native
         // List<string> the same shape the engine already round-trips for _crMotherIds.
         dataStore.SyncData(LoverChildSaveKey, ref _loverChildLinks);
      }

      /// <summary>
      ///   Registers a mother CR has just made pregnant, so her child is reassigned to her clan at birth.
      ///   Called by <see cref="PregnancyService" /> after a successful conception.
      /// </summary>
      public void TrackConception(Hero mother)
      {
         if (mother == null) return;
         if (!_crMotherIds.Contains(mother.StringId)) _crMotherIds.Add(mother.StringId);
      }

      /// <summary>
      ///   Inc 3c: registers a pregnancy as a LOVER-CHILD, remembering the genetic father (the
      ///   <paramref name="lover" />) so the child is placed in HIS line at birth, never the player-husband's
      ///   clan. Called by the open-partner tryst pass after a successful conception by the lover, in addition to
      ///   the ordinary <see cref="TrackConception" /> the reflective conception already fires. The lover-child
      ///   link wins over the mother-clan path in <see cref="OnGivenBirth" />. Replaces any prior link for the
      ///   same mother (a mother carries at most one pregnancy at a time).
      /// </summary>
      public void TrackLoverChild(Hero mother, Hero lover)
      {
         if (mother == null || lover == null) return;

         RemoveLoverChildLink(mother.StringId);
         _loverChildLinks.Add(mother.StringId + "|" + lover.StringId);
      }

      /// <summary>
      ///   Test seam (cr.testall lover_child): the lover (genetic father) StringId recorded for a lover-child
      ///   pregnancy on <paramref name="mother" />, or null when she carries no tracked lover-child. Lets the
      ///   self-test assert the persisted attribution marker names the lover, since a birth cannot be forced.
      /// </summary>
      public string DebugLoverChildFatherId(Hero mother)
      {
         if (mother == null) return null;

         string prefix = mother.StringId + "|";
         string entry = _loverChildLinks.FirstOrDefault(s => s != null && s.StartsWith(prefix, StringComparison.Ordinal));

         return entry?.Substring(prefix.Length);
      }

      /// <summary>Test seam (cr.testall lover_child cleanup): drops all CR tracking (expecting-mother + lover-child link) for <paramref name="mother" />.</summary>
      public void DebugForgetTracking(Hero mother)
      {
         if (mother == null) return;
         _crMotherIds.Remove(mother.StringId);
         RemoveLoverChildLink(mother.StringId);
      }

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         if (mother == null || aliveChildren == null) return;

         // A LOVER-CHILD (Inc 3c) is resolved FIRST and by its own rule: it belongs to the genetic father (the
         // lover), never the player-husband's clan. Drop it from BOTH tracked sets so neither leaks (the
         // reflective conception adds the mother to _crMotherIds too).
         bool isLoverChild = TryTakeLoverChild(mother.StringId, out string loverId);
         bool wasCrPregnancy = _crMotherIds.Remove(mother.StringId);

         if (isLoverChild)
         {
            PlaceLoverChild(mother, loverId, aliveChildren);

            return;
         }

         // A wife who kept her own house (giving her all, 26/10/2026): the child is of her line, whoever made the pregnancy.
         bool ofHerLine = LeaveHouseForRulerPolicy.ChildBelongsToHerHouse(new UnionChildFacts {
            MotherIsThePlayersWife = mother.Spouse != null && mother.Spouse == Hero.MainHero,
            MotherKeepsHerOwnHouse = mother.Clan != null && mother.Clan != Clan.PlayerClan
         });

         // Only correct CR's own pregnancies; vanilla NPC births keep their (patrilineal) clan. Ordinary
         // conceptions are unchanged: they land the child in the MOTHER's clan exactly as before.
         if (!wasCrPregnancy && !ofHerLine) return;

         Clan motherClan = mother.Clan;
         foreach (Hero child in aliveChildren)
         {
            if (child == null || child.Clan == motherClan) continue;
            child.Clan = motherClan;
            ModLog.Info($"[PREGNANCY] CR birth: moved {child.Name} into the mother's clan ({motherClan?.Name?.ToString() ?? "—"}).");
         }
      }

      /// <summary>
      ///   Inc 3c: places a lover-child in the genetic father's line, never the player-husband's clan. The line
      ///   is chosen by the pure <see cref="CalradiaRemembers.Logic.LoverChildClanPolicy" />: the lover's clan
      ///   when he has one, else the mother's clan only when it is not the player's, else the child is left where
      ///   the engine placed it (vanilla routes a lover-fathered child into the FATHER's line already, so this is
      ///   normally a confirmation, not a move). The player's clan is never a valid destination, defended here in
      ///   code even though the policy already refuses it.
      /// </summary>
      private static void PlaceLoverChild(Hero mother, string loverId, List<Hero> aliveChildren)
      {
         Hero lover = string.IsNullOrEmpty(loverId)
            ? null
            : Hero.FindFirst(h => h.StringId == loverId);

         Clan playerClan = Clan.PlayerClan;
         Clan loverClan = lover?.Clan;
         Clan motherClan = mother?.Clan;

         LoverChildClanTarget verdict = LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = loverClan != null, LoverClanIsPlayer = loverClan != null && loverClan == playerClan, MotherHasClan = motherClan != null, MotherClanIsPlayer = motherClan != null && motherClan == playerClan});

         Clan destination = verdict switch {
            LoverChildClanTarget.LoverClan => loverClan,
            LoverChildClanTarget.MotherClan => motherClan,
            _ => null
         };

         foreach (Hero child in aliveChildren)
         {
            if (child == null) continue;

            // The invariant, defended in code: a lover-child must NEVER live in the player-husband's clan.
            if (destination == null || destination == playerClan)
            {
               if (child.Clan == playerClan)
                  ModLog.Error($"[PREGNANCY] lover-child {child.Name} has no non-player line to take (clanless lover, mother in the player's house); left where the engine placed it. This should not happen for a lord lover.");

               continue;
            }

            if (child.Clan == destination) continue;
            child.Clan = destination;
            ModLog.Info($"[PREGNANCY] lover-child {child.Name} placed in the genetic father's line ({destination.Name?.ToString() ?? "(none)"}), never the player's clan.");
         }
      }

      /// <summary>
      ///   Pops the lover-child link for <paramref name="motherId" /> (if any), outing the lover's StringId.
      ///   Returns false when this pregnancy is not a tracked lover-child.
      /// </summary>
      private bool TryTakeLoverChild(string motherId, out string loverId)
      {
         loverId = null;
         if (string.IsNullOrEmpty(motherId)) return false;

         string prefix = motherId + "|";
         int idx = _loverChildLinks.FindIndex(s => s != null && s.StartsWith(prefix, StringComparison.Ordinal));
         if (idx < 0) return false;

         loverId = _loverChildLinks[idx].Substring(prefix.Length);
         _loverChildLinks.RemoveAt(idx);

         return true;
      }

      /// <summary>Drops any lover-child link for <paramref name="motherId" /> (a mother who died, was cleared, or is being re-tracked).</summary>
      private void RemoveLoverChildLink(string motherId)
      {
         if (string.IsNullOrEmpty(motherId)) return;
         string prefix = motherId + "|";
         _loverChildLinks.RemoveAll(s => s != null && s.StartsWith(prefix, StringComparison.Ordinal));
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         // A mother who dies while pregnant will never give birth — drop her so the set can't leak.
         if (victim == null) return;
         _crMotherIds.Remove(victim.StringId);
         RemoveLoverChildLink(victim.StringId); // Inc 3c: drop any lover-child link too
      }

      /// <summary>
      ///   Daily sweep for the stuck-pregnancy bug (see file header): while births are enabled this does
      ///   nothing at all, since vanilla's own daily check delivers on schedule and CR must never step on
      ///   that. Only when the campaign's Life/Death Cycle option is disabled does it check every
      ///   CR-tracked mother's due date and, once she is overdue past <see cref="OverdueMarginDays" />,
      ///   end her pregnancy childless via <see cref="PregnancyService.TryEndStuckPregnancy" />, the
      ///   reflective remove symmetric to the reflective add that created it. Guarded per-mother so one
      ///   failure never stops the sweep for the rest; never throws out of this method.
      /// </summary>
      private void OnDailyTick()
      {
         if (_crMotherIds.Count == 0) return;

         bool birthsDisabled = CampaignOptions.IsLifeDeathCycleDisabled;
         if (!birthsDisabled) return; // vanilla's own daily check handles normal delivery, nothing to do here.

         foreach (string motherId in _crMotherIds.ToList())
         {
            try
            {
               Hero mother = Hero.FindFirst(h => h.StringId == motherId);
               if (mother == null)
               {
                  _crMotherIds.Remove(motherId);
                  RemoveLoverChildLink(motherId);

                  continue;
               }

               if (!PregnancyService.TryGetDaysOverdue(mother, out int daysOverdue))
               {
                  // No tracked pregnancy left in the engine's own list (already delivered or cleared some
                  // other way), so nothing left for us to track either.
                  _crMotherIds.Remove(motherId);
                  RemoveLoverChildLink(motherId);

                  continue;
               }

               if (!PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled, daysOverdue, OverdueMarginDays)) continue;

               if (!PregnancyService.TryEndStuckPregnancy(mother)) continue;

               _crMotherIds.Remove(motherId);
               RemoveLoverChildLink(motherId); // Inc 3c: a stuck lover-child pregnancy ended childless drops its link too
               ModLog.Info($"[PREGNANCY] ended a stuck pregnancy for {mother.Name}: this campaign's Life/Death " +
                           $"Cycle option is disabled, so vanilla would never have delivered it (due {daysOverdue} " +
                           "day(s) ago). No child is born, this respects the player's own no-birth setting rather " +
                           "than overriding it.");
            }
            catch (Exception ex)
            {
               ModLog.Error($"[PREGNANCY] stuck-pregnancy cleanup failed for hero {motherId}: {ex.GetType().Name}: {ex.Message}");
            }
         }
      }
   }
}
