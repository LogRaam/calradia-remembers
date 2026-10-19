// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "sponsor_ward", Kimi's design,
// MINIMAL v1, adopted per this task's own scope note): a seated clan member (Family or ClanLords) takes a young
// companion of the player's own clan under their wing as a mentor-protege bond. ADOPTION WARNING (read first,
// Kimi's own review): this is NEVER a change of parentage, heritage, clan membership, or the vanilla family tree
// - Hero.Mother/Father/Clan are never touched here, only a mod-tracked LINK (mirrors CouncilStipendBehavior's/
// CouncilTributeBehavior's/CouncilHostageBehavior's own SyncData shape exactly: byte[] JSON via Newtonsoft.Json,
// key "CalradiaRemembers_CouncilWards_v1bytes", idempotent load, graceful on missing/corrupt, additive save key).
//
// MECHANIC (this task's own MINIMAL v1 scope, narrower than Kimi's fuller design - see this file's own final
// paragraph for what is deliberately NOT built): a bond has NO term (unlike CouncilStipend/CouncilTribute/
// CouncilHostage's own fixed-day tranches, see CouncilWard's own header) - it persists until dissolved by the
// world itself (either party dying or leaving the player's own clan), never by a countdown. Each daily tick, for
// every active bond where BOTH the mentor and the protege are alive and travelling in the SAME live party
// (mentor.PartyBelongedTo == protege.PartyBelongedTo, non-null - the "serving in the same party" condition this
// task's own brief names), the protege earns a small flat skill-XP bonus in TWO skills: Leadership (a fixed
// choice - the one skill every mentorship plausibly teaches, command and responsibility) plus the mentor's OWN
// single highest-value skill among the other seventeen (whatever the mentor is best at is what they pass on),
// see SelectMentorSpecialty's own doc for the exact tie-break. WardPolicy.DailyXpPerSkill (2 XP/day/skill) is
// deliberately far below CaptiveEscapeCoordinator's own one-time GrantEscapeXp amounts (15 XP for a real feat):
// this is a slow, ambient trickle over an indefinite bond, not a one-time reward, so it must never out-pace
// training a companion through ordinary play. isAffectedByFocusFactor stays true (the protege's own invested
// Focus Points still matter, exactly like every other skill gain in the game) and shouldNotify is false (a
// silent background trickle, mirroring CaptiveEscapeCoordinator's own GrantEscapeXp notify:false choice - a
// single mentorship day is not itself an event worth a popup; the player can always check the encyclopedia).
//
// DELIBERATELY NOT BUILT THIS SLICE (documented follow-ups, COUNCIL_ACTIONS.md's own fuller Kimi design): (b) the
// mentor is not made half-liable for the protege's own grudges (GrudgePolicy's own modulation pattern would be
// the natural home); (c) sponsorship is not (yet) a bridge toward arrange_marriage. Both are real, deliberately
// deferred enhancements, not silently dropped - see ResolutionKind.SponsorWard's own doc.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.Assemblies;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists active sponsor_ward mentorship bonds and grants the protege a modest daily skill-XP bonus while they serve alongside their mentor.</summary>
    public sealed class CouncilWardBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilWards_v1bytes";

        private static SkillObject[] _selectableSpecialties;

        /// <summary>
        ///   The daily bonus (see this file's own header) is split across exactly these two skills: a fixed
        ///   universal one plus the mentor's own specialty (see SelectMentorSpecialty). Built LAZILY on first use
        ///   (a daily tick, long after load), never in a static field initializer: the ctor's Instance= write
        ///   triggers this type's initializer, and touching DefaultSkills there crashed the game when LOADING a
        ///   save (TypeInitializationException), because the engine's skill registry is not yet populated when
        ///   OnGameStart adds this behavior on the load path, unlike a fresh campaign where it already is.
        /// </summary>
        private static SkillObject[] SelectableSpecialties => _selectableSpecialties ??= new[]
        {
            DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm, DefaultSkills.Bow,
            DefaultSkills.Crossbow, DefaultSkills.Throwing, DefaultSkills.Riding, DefaultSkills.Athletics,
            DefaultSkills.Crafting, DefaultSkills.Scouting, DefaultSkills.Tactics, DefaultSkills.Roguery,
            DefaultSkills.Charm, DefaultSkills.Trade, DefaultSkills.Steward, DefaultSkills.Medicine,
            DefaultSkills.Engineering
        };

        public static CouncilWardBehavior Instance;

        private List<CouncilWard> _active = new List<CouncilWard>();

        public CouncilWardBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>True once <paramref name="heroId" /> is already the protege of a live bond: sponsor_ward's own "one mentor at a time" rule (Kimi's own condition).</summary>
        public bool IsProtege(string heroId) => !string.IsNullOrEmpty(heroId) && _active.Any(w => w.ProtegeId == heroId);

        /// <summary>Council Tier 3: the live ward bond where this hero is the PROTEGE, or null. Additive read-only accessor for the encyclopedia panel (see CouncilAccordPanelText).</summary>
        public CouncilWard GetAsProtege(string heroId)
            => string.IsNullOrEmpty(heroId) ? null : _active.Find(w => w.ProtegeId == heroId);

        /// <summary>Council Tier 3: the live ward bond where this hero is the MENTOR, or null.</summary>
        public CouncilWard GetAsMentor(string heroId)
            => string.IsNullOrEmpty(heroId) ? null : _active.Find(w => w.MentorId == heroId);

        /// <summary>Dev/test only (cr.seed_accord): inject a live ward bond directly, bypassing eligibility, so the encyclopedia panel can be validated without a council playthrough.</summary>
        public void DebugSeed(string mentorId, string protegeId)
        {
            if (string.IsNullOrEmpty(protegeId)) return;
            _active.Add(new CouncilWard {MentorId = mentorId, ProtegeId = protegeId});
        }

        /// <summary>
        ///   Starts a new mentorship bond: <paramref name="protege" /> must STILL pass WardEligibilityPolicy right
        ///   now (bridge is law - re-checked here even though the caller, CouncilLift.SettleSponsorWard, already
        ///   resolved them through the SAME eligible pool via WardCandidateResolver). No cost and no term (see
        ///   this file's own header): the bond simply begins. Refuses (with a player-facing
        ///   <paramref name="failReason" />) when the mentor or protege cannot be found, or the protege no longer
        ///   passes eligibility.
        /// </summary>
        public bool TryStart(Hero mentor, Hero protege, out string failReason)
        {
            failReason = null;

            if (!Mapping.HeroPresence.IsInTheWorld(mentor))
            {
                failReason = "they could not be found.";

                return false;
            }

            if (!Mapping.ActionGate.Allows(protege, Logic.ActionKind.Ward))
            {
                failReason = "the named companion could not be found.";

                return false;
            }

            if (!WardCandidateResolver.EligibleWardsOf(mentor).Contains(protege))
            {
                failReason = "they are not an eligible companion to take on as a ward right now.";

                return false;
            }

            _active.Add(new CouncilWard {MentorId = mentor.StringId, ProtegeId = protege.StringId});

            ModLog.Info($"[WARD] {mentor.Name} takes {protege.Name} under their wing as a protege.");

            return true;
        }

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
                    ModLog.Error($"[WARD] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[WARD] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active bonds, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilWard>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilWard>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilWard>();
                        ModLog.Error($"[WARD] load failed - dropping any in-flight bonds so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilWard>();

                ModLog.Info($"[WARD] loaded {_active.Count} active mentorship bond(s) from save.");
            }
        }

        #region private

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilWard w in _active.ToList())
            {
                CouncilWard bond = w;
                // One bond's advance throwing must not stall every other bond's tick, mirroring
                // CouncilStipendBehavior.OnDailyTick's/CouncilTributeBehavior.OnDailyTick's/CouncilHostageBehavior.OnDailyTick's
                // own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilWardBehavior.Advance({bond.ProtegeId})", () => Advance(bond));
            }
        }

        /// <summary>
        ///   Drops the bond if either the mentor or the protege has died or left the player's own clan (this
        ///   task's own brief: "drop the bond if either dies or leaves the clan"); otherwise, when both are alive
        ///   and travelling in the SAME live party, grants the daily mentorship skill-XP bonus (see this file's
        ///   own header).
        /// </summary>
        private void Advance(CouncilWard w)
        {
            Hero mentor = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == w.MentorId);
            Hero protege = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == w.ProtegeId);

            if (mentor == null || protege == null)
            {
                _active.Remove(w);
                ModLog.Info("[WARD] bond ended: the mentor or the protege could no longer be found.");

                return;
            }

            if (mentor.Clan != Clan.PlayerClan || protege.Clan != Clan.PlayerClan)
            {
                _active.Remove(w);
                ModLog.Info($"[WARD] bond between {mentor.Name} and {protege.Name} ended: one of them left the clan.");

                return;
            }

            if (mentor.PartyBelongedTo == null || mentor.PartyBelongedTo != protege.PartyBelongedTo) return;

            GrantMentorshipXp(mentor, protege);
        }

        /// <summary>The daily bonus itself (see this file's own header): Leadership plus the mentor's own highest-value OTHER skill, WardPolicy.DailyXpPerSkill each, silently (shouldNotify: false).</summary>
        private static void GrantMentorshipXp(Hero mentor, Hero protege)
        {
            HeroDeveloper developer = protege.HeroDeveloper;
            if (developer == null) return;

            developer.AddSkillXp(DefaultSkills.Leadership, WardPolicy.DailyXpPerSkill, true, false);

            SkillObject specialty = SelectMentorSpecialty(mentor);
            // Leadership is always granted above; skip it here so the protege is never double-dipped on the
            // same skill when it also happens to be the mentor's own single best (guarantees two DISTINCT
            // skills are taught whenever the mentor has any OTHER skill at all).
            if (specialty != null && specialty != DefaultSkills.Leadership)
                developer.AddSkillXp(specialty, WardPolicy.DailyXpPerSkill, true, false);
        }

        /// <summary>
        ///   The mentor's own single highest-value skill among the seventeen skills OTHER than Leadership
        ///   (SelectableSpecialties), so "the mentor's own best skill" (this task's own brief) is always a
        ///   DIFFERENT skill from the fixed Leadership grant. Ties break on SelectableSpecialties' own fixed
        ///   enumeration order (whichever tied skill is listed first) - an arbitrary but stable and deterministic
        ///   tie-break, never a random one, so the same mentor always teaches the same specialty.
        /// </summary>
        private static SkillObject SelectMentorSpecialty(Hero mentor)
        {
            SkillObject best = null;
            int bestValue = -1;

            foreach (SkillObject skill in SelectableSpecialties)
            {
                int value = mentor.GetSkillValue(skill);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = skill;
                }
            }

            return best;
        }

        #endregion
    }
}
