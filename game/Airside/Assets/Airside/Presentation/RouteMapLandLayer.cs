using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The route map's land fill: a texture of the coast at the current zoom's level of detail, baked on a
    /// worker thread for the visible window plus a margin. The previous texture keeps drawing (in its own
    /// coordinates) until the next one lands, so panning and zooming never stall a frame or flash empty.
    /// Presentation only; the vector coastline is still drawn over the top, so the shore stays crisp
    /// between bakes.
    /// </summary>
    public sealed class RouteMapLandLayer
    {
        public static readonly Color32 Land = new(30, 47, 56, 255);
        private static readonly Color32 Sea = new(30, 47, 56, 0);
        private const float MinSecondsBetweenBakes = 0.12f;

        private Texture2D _texture;
        private double _west, _east, _south, _north;
        private float _bakedPixelsPerDegree;
        private MapDetail _bakedDetail;
        private Task<Color32[]> _task;
        private double _pendingWest, _pendingEast, _pendingSouth, _pendingNorth;
        private float _pendingPixelsPerDegree;
        private MapDetail _pendingDetail;
        private int _pendingWidth, _pendingHeight;
        private float _lastFinished = -10f;

        /// <summary>Draws the land into <paramref name="mapRect"/> (GUI space). Call on Repaint only.</summary>
        public void Draw(Rect mapRect, AustraliaMapLens lens)
        {
            Refresh(mapRect, lens);
            if (_texture == null)
                return;
            var width = mapRect.width;
            var height = mapRect.height;
            lens.Project(0f, 0f, width, height, _west, _north, out var left, out var top);
            lens.Project(0f, 0f, width, height, _east, _south, out var right, out var bottom);
            GUI.BeginGroup(mapRect);
            GUI.DrawTexture(new Rect(left, top, right - left, bottom - top), _texture, ScaleMode.StretchToFill, true);
            GUI.EndGroup();
        }

        private void Refresh(Rect mapRect, AustraliaMapLens lens)
        {
            if (_task != null && _task.IsCompleted)
            {
                var pixels = _task.IsFaulted ? null : _task.Result;
                _task = null;
                _lastFinished = Time.unscaledTime;
                if (pixels != null)
                {
                    Upload(pixels, _pendingWidth, _pendingHeight);
                    _west = _pendingWest; _east = _pendingEast; _south = _pendingSouth; _north = _pendingNorth;
                    _bakedPixelsPerDegree = _pendingPixelsPerDegree;
                    _bakedDetail = _pendingDetail;
                }
            }

            if (_task != null || Time.unscaledTime - _lastFinished < MinSecondsBetweenBakes)
                return;
            var detail = lens.Detail;
            if (_texture != null && detail == _bakedDetail && RouteMapLandWindow.Serves(lens, mapRect.width,
                    mapRect.height, _west, _east, _south, _north, _bakedPixelsPerDegree))
                return;

            RouteMapLandWindow.Bounds(lens, mapRect.width, mapRect.height, out var west, out var east, out var south, out var north);
            RouteMapLandWindow.TextureSize(mapRect.width, mapRect.height, out var pixelsWide, out var pixelsHigh);
            _pendingWest = west; _pendingEast = east; _pendingSouth = south; _pendingNorth = north;
            _pendingPixelsPerDegree = lens.PixelsPerDegree(mapRect.width, mapRect.height);
            _pendingDetail = detail;
            _pendingWidth = pixelsWide; _pendingHeight = pixelsHigh;
            var rings = new List<float[]>(MapGeography.Coasts(detail));
            _task = Task.Run(() =>
            {
                try
                {
                    return RegionalMiniMap.BakeRings(rings, pixelsWide, pixelsHigh, west, east, south, north, Land, Sea, true);
                }
                catch (System.Exception)
                {
                    return null;
                }
            });
        }

        private void Upload(Color32[] pixels, int width, int height)
        {
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null) Object.Destroy(_texture);
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Route map land", filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
                };
            }

            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
        }

        public void Dispose()
        {
            if (_texture != null) Object.Destroy(_texture);
            _texture = null;
            _task = null;
        }
    }
}
