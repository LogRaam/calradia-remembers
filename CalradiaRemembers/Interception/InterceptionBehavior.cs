// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Interception;
using CalradiaRemembers.Logic.Nemesis;
using CalradiaRemembers.Nemesis;
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
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Interception
{
   /// <summary>
   ///   Once a day, at most one nearby lord may be sent to intercept the player when
   ///   <see cref="InterceptionPolicy" /> says a trigger fires (a hostile territorial challenge, or a
   ///   reaction to fresh news of the player). The lord's party engages the player's; the meeting
   ///   itself opens the ordinary "We need to talk" CR conversation. Guards (one pursuer at a time,
   ///   a per-lord cooldown, a pursuit timeout) keep it from becoming harassment. The pursuit and the
   ///   remembered intent persist across saves (2026-09-03), so a mid-pursuit reload no longer wipes
   ///   the lord's errand.
   /// </summary>
   public sealed class InterceptionBehavior : CampaignBehaviorBase
   {
      private const string CooldownSaveKey = "CalradiaRemembers_Intercept_v1bytes";

      // The live pursuit + remembered intent, persisted under their OWN key (added 2026-09-03) so a save written
      // before this key existed still loads: the old key alone comes back, and no pursuit is restored — exactly
      // the old transient behaviour.
      private const string PursuitSaveKey = "CalradiaRemembers_InterceptPursuit_v1bytes";

      // The once-per-NPC "recent matter" ledger (2026-09-05, [[project_npc_recent_matter]] phase 1): a set of
      // "lordId|matterKey" a lord has already raised, so he never brings the same event up twice. Its OWN additive
      // key so an older save (which lacks it) loads clean with an empty ledger.
      private const string MatterSaveKey = "CalradiaRemembers_InterceptMatters_v1bytes";

      // How many days back a lord may reach for a matter to raise (a fresh thing, not ancient history); matches the
      // rumor window so a lived memory and a rumor are weighed over the same recent horizon.
      private const int RecentMatterWindowDays = RecentDeedWindowDays;

      // How noteworthy a NON-player world event must be before a lord bothers to probe the player about it (phase 2
      // gossip). On the world-event magnitude scale (~30 a town's hardship, 60 war looming, 75 war declared, 65-85
      // captures/executions), this keeps gossip to realm-shaking news, never idle village chatter.
      private const int GossipMagnitudeThreshold = 55;

      // Which profile memories are worth a lord riding out to speak of: shared/dramatic experiences, not a bare
      // first meeting, a farewell, or a private romantic/captive beat that a roadside challenge should never raise.
      private static readonly HashSet<NotableEventType> RaisableMemoryTypes = new()
      {
         NotableEventType.Other, NotableEventType.Collaboration, NotableEventType.Confrontation,
         NotableEventType.Agreement, NotableEventType.Betrayal, NotableEventType.Conflict
      };

      // "lordId|matterKey" of every recent matter already raised, so it is never raised again by that lord.
      private HashSet<string> _raisedMatterKeys = new();

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

      // The specific grievance/kindness a Grudge/AppreciationOwed pursuit is ABOUT (the Grudge/Appreciation's
      // own CauseSummary), so ConsumeInterceptionReason and ChatViewModel.ActivateInterceptionOpener can name
      // the actual matter rather than a generic "a grudge" / "a kindness". Null for every other trigger, and
      // for a Grudge/AppreciationOwed candidate whose underlying ledger entry carried no cause text.
      private string _activePursuerDetail;

      // True when the active slot above was stashed by FiefAudienceBehavior (StashFiefAudienceReason) rather
      // than a real road pursuit (BeginPursuit): ConsumeInterceptionReason reads this to pick "you rode out"
      // wording versus "you simply asked for a word where you already stood" -- same one-shot slot, different
      // framing for the two ways an NPC can come to want a word with the player.
      private bool _activePursuerIsFiefAudience;

      // The pursuer's distance to the player as of the LAST daily check, or -1f while no baseline exists yet
      // (the day the pursuit began). Lets ReleaseStalePursuit tell a player actively closing the gap (riding
      // toward the meeting, as the bug report described) from one who is genuinely being outrun or stalled.
      private float _lastPursuitDistance = -1f;

      // The party SteerTowardPlayer holds on the player, kept apart from the pursuer's id so a lord who dies mid-chase
      // (his party passing to someone else, or to no one) is still released. Not persisted: the restore re-steers.
      private MobileParty _pursuitParty;

      // Once the meeting has been forced open for the active pursuer, this guards against re-opening it every
      // frame. Reset when a pursuit begins, is consumed by the chat, or is released.
      private bool _meetingOpened;

      // The REMEMBERED INTENT (player report, 2026-09-03: "if I go and meet them they don't remember they
      // wanted to tell me something"): when a road pursuit is abandoned, a living lord keeps his errand in
      // mind for InterceptionPolicy.RememberedIntentWindowDays, so a player who seeks HIM out still gets the
      // The NPC who sought the player out in the conversation now open, or null. See SoughtThePlayerThisConversation.
      private string _conversationSeekerId;

      // matter raised in person (ConsumeInterceptionReason checks this slot when no pursuit is active).
      private string _rememberedIntentLordId;
      private InterceptionTrigger _rememberedIntentTrigger = InterceptionTrigger.None;
      private string _rememberedIntentDetail;
      private int _rememberedIntentAbandonedDay = -1;

      public InterceptionBehavior()
      {
         Instance = this;
      }

      /// <summary>
      ///   If <paramref name="npc" /> is the lord currently intercepting the player, returns a
      ///   one-shot opening directive for the conversation (consuming it so it only colours the
      ///   first turn) and ends the pursuit. Null otherwise. Called as the CR chat context is built.
      /// </summary>
      /// <summary>
      ///   If <paramref name="npc" /> is the lord currently intercepting the player, returns a
      ///   one-shot opening directive for the conversation (consuming it so it only colours the
      ///   first turn) and ends the pursuit. Failing that, if <paramref name="npc" /> is a lord whose
      ///   abandoned chase is still inside the remembered-intent window, returns that directive instead
      ///   (same one-shot consumption). Null otherwise. Called as the CR chat context is built.
      /// </summary>
      public string ConsumeInterceptionReason(Hero npc)
      {
         string reason = ConsumeInterceptionReasonCore(npc);

         // The DIRECTIVE is a one-shot (it tells the character how to OPEN), but the fact that they are the one
         // who came is true for the whole conversation. Fakade (19/09/2026) watched a lord who had ridden out to
         // him start speaking, one turn later, as though the player had come to him: from turn two the prompt
         // said nothing about it at all. Remembered here, restated every turn by EncounterContextBuilder.
         if (reason != null && npc != null) _conversationSeekerId = npc.StringId;

         return reason;
      }

      /// <summary>
      ///   Whether THIS conversation is one <paramref name="npc" /> sought: true from the turn the opening
      ///   directive was consumed until the conversation ends. Deliberately not persisted: a conversation never
      ///   survives a save, and a stale seeker read after a reload would be a lie in the prompt.
      /// </summary>
      public bool SoughtThePlayerThisConversation(Hero npc)
         => npc != null && _conversationSeekerId != null && _conversationSeekerId == npc.StringId;

      /// <summary>Cleared as a conversation opens, so the previous one's seeker cannot colour this one.</summary>
      public void ForgetConversationSeeker() => _conversationSeekerId = null;

      private string ConsumeInterceptionReasonCore(Hero npc)
      {
         if (npc == null || _activePursuerId == null || npc.StringId != _activePursuerId)
            return ConsumeRememberedIntent(npc);

         InterceptionTrigger trigger = _activePursuerTrigger;
         bool isFiefAudience = _activePursuerIsFiefAudience;
         string detail = _activePursuerDetail;
         _activePursuerId = null;
         _activePursuerSince = -1;
         _activePursuerTrigger = InterceptionTrigger.None;
         _activePursuerIsFiefAudience = false;
         _activePursuerDetail = null;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;

         // The player reached him first (Lordfadooboo: "i usually have to run them down myself"): the chase is over,
         // so his decisions are his own again.
         EndPursuitSteering();

         // The war duty settled or set aside since he set out (the vassal fought, paid, peace came): he does not
         // accuse a vassal who owes him nothing now (gating before declaration).
         if (trigger == InterceptionTrigger.WarDutySummons && !WarDutyStillStands(npc)) return SummonsSetAsideOpener;

         // A fief audience never rode anywhere -- the NPC was already standing where the player walked in and
         // simply asked for a word. HeardOfDeed, Grudge, AppreciationOwed, and NemesisBounty are all stashed
         // this way (FiefAudiencePolicy.IsFiefValid); only TerritorialPatrol never is (road-only).
         if (isFiefAudience)
         {
            if (trigger == InterceptionTrigger.Grudge)
               return "It is YOU who sought this meeting: a grievance you hold against the player has been " +
                      "weighing on you, and rather than let it fester you simply asked them for a private word " +
                      $"where you already stood. Open by airing it plainly, in your own voice.{DetailClause(detail)}";

            if (trigger == InterceptionTrigger.AppreciationOwed)
               return "It is YOU who sought this meeting: a kindness the player showed you has stayed with you, " +
                      "and rather than let it go unspoken you simply asked them for a private word where you " +
                      $"already stood. Open by acknowledging it warmly, in your own voice.{DetailClause(detail)}";

            if (trigger == InterceptionTrigger.WarDutySummons)
               return "It is YOU who sought this meeting: the player, your sworn vassal, has sat out your realm's war, " +
                      "and when they came into the place where you stood you had them called before you. Open by " +
                      "holding them to it, as their liege, in your own voice. What they owe and what follows is " +
                      "set out below under THE PLAYER'S RECKONING BEFORE YOU; hold to it and add nothing to it.";

            if (trigger == InterceptionTrigger.NemesisBounty)
               return "It is YOU who sought this meeting: a nemesis preys on your region still, and rather than " +
                      "wait for the player to ask you simply drew them aside where you already stood. Open by " +
                      $"naming him and where he operates, urgent but not a call to arms here and now.{DetailClause(detail)} " +
                      "Point them to the bounty you would pay, but do not settle it in this exchange -- that is " +
                      "raised and taken up the ordinary way, not here.";

            return ComposeRecentMatterOpener(npc, RecentMatterApproach.FiefAudience);
         }

         if (trigger == InterceptionTrigger.Grudge)
            return "It is YOU who sought this meeting: a grievance you hold against the player has been weighing " +
                   "on you, and you rode out to confront them with it while it was still raw. Open by airing it " +
                   $"plainly, in your own voice.{DetailClause(detail)}";

         if (trigger == InterceptionTrigger.AppreciationOwed)
            return "It is YOU who sought this meeting: a kindness the player showed you has stayed with you, and " +
                   "you rode out to seek them and acknowledge it. Open by acknowledging it warmly, in your own " +
                   $"voice.{DetailClause(detail)}";

         if (trigger == InterceptionTrigger.NemesisBounty)
            return "It is YOU who sought this meeting: a nemesis preys on your region still, and you rode out to " +
                   "find the player and raise it in person. Open by naming him and where he operates, urgent but " +
                   $"not a call to arms here and now.{DetailClause(detail)} Point them to the bounty you would " +
                   "pay, but do not settle it in this exchange -- that is raised and taken up the ordinary way, not here.";

         if (trigger == InterceptionTrigger.WarDutySummons)
            return "It is YOU who sought this meeting: the player, your sworn vassal, has sat out your realm's war, " +
                   "and you rode out to hold them to it in person. Open on that footing, as their liege, in your own " +
                   "voice. What they owe and what follows is " +
                   "set out below under THE PLAYER'S RECKONING BEFORE YOU; hold to it and add nothing to it.";

         if (trigger == InterceptionTrigger.TerritorialPatrol)
            return "It is YOU who sought this meeting: you rode out to challenge the player for crossing your realm's " +
                   "lands. Open on that footing, demand their business here, wary and territorial, rather than waiting on them.";

         return ComposeRecentMatterOpener(npc, RecentMatterApproach.RoadPursuit);
      }

      /// <summary>
      ///   The remembered-intent half of <see cref="ConsumeInterceptionReason" />: a lord whose chase was
      ///   abandoned inside the grace window still raises his matter when the player seeks him out, framed
      ///   honestly — HE set out, the chase came to nothing, and now the player has come to him. One-shot,
      ///   like the active slot; an expired slot is cleared silently. Only road pursuits ever land here (a
      ///   fief audience is cleared silently on the daily tick, per its own player report).
      /// </summary>
      private string ConsumeRememberedIntent(Hero npc)
      {
         if (npc == null || _rememberedIntentLordId == null || npc.StringId != _rememberedIntentLordId) return null;

         InterceptionTrigger trigger = _rememberedIntentTrigger;
         string detail = _rememberedIntentDetail;

         if (!InterceptionPolicy.IsRememberedIntentActive(_rememberedIntentAbandonedDay, (int) CampaignTime.Now.ToDays))
         {
            ClearRememberedIntent();

            return null;
         }

         ClearRememberedIntent();

         if (trigger == InterceptionTrigger.WarDutySummons && !WarDutyStillStands(npc)) return SummonsSetAsideOpener;

         return trigger switch
         {
            InterceptionTrigger.TerritorialPatrol =>
               "You rode out to challenge the player for crossing your realm's lands, but the chase came to " +
               "nothing — and now they have come to YOU. Raise it now as you meant to then: demand their " +
               "business here, wary and territorial.",
            InterceptionTrigger.Grudge =>
               "You rode out to confront the player over a grievance you hold against them, but the chase came " +
               "to nothing — and now they have come to YOU. Air it now, plainly and in your own voice, as you " +
               $"meant to then.{DetailClause(detail)}",
            InterceptionTrigger.AppreciationOwed =>
               "You rode out to seek the player and acknowledge a kindness they showed you, but the chase came " +
               "to nothing — and now they have come to YOU. Acknowledge it warmly now, in your own voice, as " +
               $"you meant to then.{DetailClause(detail)}",
            InterceptionTrigger.WarDutySummons =>
               "You rode out to hold the player, your sworn vassal, to the war they have sat out, but the chase " +
               "came to nothing, and now they have come to YOU. Hold them to it now, as their liege. What they " +
               "owe and what follows is " +
               "set out below under THE PLAYER'S RECKONING BEFORE YOU; hold to it and add nothing to it.",
            InterceptionTrigger.NemesisBounty =>
               "You rode out to find the player and warn them of a nemesis plaguing your region, but the chase " +
               "came to nothing — and now they have come to YOU. Name him now and where he operates, urgent " +
               $"but not a call to arms here and now.{DetailClause(detail)} Point them to the bounty you would " +
               "pay, but do not settle it in this exchange -- that is raised and taken up the ordinary way, not here.",
            _ => ComposeRecentMatterOpener(npc, RecentMatterApproach.RememberedIntent)
         };
      }

      /// <summary>Whether the reckoning that sent this ruler after the player still stands (something is still owed him).</summary>
      internal static bool WarDutyStillStands(Hero ruler) => GameState.WarLevyDesk.Owed(ruler) > 0;

      /// <summary>The opener for a ruler whose summons was settled or set aside while he rode: no accusation.</summary>
      private const string SummonsSetAsideOpener =
         "It is YOU who sought this meeting: you meant to hold the player, your sworn vassal, to the war they had sat " +
         "out, but the matter has since been settled or set aside. Do not press it or speak as though anything is " +
         "owed; open as you see fit, in your own voice.";

      private void ClearRememberedIntent()
      {
         _rememberedIntentLordId = null;
         _rememberedIntentTrigger = InterceptionTrigger.None;
         _rememberedIntentDetail = null;
         _rememberedIntentAbandonedDay = -1;
      }

      /// <summary>
      ///   Formats the specific grievance/kindness (a Grudge/Appreciation's own CauseSummary) as a trailing
      ///   sentence for a Grudge/AppreciationOwed directive, or an empty string when no detail survived (a
      ///   forced test trigger, or a ledger entry with no cause text): the directive still reads fine without it.
      /// </summary>
      private static string DetailClause(string detail)
         => string.IsNullOrWhiteSpace(detail) ? "" : $" The matter: {detail}.";

      /// <summary>
      ///   The same specific grievance/kindness as <see cref="DetailClause" />, but as a MID-sentence
      ///   parenthetical rather than a trailing full sentence (TryWriteMissedMeetingLetter's own context
      ///   text continues past it with "but the chase came to nothing...").
      /// </summary>
      private static string InlineDetail(string detail)
         => string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail})";

      /// <summary>
      ///   True while the one-shot interception slot (<see cref="ConsumeInterceptionReason" />) is already
      ///   claimed, by a real road pursuit or a stashed fief audience. FiefAudienceBehavior checks this before
      ///   offering, so the two ways an NPC can want a word with the player never collide over the same slot.
      /// </summary>
      public bool HasActivePursuer => _activePursuerId != null;

      /// <summary>
      ///   Peeks (without consuming) whether the CURRENTLY active slot is a stashed fief audience rather than a
      ///   road pursuit. ChatViewModel.ActivateInterceptionOpener reads this, synchronously, before
      ///   ConsumeInterceptionReason ever runs, to pick its own opener cue's framing ("on the road" versus
      ///   "where you already stood"), since that cue is a separate string from the one ConsumeInterceptionReason
      ///   returns and must not contradict it.
      /// </summary>
      public bool ActivePursuitIsFiefAudience => _activePursuerIsFiefAudience;

      /// <summary>
      ///   Peeks (without consuming) the CURRENTLY active slot's trigger, for the SAME reason as
      ///   <see cref="ActivePursuitIsFiefAudience" />: ChatViewModel.ActivateInterceptionOpener needs it to pick
      ///   its own opener cue's framing (a grudge confronted, a kindness acknowledged, or the generic cue)
      ///   before <see cref="ConsumeInterceptionReason" /> ever runs and clears it.
      /// </summary>
      public InterceptionTrigger ActivePursuitTrigger => _activePursuerTrigger;

      /// <summary>
      ///   Peeks (without consuming) the CURRENTLY active slot's specific grievance/kindness summary
      ///   (<see cref="InterceptionTrigger.Grudge" />/<see cref="InterceptionTrigger.AppreciationOwed" /> only;
      ///   null for every other trigger), for the same reason as <see cref="ActivePursuitTrigger" />.
      /// </summary>
      public string ActivePursuitDetail => _activePursuerDetail;

      /// <summary>
      ///   Stashes a one-shot interception directive for <paramref name="npc" /> via the SAME
      ///   <see cref="ConsumeInterceptionReason" /> slot a road pursuit uses, so a fief audience's opener
      ///   survives to the first LLM turn without a second, diverging plumbing. Unlike <see cref="BeginPursuit" />,
      ///   this never engages a party (a fief-audience NPC has none) and never touches the road pursuit's own
      ///   cooldown map: FiefAudienceBehavior owns its own gate and only calls this once
      ///   <see cref="HasActivePursuer" /> is already false. <paramref name="detail" /> carries the specific
      ///   grievance/kindness for a Grudge/AppreciationOwed trigger (see <see cref="_activePursuerDetail" />);
      ///   ignored for every other trigger.
      /// </summary>
      public void StashFiefAudienceReason(Hero npc, InterceptionTrigger trigger, string detail = null)
      {
         if (npc == null || trigger == InterceptionTrigger.None) return;

         _activePursuerId = npc.StringId;
         _activePursuerTrigger = trigger;
         _activePursuerIsFiefAudience = true;
         _activePursuerDetail = detail;
         _activePursuerSince = (int) CampaignTime.Now.ToDays;
         _meetingOpened = true; // there is no party to force a meeting for; nothing for OnTick to do
         _lastPursuitDistance = -1f;
      }

      /// <summary>
      ///   Forces the named lord to ride out and intercept the player (console testing), with <paramref name="trigger" />
      ///   defaulting to <see cref="InterceptionTrigger.HeardOfDeed" /> (the original cr.intercept behaviour,
      ///   unchanged for a caller who passes none). For <see cref="InterceptionTrigger.Grudge" />/
      ///   <see cref="InterceptionTrigger.AppreciationOwed" />, <see cref="ForcedDetailFor" /> pulls the lord's
      ///   REAL grievance/kindness against the player when one exists, so the forced test still reads as the
      ///   genuine feature rather than a placeholder line.
      /// </summary>
      public string DebugForceIntercept(Hero lord, InterceptionTrigger trigger = InterceptionTrigger.HeardOfDeed)
      {
         if (lord?.PartyBelongedTo == null || MobileParty.MainParty == null) return $"{lord?.Name?.ToString() ?? "That hero"} has no party on the map to ride out.";

         var today = (int) CampaignTime.Now.ToDays;
         BeginPursuit(lord, trigger, today, ForcedDetailFor(lord, trigger, today));

         return $"{lord.Name} now rides to intercept you ({trigger}).";
      }

      /// <summary>
      ///   The detail text a FORCED (console) Grudge/AppreciationOwed trigger should carry: the lord's REAL
      ///   live grievance/kindness against the player when <see cref="HoldsLiveGrudgeAgainstPlayer" />/
      ///   <see cref="HoldsSignificantAppreciation" /> actually finds one (so testing exercises the genuine
      ///   detail plumbing), or an honestly-labelled placeholder when none exists (a lord with no real grudge
      ///   forced to voice one should not silently read as though he had one). Null for every other trigger.
      ///   Shared by <see cref="DebugForceIntercept" /> and FiefAudienceBehavior.DebugForceOffer.
      /// </summary>
      internal static string ForcedDetailFor(Hero npc, InterceptionTrigger trigger, int today)
      {
         switch (trigger)
         {
            case InterceptionTrigger.Grudge:
               return HoldsLiveGrudgeAgainstPlayer(npc, today, out string grudgeCause)
                  ? grudgeCause
                  : "a grievance against you (forced for testing, no real grudge on file)";
            case InterceptionTrigger.AppreciationOwed:
               return HoldsSignificantAppreciation(npc, today, out string appreciationCause)
                  ? appreciationCause
                  : "a kindness you showed them (forced for testing, no real appreciation on file)";
            case InterceptionTrigger.NemesisBounty:
               return HasNemesisBountyToOffer(npc, out string nemesisDetail)
                  ? nemesisDetail
                  : "a nemesis plaguing your region (forced for testing, no real eligible nemesis on file)";
            default:
               return null;
         }
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
         if (!CanRideOutToIntercept(lord.PartyBelongedTo)) return false; // in an army or a fight: cannot break off to chase

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
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
      }

      /// <summary>
      ///   A siege can close on the player's fief within hours, and the daily release came too late: the lord waiting
      ///   at the gate was caught in the siege's battle and the player with him (Fakade, 03/10/2026). Checked every
      ///   hour; the rest of the chase is still judged once a day.
      /// </summary>
      private void OnHourlyTick()
      {
         try
         {
            if (_activePursuerId == null || _activePursuerIsFiefAudience || (!PlayerFiefBesieged() && !PlayerInSiegeOrBattle())) return;

            ReleaseStalePursuit((int) CampaignTime.Now.ToDays);
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] siege check failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The player's own party besieges, is besieged or fights on the map (Maifailun, 10/10/2026: a lord reaching a
      ///   besieging army made the game's own encounter, a fight). Includes an army the player leads in a siege.
      /// </summary>
      internal static bool PlayerInSiegeOrBattle()
      {
         MobileParty main = MobileParty.MainParty;
         if (main == null) return false;

         return main.SiegeEvent != null || main.MapEvent != null || main.BesiegedSettlement != null
                || main.Army?.LeaderParty?.SiegeEvent != null;
      }

      /// <summary>The settlement the player is in is under siege, or a siege camp is closing on it.</summary>
      private static bool PlayerFiefBesieged()
      {
         Settlement s = Hero.MainHero?.CurrentSettlement;

         return s != null && (s.IsUnderSiege || s.SiegeEvent != null);
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
      ///   Sets <paramref name="party" /> on the player and HOLDS it there until the chase ends (Lordfadooboo, Nexus,
      ///   30/09/2026: "i'll get a notification that says that a lord is riding out to meet me but... they are doing
      ///   no such thing. passing me by or going into a city"). A bare SetMoveEngageParty is a nudge: the lord's own
      ///   AI re-decides his business on its next cycle and the order is gone, while the notice already told the
      ///   player he was coming. This is the lock dance FollowMeBehavior, the hunters and the couriers already use:
      ///   release any lock, give the order, lock. Every end of a chase goes through <see cref="EndPursuitSteering" />,
      ///   which hands him back (<see cref="ReleaseToVanillaAi" />), so the lock can never outlive the chase the way
      ///   the starving-lord Hold once did.
      /// </summary>
      private void SteerTowardPlayer(MobileParty party)
      {
         if (party?.Ai == null || MobileParty.MainParty == null) return;

         party.Ai.SetDoNotMakeNewDecisions(false);
         party.SetMoveEngageParty(MobileParty.MainParty, MobileParty.NavigationType.Default);
         // MCM "Lords Hold Their Course" (Fakade, 03/10/2026): off gives the order without the lock, as before 2.6.8.
         if (ModSettings.LordsHoldTheirCourse) party.Ai.SetDoNotMakeNewDecisions(true);
         _pursuitParty = party;
      }

      /// <summary>Hands the party we were steering back to its own AI, whatever ended the chase. Safe to call twice.</summary>
      private void EndPursuitSteering()
      {
         MobileParty party = _pursuitParty;
         _pursuitParty = null;
         ReleaseToVanillaAi(party);
      }

      /// <summary>
      ///   cr.testall seam: steers a party onto the player exactly as a pursuit does, reads whether it is held on
      ///   course, then ends the steering and reads whether it is its own again. Refuses while a real pursuit runs.
      /// </summary>
      internal (bool Locked, bool OnThePlayer, bool FreeAfter, bool ThinksAgainAfter)? DebugSteerAndRelease(MobileParty party)
      {
         if (party == null || _pursuitParty != null || _activePursuerId != null) return null;

         SteerTowardPlayer(party);
         bool locked = party.Ai.DoNotMakeNewDecisions;
         bool onPlayer = party.TargetParty == MobileParty.MainParty;
         EndPursuitSteering();

         return (locked, onPlayer, !party.Ai.DoNotMakeNewDecisions, party.Ai.RethinkAtNextHourlyTick);
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
      ///   Self-heal on every load, and it now heals something real. Three passes:
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
      ///     <item>
      ///       Restore a pursuit that crossed the save (re-issue the engage order), or fold it into the
      ///       remembered intent when the world no longer allows it.
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

            // Pass 3: put a pursuit that survived the save back on the player's trail (the engage order itself
            // never crosses a save). Runs AFTER the two heals, so a load with the player inside an army has
            // already been stood down and this restore's own army guard then refuses to re-engage.
            RestorePersistedPursuit();
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] on-load heal failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Re-issues the engage order for a pursuit restored from the save, under the same guards
      ///   <see cref="BeginPursuit" /> enforces (alive, has a party, both ashore, the player not wrapped in an
      ///   army). A restored FIEF AUDIENCE has no party to re-engage — the stashed reason alone crossing the
      ///   save is the whole point. A pursuit the world no longer allows is not simply dropped: a living lord
      ///   falls back to the remembered intent, so the player who seeks him out still gets the matter.
      /// </summary>
      private void RestorePersistedPursuit()
      {
         if (_activePursuerId == null || _activePursuerIsFiefAudience) return;

         Hero pursuer = FindHero(_activePursuerId);
         MobileParty pursuerParty = pursuer?.PartyBelongedTo;
         MobileParty main = MobileParty.MainParty;

         var pursuerAlive = pursuer is {IsAlive: true};
         bool invalid = !pursuerAlive || pursuerParty == null || main == null
                        || PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(pursuerParty)
                        || !InterceptionPolicy.CanMeetOnLand(main.IsCurrentlyAtSea, pursuerParty.IsCurrentlyAtSea);

         if (invalid)
         {
            if (pursuerAlive)
            {
               _rememberedIntentLordId = pursuer.StringId;
               _rememberedIntentTrigger = _activePursuerTrigger;
               _rememberedIntentDetail = _activePursuerDetail;
               _rememberedIntentAbandonedDay = (int) CampaignTime.Now.ToDays;
            }

            ModLog.Info($"[INTERCEPT] saved pursuit of {pursuer?.Name} not restored (no longer possible); " +
                        "the remembered intent carries it.");

            // The save may have kept his decisions locked on the player; they are his own again.
            ReleaseToVanillaAi(pursuerParty);

            _activePursuerId = null;
            _activePursuerSince = -1;
            _activePursuerTrigger = InterceptionTrigger.None;
            _activePursuerIsFiefAudience = false;
            _activePursuerDetail = null;
            _meetingOpened = false;
            _lastPursuitDistance = -1f;

            return;
         }

         SteerTowardPlayer(pursuerParty);
         ModLog.Info($"[INTERCEPT] {pursuer.Name}'s pursuit restored after load ({_activePursuerTrigger}).");
      }

      /// <summary>
      ///   Persists the per-lord cooldowns (as a UTF-8 byte[], like the other CR stores), so a lord cannot
      ///   intercept the player again right after a reload, and — since 2026-09-03 — the live pursuit and the
      ///   remembered intent too, under their own key (player report: "if I go and meet them they don't
      ///   remember they wanted to tell me something"; a mid-pursuit reload used to wipe the errand outright).
      ///   The two keys stay independent so an older save, which has only the cooldown key, loads cleanly.
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

            var pursuit = new PursuitSaveData
            {
               PursuerId = _activePursuerId,
               Since = _activePursuerSince,
               Trigger = (int) _activePursuerTrigger,
               Detail = _activePursuerDetail,
               IsFiefAudience = _activePursuerIsFiefAudience,
               RememberedLordId = _rememberedIntentLordId,
               RememberedTrigger = (int) _rememberedIntentTrigger,
               RememberedDetail = _rememberedIntentDetail,
               RememberedAbandonedDay = _rememberedIntentAbandonedDay
            };

            byte[] pursuitBytes;
            try { pursuitBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pursuit, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[INTERCEPT] pursuit serialize failed — persisting none this save. {ex.GetType().Name}: {ex.Message}");
               pursuitBytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(PursuitSaveKey, ref pursuitBytes); }
            catch (Exception ex) { ModLog.Error($"[INTERCEPT] pursuit save failed: {ex.GetType().Name}: {ex.Message}"); }

            byte[] matterBytes;
            try { matterBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<string>(_raisedMatterKeys), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[INTERCEPT] matter ledger serialize failed, persisting none this save. {ex.GetType().Name}: {ex.Message}");
               matterBytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(MatterSaveKey, ref matterBytes); }
            catch (Exception ex) { ModLog.Error($"[INTERCEPT] matter ledger save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is {Length: > 0})
            {
               try
               {
                  var loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes));
                  _cooldown.Clear();

                  if (loaded != null)
                     foreach (KeyValuePair<string, int> kv in loaded)
                        _cooldown[kv.Key] = kv.Value;
               }
               catch (Exception ex)
               {
                  _cooldown.Clear();
                  ModLog.Error($"[INTERCEPT] load failed — cooldowns dropped for this load. {ex.GetType().Name}: {ex.Message}");
               }
            }

            // The recent-matter ledger loads BEFORE the pursuit's early-return below, so an older save (pursuit key
            // present, empty) never skips it; absent on a pre-2026-09-05 save, degrading to an empty ledger.
            byte[] matterBytes = null;
            try { dataStore.SyncData(MatterSaveKey, ref matterBytes); }
            catch
            {
               /* key absent in older saves, start with an empty raised-matter ledger */
            }

            if (matterBytes is {Length: > 0})
            {
               try
               {
                  var loaded = JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(matterBytes));
                  _raisedMatterKeys = new HashSet<string>(loaded ?? new List<string>());
               }
               catch (Exception ex)
               {
                  _raisedMatterKeys = new HashSet<string>();
                  ModLog.Error($"[INTERCEPT] matter ledger load failed, dropped for this load. {ex.GetType().Name}: {ex.Message}");
               }
            }

            byte[] pursuitBytes = null;
            try { dataStore.SyncData(PursuitSaveKey, ref pursuitBytes); }
            catch
            {
               /* key absent in older saves — no pursuit to restore, which is exactly the old behaviour */
            }

            if (pursuitBytes is not {Length: > 0}) return;
            try
            {
               PursuitSaveData loaded = JsonConvert.DeserializeObject<PursuitSaveData>(Encoding.UTF8.GetString(pursuitBytes));
               if (loaded == null) return;

               _activePursuerId = loaded.PursuerId;
               _activePursuerSince = loaded.Since;
               _activePursuerTrigger = (InterceptionTrigger) loaded.Trigger;
               _activePursuerDetail = loaded.Detail;
               _activePursuerIsFiefAudience = loaded.IsFiefAudience;
               _meetingOpened = _activePursuerIsFiefAudience; // an audience has no party to force a meeting for
               _lastPursuitDistance = -1f;
               _rememberedIntentLordId = loaded.RememberedLordId;
               _rememberedIntentTrigger = (InterceptionTrigger) loaded.RememberedTrigger;
               _rememberedIntentDetail = loaded.RememberedDetail;
               _rememberedIntentAbandonedDay = loaded.RememberedAbandonedDay;
            }
            catch (Exception ex)
            {
               ModLog.Error($"[INTERCEPT] pursuit load failed — dropped for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      /// <summary>The save shape for the live pursuit and the remembered intent (both transient before 2026-09-03).</summary>
      private sealed class PursuitSaveData
      {
         public string PursuerId { get; set; }
         public int Since { get; set; } = -1;
         public int Trigger { get; set; }
         public string Detail { get; set; }
         public bool IsFiefAudience { get; set; }
         public string RememberedLordId { get; set; }
         public int RememberedTrigger { get; set; }
         public string RememberedDetail { get; set; }
         public int RememberedAbandonedDay { get; set; } = -1;
      }

      #region private

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.FindFirst(h => h.StringId == id);

      /// <summary>
      ///   Internal (not private) so FiefAudienceBehavior can reuse the SAME "has this NPC a fresh, unraised matter
      ///   to raise with the player" fact the road interception uses. As of 2026-09-05 ([[project_npc_recent_matter]]
      ///   phase 1) a "matter" is the freshest thing the lord could bring up: a first-hand LIVED memory (a profile
      ///   event about the player, or a world event he took part in) OR a RUMOR about the player's deeds he only
      ///   heard of, that he has not already raised. So a lord who just fought BESIDE the player now approaches about
      ///   THAT (fkasad's report), not only about distant hearsay.
      /// </summary>
      internal static bool HeardRecentPlayerDeed(Hero lord, int today)
         => Instance != null && Instance.PickRecentMatter(lord, today) != null;

      /// <summary>
      ///   WHAT this lord would raise, in a few words, for the invitation that asks the player whether to hear
      ///   him. Null when he has nothing, which is also when no invitation is raised.
      ///   <para>
      ///     fkasad (Nexus, 18/09/2026): "The invitation to talk should specify the topic the NPC wants to
      ///     discuss (that's how it works in real life)." His whole complaint turns on this. He accepts or
      ///     declines blind, so a nine-in-ten chance of a platitude makes declining the rational move, and the
      ///     tenth one, the one worth having, is refused along with the rest. Naming the subject costs nothing,
      ///     needs no model, and hands the choice back to him.
      ///   </para>
      /// </summary>
      internal static string RecentMatterTopic(Hero lord, int today)
         => Instance?.PickRecentMatter(lord, today)?.Summary;

      /// <summary>
      ///   The invitation naming this lord's matter was shown: a piece of world gossip counts as raised from then, taken
      ///   up or declined, so the next character does not offer the same news (froggyluv, 17/10/2026). A matter between
      ///   the lord and the player is left as it was: it is raised when spoken.
      /// </summary>
      internal static void NoteGossipOffered(Hero lord, int today)
      {
         if (Instance == null || lord == null) return;

         RecentMatter m = Instance.PickRecentMatter(lord, today);
         if (m?.Register == RecentMatterRegister.WorldGossip && !string.IsNullOrEmpty(m.Key))
            Instance._raisedMatterKeys.Add(RecentMatterSelector.OfferedPrefix + lord.StringId + "|" + m.Key);
      }

      /// <summary>
      ///   The single matter this lord would raise right now: the freshest unraised one from his own profile
      ///   memories of the player (LIVED) and the world events he knows of involving the player (LIVED if he took
      ///   part, else RUMOR), or null when he has nothing fresh left. Pure selection lives in the tested
      ///   <see cref="RecentMatterSelector" />; this only gathers and classifies the live candidates.
      /// </summary>
      private RecentMatter PickRecentMatter(Hero lord, int today)
      {
         try
         {
            if (lord == null) return null;

            var raisedForLord = new HashSet<string>();
            string prefix = lord.StringId + "|";
            foreach (string entry in _raisedMatterKeys)
               if (entry != null && entry.StartsWith(prefix, StringComparison.Ordinal))
                  raisedForLord.Add(entry.Substring(prefix.Length));

            return RecentMatterSelector.PickFreshest(BuildRecentMatters(lord, today), raisedForLord, today, RecentMatterWindowDays);
         }
         catch { return null; }
      }

      // Gathers the lord's live candidate matters: his own recent profile memories about the player (first-hand by
      // construction, so LIVED), and the recent world events he knows of that involve the player (LIVED when his id
      // or faction is among the actors, else a RUMOR he merely heard). The window + type filter keep it to fresh,
      // fitting matters; the tested selector then picks the freshest unraised one.
      /// <summary>
      ///   Whether a world event happened in this lord's own place or to his own people: the settlement he is
      ///   standing in, one his clan holds, or a faction he belongs to. The mechanical stand-in for "does this
      ///   affect him", and the reason a raid on his own village now outranks a war three hundred leagues off
      ///   (fkasad, 19/09/2026). Fail-soft: unreadable means "does not touch him", which only ever costs the
      ///   event a tie-break it might have won.
      /// </summary>
      private static bool TouchesTheSpeaker(WorldEvent we, Hero lord)
      {
         try
         {
            if (we == null || lord == null) return false;

            if (!string.IsNullOrEmpty(we.PlaceId))
            {
               if (we.PlaceId == lord.CurrentSettlement?.StringId) return true;
               if (we.PlaceId == lord.HomeSettlement?.StringId) return true;
               if (lord.Clan?.Settlements != null)
                  foreach (Settlement held in lord.Clan.Settlements)
                     if (held != null && held.StringId == we.PlaceId)
                        return true;
            }

            if (we.FactionIds != null)
            {
               string clan = lord.Clan?.StringId;
               string realm = lord.MapFaction?.StringId;

               foreach (string id in we.FactionIds)
                  if (!string.IsNullOrEmpty(id) && (id == clan || id == realm))
                     return true;
            }

            return false;
         }
         catch { return false; }
      }

      private static IEnumerable<RecentMatter> BuildRecentMatters(Hero lord, int today)
      {
         var matters = new List<RecentMatter>();

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(lord.StringId);
         if (profile?.Events != null)
            foreach (NotableEvent e in profile.Events)
            {
               if (e == null || string.IsNullOrWhiteSpace(e.summary)) continue;
               if (!RaisableMemoryTypes.Contains(e.type)) continue;
               if (e.gameDay > today || today - e.gameDay > RecentMatterWindowDays) continue;

               matters.Add(new RecentMatter(MemoryKey(e), e.gameDay, e.summary, RecentMatterRegister.LivedMemory));
            }

         WorldEventStore store = WorldEventStore.Instance;
         IEnumerable<string> raisedByAnyone = Instance?._raisedMatterKeys ?? (IEnumerable<string>) Array.Empty<string>();
         if (store != null)
            foreach (WorldEvent we in store.GetKnownEvents(lord, today, 4))
            {
               if (we == null || string.IsNullOrEmpty(we.Id)) continue;
               if (we.Day > today || today - we.Day > RecentMatterWindowDays) continue;

               bool tookPart = LordTookPartIn(we, lord);

               if (we.InvolvesPlayer)
               {
                  // A matter about the PLAYER: lived if he was there, else a rumor of the player's deeds.
                  matters.Add(new RecentMatter("we:" + we.Id, we.Day, we.Summary,
                     tookPart ? RecentMatterRegister.LivedMemory : RecentMatterRegister.RumorAboutPlayer));
               }
               else if (!tookPart && ModSettings.NpcsRaiseWorldNews && we.Magnitude >= GossipMagnitudeThreshold
                        && !RecentMatterSelector.GossipAlreadyRaisedByAnyone(raisedByAnyone, "we:" + we.Id, lord.StringId))
               {
                  // Once per piece of news, not once per character (froggyluv, 17/10/2026: the same war, then the same
                  // capture, asked by notable after notable).
                  //
                  // World GOSSIP (phase 2): a noteworthy happening the lord only heard of and had no part in, so he
                  // probes the player about it. The tookPart guard is also what stops a lord gossiping about
                  // himself ("Lucon asks who Lucon's secret lover is") -- if he is a participant it is never gossip.
                  //
                  // GATED OFF BY DEFAULT since 18/09/2026. This register always yields to anything personal, so
                  // it is precisely what fires when a lord has nothing of his own to say, and three players in
                  // two days described the same result. Gating it HERE, where a gossip matter is minted, rather
                  // than at the approach: a lord left with no matter at all now has nothing to say, so
                  // HeardRecentPlayerDeed is false and he does not ask for a word. Silence is the point.
                  matters.Add(new RecentMatter("we:" + we.Id, we.Day, we.Summary, RecentMatterRegister.WorldGossip,
                     TouchesTheSpeaker(we, lord), we.Magnitude));
               }
            }

         return matters;
      }

      // A deterministic key for a profile memory (NotableEvent has no id): day + type + a bounded summary prefix, so
      // the once-per-NPC ledger recognises the same memory across saves without a process-randomised hash.
      private static string MemoryKey(NotableEvent e)
      {
         string s = e.summary ?? "";
         if (s.Length > 40) s = s.Substring(0, 40);

         return $"mem:{e.gameDay}:{(int) e.type}:{s}";
      }

      // The "he lived it" test for a world event: his own id, or his faction, is among the event's actors (either
      // side). This is also the guard that a lord never speaks of himself as rumor (he is a participant -> LIVED).
      private static bool LordTookPartIn(WorldEvent we, Hero lord)
      {
         if (we == null || lord == null) return false;
         if (we.ActorIds != null && we.ActorIds.Contains(lord.StringId)) return true;
         if (we.AggrievedActorIds != null && we.AggrievedActorIds.Contains(lord.StringId)) return true;

         string factionId = lord.MapFaction?.StringId;
         if (string.IsNullOrEmpty(factionId)) return false;

         return (we.FactionIds != null && we.FactionIds.Contains(factionId))
                || (we.AggrievedFactionIds != null && we.AggrievedFactionIds.Contains(factionId));
      }

      // How the lord came to raise the matter, which colours the opener's frame (the register colours its tone).
      private enum RecentMatterApproach
      {
         RoadPursuit,
         FiefAudience,
         RememberedIntent
      }

      /// <summary>
      ///   Builds the one-shot opener for a "recent matter" approach (the former generic HeardOfDeed line): picks
      ///   the freshest unraised matter for <paramref name="npc" />, marks it raised (so he never repeats it), and
      ///   frames it by REGISTER (a lived memory spoken in the first person, a rumor spoken as hearsay) and by how
      ///   he came to raise it (<paramref name="approach" />). Falls back to the old generic line when he somehow
      ///   has nothing fresh left (the approach was already committed to on a fact that has since aged out or been
      ///   raised), so the conversation never opens on nothing.
      /// </summary>
      private string ComposeRecentMatterOpener(Hero npc, RecentMatterApproach approach)
      {
         RecentMatter m = npc == null ? null : PickRecentMatter(npc, (int) CampaignTime.Now.ToDays);

         if (m == null)
            return approach switch
            {
               RecentMatterApproach.FiefAudience =>
                  "It is YOU who sought this meeting: word of the player's recent deeds reached you, and rather than " +
                  "ride out you drew them aside where you already stood. Open by raising what you heard.",
               RecentMatterApproach.RememberedIntent =>
                  "Word of the player's recent deeds reached you and you set out to raise it, but the chase came to " +
                  "nothing, and now they have come to YOU. Raise what you heard, be it admiration, alarm, or a reckoning.",
               _ =>
                  "It is YOU who sought this meeting: word of the player's recent deeds reached you and you rode out " +
                  "to see them while it was fresh. Open by raising what you heard."
            };

         MarkMatterRaised(npc, m);

         bool gossip = m.Register == RecentMatterRegister.WorldGossip;

         string approachClause = approach switch
         {
            RecentMatterApproach.FiefAudience => gossip
               ? "It is YOU who sought this meeting: rather than ride out, you drew the player aside where you already stood to ask about a recent happening."
               : "It is YOU who sought this meeting: rather than ride out, you drew the player aside where you already stood to raise a recent matter.",
            RecentMatterApproach.RememberedIntent => gossip
               ? "You set out to ask the player about a recent happening, but the chase came to nothing, and now they have come to YOU."
               : "You set out to raise a recent matter with the player, but the chase came to nothing, and now they have come to YOU.",
            _ => gossip
               ? "It is YOU who sought this meeting: you rode out to ask the player about a recent happening while it is the talk of the realm."
               : "It is YOU who sought this meeting: you rode out to raise a recent matter with the player while it was fresh."
         };

         string registerClause = m.Register switch
         {
            RecentMatterRegister.LivedMemory =>
               " You TOOK PART in this yourself, so speak of it in the FIRST PERSON, as your own recollection of what you lived, never as distant rumor.",
            RecentMatterRegister.RumorAboutPlayer =>
               " You did not witness this; you only HEARD of it, so speak of it as hearsay, hedged, not as something you saw firsthand.",
            _ =>
               " This concerns neither you nor the player directly; you only heard of it and do not know the whole of it. Do NOT state it as fact or pretend to know more than you do: raise it and ASK the player what THEY know, curious. They may well know nothing of it, which is only natural: then take it lightly, say what little you heard, and let it go; it is no fault of theirs and no slight to you."
         };

         // fkasad (05/10/2026): a lived memory is written to the player; quoted bare, its "you" read as the character.
         string matter = Logic.Interception.RecentMatterWording.MatterClause(new Logic.Interception.RecentMatterWordingFacts {
            Summary = m.Summary, Register = m.Register, PlayerName = Hero.MainHero?.Name?.ToString(), CharacterName = npc.Name?.ToString()
         });

         return $"{approachClause}{registerClause}{matter}{CaptureFraming(npc, m)}";
      }

      /// <summary>
      ///   A recent matter that is the player's capture of a lord is framed as the act of war it is (Fakade, 08/10/2026),
      ///   by the closest stake the speaker has in the captive. Empty for any other matter.
      /// </summary>
      private static string CaptureFraming(Hero npc, RecentMatter m)
      {
         if (m?.Key == null || !m.Key.StartsWith("we:", StringComparison.Ordinal)) return "";
         WorldEvent e = WorldEventStore.Instance?.GetById(m.Key.Substring(3));
         if (e?.Status != Logic.CaptureNorm.Status) return "";

         bool realm = npc?.MapFaction != null && e.AggrievedFactionIds?.Contains(npc.MapFaction.StringId) == true;
         bool kin = npc?.Clan != null && e.AggrievedActorIds?.Any(id => Hero.FindFirst(h => h.StringId == id)?.Clan == npc.Clan) == true;

         return " " + Logic.CaptureNorm.Line(Logic.CaptureNorm.StakeOf(kin, realm));
      }

      private void MarkMatterRaised(Hero npc, RecentMatter m)
      {
         if (npc == null || m == null || string.IsNullOrEmpty(m.Key)) return;
         _raisedMatterKeys.Add(npc.StringId + "|" + m.Key);
      }

      private const string GenericMissedMeetingContext =
         "You rode out because word of the player's recent deeds reached you and you meant to speak with them about it, but the chase came to nothing before you could catch them.";

      // The missed-meeting LETTER counterpart of ComposeRecentMatterOpener: pick the freshest unraised matter this
      // lord would have raised in person and frame the letter by its register, so a chase that ended in a letter
      // still speaks of the SAME specific thing rather than a generic "your recent deeds". Marks it raised (a letter
      // is a consumption, so a later in-person meeting moves on to the next matter). Falls back to the generic note
      // when the lord holds no fresh matter.
      private string ComposeRecentMatterLetterContext(Hero npc)
      {
         RecentMatter m = npc == null ? null : PickRecentMatter(npc, (int) CampaignTime.Now.ToDays);
         if (m == null) return GenericMissedMeetingContext;

         MarkMatterRaised(npc, m);

         return RecentMatterLetterText(m);
      }

      private static string RecentMatterLetterText(RecentMatter m)
         => m.Register switch
         {
            RecentMatterRegister.LivedMemory =>
               $"You rode out to speak with the player of something you took part in yourself and remember firsthand ({m.Summary}), but the chase came to nothing before you could reach them. Write of it in the FIRST PERSON, as your own recollection of what you lived, never as distant rumor.",
            RecentMatterRegister.RumorAboutPlayer =>
               $"You rode out because word reached you of the player's own doing ({m.Summary}) and you meant to raise it, but the chase came to nothing before you could reach them. You only HEARD of it, so write of it as hearsay, hedged, not as something you witnessed firsthand.",
            _ =>
               $"You rode out to ask the player about a recent happening that is the talk of the realm ({m.Summary}), but the chase came to nothing before you could reach them. You only heard of it and do not know the whole of it: do NOT state it as fact, but write to ASK what they know, curious."
         };

      /// <summary>cr.testall seam: the register-framed missed-meeting letter context this lord would write now, WITHOUT marking the matter raised. Null when the lord holds no fresh matter.</summary>
      internal string DebugRecentMatterLetterContext(Hero lord)
      {
         RecentMatter m = lord == null ? null : PickRecentMatter(lord, (int) CampaignTime.Now.ToDays);

         return m == null ? null : RecentMatterLetterText(m);
      }

      /// <summary>cr.testall seam: notes a piece of gossip as offered by this lord, as an invitation naming it does.</summary>
      internal void DebugNoteGossipOfferedBy(Hero lord, string matterKey)
         => _raisedMatterKeys.Add(RecentMatterSelector.OfferedPrefix + lord.StringId + "|" + matterKey);

      /// <summary>cr.testall seam: forgets every ledger entry about this matter (cleanup).</summary>
      internal void DebugForgetMatter(string matterKey)
         => _raisedMatterKeys.RemoveWhere(entry => entry != null && entry.EndsWith("|" + matterKey, StringComparison.Ordinal));

      /// <summary>cr.testall seam: the classified candidate matters this lord could raise right now (no mutation, no dedup marking).</summary>
      internal IReadOnlyList<RecentMatter> DebugBuildRecentMatters(Hero lord)
         => BuildRecentMatters(lord, (int) CampaignTime.Now.ToDays).ToList();

      /// <summary>
      ///   Internal (not private) so FiefAudienceBehavior can reuse the SAME "does this NPC hold a live,
      ///   unresolved, significant-magnitude grudge against the PLAYER" fact-building the road interception
      ///   uses, rather than a diverging copy. Mirrors CompanionAudienceTopicResolver.ResolveCompanionConflict's
      ///   own grudge read, but the target here is always the PLAYER rather than a fellow party member, and the
      ///   magnitude is gated on InterceptionPolicy.MinGrudgeMagnitudeForInterception (well above the bare
      ///   GrudgePolicy formation floor) so only a genuinely meaningful grievance pulls a lord aside for a
      ///   whole confrontation. Returns the winning grudge's CauseSummary via <paramref name="cause" /> so the
      ///   opener can name the specific matter.
      /// </summary>
      internal static bool HoldsLiveGrudgeAgainstPlayer(Hero lord, int today, out string cause)
      {
         cause = null;

         try
         {
            if (lord == null || Hero.MainHero == null) return false;

            IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(lord.StringId);
            if (grudges == null || grudges.Count == 0) return false;

            var traits = GrudgeBridge.BuildTraitFacts(lord);

            Grudge best = null;
            var bestMagnitude = 0;

            foreach (Grudge g in grudges)
            {
               // An ANSWERED grievance no longer brings its holder out: the confrontation has happened, and the
               // player's account is on the grudge (Zivkyn, 18/09/2026; see GrudgePolicy.StillDrivesConfrontation).
               if (g == null || !GrudgePolicy.StillDrivesConfrontation(g) || g.TargetId != Hero.MainHero.StringId) continue;

               int magnitude = GrudgePolicy.EffectiveMagnitude(g, today, traits);
               if (magnitude <= bestMagnitude) continue;

               best = g;
               bestMagnitude = magnitude;
            }

            if (best == null || bestMagnitude < InterceptionPolicy.MinGrudgeMagnitudeForInterception) return false;

            cause = best.CauseSummary;

            return true;
         }
         catch { return false; }
      }

      /// <summary>
      ///   Internal (not private), the mirror of <see cref="HoldsLiveGrudgeAgainstPlayer" /> for the warm
      ///   ledger: "does this NPC hold a significant, recent appreciation of the PLAYER". Mirrors
      ///   CompanionAudienceTopicResolver.ResolveGratitude's own "already sorted, take first" shape (
      ///   AppreciationBehavior.LiveAppreciationsFor is already ranked strongest-first), but that method reads
      ///   every kindness the holder feels toward ANY target, so this filters to the ones felt toward the
      ///   PLAYER specifically before taking the first, gated on
      ///   InterceptionPolicy.MinAppreciationMagnitudeForInterception (mirrors CompanionGratitudePolicy's own
      ///   "worth a whole audience" floor). Returns the winning appreciation's CauseSummary via
      ///   <paramref name="cause" /> so the opener can name the specific kindness.
      /// </summary>
      internal static bool HoldsSignificantAppreciation(Hero lord, int today, out string cause)
      {
         cause = null;

         try
         {
            if (lord == null || Hero.MainHero == null) return false;

            IReadOnlyList<Appreciation> live = AppreciationBehavior.Instance?.LiveAppreciationsFor(lord, today);
            if (live == null || live.Count == 0) return false;

            var traits = AppreciationBridge.BuildTraitFacts(lord);

            foreach (Appreciation a in live)
            {
               if (a == null || a.TargetId != Hero.MainHero.StringId) continue;

               int magnitude = AppreciationPolicy.EffectiveMagnitude(a, today, traits);
               if (magnitude < InterceptionPolicy.MinAppreciationMagnitudeForInterception) continue;

               cause = a.CauseSummary;

               return true;
            }

            return false;
         }
         catch { return false; }
      }

      /// <summary>
      ///   Internal (not private) so FiefAudienceBehavior can reuse the SAME "does this lord have an eligible
      ///   nemesis bounty to raise" fact-building the road interception uses. Reuses
      ///   <see cref="NemesisBountyDialogFlow.FindEligibleNemesisFor" /> outright rather than re-deriving the
      ///   eligibility gate, so this trigger and the scripted <c>hero_main_options</c> bounty offer it
      ///   announces can never disagree about who is eligible. <paramref name="detail" /> names the nemesis and
      ///   resolves his region the SAME way the encyclopedia dossier does
      ///   (<see cref="NemesisDossierFactory.ResolveRegionDisplay" />), so the opener can name where he operates.
      /// </summary>
      internal static bool HasNemesisBountyToOffer(Hero lord, out string detail)
      {
         detail = null;

         try
         {
            NemesisIdentity nem = NemesisBountyDialogFlow.FindEligibleNemesisFor(lord);
            if (nem == null) return false;

            string displayName = $"{nem.Name} {nem.Epithet}";
            string region = NemesisDossierFactory.ResolveRegionDisplay(nem.RegionId);
            detail = string.IsNullOrEmpty(region) ? displayName : $"{displayName}, who preys on {region}";

            return true;
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

            return lord.GetRelation(Hero.MainHero) <= VanillaRelation.WaryTier;
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

      private void BeginPursuit(Hero lord, InterceptionTrigger trigger, int today, string detail = null)
      {
         // Last line of defense against the army-encounter crash: never engage a party the player is already
         // with (a subordinate army member, a shared army, or an attached party).
         if (PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(lord.PartyBelongedTo)) return;
         // Maifailun (10/10/2026): every way a chase begins, not only the daily one, stays out of a siege or a battle.
         if (!InterceptionPolicy.PlayerCanBeApproached(PlayerInSiegeOrBattle()))
         {
            ModLog.Info($"[INTERCEPT] {lord.Name} does not ride out: the player is in a siege or a battle.");

            return;
         }

         EndPursuitSteering(); // a chase forced over another (cr.intercept) must not leave the first lord locked
         SteerTowardPlayer(lord.PartyBelongedTo);
         _activePursuerId = lord.StringId;
         _activePursuerTrigger = trigger;
         _activePursuerDetail = detail;
         _activePursuerSince = today;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;
         _cooldown[lord.StringId] = today;

         string line = trigger switch {
            InterceptionTrigger.TerritorialPatrol => $"{lord.Name} rides out to challenge you for crossing their lands.",
            InterceptionTrigger.Grudge => $"{lord.Name} rides toward you, a grievance weighing on their mind.",
            InterceptionTrigger.AppreciationOwed => $"{lord.Name} rides toward you, wishing to speak of a kindness you showed them.",
            InterceptionTrigger.NemesisBounty => $"{lord.Name} rides toward you, wishing to speak of a nemesis plaguing their region.",
            InterceptionTrigger.WarDutySummons => $"{lord.Name}, your liege, rides toward you to hold you to the war you have sat out.",
            _ => $"{lord.Name} rides toward you, having heard of your recent deeds."
         };
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
         EndPursuitSteering();
         ReleaseToVanillaAi(pursuerParty);

         // An enemy asks first (Gabriel, 16/10/2026): the player may be about to attack him, and a declined word costs
         // no call to the model.
         if (InterceptionPolicy.AsksBeforeMeeting(_activePursuerTrigger, AtWarWithPlayer(pursuer)))
         {
            OfferEnemyMeeting(pursuer, _activePursuerTrigger);

            return;
         }

         ChatSceneStager.StagePlainConversation(pursuer);
         ModLog.Info($"[INTERCEPT] {pursuer.Name} reached the player, forcing the meeting open.");
      }

      /// <summary>
      ///   The enemy's word, offered by a popup: accepted, the meeting is staged as any other; declined, the chase ends
      ///   there, with no letter and no errand kept in mind (the player chose not to hear him).
      /// </summary>
      private void OfferEnemyMeeting(Hero pursuer, InterceptionTrigger trigger)
      {
         string why = trigger == InterceptionTrigger.TerritorialPatrol
            ? " for crossing their lands"
            : "";
         ModLog.Info($"[INTERCEPT] {pursuer.Name} reached the player; an enemy, so the word is offered first ({trigger}).");

         AudienceInquiry.Show(pursuer, $"{pursuer.Name} bars your way",
            $"{pursuer.Name}, of a realm at war with yours, rides up and demands a word with you{why}.",
            "Speak with them", "Not now",
            () => MainThreadDispatcher.Enqueue(() =>
            {
               // One frame later, as the fief audience does: opening the chat inside the inquiry's consequence left the
               // inquiry on screen and the engine showed it again every frame.
               if (_activePursuerId != pursuer.StringId) return;
               ChatSceneStager.StagePlainConversation(pursuer);
               ModLog.Info($"[INTERCEPT] {pursuer.Name}'s word was heard, the meeting is staged.");
            }),
            () => DeclineEnemyMeeting(pursuer));
      }

      private void DeclineEnemyMeeting(Hero pursuer)
      {
         if (_activePursuerId != pursuer?.StringId) return;

         _activePursuerId = null;
         _activePursuerSince = -1;
         _activePursuerTrigger = InterceptionTrigger.None;
         _activePursuerDetail = null;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;
         ModLog.Info($"[INTERCEPT] {pursuer.Name}'s word was declined; no conversation, no call to the model.");
      }

      /// <summary>Whether the lord's realm is at war with the player's (the popup's test; personal dislike is not war).</summary>
      private static bool AtWarWithPlayer(Hero lord)
      {
         try
         {
            IFaction lf = lord?.MapFaction,
                     pf = Hero.MainHero?.MapFaction;

            return lf != null && pf != null && lf != pf && FactionManager.IsAtWarAgainstFaction(lf, pf);
         }
         catch { return false; }
      }

      /// <summary>
      ///   Whether this lord's party is outnumbered by the player's force (the army the player leads, else the player's
      ///   party), by the engine's own strength estimates (LapitaPolata, 18/10/2026).
      /// </summary>
      private static bool LordOutnumbered(MobileParty lordParty)
      {
         try
         {
            MobileParty main = MobileParty.MainParty;
            if (lordParty?.Party == null || main?.Party == null) return false;

            float player = main.Army != null && main.Army.LeaderParty == main ? main.Army.EstimatedStrength : main.Party.EstimatedStrength;

            return InterceptionPolicy.IsOutnumbered(lordParty.Party.EstimatedStrength, player);
         }
         catch { return false; }
      }

      /// <summary>
      ///   cr.testall seam: the reason this lord would ride out for, with the player on his realm's land and notorious,
      ///   his war and his strength read from the live world (<paramref name="atWar" /> overrides the war when the world
      ///   holds no enemy), and every other matter (grudge, news) set by the caller.
      /// </summary>
      internal static InterceptionTrigger DebugChooseOnHisLand(Hero lord, bool atWar, bool holdsGrudge)
         => InterceptionPolicy.Choose(new InterceptionFacts {
            PlayerInOwnTerritory = true,
            FactionHostileToPlayer = true,
            PlayerNotoriety = InterceptionPolicy.NotorietyThreshold,
            HoldsLiveGrudgeAgainstPlayer = holdsGrudge,
            RealmAtWarWithPlayer = atWar,
            LordOutnumbered = LordOutnumbered(lord?.PartyBelongedTo)
         });

      /// <summary>cr.testall seam: whether this lord's realm is at war with the player's.</summary>
      internal static bool DebugAtWarWithPlayer(Hero lord) => AtWarWithPlayer(lord);

      /// <summary>cr.testall seam: whether this lord's party is outnumbered by the player's force.</summary>
      internal static bool DebugLordOutnumbered(Hero lord) => LordOutnumbered(lord?.PartyBelongedTo);

      /// <summary>cr.testall seam: whether this lord, reaching the player for this reason, would ask first.</summary>
      internal static bool DebugAsksBeforeMeeting(Hero lord, InterceptionTrigger trigger)
         => InterceptionPolicy.AsksBeforeMeeting(trigger, AtWarWithPlayer(lord));

      /// <summary>
      ///   True only when the player is standing free on the open campaign map: not in a scene/battle, an existing
      ///   conversation, a settlement, an encounter, or a game menu, and with no CR chat already open or staging.
      ///   Forcing a conversation in any of those states is unsafe or nonsensical.
      /// </summary>
      internal static bool PlayerIsFreeOnMap()
      {
         if (Mission.Current != null) return false;
         if (Hero.OneToOneConversationHero != null) return false;
         if (Hero.MainHero?.CurrentSettlement != null) return false;
         if (PlayerEncounter.Current != null) return false;
         if (Campaign.Current?.CurrentMenuContext != null) return false;
         if (ChatManager.IsOpen) return false;
         if (ChatSceneStager.IsStaging) return false;
         // A council staging its own conversation scene is the same kind of "about to hand focus to a
         // conversation" moment as a captive scene staging one; a lord riding out mid-stage would collide.
         if (CouncilConversationStager.IsStaging) return false;
         // Maifailun (10/10/2026): a siege camp or a battle is no open map, whatever menu shows.
         if (PlayerInSiegeOrBattle()) return false;

         return true;
      }

      private void ReleaseStalePursuit(int today)
      {
         if (_activePursuerId == null) return;

         // A fief audience never RODE anywhere: the NPC was standing in the settlement and asked for a word,
         // and its reason is meant to be consumed by the conversation (ConsumeInterceptionReason). If one is
         // still stashed by the time a daily release runs, the conversation did not consume it, so clear it
         // SILENTLY: a partyless notable never pursued the player, so it must never produce a road-style
         // "lost track of pursuing you" notification or a missed-meeting letter (player report: a village
         // noble the player had just spoken to still sent that notice hours later).
         if (_activePursuerIsFiefAudience)
         {
            _activePursuerId = null;
            _activePursuerSince = -1;
            _activePursuerTrigger = InterceptionTrigger.None;
            _activePursuerIsFiefAudience = false;
            _activePursuerDetail = null;
            _meetingOpened = false;
            _lastPursuitDistance = -1f;

            return;
         }

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

         // Called into an army mid-chase: his place is with it now (and a party we hold would stay out of it).
         bool invalid = pursuer is not {IsAlive: true} || pursuerParty == null || main == null
                        || PlayerIsSubordinateInArmy() || PlayerSharesArmyOrAttached(pursuerParty) || atSea
                        || pursuerParty.Army != null
                        // The war duty settled or set aside while he rode: he has no business with the player now.
                        || (_activePursuerTrigger == InterceptionTrigger.WarDutySummons && !WarDutyStillStands(pursuer));

         float distance = pursuerParty != null && main != null
            ? pursuerParty.GetPosition2D.Distance(main.GetPosition2D)
            : float.MaxValue;

         // The player closing the gap ON PURPOSE ("I'll ride up to them and everything") must not read as
         // outrun or timed out from a single day's snapshot: compare against yesterday's reading, not the
         // raw speed ratio alone, and defer the give-up while the gap keeps shrinking.
         bool isClosing = !invalid && _lastPursuitDistance >= 0f && distance < _lastPursuitDistance;

         bool giveUp = !invalid &&
                       InterceptionPolicy.ShouldAbandonChase(new InterceptionPolicy.ChaseFacts {
                          PursuerSpeed = pursuerParty.Speed, PlayerSpeed = main.Speed, Distance = distance, DaysPursuing = today - _activePursuerSince,
                          IsClosing = isClosing, PlayerInsideSettlement = Hero.MainHero?.CurrentSettlement != null, PlayerSettlementBesieged = PlayerFiefBesieged(),
                          PlayerInSiegeOrBattle = PlayerInSiegeOrBattle()
                       });

         if (!invalid && !giveUp)
         {
            _lastPursuitDistance = distance;

            return;
         }

         InterceptionTrigger trigger = _activePursuerTrigger; // read before the fields below are cleared
         string detail = _activePursuerDetail;                // ditto
         AbandonReason reason = !invalid && (PlayerFiefBesieged() || PlayerInSiegeOrBattle()) ? AbandonReason.Siege : DetermineAbandonReason(atSea, invalid, today - _activePursuerSince);
         var pursuerAlive = pursuer is {IsAlive: true};

         ModLog.Info($"[INTERCEPT] {pursuer?.Name} stands down ({reason}, distance {distance:0.0}, " +
                     $"{today - _activePursuerSince} day(s) pursued, closing={isClosing}).");

         NotifyPursuitAbandoned(pursuer, reason);

         // A LIVING lord keeps his errand in mind for a few days however the chase ended: if the player seeks
         // him out within the window, he raises it in person (player report, 2026-09-03: "if I go and meet
         // them they don't remember they wanted to tell me something"). A dead man remembers nothing.
         if (pursuerAlive)
         {
            _rememberedIntentLordId = pursuer.StringId;
            _rememberedIntentTrigger = trigger;
            _rememberedIntentDetail = detail;
            _rememberedIntentAbandonedDay = today;
         }

         // The letter gate is the policy's call: an ordinary give-up always writes; drawn into other business
         // writes too, but only while the pursuer is alive to hold a pen; a chase lost at sea never does
         // (nothing was ever going to connect, and the remembered-intent window above already covers the
         // player who comes back ashore and seeks the lord out).
         if (InterceptionPolicy.ShouldWriteMissedMeetingLetter(reason, pursuerAlive, trigger))
            TryWriteMissedMeetingLetter(pursuer, trigger, detail, today);

         // End the engage on any abandoned chase too, so a lord who timed out or was outrun stops bearing on the
         // player instead of trailing them indefinitely, and RESUMES his own plans rather than standing in the
         // field. Null when the pursuer died; the party we were steering is released all the same.
         EndPursuitSteering();
         ReleaseToVanillaAi(pursuerParty);

         _activePursuerId = null;
         _activePursuerSince = -1;
         _activePursuerTrigger = InterceptionTrigger.None;
         _activePursuerDetail = null;
         _meetingOpened = false;
         _lastPursuitDistance = -1f;
      }

      /// <summary>Why an abandoned pursuit ended lives in <see cref="AbandonReason" /> (Logic) so the gates below stay pure and tested.</summary>
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
            AbandonReason.Siege         => $"{pursuer.Name} will not wait at the gates of a besieged town, and turns away.",
            _                           => $"{pursuer.Name} cannot keep pace with you and loses your trail."
         };

         try { MBInformationManager.AddQuickInformation(new TextObject(line)); }
         catch
         {
            /* notification is best-effort, same as BeginPursuit's own announcement */
         }
      }

      /// <summary>
      ///   When a chase is abandoned for a reason the letter gate (<see cref="InterceptionPolicy.ShouldWriteMissedMeetingLetter" />)
      ///   allows, the lord who wanted a word writes instead, through the existing letter pipeline, rather than
      ///   the whole thing simply evaporating. Reuses <see cref="InterceptionTrigger" /> for WHY he set out,
      ///   exactly as the in-person opener does. A lord the player has never actually met has no profile to
      ///   write from and is silently skipped; the LLM pipeline itself (checked inside
      ///   <see cref="LetterGenerationService.QueueInitialLetter" />) is the rest of "can actually deliver".
      ///   Called at most once per abandoned chase.
      /// </summary>
      private void TryWriteMissedMeetingLetter(Hero pursuer, InterceptionTrigger trigger, string detail, int today)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            BannerlordNpcMemoryStore store = behavior?.Store;
            NpcProfile profile = pursuer != null ? store?.Get(pursuer.StringId) : null;

            if (profile == null || behavior.LlmServices?.ChatService == null) return;
            // Never for the war duty, which sends its own levy letter (the policy already says so); and never through
            // the recent-matter default below, which would spend a matter.
            if (trigger == InterceptionTrigger.WarDutySummons) return;

            string context = trigger switch {
               InterceptionTrigger.TerritorialPatrol =>
                  "You rode out to challenge the player for crossing your realm's lands, wary and territorial, but the chase came to nothing before you could face them.",
               InterceptionTrigger.Grudge =>
                  $"You rode out to confront the player over a grievance you hold against them{InlineDetail(detail)}, but the chase came to nothing before you could catch them.",
               InterceptionTrigger.AppreciationOwed =>
                  $"You rode out to seek the player and acknowledge a kindness they showed you{InlineDetail(detail)}, but the chase came to nothing before you could catch them.",
               InterceptionTrigger.NemesisBounty =>
                  $"You rode out to find the player and warn them of a nemesis plaguing your region{InlineDetail(detail)}, but the chase came to nothing before you could catch them.",
               // Recent-matter chase: name the SAME specific matter the in-person opener would have raised, framed
               // by its register (a memory you lived, a rumor of the player's deed, or realm gossip you would ask
               // about), rather than a generic "your recent deeds". Consumes it (once per lord), like the opener.
               _ => ComposeRecentMatterLetterContext(pursuer)
            };

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
         if (!InterceptionPolicy.PlayerCanBeApproached(PlayerInSiegeOrBattle())) return; // Maifailun: not into a siege or a battle

         Vec2 playerPos = main.GetPosition2D;
         IFaction territory = NearestSettlementFaction(playerPos);
         var notoriety = (int) (Clan.PlayerClan?.Renown ?? 0f);

         // The war duty: the ruler who calls the player in waits at his hall and never rides out (InterceptionPolicy.IsRoadValid).
         Hero summoner = GameState.WarLevyDesk.Summoner();

         var scanned = 0;
         foreach (MobileParty p in MobileParty.All)
         {
            if (scanned >= MaxCandidates) break;

            Hero lord = p?.LeaderHero;

            if (lord is not {IsLord: true} || !Mapping.ActionGate.Allows(lord, Logic.ActionKind.RoadInterception)) continue; // IsLord is a rank, never an age
            if (p == main || p.IsBandit || p.MapFaction == null) continue;
            if (PlayerSharesArmyOrAttached(p)) continue; // player leads an army holding this party, or is attached to it
            if (!CanRideOutToIntercept(p)) continue;     // committed to an army or locked in a fight: cannot break off to chase
            if (p.GetPosition2D.Distance(playerPos) > InterceptRange) continue;

            // Gabriel (28/09/2026): an empress does not ride out herself after a landless newcomer. A greater lord is
            // passed over, and another nearby lord may still come; the same rule as a letter written on their behalf.
            if (!CorrespondenceRank.DealsInPerson(lord, NpcMemoryCampaignBehavior.Instance?.Store?.Get(lord.StringId), null)) continue;

            scanned++;

            bool holdsGrudge = HoldsLiveGrudgeAgainstPlayer(lord, today, out string grudgeCause);
            bool holdsAppreciation = HoldsSignificantAppreciation(lord, today, out string appreciationCause);
            bool hasNemesisBounty = HasNemesisBountyToOffer(lord, out string nemesisDetail);

            var facts = new InterceptionFacts {
               OnCooldown = _cooldown.TryGetValue(lord.StringId, out int last) && today - last < CooldownDays,
               AnotherPursuerActive = _activePursuerId != null,
               PlayerInOwnTerritory = territory != null && territory == lord.MapFaction,
               FactionHostileToPlayer = IsHostileToPlayer(lord),
               PlayerNotoriety = notoriety,
               HeardRecentPlayerDeed = HeardRecentPlayerDeed(lord, today),
               HoldsLiveGrudgeAgainstPlayer = holdsGrudge,
               HoldsSignificantAppreciation = holdsAppreciation,
               HasNemesisBountyToOffer = hasNemesisBounty,
               HoldsWarDutyReckoning = lord == summoner,
               RealmAtWarWithPlayer = AtWarWithPlayer(lord),
               LordOutnumbered = LordOutnumbered(p)
            };

            InterceptionTrigger trigger = InterceptionPolicy.Choose(facts);

            // A summoning ruler stays in his hall while the summons stands, for that errand or any other.
            if (trigger == InterceptionTrigger.None || !InterceptionPolicy.IsRoadValid(trigger)) continue;

            string detail = trigger switch {
               InterceptionTrigger.Grudge => grudgeCause,
               InterceptionTrigger.AppreciationOwed => appreciationCause,
               InterceptionTrigger.NemesisBounty => nemesisDetail,
               _ => null
            };

            BeginPursuit(lord, trigger, today, detail);

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

      /// <summary>
      ///   True only when <paramref name="party" /> is actually FREE to break off and ride a personal road chase of
      ///   the player. A party committed to an army (a member follows the army; even the leader is bound to keep it
      ///   together) or already locked in a battle cannot, so choosing one only spawns a pursuit that can never
      ///   connect and, worse, a heavy, consequence-free "missed meeting" LLM letter (player report 2026-09-05:
      ///   lords gathering into an army by the player kept "riding out" and writing such letters day after day).
      ///   Checked BEFORE a pursuit begins, so the useless work is never started rather than abandoned later.
      /// </summary>
      private static bool CanRideOutToIntercept(MobileParty party)
         => party != null && party.Army == null && party.MapEvent == null;

      #endregion
   }
}