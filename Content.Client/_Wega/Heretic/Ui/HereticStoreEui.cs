using Content.Client.Eui;
using Content.Client._Wega.Heretic.Ui;
using Content.Shared.Eui;
using Content.Shared.Heretic;
using JetBrains.Annotations;

namespace Content.Client._Wega.Heretic.Ui;

[UsedImplicitly]
public sealed class HereticStoreEui : BaseEui
{
    private readonly HereticStoreMenu _menu;

    public HereticStoreEui()
    {
        _menu = new HereticStoreMenu();
        _menu.OnClose += () => SendMessage(new HereticStoreCloseMessage());
        _menu.OnBuyPressed += node => SendMessage(new HereticBuyKnowledgeMessage(node));
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is HereticStoreState s)
            _menu.UpdateState(s);
    }

    public override void Opened() => _menu.OpenCentered();
    public override void Closed() => _menu.Close();
}
