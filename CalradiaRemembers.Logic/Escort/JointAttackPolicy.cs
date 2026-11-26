// Code written by Gabriel Mailhot, 25/11/2026.
// froggyluv (Nexus, 25/11/2026): "a fellow Lord rightfully complained we had a powerful enemy party directly North of our
// position - she went to great lengths about exactly how we should attack her together. Once the conversation ended, she
// took off the other direction." CR had no deed for it: she spoke a plan the game never heard. Gabriel (25/11/2026): "un
// Lord qui accepte d'attaquer avec le joueur devrait creer une armee avec le groupe du joueur et celui du NPC. C'est le
// Lord le plus puissant qui dirige l'armee. L'armee doit servir a attaquer la bonne cible (party, ville, village, chateau,
// bateau, etc)."
//
// This decides whether a lord can join the player in such an army at all, who leads it, what kind of army the target
// makes it, and when it is over. The engine's armies belong to ONE kingdom (Kingdom.CreateArmy, decompiled 1.5.7), so a
// lord and a player of different realms cannot share one, even allied: that lord can still offer follow_me. The engine
// forbids a mercenary to create or manage an army (DefaultArmyManagementCalculationModel.CanPlayerCreateArmy:
// "Mercenaries cannot create or manage armies."), so a mercenary never leads one here either.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>What the joint army is raised against.</summary>
    public enum JointAttackTargetKind
    {
        /// <summary>A hostile party on the map or at sea: the army hunts it down.</summary>
        Party,

        /// <summary>A hostile village: the army raids it.</summary>
        Village,

        /// <summary>A hostile castle or town: the army lays siege to it.</summary>
        Fortress
    }

    /// <summary>Why a lord cannot raise a joint army with the player now, or <see cref="None" /> when he can.</summary>
    public enum JointAttackRefusal
    {
        /// <summary>Nothing stands in the way.</summary>
        None,

        /// <summary>He leads no field party of his own.</summary>
        NoPartyOfHisOwn,

        /// <summary>He and the player do not serve the same realm: the game's armies belong to one kingdom.</summary>
        NotTheSameRealm,

        /// <summary>He is already in an army, a siege or a battle.</summary>
        CommittedElsewhere,

        /// <summary>The player is already in an army, a siege or a battle, or is not free on the map.</summary>
        PlayerNotFree,

        /// <summary>Neither of them may lead an army (both serve as mercenaries).</summary>
        NoOneMayLead,

        /// <summary>No hostile target the two could reach is known.</summary>
        NoTarget
    }

    /// <summary>Who commands the joint army.</summary>
    public enum JointAttackLeader
    {
        /// <summary>The player leads; the lord's party joins the player's army.</summary>
        Player,

        /// <summary>The lord leads; the player's party joins his army and follows it to the target.</summary>
        Npc
    }

    /// <summary>Why a joint army ends.</summary>
    public enum JointAttackEnd
    {
        /// <summary>It goes on.</summary>
        None,

        /// <summary>The target is gone: the party destroyed or dispersed, the settlement taken or raided.</summary>
        TargetDone,

        /// <summary>The realm is no longer at war with the target.</summary>
        Peace,

        /// <summary>The army is gone (the engine disbanded it, or a party left it).</summary>
        ArmyGone,

        /// <summary>The lord or the player was taken or fell.</summary>
        SomeoneDown,

        /// <summary>The term ran out with the target still standing.</summary>
        TermOver
    }

    /// <summary>One side of the pair, read off the live campaign.</summary>
    public sealed class JointAttackSide
    {
        /// <summary>Rules the realm both serve.</summary>
        public bool RulesTheRealm { get; init; }

        /// <summary>Serves the realm as a mercenary (the engine bars a mercenary from leading an army).</summary>
        public bool IsMercenary { get; init; }

        /// <summary>The clan's tier (0 to 6).</summary>
        public int ClanTier { get; init; }

        /// <summary>The party's estimated strength.</summary>
        public float PartyStrength { get; init; }
    }

    /// <summary>What deciding whether a joint army can be raised rests on.</summary>
    public sealed class JointAttackFacts
    {
        /// <summary>The lord leads a field party of his own.</summary>
        public bool NpcLeadsOwnParty { get; init; }

        /// <summary>The lord and the player serve the same kingdom.</summary>
        public bool SameRealm { get; init; }

        /// <summary>The lord's party is in an army, a siege or a battle now.</summary>
        public bool NpcCommittedElsewhere { get; init; }

        /// <summary>The player's party is in an army, a siege or a battle, or the player is a captive.</summary>
        public bool PlayerNotFree { get; init; }

        /// <summary>At least one hostile target is within reach of the two.</summary>
        public bool AnyTarget { get; init; }

        /// <summary>The player's side.</summary>
        public JointAttackSide Player { get; init; } = new();

        /// <summary>The lord's side.</summary>
        public JointAttackSide Npc { get; init; } = new();
    }

    /// <summary>A target the model may name, read off the live campaign.</summary>
    public sealed class JointAttackCandidate
    {
        /// <summary>The party's or settlement's StringId: what the model writes back.</summary>
        public string Id { get; init; } = "";

        /// <summary>Its name as the player sees it.</summary>
        public string Name { get; init; } = "";

        /// <summary>What kind of army it makes.</summary>
        public JointAttackTargetKind Kind { get; init; }

        /// <summary>A party at sea, reached only by ships.</summary>
        public bool AtSea { get; init; }

        /// <summary>Both parties can sail (War Sails): a target at sea is in reach only then.</summary>
        public bool BothCanSail { get; init; }

        /// <summary>Distance from the player's party, in map units.</summary>
        public float Distance { get; init; }

        /// <summary>The target's men, when known (a party, a garrison), else 0.</summary>
        public int Men { get; init; }
    }

    /// <summary>What deciding whether a joint army is over rests on, read every hour.</summary>
    public sealed class JointAttackProgressFacts
    {
        /// <summary>The army CR raised still exists and both parties are in it.</summary>
        public bool ArmyIntact { get; init; }

        /// <summary>The target party no longer exists, or the settlement has changed hands or been raided.</summary>
        public bool TargetDone { get; init; }

        /// <summary>The realm is still at war with the target.</summary>
        public bool StillAtWar { get; init; }

        /// <summary>The lord and the player are both free and alive.</summary>
        public bool BothStanding { get; init; }

        /// <summary>Whole days since the army was raised.</summary>
        public int DaysSinceRaised { get; init; }
    }

    /// <summary>Pure rules for the joint army a lord raises with the player.</summary>
    public static class JointAttackPolicy
    {
        /// <summary>
        ///   How long a joint army lasts at most before the lord returns to other business: the same fifteen days a
        ///   follow_me escort runs (EscortEligibilityPolicy.TermDays), so the two promises a lord makes last alike.
        /// </summary>
        public const int TermDays = EscortEligibilityPolicy.TermDays;

        /// <summary>
        ///   How many targets the prompt lists: the nearest few, enough to hold the one a plan names, short enough for a
        ///   small model's prompt.
        /// </summary>
        public const int MaxCandidates = 6;

        /// <summary>
        ///   How far a target may lie from the player's party, in map units: an attack planned in one talk is against an
        ///   enemy close by, a little beyond the 40 within which a lord rides out to meet the player (InterceptionBehavior).
        /// </summary>
        public const float ReachMapUnits = 60f;

        /// <summary>Which door is shut, in the order a lord would think of them, or <see cref="JointAttackRefusal.None" />.</summary>
        public static JointAttackRefusal Refusal(JointAttackFacts f)
        {
            if (f == null || !f.NpcLeadsOwnParty) return JointAttackRefusal.NoPartyOfHisOwn;
            if (!f.SameRealm) return JointAttackRefusal.NotTheSameRealm;
            if (f.NpcCommittedElsewhere) return JointAttackRefusal.CommittedElsewhere;
            if (f.PlayerNotFree) return JointAttackRefusal.PlayerNotFree;
            if (Leader(f.Player, f.Npc) == null) return JointAttackRefusal.NoOneMayLead;
            if (!f.AnyTarget) return JointAttackRefusal.NoTarget;

            return JointAttackRefusal.None;
        }

        /// <summary>Whether the joint army can be raised now. The teaching and the bridge both ask this.</summary>
        public static bool CanRaise(JointAttackFacts f) => Refusal(f) == JointAttackRefusal.None;

        /// <summary>
        ///   Who commands (Gabriel: "C'est le Lord le plus puissant qui dirige l'armee"), in order: a mercenary never leads
        ///   (the engine forbids it); the realm's ruler leads; then the higher clan tier; then the stronger party; a tie
        ///   goes to the lord, whose offer it is. Null when neither may lead.
        /// </summary>
        public static JointAttackLeader? Leader(JointAttackSide player, JointAttackSide npc)
        {
            player ??= new JointAttackSide();
            npc ??= new JointAttackSide();
            if (player.IsMercenary && npc.IsMercenary) return null;
            if (player.IsMercenary) return JointAttackLeader.Npc;
            if (npc.IsMercenary) return JointAttackLeader.Player;
            if (player.RulesTheRealm != npc.RulesTheRealm) return player.RulesTheRealm ? JointAttackLeader.Player : JointAttackLeader.Npc;
            if (player.ClanTier != npc.ClanTier) return player.ClanTier > npc.ClanTier ? JointAttackLeader.Player : JointAttackLeader.Npc;

            return player.PartyStrength > npc.PartyStrength ? JointAttackLeader.Player : JointAttackLeader.Npc;
        }

        /// <summary>The targets the prompt lists: those within <see cref="ReachMapUnits" /> (a target at sea only when both can sail), nearest first.</summary>
        public static IReadOnlyList<JointAttackCandidate> Candidates(IEnumerable<JointAttackCandidate> all)
            => (all ?? Enumerable.Empty<JointAttackCandidate>())
               .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id) && c.Distance <= ReachMapUnits && (!c.AtSea || c.BothCanSail))
               .OrderBy(c => c.Distance)
               .ThenBy(c => c.Id, StringComparer.Ordinal)
               .Take(MaxCandidates)
               .ToList();

        /// <summary>The candidate the model named, by id first, else by its exact name (case ignored); null when none matches.</summary>
        public static JointAttackCandidate Resolve(IReadOnlyList<JointAttackCandidate> candidates, string named)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(named)) return null;
            string n = named.Trim();

            return candidates.FirstOrDefault(c => string.Equals(c.Id, n, StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault(c => string.Equals(c.Name?.Trim(), n, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///   The target the army marches on: the one the model named, else the nearest listed enemy (froggyluv's was the
        ///   one "directly North"). The player sees it named in the confirmation and may refuse a wrong one.
        /// </summary>
        public static JointAttackCandidate ResolveOrNearest(IReadOnlyList<JointAttackCandidate> candidates, string named)
            => Resolve(candidates, named) ?? candidates?.FirstOrDefault();

        /// <summary>The line the prompt lists for a candidate: "id: name (what it is)".</summary>
        public static string Line(JointAttackCandidate c)
        {
            if (c == null) return "";
            string what = c.Kind switch
            {
                JointAttackTargetKind.Village => "a village, to raid",
                JointAttackTargetKind.Fortress => c.Men > 0 ? $"a stronghold held by {c.Men}, to besiege" : "a stronghold, to besiege",
                _ => c.AtSea ? $"ships at sea, {c.Men} aboard" : $"a party of {c.Men}"
            };

            return $"{c.Id}: {c.Name} ({what})";
        }

        /// <summary>
        ///   The player's yes before the army forms, saying who commands and against what: "March in Lady Ira's army against
        ///   Looters? Your party follows hers until it is done." or "Raise an army with Lady Ira against Looters, under your
        ///   command?".
        /// </summary>
        public static string Question(JointAttackLeader leader, string lordName, string targetName)
        {
            string who = string.IsNullOrWhiteSpace(lordName) ? "them" : lordName.Trim();
            string what = string.IsNullOrWhiteSpace(targetName) ? "the enemy" : targetName.Trim();

            return leader == JointAttackLeader.Npc
                ? $"March in {who}'s army against {what}? Your party follows theirs until it is done."
                : $"Raise an army with {who} against {what}, under your command?";
        }

        /// <summary>Whether the joint army is over, and why, tried in the order that names the truest reason.</summary>
        public static JointAttackEnd End(JointAttackProgressFacts f)
        {
            if (f == null) return JointAttackEnd.ArmyGone;
            if (!f.BothStanding) return JointAttackEnd.SomeoneDown;
            if (f.TargetDone) return JointAttackEnd.TargetDone;
            if (!f.StillAtWar) return JointAttackEnd.Peace;
            if (!f.ArmyIntact) return JointAttackEnd.ArmyGone;
            if (f.DaysSinceRaised >= TermDays) return JointAttackEnd.TermOver;

            return JointAttackEnd.None;
        }

        /// <summary>What the lord tells the player when the joint army cannot be raised, in his terms. Empty when it can.</summary>
        public static string Describe(JointAttackRefusal refusal, string lordName)
        {
            string who = string.IsNullOrWhiteSpace(lordName) ? "He" : lordName.Trim();
            switch (refusal)
            {
                case JointAttackRefusal.NoPartyOfHisOwn: return $"({who} leads no company to march with yours.)";
                case JointAttackRefusal.NotTheSameRealm: return $"({who} can only share an army with a lord of the same realm.)";
                case JointAttackRefusal.CommittedElsewhere: return $"({who} is already in an army, a siege or a battle.)";
                case JointAttackRefusal.PlayerNotFree: return "(You are already in an army, a siege or a battle, or not free to march.)";
                case JointAttackRefusal.NoOneMayLead: return "(Neither of you may lead an army: mercenaries cannot.)";
                case JointAttackRefusal.NoTarget: return "(No enemy within reach could be named for the attack.)";
                default: return "";
            }
        }

        /// <summary>How the end is told to the player.</summary>
        public static string DescribeEnd(JointAttackEnd end, string lordName)
        {
            string who = string.IsNullOrWhiteSpace(lordName) ? "Your ally" : lordName.Trim();
            switch (end)
            {
                case JointAttackEnd.TargetDone: return $"The attack is done. {who} returns to other business.";
                case JointAttackEnd.Peace: return $"Peace was made with the enemy. {who} returns to other business.";
                case JointAttackEnd.ArmyGone: return $"The army you raised with {who} has broken up.";
                case JointAttackEnd.SomeoneDown: return $"The army you raised with {who} has broken up.";
                case JointAttackEnd.TermOver: return $"The days promised are spent, and {who} returns to other business.";
                default: return "";
            }
        }
    }
}
