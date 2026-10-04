// Code written by Gabriel Mailhot, 03/10/2026.
// EnjoyingHydra (Nexus, 03/10/2026): companions' audiences, accepted, never opened, and one companion left over a
// grievance he never got to voice. A granted audience was spent at the yes. What it changes is now promised at the yes
// and kept only when the conversation is on screen; broken when the open fails, so the companion asks again.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What a granted audience will change, held until the conversation really opens. Pure.</summary>
   public sealed class AudiencePromiseLedger
   {
      private readonly Dictionary<string, List<Action>> _promises = new(StringComparer.Ordinal);

      /// <summary>Holds <paramref name="onKept" /> until a conversation with <paramref name="heroId" /> opens.</summary>
      public void Promise(string heroId, Action onKept)
      {
         if (string.IsNullOrEmpty(heroId) || onKept == null) return;
         if (!_promises.TryGetValue(heroId, out List<Action> list)) _promises[heroId] = list = new List<Action>();
         list.Add(onKept);
      }

      /// <summary>The conversation opened: runs every promise for this hero once; returns how many. One that throws costs only itself.</summary>
      public int Keep(string heroId)
      {
         if (string.IsNullOrEmpty(heroId) || !_promises.TryGetValue(heroId, out List<Action> list)) return 0;
         _promises.Remove(heroId);

         foreach (Action a in list)
         {
            try { a(); }
            catch { /* the others still happen; the host logs its own failures */ }
         }

         return list.Count;
      }

      /// <summary>The open failed: forgets this hero's promises unkept; returns how many.</summary>
      public int Break(string heroId)
      {
         if (string.IsNullOrEmpty(heroId) || !_promises.TryGetValue(heroId, out List<Action> list)) return 0;
         _promises.Remove(heroId);

         return list.Count;
      }

      /// <summary>True while a promise for this hero waits.</summary>
      public bool IsPending(string heroId) => !string.IsNullOrEmpty(heroId) && _promises.ContainsKey(heroId);

      /// <summary>Forgets everything (a new campaign, a load).</summary>
      public void Clear() => _promises.Clear();
   }
}
