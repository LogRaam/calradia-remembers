// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Reads the live game state into the eligibility/amount facts for the "RP with teeth" court actions
   ///   (give_influence, lend_troops, sway_opinion, release_prisoner, and their 04/07/2026 reciprocals
   ///   give_troops/spend_influence), delegating the pure math to <see cref="CourtActionPolicy" />. Shared
   ///   by three call sites so they can never drift apart: <c>EncounterContextBuilder</c> (what to teach the
   ///   LLM), <c>BannerlordGameStateBridge</c> (re-validation at execution — the bridge is the law), and the
   ///   <c>cr.rp_verbs</c>/<c>cr.give_troops</c>/<c>cr.spend_influence</c> console commands. Per-NPC
   ///   cooldowns are derived from the profile's own <see cref="NpcProfile.Events" /> (a hidden marker in
   ///   the recorded summary), so no new persisted field is needed.
   /// </summary>
   public static class CourtActionResolver
   {
      /// <summary>Hidden marker recorded on a give_influence memory event, scanned back for the cooldown.</summary>
      public const string InfluenceMarker = "[cr_influence]";

      /// <summary>Hidden marker recorded on a lend_troops memory event, scanned back for the cooldown.</summary>
      public const string TroopsMarker = "[cr_troops]";

      /// <summary>
      ///   Hidden marker recorded on a give_troops memory event (04/07/2026, the reciprocal of lend_troops),
      ///   scanned back for the cooldown. Deliberately distinct from <see cref="TroopsMarker" /> so the two
      ///   opposite directions never share a cooldown clock.
      /// </summary>
      public const string GiveTroopsMarker = "[cr_troops_given]";

      /// <summary>
      ///   Hidden marker recorded on a spend_influence memory event (04/07/2026, the reciprocal of
      ///   give_influence), scanned back for the cooldown. Deliberately distinct from
      ///   <see cref="InfluenceMarker" /> so the two opposite directions never share a cooldown clock.
      /// </summary>
      public const string SpendInfluenceMarker = "[cr_influence_spent]";

      /// <summary>Hidden marker PREFIX recorded on a sway_opinion memory event; the target's StringId follows.</summary>
      public const string SwayMarkerPrefix = "[cr_sway:";

      /// <summary>The full per-pair marker for a sway_opinion cooldown scan against one named target.</summary>
      public static string SwayMarker(string targetHeroId) => $"{SwayMarkerPrefix}{targetHeroId}]";

      // ── give_influence ───────────────────────────────────────────────────

      /// <summary>
      ///   True when this lord would currently lend the player political influence, with the amount
      ///   they would grant. Every gate re-checked: a living lord of another clan, deep enough regard,
      ///   a clan treasury of influence comfortably above the grant, and the cooldown elapsed.
      /// </summary>
      public static bool TryGiveInfluence(Hero npc, NpcProfile profile, out int amount)
      {
         amount = 0;
         if (npc is not {IsLord: true, IsAlive: true} || npc.IsPrisoner) return false;
         if (Hero.MainHero == null) return false;

         Clan giver = npc.Clan;
         if (giver == null || giver == Clan.PlayerClan) return false;

         int relation = npc.GetRelation(Hero.MainHero);
         if (relation < CourtActionPolicy.InfluenceMinRelation) return false;

         int candidate = CourtActionPolicy.InfluenceGrant(relation);
         if (candidate <= 0) return false;
         if (!CourtActionPolicy.CanAffordInfluenceGrant(giver.Influence, candidate)) return false;
         if (!CourtActionPolicy.CooldownElapsed(LastMarkerDay(profile, InfluenceMarker), Today(), CourtActionPolicy.InfluenceCooldownDays)) return false;

         amount = candidate;

         return true;
      }

      // ── lend_troops ──────────────────────────────────────────────────────

      /// <summary>
      ///   True when this NPC (leading their own party) would currently lend the player some troops,
      ///   with the count they would part with. Re-checks party leadership, regard, a healthy enough
      ///   force to spare from, the player's remaining party room, and the cooldown.
      /// </summary>
      public static bool TryLendTroops(Hero npc, NpcProfile profile, out int count)
      {
         count = 0;
         if (npc is not {IsAlive: true} || npc.IsPrisoner || npc == Hero.MainHero) return false;
         if (Hero.MainHero == null) return false;

         MobileParty npcParty = npc.PartyBelongedTo;
         if (npcParty == null || npcParty.LeaderHero != npc) return false;

         int relation = npc.GetRelation(Hero.MainHero);
         if (relation < CourtActionPolicy.TroopLoanMinRelation) return false;

         int npcHealthyTroops = HealthyRegularTroopCount(npcParty.MemberRoster);
         if (npcHealthyTroops < CourtActionPolicy.TroopLoanMinPartyStrength) return false;

         MobileParty playerParty = MobileParty.MainParty;
         if (playerParty == null) return false;

         int playerRoom = Math.Max(0, playerParty.Party.PartySizeLimit - playerParty.MemberRoster.TotalManCount);
         if (playerRoom <= 0) return false;

         int candidate = CourtActionPolicy.TroopLoanCount(relation, npcHealthyTroops, playerRoom);
         if (candidate <= 0) return false;
         if (!CourtActionPolicy.CooldownElapsed(LastMarkerDay(profile, TroopsMarker), Today(), CourtActionPolicy.TroopLoanCooldownDays)) return false;

         count = candidate;

         return true;
      }

      /// <summary>
      ///   Moves up to <paramref name="count" /> healthy regular troops (never heroes), lowest tier
      ///   first, from <paramref name="from" />'s roster into <paramref name="to" />'s. Returns how
      ///   many actually moved (may be less than asked if the roster changed since eligibility was
      ///   computed — the bridge re-reads live state, it does not trust the earlier count blindly).
      /// </summary>
      public static int MoveLowestTierTroops(MobileParty from, MobileParty to, int count)
      {
         if (from?.MemberRoster == null || to?.MemberRoster == null || count <= 0) return 0;

         var moved = 0;
         List<TroopRosterElement> candidates = from.MemberRoster.GetTroopRoster()
                                                    .Where(el => el.Character is {IsHero: false} && el.Number - el.WoundedNumber > 0)
                                                    .OrderBy(el => el.Character.Tier)
                                                    .ToList();

         foreach (TroopRosterElement el in candidates)
         {
            if (moved >= count) break;

            int healthy = el.Number - el.WoundedNumber;
            int take = Math.Min(healthy, count - moved);

            if (take <= 0) continue;

            from.MemberRoster.AddToCounts(el.Character, -take);
            to.MemberRoster.AddToCounts(el.Character, take);
            moved += take;
         }

         return moved;
      }

      // ── give_troops (reciprocal of lend_troops, 04/07/2026) ─────────────

      /// <summary>
      ///   True when the player could currently reinforce this NPC's own (genuinely under-strength) party
      ///   with some of the player's own spare troops, with the count that would be offered. Every gate
      ///   re-checked: a living NPC leading their own party, no standing war between the two factions,
      ///   non-hostile personal relation, a party that is truly weakened, the NPC party having room to
      ///   receive them, and the player's own party having spare troops above a safety floor; plus the
      ///   cooldown.
      /// </summary>
      public static bool TryGiveTroops(Hero npc, NpcProfile profile, out int count)
      {
         count = 0;
         if (npc is not {IsAlive: true} || npc.IsPrisoner || npc == Hero.MainHero) return false;
         if (Hero.MainHero == null) return false;

         MobileParty npcParty = npc.PartyBelongedTo;
         if (npcParty == null || npcParty.LeaderHero != npc) return false;

         if (npc.MapFaction != null && Hero.MainHero.MapFaction != null &&
             FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction)) return false;

         int relation = npc.GetRelation(Hero.MainHero);
         if (relation < CourtActionPolicy.GiveTroopsMinRelation) return false;

         int npcHealthyTroops = HealthyRegularTroopCount(npcParty.MemberRoster);
         if (!CourtActionPolicy.IsPartyWeakened(npcHealthyTroops, npcParty.Party.PartySizeLimit)) return false;

         int npcRoom = Math.Max(0, npcParty.Party.PartySizeLimit - npcParty.MemberRoster.TotalManCount);
         if (npcRoom <= 0) return false;

         MobileParty playerParty = MobileParty.MainParty;
         if (playerParty == null) return false;

         int playerHealthyTroops = HealthyRegularTroopCount(playerParty.MemberRoster);

         int candidate = CourtActionPolicy.TroopGiftCount(relation, playerHealthyTroops, npcRoom);
         if (candidate <= 0) return false;
         if (!CourtActionPolicy.CooldownElapsed(LastMarkerDay(profile, GiveTroopsMarker), Today(), CourtActionPolicy.GiveTroopsCooldownDays)) return false;

         count = candidate;

         return true;
      }

      // ── spend_influence (reciprocal of give_influence, 04/07/2026) ──────

      /// <summary>
      ///   True when the player's own clan could currently spend some influence to back this needy lord's
      ///   clan at court, with the amount that would transfer. Every gate re-checked: a living lord of
      ///   another clan, no standing war between the two factions, non-hostile personal relation, the
      ///   NPC's own clan genuinely low on influence, and the player's clan holding comfortably more in
      ///   reserve than the grant costs; plus the cooldown. Reuses <see cref="CourtActionPolicy.InfluenceGrant" />
      ///   and <see cref="CourtActionPolicy.CanAffordInfluenceGrant" /> exactly as <see cref="TryGiveInfluence" />
      ///   does — the same math, only the giver and receiver are swapped.
      /// </summary>
      public static bool TrySpendInfluence(Hero npc, NpcProfile profile, out int amount)
      {
         amount = 0;
         if (npc is not {IsLord: true, IsAlive: true} || npc.IsPrisoner) return false;
         if (Hero.MainHero == null) return false;

         Clan recipient = npc.Clan;
         if (recipient == null || recipient == Clan.PlayerClan) return false;

         if (npc.MapFaction != null && Hero.MainHero.MapFaction != null &&
             FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction)) return false;

         int relation = npc.GetRelation(Hero.MainHero);
         if (relation < CourtActionPolicy.SpendInfluenceMinRelation) return false;
         if (!CourtActionPolicy.ClanNeedsInfluence(recipient.Influence)) return false;

         int candidate = CourtActionPolicy.InfluenceGrant(relation);
         if (candidate <= 0) return false;

         Clan giver = Clan.PlayerClan;
         if (giver == null || !CourtActionPolicy.CanAffordInfluenceGrant(giver.Influence, candidate)) return false;
         if (!CourtActionPolicy.CooldownElapsed(LastMarkerDay(profile, SpendInfluenceMarker), Today(), CourtActionPolicy.SpendInfluenceCooldownDays)) return false;

         amount = candidate;

         return true;
      }

      // ── sway_opinion ─────────────────────────────────────────────────────

      /// <summary>
      ///   True whenever the conversation partner is a lord or notable with a tracked profile — cheap,
      ///   always-on teaching. The genuine-persuasion guard lives in the prompt wording, not a game gate:
      ///   there is no engine fact that tells us the NPC was truly convinced rather than merely asked.
      /// </summary>
      public static bool CanTeachSway(Hero npc, NpcProfile profile)
         => npc is {IsAlive: true} && !npc.IsPrisoner && npc != Hero.MainHero && (npc.IsLord || npc.IsNotable) && profile != null;

      /// <summary>
      ///   Resolves the named third party by a tolerant, case-insensitive name match against every
      ///   living hero (mirrors <c>ModCheats.ResolveHero</c>), excluding the player and the NPC
      ///   speaking — sway_opinion always targets someone ELSE. Null when no name was given or no
      ///   living hero matches.
      /// </summary>
      public static Hero ResolveSwayTarget(string name, Hero npc)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         string query = name.Trim();

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != npc && h.Name != null &&
            h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
      }

      /// <summary>True when this NPC could currently be swayed again about this particular target (per-pair cooldown).</summary>
      public static bool SwayCooldownElapsed(NpcProfile profile, string targetHeroId)
         => CourtActionPolicy.CooldownElapsed(LastMarkerDay(profile, SwayMarker(targetHeroId)), Today(), CourtActionPolicy.SwayCooldownDays);

      // ── pledge_against (RP with teeth, increment 2) ─────────────────────

      /// <summary>
      ///   The name of a rival this lord holds genuine, standing enmity toward and could pledge, in
      ///   conversation, to move against — gates the pledge_against teaching. Reuses the kin-safe,
      ///   already-disliked selection pattern from <c>sway_opinion</c>'s target picking
      ///   (<see cref="CourtActionPolicy.SelectPledgeTarget" />), scanning every other living lord. Null
      ///   when the NPC is not eligible, already has a pledge outstanding (one at a time —
      ///   <see cref="CommitmentBehavior.HasActive" />), or holds no one in real enough enmity.
      /// </summary>
      public static string ResolvePledgeRivalName(Hero npc, NpcProfile profile)
      {
         if (npc is not {IsLord: true, IsAlive: true} || npc.IsPrisoner || profile == null) return null;
         if (Hero.MainHero == null) return null;
         if (CommitmentBehavior.Instance?.HasActive(npc.StringId) == true) return null;

         var candidates = new List<CourtActionPolicy.SwayCandidate>();
         foreach (Hero other in Hero.AllAliveHeroes)
         {
            if (other == npc || other == Hero.MainHero || !other.IsLord) continue;
            candidates.Add(new CourtActionPolicy.SwayCandidate(other.StringId, npc.GetRelation(other), IsCloseKin(npc, other), other.MapFaction?.Leader == other));
         }

         string targetId = CourtActionPolicy.SelectPledgeTarget(candidates);
         if (targetId == null) return null;

         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == targetId);

         return target?.Name?.ToString();
      }

      /// <summary>
      ///   Resolves the named rival for pledge_against by a tolerant, case-insensitive name match among
      ///   every living lord (mirrors <see cref="ResolveSwayTarget" />), excluding the player, the NPC
      ///   speaking, and the NPC's own close kin (a lord is never taught, nor allowed at execution, to
      ///   pledge against their own blood). Null when no name was given or no living lord matches.
      /// </summary>
      public static Hero ResolvePledgeTarget(string name, Hero npc)
      {
         if (string.IsNullOrWhiteSpace(name) || npc == null) return null;

         string query = name.Trim();

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != npc && h.IsLord && !IsCloseKin(npc, h) &&
            h.Name != null && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
      }

      /// <summary>True when <paramref name="candidate" /> is <paramref name="npc" />'s spouse, child, parent, or sibling.</summary>
      public static bool IsCloseKin(Hero npc, Hero candidate)
         => npc != null && candidate != null &&
            (npc.Spouse == candidate || npc.Father == candidate || npc.Mother == candidate ||
             npc.Children != null && npc.Children.Contains(candidate) ||
             npc.Siblings != null && npc.Siblings.Contains(candidate));

      /// <summary>
      ///   Launches the pledge's underlying scheme and records its <see cref="TrackedCommitment" />: the ONE
      ///   shared code path every pledge_against caller runs (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecutePledgeAgainst</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettlePledgeAgainst</c>), factored out here (2026-08-04, Partie
      ///   1's schemes kind) so a lord's pledge behaves identically whichever table struck it, rather than the
      ///   two callers drifting apart over two copies of the same scheme-launch logic. Every gate (kin,
      ///   one-active-commitment, and whichever caller's own extra caveat, e.g. the council's "not a fellow
      ///   seat") must already have passed before this is called; this method only performs the launch, it is
      ///   never itself a guard.
      /// </summary>
      public static void LaunchPledge(Hero promisor, Hero target, NpcProfile promisorProfile, CommitmentBehavior behavior, int today)
      {
         // A calculating lord favours quiet sabotage; others reach for slander. Never a war action - the
         // scheme is the whole of what a pledge sets in motion.
         SchemeKind kind = promisor.GetTraitLevel(DefaultTraits.Calculating) >= 1
            ? SchemeKind.Sabotage
            : SchemeKind.Slander;
         string schemeStatus = SchemeStore.Instance?.ForcePlot(promisor, target, kind);

         behavior.Create(promisor.StringId, target.StringId, today);

         promisorProfile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
            $"Pledged to the player to move against {target.Name}."));

         ModLog.Info($"[ACTION] pledge_against: {promisor.Name} pledged to move against {target.Name} ({kind}). {schemeStatus}");
      }

      // ── arrange_marriage (Partie 1, council currency, 2026-08-04) ───────

      /// <summary>
      ///   The ONE shared code path every arrange_marriage caller runs (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteArrangeMarriage</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleArrangeMarriage</c>), factored out here (2026-08-04,
      ///   mirroring <see cref="LaunchPledge" />'s own extraction for pledge_against) so an arranged match
      ///   behaves identically whichever table struck it: resolves both kin BY NAME (the SAME formula the 1:1
      ///   action always used, <paramref name="playerKinName" /> against the player's own house,
      ///   <paramref name="targetKinName" /> against <paramref name="authorizer" />'s own house, or
      ///   <paramref name="authorizer" /> themselves when named directly), re-checks the authorizer's authority
      ///   over the target kin, defers hard legality (age, kinship, marital status) to the engine's own
      ///   <c>MarriageModel</c>, asks the pure <see cref="ArrangedMatchPolicy" /> for the family's consent, and
      ///   only then applies the real wedding (<see cref="MarriageAction.Apply" />). <paramref name="authorizer" />
      ///   is the party who must consent to giving the target kin in marriage: the 1:1 conversation NPC, or the
      ///   council's own seated allied clan leader (always their house's clan head by construction,
      ///   <c>CouncilRosterResolver.WarCouncilCandidates</c>, so <see cref="MarriageProspectResolver.HasAuthority" />
      ///   holds for any kin of their own clan exactly as the 1:1 case already relied on). Every caller resolves
      ///   nothing itself before calling this: this method is the WHOLE decision, not merely the final apply.
      /// </summary>
      public static ArrangeMarriageVerdict TryArrangeMarriage(string playerKinName, string targetKinName, Hero authorizer)
      {
         Hero a = MarriageProspectResolver.ResolveCandidate(Hero.MainHero, playerKinName);
         Hero b = authorizer != null && string.Equals(authorizer.Name?.ToString(), targetKinName, StringComparison.OrdinalIgnoreCase)
            ? authorizer
            : MarriageProspectResolver.ResolveCandidate(authorizer, targetKinName);

         if (a == null || a == Hero.MainHero || a.Clan != Clan.PlayerClan)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoPlayerKin, a, b, null, null);

         if (b == null)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoTargetKin, a, b, null, null);

         // The authorizer must be able to give B in marriage: their house's own head, or B's parent, or B
         // themselves (mirrors the 1:1 action's own "b != npcHero" guard exactly).
         if (b != authorizer && !MarriageProspectResolver.HasAuthority(authorizer, b))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoAuthority, a, b, null, null);

         // Hard legality (age, kinship, marital status, opposite sex) is the engine's own rule, not ours.
         if (Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(a, b) != true)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotSuitable, a, b, null, null);

         // CR consent: the target family weighs the player's standing. The regard read is the mod's own
         // personal ledger (what the player built in chat), per the two-ledger rule, with the vanilla clan
         // relation as a fallback only when no profile exists yet.
         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(authorizer.StringId)?.ReputationWithPlayer
                      ?? authorizer.GetRelation(Hero.MainHero);

         ArrangedMatchConsent consent = ArrangedMatchPolicy.Decide(new ArrangedMatchFacts(
            targetIsOfAnotherClan: b.Clan != Clan.PlayerClan,
            targetFamilyRegard: regard,
            playerClanTier: Clan.PlayerClan?.Tier ?? 0,
            targetClanTier: b.Clan?.Tier ?? 0));

         if (consent == ArrangedMatchConsent.NotCrossClan)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotCrossClan, a, b, null, null);

         if (consent == ArrangedMatchConsent.RefusedStanding)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.RefusedStanding, a, b, null, null);

         // Captured BEFORE the apply: MarriageAction.Apply moves one spouse into the other's clan per the
         // engine's own MarriageModel, and the council's own report (COUNCIL_ACTIONS.md's Kimi review: "le
         // libelle de la resolution doit dire qui rejoint quelle maison") needs the PRIOR house to say who moved.
         Clan aClanBefore = a.Clan;
         Clan bClanBefore = b.Clan;

         MarriageAction.Apply(a, b);

         return new ArrangeMarriageVerdict(ArrangeMarriageResult.Wed, a, b, aClanBefore, bClanBefore);
      }

      // ── appoint_governor (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both appoint_governor callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteAppointGovernor</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleAppointGovernor</c>), factored out here (2026-08-05, the
      ///   reference slice for the personal-command verbs, mirroring <see cref="LaunchPledge" /> and
      ///   <see cref="TryArrangeMarriage" />'s own extractions) so a governorship behaves identically whichever
      ///   path granted it, rather than the two callers drifting apart over two copies of the same decision. Every
      ///   guard the council executor used to check inline is re-checked HERE, in the same order (the prompt is
      ///   advice, the executor is law): the prospective governor is alive and not held prisoner, the player STILL
      ///   leads their own clan (only its head hands out its governorships), the fief named is a town or castle
      ///   that clan STILL holds (villages carry no Governor slot), and the prospective governor's standing toward
      ///   the player clears the pure <see cref="GovernorAppointmentPolicy" /> floor - decided BEFORE the post is
      ///   ever applied, never a refusal narrated after the deed took effect. Only then is the post applied
      ///   (<see cref="ChangeGovernorAction.Apply" />). <paramref name="appointeeRegard" /> is the mod's OWN
      ///   personal ledger read (<see cref="NpcProfile.ReputationWithPlayer" />), supplied by each caller from its
      ///   own source (the council's stored profile, the 1:1 conversation profile) exactly as the council path
      ///   read it before this extraction. Each caller writes its OWN message off the returned verdict.
      /// </summary>
      public static AppointGovernorVerdict TryAppointGovernor(Hero appointee, int appointeeRegard, string fiefName)
      {
         if (appointee is not {IsAlive: true} || appointee.IsPrisoner)
            return new AppointGovernorVerdict(AppointGovernorResult.AppointeeUnavailable, null);

         // Author's condition (adopted verbatim from the council executor): only a player who still leads his own
         // clan may hand out a governorship. Re-checked here, never trusted from the proposal or the prompt.
         if (Clan.PlayerClan?.Leader != Hero.MainHero)
            return new AppointGovernorVerdict(AppointGovernorResult.PlayerNotClanLeader, null);

         // Town or castle only, and still ours: a village has no Governor slot, and re-checking ownership here
         // (not just at teaching time) is the same bridge-is-law discipline the council path already applies.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == Clan.PlayerClan
            && string.Equals(s.Name?.ToString(), fiefName, StringComparison.OrdinalIgnoreCase));

         if (fief == null)
            return new AppointGovernorVerdict(AppointGovernorResult.NoSuchFief, null);

         // The refusal itself: standing toward the player is the mod's OWN personal regard, the same source the
         // council executor reads, never the vanilla clan relation. Decided BEFORE the post is ever applied.
         GovernorAppointmentVerdict decision = GovernorAppointmentPolicy.Decide(appointeeRegard);

         if (!decision.Accepted)
            return new AppointGovernorVerdict(AppointGovernorResult.Declined, fief);

         ChangeGovernorAction.Apply(fief.Town, appointee);

         return new AppointGovernorVerdict(AppointGovernorResult.Appointed, fief);
      }

      // ── assign_party_role (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both assign_party_role callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteAssignPartyRole</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleAssignPartyRole</c>), factored out here (2026-08-05, the
      ///   party-command batch, mirroring <see cref="TryAppointGovernor" />'s own extraction) so a party role behaves
      ///   identically whichever path granted it. Every guard the council executor used to check inline is re-checked
      ///   HERE, in the same order (the prompt is advice, the executor is law): the member is alive, still rides in
      ///   the player's OWN party (Scout, Engineer, Quartermaster and Surgeon all act on the party the member travels
      ///   with, so a role held outside it is meaningless), and the <paramref name="roleName" /> resolves through the
      ///   pure <see cref="PartyRoleParser" /> to one of the four supported roles (an unrecognised role fails cleanly,
      ///   never guessed). Author's stance (ratified 2026-08-03): instant, no cost, no trust gate, since this is a
      ///   purely INTERNAL attribution (who rides doing what within the player's own party). Each caller writes its
      ///   OWN message off the returned verdict, which carries the resolved <see cref="CouncilPartyRole" />.
      /// </summary>
      public static AssignPartyRoleVerdict TryAssignPartyRole(Hero actor, string roleName)
      {
         if (actor is not {IsAlive: true})
            return new AssignPartyRoleVerdict(AssignPartyRoleResult.ActorUnavailable, CouncilPartyRole.None);

         if (actor.PartyBelongedTo != MobileParty.MainParty)
            return new AssignPartyRoleVerdict(AssignPartyRoleResult.NotInPlayerParty, CouncilPartyRole.None);

         CouncilPartyRole role = PartyRoleParser.Parse(roleName);

         if (role == CouncilPartyRole.None)
            return new AssignPartyRoleVerdict(AssignPartyRoleResult.NoValidRole, CouncilPartyRole.None);

         MobileParty.MainParty.SetHeroPartyRole(actor, ToEnginePartyRole(role));

         return new AssignPartyRoleVerdict(AssignPartyRoleResult.Assigned, role);
      }

      // ── rejoin_party (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both rejoin_party callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteRejoinParty</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleRejoinParty</c>), factored out here (2026-08-05, the
      ///   party-command batch). Every guard the council executor used to check inline is re-checked HERE, in the
      ///   same order (the prompt is advice, the executor is law): the member is alive, not held prisoner (a prisoner
      ///   cannot travel), still a player companion (a clan LORD leads their own party and is out of scope), and
      ///   genuinely OUTSIDE the main party (nothing to rejoin otherwise). A companion who LEADS their own separate
      ///   party is refused rather than silently orphaning whoever they command. A companion currently holding a
      ///   settlement GOVERNOR post has it released first (the inverse of <see cref="TryAppointGovernor" />'s grant,
      ///   or the fief would claim a governor who has physically left it); the verdict reports whether that happened
      ///   and the fief's name so each caller can word its OWN message and log.
      /// </summary>
      public static RejoinPartyVerdict TryRejoinParty(Hero actor)
      {
         if (actor is not {IsAlive: true})
            return new RejoinPartyVerdict(RejoinPartyResult.ActorUnavailable, false, null);

         if (actor.IsPrisoner)
            return new RejoinPartyVerdict(RejoinPartyResult.ActorImprisoned, false, null);

         if (!actor.IsPlayerCompanion)
            return new RejoinPartyVerdict(RejoinPartyResult.NotACompanion, false, null);

         if (actor.PartyBelongedTo == MobileParty.MainParty)
            return new RejoinPartyVerdict(RejoinPartyResult.AlreadyInParty, false, null);

         // A companion who leads their OWN party cannot be lifted out of it without orphaning whoever they command:
         // refuse in v1 rather than silently disbanding a party the player never asked to break up.
         if (actor.PartyBelongedTo?.LeaderHero == actor)
            return new RejoinPartyVerdict(RejoinPartyResult.LeadsOwnCompany, false, null);

         // The inverse of TryAppointGovernor's grant: a governor rejoining the party must first be relieved of the
         // post, or the fief would be left claiming a governor who has physically left it.
         bool wasGovernor = actor.GovernorOf != null;
         string governedFief = wasGovernor ? actor.GovernorOf.Name?.ToString() : null;
         if (wasGovernor)
            ChangeGovernorAction.RemoveGovernorOf(actor);

         AddHeroToPartyAction.Apply(actor, MobileParty.MainParty, false);

         return new RejoinPartyVerdict(RejoinPartyResult.Rejoined, wasGovernor, governedFief);
      }

      // ── dispatch_mission (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both dispatch_mission callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteDispatchMission</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleDispatchMission</c>), factored out here (2026-08-05, the
      ///   party-command batch). REUSES the existing companion-mission system end to end
      ///   (<see cref="CompanionMissionBehavior.Dispatch" />) rather than inventing a new one. Every guard the
      ///   council executor used to check inline is re-checked HERE, in the same order (the prompt is advice, the
      ///   executor is law): the member is alive, still a player companion, still rides in the player's OWN party, a
      ///   mission system exists, they are not already away, and the <paramref name="missionName" /> resolves through
      ///   the pure <see cref="MissionTypeParser" /> to one of the supported errands (unrecognised fails cleanly).
      ///   The DESTINATION is never named here: Dispatch's own resolver picks the nearest suitable town (the
      ///   "system chooses, the model never names a place" design call). Dispatch itself re-validates every one of
      ///   these again internally, so a call that slips past these guards is still caught, never mis-dispatched. The
      ///   verdict carries the resolved <see cref="CouncilMissionType" /> and Dispatch's own message for the caller.
      /// </summary>
      public static DispatchMissionVerdict TryDispatchMission(Hero actor, string missionName)
      {
         if (actor is not {IsAlive: true})
            return new DispatchMissionVerdict(DispatchMissionResult.ActorUnavailable, CouncilMissionType.None, null);

         if (!actor.IsPlayerCompanion)
            return new DispatchMissionVerdict(DispatchMissionResult.NotACompanion, CouncilMissionType.None, null);

         if (actor.PartyBelongedTo != MobileParty.MainParty)
            return new DispatchMissionVerdict(DispatchMissionResult.NotInPlayerParty, CouncilMissionType.None, null);

         CompanionMissionBehavior behavior = CompanionMissionBehavior.Instance;

         if (behavior == null)
            return new DispatchMissionVerdict(DispatchMissionResult.NoMissionSystem, CouncilMissionType.None, null);

         if (behavior.IsAway(actor.StringId))
            return new DispatchMissionVerdict(DispatchMissionResult.AlreadyAway, CouncilMissionType.None, null);

         CouncilMissionType type = MissionTypeParser.Parse(missionName);

         if (type == CouncilMissionType.None)
            return new DispatchMissionVerdict(DispatchMissionResult.NoValidMission, CouncilMissionType.None, null);

         (bool dispatched, string message) = behavior.Dispatch(actor, ToEngineMissionType(type), null);

         return dispatched
            ? new DispatchMissionVerdict(DispatchMissionResult.Dispatched, type, message)
            : new DispatchMissionVerdict(DispatchMissionResult.DispatchFailed, type, message);
      }

      /// <summary>Maps the Logic-side <see cref="CouncilPartyRole" /> to the engine's own <see cref="PartyRole" />. None never reaches here (guarded in <see cref="TryAssignPartyRole" />).</summary>
      private static PartyRole ToEnginePartyRole(CouncilPartyRole role)
      {
         switch (role)
         {
            case CouncilPartyRole.Scout:         return PartyRole.Scout;
            case CouncilPartyRole.Engineer:      return PartyRole.Engineer;
            case CouncilPartyRole.Quartermaster: return PartyRole.Quartermaster;
            case CouncilPartyRole.Surgeon:       return PartyRole.Surgeon;
            default:                             throw new ArgumentOutOfRangeException(nameof(role), role, "CouncilPartyRole.None must be guarded before this is called.");
         }
      }

      /// <summary>Maps the Logic-side <see cref="CouncilMissionType" /> to the mod's own <see cref="CompanionMissionType" />. None never reaches here (guarded in <see cref="TryDispatchMission" />).</summary>
      private static CompanionMissionType ToEngineMissionType(CouncilMissionType type)
      {
         switch (type)
         {
            case CouncilMissionType.GatherNews: return CompanionMissionType.GatherNews;
            case CouncilMissionType.Spy:        return CompanionMissionType.Spy;
            case CouncilMissionType.Steal:      return CompanionMissionType.Steal;
            case CouncilMissionType.Barter:     return CompanionMissionType.Barter;
            case CouncilMissionType.Envoy:      return CompanionMissionType.Envoy;
            default:                            throw new ArgumentOutOfRangeException(nameof(type), type, "CouncilMissionType.None must be guarded before this is called.");
         }
      }

      // ── release_prisoner ─────────────────────────────────────────────────

      /// <summary>
      ///   Up to <see cref="CourtActionPolicy.ReleasePrisonerMaxNamed" /> hero prisoners currently held
      ///   by this NPC's party or by a settlement of their clan — the pool the player could negotiate
      ///   a release from. Empty when they hold none.
      /// </summary>
      public static List<Hero> HeldPrisonersOf(Hero npc)
      {
         var result = new List<Hero>();
         if (npc?.Clan == null) return result;

         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == npc || h == Hero.MainHero || !h.IsLord || !h.IsPrisoner) continue;
            if (!IsHeldByClan(h, npc.Clan)) continue;

            result.Add(h);
            if (result.Count >= CourtActionPolicy.ReleasePrisonerMaxNamed) break;
         }

         return result;
      }

      /// <summary>Re-validates that <paramref name="target" /> is genuinely held by this NPC's clan right now.</summary>
      public static bool CanRelease(Hero target, Hero npc)
         => target?.IsPrisoner == true && npc?.Clan != null && IsHeldByClan(target, npc.Clan);

      /// <summary>
      ///   Resolves the named captive by a tolerant, case-insensitive name match among EVERY hero
      ///   prisoner currently held by this NPC's clan (not only the up-to-3 named in the teaching
      ///   text, so a clan holding more still resolves correctly). Null when no name was given or no
      ///   held prisoner matches.
      /// </summary>
      public static Hero ResolveHeldPrisonerByName(string name, Hero npc)
      {
         if (string.IsNullOrWhiteSpace(name) || npc?.Clan == null) return null;

         string query = name.Trim();

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != npc && h != Hero.MainHero && h is {IsLord: true, IsPrisoner: true} &&
            IsHeldByClan(h, npc.Clan) &&
            h.Name != null && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
      }

      #region private

      /// <summary>
      ///   Sum of healthy (unwounded) rank-and-file troops in a roster — heroes never count toward a loan
      ///   or a gift. Public so the <c>cr.give_troops</c> diagnostic and the self-test seeding can read the
      ///   same figure the eligibility gate itself uses, rather than duplicating the LINQ.
      /// </summary>
      public static int HealthyRegularTroopCount(TroopRoster roster)
      {
         if (roster == null) return 0;

         var total = 0;
         foreach (TroopRosterElement el in roster.GetTroopRoster())
         {
            if (el.Character is not {IsHero: false}) continue;
            total += Math.Max(0, el.Number - el.WoundedNumber);
         }

         return total;
      }

      /// <summary>True when the captor party/settlement holding this prisoner belongs to the given clan.</summary>
      private static bool IsHeldByClan(Hero prisoner, Clan clan)
      {
         PartyBase captor = prisoner.PartyBelongedToAsPrisoner;
         if (captor == null) return false;

         Clan captorClan = captor.LeaderHero?.Clan ?? captor.Settlement?.OwnerClan;

         return captorClan != null && captorClan == clan;
      }

      /// <summary>
      ///   Stamps a court action's cooldown clock in the STRUCTURED store (romance audit M-B6), the source of
      ///   truth memory compaction cannot rewrite. Every court-action verb calls this when it fires, alongside
      ///   the human-readable event it records. Safe to call with a null profile/marker (no-op).
      /// </summary>
      public static void RecordCourtAction(NpcProfile profile, string marker, int day)
      {
         if (profile?.CourtActionCooldowns == null || string.IsNullOrEmpty(marker)) return;

         profile.CourtActionCooldowns[marker] = day;
      }

      /// <summary>
      ///   The most recent in-game day the given court action fired, or null when never, the cooldown clock.
      ///   Romance audit M-B6: reads the STRUCTURED <see cref="NpcProfile.CourtActionCooldowns" /> store first
      ///   (compaction-proof), falling back to scanning the free-text event summaries only for older saves that
      ///   predate the store. Once any action stamps the store, the fragile text scan is never consulted again.
      /// </summary>
      private static int? LastMarkerDay(NpcProfile profile, string marker)
      {
         if (profile == null) return null;

         if (profile.CourtActionCooldowns != null && profile.CourtActionCooldowns.TryGetValue(marker, out int day))
            return day;

         return ScanEventsForMarker(profile, marker);
      }

      /// <summary>Legacy cooldown source: the most recent event summary carrying the marker (pre-M-B6 saves only).</summary>
      private static int? ScanEventsForMarker(NpcProfile profile, string marker)
      {
         if (profile?.Events == null || profile.Events.Count == 0) return null;

         int? latest = null;
         foreach (NotableEvent e in profile.Events)
         {
            if (string.IsNullOrEmpty(e.summary) || e.summary.IndexOf(marker, StringComparison.Ordinal) < 0) continue;
            if (latest == null || e.gameDay > latest.Value) latest = e.gameDay;
         }

         return latest;
      }

      private static int Today() => (int) CampaignTime.Now.ToDays;

      #endregion
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryArrangeMarriage" />, read by every caller to report its own message.</summary>
   public enum ArrangeMarriageResult
   {
      /// <summary>The match was sealed: <see cref="ArrangeMarriageVerdict.A" /> and <see cref="ArrangeMarriageVerdict.B" /> are wed.</summary>
      Wed,

      /// <summary>No unwed kin of the player's own clan answers to the name given.</summary>
      NoPlayerKin,

      /// <summary>The authorizer names no one of their house by the target name given.</summary>
      NoTargetKin,

      /// <summary>The authorizer has no authority to promise the named target kin in marriage.</summary>
      NoAuthority,

      /// <summary>The engine's own MarriageModel refuses the match (age, kinship, marital status, opposite sex).</summary>
      NotSuitable,

      /// <summary>The named target kin is already of the player's own clan (this feature is cross-clan only).</summary>
      NotCrossClan,

      /// <summary>The target family declines: the player's standing does not warrant the match.</summary>
      RefusedStanding
   }

   /// <summary>
   ///   The full verdict of <see cref="CourtActionResolver.TryArrangeMarriage" />: which two heroes were
   ///   resolved (whatever the result, so a caller can name them in a refusal message), and, only on
   ///   <see cref="ArrangeMarriageResult.Wed" />, each one's clan BEFORE the wedding was applied, the "visible
   ///   price" a marriage carries (COUNCIL_ACTIONS.md's Kimi review): comparing <see cref="AClanBefore" />/
   ///   <see cref="BClanBefore" /> against <see cref="A" />.Clan/<see cref="B" />.Clan AFTER the apply tells a
   ///   caller which kin actually moved house.
   /// </summary>
   public readonly struct ArrangeMarriageVerdict
   {
      public ArrangeMarriageVerdict(ArrangeMarriageResult result, Hero a, Hero b, Clan aClanBefore, Clan bClanBefore)
      {
         Result = result;
         A = a;
         B = b;
         AClanBefore = aClanBefore;
         BClanBefore = bClanBefore;
      }

      public ArrangeMarriageResult Result { get; }
      public Hero A { get; }
      public Hero B { get; }
      public Clan AClanBefore { get; }
      public Clan BClanBefore { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryAppointGovernor" />, read by each caller to report its own message.</summary>
   public enum AppointGovernorResult
   {
      /// <summary>The post was applied: <see cref="AppointGovernorVerdict.Fief" /> is now governed by the appointee.</summary>
      Appointed,

      /// <summary>The prospective governor is dead, missing, or held prisoner and cannot take a post.</summary>
      AppointeeUnavailable,

      /// <summary>The player no longer leads their clan, so cannot hand out one of its governorships.</summary>
      PlayerNotClanLeader,

      /// <summary>No town or castle the player's clan still holds answers to the fief name given.</summary>
      NoSuchFief,

      /// <summary>The prospective governor's standing toward the player is below the acceptance floor.</summary>
      Declined
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryAppointGovernor" />: the result and, whenever a real fief
   ///   was resolved (<see cref="AppointGovernorResult.Declined" /> or <see cref="AppointGovernorResult.Appointed" />),
   ///   that <see cref="Fief" /> so the caller can name it in its message. Null <see cref="Fief" /> on the earlier
   ///   failures (no leadership, no such fief, appointee unavailable), where no fief was ever resolved.
   /// </summary>
   public readonly struct AppointGovernorVerdict
   {
      public AppointGovernorVerdict(AppointGovernorResult result, Settlement fief)
      {
         Result = result;
         Fief = fief;
      }

      public AppointGovernorResult Result { get; }
      public Settlement Fief { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryAssignPartyRole" />, read by each caller to report its own message.</summary>
   public enum AssignPartyRoleResult
   {
      /// <summary>The role was set on the party: <see cref="AssignPartyRoleVerdict.Role" /> is what the member now serves as.</summary>
      Assigned,

      /// <summary>The member is dead or missing and cannot take a post.</summary>
      ActorUnavailable,

      /// <summary>The member no longer rides in the player's own party, so a role in it is meaningless.</summary>
      NotInPlayerParty,

      /// <summary>The named role did not resolve to one of the four supported party roles (blank or unrecognised).</summary>
      NoValidRole
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryAssignPartyRole" />: the result and, only on
   ///   <see cref="AssignPartyRoleResult.Assigned" />, the resolved <see cref="Role" /> so the caller can name it.
   ///   <see cref="CouncilPartyRole.None" /> on every failure, where no role was ever set.
   /// </summary>
   public readonly struct AssignPartyRoleVerdict
   {
      public AssignPartyRoleVerdict(AssignPartyRoleResult result, CouncilPartyRole role)
      {
         Result = result;
         Role = role;
      }

      public AssignPartyRoleResult Result { get; }
      public CouncilPartyRole Role { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryRejoinParty" />, read by each caller to report its own message.</summary>
   public enum RejoinPartyResult
   {
      /// <summary>The member was added back to the main party (see <see cref="RejoinPartyVerdict.WasGovernor" /> for the governor-release side effect).</summary>
      Rejoined,

      /// <summary>The member is dead or missing and cannot travel.</summary>
      ActorUnavailable,

      /// <summary>The member is held prisoner and cannot travel to the player.</summary>
      ActorImprisoned,

      /// <summary>The member is no longer one of the player's companions.</summary>
      NotACompanion,

      /// <summary>The member already rides in the player's own party, so there is nothing to rejoin.</summary>
      AlreadyInParty,

      /// <summary>The member leads their own separate company, which the player must disband first.</summary>
      LeadsOwnCompany
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryRejoinParty" />: the result and, only on
   ///   <see cref="RejoinPartyResult.Rejoined" />, whether the member had to be relieved of a governorship first
   ///   (<see cref="WasGovernor" />) and the <see cref="GovernedFief" /> they stepped down from, so the caller can
   ///   word its message and log. False / null on every other result and when they governed nothing.
   /// </summary>
   public readonly struct RejoinPartyVerdict
   {
      public RejoinPartyVerdict(RejoinPartyResult result, bool wasGovernor, string governedFief)
      {
         Result = result;
         WasGovernor = wasGovernor;
         GovernedFief = governedFief;
      }

      public RejoinPartyResult Result { get; }
      public bool WasGovernor { get; }
      public string GovernedFief { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryDispatchMission" />, read by each caller to report its own message.</summary>
   public enum DispatchMissionResult
   {
      /// <summary>The errand was dispatched: <see cref="DispatchMissionVerdict.Message" /> is the behaviour's own report.</summary>
      Dispatched,

      /// <summary>The member is dead or missing and cannot be sent.</summary>
      ActorUnavailable,

      /// <summary>The member is no longer one of the player's companions.</summary>
      NotACompanion,

      /// <summary>The member no longer rides in the player's own party.</summary>
      NotInPlayerParty,

      /// <summary>No companion-mission system is available right now.</summary>
      NoMissionSystem,

      /// <summary>The member is already away on an errand.</summary>
      AlreadyAway,

      /// <summary>The named errand did not resolve to one of the supported mission types (blank or unrecognised).</summary>
      NoValidMission,

      /// <summary>The mission system itself refused the dispatch; <see cref="DispatchMissionVerdict.Message" /> carries its reason.</summary>
      DispatchFailed
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryDispatchMission" />: the result, the resolved
   ///   <see cref="Type" /> (whenever one was parsed), and the companion-mission behaviour's own <see cref="Message" />
   ///   on <see cref="DispatchMissionResult.Dispatched" /> or <see cref="DispatchMissionResult.DispatchFailed" />, so
   ///   the caller reports exactly what the system said. Null <see cref="Message" /> on the earlier guard failures.
   /// </summary>
   public readonly struct DispatchMissionVerdict
   {
      public DispatchMissionVerdict(DispatchMissionResult result, CouncilMissionType type, string message)
      {
         Result = result;
         Type = type;
         Message = message;
      }

      public DispatchMissionResult Result { get; }
      public CouncilMissionType Type { get; }
      public string Message { get; }
   }
}
