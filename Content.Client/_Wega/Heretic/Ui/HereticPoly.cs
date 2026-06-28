using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._Wega.Heretic.Ui;

public sealed class HereticPoly : Control
{
    public Color BgColor;
    public Color BorderColor = Color.Transparent;
    public Color? HoverBorder;
    public Color? Stripe;
    public Color? HoverStripe;
    public Texture? Icon;
    public float Bevel = 10f;
    public float BorderWidth = 1.5f;
    public float StripeWidth = 3f;
    public bool Octagon;
    public bool Clickable;
    public event Action? OnPressed;

    private bool _hover;
    private readonly List<Vector2> _verts = new(8);

    public HereticPoly(Color bg)
    {
        BgColor = bg;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;
        float w = PixelSize.X;
        float h = PixelSize.Y;
        var b = Bevel * s;
        var border = _hover && HoverBorder is { } hb ? hb : BorderColor;

        if (border.A > 0f)
        {
            var t = BorderWidth * s;
            FillPoly(handle, 0, 0, w, h, b, border);
            FillPoly(handle, t, t, w - t, h - t, MathF.Max(b - t, 1f), BgColor);
        }
        else
        {
            FillPoly(handle, 0, 0, w, h, b, BgColor);
        }

        if (Stripe is { } sc)
        {
            var sw = StripeWidth * s;
            var stripeCol = _hover && HoverStripe is { } hs ? hs : sc;
            handle.DrawRect(new UIBox2(0, b, sw, h - b), stripeCol);
        }

        if (Icon != null)
        {
            var pad = 4f * s;
            handle.DrawTextureRect(Icon, new UIBox2(pad, pad, w - pad, h - pad));
        }
    }

    private void FillPoly(DrawingHandleScreen handle, float x0, float y0, float x1, float y1, float b, Color c)
    {
        _verts.Clear();
        if (Octagon)
        {
            _verts.Add(new Vector2(x0 + b, y0));
            _verts.Add(new Vector2(x1 - b, y0));
            _verts.Add(new Vector2(x1, y0 + b));
            _verts.Add(new Vector2(x1, y1 - b));
            _verts.Add(new Vector2(x1 - b, y1));
            _verts.Add(new Vector2(x0 + b, y1));
            _verts.Add(new Vector2(x0, y1 - b));
            _verts.Add(new Vector2(x0, y0 + b));
        }
        else
        {
            _verts.Add(new Vector2(x0 + b, y0));
            _verts.Add(new Vector2(x1, y0));
            _verts.Add(new Vector2(x1, y1 - b));
            _verts.Add(new Vector2(x1 - b, y1));
            _verts.Add(new Vector2(x0, y1));
            _verts.Add(new Vector2(x0, y0 + b));
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _verts, c);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hover = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hover = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (Clickable && args.Function == EngineKeyFunctions.UIClick)
        {
            OnPressed?.Invoke();
            args.Handle();
        }
    }
}
