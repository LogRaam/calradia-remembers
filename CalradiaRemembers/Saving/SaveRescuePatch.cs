// Code written by Gabriel Mailhot, 09/11/2026.
// Opens a save the game itself refuses because one container grew past the engine's ceiling.
//
// tashmetu (Nexus, 09/11/2026): "long campaigns with CR stop loading": "An item with the same key has already been added"
// in ArchiveDeserializer.LoadFrom. Gabriel, the same day: "est-ce qu'on peut implementer notre propre repair kit dans CR
// pour les saves des joueurs?" Mods load before a save is chosen, so CR can mend the game's own loader beforehand.
//
// Read in the decompiled 1.4.8 TaleWorlds.SaveSystem: each entry id is read on 3 bytes (BinaryReader.Read3ByteInt), so in a
// container of more than 16,777,215 elements ids repeat; SaveEntryFolder.AddEntry adds them to a Dictionary and throws on
// the first repeat. Two hooks, each idle on a healthy save:
//   1. AddEntry: a repeated id is skipped instead of thrown (only a wrapped container can repeat one).
//   2. ContainerHeaderLoadData.InitialieReaders (sic, the game's spelling): a container counted past the ceiling is loaded
//      with no element, so the game never asks for the entries it lost. Its store comes back empty, the rest of the save
//      opens, and CR's next save writes its stores compressed (SaveBlob), well under the ceiling.
// What is lost is told to the player at session launch, and logged with [SAVE].

#region

using System;
using System.Collections.Generic;
using System.Reflection;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Saving;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

#endregion

namespace CalradiaRemembers.Saving
{
   /// <summary>Lets the game open a save in which one container grew past the ceiling, that container empty.</summary>
   internal static class SaveRescuePatch
   {
      private static readonly object Gate = new object();
      private static readonly List<int> Emptied = new List<int>();
      private static int _skipped;
      private static MethodInfo _setElementCount;

      /// <summary>Whether both hooks are attached (cr.save_sizes says so).</summary>
      internal static bool Attached { get; private set; }

      /// <summary>
      ///   Attaches both hooks to the game's loader. Called once, at module load, before any save is chosen. Never throws: a
      ///   hook that cannot attach is logged and the game loads as it always did.
      /// </summary>
      internal static void Attach(Harmony harmony)
      {
         try
         {
            MethodInfo addEntry = AccessTools.Method(typeof(SaveEntryFolder), nameof(SaveEntryFolder.AddEntry));
            MethodInfo readers = AccessTools.Method(typeof(ContainerHeaderLoadData), "InitialieReaders");
            _setElementCount = AccessTools.PropertySetter(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.ElementCount));
            if (addEntry == null || readers == null || _setElementCount == null)
            {
               ModLog.Error("[SAVE] the save rescue could not find the game's loader (a game update?); a save past the ceiling will not open.");

               return;
            }

            harmony.Patch(addEntry, new HarmonyMethod(typeof(SaveRescuePatch), nameof(AddEntryPrefix)));
            harmony.Patch(readers, postfix: new HarmonyMethod(typeof(SaveRescuePatch), nameof(InitialieReadersPostfix)));
            Attached = true;
         }
         catch (Exception ex) { ModLog.Error($"[SAVE] the save rescue failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   At session launch: tells the player, once, what the last load had to leave out, then forgets it. Silent when the
      ///   save was healthy.
      /// </summary>
      internal static void ReportAfterLoad()
      {
         int[] emptied;
         int skipped;
         lock (Gate)
         {
            emptied = Emptied.ToArray();
            skipped = _skipped;
            Emptied.Clear();
            _skipped = 0;
         }

         if (emptied.Length == 0 && skipped == 0) return;

         ModLog.Error($"[SAVE] this save could not have loaded without the rescue: {emptied.Length} container(s) past the "
                      + $"{SaveBlobCodec.EngineCeiling:N0} ceiling loaded empty (sizes {string.Join(", ", emptied)}), {skipped:N0} repeated entries skipped. "
                      + "CR's stores among them start empty; save now to keep the campaign loadable.");
         try
         {
            InformationManager.DisplayMessage(new InformationMessage(
               $"Calradia Remembers repaired this save: {emptied.Length} data block(s) too large for the game to load were left empty "
               + "(likely CR's relation ledger or memories). Save now to keep this campaign loadable.", Colors.Yellow));
         }
         catch
         {
            /* no UI at this moment: the log holds it */
         }
      }

      // A repeated id can only come from a container whose ids wrapped; the container itself is emptied by the postfix below,
      // so the skipped entry is never asked for. On a healthy save no id repeats and this returns true every time.
      private static bool AddEntryPrefix(Dictionary<EntryId, SaveEntry> ____entries, SaveEntry saveEntry)
      {
         if (____entries == null || saveEntry == null || !____entries.ContainsKey(saveEntry.Id)) return true;

         lock (Gate) _skipped++;

         return false;
      }

      private static void InitialieReadersPostfix(ContainerHeaderLoadData __instance)
      {
         if (__instance == null || !SaveBlobCodec.IsBeyondCeiling(__instance.ElementCount)) return;

         int count = __instance.ElementCount;
         _setElementCount.Invoke(__instance, new object[] {0});
         lock (Gate) Emptied.Add(count);
      }
   }
}
