using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Wega.Heretic.Ui;

[Virtual]
public class HereticWindow : BaseWindow
{
    private const float TitleBarHeight = 32f;

    private static readonly Color BorderColor = Color.FromHex("#5A4C82");
    private static readonly Color BgTop       = Color.FromHex("#3C3358");
    private static readonly Color BgBottom    = Color.FromHex("#2A2440");
    private static readonly Color RuleColor   = Color.FromHex("#332A48");
    private static readonly Color TitleColor  = Color.FromHex("#D9B45A");
    private static readonly Color CloseColor  = Color.FromHex("#9A90AE");
    private static readonly Color CloseHover  = Color.FromHex("#E8A0A0");

    private readonly Label _title;
    private readonly Control _contents;
    private readonly List<Vector2> _verts = new(6);
    private readonly DrawVertexUV2DColor[] _grad = new DrawVertexUV2DColor[6];

    public HereticWindow()
    {
        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        var res = IoCManager.Resolve<IResourceCache>();
        var titleFont = new VectorFont(res.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);

        var ent = IoCManager.Resolve<IEntityManager>();
        var sprite = ent.System<SpriteSystem>();
        var bookTex = sprite.Frame0(new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_Wega/Heretic/book.rsi"), "icon"));

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            MinSize = new Vector2(0, TitleBarHeight),
        };

        header.AddChild(new TextureRect
        {
            Texture = bookTex,
            Stretch = TextureRect.StretchMode.Scale,
            SetSize = new Vector2(20, 20),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        });

        _title = new Label
        {
            FontOverride = titleFont,
            FontColorOverride = TitleColor,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        header.AddChild(_title);
        header.AddChild(new Control { HorizontalExpand = true });

        var closeLabel = new Label
        {
            Text = "×",
            FontColorOverride = CloseColor,
            Margin = new Thickness(4, 0, 4, 0),
        };
        var closeBtn = new HereticPoly(Color.Transparent)
        {
            Clickable = true,
            MouseFilter = MouseFilterMode.Stop,
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(0, 8, 8, 0),
        };
        closeBtn.AddChild(closeLabel);
        closeBtn.OnMouseEntered += _ => closeLabel.FontColorOverride = CloseHover;
        closeBtn.OnMouseExited += _ => closeLabel.FontColorOverride = CloseColor;
        closeBtn.OnPressed += Close;
        header.AddChild(closeBtn);

        root.AddChild(header);

        root.AddChild(new HereticPoly(RuleColor)
        {
            Bevel = 0f,
            HorizontalExpand = true,
            MinSize = new Vector2(0, 1),
        });

        _contents = new Control
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(2),
        };
        root.AddChild(_contents);

        AddChild(root);
        XamlChildren = _contents.Children;
    }

    public string? Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return relativeMousePos.Y <= TitleBarHeight ? DragMode.Move : DragMode.None;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;
        float w = PixelSize.X, h = PixelSize.Y, b = 14f * s, t = 1.5f * s;

        FillCut(handle, 0, 0, w, h, b, BorderColor);
        FillCutGradient(handle, t, t, w - t, h - t, b - t, BgTop, BgBottom);
    }

    private void FillCut(DrawingHandleScreen handle, float x0, float y0, float x1, float y1, float b, Color c)
    {
        _verts.Clear();
        _verts.Add(new Vector2(x0 + b, y0));
        _verts.Add(new Vector2(x1, y0));
        _verts.Add(new Vector2(x1, y1 - b));
        _verts.Add(new Vector2(x1 - b, y1));
        _verts.Add(new Vector2(x0, y1));
        _verts.Add(new Vector2(x0, y0 + b));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _verts, c);
    }

    private void FillCutGradient(DrawingHandleScreen handle, float x0, float y0, float x1, float y1, float b, Color top, Color bottom)
    {
        var max = (x1 - x0) + (y1 - y0);

        _grad[0] = GradV(new Vector2(x0 + b, y0), x0, y0, max, top, bottom);
        _grad[1] = GradV(new Vector2(x1, y0), x0, y0, max, top, bottom);
        _grad[2] = GradV(new Vector2(x1, y1 - b), x0, y0, max, top, bottom);
        _grad[3] = GradV(new Vector2(x1 - b, y1), x0, y0, max, top, bottom);
        _grad[4] = GradV(new Vector2(x0, y1), x0, y0, max, top, bottom);
        _grad[5] = GradV(new Vector2(x0, y0 + b), x0, y0, max, top, bottom);

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, _grad);
    }

    private static DrawVertexUV2DColor GradV(Vector2 p, float x0, float y0, float max, Color top, Color bottom)
    {
        var f = max <= 0f ? 0f : ((p.X - x0) + (p.Y - y0)) / max;
        var col = Color.FromSrgb(new Color(
            top.R + (bottom.R - top.R) * f,
            top.G + (bottom.G - top.G) * f,
            top.B + (bottom.B - top.B) * f,
            1f));
        return new DrawVertexUV2DColor(p, new Vector2(0.5f, 0.5f), col);
    }
}
