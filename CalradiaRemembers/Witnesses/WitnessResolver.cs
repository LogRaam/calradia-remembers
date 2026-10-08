// Code written by Gabriel Mailhot, 12/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
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

      // The companion picker and the scene scan share this cap. Internal (not private): the companion-briefing
      // picker (CompanionBriefingCoordinator) reuses the SAME cap, so a briefing can never offer more companions
      // than the follow-up conversation's own witness picker could ever seat.
      internal const int MaxCompanions = 2;
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
            // Witness density (player complaint: conversations are ALWAYS crowded): read once so both
            // convergence paths below thin against the same setting, then post-filtered together at the single
            // chokepoint where hero witnesses from either path land in the same list.
            WitnessDensity density = ModSettings.WitnessDensity;

            Settlement settlement = npcHero.CurrentSettlement ?? npcHero.StayingInSettlement;
            if (settlement != null)
               ResolveSettlementWitnesses(witnesses, settlement, npcHero, store, density);
            else
               ResolveCampaignMapWitnesses(witnesses, npcHero, store);

            ApplyWitnessDensity(witnesses, density);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] ResolveAutoWitnesses failed: {ex.Message}");
         }

         return witnesses;
      }

      /// <summary>
      ///   Post-filter chokepoint for BOTH hero-witness paths (settlement + campaign map): thins the AMBIENT
      ///   HERO witnesses (entries carrying a <see cref="WitnessEntry.HeroStringId" />) per
      ///   <see cref="WitnessPresencePolicy" />, applied ON TOP of the resolver's own existing cap
      ///   (<see cref="MaxLordWitnesses" />) since it runs after that cap already truncated the list, and at
      ///   <see cref="WitnessDensity.Bustling" /> (the default) this is an exact passthrough (p=1.0,
      ///   cap=int.MaxValue), so it reproduces today's live behaviour unchanged. Never touches entries without a
      ///   HeroStringId (the notable's synthetic flavor witness, which has its own density-aware skip in
      ///   <see cref="TryAddFlavorWitness" />); the player's own companions never enter this list at all, they
      ///   are resolved separately by <see cref="ResolveSceneCompanions" />.
      /// </summary>
      private static void ApplyWitnessDensity(List<WitnessEntry> witnesses, WitnessDensity density)
      {
         List<WitnessEntry> heroWitnesses = witnesses.Where(w => !string.IsNullOrEmpty(w.HeroStringId)).ToList();
         if (heroWitnesses.Count == 0) return;

         List<WitnessEntry> kept = WitnessPresencePolicy.Filter(heroWitnesses, density, () => MBRandom.RandomFloat);

         witnesses.RemoveAll(w => !string.IsNullOrEmpty(w.HeroStringId) && !kept.Contains(w));
      }

      /// <summary>
      ///   Companions physically beside the player in the loaded scene (Visit path).
      ///   When non-empty, they join the conversation automatically and the picker is
      ///   NOT shown — what the player sees standing next to them is what the
      ///   conversation contains, eliminating the "go alone while Olaf stands right
      ///   there" dissonance. Empty when no mission is loaded (Talk) or no companion
      ///   followed the player within <see cref="FollowRadiusMeters" />.
      /// </summary>
      public static List<WitnessEntry> ResolveSceneCompanions(Hero conversationNpc = null)
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

               if (h == null || !Mapping.ActionGate.Allows(h, Logic.ActionKind.Witness)) continue;
               // A companion who IS the conversation NPC does not witness their own conversation.
               if (conversationNpc != null && h.StringId == conversationNpc.StringId) continue;
               if (h.PartyBelongedTo != MobileParty.MainParty) continue;

               if ((agent.Position - playerAgent.Position).LengthSquared > FollowRadiusMeters * FollowRadiusMeters) continue;

               result.Add(EntryForCompanion(h));
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
      ///   The witness entry for one of the player's own companions. ONE read of what a companion looks like
      ///   in a room, so the two ways they can arrive - walking in beside the player, or being seated by the
      ///   briefing they were given - cannot describe the same person differently.
      /// </summary>
      public static WitnessEntry EntryForCompanion(Hero h)
         => h == null
            ? null
            : new WitnessEntry {
               Name = h.Name?.ToString(),
               RelationToNpc = "the player's companion",
               IsPlayerCompanion = true,
               HeroStringId = h.StringId,
               IsFemale = h.IsFemale,
               Gear = ComposeGear(h)
            };

      /// <summary>
      ///   Shows a companion-selection overlay if the player has companions in their
      ///   party. Each confirmed companion is handed to <paramref name="onCompanionJoins" />
      ///   — the resolver only resolves; the caller owns what "joining" means (same
      ///   contract as <see cref="ResolveSceneCompanions" /> returning a list).
      ///   Fail-soft: any exception is caught — the chat stays open with auto-witnesses.
      ///   Skipped when companions are already physically present in the scene
      ///   (see <see cref="ResolveSceneCompanions" /> — the caller decides).
      /// </summary>
      public static void ShowCompanionPickerIfNeeded(Hero conversationNpc, Action<WitnessEntry> onCompanionJoins)
      {
         if (onCompanionJoins == null) return;
         try
         {
            // A prisoner faces their captor alone, no companions, ever. The captive
            // chat path never calls this, but the guarantee belongs here, not to a
            // code-path accident.
            if (Hero.MainHero?.IsPrisoner == true) return;

            // Gabriel (08/10/2026): with the Invite button, the question is asked only when the player wants it.
            if (!ModSettings.AskCompanionsAtStart)
            {
               ModLog.Info("[WitnessResolver] Companion picker not shown (MCM 'Ask Which Companions Come Along' is off); Invite brings them in.");

               return;
            }

            List<Hero> companions = GetPlayerCompanions(conversationNpc);
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
                           HeroStringId = companion.StringId,
                           IsFemale = companion.IsFemale,
                           Gear = ComposeGear(companion)
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

      /// <summary>
      ///   Bring-participants-to-a-captor-scene, increment 2 (2026-08-21): shows a picker offering the OTHER
      ///   lord prisoners the player currently holds (excluding <paramref name="subjectHero" />, the scene's own
      ///   captive), so the player can bring one along as leverage against the other ("use Ascyron to pressure
      ///   Lucon"). Mirrors <see cref="ShowCompanionPickerIfNeeded" />'s shape exactly (same async
      ///   <c>MBInformationManager.ShowMultiSelectionInquiry</c>, same "onX joins" callback contract, same
      ///   fail-soft try/catch), but draws from the prisoner roster instead of the party roster, and marks each
      ///   picked hero <see cref="WitnessEntry.IsBroughtCaptive" /> instead of <see cref="WitnessEntry.IsPlayerCompanion" />
      ///   so the prompt (PromptBuilder.AppendBroughtCaptives) frames them as coerced leverage, never a free
      ///   witness and never an acting captor. A prisoner facing their OWN captor has nobody else to bring, so
      ///   this only ever fires from the PLAYER-as-captor direction, never the victim-encounter one.
      /// </summary>
      public static void ShowBroughtCaptivePickerIfNeeded(Hero subjectHero, Action<WitnessEntry> onCaptiveBrought)
      {
         if (onCaptiveBrought == null) return;
         try
         {
            List<Hero> otherPrisoners = GetOtherHeldLordPrisoners(subjectHero);
            if (otherPrisoners.Count == 0)
            {
               ModLog.Info("[WitnessResolver] Brought-captive picker skipped, no other lord prisoners held.");

               return;
            }

            var elements = new List<InquiryElement>();
            foreach (Hero h in otherPrisoners)
               elements.Add(new InquiryElement(h.StringId, h.Name.ToString(), null));

            ModLog.Info($"[WitnessResolver] Brought-captive picker opening ({otherPrisoners.Count} option(s)).");

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Bring Another Prisoner?",
                  "You may bring up to two of your other prisoners into this scene, to use one against the other.",
                  elements,
                  true, // isExitShown (Exit = leave them in the cells)
                  0, MaxCompanions,
                  "Bring", "Leave them",
                  selected =>
                  {
                     int count = selected?.Count ?? 0;
                     ModLog.Info($"[WitnessResolver] Brought-captive picker confirmed with {count} selection(s).");

                     if (selected == null) return;
                     foreach (InquiryElement element in selected)
                     {
                        var id = element.Identifier as string;

                        if (string.IsNullOrEmpty(id)) continue;

                        Hero captive = otherPrisoners.FirstOrDefault(h => h.StringId == id);

                        if (captive == null) continue;

                        onCaptiveBrought(new WitnessEntry {
                           Name = captive.Name.ToString(),
                           RelationToNpc = "another of the player's prisoners",
                           IsBroughtCaptive = true,
                           HeroStringId = captive.StringId,
                           IsFemale = captive.IsFemale,
                           Gear = ComposeGear(captive)
                        });
                        ModLog.Info($"[WitnessResolver] Brought captive joined: {captive.Name}.");
                     }
                  },
                  null),
               false,
               true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] ShowBroughtCaptivePickerIfNeeded failed: {ex.Message}");
         }
      }

      /// <summary>
      ///   The lord prisoners currently held in the player's own party, excluding <paramref name="subjectHero" />
      ///   (the scene's own captive, never offered as leverage against themselves). Mirrors the lord-only filter
      ///   <c>EncounterContextBuilder.ResolveHeldLordPrisoners</c> already applies for the prisoner-bargain
      ///   teaching: a wanderer or notable prisoner is not the kind of leverage a "use one lord against another"
      ///   scene is about.
      /// </summary>
      private static List<Hero> GetOtherHeldLordPrisoners(Hero subjectHero)
      {
         var result = new List<Hero>();
         try
         {
            IEnumerable<TroopRosterElement> roster = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster();
            if (roster == null) return result;

            foreach (TroopRosterElement element in roster)
            {
               Hero h = element.Character?.HeroObject;

               if (h is not {IsLord: true, IsHumanPlayerCharacter: false}) continue;
               if (subjectHero != null && h.StringId == subjectHero.StringId) continue;

               result.Add(h);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WitnessResolver] GetOtherHeldLordPrisoners failed: {ex.Message}");
         }

         return result;
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

      /// <summary>
      ///   Re-describes <paramref name="entry" /> from <paramref name="observer" />'s point of view, which is
      ///   what <see cref="WitnessEntry.RelationToNpc" /> has always claimed to be ("from the NPC's
      ///   perspective") and was not once several people speak in turn.
      ///   <para>
      ///     Round-table audit D2: entries are built once, from the HOST's angle, and were then handed
      ///     unchanged to every other speaker. At a council hosted by king Derthert with his vassal Aldric
      ///     and the rival Boyd present, Boyd's own prompt read "Aldric (your vassal)". Aldric is Derthert's
      ///     vassal, not Boyd's, so a lord could get his own liege wrong in the middle of a council.
      ///   </para>
      ///   Returns the entry unchanged when there is nothing to re-describe: a synthetic flavour witness (an
      ///   enforcer, an apprentice) has no hero to reason about, and an entry carrying a fixed role (the
      ///   player's companion, a captive victim) means the same thing to everyone in the room.
      /// </summary>
      public static WitnessEntry ForObserver(WitnessEntry entry, Hero observer, BannerlordNpcMemoryStore store)
      {
         if (entry == null || observer == null) return entry;
         if (string.IsNullOrEmpty(entry.HeroStringId)) return entry;
         if (entry.IsPlayerCompanion || entry.IsCaptiveVictim) return entry;
         if (entry.HeroStringId == observer.StringId) return entry;

         try
         {
            Hero subject = Hero.FindFirst(h => h != null && h.StringId == entry.HeroStringId);

            if (subject == null) return entry;

            return new WitnessEntry {
               Name = entry.Name,
               HeroStringId = entry.HeroStringId,
               IsPlayerCompanion = entry.IsPlayerCompanion,
               IsCaptiveVictim = entry.IsCaptiveVictim,
               Persona = entry.Persona,
               // Sex does not change with the observer either: carried over, with the live hero as fallback
               // for an entry built before it was recorded.
               IsFemale = entry.IsFemale ?? subject.IsFemale,
               // Gear does not change with the observer: carried over rather than recomposed.
               Gear = entry.Gear,
               // Neither does what they REMEMBER. This was silently dropped while Gear beside it was kept, so
               // re-describing the room for another speaker quietly emptied everyone's past (2026-09-07).
               Memory = entry.Memory,
               RelationToNpc = DescribeWitnessRelation(observer, subject)
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WITNESS] could not re-describe {entry.Name} for {observer.Name}: {ex.GetType().Name}: {ex.Message}");

            return entry;
         }
      }

      /// <summary>Builds the entry for the conversation HOST as seen by another speaker: a real relation, plus their role.</summary>
      public static WitnessEntry HostForObserver(Hero host, Hero observer, BannerlordNpcMemoryStore store)
      {
         if (host == null) return null;

         string relation = observer != null && observer != host
            ? $"{DescribeWitnessRelation(observer, host)}, and the host of this gathering"
            : "the host of this conversation, standing beside you";

         return new WitnessEntry {
            Name = host.Name?.ToString() ?? "",
            HeroStringId = host.StringId,
            RelationToNpc = relation,
            IsPlayerCompanion = false,
            Persona = ResolvePersona(host, store),
            IsFemale = host.IsFemale,
            Gear = ComposeGear(host)
         };
      }

      private static WitnessEntry BuildEntry(Hero h, Hero npcHero, BannerlordNpcMemoryStore store) => new() {
         Name = h.Name.ToString(),
         RelationToNpc = DescribeWitnessRelation(npcHero, h),
         HeroStringId = h.StringId,
         Persona = ResolvePersona(h, store),
         IsFemale = h.IsFemale,
         Gear = ComposeGear(h)
      };

      /// <summary>
      ///   Equipment-awareness pillar: a witness's notable equipped gear (Subject voice), so the speaking NPC
      ///   can react to a companion's mastercraft blade or a bystander's shabby rags. Null when nothing about
      ///   their gear stands out or the hero cannot be read. Never throws: called for every witness, every turn.
      /// </summary>
      internal static string ComposeGear(Hero hero)
      {
         try
         {
            // Scene-appropriate loadout, the same rule as the player and the speaking NPC (EquipmentLoadoutPolicy):
            // civilian inside a settlement, battle in the field, so a companion at the player's side is not
            // described in plate in a tavern. Resolved off the live bridge; an unresolved scene keeps battle.
            SceneType scene = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.GetCurrentScene() ?? SceneType.Unknown;
            bool preferCivilian = EquipmentLoadoutPolicy.PreferCivilianLoadout(scene);

            string composed = EquipmentNotabilityPolicy.ComposeWorn(
               EquipmentFactsExtractor.Extract(hero, preferCivilian), hero.IsFemale, AppearanceVoice.Subject);

            return string.IsNullOrWhiteSpace(composed) ? null : composed;
         }
         catch { return null; }
      }

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

            // "lord" is gendered in the prompt's ear (player report 2026-08-31: a lady witness read as male);
            // "your liege" / "your vassal" / "your clan member" already read either way.
            string title = witness.IsFemale ? "lady" : "lord";

            if (rel >= 30) return $"a {title} you trust deeply";
            if (rel >= 10) return $"a {title} you regard favorably";
            if (rel <= -30) return $"a rival {title} (deep enmity)";
            if (rel <= -10) return $"a {title} you distrust";

            return $"a {title} you know";
         }
         catch
         {
            return witness?.IsFemale == true ? "a lady present" : "a lord present";
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

      // Internal (not private): the companion-briefing picker (CompanionBriefingCoordinator) reuses this exact
      // candidate list, so it can never offer a hero the ordinary witness picker would refuse to seat.
      internal static List<Hero> GetPlayerCompanions(Hero conversationNpc = null)
      {
         var companions = new List<Hero>();
         try
         {
            if (MobileParty.MainParty?.MemberRoster == null) return companions;
            foreach (TroopRosterElement element in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
               Hero h = element.Character?.HeroObject;

               if (h == null || !Mapping.ActionGate.Allows(h, Logic.ActionKind.Witness)) continue;
               // Never offer the NPC you are actually talking to as a companion to bring: a party member
               // (a married-in spouse, a sibling) who IS the conversation partner would otherwise witness
               // their own conversation, and in a round table take a second turn as themselves.
               if (conversationNpc != null && h.StringId == conversationNpc.StringId) continue;
               // Any hero travelling in the player's party qualifies: tavern
               // companions (Wanderer) AND clan family (spouse, siblings, they
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
         if (!Mapping.ActionGate.Allows(h, Logic.ActionKind.Witness)) return false;
         if (h == npcHero) return false;

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
      ///   Internal (not private): the council narrator model reuses it in <see cref="ChatManager" /> to seat
      ///   each council member with a real persona rather than a bare seat description.
      /// </summary>
      internal static string ResolvePersona(Hero h, BannerlordNpcMemoryStore store)
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
         BannerlordNpcMemoryStore store,
         WitnessDensity density)
      {
         // Notables (artisans, merchants, gang leaders…) receive the player one-on-one
         // in their own corner of town — other heroes are never plausibly present at
         // that meeting. A non-hero flavor witness fitting their trade may be, though.
         if (npcHero.IsNotable)
         {
            TryAddFlavorWitness(witnesses, npcHero, density);
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
      ///   <para>
      ///     Witness density (player complaint: conversations are always crowded): this flavor witness has no
      ///     HeroStringId, so the general <see cref="ApplyWitnessDensity" /> hero post-filter never reaches it.
      ///     Instead it gets its own, simpler density rule here: at <see cref="WitnessDensity.Sparse" /> it is
      ///     skipped entirely (a room already thinned to almost no one should not gain a substitute bystander);
      ///     at Normal and Bustling the existing 50% roll is left exactly as it was.
      ///   </para>
      /// </summary>
      private static void TryAddFlavorWitness(List<WitnessEntry> witnesses, Hero npcHero, WitnessDensity density)
      {
         if (density == WitnessDensity.Sparse) return;
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