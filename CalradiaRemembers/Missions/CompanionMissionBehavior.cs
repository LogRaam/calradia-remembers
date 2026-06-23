// Code written by Gabriel Mailhot, 23/06/2026.
// Companion missions (ROADMAP "Also planned") — engine glue for the first slice, "bring me the news":
// dispatch a companion → a timer runs → they return with a report drawn from the world-event store. The
// OUTCOME (success band, duration, report size) is the pure CalradiaRemembers.Logic; this only moves data
// between the game and that logic, persists the in-flight errands, and announces the report.
//
// MVP SCOPE (deliberate, flagged for next pass with Gabriel's in-game eyes):
//   - Timer-only: the companion is NOT physically removed from / returned to the party yet.
//   - Templated report (no LLM framing yet) — the seam is BuildReport.
//   - Console-driven dispatch (cr.send_news) before any in-conversation gather_news action.
//   - Only GatherNews is wired; the resolver/duration already cover the other errands.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
    /// <summary>
    ///   Tracks the errands companions are away on and resolves each on its return day. Persisted as a
    ///   UTF-8 byte[] (no native string ceiling, same "never brick a save" degradation as the world-event
    ///   store). The decision logic is pure (<see cref="CompanionMissions"/>); this is the glue.
    /// </summary>
    public sealed class CompanionMissionBehavior : CampaignBehaviorBase
    {
        public static CompanionMissionBehavior Instance;

        private const string SaveKey = "CalradiaRemembers_CompanionMissions_v1bytes";

        // Tuning (flagged for ratification — Gabriel adjusts by in-game feel).
        private const int NewsDifficulty   = 30; // a "bring me the news" errand's baseline difficulty
        private const int MinNewsMagnitude = 35; // skip trivial happenings from a news report

        private List<CompanionMission> _active = new List<CompanionMission>();

        public CompanionMissionBehavior() => Instance = this;

        public int ActiveCount => _active.Count;

        // ── Dispatch ──────────────────────────────────────────────────────────

        /// <summary>
        ///   Sends a companion on an errand, returning a player-facing message. Refuses (with a reason)
        ///   when the hero is not a usable companion, is already away, or is too green in the governing
        ///   skill. MVP: only <see cref="CompanionMissionType.GatherNews"/> is wired.
        /// </summary>
        public string Dispatch(Hero companion, CompanionMissionType type)
        {
            if (companion == null || companion == Hero.MainHero) return "No companion to send.";
            if (!IsUsableCompanion(companion)) return $"{companion.Name} is not one of your companions to send.";
            if (HasActiveMission(companion.StringId)) return $"{companion.Name} is already away on an errand.";

            int skill = GoverningSkillValue(companion, type);
            if (!CompanionMissions.CanAttempt(skill))
                return $"{companion.Name} is too unschooled in {CompanionMissions.GoverningSkill(type)} for that errand.";

            int today    = Today;
            int duration = CompanionMissions.DurationDays(type, NewsDifficulty);

            _active.Add(new CompanionMission
            {
                Id            = Guid.NewGuid().ToString("N"),
                CompanionId   = companion.StringId,
                CompanionName = companion.Name?.ToString() ?? "Your companion",
                Type          = type,
                DispatchDay   = today,
                ReturnDay     = today + duration,
                Difficulty    = NewsDifficulty
            });

            ModLog.Info($"[MISSION] dispatched {companion.Name} on {type} (returns day {today + duration}).");
            return $"{companion.Name} rides out to gather word. Expect their report in {duration} days.";
        }

        // ── Resolution ────────────────────────────────────────────────────────

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;
            int today = Today;

            // Resolve a copy so the announcement can mutate the list safely.
            foreach (CompanionMission m in _active.Where(m => m.ReturnDay <= today).ToList())
            {
                string report = Resolve(m, out Hero companion);
                _active.Remove(m);
                Announce(companion, report);
            }
        }

        /// <summary>Force-resolves a named companion's errand NOW (console test path); returns its report.</summary>
        public string ForceResolve(Hero companion)
        {
            if (companion == null) return "No companion given.";
            CompanionMission m = _active.FirstOrDefault(x => x.CompanionId == companion.StringId);
            if (m == null) return $"{companion.Name} is not away on any errand.";

            string report = Resolve(m, out Hero _);
            _active.Remove(m);
            Announce(companion, report);
            return report;
        }

        /// <summary>Rolls the errand's outcome and builds its report. Out: the resolved companion hero (may be null/dead).</summary>
        private string Resolve(CompanionMission m, out Hero companion)
        {
            companion = HeroById(m.CompanionId);
            int skill = companion != null ? GoverningSkillValue(companion, m.Type) : 0;
            int roll  = MBRandom.RandomInt(0, 100);

            CompanionMissionOutcome outcome = CompanionMissions.Resolve(m.Type, skill, roll, m.Difficulty);
            ModLog.Info($"[MISSION] {m.CompanionName} {m.Type} resolved: skill {skill}, roll {roll}, diff {m.Difficulty} → {outcome.Result}{(outcome.CompanionCaptured ? " (CAPTURED)" : "")}.");

            return BuildReport(m, outcome);
        }

        // ── Report (templated — the LLM-framing seam) ─────────────────────────

        private string BuildReport(CompanionMission m, CompanionMissionOutcome outcome)
        {
            string name = m.CompanionName ?? "Your companion";

            if (outcome.Result == Logic.MissionResult.Failure)
                return outcome.CompanionCaptured
                    ? $"{name} has not returned — word is they were taken on the road."
                    : $"{name} returns empty-handed; the roads gave up nothing worth telling.";

            WorldEventStore store = WorldEventStore.Instance;
            int today = Today;
            var found = store?.GetRecentSignificant(today, /*max*/ 4, MinNewsMagnitude) ?? new List<WorldEvent>();
            int count = CompanionMissions.NewsItemsForResult(outcome.Result, found.Count);

            if (count == 0)
                return $"{name} returns, but Calradia has been quiet of late — little worth reporting.";

            var sb = new StringBuilder();
            sb.AppendLine(outcome.Result == Logic.MissionResult.Success
                ? $"{name} returns with a full account of the realm's doings:"
                : $"{name} returns with what little they could gather:");
            foreach (WorldEvent e in found.Take(count))
                sb.AppendLine($"- {e.Summary.TrimEnd('.', ' ')}.");

            return sb.ToString().TrimEnd();
        }

        private static void Announce(Hero companion, string report)
        {
            if (string.IsNullOrWhiteSpace(report)) return;
            string headline = report.Split('\n')[0];
            MBInformationManager.AddQuickInformation(new TextObject(headline), 0, companion?.CharacterObject);
            ModLog.Info($"[MISSION] report:\n{report}");
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[MISSION] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[MISSION] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch { /* key absent in older saves */ }

                if (bytes != null && bytes.Length > 0)
                    try { _active = JsonConvert.DeserializeObject<List<CompanionMission>>(Encoding.UTF8.GetString(bytes)) ?? new List<CompanionMission>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CompanionMission>();
                        ModLog.Error($"[MISSION] load failed — dropping in-flight errands so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }

                ModLog.Info($"[MISSION] loaded {_active.Count} in-flight errand(s) from save.");
            }
        }

        // ── Diagnostics (console) ─────────────────────────────────────────────

        /// <summary>A one-line-per-errand summary of what is currently in flight.</summary>
        public string DescribeActive()
        {
            if (_active.Count == 0) return "No companions are away on errands.";
            int today = _active.Count > 0 ? Today : 0;
            return string.Join("\n", _active.Select(m =>
                $"- {m.CompanionName}: {m.Type}, returns day {m.ReturnDay} ({Math.Max(0, m.ReturnDay - today)} day(s) left)."));
        }

        #region private

        private static int Today => (int) CampaignTime.Now.ToDays;

        private static bool IsUsableCompanion(Hero h)
            => h != null && h.IsAlive && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan);

        private bool HasActiveMission(string companionId)
            => _active.Any(m => m.CompanionId == companionId);

        private static Hero HeroById(string id)
            => string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        /// <summary>The companion's level in the skill that governs this errand.</summary>
        private static int GoverningSkillValue(Hero companion, CompanionMissionType type)
        {
            SkillObject skill;
            switch (CompanionMissions.GoverningSkill(type))
            {
                case MissionSkill.Roguery: skill = DefaultSkills.Roguery; break;
                case MissionSkill.Trade:   skill = DefaultSkills.Trade; break;
                case MissionSkill.Charm:   skill = DefaultSkills.Charm; break;
                default:                   skill = DefaultSkills.Scouting; break;
            }
            return companion.GetSkillValue(skill);
        }

        #endregion
    }
}
