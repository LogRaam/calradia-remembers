// Code written by Gabriel Mailhot, 12/10/2026.
// Queens with a chosen face. Gabriel (12/10/2026): "Je vais creer une reine pour chaque faction" in the character editor;
// "une reine par Empire, et on habille la reine du roi". At each session launch and each day, the ruler's wife of every
// realm named in a faces file (Logic/RealmFaces: the player's Custom faces.json, each mod's ModuleData/cr_faces.json,
// CR's ModuleData/faces.json) wears that face; a woman who is no longer that realm's queen gets her own face back. Her
// original face is kept in the save (additive key) so it can be given back; only the face changes (Hero.
// StaticBodyProperties), never her name, age or build. A face that does not load is reported and dresses nobody.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Dresses each realm's queen in the face its faces file gives, and gives a former queen her own face back.</summary>
   public sealed class RealmFaceBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_RealmFaces_v1bytes";

      /// <summary>CR's own file of faces, under its ModuleData.</summary>
      public const string FileName = "faces.json";

      /// <summary>A mod's file of faces, under its own ModuleData.</summary>
      public const string ModFileName = "cr_faces.json";

      public static RealmFaceBehavior Instance { get; private set; }

      // The women CR dressed: hero id to the realm whose face they wear and their own face, to give back.
      private Dictionary<string, DressedQueen> _dressed = new();
      private IReadOnlyDictionary<string, RealmFace> _faces = new Dictionary<string, RealmFace>();
      private readonly List<string> _problems = new();

      public RealmFaceBehavior()
      {
         Instance = this;
      }

      /// <summary>What reading the faces files found wrong (cr.testall reports it).</summary>
      internal IReadOnlyList<string> Problems => _problems;

      /// <summary>The faces in use, by realm id.</summary>
      internal IReadOnlyDictionary<string, RealmFace> Faces => _faces;

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => SafeRun.Step("RealmFaces.Load", () =>
         {
            LoadFaces();
            Apply();
         }));
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("RealmFaces.Daily", Apply));
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_dressed, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[FACES] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[FACES] save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves: nobody was dressed */
         }

         if (loaded is not {Length: > 0}) return;

         try { _dressed = JsonConvert.DeserializeObject<Dictionary<string, DressedQueen>>(Encoding.UTF8.GetString(loaded)) ?? new Dictionary<string, DressedQueen>(); }
         catch (Exception ex)
         {
            _dressed = new Dictionary<string, DressedQueen>();
            ModLog.Error($"[FACES] load failed, the dressed queens are forgotten for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Reads the faces files again: the player's Custom one, each mod's, then CR's; the first one read keeps a realm.</summary>
      internal void LoadFaces()
      {
         _problems.Clear();
         var all = new List<RealmFace>();
         void Read(string path, string source)
         {
            try
            {
               if (File.Exists(path)) all.AddRange(RealmFaceFile.Parse(File.ReadAllText(path), source, _problems));
            }
            catch (Exception ex) { _problems.Add($"{path} could not be read ({ex.GetType().Name}: {ex.Message})."); }
         }

         Read(Path.Combine(NarrativeLoader.CustomBaseFolder(), FileName), ScenarioPackFolders.CustomSource);
         foreach (string id in NarrativePatchScanner.LoadedModuleIds())
         {
            if (string.Equals(id, ScenarioPackFolders.OwnSource, StringComparison.OrdinalIgnoreCase)) continue;
            string root;
            try { root = TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath(id); }
            catch { continue; }
            Read(Path.Combine(root, "ModuleData", ModFileName), id);
         }

         Read(Path.Combine(NarrativeLoader.ModuleDataFolder(), FileName), ScenarioPackFolders.OwnSource);
         _faces = RealmFaceFile.Merge(all);
         foreach (string problem in _problems) ModLog.Info("[FACES] ! " + problem);
         ModLog.Info($"[FACES] {_faces.Count} queen face(s): {string.Join(", ", _faces.Values.Select(f => f.RealmId + " (" + f.Source + ")"))}.");
      }

      /// <summary>Dresses every queen who should wear a face and gives a former queen her own face back (RealmFacePlanner).</summary>
      internal void Apply()
      {
         var queenOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         foreach (string realm in _faces.Keys)
         {
            Kingdom k = Kingdom.All.FirstOrDefault(x => string.Equals(x.StringId, realm, StringComparison.OrdinalIgnoreCase));
            Hero queen = k is {IsEliminated: false} ? k.Leader?.Spouse : null;
            queenOf[realm] = queen != null && HeroPresence.IsInTheWorld(queen) ? queen.StringId : null;
         }

         RealmFacePlan plan = RealmFacePlanner.Plan(new RealmFaceFacts {
            FaceRealms = _faces.Keys.ToList(), QueenOf = queenOf,
            Dressed = _dressed.ToDictionary(d => d.Key, d => d.Value.Realm)
         });

         foreach (string heroId in plan.Restore) GiveBack(heroId);
         foreach (KeyValuePair<string, string> d in plan.Dress) Dress(d.Key, d.Value, _faces[d.Value].Key);
         // A face changed in its file: the queen who wears the realm's face takes the new one.
         foreach (KeyValuePair<string, DressedQueen> d in _dressed.ToList())
            if (_faces.TryGetValue(d.Value.Realm, out RealmFace face) && face.Key != d.Value.Key)
               Dress(d.Key, d.Value.Realm, face.Key);
      }

      /// <summary>Self-test seam: dresses this hero in a face, as the plan would, and records her own face.</summary>
      internal bool DebugDress(Hero hero, string realm, string key) => hero != null && Dress(hero.StringId, realm, key);

      /// <summary>Self-test seam: gives this hero her own face back, as the plan would.</summary>
      internal bool DebugGiveBack(Hero hero) => hero != null && GiveBack(hero.StringId);

      #region private

      private bool Dress(string heroId, string realm, string key)
      {
         Hero hero = Hero.FindFirst(h => h != null && h.StringId == heroId);
         if (hero == null) return false;

         if (!BodyProperties.FromString(key, out BodyProperties face))
         {
            ModLog.Error($"[FACES] the face for '{realm}' does not load: {hero.Name} keeps her own.");
            return false;
         }

         if (!_dressed.ContainsKey(heroId)) _dressed[heroId] = new DressedQueen {Realm = realm, Original = hero.BodyProperties.ToString()};
         _dressed[heroId].Realm = realm;
         _dressed[heroId].Key = key;
         hero.StaticBodyProperties = face.StaticProperties;
         ModLog.Info($"[FACES] {hero.Name}, queen of {realm}, wears her realm's face.");

         return true;
      }

      private bool GiveBack(string heroId)
      {
         if (!_dressed.TryGetValue(heroId, out DressedQueen d)) return false;

         _dressed.Remove(heroId);
         Hero hero = Hero.FindFirst(h => h != null && h.StringId == heroId);
         if (hero == null || !BodyProperties.FromString(d.Original, out BodyProperties own)) return false;

         hero.StaticBodyProperties = own.StaticProperties;
         ModLog.Info($"[FACES] {hero.Name} is no longer queen of {d.Realm}: her own face is given back.");

         return true;
      }

      #endregion

      /// <summary>A woman CR dressed: the realm whose face she wears, and her own face to give back.</summary>
      private sealed class DressedQueen
      {
         public string Realm { get; set; }
         public string Original { get; set; }

         /// <summary>The face she wears now (a changed file dresses her again).</summary>
         public string Key { get; set; }
      }
   }
}
