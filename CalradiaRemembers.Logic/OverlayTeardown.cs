// Code written by Gabriel Mailhot, 21/06/2026.
// The pure state machine behind the chat overlay's "garbage-collected" close: removing the layer from
// inside the UI LateUpdate NREs GauntletUI (the letter-reply crash), so on the map we mark the overlay
// for destruction, let a native tick fire, and tear it down then. The lifecycle (pending flag, the
// map-vs-mission branch, destroy-exactly-once, clearing a stale flag) is isolated behind an effects
// port so it can be unit-tested; the host performs the irreducible engine work (RemoveLayer, time).

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The engine actions the teardown drives — supplied by the host (the mod), faked by tests.
    ///   Pure logic decides WHICH to invoke and in what order; this port performs them.
    /// </summary>
    public interface IOverlayTeardownEffects
    {
        /// <summary>True while the overlay is up (its layer still attached).</summary>
        bool IsOpen { get; }

        /// <summary>True when a mission/scene is active — the native campaign tick will NOT fire, so defer is unsafe.</summary>
        bool InMission { get; }

        /// <summary>Records the session (memory/story log) and ends the vanilla conversation — data, not UI; safe now.</summary>
        void FinalizeSession();

        /// <summary>Un-pauses campaign time so a native per-frame tick fires to run the deferred destroy.</summary>
        void UnpauseForTick();

        /// <summary>Tears the overlay down (remove layer, restore time, free resources) — called on the native tick.</summary>
        void DestroyNow();

        /// <summary>The synchronous fallback close, used off the map where no campaign tick will fire.</summary>
        void SynchronousClose();
    }

    /// <summary>
    ///   Two-step overlay close. <see cref="RequestClose"/> either closes synchronously (in a mission)
    ///   or, on the map, finalizes + un-pauses + marks the overlay pending; a later <see cref="Tick"/>
    ///   (from a native frame, outside the UI update) destroys it once. <see cref="Reset"/> clears the
    ///   flag when the overlay is freed by any other path, so it can never destroy a fresh overlay.
    /// </summary>
    public sealed class OverlayTeardown
    {
        /// <summary>True once a deferred destroy is awaiting the next tick.</summary>
        public bool IsPending { get; private set; }

        /// <summary>
        ///   Begins a close. A no-op if the overlay is not open or a destroy is already pending. In a
        ///   mission, closes synchronously (no tick to defer to). On the map, finalizes the session,
        ///   un-pauses so a tick will fire, and marks the overlay pending — the layer is left up until
        ///   the tick removes it outside the UI update.
        /// </summary>
        public void RequestClose(IOverlayTeardownEffects fx)
        {
            if (fx == null || IsPending || !fx.IsOpen) return;

            if (fx.InMission)
            {
                fx.SynchronousClose();
                return;
            }

            fx.FinalizeSession();
            fx.UnpauseForTick();
            IsPending = true;
        }

        /// <summary>
        ///   Runs the deferred destroy if one is pending — exactly once. The flag is cleared first, so
        ///   repeated ticks (or an overlay already gone) never destroy twice or destroy a fresh overlay.
        /// </summary>
        public void Tick(IOverlayTeardownEffects fx)
        {
            if (!IsPending) return;
            IsPending = false;
            if (fx == null || !fx.IsOpen) return;
            fx.DestroyNow();
        }

        /// <summary>Clears a pending destroy — called when the overlay is freed by any other close path.</summary>
        public void Reset() => IsPending = false;
    }
}
