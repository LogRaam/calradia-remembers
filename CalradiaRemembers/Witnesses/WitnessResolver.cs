// Code written by Gabriel Mailhot, 12/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Witnesses
{
   /// <summary>
   ///   Determines which NPCs are present during a conversation and returns them
   ///   as <see cref="WitnessEntry" /> objects for prompt injection.
   ///   Two scenarios:
   ///   - Settlement (Keep / Town): enumerates <see cref="Settlement.HeroesWithoutParty" />
   ///   AND party leaders currently inside the settlement; filters to known profiles.
   ///   - Campaign map: enumerates the NPC's own party roster for known hero members.
   ///   Companions are handled separately via <see cref="ShowCompanionPickerIfNeeded" />,
   ///   which fires an async <see cref="MBInformationManager.ShowMultiSelectionInquiry" />
   ///   overlay on top of the already-open chat window.
   /// </summary>
   public static class WitnessResolver
   {
      // A companion further than this from the player in a scene is not "following"
      // them into the meeting — they're idling at the gate or across the courtyard.
      private const float FollowRadiusMeters = 15f;

      // The companion picker and the scene scan share this cap.
      private const int MaxCompanions = 2;
      private const int MaxLordWitnesses = 4;

      /// <summary>
      ///   Diagnostic: logs detailed witness-resolution data for a given NPC without
      ///   opening the chat. Used by <c>cr.witnesses [name]</c> to answer the
      ///   Talk-vs-Visit question (which collection holds present lords).
      /// </summary>
      public static string DiagnoseWitnesses(Hero npcHero, BannerlordNpcMemoryStore store)
      {
         if (npcHero == null) return "Hero is null.";

         var sb = new StringBuilder();
         sb.AppendLine($"NPC: {npcHero.Name}");
         sb.AppendLine($"  CurrentSettlement    : {npcHero.CurrentSettlement?.Name?.ToString() ?? "(null)"}");
         sb.AppendLine($"  StayingInSettlement  : {npcHero.StayingInSettlement?.Name?.ToString() ?? "(null)"}");
         sb.AppendLine($"  PartyBelongedTo      : {npcHero.PartyBelongedTo?.Name?.ToString() ?? "(null)"}");
         sb.AppendLine($"  IsNotable            : {npcHero.IsNotable}  Occupation: {npcHero.Occupation}");

         // ── Scene/location state — answers the Talk-vs-Visit question ────────
         // Visit loads a mission with a Location; Talk (from the menu) should not.
         // LocationComplex assigns every settlement hero to a Location regardless
         // of whether a scene is loaded — if that holds in both paths, it is our
         // same-location witness filter.
         AppendSceneDiagnostics(sb, npcHero);

         Settlement settlement = npcHero.CurrentSettlement ?? npcHero.StayingInSettlement;
         if (settlement != null)
         {
            sb.AppendLine($"Settlement: {settlement.Name}");

            // HeroesWithoutParty
            List<Hero> hwp = settlement.HeroesWithoutParty?.ToList() ?? new List<Hero>();
            sb.AppendLine($"  HeroesWithoutParty ({hwp.Count} total):");
            var knownIds = new HashSet<string>(store?.ListIds() ?? new List<string>());
            var profiled = 0;
            foreach (Hero h in hwp)
            {
               if (h == Hero.MainHero || h == npcHero) continue;
               bool known = knownIds.Contains(h.StringId);
               if (known) profiled++;
               sb.AppendLine($"    {h.Name}  (profile: {(known ? "YES" : "no")}, " +
                             $"location: {DescribeHeroLocation(h)}, notable: {h.IsNotable})");
            }

            sb.AppendLine($"  → {profiled} with existing profile (usable as witnesses)");

            // Parties inside settlement
            try
            {
               var partyCount = 0;
               var partyProfiled = 0;
               sb.AppendLine("  Parties in settlement:");
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
               var count = 0;
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
         List<Hero> comps = GetPlayerCompanions();
         sb.AppendLine($"Player companions available for picker: {comps.Count}");
         foreach (Hero c in comps)
            sb.AppendLine($"  {c.Name}");

         return sb.ToString();
      }

      // ── Public API ────────────────────────────────────────────────────────

      /// <summary>
      ///   Synchronously resolves lord witnesses for the given NPC from current
      ///   game state. Returns an empty list (never null) on any failure.
      /// </summary>
      public static List<WitnessEntry> ResolveAutoWitnesses(
         Hero npcHero,
         BannerlordNpcMemoryStore store)
      {
         var witnesses = new List<WitnessEntry>();

         if (npcHero == null || store == null) return witnesses;

         try
         {
            Settlement settlement = npcHero.CurrentSettlement ?? npcHero.StayingInSettlement;
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
      ///   Companions physically beside the player in the loaded scene (Visit path).
      ///   When non-empty, they join the conversation automatically and the picker is
      ///   NOT shown — what the player sees standing next to them is what the
      ///   conversation contains, eliminating the "go alone while Olaf stands right
      ///   there" dissonance. Empty when no mission is loaded (Talk) or no companion
      ///   followed the player within <see cref="FollowRadiusMeters" />.
      /// </summary>
      public static List<WitnessEntry> ResolveSceneCompanions()
      {
         var result = new List<WitnessEntry>();
         try
         {
            Mission mission = Mission.Current;
            Agent playerAgent = mission?.MainAgent;

            if (mission == null || playerAgent == null) return result;

            foreach (Agent agent in mission.Agents)
            {
               if (result.Count >= MaxCompanions) break;

               if (agent == null || agent == playerAgent || !agent.IsHuman) continue;

               Hero h = (agent.Character as CharacterObject)?.HeroObject;

               if (h == null || h == Hero.MainHero || !h.IsAlive) continue;
               if (h.PartyBelongedTo != MobileParty.MainParty) continue;

               if ((agent.Position - playerAgent.Position).LengthSquared > FollowRadiusMeters * FollowRadiusMeters) continue;

               result.Add(new WitnessEntry {
                  Name = h.Name.ToString(),
                  RelationToNpc = "the player's companion",
                  IsPlayerCompanion = true,
                  HeroStringId = h.StringId
               });
            }

            if (result.Count > 0)
               ModLog.Info($"[WitnessResolver] {result.Count} companion(s) physically present " +
                           $"in scene — auto-included, picker suppressed.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] ResolveSceneCompanions failed: {ex.Message}");
         }

         return result;
      }

      /// <summary>
      ///   Shows a companion-selection overlay if the player has companions in their
      ///   party. Each confirmed companion is handed to <paramref name="onCompanionJoins" />
      ///   — the resolver only resolves; the caller owns what "joining" means (same
      ///   contract as <see cref="ResolveSceneCompanions" /> returning a list).
      ///   Fail-soft: any exception is caught — the chat stays open with auto-witnesses.
      ///   Skipped when companions are already physically present in the scene
      ///   (see <see cref="ResolveSceneCompanions" /> — the caller decides).
      /// </summary>
      public static void ShowCompanionPickerIfNeeded(Action<WitnessEntry> onCompanionJoins)
      {
         if (onCompanionJoins == null) return;
         try
         {
            // A prisoner faces their captor alone — no companions, ever. The captive
            // chat path never calls this, but the guarantee belongs here, not to a
            // code-path accident.
            if (Hero.MainHero?.IsPrisoner == true) return;

            List<Hero> companions = GetPlayerCompanions();
            if (companions.Count == 0)
            {
               ModLog.Info("[WitnessResolver] Companion picker skipped — no companions or clan members in the player's party.");

               return;
            }

            var elements = new List<InquiryElement>();
            foreach (Hero c in companions)
               elements.Add(new InquiryElement(c.StringId, c.Name.ToString(), null));

            ModLog.Info($"[WitnessResolver] Companion picker opening ({companions.Count} option(s)).");

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Bring Companions?",
                  "Would you like up to two companions to accompany you to this meeting?",
                  elements,
                  true, // isExitShown — Exit = meet the NPC alone
                  0, MaxCompanions,
                  "Bring", "Go alone",
                  selected =>
                  {
                     // Log the count the affirmative action actually received: a player report of a
                     // brought companion never appearing (not in the header, not in the prompt) is
                     // resolved here (the pick was captured) or upstream (an empty selection) by this line.
                     int count = selected?.Count ?? 0;
                     ModLog.Info($"[WitnessResolver] Companion picker confirmed with {count} selection(s).");

                     if (selected == null) return;
                     foreach (InquiryElement element in selected)
                     {
                        var id = element.Identifier as string;

                        if (string.IsNullOrEmpty(id))
                        {
                           ModLog.Info("[WitnessResolver] a picked element carried no hero id — skipped.");

                           continue;
                        }

                        Hero companion = Hero.AllAliveHeroes
                                             .FirstOrDefault(h => h.StringId == id);

                        if (companion == null)
                        {
                           ModLog.Info($"[WitnessResolver] picked companion id '{id}' was not found among alive heroes — skipped.");

                           continue;
                        }

                        onCompanionJoins(new WitnessEntry {
                           Name = companion.Name.ToString(),
                           RelationToNpc = "the player's companion",
                           IsPlayerCompanion = true,
                           HeroStringId = companion.StringId
                        });
                        ModLog.Info($"[WitnessResolver] Companion joined: {companion.Name}.");
                     }
                  },
                  null),
               false, // pauseGameActiveState — chat already paused time
               true); // prioritize — ensures focus over the chat layer
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] ShowCompanionPickerIfNeeded failed: {ex.Message}");
         }
      }

      #region private

      // ── Private ───────────────────────────────────────────────────────────

      /// <summary>
      ///   Dumps the mission/location state that decides whether witnesses can be
      ///   resolved from real presence (Visit — a scene with a Location is loaded)
      ///   or must be extrapolated from LocationComplex assignments (Talk — no scene).
      /// </summary>
      private static void AppendSceneDiagnostics(StringBuilder sb, Hero npcHero)
      {
         try
         {
            ICampaignMission mission = CampaignMission.Current;
            sb.AppendLine($"  CampaignMission.Current : {(mission != null ? "YES" : "(null)")}");
            if (mission != null)
            {
               sb.AppendLine($"    Mission.Location      : {mission.Location?.StringId ?? "(null)"}");
               sb.AppendLine($"    Mission.Mode          : {mission.Mode}");
            }

            LocationComplex complex = LocationComplex.Current;
            sb.AppendLine($"  LocationComplex.Current : {(complex != null ? "YES" : "(null)")}");
            if (complex != null)
               sb.AppendLine($"    NPC location          : {DescribeHeroLocation(npcHero)}");
         }
         catch (Exception ex)
         {
            sb.AppendLine($"  Scene diagnostics failed: {ex.Message}");
         }
      }

      private static WitnessEntry BuildEntry(Hero h, Hero npcHero, BannerlordNpcMemoryStore store) => new() {
         Name = h.Name.ToString(),
         RelationToNpc = DescribeWitnessRelation(npcHero, h),
         HeroStringId = h.StringId,
         Persona = ResolvePersona(h, store)
      };

      /// <summary>
      ///   The LocationComplex location a hero is assigned to ("lordshall", "tavern",
      ///   "center", …), or a fallback marker when unavailable. Fail-soft by design —
      ///   this is the data the same-location witness filter would rely on.
      /// </summary>
      private static string DescribeHeroLocation(Hero h)
      {
         try
         {
            LocationComplex complex = LocationComplex.Current;

            if (complex == null) return "(no complex)";
            Location location = complex.GetLocationOfCharacter(h);

            return location?.StringId ?? "(unassigned)";
         }
         catch
         {
            return "(error)";
         }
      }

      private static string DescribeWitnessRelation(Hero npcHero, Hero witness)
      {
         try
         {
            if (npcHero.Clan?.Kingdom?.Leader == witness) return "your liege";
            if (witness.Clan?.Kingdom?.Leader == npcHero) return "your vassal";
            if (npcHero.Clan != null && npcHero.Clan == witness.Clan)
               return "your clan member";
            int rel = witness.GetRelation(npcHero);

            if (rel >= 30) return "a lord you trust deeply";
            if (rel >= 10) return "a lord you regard favorably";
            if (rel <= -30) return "a rival lord (deep enmity)";
            if (rel <= -10) return "a lord you distrust";

            return "a lord you know";
         }
         catch
         {
            return "a lord present";
         }
      }

      /// <summary>
      ///   The LocationComplex location a hero is assigned to, or null when it cannot
      ///   be read. Null means "presence unknown" and callers must treat it as absent.
      /// </summary>
      private static Location GetLocationOf(Hero h)
      {
         try
         {
            return LocationComplex
                   .Current?.GetLocationOfCharacter(h);
         }
         catch
         {
            return null;
         }
      }

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
               // Any hero travelling in the player's party qualifies — tavern
               // companions (Wanderer) AND clan family (spouse, siblings — they
               // carry Occupation.Lord). Bringing your spouse or brother to a
               // meeting is exactly the point of the picker.
               companions.Add(h);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] GetPlayerCompanions failed: {ex.Message}");
         }

         return companions;
      }

      /// <summary>
      ///   Validity for campaign-map party witnesses: sharing a roster is real
      ///   co-presence, so no location gate applies.
      /// </summary>
      private static bool IsValidWitness(Hero h, Hero npcHero, HashSet<string> knownIds)
      {
         if (h is not {IsAlive: true}) return false;
         if (h == Hero.MainHero || h == npcHero) return false;

         return knownIds.Contains(h.StringId);
      }

      /// <summary>
      ///   Validity for settlement witnesses: on top of the base checks, the hero
      ///   must not be a notable (notables keep to their own business — they do not
      ///   attend keep audiences) and must be assigned to the SAME location as the
      ///   conversation NPC. Unknown locations are excluded by design.
      /// </summary>
      private static bool IsValidWitness(
         Hero h,
         Hero npcHero,
         HashSet<string> knownIds,
         Location npcLocation)
      {
         if (!IsValidWitness(h, npcHero, knownIds)) return false;
         if (h.IsNotable) return false;

         if (npcLocation == null) return false;
         Location witnessLocation = GetLocationOf(h);

         return witnessLocation == npcLocation;
      }

      private static void ResolveCampaignMapWitnesses(
         List<WitnessEntry> witnesses,
         Hero npcHero,
         BannerlordNpcMemoryStore store)
      {
         MobileParty party = npcHero.PartyBelongedTo;

         if (party == null) return;

         var knownIds = new HashSet<string>(store.ListIds());
         var count = 0;

         foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
         {
            if (count >= MaxLordWitnesses) break;
            Hero h = element.Character?.HeroObject;

            if (!IsValidWitness(h, npcHero, knownIds)) continue;

            witnesses.Add(BuildEntry(h, npcHero, store));
            count++;
         }

         ModLog.Info($"[WitnessResolver] Campaign party of {npcHero.Name}: " +
                     $"{count} witness(es) (known-profile filter applied).");
      }

      /// <summary>
      ///   Extracts a concise character descriptor (the archetype label — the first
      ///   sentence of the profile's Trait) so the main NPC can voice this witness's
      ///   reactions in character. Returns null when no profile or trait is available.
      /// </summary>
      private static string ResolvePersona(Hero h, BannerlordNpcMemoryStore store)
      {
         try
         {
            NpcProfile profile = store?.Get(h.StringId);
            string trait = profile?.Trait;

            if (string.IsNullOrWhiteSpace(trait)) return null;

            // Take the first sentence (archetype name), lower-cased for natural reading.
            int dot = trait.IndexOf('.');
            string label = dot > 0
               ? trait.Substring(0, dot)
               : trait;

            return label.Trim();
         }
         catch
         {
            return null;
         }
      }

      private static void ResolveSettlementWitnesses(
         List<WitnessEntry> witnesses,
         Settlement settlement,
         Hero npcHero,
         BannerlordNpcMemoryStore store)
      {
         // Notables (artisans, merchants, gang leaders…) receive the player one-on-one
         // in their own corner of town — other heroes are never plausibly present at
         // that meeting. A non-hero flavor witness fitting their trade may be, though.
         if (npcHero.IsNotable)
         {
            TryAddFlavorWitness(witnesses, npcHero);
            ModLog.Info($"[WitnessResolver] '{npcHero.Name}' is a notable — " +
                        $"{witnesses.Count} flavor witness(es), no hero witnesses.");

            return;
         }

         var knownIds = new HashSet<string>(store.ListIds());
         // Same-location gate. LocationComplex assigns every settlement hero to a
         // location (lordshall, tavern, center…) in both the Talk and Visit paths
         // (validated in-game 2026-06-12) — a witness must share the NPC's location.
         // When either location is unreadable we EXCLUDE: a misplaced witness (the
         // smith attending a keep audience) is the bug; a missing one is just quiet.
         Location npcLocation = GetLocationOf(npcHero);
         var count = 0;

         // Pass 1 — heroes explicitly without a party (most reliable for Talk)
         foreach (Hero h in settlement.HeroesWithoutParty)
         {
            if (count >= MaxLordWitnesses) break;

            if (!IsValidWitness(h, npcHero, knownIds, npcLocation)) continue;

            witnesses.Add(BuildEntry(h, npcHero, store));
            count++;
         }

         // Pass 2 — party leaders currently in the settlement (catches lords whose
         //          party is parked inside the gates)
         if (count < MaxLordWitnesses)
            try
            {
               foreach (MobileParty party in settlement.Parties)
               {
                  if (count >= MaxLordWitnesses) break;
                  Hero leader = party.LeaderHero;

                  if (!IsValidWitness(leader, npcHero, knownIds, npcLocation)) continue;
                  if (witnesses.Any(w => w.HeroStringId == leader.StringId)) continue; // dedup

                  witnesses.Add(BuildEntry(leader, npcHero, store));
                  count++;
               }
            }
            catch (Exception ex)
            {
               ModLog.Error($"[WitnessResolver] settlement.Parties enumeration failed: {ex.Message}");
            }

         ModLog.Info($"[WitnessResolver] Settlement '{settlement.Name}': {count} witness(es) " +
                     $"(same-location '{npcLocation?.StringId ?? "?"}' + known-profile filters).");
      }

      /// <summary>
      ///   Adds at most one synthetic, non-hero witness that plausibly belongs at a
      ///   notable's side — a gang enforcer, an apprentice, a lingering customer.
      ///   Half the time no one is around, so private moments stay possible and the
      ///   scenes stay varied. Synthetic entries carry no HeroStringId; the display
      ///   and [WITNESS_REACTION] paths already support that (player companions too).
      /// </summary>
      private static void TryAddFlavorWitness(List<WitnessEntry> witnesses, Hero npcHero)
      {
         if (MBRandom.RandomFloat < 0.5f) return;

         WitnessEntry entry;
         switch (npcHero.Occupation)
         {
            case Occupation.GangLeader:
               entry = new WitnessEntry {
                  Name = "One of the gang",
                  RelationToNpc = "one of your enforcers, never far from your side",
                  Persona = "Hard-eyed — silent, loyal, sizing up the visitor"
               };

               break;
            case Occupation.Artisan:
               entry = new WitnessEntry {
                  Name = "The apprentice",
                  RelationToNpc = "your apprentice, working within earshot",
                  Persona = "Curious — steals glances while pretending to work"
               };

               break;
            case Occupation.Merchant:
               entry = new WitnessEntry {
                  Name = "A customer",
                  RelationToNpc = "a customer browsing your wares nearby",
                  Persona = "Nosy — lingers over the goods, ears open"
               };

               break;
            default:
               return; // rural notables and the rest: simply alone
         }

         witnesses.Add(entry);
      }

      #endregion
   }
}