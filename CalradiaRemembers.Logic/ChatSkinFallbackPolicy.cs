// Code written by Gabriel Mailhot, 04/10/2026.
// Which chat window opens when the chosen skin cannot be drawn. The Dark Leather (Manuscript) skin paints its End
// Conversation and Memories labels into its page art, over invisible hit-boxes; without the art the window is
// see-through and shows no way out (EmperorHaterius, Nexus, 04/10/2026). Origin draws its own buttons.

namespace CalradiaRemembers.Logic
{
   /// <summary>The facts the chat window is chosen on.</summary>
   public sealed class ChatSkinFacts
   {
      /// <summary>The window asked for: the council's, a forced one, the console override, or the MCM choice.</summary>
      public string Requested { get; init; }

      public string ManuscriptSkin { get; init; }

      public string ClassicSkin { get; init; }

      /// <summary>The Dark Leather page art loaded. When false, that window would hide its own controls.</summary>
      public bool ManuscriptArtLoads { get; init; } = true;
   }

   /// <summary>Picks the window that will actually be usable. Pure.</summary>
   public static class ChatSkinFallbackPolicy
   {
      public static string Resolve(ChatSkinFacts f)
      {
         if (string.IsNullOrWhiteSpace(f?.Requested)) return f?.ClassicSkin;
         if (f.Requested == f.ManuscriptSkin && !f.ManuscriptArtLoads) return f.ClassicSkin;

         return f.Requested;
      }
   }
}
