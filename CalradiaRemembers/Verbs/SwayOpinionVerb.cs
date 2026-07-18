// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   sway_opinion (RP with teeth, increment 1): the player's case, made in conversation, genuinely
    ///   moves the NPC's own regard for a named THIRD PARTY (not the player). Taught cheaply whenever
    ///   <see cref="CourtActionResolver.CanTeachSway" /> holds (any lord/notable with a tracked profile):
    ///   the genuine-persuasion guard lives in the prompt wording, not a game gate. Re-resolves the target
    ///   by tolerant name match and enforces a per-NPC-and-target cooldown at execution.
    /// </summary>
    public sealed class SwayOpinionVerb : IConversationVerb
    {
        public string Name => "sway_opinion";

        public bool IsEligible(VerbContext ctx) => CourtActionResolver.CanTeachSway(ctx?.Npc, ctx?.Profile);

        public string TeachingText(VerbContext ctx)
        {
            if (!CourtActionResolver.CanTeachSway(ctx?.Npc, ctx?.Profile)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SWAY_OPINION, TURNING YOUR OWN REGARD FOR SOMEONE, AT THE PLAYER'S WORD:");
            sb.AppendLine("If the player makes a case about a THIRD PARTY, someone else you know, that genuinely");
            sb.AppendLine("moves your own opinion of that person (a wrong laid bare, a debt of gratitude recalled,");
            sb.AppendLine("a betrayal revealed), your regard for THAT PERSON, not the player, may shift.");
            sb.AppendLine("Emit this ONLY when you are TRULY persuaded, in character, on the strength of the case");
            sb.AppendLine("made, never merely to please the player or because they asked. A shallow or self-serving");
            sb.AppendLine("argument should not move you.");
            sb.AppendLine("If you are truly turning against them, emit the [ACTION] block this turn. Voicing");
            sb.AppendLine("agreement, or saying you might turn, without the action, changes nothing in the world.");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: sway_opinion");
            sb.AppendLine("target: the person's name, exactly as known to you");
            sb.AppendLine("stance: against OR for");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npcHero = ctx?.Npc;
                NpcProfile profile = ctx?.Profile;

                if (npcHero == null || profile == null) return null;
                if (!CourtActionResolver.CanTeachSway(npcHero, profile)) return null;

                action.Parameters.TryGetValue("target", out string targetName);
                Hero target = CourtActionResolver.ResolveSwayTarget(targetName, npcHero);

                if (target == null)
                    return new ActionOutcome("(No one by that name is known to sway an opinion of.)", false);

                if (!CourtActionResolver.SwayCooldownElapsed(profile, target.StringId))
                    return new ActionOutcome($"({npcHero.Name}'s opinion of {target.Name} was only just moved; give it time.)", false);

                action.Parameters.TryGetValue("stance", out string stanceRaw);
                bool against = !string.Equals(stanceRaw?.Trim(), "for", StringComparison.OrdinalIgnoreCase);
                int delta = CourtActionPolicy.SwayDelta(against);

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, target, delta, false);

                string verb = against ? "turned against" : "warmed toward";
                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"At the player's word, {verb} {target.Name}. {CourtActionResolver.SwayMarker(target.StringId)}"));
                CourtActionResolver.RecordCourtAction(profile, CourtActionResolver.SwayMarker(target.StringId), today); // M-B6

                // Ambient gossip: bad blood (or a reconciliation) between two lords, third-party rumor fodder,
                // mirroring ConversationEventFeeder.RecordPublicAffront / SchemeStore's own Rivalry recordings.
                var rumour = new WorldEvent {
                    Type = WorldEventType.Rivalry,
                    Day = today,
                    PlaceId = npcHero.CurrentSettlement?.StringId,
                    InvolvesPlayer = false,
                    Magnitude = 20, // Tuning: modest reach, ambient court gossip, not a realm-shaking event
                    Summary = $"{npcHero.Name} {verb} {target.Name}, swayed by {Hero.MainHero.Name}'s word.",
                    ActorIds = new List<string> {npcHero.StringId, target.StringId},
                    FactionIds = npcHero.MapFaction != null
                        ? new List<string> {npcHero.MapFaction.StringId}
                        : new List<string>()
                };
                WorldEventStore.Instance?.Record(rumour);

                // Grudges pillar (Tier 2, LIVE): only a sway AGAINST the target wounds them, a sway FOR
                // them is a kindness, never a grudge. Queues a delayed, hidden reveal check against the
                // very Rivalry rumour just recorded above. SwayExposureBehavior watches it through
                // WorldEventStore's own awareness model, reusing the existing rumour-spread mechanism
                // rather than inventing a new one (mirrors JealousyBehavior's own PendingAwareness gate).
                if (against)
                    SwayExposureBehavior.Instance?.QueueExposure(target, rumour.Id, Math.Abs(delta), today, npcHero.Name?.ToString());

                ModLog.Info($"[ACTION] sway_opinion: {npcHero.Name} {verb} {target.Name} ({delta:+#;-#;0}).");

                return new ActionOutcome($"{npcHero.Name} {(against ? "turns against" : "warms toward")} {target.Name}. ({delta:+#;-#;0})", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] sway_opinion failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
