using Content.Shared._Wega.Heretic;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Wega.Overlays;

public sealed partial class MaskVignetteSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private MaskVignetteOverlay _overlay = default!;
    private bool _added;

    private const float SmoothRate = 8f;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        var player = _player.LocalEntity;
        if (player != null && TryComp<HereticMaskVictimComponent>(player, out var victim))
            target = victim.Stacks / 8f;

        var cur = _overlay.Intensity;
        cur += (target - cur) * MathF.Min(1f, frameTime * SmoothRate);
        if (cur < 0.0005f && target <= 0f)
            cur = 0f;
        _overlay.Intensity = cur;

        if (cur > 0f && !_added)
        {
            _overlayMan.AddOverlay(_overlay);
            _added = true;
        }
        else if (cur <= 0f && _added)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _added = false;
        }
    }
}
