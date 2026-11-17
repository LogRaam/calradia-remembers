// Code written by Gabriel Mailhot, 14/11/2026.
// Women in the ranks (Gabriel, 14/11/2026; Logic.Troops.WomenInRanksPolicy). Every soldier of a battle, the War Sails ship
// crews included (NavalShipAgents calls Mission.SpawnTroop), is prepared by Mission.GetAgentBuildDataToSpawnTroop (read in
// the 1.5.7 binaries). A postfix on it marks the soldier a woman when the draw says so. Her face: with only the gender set,
// the game would draw the troop's own face keys, a man's, onto a woman's body; so CR answers Mission.OnComputeTroopBodyProperties
// (no module of the game subscribes to it in 1.5.7), which receives the equipment she really spawns with (her helmet decides
// how much hair shows) and her seed, and draws a woman's face in the range of her culture's village women. For every other
// soldier it returns exactly what the game would have computed (the same call). Resolved by name and return type, read
// only from the result, and switched off with a log line when the game moves it (the 1.5.5 SpawnAgent change, tashmetu).
// Her weight and build are then made lean (WomenInRanksPolicy.LeanFrame): the townswoman range gave stout frames under which
// her head looked tiny (EvilPoptart1987, Nexus 15/11/2026).
//
// EvilPoptart1987 (Nexus, 16/11/2026), after the lean fix: "Still attack of the killer shrunken heads." Women from Retinues,
// whose troops are women in the game's own data, look right. What set ours apart: answering OnComputeTroopBodyProperties
// makes the game turn its face cache on for EVERY non-hero soldier (Mission.SpawnAgent, 1.5.7: UseFaceCache = !IsHero,
// all on cache id 0), a path no module of the game runs in single player. So CR no longer subscribes: her body is set
// on the prepared soldier itself (AgentBuildData.BodyProperties), the road on which the game leaves the cache off, and
// every man is spawned exactly as without CR. The cache as the cause is SUSPECTED (native code), not proven.
// Her helmet is read from the equipment the game will draw for her seed, so her hair still fits under it.

#region

using System;
using System.Linq;
using System.Reflection;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Troops;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Makes a share of the soldiers entering a battle women, per culture and realm.</summary>
   public static class WomenInRanksPatch
   {
      private const string TargetMethod = "GetAgentBuildDataToSpawnTroop";

      private static Harmony _harmony;
      private static bool _complained;
      private static bool _faceComplained;

      /// <summary>True once attached to the game's soldier preparation (cr.test women_in_ranks asserts it).</summary>
      public static bool IsPatched { get; private set; }

      /// <summary>Attaches the postfix. Idempotent; logs and stays off when the game has no such method.</summary>
      public static void EnsurePatched()
      {
         if (IsPatched) return;

         try
         {
            MethodInfo target = typeof(Mission).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                               .FirstOrDefault(m => m.Name == TargetMethod && m.ReturnType == typeof(AgentBuildData));
            if (target == null)
            {
               ModLog.Compat($"[RANKS] women in the ranks not attached: Mission.{TargetMethod} returning AgentBuildData not found in this game version.");

               return;
            }

            _harmony ??= new Harmony("CalradiaRemembers.WomenInRanks");
            _harmony.Patch(target, postfix: new HarmonyMethod(typeof(WomenInRanksPatch), nameof(Postfix)));
            IsPatched = true;
            ModLog.Info($"[RANKS] women in the ranks attached to Mission.{TargetMethod}.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[RANKS] women in the ranks failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The culture's woman whose face range a woman soldier is drawn in (village woman, else townswoman), or null.</summary>
      public static BasicCharacterObject FemaleFaceOf(CultureObject culture)
      {
         CharacterObject face = culture?.VillageWoman ?? culture?.Townswoman;

         // A face is drawn inside her body range: a template without one cannot give a face, so it gives no women.
         return face != null && face.IsFemale && face.BodyPropertyRange != null ? face : null;
      }

      /// <summary>Marks the soldier a woman when the draw says so. Reads only the prepared result.</summary>
      public static void Postfix(Mission __instance, AgentBuildData __result)
      {
         try
         {
            if (__result == null || __instance == null || GameNetwork.IsMultiplayer || !ModSettings.WomenInRanks) return;
            if (!(__result.AgentCharacter is CharacterObject troop)) return;

            IAgentOriginBase origin = __result.AgentOrigin;
            var soldier = new RankSoldierFacts {
               CultureId = troop.Culture?.StringId,
               RealmId = (origin?.BattleCombatant as PartyBase)?.MapFaction?.StringId,
               TroopId = troop.StringId,
               Seed = unchecked((origin?.Seed ?? 0) * 31 + (origin?.UniqueSeed ?? 0)),
               IsHero = troop.IsHero || troop.IsPlayerCharacter,
               IsAlreadyFemale = troop.IsFemale,
               HasFemaleFace = FemaleFaceOf(troop.Culture) != null
            };
            if (!WomenInRanksPolicy.IsWoman(WomenInRanksLoader.Table, soldier)) return;

            __result.IsFemale(true);
            __result.BodyProperties(HerBody(troop, __result.AgentEquipmentSeed, __result.AgentCivilianEquipment));
         }
         catch (Exception ex)
         {
            if (_complained) return;

            _complained = true;
            ModLog.Error($"[RANKS] preparing a soldier failed, left as the game made him (said once): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Her whole body, set on the prepared soldier: the equipment the game will draw for this seed (Mission.
      ///   DecideAgentSpawnEquipment draws it the same way) decides how much hair her helmet shows.
      /// </summary>
      public static BodyProperties HerBody(CharacterObject troop, int equipmentSeed, bool civilian)
      {
         Equipment drawn = null;
         try
         {
            drawn = Equipment.GetRandomEquipmentElements(troop, false, civilian ? Equipment.EquipmentType.Civilian : Equipment.EquipmentType.Battle, equipmentSeed);
         }
         catch
         {
            // only her hair under the helmet depends on it; the troop's first set will do
         }

         return FaceFor(true, troop, drawn ?? (civilian ? troop.FirstCivilianEquipment : troop.FirstBattleEquipment), equipmentSeed);
      }

      /// <summary>
      ///   The face itself, apart from the prepared soldier: an AgentBuildData cannot be built outside a battle (its
      ///   Team.Invalid builds a team with no mission and throws, 1.5.7), so cr.test all asks this directly.
      /// </summary>
      public static BodyProperties FaceFor(bool madeWoman, BasicCharacterObject character, Equipment equipment, int seed)
      {
         try
         {
            if (madeWoman && character != null && !character.IsFemale
                && FemaleFaceOf((character as CharacterObject)?.Culture) is BasicCharacterObject face)
            {
               BodyProperties drawn = TaleWorlds.Core.FaceGen.GetRandomBodyProperties(character.Race, true, face.GetBodyPropertiesMin(), face.GetBodyPropertiesMax(),
                                                      (int) (equipment?.HairCoverType ?? ArmorComponent.HairCoverTypes.None), seed,
                                                      face.BodyPropertyRange?.HairTags, face.BodyPropertyRange?.BeardTags, face.BodyPropertyRange?.TattooTags, 0f);

               // The townswoman range reaches a stout peasant's frame, under which her head looked tiny (EvilPoptart1987,
               // 15/11/2026): her face stays as drawn, her weight and build are carried into a lean band. Her age is the man's
               // she stands in for (Gabriel, 17/11/2026: every woman measured 41, the men 20 to 41).
               float hisAge = character.GetBodyProperties(equipment, seed).Age;
               return new BodyProperties(new DynamicBodyProperties(WomenInRanksPolicy.SoldierAge(hisAge), WomenInRanksPolicy.LeanFrame(drawn.Weight), WomenInRanksPolicy.LeanFrame(drawn.Build)),
                                         drawn.StaticProperties);
            }
         }
         catch (Exception ex)
         {
            // This runs for every soldier a battle spawns: a failure here must never stop a battle, only cost her face.
            if (!_faceComplained)
            {
               _faceComplained = true;
               ModLog.Error($"[RANKS] drawing a woman soldier's face failed, the game's own face used (said once): {ex.GetType().Name}: {ex.Message}");
            }
         }

         return character.GetBodyProperties(equipment, seed);
      }
   }
}
