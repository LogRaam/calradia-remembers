// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Reads the live game state into the eligibility/amount facts for the four "RP with teeth"
   ///   court actions (give_influence, lend_troops, sway_opinion, release_prisoner), delegating the
   ///   pure math to <see cref="CourtActionPolicy" />. Shared by three call sites so they can never
   ///   drift apart: <c>EncounterContextBuilder</c> (what to teach the LLM), <c>BannerlordGameStateBridge</c>
   ///   (re-validation at execution — the bridge is the law), and the <c>cr.rp_verbs</c> console command.
   ///   Per-NPC cooldowns are derived from the profile's own <see cref="NpcProfile.Events" /> (a hidden
   ///   marker in the recorded summary), so no new persisted field is needed.
   /// </summary>
   public static class CourtActionResolver
   {
      /// <summary>Hidden marker recorded on a give_influence memory event, scanned back for the cooldown.</summary>
      public const string InfluenceMarker = "[cr_influence]";

      /// <summary>Hidden marker recorded on a lend_troops memory event, scanned back for the cooldown.</summary>
      public const string TroopsMarker = "[cr_troops]";

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

      /// <summary>Sum of healthy (unwounded) rank-and-file troops in a roster — heroes never count toward a loan.</summary>
      private static int HealthyRegularTroopCount(TroopRoster roster)
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
      ///   The most recent in-game day the NPC's own memory events carry the given marker, or null when
      ///   never — the cooldown clock. Deliberately reads the PERSISTED <see cref="NpcProfile.Events" />
      ///   list rather than a new field: the action itself stamps the marker into its own recorded summary.
      /// </summary>
      private static int? LastMarkerDay(NpcProfile profile, string marker)
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
}
