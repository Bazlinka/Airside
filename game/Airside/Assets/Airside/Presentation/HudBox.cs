using System;

namespace Airside.Presentation
{
    /// <summary>
    /// A rectangle in virtual HUD points. Deliberately free of UnityEngine so the
    /// workspace layouts built on it compile and run in the headless harness
    /// (scripts/test-domain.sh) and in the offline mockup renderer, not only inside an
    /// editor. Presentation converts to <c>UnityEngine.Rect</c> at draw time.
    /// </summary>
    public readonly struct HudBox : IEquatable<HudBox>
    {
        public static readonly HudBox Empty = new(0f, 0f, 0f, 0f);

        public HudBox(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width < 0f ? 0f : width;
            Height = height < 0f ? 0f : height;
        }

        private HudBox(float x, float y, float width, float height, bool signed)
        {
            X = x;
            Y = y;
            Width = signed ? width : width < 0f ? 0f : width;
            Height = signed ? height : height < 0f ? 0f : height;
        }

        /// <summary>
        /// A line from one corner to the other, keeping the sign of the extent. A plain box
        /// clamps negative sizes away, which would flatten every line that runs up or left.
        /// </summary>
        public static HudBox Segment(float x0, float y0, float x1, float y1) =>
            new(x0, y0, x1 - x0, y1 - y0, signed: true);

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public bool IsEmpty => Width <= 0f || Height <= 0f;

        /// <summary>Shrink on every edge by <paramref name="amount"/>, never past zero size.</summary>
        public HudBox Inset(float amount) => Inset(amount, amount, amount, amount);

        public HudBox Inset(float left, float top, float right, float bottom) =>
            new(X + left, Y + top, Width - left - right, Height - top - bottom);

        /// <summary>A box of <paramref name="height"/> at <paramref name="y"/>, spanning this box's width.</summary>
        public HudBox Row(float y, float height) => new(X, y, Width, height);

        public HudBox WithHeight(float height) => new(X, Y, Width, height);
        public HudBox WithWidth(float width) => new(X, Y, width, Height);
        public HudBox Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);

        /// <summary>The left <paramref name="width"/> points of this box.</summary>
        public HudBox SliceLeft(float width) => new(X, Y, Math.Min(width, Width), Height);

        /// <summary>The right <paramref name="width"/> points of this box.</summary>
        public HudBox SliceRight(float width)
        {
            var w = Math.Min(width, Width);
            return new HudBox(Right - w, Y, w, Height);
        }

        /// <summary>The top <paramref name="height"/> points of this box.</summary>
        public HudBox SliceTop(float height) => new(X, Y, Width, Math.Min(height, Height));

        /// <summary>The bottom <paramref name="height"/> points of this box.</summary>
        public HudBox SliceBottom(float height)
        {
            var h = Math.Min(height, Height);
            return new HudBox(X, Bottom - h, Width, h);
        }

        public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;

        public bool Overlaps(HudBox other) =>
            !IsEmpty && !other.IsEmpty
            && X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

        public bool Equals(HudBox other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object obj) => obj is HudBox other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Width:0.##} x {Height:0.##})";
    }

    /// <summary>
    /// Semantic colour role for a piece of HUD text or fill. The concrete colours live in
    /// <see cref="AirsidePalette"/>; keeping the role separate from the colour is what lets
    /// the offline mockup renderer paint exactly what the game paints.
    /// </summary>
    public enum HudTone
    {
        /// <summary>Primary Cloud text.</summary>
        Default,
        /// <summary>Secondary Concrete text — captions, column headers, unit suffixes.</summary>
        Muted,
        /// <summary>Coastal Blue — selection, links and the primary action.</summary>
        Accent,
        /// <summary>Safety Yellow — the one thing that needs the player right now.</summary>
        Caution,
        /// <summary>Clear Green — done, on time, available.</summary>
        Positive,
        /// <summary>Signal Red — refused, cancelled, overdue.</summary>
        Negative
    }
}
