// Code written by Gabriel Mailhot, 14/10/2026.
// Every standing aim, keyed by its id, with the raise ledger on each (tashmetu, 14/10/2026). Pure so the whole contract
// (admission, update keeping the ledger, retraction, expiry, the raise record) is held by tests; the mod resolves the
// hero, persists the list and wires the channels.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>The standing aims and what has been done with them.</summary>
   public sealed class AimBook
   {
      private readonly List<StandingAim> _aims = new List<StandingAim>();

      /// <summary>Starts a book from saved aims (null or empty for an old save).</summary>
      public AimBook(IEnumerable<StandingAim> saved = null)
      {
         if (saved == null) return;

         foreach (var aim in saved)
            if (AimRules.HasRequiredIdentity(aim) && !string.IsNullOrWhiteSpace(aim.Want) && Find(aim.Id) == null)
               _aims.Add(aim);
      }

      /// <summary>Every standing aim, for persistence.</summary>
      public IReadOnlyList<StandingAim> All => _aims;

      /// <summary>
      ///   Registers or updates an aim. An existing id is UPDATED in place and keeps its raise ledger: a bridge that
      ///   rewords an aim the lord already raised must not hand him a fresh leave to raise it again.
      /// </summary>
      public AimAdmission Register(StandingAim candidate, int today)
      {
         if (!AimRules.HasRequiredIdentity(candidate)) return AimAdmission.MissingIdentity;

         string want = AimRules.BoundWant(candidate.Want);
         if (want == null) return AimAdmission.EmptyWant;
         if (candidate.ExpiresOnDay.HasValue && candidate.ExpiresOnDay.Value < today) return AimAdmission.AlreadyExpired;

         var existing = Find(candidate.Id);
         if (existing != null)
         {
            existing.Want = want;
            existing.PlayerPart = AimRules.BoundPlayerPart(candidate.PlayerPart);
            existing.MissionId = Clean(candidate.MissionId);
            existing.Channels = candidate.Channels;
            existing.ExpiresOnDay = candidate.ExpiresOnDay;
            return AimAdmission.Updated;
         }

         if (ForNpc(candidate.NpcId) != null) return AimAdmission.LordAlreadyHasAnAim;
         if (_aims.Count(a => SameText(a.SourceMod, candidate.SourceMod)) >= AimRules.MaxStandingAimsPerSource)
            return AimAdmission.SourceAtCapacity;

         _aims.Add(new StandingAim
         {
            Id = candidate.Id.Trim(),
            NpcId = candidate.NpcId.Trim(),
            SourceMod = candidate.SourceMod.Trim(),
            Want = want,
            PlayerPart = AimRules.BoundPlayerPart(candidate.PlayerPart),
            MissionId = Clean(candidate.MissionId),
            Channels = candidate.Channels,
            RegisteredOnDay = today,
            ExpiresOnDay = candidate.ExpiresOnDay
         });
         return AimAdmission.Accepted;
      }

      /// <summary>Removes an aim by id; false when none stood.</summary>
      public bool Retract(string id)
      {
         var aim = Find(id);
         return aim != null && _aims.Remove(aim);
      }

      /// <summary>The aim a lord holds, or null.</summary>
      public StandingAim ForNpc(string npcId)
         => string.IsNullOrWhiteSpace(npcId) ? null : _aims.FirstOrDefault(a => SameText(a.NpcId, npcId.Trim()));

      /// <summary>The aim with this id, or null.</summary>
      public StandingAim Find(string id)
         => string.IsNullOrWhiteSpace(id) ? null : _aims.FirstOrDefault(a => string.Equals(a.Id, id.Trim(), StringComparison.Ordinal));

      /// <summary>Records that the lord raised his aim with the player, through one channel, on a day.</summary>
      public bool MarkRaised(string id, int day, AimChannels channel)
      {
         var aim = Find(id);
         if (aim == null) return false;

         aim.TimesRaised++;
         aim.LastRaisedDay = day;
         aim.LastRaisedChannel = channel;
         return true;
      }

      /// <summary>Drops every aim whose last day is past; returns the dropped ones so the mod can tell their source.</summary>
      public IReadOnlyList<StandingAim> PruneExpired(int today)
      {
         var gone = _aims.Where(a => a.ExpiresOnDay.HasValue && a.ExpiresOnDay.Value < today).ToList();
         foreach (var aim in gone) _aims.Remove(aim);
         return gone;
      }

      private static string Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

      private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
   }
}
