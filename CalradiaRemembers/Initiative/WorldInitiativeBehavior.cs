// Code written by Gabriel Mailhot, 28/10/2026.
// The world's initiative, engine side: the one director every system asks before it comes to the player unasked (Gabriel,
// 28/10/2026: "Le regulateur devrait orchestrer a qui le tour pour que chaque systeme ait le droit de deranger le
// joueur"). A system asks at the door when it wants to come (MayKnock) and acts only when given the turn; it withdraws its
// request when its reason goes. The rule is pure (Logic/Initiative/WorldInitiativePolicy); this keeps the queue and the
// turns given under one additive save key, reads the moment from the live game, and tells the log why a system waits.
// The pace "Off" in the MCM leaves every system to pace itself as before.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Initiative;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Initiative
{
   /// <summary>What the director keeps across saves.</summary>
   internal sealed class WorldInitiativeState
   {
      public List<InitiativeRequest> Requests { get; set; } = new();
      public List<InitiativeGrant> Grants { get; set; } = new();
      public double KeptSinceHour { get; set; }
      public double LastSettledHour { get; set; } = double.MinValue;
      public double LastOwnTalkHour { get; set; } = double.MinValue;
   }

   /// <summary>Decides, for every system that would come to the player unasked, whether it is its turn.</summary>
   public sealed class WorldInitiativeBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and starts keeping turns from its first load.
      private const string SaveKey = "cr_world_initiative";

      /// <summary>The prefix of a request bound to the place the player stands in; such requests go when the player leaves.</summary>
      internal const string HerePrefix = "here:";

      /// <summary>Hours within which a conversation with a character the director just gave a turn is that turn, not the player's own.</summary>
      private const double TurnConversationHours = 72;

      public WorldInitiativeBehavior() => Instance = this;

      /// <summary>The live director, or null outside a campaign.</summary>
      public static WorldInitiativeBehavior Instance { get; private set; }

      /// <summary>True while a battle, a chat or a game conversation holds the player (set by the submodule: no cycle to Chat).</summary>
      internal static Func<bool> IsBusy;

      /// <summary>How many systems are at the player right now, e.g. a lord riding after them (set by the submodule).</summary>
      internal static Func<int> ActiveCount;

      internal WorldInitiativeState State { get; private set; } = new();

      /// <summary>The last decision taken, for cr.knocks.</summary>
      internal InitiativeDecision LastDecision { get; private set; }

      /// <summary>The key of the request the last decision was asked for, for cr.knocks.</summary>
      internal string LastAskedKey { get; private set; }

      private static double Now => CampaignTime.Now.ToHours;

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ =>
         {
            if (State.KeptSinceHour <= 0) State.KeptSinceHour = Now;
         });
         // A request bound to a place ("here:" keys: a word asked in the town) goes when the player leaves it.
         CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, (party, _) =>
         {
            if (party != null && party == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty) State.Requests.RemoveAll(r => r.Key.StartsWith(HerePrefix, StringComparison.Ordinal));
         });
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, e =>
         {
            if (e != null && (e.IsPlayerMapEvent || e.InvolvedParties.Any(p => p == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty))) NoteSettled();
         });
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            Prune();
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Serialize(State)); }
            catch (Exception ex)
            {
               ModLog.Error($"[INITIATIVE] serialize failed, saving no turns: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[INITIATIVE] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before the director */
            }

            State = bytes is {Length: > 0} ? Deserialize(Encoding.UTF8.GetString(bytes)) ?? new WorldInitiativeState() : new WorldInitiativeState();
         }
      }

      /// <summary>The state as saved (JSON), and read back: the self-test proves the round trip.</summary>
      internal static string Serialize(WorldInitiativeState state) => JsonConvert.SerializeObject(state, Formatting.None);

      /// <summary>Reads a saved state, or null when it cannot be read.</summary>
      internal static WorldInitiativeState Deserialize(string json)
      {
         try { return JsonConvert.DeserializeObject<WorldInitiativeState>(json); }
         catch (Exception ex)
         {
            ModLog.Error($"[INITIATIVE] load failed, starting a fresh queue: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   A system asks whether it may come to the player now. Files or refreshes its request (same key: the first filing
      ///   hour is kept), asks the rule, and returns true only when this request is given the turn, which is then recorded.
      ///   With the pace Off, every system paces itself as before: always true, nothing recorded. Never throws: on a fault
      ///   it answers true, so a director bug never silences the world.
      /// </summary>
      internal bool MayKnock(string key, InitiativeFamily family, InitiativeRank rank, Hero character = null, bool isLetter = false)
      {
         try
         {
            if (string.IsNullOrWhiteSpace(key) || ModSettings.WorldInitiativeOff) return true;

            double now = Now;
            if (State.KeptSinceHour <= 0) State.KeptSinceHour = now;
            InitiativeRequest old = State.Requests.FirstOrDefault(r => r.Key == key);
            var request = new InitiativeRequest {
               Key = key, Family = family, Rank = rank, CharacterId = character?.StringId, IsLetter = isLetter,
               FiledAtHour = old?.FiledAtHour ?? now, LastAskedAtHour = now
            };
            if (old != null) State.Requests.Remove(old);
            State.Requests.Add(request);

            InitiativeSettings settings = ModSettings.WorldInitiative;
            InitiativeDecision d = WorldInitiativePolicy.Decide(State.Requests, State.Grants, settings, MomentAt(now), MBRandom.RandomFloat);
            LastDecision = d;
            LastAskedKey = key;

            foreach (InitiativeRequest gone in d.Expired)
            {
               State.Requests.Remove(gone);
               ModLog.Info($"[INITIATIVE] {gone.Key} ({gone.Family}) waited too long and is dropped.");
            }

            if (d.Verdict == InitiativeVerdict.Granted && d.Granted?.Key == key)
            {
               State.Requests.Remove(request);
               State.Grants.Add(new InitiativeGrant {Key = key, Family = family, CharacterId = request.CharacterId, GrantedAtHour = now, Cost = d.Cost});
               ModLog.Info($"[INITIATIVE] {key} ({family}, {rank}) has its turn ({d.Ground}).");

               return true;
            }

            string why = d.Verdict == InitiativeVerdict.Granted ? $"it is {d.Granted?.Key}'s turn" : d.Verdict.ToString();
            ModLog.Info($"[INITIATIVE] {key} ({family}, {rank}) waits: {why}.");

            return false;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[INITIATIVE] {key} asked and the director failed, so it may come: {ex.GetType().Name}: {ex.Message}");

            return true;
         }
      }

      /// <summary>A system's reason went (the player left the town, the matter was settled): its request leaves the queue.</summary>
      internal void Withdraw(string key)
      {
         if (string.IsNullOrWhiteSpace(key)) return;
         State.Requests.RemoveAll(r => r.Key == key);
      }

      /// <summary>The player settled: a battle, a scene or a conversation ended. The settling delay counts from here.</summary>
      internal void NoteSettled() => State.LastSettledHour = Now;

      /// <summary>
      ///   A conversation opened with <paramref name="npcId" />. If the director gave a turn that brings this character in the
      ///   last three days, it is that turn; otherwise the player opened it themselves, and the world gives them a quiet.
      /// </summary>
      internal void NoteConversationStarted(string npcId)
      {
         double now = Now;
         bool directorsTurn = !string.IsNullOrEmpty(npcId)
                              && State.Grants.Any(g => g.CharacterId == npcId && now - g.GrantedAtHour <= TurnConversationHours);
         if (!directorsTurn) State.LastOwnTalkHour = now;
      }

      /// <summary>The moment as the rule reads it.</summary>
      internal InitiativeMoment MomentAt(double now)
         => new() {
            NowHour = now,
            Busy = IsBusy?.Invoke() == true,
            HoursSinceSettled = State.LastSettledHour == double.MinValue ? double.MaxValue : now - State.LastSettledHour,
            HoursSinceOwnTalk = State.LastOwnTalkHour == double.MinValue ? double.MaxValue : now - State.LastOwnTalkHour,
            ActiveCount = ActiveCount?.Invoke() ?? 0,
            KeptSinceHour = State.KeptSinceHour
         };

      // Turns older than the longest window the rule reads (the fair month, the budget) say nothing more.
      private void Prune()
      {
         InitiativeSettings s = ModSettings.WorldInitiative;
         double keepHours = (Math.Max(s.FairTurnDays, s.WindowDays) + 1) * 24.0;
         double now = Now;
         State.Grants.RemoveAll(g => now - g.GrantedAtHour > keepHours);
         State.Requests.RemoveAll(r => r.Rank != InitiativeRank.Imposed && now - r.FiledAtHour > s.ExpireDays * 24.0);
      }
   }
}
