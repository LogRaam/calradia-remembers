// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Interception
{
   /// <summary>
   ///   Once a day, at most one nearby lord may be sent to intercept the player when
   ///   <see cref="InterceptionPolicy" /> says a trigger fires (a hostile territorial challenge, or a
   ///   reaction to fresh news of the player). The lord's party engages the player's; the meeting
   ///   itself opens the ordinary "We need to talk" CR conversation. Guards (one pursuer at a time,
   ///   a per-lord cooldown, a pursuit timeout) keep it from becoming harassment. State is transient.
   /// </summary>
   public sealed class InterceptionBehavior : CampaignBehaviorBase
   {
      private const string CooldownSaveKey = "CalradiaRemembers_Intercept_v1bytes";

      // How often lords ride out is the PLAYER's taste, not ours to fix (Fakade asked for the dial, and named
      // both reasons in one breath: he likes being approached, and he does not want to be forced into a string of
      // conversations). Read live from the MCM menu, so a change applies from the next day with no reload. The
      // default resolves to exactly the shipped numbers, so nothing moves for a player who never touches it.
      private static int CooldownDays => InterceptionPacingRules.CooldownDays(ModSettings.LordsApproaching);

      private static int DailyInterceptChancePercent => InterceptionPacingRules.DailyChancePercent(ModSettings.LordsApproaching);

      private const float InterceptRange = 40f; // a lord this close can plausibly ride out

      // TUNE: how close the pursuer must come before the meeting is forced open. Matches ArrivalDistance
      // (2.0f) in CourierService/CompanionMissionBehavior, the codebase's own established threshold for
      // "one party has reached another" (this used to sit tighter, at 1.5f, which is one plausible reason
      // a player who rode right up to a lord, "I'll ride up to them and everything", never triggered the
      // meeting). Tune further in-game if a lord still opens the chat while visibly distant, or still
      // doesn't at point-blank range.
      private const float ArrivalRange = 2.0f;
      private const int MaxCandidates = 12; // bound the per-day work
      private const int RecentDeedWindowDays = 6;
      public static InterceptionBehavior Instance;
      private readonly Dictionary<string, int> _cooldown = new();

      private string _activePursuerId;
      private int _activePursuerSince = -1;
      private InterceptionTrigger _activePursuerTrigger = InterceptionTrigger.None;

      // The pursuer's distance to the player as of the LAST daily check, or -1f while no baseline exists yet
      // (the day the pursuit began). Lets ReleaseStalePursuit tell a player actively closing the gap (riding
      // toward the meeting, as the bug report described) from one who is genuinely being outrun or stalled.
      private float _lastPursuitDistance = -1f;

      // Once the meeting has been forced open for the active pursuer, this guards against re-opening it every
      // frame. Reset when a pursuit begins, is consumed by the chat, or is released.
      private bool _meetingOpened;

      public InterceptionBehavior()
      {
         Instance = this;
      }

      /// <summary>
      ///   If <paramref name="npc" /> is the lord currently intercepting the player, returns a
      ///   one-shot opening directive for the conversation (consuming it so it only colours the
      ///   first turn) and ends the pursuit. Null otherwise. Called as the CR chat context is built.
      /// </summary>
      public string ConsumeInterceptionReason(Hero npc)
      {
         if (npc == null || _activePursuerId == null || npc.StringId != _activePursuerId) return null;

         InterceptionTrigger trigger = _activePursuerTrigger;
         _activePursuerId = null;
         _activePursuerSince = -1;
         _activePursuerTrigger = InterceptionTrigger.None;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;

         return trigger == InterceptionTrigger.TerritorialPatrol
            ? "It is YOU who sought this meeting: you rode out to challenge the player for crossing your realm's " +
              "lands. Open on that footing — demand their business here, wary and territorial — rather than waiting on them."
            : "It is YOU who sought this meeting: word of the player's recent deeds reached you and you rode out to " +
              "see them while it was fresh. Open by raising what you heard — be it admiration, alarm, or a reckoning.";
      }

      /// <summary>Forces the named lord to ride out and intercept the player (console testing).</summary>
      public string DebugForceIntercept(Hero lord)
      {
         if (lord?.PartyBelongedTo == null || MobileParty.MainParty == null) return $"{lord?.Name?.ToString() ?? "That hero"} has no party on the map to ride out.";
         BeginPursuit(lord, InterceptionTrigger.HeardOfDeed, (int) CampaignTime.Now.ToDays);

         return $"{lord.Name} now rides to intercept you.";
      }

      /// <summary>
      ///   Production entry point for the stance-consequence weekly tick ("stance with teeth" increment 2):
      ///   a lord whose earned posture warrants the Intercept verdict actually rides out, through the same
      ///   <see cref="BeginPursuit" /> path as <see cref="DebugForceIntercept" />. Unlike that debug seam,
      ///   this honours the lord's own per-lord cooldown and the "one pursuer at a time" rule, so it never
      ///   spams a pursuit; it may legitimately do nothing. Returns true only when a pursuit actually began.
      /// </summary>
      public bool TriggerStanceIntercept(Hero lord)
      {
         if (lord?.PartyBelongedTo == null || MobileParty.MainParty == null) return false;
         if (_activePursuerId != null) return false; // one interception at a time, same as the organic daily roll

         var today = (int) CampaignTime.Now.ToDays;
         if (_cooldown.TryGetValue(lord.StringId, out int last) && today - last < CooldownDays) return false;

         BeginPursuit(lord, InterceptionTrigger.HeardOfDeed, today);

         return _activePursuerId == lord.StringId;
      }

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
         CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
      }

      /// <summary>
      ///   Hands a party back to its OWN AI, which is the whole point and the thing we used to get wrong.
      ///   <para>
      ///     <see cref="MobileParty.SetMoveModeHold" /> does not mean "resume your life", it means "stand here".
      ///     It leaves the party on <c>AiBehavior.Hold</c> with a behaviour it considers perfectly valid, so its
      ///     AI never reconsiders and it simply stays put. A player watched an intercepting clan party stand on
      ///     one spot until its food ran out and it starved (Fakade, 2026-07-13). Dropping our order is only half
      ///     the job: the party must also be TOLD to think again, which is what RethinkAtNextHourlyTick does.
      ///   </para>
      /// </summary>
      internal static void ReleaseToVanillaAi(MobileParty party)
      {
         if (party == null) return;

         try
         {
            party.SetMoveModeHold();                    // drop OUR engage order, so it stops bearing on the player
            party.Ai.SetDoNotMakeNewDecisions(false);   // nothing of ours is holding its decisions shut
            party.Ai.RethinkAtNextHourlyTick = true;    // and MAKE it decide afresh within the hour
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] could not release {party.Name} to its own AI: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Tells every lord party idling on <c>Hold</c> out in the open field to think again, and returns how many
      ///   were nudged. This is what rescues a campaign already broken by the old bug: without it, a party stranded
      ///   by an earlier version stays stranded for good, however well the cause is fixed. Safe by construction, a
      ///   party that genuinely means to hold will simply choose to hold again on its next think.
      /// </summary>
      internal static int RescueHeldParties()
      {
         MobileParty main = MobileParty.MainParty;
         var freed = 0;

         foreach (MobileParty p in MobileParty.All)
         {
            if (p == null || p == main) continue;
            if (p.LeaderHero == null || p.CurrentSettlement != null || p.Army != null) continue;
            if (p.DefaultBehavior != AiBehavior.Hold || p.Ai.RethinkAtNextHourlyTick) continue;

            p.Ai.RethinkAtNextHourlyTick = true;
            freed++;
         }

         return freed;
      }

      /// <summary>
      ///   Self-heal on every load, and it now heals something real. Two passes:
      ///   <list type="number">
      ///     <item>
      ///       RESCUE parties frozen by the old bug. Any lord party standing on <c>Hold</c> out on the open map
      ///       is told to think again. This is safe by construction: a party that genuinely wants to hold will
      ///       simply choose to hold once more. Without it, every party already stranded by an earlier version
      ///       stays stranded for the rest of that campaign, however well we fix the cause.
      ///     </item>
      ///     <item>
      ///       The original guard: if the player is a SUBORDINATE inside an army (the state the game cannot
      ///       resolve an encounter in, and crashes on), stand down anything still bearing on their party.
      ///     </item>
      ///   </list>
      /// </summary>
      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         try
         {
            MobileParty main = MobileParty.MainParty;

            if (main == null) return;

            // Pass 1: rescue anyone left standing on Hold in the open field by the old bug.
            int freed = RescueHeldParties();

            // Pass 2: the army-crash guard, now standing them down for real rather than freezing them.
            var stood = 0;

            if (PlayerIsSubordinateInArmy())
               foreach (MobileParty p in MobileParty.All)
               {
                  if (p == null || p == main || p.TargetParty != main) continue;

                  ReleaseToVanillaAi(p);
                  stood++;
               }

            if (freed > 0)
               ModLog.Info($"[INTERCEPT] on load, told {freed} party(ies) idling on Hold to think again (rescues anyone stranded by the old stand-down bug).");

            if (stood > 0)
               ModLog.Info($"[INTERCEPT] on load, stood down {stood} party(ies) bearing on the player: the player is inside an army and cannot be safely encountered.");
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] on-load heal failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Persists only the per-lord cooldowns (as a UTF-8 byte[], like the other CR stores), so a
      ///   lord cannot intercept the player again right after a reload. The active pursuit itself is
      ///   transient by design — a mid-pursuit reload simply lets the lord's AI return to its plans.
      /// </summary>
      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_cooldown, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[INTERCEPT] serialize failed — persisting no cooldowns this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[INTERCEPT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
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
            try
            {
               var loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes));
               _cooldown.Clear();

               if (loaded == null) return;
               foreach (KeyValuePair<string, int> kv in loaded)
                  _cooldown[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
               _cooldown.Clear();
               ModLog.Error($"[INTERCEPT] load failed — cooldowns dropped for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      #region private

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      private static bool HeardRecentPlayerDeed(Hero lord, int today)
      {
         try
         {
            var store = WorldEventStore.Instance;

            if (store == null) return false;

            foreach (WorldEvent e in store.GetKnownEvents(lord, today, 4))
               if (e.InvolvesPlayer && today - e.Day <= RecentDeedWindowDays)
                  return true;

            return false;
         }
         catch { return false; }
      }

      private static bool IsHostileToPlayer(Hero lord)
      {
         try
         {
            IFaction lf = lord.MapFaction,
                     pf = Hero.MainHero?.MapFaction;

            if (lf != null && pf != null && lf != pf && FactionManager.IsAtWarAgainstFaction(lf, pf)) return true;

            return lord.GetRelation(Hero.MainHero) <= -20;
         }
         catch { return false; }
      }

      private static IFaction NearestSettlementFaction(Vec2 pos)
      {
         try
         {
            Settlement best = null;
            var bestDist = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
               if (s == null) continue;
               float d = s.GetPosition2D.Distance(pos);

               if (!(d < bestDist)) continue;
               bestDist = d;
               best = s;
            }

            return best?.MapFaction;
         }
         catch { return null; }
      }

      private void BeginPursuit(Hero lord, InterceptionTrigger trigger, int today)
      {
         // Last line of defense against the army-encounter crash: never engage a party the player is already
         // with (a subordinate army member, a shared army, or an attached party).
         if (PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(lord.PartyBelongedTo)) return;

         lord.PartyBelongedTo.SetMoveEngageParty(MobileParty.MainParty, MobileParty.NavigationType.Default);
         _activePursuerId = lord.StringId;
         _activePursuerTrigger = trigger;
         _activePursuerSince = today;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;
         _cooldown[lord.StringId] = today;

         string line = trigger == InterceptionTrigger.TerritorialPatrol
            ? $"{lord.Name} rides out to challenge you for crossing their lands."
            : $"{lord.Name} rides toward you, having heard of your recent deeds.";
         try { MBInformationManager.AddQuickInformation(new TextObject(line)); }
         catch
         {
            /* notification is best-effort */
         }

         ModLog.Info($"[INTERCEPT] {lord.Name} pursues the player ({trigger}).");
      }

      private void OnDailyTick()
      {
         try
         {
            var today = (int) CampaignTime.Now.ToDays;

            // A chase already under way is still released even when the player has since switched the feature
            // off, so nobody is left trailing them (or, worse, standing in a field) by a change of mind.
            ReleaseStalePursuit(today);

            if (InterceptionPacingRules.IsOff(ModSettings.LordsApproaching)) return; // the road is theirs alone
            if (_activePursuerId != null) return; // one interception at a time
            if (MBRandom.RandomInt(100) >= DailyInterceptChancePercent) return;
            TryStartInterception(today);
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Per-frame arrival check. The lord riding out (<see cref="BeginPursuit" />) only moves them toward the
      ///   player; an AI engage does NOT reliably open a conversation (a friendly party just stacks, and a moving
      ///   player is never physically caught), which is why players saw "a lord rides toward me and then nothing
      ///   comes of it". So the moment the pursuer is on the player and the player is free on the open map, we
      ///   FORCE the meeting open ourselves, via the same VRAM-safe staged conversation the captive scenes use.
      ///   It opens the CR chat AUTOMATICALLY (no vanilla menu the player could simply back out of to dodge it).
      /// </summary>
      private void OnTick(float dt)
      {
         if (_activePursuerId == null || _meetingOpened) return;

         try { TryForceMeeting(); }
         catch (Exception ex)
         {
            _meetingOpened = true; // never re-attempt a throwing open every frame; the daily release clears it
            ModLog.Error($"[INTERCEPT] forcing the meeting failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void TryForceMeeting()
      {
         Hero pursuer = FindHero(_activePursuerId);
         MobileParty pursuerParty = pursuer?.PartyBelongedTo;
         MobileParty main = MobileParty.MainParty;

         if (pursuer is not {IsAlive: true} || pursuerParty == null || main == null) return;
         if (pursuerParty.GetPosition2D.Distance(main.GetPosition2D) > ArrivalRange) return;
         if (!PlayerIsFreeOnMap()) return;

         // The sea guard, and the one that matters most: staging the meeting asks the engine for a
         // conversation scene on the terrain under the party, and there is none for open water, so the
         // load never completes and the game sits on an endless loading screen (two player reports on
         // v1.30.16, both while sailing). Nothing is lost by waiting: the meeting opens on the first
         // frame both are ashore, and the daily release stands the chase down if the player keeps sailing.
         if (!InterceptionPolicy.CanMeetOnLand(main.IsCurrentlyAtSea, pursuerParty.IsCurrentlyAtSea)) return;

         // Same army guard as BeginPursuit: forcing an encounter/conversation while the player is a subordinate
         // inside an army crashes deep in the engine.
         if (PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(pursuerParty)) return;

         // Stage the meeting as a conversation scene (3D, VRAM-safe) and let it open the CR chat on its own. The
         // interception opener is consumed as the chat's context is built (EncounterContextBuilder ->
         // ConsumeInterceptionReason), colouring the lord's first words.
         _meetingOpened = true;

         // The lord has arrived and the meeting is opening: END the engage now and hand him back to his own AI.
         // Clearing only our tracking left the vanilla SetMoveEngageParty in place and he kept shadowing the
         // player; but merely holding him was worse, because Hold is not a release, it is a command to stand
         // still, and he obeyed it until he starved (Fakade, 2026-07-13). He must be told to think again.
         ReleaseToVanillaAi(pursuerParty);

         CaptiveSceneStager.StagePlainConversation(pursuer);
         ModLog.Info($"[INTERCEPT] {pursuer.Name} reached the player, forcing the meeting open.");
      }

      /// <summary>
      ///   True only when the player is standing free on the open campaign map: not in a scene/battle, an existing
      ///   conversation, a settlement, an encounter, or a game menu, and with no CR chat already open or staging.
      ///   Forcing a conversation in any of those states is unsafe or nonsensical.
      /// </summary>
      private static bool PlayerIsFreeOnMap()
      {
         if (Mission.Current != null) return false;
         if (Hero.OneToOneConversationHero != null) return false;
         if (Hero.MainHero?.CurrentSettlement != null) return false;
         if (PlayerEncounter.Current != null) return false;
         if (Campaign.Current?.CurrentMenuContext != null) return false;
         if (ChatManager.IsOpen) return false;
         if (CaptiveSceneStager.IsStaging) return false;
         // A council staging its own conversation scene is the same kind of "about to hand focus to a
         // conversation" moment as a captive scene staging one; a lord riding out mid-stage would collide.
         if (CouncilConversationStager.IsStaging) return false;

         return true;
      }

      private void ReleaseStalePursuit(int today)
      {
         if (_activePursuerId == null) return;

         Hero pursuer = FindHero(_activePursuerId);
         MobileParty pursuerParty = pursuer?.PartyBelongedTo;
         MobileParty main = MobileParty.MainParty;

         // NB: do NOT clear on arrival — the pursuit ends when the player actually talks to them
         // (ConsumeInterceptionReason) so the opener survives the meeting. Otherwise the pure rule
         // gives up a stale chase (timeout) or a hopeless one (the player is well clear and faster).
         // Also release the moment the player is no longer freely interceptable: if they joined the pursuer's
         // army (or any army as a subordinate) mid-chase, the pending engagement would crash on contact.
         // A chase across water is not merely slow, it is impossible: a land party cannot come alongside a
         // ship, and the meeting could never be staged there anyway. Stand it down rather than leave the
         // lord trailing a sail he can never catch until the timeout notices.
         bool atSea = main != null && pursuerParty != null
                      && !InterceptionPolicy.CanMeetOnLand(main.IsCurrentlyAtSea, pursuerParty.IsCurrentlyAtSea);

         bool invalid = pursuer is not {IsAlive: true} || pursuerParty == null || main == null
                        || PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(pursuerParty) || atSea;

         float distance = pursuerParty != null && main != null
            ? pursuerParty.GetPosition2D.Distance(main.GetPosition2D)
            : float.MaxValue;

         // The player closing the gap ON PURPOSE ("I'll ride up to them and everything") must not read as
         // outrun or timed out from a single day's snapshot: compare against yesterday's reading, not the
         // raw speed ratio alone, and defer the give-up while the gap keeps shrinking.
         bool isClosing = !invalid && _lastPursuitDistance >= 0f && distance < _lastPursuitDistance;

         bool giveUp = !invalid &&
                       InterceptionPolicy.ShouldAbandonChase(
                          pursuerParty.Speed, main.Speed, distance,
                          today - _activePursuerSince, isClosing);

         if (!invalid && !giveUp)
         {
            _lastPursuitDistance = distance;

            return;
         }

         InterceptionTrigger trigger = _activePursuerTrigger; // read before the fields below are cleared
         AbandonReason reason = DetermineAbandonReason(atSea, invalid, today - _activePursuerSince);

         ModLog.Info($"[INTERCEPT] {pursuer?.Name} stands down ({reason}, distance {distance:0.0}, " +
                     $"{today - _activePursuerSince} day(s) pursued, closing={isClosing}).");

         NotifyPursuitAbandoned(pursuer, reason);

         // Only an ORDINARY give-up (outrun or timeout) gets the letter, never a genuinely impossible
         // situation (sea, army, death): mirrors the assembly's ambassador fallback (narration covers the
         // gap instead of a broken feature) rather than papering over a pursuit that could never have
         // succeeded regardless.
         if (reason is AbandonReason.GaveUp or AbandonReason.LostTrail) TryWriteMissedMeetingLetter(pursuer, trigger, today);

         // End the engage on any abandoned chase too, so a lord who timed out or was outrun stops bearing on the
         // player instead of trailing them indefinitely, and RESUMES his own plans rather than standing in the
         // field. Null when the pursuer died (nothing to release).
         ReleaseToVanillaAi(pursuerParty);

         _activePursuerId = null;
         _activePursuerSince = -1;
         _activePursuerTrigger = InterceptionTrigger.None;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;
      }

      /// <summary>Why an abandoned pursuit ended, purely to pick the player-facing wording and the letter gate.</summary>
      private enum AbandonReason
      {
         /// <summary>The chase went to sea, where no meeting can physically be staged.</summary>
         WentToSea,

         /// <summary>The pursuer vanished from the chase (died, lost their party) or the player is now inside an army.</summary>
         OtherBusiness,

         /// <summary>The pursuit ran past <see cref="InterceptionPolicy.MaxPursuitDays" /> without connecting.</summary>
         GaveUp,

         /// <summary>The player was clearly faster and pulled well clear (the outrun branch of ShouldAbandonChase).</summary>
         LostTrail
      }

      private static AbandonReason DetermineAbandonReason(bool atSea, bool invalid, int daysPursuing)
      {
         if (atSea) return AbandonReason.WentToSea;
         if (invalid) return AbandonReason.OtherBusiness;

         return daysPursuing >= InterceptionPolicy.MaxPursuitDays ? AbandonReason.GaveUp : AbandonReason.LostTrail;
      }

      /// <summary>
      ///   Tells the player, once, that a called meeting is off: a promise silently withdrawn ("a lord rides
      ///   toward you") is worse than no promise, and this is the honest minimum (player report: "I'll ride up
      ///   to them and everything but they won't talk to me"). Uses the same best-effort notification
      ///   mechanism as BeginPursuit's own announcement, so the two read as a matched pair.
      /// </summary>
      private static void NotifyPursuitAbandoned(Hero pursuer, AbandonReason reason)
      {
         if (pursuer?.Name == null) return; // nothing to honestly name, nothing to say

         string line = reason switch
         {
            AbandonReason.WentToSea     => $"{pursuer.Name} could not follow you out to sea, and turns back.",
            AbandonReason.OtherBusiness => $"{pursuer.Name} was drawn into other business, and the meeting will not happen after all.",
            AbandonReason.GaveUp        => $"{pursuer.Name} gives up trying to reach you, and turns back to their own affairs.",
            _                           => $"{pursuer.Name} cannot keep pace with you and loses your trail."
         };

         try { MBInformationManager.AddQuickInformation(new TextObject(line)); }
         catch
         {
            /* notification is best-effort, same as BeginPursuit's own announcement */
         }
      }

      /// <summary>
      ///   When a chase is abandoned for an ORDINARY reason (never an impossible one), the lord who wanted a
      ///   word writes instead, through the existing letter pipeline, rather than the whole thing simply
      ///   evaporating. Reuses <see cref="InterceptionTrigger" /> for WHY he set out, exactly as the in-person
      ///   opener does. A lord the player has never actually met has no profile to write from and is silently
      ///   skipped; the LLM pipeline itself (checked inside <see cref="LetterGenerationService.QueueInitialLetter" />)
      ///   is the rest of "can actually deliver". Called at most once per abandoned chase.
      /// </summary>
      private static void TryWriteMissedMeetingLetter(Hero pursuer, InterceptionTrigger trigger, int today)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            BannerlordNpcMemoryStore store = behavior?.Store;
            NpcProfile profile = pursuer != null ? store?.Get(pursuer.StringId) : null;

            if (profile == null || behavior.LlmServices?.ChatService == null) return;

            string context = trigger == InterceptionTrigger.TerritorialPatrol
               ? "You rode out to challenge the player for crossing your realm's lands, wary and territorial, but the chase came to nothing before you could face them."
               : "You rode out because word of the player's recent deeds reached you and you meant to speak with them about it, but the chase came to nothing before you could catch them.";

            LetterGenerationService.QueueInitialLetter(
               profile, pursuer, LetterReason.InterceptionMissed, context, behavior.LlmServices, store, today);

            ModLog.Info($"[INTERCEPT] {pursuer.Name} could not meet the player in person, writes instead ({trigger}).");
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] missed-meeting letter failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void TryStartInterception(int today)
      {
         MobileParty main = MobileParty.MainParty;

         if (main == null || Hero.MainHero == null) return;
         if (PlayerIsSubordinateInArmy()) return; // never intercept a player attached inside another lord's army
         if (main.IsCurrentlyAtSea) return;       // no one rides out to a ship (see InterceptionPolicy.CanMeetOnLand)

         Vec2 playerPos = main.GetPosition2D;
         IFaction territory = NearestSettlementFaction(playerPos);
         var notoriety = (int) (Clan.PlayerClan?.Renown ?? 0f);

         var scanned = 0;
         foreach (MobileParty p in MobileParty.All)
         {
            if (scanned >= MaxCandidates) break;

            Hero lord = p?.LeaderHero;

            if (lord is not {IsLord: true} || lord == Hero.MainHero) continue;
            if (p == main || p.IsBandit || p.MapFaction == null) continue;
            if (PlayerSharesArmyOrAttached(p)) continue; // player leads an army holding this party, or is attached to it
            if (p.GetPosition2D.Distance(playerPos) > InterceptRange) continue;

            scanned++;

            var facts = new InterceptionFacts {
               OnCooldown = _cooldown.TryGetValue(lord.StringId, out int last) && today - last < CooldownDays,
               AnotherPursuerActive = _activePursuerId != null,
               PlayerInOwnTerritory = territory != null && territory == lord.MapFaction,
               FactionHostileToPlayer = IsHostileToPlayer(lord),
               PlayerNotoriety = notoriety,
               HeardRecentPlayerDeed = HeardRecentPlayerDeed(lord, today)
            };

            InterceptionTrigger trigger = InterceptionPolicy.Choose(facts);

            if (trigger == InterceptionTrigger.None) continue;

            BeginPursuit(lord, trigger, today);

            return; // one interception started per day
         }
      }

      /// <summary>
      ///   True when the player is a SUBORDINATE member of some army (their party is attached to an army they
      ///   do not lead). Forcing a PlayerEncounter in that state throws deep in PlayerEncounter.SetupFields
      ///   (a player-reported save-corrupting crash: an army leader "rides toward" the player who is already
      ///   in their army). No interception may start or continue while this holds.
      /// </summary>
      private static bool PlayerIsSubordinateInArmy()
      {
         MobileParty main = MobileParty.MainParty;

         return main?.Army != null && main.Army.LeaderParty != main;
      }

      /// <summary>
      ///   True when <paramref name="party" /> is already TOGETHER with the player and so cannot validly be
      ///   an interception target: it shares the player's army (e.g. the player leads an army this party is
      ///   in), or one is physically attached to the other.
      /// </summary>
      private static bool PlayerSharesArmyOrAttached(MobileParty party)
      {
         MobileParty main = MobileParty.MainParty;

         if (main == null || party == null) return false;
         if (main.Army != null && party.Army == main.Army) return true;

         return main.AttachedTo == party || party.AttachedTo == main;
      }

      #endregion
   }
}