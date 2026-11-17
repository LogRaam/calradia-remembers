// Code written by Gabriel Mailhot, 17/11/2026.
// The full text of a fed world event (Bellum Civile / AI Rebel Relay request, 17/11/2026: "remove the irreversible 300-character
// information loss at the ingestion/storage boundary"; Gabriel: a bridge may want it back to digest or compress on its side).
// Kept OUTSIDE the save, like the story archive (a save never grows with a digest): one file per text, under
// Documents\Mount and Blade II Bannerlord\CalradiaRemembers\EventDetails\<campaign>\<key>.txt. A text is written once under a key
// made from its source and its words (ExternalEventRules.DetailsKey); the save holds only the key. So a save loaded later reads
// exactly the text it wrote, never a newer one an Update wrote on another branch of the campaign. A key no save points at any
// more (a retracted or faded event, an abandoned branch) is a small file nobody reads. Writes are locked: Feed may be called off
// the main thread.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>Writes and reads the full text of fed world events, outside the save.</summary>
   public static class EventDetailsStore
   {
      private static readonly object Gate = new object();
      private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

      /// <summary>A self-test's folder, so it never writes beside the player's own texts. Null in play.</summary>
      internal static string FolderOverride { get; set; }

      /// <summary>The folder every campaign's texts live in.</summary>
      public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                "Mount and Blade II Bannerlord", "CalradiaRemembers", "EventDetails");

      /// <summary>This campaign's folder; null outside a campaign.</summary>
      public static string Folder
      {
         get
         {
            if (FolderOverride != null) return FolderOverride;
            string campaign = CharacterOverrideSet.CampaignFolderName(Campaign.Current?.UniqueGameId);

            return campaign == null ? null : Path.Combine(Root, campaign);
         }
      }

      /// <summary>Keeps <paramref name="text" /> under <paramref name="key" /> (once; the same key is the same text). False when it could not be written. Never throws.</summary>
      public static bool Write(string key, string text)
      {
         string folder = Folder;
         if (string.IsNullOrEmpty(key) || text == null || folder == null) return false;

         string path = Path.Combine(folder, key + ".txt");
         lock (Gate)
         {
            try
            {
               if (!File.Exists(path))
               {
                  Directory.CreateDirectory(folder);
                  string temp = path + ".tmp";
                  File.WriteAllText(temp, text, new UTF8Encoding(false));
                  File.Move(temp, path);
               }

               Cache[path] = text;

               return true;
            }
            catch (Exception ex)
            {
               ModLog.Error($"[FEED] details not kept ({key}): {ex.GetType().Name}: {ex.Message}");

               return false;
            }
         }
      }

      /// <summary>The text kept under <paramref name="key" />, or null when there is none. Never throws.</summary>
      public static string Read(string key)
      {
         string folder = Folder;
         if (string.IsNullOrEmpty(key) || folder == null) return null;

         string path = Path.Combine(folder, key + ".txt");
         lock (Gate)
         {
            if (Cache.TryGetValue(path, out string cached)) return cached;
            try
            {
               string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
               if (text != null) Cache[path] = text;

               return text;
            }
            catch (Exception ex)
            {
               ModLog.Error($"[FEED] details not read ({key}): {ex.GetType().Name}: {ex.Message}");

               return null;
            }
         }
      }

      /// <summary>Forgets what was read (a self-test's folder going away).</summary>
      internal static void ClearCache()
      {
         lock (Gate) Cache.Clear();
      }
   }
}
