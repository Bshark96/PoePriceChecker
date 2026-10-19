using System;
using Windows.UI.Xaml;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Manages pointer hover and user interaction detection for the Game Bar overlay.
    /// Filters out synthetic pointer events generated when the overlay window pops up
    /// underneath a stationary cursor, while immediately pausing the auto-minimize timer
    /// on genuine cursor movement, pointer entry after restore, or user interaction.
    /// </summary>
    public sealed class PoeHoverDetector
    {
        private const double RestoreGraceWindowMs = 450.0;
        private const double MovementThresholdPixels = 5.0;
        private const double LeaveDebounceMs = 120.0;

        private readonly DispatcherTimer _leaveDebounceTimer;
        private DateTime _lastRestoreUtc = DateTime.MinValue;
        private bool _restoreGraceCleared;
        private bool _hasAnchorPoint;
        private double _anchorX;
        private double _anchorY;
        private bool _isHovered;

        public event Action<bool> HoverStateChanged;

        public bool IsHovered => _isHovered;

        public PoeHoverDetector()
        {
            _leaveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(LeaveDebounceMs)
            };
            _leaveDebounceTimer.Tick += OnLeaveDebounceTick;
        }

        /// <summary>
        /// Resets hover state when the overlay is freshly restored or starts a new countdown.
        /// Captures the restore timestamp so stationary cursor events under the popup are ignored
        /// until the user actually moves the mouse or interacts with the widget.
        /// </summary>
        public void ResetOnRestore()
        {
            _leaveDebounceTimer.Stop();
            _lastRestoreUtc = DateTime.UtcNow;
            _restoreGraceCleared = false;
            _hasAnchorPoint = false;
            _anchorX = 0;
            _anchorY = 0;
            SetHovered(false);
        }

        /// <summary>
        /// Clears hover state when the overlay is minimized or hidden.
        /// </summary>
        public void ResetOnDismiss()
        {
            _leaveDebounceTimer.Stop();
            _restoreGraceCleared = false;
            _hasAnchorPoint = false;
            _anchorX = 0;
            _anchorY = 0;
            SetHovered(false);
        }

        /// <summary>
        /// Marks the overlay as actively interacted with (e.g. button click, keyboard input, wheel scroll).
        /// Immediately pauses the auto-minimize countdown regardless of the restore grace window.
        /// </summary>
        public void OnUserInteraction()
        {
            _leaveDebounceTimer.Stop();
            _restoreGraceCleared = true;
            SetHovered(true);
        }

        /// <summary>
        /// Processes pointer hover telemetry from the WebView2 host bridge.
        /// </summary>
        public void OnPointerEvent(bool isHovered, string eventType, double x, double y)
        {
            if (!isHovered || string.Equals(eventType, "leave", StringComparison.OrdinalIgnoreCase))
            {
                if (_isHovered && !_leaveDebounceTimer.IsEnabled)
                {
                    _leaveDebounceTimer.Start();
                }
                return;
            }

            // Pointer is inside the overlay; cancel any pending leave debounce
            if (_leaveDebounceTimer.IsEnabled)
            {
                _leaveDebounceTimer.Stop();
            }

            if (_restoreGraceCleared)
            {
                SetHovered(true);
                return;
            }

            // Explicit user interaction (click, wheel, keydown) always clears the restore grace filter immediately
            if (string.Equals(eventType, "interact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, "down", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, "wheel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, "key", StringComparison.OrdinalIgnoreCase))
            {
                _restoreGraceCleared = true;
                SetHovered(true);
                return;
            }

            double elapsedMs = (DateTime.UtcNow - _lastRestoreUtc).TotalMilliseconds;
            bool hasCoords = !double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y);

            if (!_hasAnchorPoint)
            {
                if (elapsedMs < RestoreGraceWindowMs)
                {
                    // Overlay just appeared. Record initial cursor coordinates as anchor point
                    // so stationary synthetic hover events do not pause the countdown.
                    if (hasCoords)
                    {
                        _anchorX = x;
                        _anchorY = y;
                        _hasAnchorPoint = true;
                    }
                    return;
                }

                // Pointer entered after the restore grace window had already elapsed
                _restoreGraceCleared = true;
                SetHovered(true);
                return;
            }

            // Anchor point was captured when the overlay popped up under the cursor.
            // Require actual cursor displacement before pausing the timer.
            if (hasCoords)
            {
                double dx = x - _anchorX;
                double dy = y - _anchorY;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance >= MovementThresholdPixels)
                {
                    _restoreGraceCleared = true;
                    SetHovered(true);
                }
            }
            else if (elapsedMs >= RestoreGraceWindowMs)
            {
                _restoreGraceCleared = true;
                SetHovered(true);
            }
        }

        private void OnLeaveDebounceTick(object sender, object e)
        {
            _leaveDebounceTimer.Stop();
            SetHovered(false);
        }

        private void SetHovered(bool value)
        {
            if (_isHovered == value) return;
            _isHovered = value;
            HoverStateChanged?.Invoke(_isHovered);
        }
    }
}
