// Code written by Gabriel Mailhot, 06/09/2026.
// Two player reports of the CR chat option failing to reach the player, neither reproducible on demand (the
// two reports are named in full in CalradiaRemembers.Logic.DialogueEntryTracePolicy, which composes the line
// this collector emits). A console command is nearly useless here: it needs the player to reproduce AND to
// think of running it at that instant. So this collects silently on every hero conversation and writes ONE
// line to the CR log at the end of it.
// Follows the exact engine facts VanillaDialogueRecorder already learned the hard way: ConversationBegin
// does NOT fire reliably in this engine build, ConsequenceRunned is the one event proven to fire, and
// ConversationEnd (proven reliable) is used only to arm a deferred reset rather than clear immediately, so a
// second conversation with the SAME partner still starts a fresh trail (the recorder's own documented gap).
// At ConversationEnd the active token has typically already moved to the terminal close_window state, which
// alone would tell us nothing, so the trail accumulates the DISTINCT token names seen along the way instead
// of the final one.

#region

using System;
using System.Collections.Generic;
using System.Reflection;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

#endregion

namespace CalradiaRemembers.Dialogue
{
   /// <summary>
   ///   Collects the dialogue-hub token trail of a hero conversation and, at its end, writes ONE
   ///   <see cref="DialogueEntryTracePolicy" /> line to the CR log naming the partner, whether the CR entry
   ///   option was ever offered, and the tokens the conversation actually traversed. See the file header
   ///   for the engine facts this depends on.
   /// </summary>
   public class DialogueEntryTraceCollector
   {
      private static bool _stateMapUnavailable;
      private static FieldInfo _stateMapField;

      private readonly List<string> _trail = new();
      private readonly HashSet<string> _trailSeen = new();
      private bool _clearBeforeNextCapture;
      private Hero _currentPartnerHero;
      private bool _entryOffered;

      // Same partner-change detection VanillaDialogueRecorder uses, and for the same reason:
      // ConversationBegin is not reliable enough in this engine build to reset on.
      private CharacterObject _lastCapturePartner;
      private bool _subscribed;

      /// <summary>
      ///   Called from <see cref="LlmDialogFlow.IsHeroEligible" />. Its EXECUTION is the proof that the
      ///   engine evaluated CR's player line condition while sitting at one of the two hubs CR is
      ///   registered on (<see cref="LlmDialogFlow.EntryHubTokens" />); this records that fact for the
      ///   conversation currently being traced, whatever IsHeroEligible then goes on to return.
      /// </summary>
      public void NoteEntryOffered()
      {
         if (!ModLog.AnyChannelEnabled) return;

         _entryOffered = true;
      }

      /// <summary>
      ///   Subscribes to the ConversationManager events. Must be called once the campaign is live (during
      ///   behavior RegisterEvents), mirroring VanillaDialogueRecorder.Subscribe.
      /// </summary>
      public void Subscribe()
      {
         if (_subscribed)
         {
            ModLog.Info("DialogueEntryTraceCollector.Subscribe called twice, ignored.");

            return;
         }

         ConversationManager mgr = Campaign.Current?.ConversationManager;
         if (mgr == null)
         {
            ModLog.Error("DialogueEntryTraceCollector.Subscribe: ConversationManager not available.");

            return;
         }

         mgr.ConsequenceRunned += OnConsequenceRunned;
         mgr.ConversationEnd += OnConversationEnd;

         _subscribed = true;
         ModLog.Info("DialogueEntryTraceCollector subscribed to ConversationManager events.");
      }

      #region private

      private void OnConsequenceRunned(ConversationSentence sentence)
      {
         try
         {
            // Logging is off by default for Nexus: skip the reflection and trail bookkeeping below
            // entirely rather than build state that ModLog would only throw away.
            if (!ModLog.AnyChannelEnabled) return;

            ConversationManager mgr = Campaign.Current?.ConversationManager;
            if (mgr == null) return;

            CharacterObject partner = mgr.OneToOneConversationCharacter;
            if (_clearBeforeNextCapture || !ReferenceEquals(partner, _lastCapturePartner))
            {
               _trail.Clear();
               _trailSeen.Clear();
               _entryOffered = false;
               _clearBeforeNextCapture = false;
               _lastCapturePartner = partner;
               _currentPartnerHero = partner?.HeroObject;
            }

            AppendToken(ResolveTokenName(mgr, mgr.ActiveToken));
         }
         catch (Exception ex)
         {
            ModLog.Error($"DialogueEntryTraceCollector.OnConsequenceRunned threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void OnConversationEnd()
      {
         try
         {
            if (!ModLog.AnyChannelEnabled) return;

            // Null when this conversation was never with a hero (a common trooper, a tavernkeeper) or
            // when ConsequenceRunned never fired even once for it: CR could never have applied either way,
            // so there is nothing worth a log line about.
            if (_currentPartnerHero == null) return;

            string kind = _currentPartnerHero.Occupation.ToString().ToLowerInvariant();
            string partnerLabel = HeroLabel.Describe(
               _currentPartnerHero.Name?.ToString(),
               _currentPartnerHero.Clan?.Name?.ToString(),
               _currentPartnerHero.Culture?.Name?.ToString());

            ModLog.Info(DialogueEntryTracePolicy.ComposeLine(partnerLabel, kind, _entryOffered, _trail, LlmDialogFlow.EntryHubTokens));
         }
         catch (Exception ex)
         {
            ModLog.Error($"DialogueEntryTraceCollector.OnConversationEnd threw {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            // Deferred, not immediate: mirrors VanillaDialogueRecorder so a second conversation with the
            // SAME partner (the partner-change check alone would miss it) still starts a fresh trail.
            _clearBeforeNextCapture = true;

            // The partner, though, is dropped EAGERLY. Unlike the recorder's buffer nothing reads this
            // after the line is written, and holding it would let a conversation that never fired a single
            // ConsequenceRunned re-emit the PREVIOUS conversation's partner and trail as if they were its
            // own. A diagnostic that reports the wrong conversation is worse than one that stays silent.
            _currentPartnerHero = null;
         }
      }

      private void AppendToken(string token)
      {
         if (string.IsNullOrEmpty(token)) return;
         if (_trail.Count >= DialogueEntryTracePolicy.MaxTrailTokens) return;
         if (!_trailSeen.Add(token)) return; // ordered, DISTINCT: a hub revisited several times still counts once

         _trail.Add(token);
      }

      /// <summary>
      ///   Reverse-maps a <see cref="ConversationManager.ActiveToken" /> value to its registered name via
      ///   the private <c>stateMap</c> field. Resolved BY NAME and cached; if the field is absent or not the
      ///   expected shape (a future game patch), this reports the raw token index instead of the name,
      ///   rather than throwing or going silent, and says so exactly once per session via
      ///   <see cref="ModLog.Compat" />. Takes the token explicitly (not read off <paramref name="mgr" />)
      ///   so a self-test can round-trip an arbitrary index without touching the live conversation state.
      /// </summary>
      private static string ResolveTokenName(ConversationManager mgr, int token)
      {
         try
         {
            Dictionary<string, int> stateMap = ReadStateMap(mgr);
            if (stateMap == null)
            {
               ReportStateMapUnavailable(null);

               return $"#{token}";
            }

            foreach (KeyValuePair<string, int> entry in stateMap)
               if (entry.Value == token)
                  return entry.Key;

            return $"#{token}";
         }
         catch (Exception ex)
         {
            ReportStateMapUnavailable(ex);

            return $"#{token}";
         }
      }

      private static Dictionary<string, int> ReadStateMap(ConversationManager mgr)
      {
         if (!_stateMapUnavailable && _stateMapField == null)
            _stateMapField = typeof(ConversationManager).GetField("stateMap", BindingFlags.NonPublic | BindingFlags.Instance);

         return _stateMapField?.GetValue(mgr) as Dictionary<string, int>;
      }

      private static void ReportStateMapUnavailable(Exception ex)
      {
         if (_stateMapUnavailable) return;

         _stateMapUnavailable = true;
         string reason = ex == null ? "field not found" : $"{ex.GetType().Name}: {ex.Message}";
         ModLog.Compat($"DialogueEntryTraceCollector: could not read ConversationManager.stateMap by reflection ({reason}); the dialogue token trail will report raw indices instead of names.");
      }

      /// <summary>
      ///   Test-only seam (cr.testall): forward-resolves a token NAME to its registered index using the
      ///   exact reflection path <see cref="ResolveTokenName" /> reads, so the self-test drives the real
      ///   lookup rather than a parallel copy. Null when the token is unknown or reflection is unavailable.
      /// </summary>
      internal static int? DebugResolveTokenIndex(ConversationManager mgr, string tokenName)
      {
         try
         {
            Dictionary<string, int> stateMap = ReadStateMap(mgr);

            return stateMap != null && stateMap.TryGetValue(tokenName, out int index) ? index : (int?) null;
         }
         catch { return null; }
      }

      /// <summary>Test-only seam (cr.testall): exposes the private reverse-lookup for a self-test to call directly.</summary>
      internal static string DebugResolveTokenName(ConversationManager mgr, int token) => ResolveTokenName(mgr, token);

      #endregion
   }
}
