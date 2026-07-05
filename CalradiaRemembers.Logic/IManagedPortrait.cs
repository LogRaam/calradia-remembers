// Code written by Gabriel Mailhot, 05/07/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   A UI element that owns exactly one character portrait — which, on screen, is backed by a GPU
    ///   render-target (a character tableau). The portrait cap policy releases the render-targets it must to
    ///   keep VRAM bounded, without knowing any engine type: it speaks only to this contract.
    /// </summary>
    public interface IManagedPortrait
    {
        /// <summary>
        ///   Stable identity of the portrayed participant (a Hero id, else the troop face id, else the name).
        ///   Elements sharing a key are the same face; only the most recent needs a live render-target.
        /// </summary>
        string PortraitKey { get; }

        /// <summary>True while this element still holds a rendered tableau (an allocated GPU render-target).</summary>
        bool HasLivePortrait { get; }

        /// <summary>
        ///   Frees this element's render-target by clearing the image id it renders from. The element's text is
        ///   left untouched, so a released chat line keeps its words and only loses its little portrait.
        /// </summary>
        void ReleasePortrait();
    }
}
