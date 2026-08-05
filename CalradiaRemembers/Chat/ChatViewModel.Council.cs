// Code written by Gabriel Mailhot, 30/07/2026.
// Bespoke COUNCIL UI, Inc 0: a dedicated 3-column window (left seat strip / centre chatroom / right
// engagements) bound to this SAME ChatViewModel, exactly like the Manuscript skin is a second prefab over the
// same data. This file owns the extra state the new prefab needs that the existing Classic/Manuscript prefabs
// never bound: the seat tiles (CouncilParticipants), the agenda line, the dossier overlay, and the empty
// Engagements placeholder (Inc 1 fills it from CouncilSessionBehavior's ledger).

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using NpcMemoryService.Core.Compression;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private const int CouncilEngagementExtractTimeoutSeconds = 45;
      private readonly MBBindingList<CouncilEngagementViewModel> _engagements = new MBBindingList<CouncilEngagementViewModel>();
      private readonly MBBindingList<CouncilParticipantViewModel> _councilParticipants = new MBBindingList<CouncilParticipantViewModel>();
      private readonly EngagementLedger _engagementLedger = new EngagementLedger();
      private string _councilAgendaText = "";
      private ChatMessageViewModel _councilDossierPortrait; // transient carrier resolving the clicked seat's portrait, released on close/reopen
      private bool _isCouncilDossierOpen;
      private bool _engagementExtractionRunning;

      /// <summary>
      ///   The short "Ordre du jour" line shown under the title in the bespoke council window. Seeded from the
      ///   scope in <see cref="ActivateCouncil" />; Inc 1 may replace this placeholder with the actual matter the
      ///   player raises.
      /// </summary>
      [DataSourceProperty]
      public string CouncilAgendaText
      {
         get => _councilAgendaText;
         set
         {
            if (_councilAgendaText == value) return;
            _councilAgendaText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The left strip's seat tiles: the anchor plus every present witness. Populated by <see cref="PopulateCouncilParticipants" />.</summary>
      [DataSourceProperty]
      public MBBindingList<CouncilParticipantViewModel> CouncilParticipants => _councilParticipants;

      /// <summary>The right panel's pledge rows. Always empty in Inc 0 (Inc 1 fills it from the council's ledger).</summary>
      [DataSourceProperty]
      public MBBindingList<CouncilEngagementViewModel> Engagements => _engagements;

      /// <summary>The placeholder shown in the Engagements panel while <see cref="Engagements" /> is empty.</summary>
      [DataSourceProperty]
      public string EngagementsPlaceholderText => "No decisions yet.";

      /// <summary>True while at least one engagement has been recorded (Inc 1); hides the empty-state placeholder.</summary>
      [DataSourceProperty]
      public bool HasEngagements => _engagements.Count > 0;

      /// <summary>
      ///   The inverse of <see cref="HasEngagements" />, exposed separately because the prefab's IsVisible
      ///   binding has no negation operator: the empty-state placeholder needs its OWN positive boolean.
      /// </summary>
      [DataSourceProperty]
      public bool ShowEngagementsPlaceholder => !HasEngagements;

      /// <summary>True while the council dossier overlay is open, showing the seat clicked in the left strip.</summary>
      [DataSourceProperty]
      public bool IsCouncilDossierOpen
      {
         get => _isCouncilDossierOpen;
         set
         {
            if (_isCouncilDossierOpen == value) return;
            _isCouncilDossierOpen = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>Closes the council dossier overlay. Bound via Command.Click on its close button.</summary>
      public void ExecuteCloseCouncilDossier() => CloseCouncilDossier();

      /// <summary>Closes the council dossier overlay.</summary>
      public void CloseCouncilDossier()
      {
         IsCouncilDossierOpen = false;
      }

      /// <summary>
      ///   Opens the council dossier overlay for <paramref name="heroStringId" />, reusing the exact same
      ///   Panel* dossier data <see cref="OpenPortraitPanel" /> populates (facts, background, character,
      ///   appearance, discoveries, grievances, portrait). <see cref="PopulatePanelData" /> reads its portrait
      ///   fields off a <see cref="ChatMessageViewModel" />, the chat-line carrier, not a bare Hero (there is no
      ///   hero-typed overload), so a TRANSIENT one is built here purely to resolve this seat's portrait; it is
      ///   never added to <see cref="Messages" /> (it is not a chat line) and its vanilla tableau is released
      ///   before the next one is built and at council close (<see cref="ReleaseCouncilPortraits" />), so it can
      ///   never linger as an orphaned GPU render-target.
      /// </summary>
      public void OpenCouncilDossier(string heroStringId)
      {
         try
         {
            if (string.IsNullOrEmpty(heroStringId)) return;

            Hero hero = Hero.FindFirst(h => h.StringId == heroStringId);
            if (hero == null) return;

            _councilDossierPortrait?.ReleasePortrait();
            _councilDossierPortrait = new ChatMessageViewModel(hero, hero.Name?.ToString() ?? "", "", hero == Hero.MainHero);

            if (!PopulatePanelData(_councilDossierPortrait))
            {
               _councilDossierPortrait = null;

               return;
            }

            IsCouncilDossierOpen = true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"ChatViewModel.OpenCouncilDossier failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Seats the left strip: the anchor (<see cref="MainSpeaker" />) followed by every present witness,
      ///   resolved to a live hero and deduplicated by StringId. Called by <see cref="ActivateCouncil" /> once
      ///   the caller (ChatManager.OpenForCouncil/OpenForAssembly) has already added every witness, so the whole
      ///   seated roster is present. Guards every resolution gap rather than throwing: a hero who cannot be
      ///   found (already left play) is simply absent from the strip.
      /// </summary>
      public void PopulateCouncilParticipants()
      {
         ClearCouncilParticipants();

         var seatedHeroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         ChatSpeaker anchor = MainSpeaker();
         if (anchor?.Hero != null)
         {
            // Council narrator model (2026-08-02): the anchor is the conversation's staging body only (the
            // profile behind the LLM call, hidden behind the opaque backdrop), never a privileged voice. The
            // seat label used to read "Presides over this council"; that presiding language is exactly what
            // the reshape drops, so the label is now the same neutral seat description any other member gets.
            _councilParticipants.Add(new CouncilParticipantViewModel(anchor.Hero, "Seated at this council"));
            seatedHeroIds.Add(anchor.Hero.StringId);
         }

         foreach (WitnessEntry w in _witnesses)
         {
            if (w == null || string.IsNullOrEmpty(w.HeroStringId) || seatedHeroIds.Contains(w.HeroStringId)) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == w.HeroStringId);
            if (hero == null) continue;

            seatedHeroIds.Add(w.HeroStringId);
            _councilParticipants.Add(new CouncilParticipantViewModel(hero, w.RelationToNpc));
         }

         OnPropertyChanged(nameof(CouncilParticipants));
      }

      /// <summary>
      ///   Frees every seat's vanilla character tableau and clears the strip. Called by
      ///   <see cref="ChatManager.KillChatWindow" /> before the movie is released, mirroring
      ///   <see cref="ReleaseAllPortraits" /> for the ordinary message list.
      /// </summary>
      public void ReleaseCouncilPortraits()
      {
         ClearCouncilParticipants();
         _councilDossierPortrait?.ReleasePortrait();
         _councilDossierPortrait = null;
      }

      /// <summary>Removes any council seat highlight (a no-op when none is set). See <see cref="SetActiveCouncilSpeaker(string)" />.</summary>
      public void ClearActiveCouncilSpeaker()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.IsActiveSpeaker = false;
      }

      /// <summary>Highlights the seat matching <paramref name="heroStringId" /> and clears every other seat's highlight.</summary>
      public void SetActiveCouncilSpeaker(string heroStringId)
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.IsActiveSpeaker = !string.IsNullOrEmpty(heroStringId)
                              && string.Equals(p.HeroStringId, heroStringId, StringComparison.OrdinalIgnoreCase);
      }

      /// <summary>Releases every seat tile's tableau and empties the strip. Shared by re-population and close-time cleanup.</summary>
      private void ClearCouncilParticipants()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.ReleasePortrait();

         _councilParticipants.Clear();
      }

      /// <summary>
      ///   True once any seated member's hero currently rides in the player's own party: the TargetInPlayerParty
      ///   fact assign_party_role needs (ResolutionOfferingResolver.OfferedKindKeys) before the offering can teach
      ///   it. A filled seat alone does not prove this (a companion may be seated at a council while governing a
      ///   town elsewhere, see anySeatedMemberAvailable's own doc), so each seat's live Hero is resolved and its
      ///   PartyBelongedTo checked for real, rather than assuming every seat qualifies.
      /// </summary>
      private bool AnySeatedMemberInPlayerParty()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero != null && hero.PartyBelongedTo == MobileParty.MainParty) return true;
         }

         return false;
      }

      /// <summary>
      ///   True once any seated member's hero is a live clan companion currently OUTSIDE the player's own party:
      ///   the TargetOutOfPlayerParty fact rejoin_party needs (ResolutionOfferingResolver.OfferedKindKeys),
      ///   mirroring <see cref="AnySeatedMemberInPlayerParty" />'s own wiring exactly, but the symmetric inverse
      ///   condition. Closes bug d: a clan companion may be seated at a council while away (the roster's own
      ///   availability rule already allows this, see anySeatedMemberAvailable's doc), and today the only "join"
      ///   path (the 1:1 join_party action) hires a free Wanderer only, so an existing companion pledging to
      ///   rejoin had nothing to execute it. A clan LORD who leads their own party is out of scope for this
      ///   rejoin (see CouncilLift.SettleRejoinParty's own refusal for that case), so this reads IsPlayerCompanion
      ///   specifically, not merely same-clan.
      /// </summary>
      private bool AnySeatedCompanionOutOfPlayerParty()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is {IsAlive: true, IsPlayerCompanion: true, IsPrisoner: false}
                && hero.PartyBelongedTo != MobileParty.MainParty)
               return true;
         }

         return false;
      }

      /// <summary>
      ///   True once the player's own gold can cover at least StipendPolicy.MinTrancheCost (the cheapest possible
      ///   30-day tranche, 50 denars/day floor): the PlayerCanAffordStipend fact grant_stipend needs
      ///   (ResolutionOfferingResolver.OfferedKindKeys). The LLM has not proposed a real amount at offer time (that
      ///   only exists once a [RESOLUTION] names target_amount), so this proves only the FLOOR is affordable; the
      ///   actual proposed amount is re-clamped and re-checked against the player's gold at the lift
      ///   (CouncilLift.SettleGrantStipend -> CouncilStipendBehavior.TryStart), bridge-is-law like every other
      ///   executor's own re-check.
      /// </summary>
      private static bool PlayerCanAffordStipend() => Hero.MainHero != null && Hero.MainHero.Gold >= StipendPolicy.MinTrancheCost;

      /// <summary>
      ///   True once any seated member currently holds gold greater than zero: Partie 1's own fact, give_gold
      ///   (ResolutionOfferingResolver.OfferedKindKeys). Unlike PlayerCanAffordStipend (the PLAYER'S own gold),
      ///   this reads each SEAT's own purse, since give_gold moves the opposite direction: a seated lord pledges
      ///   FROM their own coffers TO the player. Scoped to ClanLords and WarCouncil only by the catalogue itself
      ///   (ResolutionCatalog's two GiveGold entries), so a Companions or Family council never sees give_gold
      ///   offered regardless of what this returns. The real proposed amount is unknown at offer time; the
      ///   actual figure is re-clamped against the giver's ACTUAL gold at the lift (CouncilLift.SettleGiveGold,
      ///   GoldPledgePolicy.ClampPayable), bridge-is-law like every other executor's own re-check.
      /// </summary>
      private bool AnySeatedMemberHasGold()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is {IsAlive: true} && hero.Gold > 0) return true;
         }

         return false;
      }

      /// <summary>
      ///   True once any seated ally's OWN clan currently holds influence it could spare to the player's clan:
      ///   Partie 1's other own fact, give_influence (ResolutionOfferingResolver.OfferedKindKeys). Reuses the
      ///   SAME gates the 1:1 give_influence verb already checks (CalradiaRemembers.Verbs.GiveInfluenceVerb /
      ///   CourtActionResolver.TryGiveInfluence): a living lord, a clan of their own distinct from the player's
      ///   (COUNCIL_ACTIONS.md's Kimi review: the 1:1 verb already excludes giver == Clan.PlayerClan, so this
      ///   would be mechanically vacant within the player's own clan), BOTH clans seated in a kingdom (influence
      ///   carries no weight for a clan holding no seat at any court), the relation floor
      ///   (CourtActionPolicy.InfluenceMinRelation), and the giver's clan holding comfortably more than the
      ///   computed grant in reserve (CourtActionPolicy.CanAffordInfluenceGrant). Skips the 1:1 verb's own
      ///   cooldown check (no NpcProfile is read here, mirroring AnySeatedMemberHasGold's own loose gate): a
      ///   member whose cooldown has not elapsed is instead caught, and reported honestly, at the lift
      ///   (CouncilLift.SettleGiveInfluence), the same "prove SOME grant is possible, never the exact figure"
      ///   discipline AnySeatedMemberHasGold already uses for give_gold.
      /// </summary>
      private bool AnySeatedMemberCanGrantInfluence()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true, IsLord: true} || hero.IsPrisoner) continue;

            Clan giver = hero.Clan;
            if (giver == null || giver == Clan.PlayerClan) continue;
            if (giver.Kingdom == null || Clan.PlayerClan?.Kingdom == null) continue;

            int relation = hero.GetRelation(Hero.MainHero);
            if (relation < CourtActionPolicy.InfluenceMinRelation) continue;

            int amount = CourtActionPolicy.InfluenceGrant(relation);
            if (amount <= 0) continue;
            if (!CourtActionPolicy.CanAffordInfluenceGrant(giver.Influence, amount)) continue;

            return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS): lend_troops' own fact. True
      ///   once any seated lord (ClanLords or WarCouncil, mirroring AnySeatedMemberHasGold's own two-scope entry)
      ///   currently leads their own party, is NOT at war with the player (Gabriel's condition D), and fields
      ///   enough healthy troops that <see cref="CouncilTroopLoanPolicy.ComputeLoan" /> would loan more than zero once
      ///   bucketed by <see cref="TroopTierBucketer.BucketHealthyTroops" />. Mirrors AnySeatedMemberHasGold's own
      ///   loose gate (proves only that SOME loan is possible, never the exact tier split): the real split is
      ///   computed fresh at the lift from the donor's LIVE roster regardless (CouncilLift.SettleLendTroops,
      ///   bridge is law).
      /// </summary>
      private bool AnySeatedMemberCanLendTroops()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true} || hero.IsPrisoner) continue;

            MobileParty party = hero.PartyBelongedTo;
            if (party == null || party.LeaderHero != hero) continue;

            if (hero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
                FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction)) continue;

            TierTroopCounts counts = TroopTierBucketer.BucketHealthyTroops(party.MemberRoster);
            if (CouncilTroopLoanPolicy.ComputeLoan(counts).Total > 0) return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's give_troops, the reciprocal of lend_troops): give_troops' own fact.
      ///   True once the PLAYER's own party fields enough healthy troops that
      ///   <see cref="CouncilTroopLoanPolicy.ComputeLoan" /> would yield more than zero from the PLAYER's own
      ///   roster (never the seated lord's, the direction reversed from <see cref="AnySeatedMemberCanLendTroops" />)
      ///   AND at least one seated lord (ClanLords or WarCouncil, mirroring AnySeatedMemberCanLendTroops' own
      ///   two-scope entry) currently leads their own party with room to receive more, is not the player, and
      ///   is not at war with the player. An APPROXIMATION, mirroring AnySeatedMemberCanLendTroops' own loose
      ///   gate: proves only that SOME gift is possible, never the exact tier split or which seated lord ends
      ///   up chosen, both computed fresh at the lift from the PLAYER's LIVE roster regardless
      ///   (CouncilLift.SettleGiveTroops, bridge is law).
      /// </summary>
      private bool AnySeatedLordCanReceiveTroops()
      {
         MobileParty playerParty = MobileParty.MainParty;
         if (playerParty?.MemberRoster == null) return false;

         TierTroopCounts playerCounts = TroopTierBucketer.BucketHealthyTroops(playerParty.MemberRoster);
         if (CouncilTroopLoanPolicy.ComputeLoan(playerCounts).Total <= 0) return false;

         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true} || hero.IsPrisoner || hero == Hero.MainHero) continue;

            MobileParty party = hero.PartyBelongedTo;
            if (party == null || party.LeaderHero != hero || party == playerParty) continue;

            if (hero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
                FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction)) continue;

            if (party.MemberRoster.TotalManCount >= party.Party.PartySizeLimit) continue;

            return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 2's own fact, revoke_fief (COUNCIL_ACTIONS.md; ResolutionOfferingResolver.OfferedKindKeys). True
      ///   once any seated WarCouncil vassal's own clan currently owns a town or castle: without this, the model
      ///   could threaten a confiscation the lift then finds nobody present actually holds anything to strip.
      ///   Mirrors AnySeatedMemberHasGold's own loose gate (proves only SOME fief exists, never which one is
      ///   named): the actual fief named is re-resolved and re-checked against the SAME seated vassal's own clan
      ///   at the lift (CouncilLift.SettleRevokeFief, bridge is law).
      /// </summary>
      private bool AnySeatedVassalOwnsFief()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            Clan clan = hero?.Clan;
            if (clan == null || clan == Clan.PlayerClan) continue;

            if (Settlement.All != null && Settlement.All.Any(s => (s.IsTown || s.IsCastle) && s.OwnerClan == clan))
               return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 8's own fact, expel_from_clan (COUNCIL_ACTIONS.md's Kimi review; ResolutionOfferingResolver.OfferedKindKeys).
      ///   True once any seated companion is currently expellable per <see cref="ExpulsionPolicy.CanExpel" />:
      ///   the player leads their own clan (a companion's hire/fire is a genuine CLAN-internal decision, unlike
      ///   grant_fief/revoke_fief's cross-clan sovereignty bar), the seat is a LIVE companion of that same clan,
      ///   and not the player's own spouse (that bond has its own dissolution channel, never this one). Scope
      ///   note: this is Companions' own fact only (a deliberate deviation from this build's original brief,
      ///   Family/ClanLords - see ExpulsionPolicy's own header for why neither of those rosters ever seats a
      ///   companion in the first place). Mirrors AnySeatedMemberHasGold's own loose gate (proves only SOME
      ///   companion is expellable, never which one the LLM will actually name): the away-on-mission check is
      ///   deliberately skipped here too and re-verified at the lift instead (CouncilLift.SettleExpelFromClan,
      ///   bridge is law), the same discipline SettleDispatchMission's own IsAway re-check already follows.
      /// </summary>
      private bool AnySeatedCompanionExpellable()
      {
         bool playerIsClanLeader = Clan.PlayerClan?.Leader == Hero.MainHero;
         if (!playerIsClanLeader) return false;

         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero == null) continue;

            bool isSpouse = hero == Hero.MainHero?.Spouse;
            if (ExpulsionPolicy.CanExpel(playerIsClanLeader, hero.IsPlayerCompanion, isSpouse, hero.IsAlive))
               return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's schemes block): pledge_against's own fact, REUSING the existing 1:1
      ///   pledge_against system end to end. True once any seated LORD, alive, free, and not the player, holds
      ///   no pledge already outstanding (CommitmentBehavior.HasActive). An APPROXIMATION, mirroring
      ///   AnySeatedMemberHasGold's own loose gate: proving a genuine RIVAL also exists (standing enmity,
      ///   non-kin, non-faction-leader) would need each seat's own NpcProfile plus a full Hero.AllAliveHeroes
      ///   scan, real cost for a fact that only gates TEACHING, and the exact rival is not known at offer time
      ///   regardless (the LLM has not named one yet); the full validation, plus the council's own
      ///   not-a-fellow-seat caveat, still happens at the lift (CourtActionResolver.ResolvePledgeTarget,
      ///   CouncilLift.SettlePledgeAgainst, bridge is law).
      /// </summary>
      private bool AnySeatedMemberCanPledgeAgainst()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true, IsLord: true} || hero.IsPrisoner || hero == Hero.MainHero) continue;

            if (CommitmentBehavior.Instance?.HasActive(hero.StringId) != true) return true;
         }

         return false;
      }

      /// <summary>
      ///   R7-light (COUNCIL_ACTIONS.md's Partie 8, "swear_oath"): true once any seated member, alive, free, not
      ///   the player, and not already carrying a commitment (<see cref="CommitmentBehavior.HasActive" />, the
      ///   SAME one-vow-at-a-time gate pledge_against already shares), plausibly qualifies for AT LEAST ONE of
      ///   the three whitelisted oath kinds: holds gold (pay_gold), leads a party of their own (protect, mirrors
      ///   <see cref="AnySeatedMemberCanLendTroops" />'s own party-leadership check), or leads their own
      ///   kingdom/independent clan (keep_peace). An APPROXIMATION, mirroring <see cref="AnySeatedMemberCanPledgeAgainst" />'s
      ///   own loose gate: proves only that SOME oath is plausible for SOME seat, never which kind the model
      ///   will actually name or whether it fits THIS particular swearer, the full validation (and the honest
      ///   refusal of a mismatched kind) happens at the lift regardless (CouncilLift.SettleSwearOath, bridge is
      ///   law).
      /// </summary>
      private bool AnySeatedMemberCanSwearOath()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true} || hero.IsPrisoner || hero == Hero.MainHero) continue;
            if (CommitmentBehavior.Instance?.HasActive(hero.StringId) == true) continue;

            if (hero.Gold > 0) return true;
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero) return true;
            if (hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero) return true;
            if (hero.Clan != null && hero.Clan.Kingdom == null && hero.Clan.Leader == hero) return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est valide"): arrange_marriage's own fact,
      ///   WarCouncil only, REUSING the existing 1:1 arrange_marriage system end to end. True once the player's
      ///   own clan holds an unwed kin (<see cref="PlayerClanHasUnwedKin" />) AND at least one seated ally's own
      ///   house (a genuine OTHER clan, mirroring <see cref="AnySeatedMemberCanGrantInfluence" />'s own
      ///   giver-is-not-the-player's-clan gate) offers an unwed kin of its own, or is themselves unwed (mirroring
      ///   the 1:1 action's own "b == npcHero" case). An APPROXIMATION, mirroring <see cref="AnySeatedMemberHasGold" />'s
      ///   own loose gate deliberately: only alive-and-unwed is checked here, never the opposite-sex/age pairing
      ///   <see cref="CalradiaRemembers.Marriage.MarriageProspectResolver.IsMarriageable" /> enforces, nor
      ///   <see cref="CalradiaRemembers.Logic.ArrangedMatchPolicy" />'s own standing consent, the exact pairing
      ///   and every hard gate are re-validated at the lift regardless
      ///   (CalradiaRemembers.GameState.CourtActionResolver.TryArrangeMarriage, bridge is law), the same "prove
      ///   SOME pledge is possible, never the exact match" discipline every other loose gate above already uses.
      /// </summary>
      private bool AnySeatedAllyHasMarriageableKin()
      {
         if (!PlayerClanHasUnwedKin()) return false;

         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero ally = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (ally is not {IsAlive: true} || ally.IsPrisoner) continue;

            Clan allyClan = ally.Clan;
            if (allyClan == null || allyClan == Clan.PlayerClan) continue;

            if (ally.Spouse == null) return true; // the ally themselves may be the match

            if (allyClan.Heroes != null)
               foreach (Hero member in allyClan.Heroes)
                  if (member != ally && member is {IsAlive: true, Spouse: null})
                     return true;
         }

         return false;
      }

      /// <summary>
      ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): tribute's own fact, Parley-only. True once any
      ///   seated member is alive and leads a clan distinct from the player's own - in practice the ONE seated
      ///   Parley counterpart (CouncilRosterResolver's own single-seat pool for that scope), so this reads exactly
      ///   like AnySeatedMemberHasGold's own loose gate over whatever is seated, rather than special-casing "the
      ///   one seat". The real proposed amount is unknown at offer time, and whether the payer's live gold covers
      ///   even one day of it is checked fresh at the lift and at every daily tick (CouncilLift.SettleTribute,
      ///   CouncilTributeBehavior.Advance), bridge-is-law like every other executor's own re-check.
      /// </summary>
      private bool AnySeatedTributePayerAvailable()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is {IsAlive: true} && hero.Clan != null && hero.Clan != Clan.PlayerClan) return true;
         }

         return false;
      }

      /// <summary>
      ///   Rounds out the Parley toolkit (make_peace + tribute + release_prisoner, 2026-08-04): release_prisoner's
      ///   own fact. True once the player currently holds, in their own party (the SAME pool the 1:1 prisoner-trade
      ///   verbs already read, <see cref="CalradiaRemembers.Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners" />), at
      ///   least one hero prisoner belonging to the SAME faction this parley was convened against
      ///   (<see cref="CalradiaRemembers.Assemblies.ParleyFactionLookup" />, the ONE source of truth
      ///   PlayerCanMakePeace already reads for the identical reason): a captive of an unrelated third house
      ///   would be no genuine concession to the seated counterpart across the table. An APPROXIMATION,
      ///   mirroring AnySeatedTributePayerAvailable's own loose gate: proves only that SOME real concession
      ///   exists, never the exact captive the LLM will actually name; the named captive is re-resolved and
      ///   re-checked against the SAME faction at the lift regardless (CouncilLift.SettleReleasePrisoner, bridge
      ///   is law).
      /// </summary>
      private bool AnySeatedPrisonerAvailableToRelease()
      {
         IFaction enemyFaction = CalradiaRemembers.Assemblies.ParleyFactionLookup.ByStringId(_councilParleyTargetFactionId);
         if (enemyFaction == null) return false;

         foreach (Hero prisoner in CalradiaRemembers.Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners())
            if (prisoner?.MapFaction != null && prisoner.MapFaction == enemyFaction) return true;

         return false;
      }

      /// <summary>
      ///   Kimi's "v1 invite d'honneur" (COUNCIL_ACTIONS.md's Partie 8, "give_hostage"): give_hostage's own fact,
      ///   Parley-only. True once any seated member (in practice the ONE seated Parley counterpart, mirroring
      ///   <see cref="AnySeatedTributePayerAvailable" />'s own "reads exactly like the loose gate over whatever
      ///   is seated" shape) currently has at least one eligible relative to give
      ///   (<see cref="CalradiaRemembers.Assemblies.HostageCandidateResolver.AnyEligibleRelative" />, the EXACT
      ///   same predicate the lift re-runs, unlike most of the loose gates elsewhere in this file). The named
      ///   relative itself is re-resolved and re-checked against that same pool at the lift regardless
      ///   (CouncilLift.SettleGiveHostage, bridge is law).
      /// </summary>
      private bool AnySeatedHostageEligibleRelativeAvailable()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is {IsAlive: true} && CalradiaRemembers.Assemblies.HostageCandidateResolver.AnyEligibleRelative(hero)) return true;
         }

         return false;
      }

      /// <summary>
      ///   Rounds out the Parley toolkit with a FIFTH concession (COUNCIL_ACTIONS.md's Partie 8,
      ///   "non_aggression_pact"): non_aggression_pact's own fact, Parley-only. True once any seated member is
      ///   alive and leads a clan distinct from the player's own (mirroring <see cref="AnySeatedTributePayerAvailable" />'s
      ///   own loose gate exactly), AND the two are not already bound by a live pact
      ///   (<see cref="CalradiaRemembers.Assemblies.CouncilPactBehavior.IsUnderPact" />) - teaching a second,
      ///   redundant pact while one already binds the same two factions would offer nothing new. The real
      ///   faction pair is re-resolved and re-checked at the lift regardless (CouncilLift.SettleNonAggressionPact,
      ///   bridge is law).
      /// </summary>
      private bool AnySeatedPactPartnerAvailable()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true} || hero.Clan == null || hero.Clan == Clan.PlayerClan) continue;

            IFaction playerFaction = Hero.MainHero?.MapFaction;
            IFaction theirFaction = hero.MapFaction;
            if (playerFaction == null || theirFaction == null) continue;

            if (CalradiaRemembers.Assemblies.CouncilPactBehavior.Instance?.IsUnderPact(playerFaction, theirFaction) == true) continue;

            return true;
         }

         return false;
      }

      /// <summary>
      ///   Kimi's design, MINIMAL v1 (COUNCIL_ACTIONS.md's Partie 8, "sponsor_ward"): sponsor_ward's own fact.
      ///   True once any seated member, alive, free, and not the player, could plausibly be a mentor (mirroring
      ///   <see cref="AnySeatedMemberCanPledgeAgainst" />'s own IsLord gate exactly, since Family/ClanLords always
      ///   seat a Hero.IsLord member of the player's own clan - ExpulsionPolicy's own header documents companions
      ///   as disjoint from both rosters), AND the player's own clan currently has at least one eligible young
      ///   companion (<see cref="CalradiaRemembers.Assemblies.WardCandidateResolver.AnyEligibleWard" />, the EXACT
      ///   same predicate the lift re-runs, unlike most of the loose gates elsewhere in this file). The named
      ///   companion itself is re-resolved and re-checked against that same pool at the lift regardless
      ///   (CouncilLift.SettleSponsorWard, bridge is law).
      /// </summary>
      private bool AnySeatedMemberCanSponsorWard()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
         {
            Hero hero = Hero.FindFirst(h => h.StringId == p.HeroStringId);
            if (hero is not {IsAlive: true, IsLord: true} || hero.IsPrisoner || hero == Hero.MainHero) continue;

            if (CalradiaRemembers.Assemblies.WardCandidateResolver.AnyEligibleWard(hero)) return true;
         }

         return false;
      }

      /// <summary>Whether the player's own clan currently holds any alive, unwed kin other than the player: the half of <see cref="AnySeatedAllyHasMarriageableKin" /> that never depends on who is seated.</summary>
      private static bool PlayerClanHasUnwedKin()
      {
         Clan clan = Clan.PlayerClan;
         if (clan?.Heroes == null) return false;

         foreach (Hero member in clan.Heroes)
            if (member != Hero.MainHero && member is {IsAlive: true, Spouse: null})
               return true;

         return false;
      }

      /// <summary>
      ///   The WarCouncil's own gate: declare_war's PlayerCanDeclareWar fact (ResolutionOfferingResolver.OfferedKindKeys).
      ///   True once the player leads their own MapFaction (kingdom ruler, or an independent clan that IS its own
      ///   faction, see Hero.IsFactionLeader) AND at least one other faction exists that is neither the player's
      ///   own nor already at war. (Discovery note: vanilla Bannerlord v1.3.15 has no formal "allied faction"
      ///   stance to also exclude here, see CouncilLift.SettleDeclareWar's own doc for the ilspycmd-confirmed
      ///   FactionManager API surface.) Mirrors PlayerCanAffordStipend's own shape: proves only that SOME valid
      ///   target exists, never which one the LLM will actually name; the real target named in target_faction is
      ///   re-resolved and re-checked at the lift (CouncilLift.SettleDeclareWar, bridge is law).
      /// </summary>
      private static bool PlayerCanDeclareWar()
      {
         if (Hero.MainHero is not {IsFactionLeader: true}) return false;

         IFaction playerFaction = Hero.MainHero.MapFaction;
         if (playerFaction == null) return false;

         bool anyKingdomTarget = Kingdom.All != null && Kingdom.All.Any(k =>
            k != playerFaction && !FactionManager.IsAtWarAgainstFaction(playerFaction, k));

         if (anyKingdomTarget) return true;

         return Clan.All != null && Clan.All.Any(c =>
            c.Kingdom == null && c != playerFaction && !c.IsEliminated
            && !FactionManager.IsAtWarAgainstFaction(playerFaction, c));
      }

      /// <summary>
      ///   The Parley's own gate, declare_war's symmetric counterpart: make_peace's PlayerCanMakePeace fact
      ///   (ResolutionOfferingResolver.OfferedKindKeys). Unlike PlayerCanDeclareWar (a static, scope-wide check
      ///   over EVERY possible target), this reads <see cref="_councilParleyTargetFactionId" />, the ONE faction
      ///   the parley was actually convened against: true once the player leads their own faction AND that
      ///   faction is currently at war with the stored parley target. Instance (not static) because it needs the
      ///   session's own stored target; the real faction is re-resolved and re-checked again at the lift
      ///   regardless (CouncilLift.SettleMakePeace, bridge is law).
      /// </summary>
      private bool PlayerCanMakePeace()
      {
         if (Hero.MainHero is not {IsFactionLeader: true}) return false;

         IFaction playerFaction = Hero.MainHero.MapFaction;
         if (playerFaction == null) return false;

         IFaction target = CalradiaRemembers.Assemblies.ParleyFactionLookup.ByStringId(_councilParleyTargetFactionId);
         if (target == null) return false;

         return FactionManager.IsAtWarAgainstFaction(playerFaction, target);
      }

      #region Scene image on the thinking placeholder

      /// <summary>
      ///   Paints a council "thinking" placeholder (<see cref="CouncilPlaceholderText" />) with a situational
      ///   SCENE image instead of the anchor's own portrait: no single seated member presides over a council, so
      ///   their face on the waiting bubble reads wrong (the narrator model already dropped that framing). Called
      ///   from both council placeholder sites (<see cref="ActivateCouncil" />'s opener and
      ///   <see cref="ExecuteSendCouncilMessage" />'s per-turn placeholder) right after the placeholder is built.
      ///   Every resolution step is guarded: any failure (terrain lookup, missing file on disk, ...) simply leaves
      ///   the placeholder with the ordinary anchor portrait <see cref="MakeSpeakerMessage" /> already gave it.
      /// </summary>
      private void ApplyCouncilSceneImage(ChatMessageViewModel placeholder)
      {
         try
         {
            if (placeholder == null) return;

            bool outside = Settlement.CurrentSettlement == null;
            bool scopeIsFamily = _councilScope == CouncilScope.Family;
            bool ownedByPlayer = Settlement.CurrentSettlement?.OwnerClan == Clan.PlayerClan;
            CouncilSceneTerrain terrain = ResolveCouncilSceneTerrain();
            bool playerIsFemale = Hero.MainHero?.IsFemale == true;

            string basename = CouncilSceneImagePolicy.Resolve(outside, scopeIsFamily, ownedByPlayer, terrain, playerIsFemale);
            string path = Path.Combine(BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", basename + ".jpg");

            placeholder.ApplyScenePortrait(path);
         }
         catch (Exception ex)
         {
            ModLog.Error($"ChatViewModel.ApplyCouncilSceneImage failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Maps the engine terrain at the player party's map position to the coarse bucket
      ///   <see cref="CouncilSceneImagePolicy" /> cares about (Gabriel commissioned art only for Desert and
      ///   Steppe; every other TerrainType falls back to Other). Guards a missing map scene or party (Other).
      /// </summary>
      private static CouncilSceneTerrain ResolveCouncilSceneTerrain()
      {
         try
         {
            if (Campaign.Current?.MapSceneWrapper == null || MobileParty.MainParty == null) return CouncilSceneTerrain.Other;

            TerrainType terrain = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(MobileParty.MainParty.Position);

            return terrain switch {
               TerrainType.Desert => CouncilSceneTerrain.Desert,
               TerrainType.Steppe => CouncilSceneTerrain.Steppe,
               _ => CouncilSceneTerrain.Other
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"ChatViewModel.ResolveCouncilSceneTerrain failed: {ex.GetType().Name}: {ex.Message}");

            return CouncilSceneTerrain.Other;
         }
      }

      #endregion

      #region Engagements extraction (Inc 1)

      /// <summary>
      ///   Kicks a background reading of the council's standing engagements, unless one is already in flight, the
      ///   LLM is unavailable, or this is not a council. Fired once per player turn (from ExecuteSendMessage), so
      ///   the right-hand Engagements panel mirrors what the table has settled through the previous turn. Fire and
      ///   forget: a failed or slow reading simply leaves the panel as it was. There is no cancel button by design
      ///   (Gabriel 2026-07-30); a decision is walked back purely by the conversation no longer asserting it, which
      ///   the next reading picks up.
      /// </summary>
      private void KickCouncilEngagementExtraction()
      {
         if (_councilScope == null || _engagementExtractionRunning) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.LlmServices is not {IsAvailable: true}) return;

         _engagementExtractionRunning = true;
         _ = RunCouncilEngagementExtractionAsync();
      }

      /// <summary>
      ///   Reads the whole conversation, asks the extractor which decisions currently stand, and reconciles the
      ///   ledger to that snapshot (a decision walked back in the talk simply stops appearing and is dropped). The
      ///   LLM runs off-thread; the ledger mutation and the binding refresh happen back on the main thread. Never
      ///   throws, and always clears the in-flight guard.
      /// </summary>
      private async Task RunCouncilEngagementExtractionAsync()
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            string conversation = _transcript.ToPlainText(PlayerTranscriptLabel());
            if (string.IsNullOrWhiteSpace(conversation) || behavior?.LlmServices?.LlmClient == null) return;

            var extractor = new CouncilEngagementExtractor(behavior.LlmServices.LlmClient);
            string raw = null;

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CouncilEngagementExtractTimeoutSeconds)))
            {
               try
               {
                  raw = await Task.Run(() => extractor.ExtractAsync(conversation, cts.Token)).ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  ModLog.Error($"[ENGAGEMENTS] extraction call failed: {netEx.Message}");
               }
            }

            await MainThreadDispatcher.SwitchToMainThread();

            // The window may have closed or the council ended while the call was in flight.
            if (_councilScope == null || raw == null) return;

            _engagementLedger.Reconcile(EngagementSnapshotParser.Parse(raw));
            RefreshEngagementsBinding();
            ModLog.Info($"[ENGAGEMENTS] reconciled: {_engagementLedger.Engagements.Count} standing.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"RunCouncilEngagementExtractionAsync failed: {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _engagementExtractionRunning = false;
         }
      }

      /// <summary>Rebuilds the bound Engagements rows from the ledger and refreshes the empty-state gates.</summary>
      private void RefreshEngagementsBinding()
      {
         _engagements.Clear();

         foreach (CouncilEngagement e in _engagementLedger.Engagements)
            _engagements.Add(new CouncilEngagementViewModel(e.Subject, e.Text, DescribeEngagementStatus(e.Status)));

         OnPropertyChanged(nameof(HasEngagements));
         OnPropertyChanged(nameof(ShowEngagementsPlaceholder));
      }

      /// <summary>The seated-panel wording for an engagement's standing: settled versus still under discussion.</summary>
      private static string DescribeEngagementStatus(EngagementStatus status)
         => status == EngagementStatus.Agreed ? "Agreed" : "Discussed";

      #endregion

      #region Engagements seal (apply-at-End)

      /// <summary>
      ///   Council apply-at-End for engagements: real world-effect executors (grant fief, gold, etc.) do not
      ///   exist yet in this codebase (only a bandit-clear quest is executable, see CouncilLift.Settle), so an
      ///   AGREED engagement's applied effect is a durable memory sealed onto the concerned hero, so the NPC
      ///   remembers what the council settled. A decision bound to a seated member (see
      ///   <see cref="EngagementSubjectMatcher" />) seals in the first person on THEM; a collective or unresolved
      ///   subject seals on the presiding anchor instead. What the player saw standing in the Engagements panel
      ///   at the last reconciliation is exactly what is sealed here: the ledger is one turn behind, never
      ///   mid-turn, which is the intended "only what was seen applies" property the whole feature is built on.
      ///   Called once from <see cref="FinalizeCouncilResolutions" />, under the same idempotency guard, so it
      ///   fires once per close.
      /// </summary>
      public void SealCouncilEngagements(NpcMemoryCampaignBehavior behavior, int gameDay)
      {
         try
         {
            if (behavior?.Store == null) return;

            var names = new List<string>();
            var heroIds = new List<string>();

            foreach (CouncilParticipantViewModel seat in _councilParticipants)
            {
               names.Add(seat.Name);
               heroIds.Add(seat.HeroStringId);
            }

            string anchorHeroId = MainSpeaker()?.Hero?.StringId;

            foreach (CouncilEngagement e in _engagementLedger.PendingCommit())
            {
               if (string.IsNullOrWhiteSpace(e.Text)) continue;

               int idx = EngagementSubjectMatcher.MatchIndex(e.Subject, names);
               bool onBound = idx >= 0;
               string heroId = onBound ? heroIds[idx] : anchorHeroId;

               if (string.IsNullOrEmpty(heroId)) continue;

               string summary = EngagementSealText.Summarize(e.Text, onBound);
               if (string.IsNullOrEmpty(summary)) continue;

               NpcProfile profile = behavior.Store.Get(heroId);
               if (profile == null) continue;

               profile.Events.Add(new NotableEvent(gameDay, NotableEventType.Agreement, summary));
               behavior.Store.Set(profile);
               ModLog.Info($"[ENGAGEMENTS] sealed onto {heroId}: {summary}");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"SealCouncilEngagements failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion

      #region IRoundTableHost (council seat highlight)

      void IRoundTableHost.SetActiveCouncilSpeaker(ChatSpeaker speaker) => SetActiveCouncilSpeaker(speaker?.Hero?.StringId);

      void IRoundTableHost.ClearActiveCouncilSpeaker() => ClearActiveCouncilSpeaker();

      #endregion
   }
}
