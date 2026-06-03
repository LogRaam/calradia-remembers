// Code written by Gabriel Mailhot, 03/06/2026.
// Sprint multi-NPC: resolves witnesses from game state for the current encounter.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Witnesses
{
    /// <summary>
    ///   Determines which NPCs are present during a conversation and returns them
    ///   as <see cref="WitnessEntry" /> objects for prompt injection.
    ///
    ///   Two scenarios:
    ///   - Settlement (Keep / Town): enumerates <see cref="Settlement.HeroesWithoutParty" />
    ///     AND party leaders currently inside the settlement; filters to known profiles.
    ///   - Campaign map: enumerates the NPC's own party roster for known hero members.
    ///
    ///   Companions are handled separately via <see cref="ShowCompanionPickerIfNeeded" />,
    ///   which fires an async <see cref="MBInformationManager.ShowMultiSelectionInquiry" />
    ///   overlay on top of the already-open chat window.
    /// </summary>
    public static class WitnessResolver
    {
        private const int MaxLordWitnesses = 4;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        ///   Synchronously resolves lord witnesses for the given NPC from current
        ///   game state. Returns an empty list (never null) on any failure.
        /// </summary>
        public static List<WitnessEntry> ResolveAutoWitnesses(
            Hero npcHero, BannerlordNpcMemoryStore store)
        {
            var witnesses = new List<WitnessEntry>();
            if (npcHero == null || store == null) return witnesses;

            try
            {
                Settlement settlement = npcHero.CurrentSettlement
                                     ?? npcHero.StayingInSettlement;
                if (settlement != null)
                    ResolveSettlementWitnesses(witnesses, settlement, npcHero, store);
                else
                    ResolveCampaignMapWitnesses(witnesses, npcHero, store);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[WitnessResolver] ResolveAutoWitnesses failed: {ex.Message}");
            }

            return witnesses;
        }

        /// <summary>
        ///   Shows a companion-selection overlay if the player has companions in their
        ///   party. On confirm, the chosen companion is added to the currently open chat
        ///   window via <see cref="ChatManager.ActiveViewModel" />.
        ///   Fail-soft: any exception is caught — the chat stays open with auto-witnesses.
        ///   Only called when auto-witnesses are already present.
        /// </summary>
        public static void ShowCompanionPickerIfNeeded()
        {
            try
            {
                List<Hero> companions = GetPlayerCompanions();
                if (companions.Count == 0) return;

                var elements = new List<InquiryElement>();
                foreach (Hero c in companions)
                    elements.Add(new InquiryElement(c.StringId, c.Name.ToString(), null));

                ModLog.Info($"[WitnessResolver] Companion picker opening ({companions.Count} option(s)).");

                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        "Bring a Companion?",
                        "Would you like a companion to accompany you to this meeting?",
                        elements,
                        true,  // isExitShown — Exit = no companion
                        0, 1,  // min 0 selected, max 1
                        "Bring", "Skip",
                        selected => {
                            if (selected == null || selected.Count == 0) return;
                            var id = selected[0].Identifier as string;
                            if (string.IsNullOrEmpty(id)) return;
                            Hero companion = Hero.AllAliveHeroes
                                .FirstOrDefault(h => h.StringId == id);
                            if (companion == null) return;

                            var entry = new WitnessEntry {
                                Name              = companion.Name.ToString(),
                                RelationToNpc     = "the player's companion",
                                IsPlayerCompanion = true
                            };
                            ChatManager.ActiveViewModel?.AddWitness(entry);
                            ModLog.Info($"[WitnessResolver] Companion joined: {companion.Name}.");
                        },
                        null,   // negativeAction (Skip — no companion added)
                        "", false),
                    false,  // pauseGameActiveState — chat already paused time
                    true);  // prioritize — ensures focus over the chat layer
            }
            catch (Exception ex)
            {
                ModLog.Error($"[WitnessResolver] ShowCompanionPickerIfNeeded failed: {ex.Message}");
            }
        }

        /// <summary>
        ///   Diagnostic: logs detailed witness-resolution data for a given NPC without
        ///   opening the chat. Used by <c>cr.witnesses [name]</c> to answer the
        ///   Talk-vs-Visit question (which collection holds present lords).
        /// </summary>
        public static string DiagnoseWitnesses(Hero npcHero, BannerlordNpcMemoryStore store)
        {
            if (npcHero == null) return "Hero is null.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"NPC: {npcHero.Name}");
            sb.AppendLine($"  CurrentSettlement    : {npcHero.CurrentSettlement?.Name?.ToString() ?? "(null)"}");
            sb.AppendLine($"  StayingInSettlement  : {npcHero.StayingInSettlement?.Name?.ToString() ?? "(null)"}");
            sb.AppendLine($"  PartyBelongedTo      : {npcHero.PartyBelongedTo?.Name?.ToString() ?? "(null)"}");

            Settlement settlement = npcHero.CurrentSettlement ?? npcHero.StayingInSettlement;
            if (settlement != null)
            {
                sb.AppendLine($"Settlement: {settlement.Name}");

                // HeroesWithoutParty
                var hwp = settlement.HeroesWithoutParty?.ToList() ?? new List<Hero>();
                sb.AppendLine($"  HeroesWithoutParty ({hwp.Count} total):");
                var knownIds = new HashSet<string>(store?.ListIds() ?? new List<string>());
                int profiled = 0;
                foreach (Hero h in hwp)
                {
                    if (h == Hero.MainHero || h == npcHero) continue;
                    bool known = knownIds.Contains(h.StringId);
                    if (known) profiled++;
                    sb.AppendLine($"    {h.Name}  (profile: {(known ? "YES" : "no")})");
                }
                sb.AppendLine($"  → {profiled} with existing profile (usable as witnesses)");

                // Parties inside settlement
                try
                {
                    int partyCount = 0;
                    int partyProfiled = 0;
                    sb.AppendLine($"  Parties in settlement:");
                    foreach (MobileParty party in settlement.Parties ?? Enumerable.Empty<MobileParty>())
                    {
                        Hero leader = party.LeaderHero;
                        if (leader == null || leader == Hero.MainHero || leader == npcHero) continue;
                        bool known = knownIds.Contains(leader.StringId);
                        if (known) partyProfiled++;
                        partyCount++;
                        sb.AppendLine($"    Party '{party.Name}' leader: {leader.Name}  (profile: {(known ? "YES" : "no")})");
                    }
                    if (partyCount == 0) sb.AppendLine("    (none)");
                    sb.AppendLine($"  → {partyProfiled} party leader(s) with existing profile");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  Parties enumeration failed: {ex.Message}");
                }
            }
            else
            {
                MobileParty party = npcHero.PartyBelongedTo;
                if (party != null)
                {
                    sb.AppendLine($"Campaign map — party: {party.Name}");
                    var knownIds = new HashSet<string>(store?.ListIds() ?? new List<string>());
                    int count = 0;
                    try
                    {
                        foreach (TroopRosterElement el in party.MemberRoster.GetTroopRoster())
                        {
                            Hero h = el.Character?.HeroObject;
                            if (h == null || h == npcHero || h == Hero.MainHero) continue;
                            bool known = knownIds.Contains(h.StringId);
                            sb.AppendLine($"  {h.Name}  (profile: {(known ? "YES" : "no")})");
                            count++;
                        }
                        if (count == 0) sb.AppendLine("  (no other heroes in party)");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  MemberRoster enumeration failed: {ex.Message}");
                    }
                }
                else
                {
                    sb.AppendLine("No settlement and no party — isolated NPC (wandering or in dungeon).");
                }
            }

            // Player companions
            var comps = GetPlayerCompanions();
            sb.AppendLine($"Player companions available for picker: {comps.Count}");
            foreach (Hero c in comps)
                sb.AppendLine($"  {c.Name}");

            return sb.ToString();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static void ResolveSettlementWitnesses(
            List<WitnessEntry> witnesses, Settlement settlement,
            Hero npcHero, BannerlordNpcMemoryStore store)
        {
            var knownIds = new HashSet<string>(store.ListIds());
            int count = 0;

            // Pass 1 — heroes explicitly without a party (most reliable for Talk)
            foreach (Hero h in settlement.HeroesWithoutParty)
            {
                if (count >= MaxLordWitnesses) break;
                if (!IsValidWitness(h, npcHero, knownIds)) continue;

                witnesses.Add(BuildEntry(h, npcHero));
                count++;
            }

            // Pass 2 — party leaders currently in the settlement (catches lords whose
            //          party is parked inside the gates)
            if (count < MaxLordWitnesses)
            {
                try
                {
                    foreach (MobileParty party in settlement.Parties)
                    {
                        if (count >= MaxLordWitnesses) break;
                        Hero leader = party.LeaderHero;
                        if (!IsValidWitness(leader, npcHero, knownIds)) continue;
                        if (witnesses.Any(w => w.Name == leader.Name.ToString())) continue; // dedup

                        witnesses.Add(BuildEntry(leader, npcHero));
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[WitnessResolver] settlement.Parties enumeration failed: {ex.Message}");
                }
            }

            ModLog.Info($"[WitnessResolver] Settlement '{settlement.Name}': " +
                        $"{count} lord witness(es) (known-profile filter applied).");
        }

        private static void ResolveCampaignMapWitnesses(
            List<WitnessEntry> witnesses, Hero npcHero, BannerlordNpcMemoryStore store)
        {
            MobileParty party = npcHero.PartyBelongedTo;
            if (party == null) return;

            var knownIds = new HashSet<string>(store.ListIds());
            int count = 0;

            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
            {
                if (count >= MaxLordWitnesses) break;
                Hero h = element.Character?.HeroObject;
                if (!IsValidWitness(h, npcHero, knownIds)) continue;

                witnesses.Add(BuildEntry(h, npcHero));
                count++;
            }

            ModLog.Info($"[WitnessResolver] Campaign party of {npcHero.Name}: " +
                        $"{count} witness(es) (known-profile filter applied).");
        }

        private static bool IsValidWitness(Hero h, Hero npcHero, HashSet<string> knownIds)
        {
            if (h == null || !h.IsAlive) return false;
            if (h == Hero.MainHero || h == npcHero) return false;
            return knownIds.Contains(h.StringId);
        }

        private static WitnessEntry BuildEntry(Hero h, Hero npcHero) => new WitnessEntry {
            Name          = h.Name.ToString(),
            RelationToNpc = DescribeWitnessRelation(npcHero, h),
            HeroStringId  = h.StringId
        };

        private static List<Hero> GetPlayerCompanions()
        {
            var companions = new List<Hero>();
            try
            {
                if (MobileParty.MainParty?.MemberRoster == null) return companions;
                foreach (TroopRosterElement element in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                {
                    Hero h = element.Character?.HeroObject;
                    if (h == null || h == Hero.MainHero || !h.IsAlive) continue;
                    if (h.Occupation != Occupation.Wanderer) continue;
                    companions.Add(h);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[WitnessResolver] GetPlayerCompanions failed: {ex.Message}");
            }
            return companions;
        }

        private static string DescribeWitnessRelation(Hero npcHero, Hero witness)
        {
            try
            {
                if (npcHero.Clan?.Kingdom?.Leader == witness) return "your liege";
                if (witness.Clan?.Kingdom?.Leader == npcHero)  return "your vassal";
                if (npcHero.Clan != null && npcHero.Clan == witness.Clan)
                    return "your clan member";
                int rel = witness.GetRelation(npcHero);
                if (rel >= 30)  return "a lord you trust deeply";
                if (rel >= 10)  return "a lord you regard favorably";
                if (rel <= -30) return "a rival lord (deep enmity)";
                if (rel <= -10) return "a lord you distrust";
                return "a lord you know";
            }
            catch
            {
                return "a lord present";
            }
        }
    }
}
