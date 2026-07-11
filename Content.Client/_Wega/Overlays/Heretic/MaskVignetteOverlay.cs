using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Wega.Overlays;

public sealed partial class MaskVignetteOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> VignetteOverlay = "AshMaskVignette";
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _ashShader;

    public float Intensity;

    public MaskVignetteOverlay()
    {
        IoCManager.InjectDependencies(this);
        _ashShader = _prototypeManager.Index(VignetteOverlay).InstanceUnique();
        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        if (Intensity <= 0f)
            return;

        var handle = args.ScreenHandle;

        _ashShader.SetParameter("aspect", (float) args.ViewportBounds.Width / args.ViewportBounds.Height);
        _ashShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _ashShader.SetParameter("intensity", Intensity);

        handle.UseShader(_ashShader);
        handle.DrawRect(args.ViewportBounds, Color.White);
        handle.UseShader(null);
    }
}
