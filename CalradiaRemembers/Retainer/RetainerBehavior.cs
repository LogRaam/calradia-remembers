// Code written by Gabriel Mailhot, 11/08/2026.
// RETAINER (the idle-lord half of "a lord accompanies you", the complement of the follow_me escort). A lord/lady
// of ANOTHER clan rides IN the player's own party for a time, WITHOUT marriage and WITHOUT joining the player's
// clan, staying affiliated to their OWN clan, and breaks off cleanly (the author's rule: a war with their faction
// forces them to leave). This behavior is the persisted mechanism plus the observation/safety seam behind the
// player-facing ride_with_me / part_ways verbs (BannerlordGameStateBridge.ExecuteRideWithMe / ExecutePartWays).
//
// ON by default (<see cref="Enabled" />, default true): the party-screen/UI feel of an other-clan lord riding in
// the player's roster was validated in-game 2026-08-11 (he shows as a party member and leaves cleanly). The flag
// is kept so a player can still turn the path off with cr.retainer enable off; while disabled, the verbs are
// neither taught (EncounterContextBuilder never sets NpcCanRideWithPlayer) nor executed.
//
// TAKE / RELEASE (decompile-confirmed against the shipped TaleWorlds.CampaignSystem.dll):
//   * AddHeroToPartyAction.Apply(hero, MobileParty.MainParty) is the correct primitive: it removes the hero
//     from any prior party's MEMBER ROSTER, clears StayingInSettlement, strips a governorship, adds their
//     CharacterObject to the new party's roster, and dispatches OnHeroJoinedParty. It NEVER touches Hero.Clan
//     or Occupation - so a foreign Lord stays a Lord of their own clan, which is exactly the intent. It is NOT
//     AddCompanionAction (that would move them into the player's clan), which is deliberately avoided here.
//   * That primitive was built for wanderers/companions, so for a lord who currently LEADS their own party it
//     leaves that now-leaderless party orphaned (unlike RemoveCompanionAction, it does not retire it). We
//     therefore mirror vanilla's own leader-removal cleanup (RemoveCompanionAction's leader branch: SetMoveModeHold
//     + RethinkAtNextHourlyTick, then Disband if the party still has troops else Destroy) so no ghost party wanders.
//   * The reverse (release) is a bare roster removal: MainParty.MemberRoster.AddToCounts(character, -1). The
//     engine's own OnHeroRemoved -> OnRemovedFromParty sets PartyBelongedTo back to null, and vanilla's
//     ConsiderSpawningLordParties (MobilePartyHelper) then hands the now-partyless lord a fresh party on a later
//     daily tick - so we do NOT have to re-create their party ourselves; leaving is a clean hand-back.
//
// STABILITY: while the retainer sits in MainParty their PartyBelongedTo is MainParty (not null), and the vanilla
// AI spawn behavior only reclaims heroes whose PartyBelongedTo is null - so it never silently ejects a retainer.
// The member roster is saved by vanilla, so the arrangement PERSISTS across save/load with no re-add; this
// behavior tracks only the StringIds it needs for the war-leave rule, status and release (an additive, save-safe
// byte[] JSON key, mirroring FollowMeBehavior/CouncilStipendBehavior). Every engine read is guarded so a spike
// can never crash a tick.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Retainer
{
    /// <summary>Tracks foreign lords riding IN the player's party (the ride_with_me retainer), observes them daily, and releases on war.</summary>
    public sealed class RetainerBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_Retainers_v1bytes";

        /// <summary>
        ///   Master switch for the whole ride_with_me / part_ways player-facing path, DEFAULT false. When off, the
        ///   verb is neither taught (EncounterContextBuilder leaves NpcCanRideWithPlayer/NpcIsRidingWithPlayer
        ///   false) nor executed by the bridge; the raw take/release mechanism the console and self-test drive is
        ///   deliberately NOT gated by this, so the mechanism stays verifiable while the player path is held back
        ///   pending the author's in-game party-screen/UI check. Toggled via cr.retainer enable [on|off]. A plain
        ///   static (not persisted): an unusual, experimental path defaults off on every boot until switched on.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>
        ///   Shows the player a brief on-screen line when a retainer joins or leaves. Both are things the player
        ///   should always notice: a war breaking off their companion happens on its own, so a silent departure
        ///   would read as a bug. Best-effort and guarded.
        /// </summary>
        private static void NotifyPlayer(Hero lord, string text)
        {
            try { MBInformationManager.AddQuickInformation(new TextObject(text), 0, lord?.CharacterObject); }
            catch { /* a missing notification is never worth a thrown tick */ }
        }

        public static RetainerBehavior Instance;

        private List<string> _retainerIds = new List<string>();

        public RetainerBehavior()
        {
            Instance = this;
        }

        /// <summary>True when this hero (by StringId) is currently tracked as a retainer of the player's party.</summary>
        public bool IsRetainer(string heroId)
            => !string.IsNullOrEmpty(heroId) && _retainerIds.Contains(heroId);

        /// <summary>The StringIds of every lord currently riding with the player as a retainer (a snapshot copy, safe to enumerate).</summary>
        public IEnumerable<string> RetainerIds => _retainerIds.ToList();

        /// <summary>
        ///   TAKE: adds an other-clan lord/lady to <see cref="MobileParty.MainParty" /> via the reverse-engineered
        ///   correct call (<see cref="AddHeroToPartyAction" />), keeping their <see cref="Hero.Clan" /> unchanged,
        ///   retiring any party they had been leading (mirroring vanilla's own leader-removal cleanup), and tracking
        ///   them. Returns a full state report either way; refuses with a reason on any guard failure.
        /// </summary>
        public bool TryTake(Hero lord, out string report)
        {
            if (lord is not {IsAlive: true} || lord == Hero.MainHero || MobileParty.MainParty == null)
            {
                report = "they could not be found (or are the player).";

                return false;
            }

            if (lord.IsPrisoner)
            {
                report = $"{lord.Name} is a prisoner and cannot ride with you.";

                return false;
            }

            if (lord.Clan == Clan.PlayerClan)
            {
                report = $"{lord.Name} is already of your own clan - a retainer must stay in THEIR clan (use join_party/join_clan for your own).";

                return false;
            }

            if (IsRetainer(lord.StringId))
            {
                report = $"{lord.Name} is already riding with you as a retainer.";

                return false;
            }

            Clan originalClan = lord.Clan;
            MobileParty oldParty = lord.PartyBelongedTo;
            bool ledOwnParty = oldParty != null && oldParty != MobileParty.MainParty && oldParty.LeaderHero == lord;

            try
            {
                AddHeroToPartyAction.Apply(lord, MobileParty.MainParty, false);
                RetireOrphanedParty(oldParty, ledOwnParty);
            }
            catch (Exception ex)
            {
                report = $"the engine refused to add them: {ex.GetType().Name}: {ex.Message}";

                return false;
            }

            _retainerIds.Add(lord.StringId);
            NotifyPlayer(lord, $"{lord.Name} now rides at your side, keeping their own house and colours.");

            bool inMainParty = lord.PartyBelongedTo == MobileParty.MainParty;
            bool clanUnchanged = lord.Clan == originalClan && lord.Clan != Clan.PlayerClan;
            ModLog.Info($"[RETAINER] took {lord.Name} as a retainer (inMainParty={inMainParty}, clan={lord.Clan?.Name}, " +
                        $"clanUnchanged={clanUnchanged}, occupation={lord.CharacterObject?.Occupation}, ledOwnParty={ledOwnParty}).");

            report = $"{lord.Name} now rides in your party. inMainRoster={inMainParty}, clan={lord.Clan?.Name} " +
                     $"(unchanged={clanUnchanged}, NOT player clan), occupation={lord.CharacterObject?.Occupation}" +
                     (ledOwnParty ? ", their own party was retired (mirroring vanilla leader-removal)." : ".");

            return true;
        }

        /// <summary>
        ///   RELEASE: removes a tracked retainer from <see cref="MobileParty.MainParty" /> by the bare roster
        ///   removal the engine reverses cleanly (<see cref="Hero.PartyBelongedTo" /> returns to null), drops them
        ///   from tracking, and lets vanilla's own AI hand the now-partyless lord a fresh party. Refuses with a
        ///   reason when this hero is not currently a retainer.
        /// </summary>
        public bool TryRelease(Hero lord, out string report)
        {
            if (lord == null || !IsRetainer(lord.StringId))
            {
                report = "they are not currently riding with you as a retainer.";

                return false;
            }

            _retainerIds.Remove(lord.StringId);

            try
            {
                if (lord.PartyBelongedTo == MobileParty.MainParty)
                    MobileParty.MainParty.MemberRoster.AddToCounts(lord.CharacterObject, -1);
            }
            catch (Exception ex)
            {
                report = $"{lord.Name} was dropped from tracking but the engine threw removing them: {ex.GetType().Name}: {ex.Message}";

                return false;
            }

            bool stillInMainParty = lord.PartyBelongedTo == MobileParty.MainParty;
            ModLog.Info($"[RETAINER] released {lord.Name} (stillInMainParty={stillInMainParty}, partyless={lord.PartyBelongedTo == null}, clan={lord.Clan?.Name}).");

            report = $"{lord.Name} left your party. inMainRoster={stillInMainParty}, partyless={lord.PartyBelongedTo == null} " +
                     $"(vanilla will spawn them a lord party on a later tick), clan={lord.Clan?.Name}.";

            return true;
        }

        /// <summary>A multi-line status of every tracked retainer: clan, alive, at-war, and whether they are ACTUALLY still in MainParty.</summary>
        public string DescribeStatus()
        {
            if (_retainerIds.Count == 0) return "No lord is currently riding with you as a retainer.";

            var sb = new StringBuilder();
            sb.AppendLine($"{_retainerIds.Count} tracked retainer(s):");

            foreach (string id in _retainerIds)
            {
                Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);
                if (lord == null)
                {
                    sb.AppendLine($"  - {id}: NOT FOUND among living heroes (dead/removed) - will be dropped next tick.");

                    continue;
                }

                bool inMainParty = lord.PartyBelongedTo == MobileParty.MainParty;
                sb.AppendLine($"  - {lord.Name}: clan={lord.Clan?.Name} (playerClan={lord.Clan == Clan.PlayerClan}), " +
                              $"inMainParty={inMainParty}, alive={lord.IsAlive}, atWarWithPlayer={IsAtWarWithPlayer(lord)}.");
            }

            return sb.ToString();
        }

        /// <summary>
        ///   The observation + safety seam, shared by the daily tick and the self-test. For each tracked retainer
        ///   it LOGS whether they are still in MainParty and still of their own clan (the feasibility signal: if
        ///   vanilla ever ejected one, this surfaces it), then applies the author's leave rule via the pure
        ///   <see cref="RetainerLeavePolicy" /> (war with the player, or death, forces a release/drop).
        /// </summary>
        public void RunDailyObservation()
        {
            if (_retainerIds.Count == 0) return;

            foreach (string id in _retainerIds.ToList())
            {
                Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);
                bool alive = lord is {IsAlive: true};
                bool atWar = alive && IsAtWarWithPlayer(lord);
                bool inMainParty = alive && lord.PartyBelongedTo == MobileParty.MainParty;

                ModLog.Info($"[RETAINER-OBSERVE] {(lord?.Name?.ToString() ?? id)}: inMainParty={inMainParty}, " +
                            $"clan={lord?.Clan?.Name}, ownClanKept={alive && lord.Clan != Clan.PlayerClan}, alive={alive}, atWar={atWar}.");

                if (alive && !inMainParty && !atWar)
                {
                    ModLog.Info($"[RETAINER-EJECT] {lord.Name} is tracked but NO LONGER in MainParty - a vanilla behavior removed them. Dropping the stale track.");
                    NotifyPlayer(lord, $"{lord.Name} no longer rides with you.");
                }

                if (RetainerLeavePolicy.MustLeave(atWar, alive, IsRetainer(id)))
                {
                    if (alive)
                    {
                        if (atWar)
                            NotifyPlayer(lord, $"{lord.Name} breaks from your company: their people are now at war with yours.");

                        TryRelease(lord, out string _);
                        ModLog.Info($"[RETAINER] {lord.Name} released by rule ({(atWar ? "their faction is at war with the player" : "left the party")}).");
                    }
                    else
                    {
                        _retainerIds.Remove(id);
                        ModLog.Info($"[RETAINER] dropped dead/missing retainer {id} from tracking.");
                    }
                }
                else if (alive && !inMainParty)
                {
                    // Tracked, at peace, but no longer in the party (a vanilla eject): drop the stale id so status is honest.
                    _retainerIds.Remove(id);
                }
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_retainerIds, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[RETAINER] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[RETAINER] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no tracked retainers, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _retainerIds = JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(bytes)) ?? new List<string>(); }
                    catch (Exception ex)
                    {
                        _retainerIds = new List<string>();
                        ModLog.Error($"[RETAINER] load failed - dropping any tracked retainers so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _retainerIds = new List<string>();

                ModLog.Info($"[RETAINER] loaded {_retainerIds.Count} tracked retainer(s) from save.");
            }
        }

        #region private

        private static bool IsAtWarWithPlayer(Hero lord)
            => lord?.MapFaction != null && Hero.MainHero?.MapFaction != null &&
               FactionManager.IsAtWarAgainstFaction(lord.MapFaction, Hero.MainHero.MapFaction);

        private void OnDailyTick()
        {
            // One retainer's observation throwing must not stall the rest, mirroring FollowMeBehavior.OnDailyTick.
            SafeRun.Step("RetainerBehavior.RunDailyObservation", RunDailyObservation);
        }

        /// <summary>
        ///   On load, the member roster already carried the retainer(s) natively, so nothing is re-added; this only
        ///   LOGS the observed state (the feasibility signal for whether vanilla kept them) and lets the daily rule
        ///   run once so a war that was declared while this save sat on disk is honoured immediately.
        /// </summary>
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SafeRun.Step("RetainerBehavior.OnSessionLaunched", () =>
            {
                if (_retainerIds.Count == 0) return;
                ModLog.Info($"[RETAINER] session launched with {_retainerIds.Count} tracked retainer(s); re-observing (roster persisted natively, no re-add needed).");
                RunDailyObservation();
            });
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            // A war anywhere may be the one that puts a retainer's faction against the player. Rather than reason
            // about which faction is which, re-run the same pure gate over every tracked retainer.
            SafeRun.Step("RetainerBehavior.OnWarDeclared", RunDailyObservation);
        }

        /// <summary>
        ///   AddHeroToPartyAction removed the lord from their own party's roster and nulled its leader but, unlike
        ///   RemoveCompanionAction, did NOT retire that now-leaderless party. Mirror vanilla's own leader-removal
        ///   cleanup so no orphan party (owner set, no leader) is left wandering.
        /// </summary>
        private static void RetireOrphanedParty(MobileParty oldParty, bool ledOwnParty)
        {
            if (!ledOwnParty || oldParty == null || !oldParty.IsActive) return;

            try
            {
                oldParty.Ai.RethinkAtNextHourlyTick = true;
                oldParty.SetMoveModeHold();

                if (oldParty.MemberRoster.Count == 0) DestroyPartyAction.Apply(null, oldParty);
                else DisbandPartyAction.StartDisband(oldParty);
            }
            catch (Exception ex) { ModLog.Error($"[RETAINER] retiring the retainer's old party failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        #endregion
    }
}
