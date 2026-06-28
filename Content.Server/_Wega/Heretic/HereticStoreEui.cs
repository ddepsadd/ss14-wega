using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Heretic;

namespace Content.Server.Heretic;

public sealed class HereticStoreEui(EntityUid heretic, HereticKnowledgeSystem knowledge) : BaseEui
{
    public override EuiStateBase GetNewState()
        => knowledge.BuildStoreState(heretic);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is HereticBuyKnowledgeMessage buy)
        {
            if (knowledge.TryPurchase(heretic, buy.Node))
                StateDirty();
        }
        else if (msg is HereticStoreCloseMessage)
            Close();
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }
    public override void Closed()
    {
        base.Closed();
        knowledge.OnStoreClosed(heretic);
    }
}
