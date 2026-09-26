#nullable enable
using System;
using System.Globalization;

namespace Odyssey.Hud
{
    /// <summary>
    /// Zoom and pan on the World map (design 59 §9c, the specification's "Zoom and pan"): 1× to 4× in
    /// steps of ×1.5, eased over 180 ms, centred with no pan at 1×, the north–south pan held so the map
    /// cannot leave the box, and east–west free because the planet wraps.
    ///
    /// <para>Engine-free and in map pixels at 1× (<see cref="WorldMapGeometry"/>), so the arithmetic a
    /// click depends on — screen to tile through the fit, the zoom, the pan and the wrap — is tested in
    /// the fast tier. The page only reads <see cref="Scale"/> and <see cref="OriginX"/> to place its
    /// three copies of the texture and its labels.</para>
    /// </summary>
    public sealed class WorldMapView
    {
        readonly WorldMapGeometry _map;
        float _boxWidth = 1f, _boxHeight = 1f;

        // The eased transition: from where it was to where it is going.
        float _fromZoom = 1f, _fromPanX, _fromPanY;
        float _toZoom = 1f, _toPanX, _toPanY;
        float _progress = 1f;

        public WorldMapView(WorldMapGeometry map)
        {
            _map = map;
        }

        public WorldMapGeometry Map => _map;

        /// <summary>Raised whenever what is drawn moves: a new target, or a step of the ease.</summary>
        public event Action? Changed;

        /// <summary>The map box's size in screen pixels. The fit is recomputed from it.</summary>
        public void Resize(float width, float height)
        {
            if (width <= 0 || height <= 0) return;
            if (Math.Abs(width - _boxWidth) < 0.01f && Math.Abs(height - _boxHeight) < 0.01f) return;
            _boxWidth = width;
            _boxHeight = height;
            _toPanY = ClampPanY(_toPanY, _toZoom);
            _fromPanY = ClampPanY(_fromPanY, _fromZoom);
            Changed?.Invoke();
        }

        public float BoxWidth => _boxWidth;
        public float BoxHeight => _boxHeight;

        /// <summary>The scale at which the whole map just fits the box: <c>object-fit: contain</c>.</summary>
        public float Fit => Math.Min(_boxWidth / _map.Width, _boxHeight / _map.Height);

        /// <summary>The zoom drawn now, part way through an ease.</summary>
        public float Zoom => Lerp(_fromZoom, _toZoom);

        /// <summary>The zoom being eased to: what the readout and the next step start from.</summary>
        public float TargetZoom => _toZoom;

        public float PanX => Lerp(_fromPanX, _toPanX);
        public float PanY => Lerp(_fromPanY, _toPanY);

        /// <summary>Screen pixels per map pixel, now.</summary>
        public float Scale => Fit * Zoom;

        public bool Zoomed => _toZoom > ZoomFloor;

        /// <summary>Whether an ease is still running and the page must keep calling <see cref="Tick"/>.</summary>
        public bool Moving => _progress < 1f;

        /// <summary>"1x", "1.5x", "2.3x": the readout under the plus.</summary>
        public string ZoomLabel =>
            (Math.Round(_toZoom * 10f) / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "x";

        /// <summary>Where the centre copy of the map's left edge sits on screen.</summary>
        public float OriginX => _boxWidth / 2f + (-_map.Width / 2f - PanX) * Scale;

        public float OriginY => _boxHeight / 2f + (-_map.Height / 2f - PanY) * Scale;

        public float ToScreenX(float mapX) => OriginX + mapX * Scale;
        public float ToScreenY(float mapY) => OriginY + mapY * Scale;

        public float ToMapX(float screenX) => (screenX - OriginX) / Scale;
        public float ToMapY(float screenY) => (screenY - OriginY) / Scale;

        /// <summary>The tile under a point in the box, or −1 off the top or bottom of the planet.</summary>
        public int TileAt(float screenX, float screenY) => _map.TileAt(ToMapX(screenX), ToMapY(screenY));

        /// <summary>One step closer, about a point in the box (the pointer, or the centre).</summary>
        public void ZoomIn(float screenX, float screenY) => ZoomTo(Math.Min(WorldLayout.ZoomMax, _toZoom * WorldLayout.ZoomStep), screenX, screenY);

        public void ZoomOut(float screenX, float screenY) => ZoomTo(_toZoom / WorldLayout.ZoomStep, screenX, screenY);

        public void ZoomIn() => ZoomIn(_boxWidth / 2f, _boxHeight / 2f);
        public void ZoomOut() => ZoomOut(_boxWidth / 2f, _boxHeight / 2f);

        /// <summary>Back to 1× and the centre.</summary>
        public void FitToBox() => Target(1f, 0f, 0f);

        /// <summary>
        /// Zoom to a level, keeping the map point under a screen point where it is. At 1× the pan is
        /// forced back to nought, as the specification says.
        /// </summary>
        public void ZoomTo(float zoom, float screenX, float screenY)
        {
            zoom = Math.Max(WorldLayout.ZoomMin, Math.Min(WorldLayout.ZoomMax, zoom));
            if (zoom <= ZoomFloor) { Target(1f, 0f, 0f); return; }

            // The map point under the pointer, by the target transform the player is steering.
            float scaleNow = Fit * _toZoom;
            float mapX = _map.Width / 2f + _toPanX + (screenX - _boxWidth / 2f) / scaleNow;
            float mapY = _map.Height / 2f + _toPanY + (screenY - _boxHeight / 2f) / scaleNow;

            float scaleNext = Fit * zoom;
            float panX = mapX - _map.Width / 2f - (screenX - _boxWidth / 2f) / scaleNext;
            float panY = mapY - _map.Height / 2f - (screenY - _boxHeight / 2f) / scaleNext;
            Target(zoom, panX, panY);
        }

        /// <summary>Drag or arrow-key the map by a screen distance. Nothing at 1×.</summary>
        public void PanBy(float screenDx, float screenDy)
        {
            if (!Zoomed) return;
            float scale = Fit * _toZoom;
            Target(_toZoom, _toPanX - screenDx / scale, _toPanY - screenDy / scale, immediate: true);
        }

        /// <summary>Advance the ease by a frame's real seconds.</summary>
        public void Tick(float seconds)
        {
            if (_progress >= 1f) return;
            _progress = Math.Min(1f, _progress + seconds / WorldLayout.ZoomEaseSeconds);
            if (_progress >= 1f) Settle();
            Changed?.Invoke();
        }

        void Target(float zoom, float panX, float panY, bool immediate = false)
        {
            _fromZoom = Zoom;
            _fromPanX = PanX;
            _fromPanY = PanY;
            _toZoom = zoom;
            _toPanX = zoom <= ZoomFloor ? 0f : panX;
            _toPanY = zoom <= ZoomFloor ? 0f : ClampPanY(panY, zoom);
            _progress = immediate ? 1f : 0f;
            if (immediate) Settle();
            Changed?.Invoke();
        }

        /// <summary>Once still, fold the east–west pan back into one turn of the planet so it never grows without bound.</summary>
        void Settle()
        {
            float wrap = _map.WrapWidth;
            float folded = _toPanX % wrap;
            if (folded > wrap / 2f) folded -= wrap;
            if (folded < -wrap / 2f) folded += wrap;
            _toPanX = folded;
            _fromZoom = _toZoom;
            _fromPanX = _toPanX;
            _fromPanY = _toPanY;
        }

        /// <summary>The north–south pan that keeps the map covering the box, or centred when it is shorter than the box.</summary>
        float ClampPanY(float panY, float zoom)
        {
            float visibleHalf = _boxHeight / (2f * Fit * zoom);
            float room = _map.Height / 2f - visibleHalf;
            if (room <= 0f) return 0f;
            return Math.Max(-room, Math.Min(room, panY));
        }

        float Lerp(float from, float to)
        {
            if (_progress >= 1f) return to;
            float t = 1f - (1f - _progress) * (1f - _progress) * (1f - _progress); // ease out, cubic
            return from + (to - from) * t;
        }

        const float ZoomFloor = 1.001f;
    }
}
