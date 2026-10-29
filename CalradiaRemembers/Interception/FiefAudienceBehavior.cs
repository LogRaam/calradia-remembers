// Code written by Gabriel Mailhot, 20/08/2026.
// Player wish (Fakade, roadmap): today a lord can only ride out to meet the player on the ROAD
// (InterceptionBehavior, which needs a field party to move), so everyone with none -- ladies, notables, a
// garrisoned lord standing around in a fief -- never gets a turn. On the player ENTERING a settlement, this
// scans everyone standing there with no field party for the SAME "does this NPC have a live reason to speak"
// question InterceptionPolicy already answers (via FiefAudiencePolicy.ChooseFiefValid, which narrows its
// triggers to the ones that make sense off the road), and offers an Accept/Decline inquiry for the single
// strongest one found. Declining is free: no grudge, no relation hit, nothing spent beyond the pacing roll and
// per-NPC cooldown the offer itself already started. On Accept, the reason is stashed through
// InterceptionBehavior.StashFiefAudienceReason (the SAME one-shot ConsumeInterceptionReason slot a road
// pursuit uses) and the chat opens via LlmDialogFlow.OpenChatForInterceptingHero, so the opener survives to
// colour the NPC's first words without a second, diverging opening path.
//
// COORDINATION: CompanionAudienceTopicBehavior also offers an audience on settlement entry. To keep at most
// ONE popup per entry, this behavior does NOT register its own SettlementEntered listener -- instead,
// CompanionAudienceTopicBehavior.OnSettlementEntered calls TryOfferOnSettlementEntry() only when it did not
// itself raise a popup this entry (see that file). This is the "share a single per-entry gate" option: the
// gate is the single event-handler call chain rather than mutable static state.

#region

using CalradiaRemembers.Logic.Interception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Interception
{
   /// <summary>
   ///   On the player's party ENTERING A SETTLEMENT (driven by CompanionAudienceTopicBehavior, see the class
   ///   header), offers an Accept/Decline "may I have a word" audience for the strongest partyless NPC standing
   ///   there with a live, fief-valid reason. Never forced; declining costs nothing. State is a per-NPC cooldown
   ///   only, persisted additively.
   /// </summary>
   public sealed class FiefAudienceBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_FiefAudienceCooldown_v1bytes";
      public static FiefAudienceBehavior Instance;

      private readonly Dictionary<string, int> _lastOfferedDay = new();

      private string _pendingHeroId;
      private InterceptionTrigger _pendingTrigger = InterceptionTrigger.None;
      private string _pendingDetail;

      public FiefAudienceBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      // No listeners of our own: see the class header for why this is driven by
      // CompanionAudienceTopicBehavior.OnSettlementEntered rather than a second SettlementEntered subscription.
      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_lastOfferedDay, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[FIEF_AUDIENCE] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[FIEF_AUDIENCE] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is not {Length: > 0}) return;

            try
            {
               var loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes));
               _lastOfferedDay.Clear();

               if (loaded == null) return;
               foreach (KeyValuePair<string, int> kv in loaded)
                  _lastOfferedDay[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
               _lastOfferedDay.Clear();
               ModLog.Error($"[FIEF_AUDIENCE] load failed, cooldowns dropped for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      /// <summary>
      ///   Self-test seam (CrSelfTest "fief_audience", Severity.Light): runs the SAME candidate scan
      ///   <see cref="TryOfferOnSettlementEntry" /> uses against an EXPLICIT settlement, bypassing the pacing
      ///   roll and the road-pursuit guard (mirrors DebugForceOffer), and stamps the winner's cooldown exactly
      ///   like a real offer would (<see cref="RequestAudience" />) -- so calling it twice in a row exercises
      ///   the per-NPC cooldown for real, without ever raising the live UI inquiry. Returns the winning
      ///   candidate, or null when nothing qualifies.
      /// </summary>
      internal FiefAudienceCandidate DebugScanAndStamp(Settlement settlement)
      {
         if (settlement == null) return null;

         int today = Today;
         FiefAudienceCandidate best = SelectBestCandidate(settlement, today);
         if (best != null) _lastOfferedDay[best.HeroId] = today;

         return best;
      }

      /// <summary>Self-test cleanup seam: undoes the cooldown stamp <see cref="DebugScanAndStamp" /> or a real offer left behind, so a test run never leaves the live campaign's fief-audience cooldown state changed.</summary>
      internal void DebugClearCooldown(string heroId)
      {
         if (!string.IsNullOrEmpty(heroId)) _lastOfferedDay.Remove(heroId);
      }

      /// <summary>
      ///   Runs the fief-audience offer pass. Called by CompanionAudienceTopicBehavior when it did not itself
      ///   raise a popup on this settlement entry (see the class header), and by the console test path.
      ///   Returns true only when an inquiry was actually raised.
      /// </summary>
      public bool TryOfferOnSettlementEntry()
      {
         if (InterceptionPacingRules.IsOff(ModSettings.LordsApproaching)) return false;

         // A road pursuit already owns the one-shot ConsumeInterceptionReason slot; never collide over it.
         if (InterceptionBehavior.Instance?.HasActivePursuer == true) return false;

         Settlement here = Hero.MainHero?.CurrentSettlement;
         if (here == null) return false;

         FiefAudienceCandidate best = SelectBestCandidate(here, Today);
         if (best == null) return false;

         // The SAME "how often does an NPC seek the player out" roll the road uses, applied once per entry
         // instead of once per day -- a settlement entry is this feature's equivalent of the daily tick. A liege
         // calling in his summoned vassal is not left to the roll: the vassal walked into his town.
         if (best.Trigger != InterceptionTrigger.WarDutySummons
             && MBRandom.RandomInt(100) >= InterceptionPacingRules.DailyChancePercent(ModSettings.LordsApproaching)) return false;

         Hero npc = Hero.FindFirst(h => h != null && h.StringId == best.HeroId);
         if (npc == null) return false;

         // The world's initiative (Gabriel, 28/10/2026): asked before the cooldown is stamped; a lord kept waiting asks
         // again on the player's next entry, and his request goes when the player leaves the town ("here:").
         bool myTurn = Initiative.WorldInitiativeBehavior.Instance?.MayKnock($"{Initiative.WorldInitiativeBehavior.HerePrefix}audience:{npc.StringId}",
                          Logic.Initiative.InitiativeRanks.FamilyOf(best.Trigger), Logic.Initiative.InitiativeRanks.RankOf(best.Trigger), npc) ?? true;
         if (!myTurn) return false;

         RequestAudience(npc, best.Trigger, best.Detail);

         return true;
      }

      /// <summary>
      ///   Forces the fief-audience popup for the named partyless NPC (console testing), bypassing the pacing
      ///   roll and the per-NPC cooldown, mirroring InterceptionBehavior.DebugForceIntercept.
      ///   <paramref name="trigger" /> defaults to <see cref="InterceptionTrigger.HeardOfDeed" /> (the original
      ///   cr.fief_audience behaviour). For Grudge/AppreciationOwed, <see cref="InterceptionBehavior.ForcedDetailFor" />
      ///   pulls the NPC's REAL grievance/kindness against the player when one exists.
      /// </summary>
      public string DebugForceOffer(Hero npc, InterceptionTrigger trigger = InterceptionTrigger.HeardOfDeed)
      {
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (npc.PartyBelongedTo != null) return $"{npc.Name} has a field party of their own -- use cr.intercept instead of a fief audience.";
         if (npc.CurrentSettlement == null) return $"{npc.Name} is not standing in any settlement right now.";

         RequestAudience(npc, trigger, InterceptionBehavior.ForcedDetailFor(npc, trigger, Today));

         return $"{npc.Name} asks for a word ({trigger}, forced).";
      }

      #region private

      /// <summary>Test seam (cr.test all): everyone who may ask the player for an audience in this settlement.</summary>
      internal static List<Hero> DebugEligible(Settlement settlement) => EligibleHeroes(settlement).ToList();

      private static IEnumerable<Hero> EligibleHeroes(Settlement settlement)
      {
         Hero summoner = GameState.WarLevyDesk.Summoner();

         return Hero.AllAliveHeroes.Where(h =>
            Mapping.ActionGate.Allows(h, Logic.ActionKind.FiefAudience)
            && FiefAudiencePolicy.MaySeekAudience(new FiefAudienceSeekerFacts {
               StandsInThisSettlement = h.CurrentSettlement == settlement, IsPrisoner = h.IsPrisoner,
               HasFieldParty = h.PartyBelongedTo != null, IsChild = h.IsChild, SummonsThePlayer = h == summoner
            }));
      }

      private FiefAudienceCandidate SelectBestCandidate(Settlement settlement, int today)
      {
         var candidates = new List<FiefAudienceCandidate>();
         Hero summoner = GameState.WarLevyDesk.Summoner();

         foreach (Hero h in EligibleHeroes(settlement))
         {
            int lastOffered = _lastOfferedDay.TryGetValue(h.StringId, out int last) ? last : FiefAudiencePolicy.NeverOffered;

            bool holdsGrudge = InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(h, today, out string grudgeCause);
            bool holdsAppreciation = InterceptionBehavior.HoldsSignificantAppreciation(h, today, out string appreciationCause);
            bool hasNemesisBounty = InterceptionBehavior.HasNemesisBountyToOffer(h, out string nemesisDetail);

            var facts = new InterceptionFacts {
               OnCooldown = FiefAudiencePolicy.IsOnCooldown(lastOffered, today, ModSettings.LordsApproaching),
               AnotherPursuerActive = false, // already refused above (InterceptionBehavior.HasActivePursuer)
               PlayerInOwnTerritory = false, // TerritorialPatrol never applies in a fief (see FiefAudiencePolicy)
               FactionHostileToPlayer = false,
               PlayerNotoriety = 0,
               HeardRecentPlayerDeed = InterceptionBehavior.HeardRecentPlayerDeed(h, today),
               HoldsLiveGrudgeAgainstPlayer = holdsGrudge,
               HoldsSignificantAppreciation = holdsAppreciation,
               HasNemesisBountyToOffer = hasNemesisBounty,
               HoldsWarDutyReckoning = h == summoner,
               // An aim another mod registered for him (CrAims), open to audiences (Gabriel, 28/10/2026).
               HoldsAimToRaise = Aims.AimsBehavior.Instance?.AimToRaise(h, Logic.Aims.AimChannels.Audience) != null
            };

            InterceptionTrigger trigger = FiefAudiencePolicy.ChooseFiefValid(facts);
            if (trigger == InterceptionTrigger.None) continue;

            string detail = trigger switch {
               InterceptionTrigger.Grudge => grudgeCause,
               InterceptionTrigger.AppreciationOwed => appreciationCause,
               InterceptionTrigger.NemesisBounty => nemesisDetail,
               // HeardOfDeed is the commonest trigger by far and was the one with no detail at all, so the
               // invitation it raised could only say "someone wants a word". The matter is right there.
               InterceptionTrigger.HeardOfDeed => InterceptionBehavior.RecentMatterTopic(h, today),
               // Named in the invitation only; the opener holds to the footing and the terms section to the amounts.
               InterceptionTrigger.WarDutySummons => "the war you have sat out",
               // The want is his to put; the invitation names only that it is his own matter.
               InterceptionTrigger.StandingAim => "a matter of their own",
               _ => null
            };

            candidates.Add(new FiefAudienceCandidate(h.StringId, trigger, detail));
         }

         return FiefAudiencePolicy.SelectCandidate(candidates);
      }

      private void RequestAudience(Hero npc, InterceptionTrigger trigger, string detail = null)
      {
         // The cooldown starts the moment the popup is raised, whether or not the player answers it -- the same
         // "never silently, but never spammed either" rule CompanionAudienceTopicBehavior's own global cooldown
         // follows, so declining costs nothing BEYOND what the offer itself already started.
         _lastOfferedDay[npc.StringId] = Today;
         if (trigger == InterceptionTrigger.HeardOfDeed) InterceptionBehavior.NoteGossipOffered(npc, Today);
         _pendingHeroId = npc.StringId;
         _pendingTrigger = trigger;
         _pendingDetail = detail;

         var title = new TextObject($"{npc.Name} asks for a word");

         // NAME THE SUBJECT (fkasad, 18/09/2026). Declining blind nine times out of ten is rational, and it
         // costs the player the tenth one too. The topic has always been in hand at this point; it was simply
         // never shown. Trimmed, because a summary can run long and an inquiry box cannot.
         string about = AudienceTopicLine.For(detail);
         var body = new TextObject(
            $"{npc.Name}, standing here, catches your eye and seems to want a private word with you.{about}");

         // A liege does not catch his vassal's eye: he has him called before him (the war duty, inc 6).
         if (trigger == InterceptionTrigger.WarDutySummons)
         {
            title = new TextObject($"{npc.Name} summons you");
            body = new TextObject($"{npc.Name}, your liege, is here and has you called before them over the war you have sat out.");
         }

         // WHO IS ASKING (Lordfadooboo, 13/10/2026): said in the invitation, with a "Who is this?" button to their page.
         AudienceInquiry.Show(npc, title.ToString(), body.ToString(), "Speak with them", "Not now",
            GrantAudience,
            () => ModLog.Info($"[FIEF_AUDIENCE] {npc.Name}'s word was declined, no penalty, free refusal."));
      }

      private void GrantAudience()
      {
         string heroId = _pendingHeroId;
         InterceptionTrigger trigger = _pendingTrigger;
         string detail = _pendingDetail;
         _pendingHeroId = null;
         _pendingTrigger = InterceptionTrigger.None;
         _pendingDetail = null;

         Hero npc = heroId != null ? Hero.FindFirst(h => h != null && h.StringId == heroId) : null;
         if (npc == null) return;

         // Defer the stash + chat open to the next main-thread frame. Opening the focus-grabbing, time-pausing
         // chat synchronously from inside this inquiry consequence, while the settlement menu is the active state
         // being restored under the dismissing inquiry, left the inquiry undismissed and the engine re-presented
         // it every frame (the "million popups" cascade). One frame later the inquiry is gone and the menu has
         // settled, so the chat opens cleanly. The stash rides with the open so the one-shot reason it feeds is
         // set right before it is consumed. See NpcMemoryServiceSubmodule.OnApplicationTick.
         MainThreadDispatcher.Enqueue(() =>
         {
            InterceptionBehavior.Instance?.StashFiefAudienceReason(npc, trigger, detail);
            if (trigger == InterceptionTrigger.StandingAim) Aims.AimsBehavior.Instance?.ExpectAudience(npc);

            // Stage a conversation scene with the NPC (the same VRAM-safe path the council and companion audience
            // use) rather than a bare overlay over the suspended settlement menu, where a hidden sub-panel
            // (Memories/Compose) left ghost pixels. The stashed reason above is consumed when the chat opens over
            // the loaded scene (StagePlainConversation -> OpenChatForInterceptingHero), a few frames later. Falls
            // back to the direct overlay open if no scene can load for this partyless notable.
            ChatSceneStager.StagePlainConversation(npc);
            ModLog.Info($"[FIEF_AUDIENCE] {npc.Name}'s audience staged ({trigger}).");
         });
      }

      #endregion
   }
}
