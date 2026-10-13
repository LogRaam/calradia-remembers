// Code written by Gabriel Mailhot, 12/10/2026.
// Queens with a chosen face. Gabriel (12/10/2026): "Je vais creer une reine pour chaque faction" in the character editor;
// "une reine par Empire, et on habille la reine du roi". A faces file names, per realm, the face its queen wears: the
// line the face editor copies with Ctrl+C (<BodyProperties ... key="..."/>). CR dresses the ruler's wife in it, and gives
// a former queen her own face back when the king remarries or the face is taken out. Files: the player's Custom
// faces.json first, then each mod's ModuleData/cr_faces.json, then CR's own ModuleData/faces.json; the first one read
// keeps a realm. Only the face changes: her name, age and build stay hers.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The face a realm's queen wears, and the file it came from.</summary>
   public sealed class RealmFace
   {
      /// <summary>The realm's game id ("vlandia", "empire_s").</summary>
      public string RealmId { get; init; }

      /// <summary>The face editor's line: &lt;BodyProperties ... key="..."/&gt;.</summary>
      public string Key { get; init; }

      /// <summary>Where it came from: "Custom", a mod's id, or "CalradiaRemembers".</summary>
      public string Source { get; init; }
   }

   /// <summary>Reads and merges faces files. Pure.</summary>
   public static class RealmFaceFile
   {
      /// <summary>The prefix of every face the editor copies.</summary>
      public const string KeyPrefix = "<BodyProperties ";

      /// <summary>
      ///   { "queens": { "&lt;realm id&gt;": "&lt;BodyProperties .../&gt;" } }. A line that is not the editor's, or a file
      ///   that is not JSON, is reported and left out; nothing here throws.
      /// </summary>
      public static IReadOnlyList<RealmFace> Parse(string json, string source, List<string> problems)
      {
         var faces = new List<RealmFace>();
         JObject root;
         try { root = JObject.Parse(json ?? ""); }
         catch (Exception ex)
         {
            problems?.Add($"{source} is not valid JSON ({ex.Message}): no face read from it.");
            return faces;
         }

         if (root["queens"] is not JObject queens) return faces;

         foreach (JProperty p in queens.Properties())
         {
            string key = p.Value.Type == JTokenType.String ? ((string) p.Value).Trim() : null;
            if (key == null || !key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
               problems?.Add($"{source}: the face for '{p.Name}' is not the face editor's line (it starts \"{KeyPrefix.Trim()}\"): left out.");
               continue;
            }

            faces.Add(new RealmFace {RealmId = p.Name.Trim().ToLowerInvariant(), Key = key, Source = source});
         }

         return faces;
      }

      /// <summary>One face per realm: the first read keeps it (the player's own, then a mod's, then CR's).</summary>
      public static IReadOnlyDictionary<string, RealmFace> Merge(IEnumerable<RealmFace> facesInReadingOrder)
      {
         var merged = new Dictionary<string, RealmFace>(StringComparer.OrdinalIgnoreCase);
         foreach (RealmFace f in facesInReadingOrder ?? Enumerable.Empty<RealmFace>())
            if (f?.RealmId != null && !merged.ContainsKey(f.RealmId))
               merged[f.RealmId] = f;

         return merged;
      }
   }

   /// <summary>Who wears what now.</summary>
   public sealed class RealmFaceFacts
   {
      /// <summary>The realms a face is given for.</summary>
      public IReadOnlyCollection<string> FaceRealms { get; init; } = Array.Empty<string>();

      /// <summary>Each realm's queen now (the ruler's wife, alive and in the world), by realm id; null when he has none.</summary>
      public IReadOnlyDictionary<string, string> QueenOf { get; init; } = new Dictionary<string, string>();

      /// <summary>The women CR dressed and has not given back yet, hero id to the realm whose face they wear.</summary>
      public IReadOnlyDictionary<string, string> Dressed { get; init; } = new Dictionary<string, string>();
   }

   /// <summary>What to change.</summary>
   public sealed class RealmFacePlan
   {
      /// <summary>Heroes to dress, hero id to realm id.</summary>
      public Dictionary<string, string> Dress { get; } = new();

      /// <summary>Heroes to give their own face back.</summary>
      public List<string> Restore { get; } = new();
   }

   /// <summary>Who to dress and who to give her face back. Pure.</summary>
   public static class RealmFacePlanner
   {
      /// <summary>
      ///   Each realm with a face dresses its queen, unless she already wears it; a dressed woman who is no longer the
      ///   queen of the realm whose face she wears, or whose realm has no face any more, gets her own face back.
      /// </summary>
      public static RealmFacePlan Plan(RealmFaceFacts f)
      {
         var plan = new RealmFacePlan();
         if (f == null) return plan;

         var withFace = new HashSet<string>(f.FaceRealms ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
         foreach (KeyValuePair<string, string> d in f.Dressed ?? new Dictionary<string, string>())
         {
            bool stillHers = withFace.Contains(d.Value) && f.QueenOf != null && f.QueenOf.TryGetValue(d.Value, out string queen) && queen == d.Key;
            if (!stillHers) plan.Restore.Add(d.Key);
         }

         foreach (string realm in withFace)
         {
            if (f.QueenOf == null || !f.QueenOf.TryGetValue(realm, out string queen) || string.IsNullOrEmpty(queen)) continue;
            if (f.Dressed != null && f.Dressed.TryGetValue(queen, out string wearing) && string.Equals(wearing, realm, StringComparison.OrdinalIgnoreCase)) continue;

            plan.Dress[queen] = realm;
         }

         return plan;
      }
   }
}
