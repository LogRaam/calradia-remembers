// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Logic.Prisoners;
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

         int candidate = CourtActionPolicy.InfluenceGrant(new NpcMemoryService.Core.Standing.VanillaRelation(relation));
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

         int candidate = CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = relation, NpcTotalRegularTroops = npcHealthyTroops, PlayerRoom = playerRoom});
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
      public static int MoveLowestTierTroops(MobileParty from, MobileParty to, int count) => MoveMatchingTroops(from, to, count, null);

      /// <summary>
      ///   <see cref="MoveLowestTierTroops" />, restricted to the troops <paramref name="matches" /> accepts (null: any
      ///   regular): the recruit task moves only the recruits asked for (Lordfadooboo, 01/10/2026).
      /// </summary>
      public static int MoveMatchingTroops(MobileParty from, MobileParty to, int count, Func<CharacterObject, bool> matches)
      {
         if (from?.MemberRoster == null || to?.MemberRoster == null || count <= 0) return 0;

         var moved = 0;
         List<TroopRosterElement> candidates = from.MemberRoster.GetTroopRoster()
                                                    .Where(el => el.Character is {IsHero: false} && el.Number - el.WoundedNumber > 0
                                                                 && (matches == null || matches(el.Character)))
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

         int candidate = CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = relation, PlayerHealthyTroops = playerHealthyTroops, NpcRoom = npcRoom});
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

         int candidate = CourtActionPolicy.InfluenceGrant(new NpcMemoryService.Core.Standing.VanillaRelation(relation));
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
         => npc is {IsAlive: true} && !npc.IsPrisoner && npc != Hero.MainHero && HoldsCourt(npc) && profile != null;

      /// <summary>
      ///   A lord or a notable: someone a case can be made to. The one reading for sway_opinion and for a plead_case
      ///   task's target (the engine-facts map found it written twice, 01/10/2026).
      /// </summary>
      public static bool HoldsCourt(Hero h) => h != null && (h.IsLord || h.IsNotable);

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

      /// <summary>
      ///   The game's own ransom valuation of a captive, or the trade policy's fallback when unreadable. The one call to
      ///   the engine's model: the prisoner trade and an envoy's purse both price a man by it (moved here from
      ///   BuyPrisonerVerb on 01/10/2026, so the quests could read it without depending on the verbs).
      /// </summary>
      public static int RansomAnchor(Hero captive)
      {
         try
         {
            int v = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(captive.CharacterObject, Hero.MainHero);

            return v > 0 ? v : PrisonerTradePolicy.FallbackAnchor;
         }
         catch { return PrisonerTradePolicy.FallbackAnchor; }
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
            candidates.Add(new CourtActionPolicy.SwayCandidate(other.StringId, Mapping.PairBondReader.Points(npc, other), IsCloseKin(npc, other), other.MapFaction?.Leader == other));
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

         // v1.34.2 marriage hardening (finding 1): the direct marry action re-applies MarriageGuardPolicy (no
         // captive on either side, no active war between the two houses), but arrange_marriage used to delegate
         // ALL hard legality to the engine's IsCoupleSuitableForMarriage, which does NOT bar a captive kin or
         // kin whose houses are at war. Re-apply the SAME shared pure guard to BOTH parties so an arranged match
         // cannot wed a captive or across a war in one click when the direct action forbids exactly that.
         if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = a.IsPrisoner, PlayerPrisoner = b.IsPrisoner, AtWarWithCandidate = HousesAtWar(a, b)}))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.BlockedByGuard, a, b, null, null);

         // CR consent: the target family weighs the player's standing. The regard read is the mod's own
         // personal ledger (what the player built in chat), per the two-ledger rule, with the vanilla clan
         // relation as a fallback only when no profile exists yet.
         int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(authorizer.StringId)?.ReputationWithPlayer
                      ?? authorizer.GetRelation(Hero.MainHero);

         ArrangedMatchConsent consent = ArrangedMatchPolicy.Decide(new ArrangedMatchFacts {TargetIsOfAnotherClan = b.Clan != Clan.PlayerClan, TargetFamilyRegard = regard, PlayerClanTier = Mapping.StatureReader.Player.ClanTier, TargetClanTier = Mapping.StatureReader.Of(b).ClanTier});

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

      /// <summary>
      ///   A match the player brokers between two other people (fkasad's matchmaker, Gabriel's "real" marriage,
      ///   01/10/2026): the same law as <see cref="TryArrangeMarriage" /> (authority, the engine's legality, no
      ///   captivity, no war between the houses), then <see cref="MatchmakerPolicy" />: her heart first, then her family's
      ///   consent weighed on the suitor's house. With <paramref name="apply" /> the game weds them; without, it only
      ///   answers (cr.test all).
      /// </summary>
      public static ArrangeMarriageVerdict TryArrangeThirdPartyMatch(Hero suitor, Hero bride, Hero authorizer, bool apply)
      {
         if (bride == null || suitor == null) return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoTargetKin, suitor, bride, null, null);
         if (bride != authorizer && !MarriageProspectResolver.HasAuthority(authorizer, bride))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoAuthority, suitor, bride, null, null);
         if (Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(suitor, bride) != true)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotSuitable, suitor, bride, null, null);
         if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = suitor.IsPrisoner, PlayerPrisoner = bride.IsPrisoner, AtWarWithCandidate = HousesAtWar(suitor, bride)}))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.BlockedByGuard, suitor, bride, null, null);

         Hero head = authorizer ?? bride.Clan?.Leader;
         int regard = head == null ? 0 : NpcMemoryCampaignBehavior.Instance?.Store?.Get(head.StringId)?.ReputationWithPlayer ?? head.GetRelation(Hero.MainHero);

         MatchVerdict verdict = MatchmakerPolicy.Decide(new MatchFacts {
            BrideRelationToSuitor = Mapping.PairBondReader.Points(bride, suitor), OfAnotherHouse = bride.Clan != suitor.Clan,
            FamilyRegardForThePlayer = regard, SuitorClanTier = suitor.Clan?.Tier ?? 0, BrideClanTier = bride.Clan?.Tier ?? 0
         });

         switch (verdict)
         {
            case MatchVerdict.NotWonOver: return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotWonOver, suitor, bride, null, null);
            case MatchVerdict.SameHouse: return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotCrossClan, suitor, bride, null, null);
            case MatchVerdict.FamilyRefuses: return new ArrangeMarriageVerdict(ArrangeMarriageResult.RefusedStanding, suitor, bride, null, null);
         }

         Clan suitorClanBefore = suitor.Clan;
         Clan brideClanBefore = bride.Clan;
         if (apply) MarriageAction.Apply(suitor, bride);

         return new ArrangeMarriageVerdict(ArrangeMarriageResult.Wed, suitor, bride, suitorClanBefore, brideClanBefore);
      }

      /// <summary>
      ///   tashmetu (20/10/2026): the groom's side asked the player for the hand of a woman of his house (his daughter, his
      ///   sister), and the player said yes in the chat. The law as for every brokered match, but the family's consent is
      ///   the player's own: the player must still hold the say over her, <paramref name="groomSide" /> over the groom (or be
      ///   him), the game must find them lawful, no war or captivity between the houses, and she must hold him dear enough.
      /// </summary>
      public static ArrangeMarriageVerdict TryGiveHandOfPlayersHouse(Hero suitor, Hero bride, Hero groomSide, bool apply)
      {
         if (bride == null || suitor == null) return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoTargetKin, suitor, bride, null, null);
         if (!MarriageProspectResolver.HasAuthority(Hero.MainHero, bride) || groomSide == null
             || groomSide != suitor && !MarriageProspectResolver.HasAuthority(groomSide, suitor))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NoAuthority, suitor, bride, null, null);
         if (Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(suitor, bride) != true)
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotSuitable, suitor, bride, null, null);
         if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = suitor.IsPrisoner, PlayerPrisoner = bride.IsPrisoner, AtWarWithCandidate = HousesAtWar(suitor, bride)}))
            return new ArrangeMarriageVerdict(ArrangeMarriageResult.BlockedByGuard, suitor, bride, null, null);

         // The family is the player, whose yes was asked in the chat: only her heart and the houses are weighed.
         MatchVerdict verdict = MatchmakerPolicy.Decide(new MatchFacts {
            BrideRelationToSuitor = Mapping.PairBondReader.Points(bride, suitor), OfAnotherHouse = bride.Clan != suitor.Clan,
            FamilyRegardForThePlayer = 100, SuitorClanTier = suitor.Clan?.Tier ?? 0, BrideClanTier = suitor.Clan?.Tier ?? 0
         });
         if (verdict == MatchVerdict.NotWonOver) return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotWonOver, suitor, bride, null, null);
         if (verdict == MatchVerdict.SameHouse) return new ArrangeMarriageVerdict(ArrangeMarriageResult.NotCrossClan, suitor, bride, null, null);

         Clan suitorClanBefore = suitor.Clan;
         Clan brideClanBefore = bride.Clan;
         if (apply) MarriageAction.Apply(suitor, bride);

         return new ArrangeMarriageVerdict(ArrangeMarriageResult.Wed, suitor, bride, suitorClanBefore, brideClanBefore);
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

         // v1.34.2 governance hardening (finding 1): the post must be VACANT. Matching a fief by name alone would
         // otherwise silently EVICT its sitting governor when the player mis-named the target, so refuse rather
         // than unseat someone the player never meant to touch. The offer only ever names vacant fiefs; this is
         // the commit re-check (the prompt is advice, the executor is law).
         if (fief.Town?.Governor != null)
            return new AppointGovernorVerdict(AppointGovernorResult.PostOccupied, fief);

         // v1.34.2 governance hardening (finding 2): one governor, one seat. A hero already governing another fief
         // cannot be double-posted, or the first fief would silently lose its governor to the second.
         if (appointee.GovernorOf != null)
            return new AppointGovernorVerdict(AppointGovernorResult.AppointeeAlreadyGoverns, fief);

         // v1.34.2 governance hardening (finding 2): a companion away on a companion-mission errand cannot be
         // seated in absentia. Same IsAway fact TryDispatchMission and the expel guard already consult.
         if (CompanionMissionBehavior.Instance?.IsAway(appointee.StringId) == true)
            return new AppointGovernorVerdict(AppointGovernorResult.AppointeeAway, fief);

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
      ///   "system chooses, the model never names a place" design call). <paramref name="about" /> is the OPTIONAL
      ///   topic the player named (a faction, culture, town, or lord to bring back word of), threaded to Dispatch's
      ///   own <c>about</c> arg; null when the player left it open. This is the one capability the older gather_news
      ///   verb had that dispatch_mission lacked before the two were merged (2026-08-05); the council caller passes
      ///   no topic (its default), so it compiles and behaves unchanged. Dispatch itself re-validates every one of
      ///   these guards again internally, so a call that slips past them is still caught, never mis-dispatched. The
      ///   companion set matches <c>CompanionMissionBehavior.IsUsableCompanion</c> (a player companion OR a member of
      ///   the player's own clan), the SAME population the old gather_news gate served, so folding gather_news in
      ///   dropped no one. The verdict carries the resolved <see cref="CouncilMissionType" /> and Dispatch's own
      ///   message for the caller.
      /// </summary>
      public static DispatchMissionVerdict TryDispatchMission(Hero actor, string missionName, string about = null, string settlementName = null)
      {
         if (actor is not {IsAlive: true})
            return new DispatchMissionVerdict(DispatchMissionResult.ActorUnavailable, CouncilMissionType.None, null);

         if (!(actor.IsPlayerCompanion || actor.Clan == Clan.PlayerClan))
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

         // jaserms1 (16/10/2026): a patrol of a place the player NAMED, with the player's own men (Gabriel's ruling).
         // The place must match a real settlement exactly; the game never picks one for a patrol.
         if (type == CouncilMissionType.Patrol)
         {
            Settlement place = PatrolPlaceNamed(settlementName);
            if (place == null) return new DispatchMissionVerdict(DispatchMissionResult.NoSuchPlace, type, null);
            if (CompanionPatrolBehavior.Instance == null) return new DispatchMissionVerdict(DispatchMissionResult.NoMissionSystem, type, null);

            (bool accepted, string order) = CompanionPatrolBehavior.Instance.RequestPatrol(actor, place, null);

            return accepted
               ? new DispatchMissionVerdict(DispatchMissionResult.PatrolOrdered, type, order)
               : new DispatchMissionVerdict(DispatchMissionResult.DispatchFailed, type, order);
         }

         string topic = string.IsNullOrWhiteSpace(about) ? null : about.Trim();
         (bool dispatched, string message) = behavior.Dispatch(actor, ToEngineMissionType(type), topic);

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

      /// <summary>
      ///   The settlement a patrol order named: an exact name (case and surrounding spaces aside), hideouts excluded;
      ///   the nearest to the player when two share the name. Null when nothing matches, so the order is refused
      ///   rather than sent somewhere the player never named.
      /// </summary>
      private static Settlement PatrolPlaceNamed(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;
         string wanted = name.Trim();
         TaleWorlds.Library.Vec2 at = MobileParty.MainParty?.GetPosition2D ?? TaleWorlds.Library.Vec2.Zero;

         return Settlement.All.Where(s => s != null && !s.IsHideout && string.Equals(s.Name?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                          .OrderBy(s => s.GetPosition2D.Distance(at))
                          .FirstOrDefault();
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

      /// <summary>
      ///   Releases whatever governor is currently seated on <paramref name="fief" /> before its ownership passes
      ///   to another house (v1.34.2 governance hardening, finding 3): factored out of <see cref="TryGrantFief" />
      ///   so the grant, the revoke (<see cref="TryRevokeFief" />), and the quest fief payout
      ///   (<c>BannerlordGameStateBridge.GrantFiefReward</c>) all land the same release rather than a spoliated or
      ///   ceded hero keeping the crown fief they govern. A released PLAYER companion (only ever the case on a
      ///   grant of the player's own fief) is sent back to the main party so they are never stranded; a governor of
      ///   any other house is simply relieved of the post. Total and null-safe: a null fief or an unseated post is
      ///   a no-op, so every caller may invoke it unconditionally just before the ownership transfer.
      /// </summary>
      internal static void ReleaseSittingGovernor(Settlement fief)
      {
         Hero formerGovernor = fief?.Town?.Governor;
         if (formerGovernor == null) return;

         ChangeGovernorAction.RemoveGovernorOf(formerGovernor);

         if (formerGovernor.IsPlayerCompanion && formerGovernor.PartyBelongedTo != MobileParty.MainParty)
            AddHeroToPartyAction.Apply(formerGovernor, MobileParty.MainParty, false);
      }

      // ── grant_fief (suzerain-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both grant_fief callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteGrantFief</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleGrantFief</c>), factored out here (2026-08-05, the
      ///   suzerain-command batch, mirroring <see cref="TryAppointGovernor" />'s own extraction) so a fief grant
      ///   behaves identically whichever path granted it. Every guard the council executor used to check inline is
      ///   re-checked HERE, in the same order (the prompt is advice, the executor is law): the recipient is alive and
      ///   not held prisoner, they belong to a SEPARATE house (a fief is CLAN-owned in vanilla, so a grant is
      ///   inherently cross-clan; the player's own clan cannot grant a fief to itself), the player STILL rules a
      ///   kingdom AND the named fief is a town or castle their own clan STILL holds - the pure
      ///   <see cref="FiefTransferPolicy.CanGrant" /> gate. A governor in post on the ceded fief is released first
      ///   (a released PLAYER companion is sent back to the main party rather than stranded in now-foreign land),
      ///   then the fief passes via <see cref="ChangeOwnerOfSettlementAction.ApplyByKingDecision" /> and the FIXED
      ///   influence cost (<see cref="FiefTransferPolicy.GrantInfluenceCost" />, "on ne veut pas que ce soit facile")
      ///   is charged to the player's clan. Each caller writes its OWN message off the returned verdict.
      /// </summary>
      public static GrantFiefVerdict TryGrantFief(Hero recipient, string fiefName)
      {
         if (recipient is not {IsAlive: true} || recipient.IsPrisoner)
            return new GrantFiefVerdict(GrantFiefResult.RecipientUnavailable, null);

         // Defensive, adopted verbatim from the council executor: a fief passes to another HOUSE, so the recipient
         // must be a genuine OTHER clan (the player's own clan cannot grant a crown fief to itself).
         if (recipient.Clan == null || recipient.Clan == Clan.PlayerClan)
            return new GrantFiefVerdict(GrantFiefResult.RecipientNotSeparateHouse, null);

         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         // v1.34.1 hotfix invariant: a crown fief passes only to a lord who owes THIS crown fealty. The recipient's
         // clan must be a vassal of the player's OWN kingdom, never a foreign or enemy house. Re-checked here, at
         // commit, regardless of what the prompt taught (the prompt is advice, the executor is law).
         bool recipientInPlayerKingdom = recipient.Clan.Kingdom != null && recipient.Clan.Kingdom == Clan.PlayerClan?.Kingdom;

         // Town or castle only (a village is not cedable on its own), and STILL a crown fief the player's own clan
         // holds right now: a fief already given or lost between the pledge/prompt and here must refuse rather than
         // conjure a transfer from nothing.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == Clan.PlayerClan
            && string.Equals(s.Name?.ToString(), fiefName, StringComparison.OrdinalIgnoreCase));

         if (!FiefTransferPolicy.CanGrant(new FiefTransferPolicy.GrantFacts {PlayerIsKingdomLeader = playerIsKingdomLeader, RecipientInPlayerKingdom = recipientInPlayerKingdom, FiefOwnedByPlayerClan = fief != null}))
            return new GrantFiefVerdict(
               !playerIsKingdomLeader ? GrantFiefResult.PlayerNotKingdomLeader
                  : !recipientInPlayerKingdom ? GrantFiefResult.RecipientNotVassal
                  : GrantFiefResult.NoSuchFief, null);

         // Kimi review (COUNCIL_ACTIONS.md, grant_fief cas limites): a fief ceded while a governor is in post would
         // otherwise leave that governor a wandering hero in what is now foreign land. Release the post first
         // (shared helper, also used by TryRevokeFief and the quest fief payout so no owner transfer strands a
         // governor over land that is no longer theirs), and send a released PLAYER companion back to the main
         // party so they are never simply stranded.
         ReleaseSittingGovernor(fief);

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(recipient, fief);

         // Author's condition: "on ne veut pas que ce soit facile". A fixed, unconditional influence cost, never
         // gated on the player's current reserve: the sovereign's OWN prerogative to grant is never blocked by a
         // thin treasury, but it is never a free favour either.
         if (Clan.PlayerClan != null) ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -FiefTransferPolicy.GrantInfluenceCost);

         return new GrantFiefVerdict(GrantFiefResult.Granted, fief);
      }

      // ── revoke_fief (suzerain-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both revoke_fief callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteRevokeFief</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleRevokeFief</c>), factored out here (2026-08-05, the
      ///   suzerain-command batch, grant_fief's grim mirror). Every guard the council executor used to check inline is
      ///   re-checked HERE, in the same order (the prompt is advice, the executor is law): the vassal is alive, they
      ///   belong to a SEPARATE house (a clan's own fiefs already belong to it, so there is nothing to revoke from
      ///   one's own clan), the player STILL rules a kingdom AND the named fief is a town or castle the SPOLIATED
      ///   vassal's own clan STILL holds - the pure <see cref="FiefTransferPolicy.CanRevoke" /> gate. On success the
      ///   fief returns to the crown via <see cref="ChangeOwnerOfSettlementAction.ApplyByKingDecision" />, and the
      ///   council's grave side effects fire HERE so both paths land them identically: a MAXIMUM
      ///   <see cref="GrudgeSource.FiefRevoked" /> grudge through the REAL gate
      ///   (<see cref="GrudgeBehavior.TryPlant" />, anti-noise guardrails, per-holder cap, dated by
      ///   <paramref name="gameDay" />) plus a real relation hit (<see cref="FiefTransferPolicy.RevokeRelationHit" />).
      ///   Each caller writes its OWN message off the returned verdict.
      /// </summary>
      public static RevokeFiefVerdict TryRevokeFief(Hero vassal, string fiefName, int gameDay)
      {
         if (vassal is not {IsAlive: true})
            return new RevokeFiefVerdict(RevokeFiefResult.VassalUnavailable, null);

         if (vassal.Clan == null || vassal.Clan == Clan.PlayerClan)
            return new RevokeFiefVerdict(RevokeFiefResult.VassalNotSeparateHouse, null);

         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         Clan spoliatedClan = vassal.Clan;

         // v1.34.1 hotfix invariant: the crown confiscates only from its OWN sworn lords. The target's clan must be
         // a vassal of the player's own kingdom, never an enemy house whose fief would otherwise be annexed by
         // decree, mid-war, with no siege. Re-checked here, at commit (the prompt is advice, the executor is law).
         bool targetInPlayerKingdom = spoliatedClan.Kingdom != null && spoliatedClan.Kingdom == Clan.PlayerClan?.Kingdom;

         // STILL owned by the SPOLIATED vassal's own clan right now: naming a fief they never held, or already lost,
         // would confiscate nothing.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == spoliatedClan
            && string.Equals(s.Name?.ToString(), fiefName, StringComparison.OrdinalIgnoreCase));

         if (!FiefTransferPolicy.CanRevoke(new FiefTransferPolicy.RevokeFacts {PlayerIsKingdomLeader = playerIsKingdomLeader, TargetInPlayerKingdom = targetInPlayerKingdom, FiefOwnedBySpoliatedClan = fief != null}))
            return new RevokeFiefVerdict(
               !playerIsKingdomLeader ? RevokeFiefResult.PlayerNotKingdomLeader
                  : !targetInPlayerKingdom ? RevokeFiefResult.VassalNotInKingdom
                  : RevokeFiefResult.NoSuchFief, null);

         // v1.34.2 governance hardening (finding 3): release the spoliated vassal's own governor before the fief
         // returns to the crown, or that hostile hero would keep governing a fief that is no longer theirs (the
         // grant path already did this; the revoke and the quest payout did not).
         ReleaseSittingGovernor(fief);

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(Hero.MainHero, fief);

         // Author's condition: one of the gravest punishments the table can hand down. Planted through the REAL
         // grudge gate (anti-noise guardrails, per-holder cap) rather than force-inserted, so a maximum magnitude
         // still respects the same admission rule every other grudge in the game does.
         GrudgeBehavior.Instance?.TryPlant(vassal, Hero.MainHero, GrudgeSource.FiefRevoked,
            $"stripped of {fief.Name} by the crown's own decision", FiefTransferPolicy.RevokeGrudgeMagnitude,
            knownToPlayer: true, gameDay);

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(vassal, Hero.MainHero, FiefTransferPolicy.RevokeRelationHit, false);
         Mapping.DeedRegardApplier.Apply(vassal, Logic.PlayerDeed.RevokedFief); // the personal regard too (Gabriel 23/10)

         return new RevokeFiefVerdict(RevokeFiefResult.Revoked, fief);
      }

      // ── expel_from_clan (suzerain-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both expel_from_clan callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteExpelFromClan</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleExpelFromClan</c>), factored out here (2026-08-05, the
      ///   suzerain-command batch). Every guard the council executor used to check inline is re-checked HERE, in the
      ///   same order (the prompt is advice, the executor is law): the companion is alive, the pure
      ///   <see cref="ExpulsionPolicy.CanExpel" /> gate holds (the player STILL leads their clan, the target is a
      ///   live COMPANION of that same clan, and is not the player's own spouse - that bond has its own dissolution
      ///   channel), and they are not AWAY on a companion-mission errand (refused rather than expelled out from under
      ///   an in-flight errand). On success the companion is cast out via
      ///   <see cref="RemoveCompanionAction.ApplyByFire" /> (they become a Fugitive, ApplyByFire also releasing any
      ///   held governorship internally; the explicit capture below only lets the caller name the vacated fief), and
      ///   the council's side effects fire HERE so both paths land them identically: a
      ///   <see cref="GrudgeSource.Expelled" /> grudge through the REAL gate (<see cref="GrudgeBehavior.TryPlant" />,
      ///   dated by <paramref name="gameDay" />) plus a real relation hit (<see cref="ExpulsionPolicy.RelationHit" />).
      ///   The verdict reports whether they governed a fief and its name so each caller can word its own message.
      /// </summary>
      public static ExpelFromClanVerdict TryExpelFromClan(Hero companion, int gameDay)
      {
         if (companion is not {IsAlive: true})
            return new ExpelFromClanVerdict(ExpelFromClanResult.ActorUnavailable, false, null);

         bool playerIsClanLeader = Clan.PlayerClan?.Leader == Hero.MainHero;
         bool isSpouse = companion == Hero.MainHero?.Spouse;

         if (!ExpulsionPolicy.CanExpel(new ExpulsionPolicy.ExpulsionFacts {PlayerIsClanLeader = playerIsClanLeader, TargetIsCompanion = companion.IsPlayerCompanion, TargetIsSpouse = isSpouse, TargetAlive = companion.IsAlive}))
            return new ExpelFromClanVerdict(
               !playerIsClanLeader ? ExpelFromClanResult.PlayerNotClanLeader
                  : !companion.IsPlayerCompanion ? ExpelFromClanResult.NotACompanion
                     : ExpelFromClanResult.IsSpouse,
               false, null);

         // Mechanical guard beyond the pure policy: a companion AWAY on a companion-mission errand is refused
         // outright rather than expelled out from under an in-flight errand, mirroring TryDispatchMission's own
         // re-check of the same state.
         if (CompanionMissionBehavior.Instance != null && CompanionMissionBehavior.Instance.IsAway(companion.StringId))
            return new ExpelFromClanVerdict(ExpelFromClanResult.AwayOnErrand, false, null);

         bool wasGovernor = companion.GovernorOf != null;
         string governedFief = wasGovernor ? companion.GovernorOf.Name?.ToString() : null;

         RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, companion);

         GrudgeBehavior.Instance?.TryPlant(companion, Hero.MainHero, GrudgeSource.Expelled,
            "cast out of the clan by its own chief", ExpulsionPolicy.GrudgeMagnitude,
            knownToPlayer: true, gameDay);

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(companion, Hero.MainHero, ExpulsionPolicy.RelationHit, false);
         Mapping.DeedRegardApplier.Apply(companion, Logic.PlayerDeed.Expelled); // the personal regard too (Gabriel 23/10)

         return new ExpelFromClanVerdict(ExpelFromClanResult.Expelled, wasGovernor, governedFief);
      }

      // ── grant_stipend (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both grant_stipend callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteGrantStipend</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleGrantStipend</c>), factored out here (2026-08-05, mirroring
      ///   <see cref="TryAppointGovernor" />'s own extraction) so a stipend behaves identically whichever path
      ///   granted it, and neither path can ever double-escrow a second tranche on top of one already running. Every
      ///   guard the council executor used to check inline is re-checked HERE, in the same order (the prompt is
      ///   advice, the executor is law): the recipient is alive, still one of the player's OWN clan companions, the
      ///   escrow ledger (<see cref="CouncilStipendBehavior" />) exists, and NO stipend is already outstanding for
      ///   them - the ONE new invariant this extraction adds to BOTH paths, so a fresh 30-day tranche is never funded
      ///   on top of a running one (re-checked against LIVE persisted state, never trusted from the offer). Only then
      ///   does <see cref="CouncilStipendBehavior.TryStart" /> re-clamp the amount to <see cref="StipendPolicy" />'s
      ///   [50, 500] range and re-check the player's purse against the FULL tranche before deducting it up front. Each
      ///   caller writes its OWN message off the returned verdict, which carries the clamped daily amount.
      /// </summary>
      public static GrantStipendVerdict TryGrantStipend(Hero recipient, int amountPerDay)
      {
         if (recipient is not {IsAlive: true})
            return new GrantStipendVerdict(GrantStipendResult.RecipientUnavailable, 0, null);

         if (!recipient.IsPlayerCompanion)
            return new GrantStipendVerdict(GrantStipendResult.NotPlayerCompanion, 0, null);

         CouncilStipendBehavior behavior = CouncilStipendBehavior.Instance;

         if (behavior == null)
            return new GrantStipendVerdict(GrantStipendResult.NoLedger, 0, null);

         // The ONE invariant this shared core ADDS to both paths: never fund a second concurrent stipend on top of
         // one already running for this hero (a double-escrow bug the 1:1 path could otherwise introduce, since the
         // player can grant a stipend both here and at a council). Re-checked against the LIVE persisted escrow.
         if (behavior.HasActive(recipient.StringId))
            return new GrantStipendVerdict(GrantStipendResult.AlreadyActive, 0, null);

         if (!behavior.TryStart(recipient, amountPerDay, out string failReason))
            return new GrantStipendVerdict(GrantStipendResult.CouldNotFund, 0, failReason);

         return new GrantStipendVerdict(GrantStipendResult.Started, StipendPolicy.ClampAmountPerDay(amountPerDay), null);
      }

      // ── swear_oath (personal-command verb, council + 1:1, 2026-08-05) ──

      /// <summary>
      ///   The ONE shared code path both swear_oath callers run (the 1:1 action,
      ///   <c>BannerlordGameStateBridge.ExecuteSwearOath</c>, and the council's own resolution,
      ///   <c>CalradiaRemembers.Chat.CouncilLift.SettleSwearOath</c>), factored out here (2026-08-05, mirroring
      ///   <see cref="TryAppointGovernor" />'s own extraction) so a sworn oath behaves identically whichever path
      ///   recorded it. Every guard the council executor used to check inline is re-checked HERE, in the same order
      ///   (the prompt is advice, the executor is law): the swearer is alive, not held prisoner, not the player, the
      ///   commitment ledger exists, they carry NO oath already outstanding (one vow at a time,
      ///   <see cref="CommitmentBehavior.HasActive" />), the <paramref name="oathKindKey" /> resolves through the
      ///   whitelist (<see cref="OathKindParser" />, so an unverifiable kind fails cleanly), and that kind's own
      ///   payload validates (a positive clamped sum for pay_gold; a swearer who leads a faction of their own and a
      ///   real OTHER faction named for keep_peace; nothing for protect). Only then is the oath recorded through the
      ///   SAME generalized <see cref="CommitmentBehavior.Create" /> engine, carrying <paramref name="witnessIds" />
      ///   (the OTHER seats present at a council table, or null for a private 1:1 oath, which costs less to break).
      ///   Each caller writes its OWN sworn line off the returned verdict, which carries the resolved kind, the
      ///   clamped promised amount (pay_gold), and the target faction's display name (keep_peace).
      /// </summary>
      public static SwearOathVerdict TrySwearOath(Hero swearer, string oathKindKey, string targetValue, int gameDay, List<string> witnessIds)
      {
         if (swearer is not {IsAlive: true} || swearer.IsPrisoner || swearer == Hero.MainHero)
            return new SwearOathVerdict(SwearOathResult.SwearerUnavailable, default, null, null);

         CommitmentBehavior behavior = CommitmentBehavior.Instance;

         if (behavior == null)
            return new SwearOathVerdict(SwearOathResult.NoLedger, default, null, null);

         if (behavior.HasActive(swearer.StringId))
            return new SwearOathVerdict(SwearOathResult.AlreadyCommitted, default, null, null);

         if (!OathKindParser.TryParse(oathKindKey, out CommitmentKind kind))
            return new SwearOathVerdict(SwearOathResult.UnknownKind, default, null, null);

         string targetId = null;
         int? promisedAmount = null;
         string targetFactionName = null;

         switch (kind)
         {
            case CommitmentKind.PayGold:
               if (!int.TryParse(targetValue, out int proposed) || CommitmentPolicy.ClampPromisedAmount(proposed) <= 0)
                  return new SwearOathVerdict(SwearOathResult.InvalidAmount, kind, null, null);

               promisedAmount = CommitmentPolicy.ClampPromisedAmount(proposed);

               break;

            case CommitmentKind.KeepPeace:
               bool leadsKingdom = swearer.Clan?.Kingdom != null && swearer.Clan.Kingdom.Leader == swearer;
               bool leadsIndependentClan = swearer.Clan != null && swearer.Clan.Kingdom == null && swearer.Clan.Leader == swearer;

               if (!leadsKingdom && !leadsIndependentClan)
                  return new SwearOathVerdict(SwearOathResult.CannotSwearPeace, kind, null, null);

               IFaction targetFaction = CalradiaRemembers.Chat.CouncilLift.ResolveFactionByName(targetValue);

               if (targetFaction == null)
                  return new SwearOathVerdict(SwearOathResult.NoSuchFaction, kind, null, null);

               if (targetFaction == swearer.MapFaction)
                  return new SwearOathVerdict(SwearOathResult.OwnFaction, kind, null, null);

               targetId = targetFaction.StringId;
               targetFactionName = targetFaction.Name?.ToString();

               break;

            case CommitmentKind.Protect:
               break;

            default:
               return new SwearOathVerdict(SwearOathResult.UnknownKind, default, null, null);
         }

         behavior.Create(swearer.StringId, targetId, gameDay, kind, promisedAmount, witnessIds);

         return new SwearOathVerdict(SwearOathResult.Sworn, kind, promisedAmount, targetFactionName);
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
            if (h == npc || !h.IsLord || !h.IsPrisoner) continue;
            // A member of the player's own house is never merchandise: exclude the player AND player-clan kin.
            if (CourtActionPolicy.IsPlayerHousehold(h == Hero.MainHero, h.Clan == Clan.PlayerClan)) continue;
            if (!IsHeldByClan(h, npc.Clan)) continue;

            result.Add(h);
            if (result.Count >= CourtActionPolicy.ReleasePrisonerMaxNamed) break;
         }

         return result;
      }

      /// <summary>
      ///   Re-validates that <paramref name="target" /> is genuinely held by this NPC's clan right now AND is
      ///   not a member of the player's own house (a captured relative is never merchandise the player buys
      ///   back into their own cage).
      /// </summary>
      public static bool CanRelease(Hero target, Hero npc)
         => target?.IsPrisoner == true && npc?.Clan != null && IsHeldByClan(target, npc.Clan)
            && !CourtActionPolicy.IsPlayerHousehold(target == Hero.MainHero, target.Clan == Clan.PlayerClan);

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
            h != npc && !CourtActionPolicy.IsPlayerHousehold(h == Hero.MainHero, h.Clan == Clan.PlayerClan) &&
            h is {IsLord: true, IsPrisoner: true} &&
            IsHeldByClan(h, npc.Clan) &&
            h.Name != null && Logic.Prisoners.CaptiveNameMatch.Matches(h.Name.ToString(), query));
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
      /// <summary>
      ///   Every hero captive this NPC's clan holds that an EXCHANGE may put on the table: lords and companions,
      ///   INCLUDING the player's own people. Unlike <see cref="HeldPrisonersOf" />, which keeps the player's household
      ///   out of a sale ("never merchandise"), getting the player's own companion back is exactly what an exchange is
      ///   for (fkasad, 25/09/2026). The player himself never.
      /// </summary>
      public static List<Hero> ExchangeablePrisonersHeldBy(Hero npc)
      {
         var result = new List<Hero>();
         if (npc?.Clan == null) return result;

         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == npc || h == Hero.MainHero || !h.IsPrisoner) continue;
            if (!h.IsLord && h.CompanionOf == null && !h.IsWanderer) continue;
            if (!IsHeldByClan(h, npc.Clan)) continue;

            result.Add(h);
         }

         return result;
      }

      /// <summary>Tolerant name match among <see cref="ExchangeablePrisonersHeldBy" />. Null when nothing matches.</summary>
      public static Hero ResolveExchangeableHeldByName(string name, Hero npc)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         string query = name.Trim();

         return ExchangeablePrisonersHeldBy(npc).FirstOrDefault(h =>
            h.Name != null && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
      }

      private static bool IsHeldByClan(Hero prisoner, Clan clan)
      {
         PartyBase captor = prisoner.PartyBelongedToAsPrisoner;
         if (captor == null) return false;

         Clan captorClan = captor.LeaderHero?.Clan ?? captor.Settlement?.OwnerClan;

         return captorClan != null && captorClan == clan;
      }

      /// <summary>
      ///   True when this prisoner is currently held by the player's OWN clan, whether in a party or in a fief
      ///   dungeon. Public so the RELATIONSHIPS block can tell a captive's kin, truthfully, that the person
      ///   before them is the captor (a player report: an NPC denied a captivity the player had caused).
      /// </summary>
      public static bool IsHeldByPlayerClan(Hero prisoner)
         => prisoner is {IsPrisoner: true} && Clan.PlayerClan != null && IsHeldByClan(prisoner, Clan.PlayerClan);

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

      /// <summary>
      ///   Whether the two prospective spouses' houses are on opposite sides of an active war (finding 1's war
      ///   half). Mirrors <c>AssemblyInvitationFactsBuilder.AtWar</c>: read each hero's map faction and ask the
      ///   engine's own <see cref="IFaction.IsAtWarWith" />. False when either faction is unresolved (no war we
      ///   can see is no war to block on).
      /// </summary>
      private static bool HousesAtWar(Hero a, Hero b)
      {
         IFaction fa = a?.MapFaction;
         IFaction fb = b?.MapFaction;

         return fa != null && fb != null && fa.IsAtWarWith(fb);
      }

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

      /// <summary>For a brokered match: she does not yet hold the suitor a friend; she must be won over first.</summary>
      NotWonOver,

      /// <summary>The named target kin is already of the player's own clan (this feature is cross-clan only).</summary>
      NotCrossClan,

      /// <summary>The target family declines: the player's standing does not warrant the match.</summary>
      RefusedStanding,

      /// <summary>
      ///   The captivity/war guard (<see cref="CalradiaRemembers.Logic.Marriage.MarriageGuardPolicy.MarriageAllowed" />) forbids the match:
      ///   one of the pair is held captive, or their two houses are on opposite sides of an active war. The direct
      ///   marry action already refuses exactly these states; arrange_marriage now honours the same law (v1.34.2).
      /// </summary>
      BlockedByGuard
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

      /// <summary>The named fief already has a governor seated; appointing here would silently evict them (v1.34.2 governance hardening, finding 1).</summary>
      PostOccupied,

      /// <summary>The prospective governor already governs another fief; one hero cannot hold two seats (v1.34.2 governance hardening, finding 2).</summary>
      AppointeeAlreadyGoverns,

      /// <summary>The prospective governor is away on a companion-mission errand and cannot be seated in absentia (v1.34.2 governance hardening, finding 2).</summary>
      AppointeeAway,

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
      DispatchFailed,

      /// <summary>A patrol order was taken; the player chooses the men on the troop screen back on the map (nothing has left yet).</summary>
      PatrolOrdered,

      /// <summary>A patrol named no place, or none that exists by that name.</summary>
      NoSuchPlace
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

   /// <summary>The outcome of <see cref="CourtActionResolver.TryGrantFief" />, read by each caller to report its own message.</summary>
   public enum GrantFiefResult
   {
      /// <summary>The fief passed to the recipient's house: <see cref="GrantFiefVerdict.Fief" /> is the settlement granted.</summary>
      Granted,

      /// <summary>The recipient is dead, missing, or held prisoner and cannot receive a fief.</summary>
      RecipientUnavailable,

      /// <summary>The recipient belongs to the player's own house; a crown fief is granted across houses, not to one's own clan.</summary>
      RecipientNotSeparateHouse,

      /// <summary>The recipient's clan is not a vassal of the player's own kingdom; a crown fief passes only to a lord sworn to this realm (v1.34.1 hotfix invariant).</summary>
      RecipientNotVassal,

      /// <summary>The player no longer rules a kingdom, so has no crown fief to grant.</summary>
      PlayerNotKingdomLeader,

      /// <summary>No town or castle the player's clan still holds answers to the fief name given.</summary>
      NoSuchFief
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryGrantFief" />: the result and, only on
   ///   <see cref="GrantFiefResult.Granted" />, the resolved <see cref="Fief" /> so the caller can name it. Null
   ///   <see cref="Fief" /> on every failure, where no fief was ever resolved or transferred.
   /// </summary>
   public readonly struct GrantFiefVerdict
   {
      public GrantFiefVerdict(GrantFiefResult result, Settlement fief)
      {
         Result = result;
         Fief = fief;
      }

      public GrantFiefResult Result { get; }
      public Settlement Fief { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryRevokeFief" />, read by each caller to report its own message.</summary>
   public enum RevokeFiefResult
   {
      /// <summary>The fief was stripped back to the crown: <see cref="RevokeFiefVerdict.Fief" /> is the settlement revoked.</summary>
      Revoked,

      /// <summary>The vassal is dead or missing and no fief could be revoked from them.</summary>
      VassalUnavailable,

      /// <summary>The vassal holds no separate house whose fief could be revoked (a clan's own fiefs already belong to it).</summary>
      VassalNotSeparateHouse,

      /// <summary>The vassal's clan is not a vassal of the player's own kingdom; the crown confiscates only from its own sworn lords, never an enemy holding (v1.34.1 hotfix invariant).</summary>
      VassalNotInKingdom,

      /// <summary>The player no longer rules a kingdom, so cannot revoke a fief in its name.</summary>
      PlayerNotKingdomLeader,

      /// <summary>No town or castle the vassal's own clan still holds answers to the fief name given.</summary>
      NoSuchFief
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryRevokeFief" />: the result and, only on
   ///   <see cref="RevokeFiefResult.Revoked" />, the resolved <see cref="Fief" /> so the caller can name it. Null
   ///   <see cref="Fief" /> on every failure, where no fief was ever resolved or confiscated.
   /// </summary>
   public readonly struct RevokeFiefVerdict
   {
      public RevokeFiefVerdict(RevokeFiefResult result, Settlement fief)
      {
         Result = result;
         Fief = fief;
      }

      public RevokeFiefResult Result { get; }
      public Settlement Fief { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryExpelFromClan" />, read by each caller to report its own message.</summary>
   public enum ExpelFromClanResult
   {
      /// <summary>The companion was cast out (see <see cref="ExpelFromClanVerdict.WasGovernor" /> for the governor-release side effect).</summary>
      Expelled,

      /// <summary>The companion is dead or missing and cannot be cast out.</summary>
      ActorUnavailable,

      /// <summary>The player no longer leads their clan, so cannot cast a companion out of it.</summary>
      PlayerNotClanLeader,

      /// <summary>The target is no longer one of the player's clan companions.</summary>
      NotACompanion,

      /// <summary>The target is the player's own spouse; that bond can only be ended through end_marriage/divorce.</summary>
      IsSpouse,

      /// <summary>The companion is away on a companion-mission errand and must be recalled before being cast out.</summary>
      AwayOnErrand
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryExpelFromClan" />: the result and, only on
   ///   <see cref="ExpelFromClanResult.Expelled" />, whether the companion had to be relieved of a governorship first
   ///   (<see cref="WasGovernor" />) and the <see cref="GovernedFief" /> they held, so the caller can word its message.
   ///   False / null on every other result and when they governed nothing.
   /// </summary>
   public readonly struct ExpelFromClanVerdict
   {
      public ExpelFromClanVerdict(ExpelFromClanResult result, bool wasGovernor, string governedFief)
      {
         Result = result;
         WasGovernor = wasGovernor;
         GovernedFief = governedFief;
      }

      public ExpelFromClanResult Result { get; }
      public bool WasGovernor { get; }
      public string GovernedFief { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TryGrantStipend" />, read by each caller to report its own message.</summary>
   public enum GrantStipendResult
   {
      /// <summary>The stipend was started: <see cref="GrantStipendVerdict.ClampedAmountPerDay" /> denars/day are now escrowed for the tranche.</summary>
      Started,

      /// <summary>The recipient is dead or missing and cannot be kept on a stipend.</summary>
      RecipientUnavailable,

      /// <summary>The recipient is not one of the player's own clan companions, so no clan stipend applies.</summary>
      NotPlayerCompanion,

      /// <summary>No stipend escrow ledger is available right now.</summary>
      NoLedger,

      /// <summary>A stipend is already running for this hero; a second concurrent one would double-escrow.</summary>
      AlreadyActive,

      /// <summary>The escrow could not be funded (the player's purse cannot cover the full tranche); see <see cref="GrantStipendVerdict.FailReason" />.</summary>
      CouldNotFund
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TryGrantStipend" />: the result, the clamped daily amount on
   ///   <see cref="GrantStipendResult.Started" /> (zero otherwise), and <see cref="FailReason" /> - the escrow's own
   ///   player-facing reason - only on <see cref="GrantStipendResult.CouldNotFund" /> (null on every other result).
   /// </summary>
   public readonly struct GrantStipendVerdict
   {
      public GrantStipendVerdict(GrantStipendResult result, int clampedAmountPerDay, string failReason)
      {
         Result = result;
         ClampedAmountPerDay = clampedAmountPerDay;
         FailReason = failReason;
      }

      public GrantStipendResult Result { get; }
      public int ClampedAmountPerDay { get; }
      public string FailReason { get; }
   }

   /// <summary>The outcome of <see cref="CourtActionResolver.TrySwearOath" />, read by each caller to report its own message.</summary>
   public enum SwearOathResult
   {
      /// <summary>The oath was recorded: <see cref="SwearOathVerdict.Kind" /> (and its payload) is what was sworn.</summary>
      Sworn,

      /// <summary>The swearer is dead, missing, held prisoner, or is the player themselves, and cannot swear.</summary>
      SwearerUnavailable,

      /// <summary>No commitment ledger is available right now.</summary>
      NoLedger,

      /// <summary>The swearer already carries an oath outstanding; one vow at a time.</summary>
      AlreadyCommitted,

      /// <summary>The named oath kind did not resolve to one of the three whitelisted, verifiable kinds.</summary>
      UnknownKind,

      /// <summary>A pay_gold oath named no valid sum (unparseable, or non-positive once clamped).</summary>
      InvalidAmount,

      /// <summary>A keep_peace oath from a swearer who leads no faction of their own to swear peace for.</summary>
      CannotSwearPeace,

      /// <summary>A keep_peace oath named no faction that could be found.</summary>
      NoSuchFaction,

      /// <summary>A keep_peace oath named the swearer's OWN faction, which is meaningless.</summary>
      OwnFaction
   }

   /// <summary>
   ///   The verdict of <see cref="CourtActionResolver.TrySwearOath" />: the result, the resolved <see cref="Kind" />
   ///   (whenever one was parsed), the clamped <see cref="PromisedAmount" /> for a pay_gold oath, and the
   ///   <see cref="TargetFactionName" /> for a keep_peace oath, so each caller can word its OWN sworn line. Null
   ///   amount/faction on the kinds and failures where they do not apply.
   /// </summary>
   public readonly struct SwearOathVerdict
   {
      public SwearOathVerdict(SwearOathResult result, CommitmentKind kind, int? promisedAmount, string targetFactionName)
      {
         Result = result;
         Kind = kind;
         PromisedAmount = promisedAmount;
         TargetFactionName = targetFactionName;
      }

      public SwearOathResult Result { get; }
      public CommitmentKind Kind { get; }
      public int? PromisedAmount { get; }
      public string TargetFactionName { get; }
   }
}
