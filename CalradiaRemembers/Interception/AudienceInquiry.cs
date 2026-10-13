// Code written by Gabriel Mailhot, 13/10/2026.
// Lordfadooboo (Nexus, 13/10/2026), seconded by fkasad: "(person) wants to speak to you" when often i find myself
// saying "who is that? and dont know if i want to actually talk to that person. an ability to open their encyclopedia
// page from that screen would be helpful."
//
// Every invitation where someone asks the player for a word goes through here. It does two things:
//  - it says who is asking, in the popup itself (Logic/Interception/AudienceWhoLine: name with house and culture, their
//    station, whether you have met, and their regard in the encyclopedia's words);
//  - it carries a "Who is this?" button (the AudienceWhoButton prefab patch + mixin). The game's popup is modal over
//    every screen, so the button closes it, opens the encyclopedia on that person, and raises the very same invitation
//    again when the encyclopedia closes (Logic/Interception/EncyclopediaDetour). An audience starts its cooldown when
//    it is offered, so the invitation must come back, whatever happens to the page.
// The button shows only on an invitation raised here (each one is remembered against its own InquiryData), only on the
// campaign map (where the encyclopedia's open state can be read), so no other popup in the game ever shows it.

#region

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Interception;
using CalradiaRemembers.Mapping;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Interception
{
   /// <summary>Raises an invitation to talk that says who is asking and can open their encyclopedia page.</summary>
   public static class AudienceInquiry
   {
      // The hero each invitation raised here is about. Weak, so a closed popup is forgotten with its data.
      private static readonly ConditionalWeakTable<InquiryData, string> HeroOf = new();

      // The invitation away while the encyclopedia is open, and how its detour stands.
      private static InquiryData _away;
      private static string _awayHeroId;
      private static bool _seenOpen;
      private static float _waited;

      /// <summary>The invitation the popup shows now, set by the popup patch (null when it shows none of ours).</summary>
      internal static InquiryData Showing { get; private set; }

      /// <summary>
      ///   Shows an invitation from <paramref name="hero" />. With <paramref name="sayWho" /> the body ends with who
      ///   they are (left off for a companion, whom the player knows); the "Who is this?" button is always offered.
      /// </summary>
      public static InquiryData Show(Hero hero, string title, string body, string accept, string decline, Action onAccept, Action onDecline, bool sayWho = true)
      {
         string text = body + (sayWho ? WhoLine(hero) : "");
         var data = new InquiryData(title, text, true, true, accept, decline, onAccept, onDecline);
         if (hero != null) HeroOf.Add(data, hero.StringId);
         InformationManager.ShowInquiry(data, true);

         return data;
      }

      /// <summary>The "Who: ..." paragraph for this hero (AudienceWhoLine), or empty.</summary>
      public static string WhoLine(Hero hero)
      {
         if (hero == null) return "";
         try
         {
            int? regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.ReputationWithPlayer;

            return AudienceWhoLine.For(new AudienceWhoFacts {
               Label = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = hero.Name?.ToString(), ClanName = hero.Clan?.Name?.ToString(), CultureName = hero.Culture?.Name?.ToString()}),
               Station = StationOf(hero),
               HasMet = hero.HasMet,
               Regard = regard
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AUDIENCE_WHO] who-line failed for {hero.Name}: {ex.GetType().Name}: {ex.Message}");

            return "";
         }
      }

      /// <summary>Whether the "Who is this?" button belongs on this popup: one of ours, about someone, on the campaign map.</summary>
      internal static bool OffersWho(InquiryData data)
         => data != null && HeroOf.TryGetValue(data, out string id) && !string.IsNullOrEmpty(id) && Mission.Current == null && Campaign.Current != null;

      /// <summary>The popup patch: the popup now shows <paramref name="data" /> (null when it is cleared).</summary>
      internal static void OnPopupShown(InquiryData data)
      {
         Showing = data;
         AudienceWhoButtonMixin.Current?.Refresh(OffersWho(data));
      }

      /// <summary>
      ///   The button. Deferred one frame: closing the popup from inside its own click is a close inside a widget update
      ///   (philip0105's Escape crash was that), and the audience popups already open their chats a frame later.
      /// </summary>
      internal static void OnWhoClicked()
      {
         InquiryData data = Showing;
         if (data == null || !HeroOf.TryGetValue(data, out string heroId)) return;

         MainThreadDispatcher.Enqueue(() => StartDetour(data, heroId, openPage: true));
      }

      /// <summary>Each frame: raises the invitation again once its encyclopedia detour is over.</summary>
      public static void Tick(float dt)
      {
         if (_away == null) return;

         bool open = IsEncyclopediaOpen();
         _seenOpen |= open;
         _waited += dt;
         if (EncyclopediaDetour.Next(new EncyclopediaDetourFacts {SeenOpen = _seenOpen, IsOpenNow = open, SecondsWaited = _waited}) != EncyclopediaDetourStep.RaiseAgain) return;

         InquiryData data = _away;
         string heroId = _awayHeroId;
         _away = null;
         _awayHeroId = null;

         Hero hero = Hero.FindFirst(h => h != null && h.StringId == heroId);
         if (hero == null || !HeroPresence.IsInTheWorld(hero))
         {
            ModLog.Info("[AUDIENCE_WHO] the one asking is no longer in the world: the invitation is not raised again.");

            return;
         }

         InformationManager.ShowInquiry(data, true, true);
         ModLog.Info($"[AUDIENCE_WHO] {hero.Name}'s invitation raised again after the encyclopedia ({_waited:0.0}s, seen open: {_seenOpen}).");
      }

      /// <summary>Self-test seam: whether this invitation carries the button's hero.</summary>
      internal static string DebugHeroOf(InquiryData data) => data != null && HeroOf.TryGetValue(data, out string id) ? id : null;

      /// <summary>Self-test seam: whether a detour is under way.</summary>
      internal static bool DebugDetourPending => _away != null;

      /// <summary>Self-test seam: starts a detour for this invitation as the button would, without the click and without the page.</summary>
      internal static void DebugStartDetour(InquiryData data) => StartDetour(data, DebugHeroOf(data), openPage: false);

      /// <summary>Self-test seam: forgets a detour under way.</summary>
      internal static void DebugCancelDetour()
      {
         _away = null;
         _awayHeroId = null;
      }

      #region private

      private static void StartDetour(InquiryData data, string heroId, bool openPage)
      {
         Hero hero = heroId != null ? Hero.FindFirst(h => h != null && h.StringId == heroId) : null;
         if (hero == null || Campaign.Current?.EncyclopediaManager == null) return;

         _away = data;
         _awayHeroId = heroId;
         _seenOpen = false;
         _waited = 0f;
         if (Showing == data) InformationManager.HideInquiry();
         if (!openPage) return;

         try { Campaign.Current.EncyclopediaManager.GoToLink(hero.EncyclopediaLink); }
         catch (Exception ex) { ModLog.Error($"[AUDIENCE_WHO] encyclopedia failed for {hero.Name}: {ex.GetType().Name}: {ex.Message}"); }

         ModLog.Info($"[AUDIENCE_WHO] {hero.Name}'s encyclopedia page opened from the invitation.");
      }

      private static string StationOf(Hero hero)
      {
         bool isLord = hero.IsLord;
         string occupation = null;
         if (!isLord)
         {
            try { occupation = GameTexts.FindText("str_occupation", hero.Occupation.ToString())?.ToString(); }
            catch
            {
               // a trade the game has no word for: the line keeps the name alone
            }
         }

         return AudienceStation.Describe(new AudienceStationFacts {
            IsLord = isLord,
            IsRuler = hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero,
            IsClanLeader = hero.Clan != null && hero.Clan.Leader == hero,
            ClanName = hero.Clan?.Name?.ToString(),
            RealmName = hero.Clan?.Kingdom?.Name?.ToString(),
            OccupationName = occupation,
            SettlementName = hero.IsNotable ? (hero.CurrentSettlement ?? hero.HomeSettlement)?.Name?.ToString() : null
         });
      }

      // MapScreen lives in SandBox.View, which CR reaches by reflection (as GameMenuOpener does). The visible screen is
      // asked first (a naval map sits over the land one), then the land map.
      private static bool IsEncyclopediaOpen()
      {
         try
         {
            if (IsOpenOn(ScreenManager.TopScreen)) return true;
            Type mapScreen = Type.GetType("SandBox.View.Map.MapScreen, SandBox.View");
            object instance = mapScreen?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

            return IsOpenOn(instance);
         }
         catch
         {
            return false;
         }
      }

      private static bool IsOpenOn(object screen)
      {
         object manager = screen?.GetType().GetProperty("EncyclopediaScreenManager", BindingFlags.Public | BindingFlags.Instance)?.GetValue(screen);

         return manager?.GetType().GetProperty("IsEncyclopediaOpen", BindingFlags.Public | BindingFlags.Instance)?.GetValue(manager) is true;
      }

      #endregion
   }
}
