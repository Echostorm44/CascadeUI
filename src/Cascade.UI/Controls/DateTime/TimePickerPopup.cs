namespace Cascade.UI;

/// <summary>What a point in a <see cref="TimePicker"/>'s popup is over.</summary>
internal enum TimePickerPopupPart
{
    None,
    WheelRow,   // a row of a wheel column (Column, Offset from the chosen value)
    Header,     // the clock's hour or minute digits (Column 0 or 1)
    AmPm,       // AM or PM (Column 0 = AM, 1 = PM)
    Dial,       // the clock face
}

/// <summary>
/// Where everything in a time picker's popup is, for its style: wheels of hours, minutes and
/// AM/PM, or a clock face with the time above it. One arrangement for painting and input.
/// </summary>
internal sealed class TimePickerPopup
{
    internal const float Pad = 10f;
    internal const float RowHeight = 32f;
    internal const int VisibleRows = 5;
    internal const float HeaderHeight = 44f;
    internal const float DialSize = 220f;

    internal Rect Bounds { get; private set; }
    internal List<Rect> Columns { get; } = [];
    internal Rect HourDigits { get; private set; }
    internal Rect MinuteDigits { get; private set; }
    internal Rect Am { get; private set; }
    internal Rect Pm { get; private set; }
    internal Point DialCenter { get; private set; }
    internal float DialRadius { get; private set; }

    /// <summary>The popup's size for a style (the painter places it under the field).</summary>
    internal static Size SizeFor(TimePicker tp, float fieldWidth) => tp.PopupDisplayStyle switch
    {
        TimePickerPopupStyle.Grid => new Size(Math.Max(fieldWidth, DialSize + Pad * 2f), Pad * 3f + HeaderHeight + DialSize),
        _ => new Size(Math.Max(fieldWidth, tp.Format == TimeFormat.Hour12 ? 220f : 160f), Pad * 2f + RowHeight * VisibleRows),
    };

    internal static TimePickerPopup Arrange(TimePicker tp, Rect bounds)
    {
        var p = new TimePickerPopup { Bounds = bounds };
        bool twelve = tp.Format == TimeFormat.Hour12;
        if (tp.PopupDisplayStyle == TimePickerPopupStyle.Grid)
        {
            float digitsW = 46f;
            float y = bounds.Y + Pad;
            float total = digitsW * 2f + 16f + (twelve ? 56f : 0f);
            float x = bounds.X + (bounds.Width - total) / 2f;
            p.HourDigits = new Rect(x, y, digitsW, HeaderHeight);
            p.MinuteDigits = new Rect(x + digitsW + 16f, y, digitsW, HeaderHeight);
            if (twelve)
            {
                float ax = p.MinuteDigits.Right + 10f;
                p.Am = new Rect(ax, y + 2f, 46f, HeaderHeight / 2f - 3f);
                p.Pm = new Rect(ax, y + HeaderHeight / 2f + 1f, 46f, HeaderHeight / 2f - 3f);
            }
            p.DialRadius = DialSize / 2f;
            p.DialCenter = new Point(bounds.X + bounds.Width / 2f, y + HeaderHeight + Pad + p.DialRadius);
            return p;
        }

        int columns = twelve ? 3 : 2;
        float w = (bounds.Width - Pad * 2f) / columns;
        for (int i = 0; i < columns; i++)
        {
            p.Columns.Add(new Rect(bounds.X + Pad + i * w, bounds.Y + Pad, w, RowHeight * VisibleRows));
        }
        return p;
    }

    /// <summary>The rect of a wheel row: <paramref name="offset"/> rows from the chosen one (−2 … 2).</summary>
    internal Rect Row(int column, int offset)
    {
        var c = Columns[column];
        return new Rect(c.X, c.Y + (offset + VisibleRows / 2) * RowHeight, c.Width, RowHeight);
    }

    internal (TimePickerPopupPart Part, int Column, int Offset) HitTest(Point p)
    {
        for (int c = 0; c < Columns.Count; c++)
        {
            if (Columns[c].Contains(p))
            {
                int row = (int)((p.Y - Columns[c].Y) / RowHeight);
                return (TimePickerPopupPart.WheelRow, c, Math.Clamp(row, 0, VisibleRows - 1) - VisibleRows / 2);
            }
        }
        if (HourDigits.Width > 0f && HourDigits.Contains(p))
        {
            return (TimePickerPopupPart.Header, 0, 0);
        }
        if (MinuteDigits.Width > 0f && MinuteDigits.Contains(p))
        {
            return (TimePickerPopupPart.Header, 1, 0);
        }
        if (Am.Width > 0f && Am.Contains(p))
        {
            return (TimePickerPopupPart.AmPm, 0, 0);
        }
        if (Pm.Width > 0f && Pm.Contains(p))
        {
            return (TimePickerPopupPart.AmPm, 1, 0);
        }
        if (DialRadius > 0f)
        {
            float dx = p.X - DialCenter.X, dy = p.Y - DialCenter.Y;
            if (dx * dx + dy * dy <= DialRadius * DialRadius)
            {
                return (TimePickerPopupPart.Dial, 0, 0);
            }
        }
        return (TimePickerPopupPart.None, 0, 0);
    }

    /// <summary>The angle of a point about the dial, clockwise from twelve o'clock, in turns (0–1).</summary>
    internal float TurnsAt(Point p)
    {
        float a = MathF.Atan2(p.X - DialCenter.X, -(p.Y - DialCenter.Y)) / (2f * MathF.PI);
        return a < 0f ? a + 1f : a;
    }

    /// <summary>Whether a point is on the inner ring (24-hour clocks put 13–00 inside 1–12).</summary>
    internal bool IsInner(Point p)
    {
        float dx = p.X - DialCenter.X, dy = p.Y - DialCenter.Y;
        return MathF.Sqrt(dx * dx + dy * dy) < DialRadius * 0.62f;
    }

    /// <summary>Where a number sits on the dial: twelve positions, on the outer or inner ring.</summary>
    internal Point DialPoint(int position, bool inner)
    {
        float r = DialRadius * (inner ? 0.48f : 0.8f);
        float a = position / 12f * 2f * MathF.PI;
        return new Point(DialCenter.X + MathF.Sin(a) * r, DialCenter.Y - MathF.Cos(a) * r);
    }
}
