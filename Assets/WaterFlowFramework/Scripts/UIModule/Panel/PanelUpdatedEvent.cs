using WaterFlow.Framework.Systems.EventBus;

namespace WaterFlow.Framework.UIModule
{
    public struct PanelUpdatedEvent : IEvent
    {
        public bool isOpen;
    }

    public struct BlockGeneralCurrencyEvent : IEvent
    {
        public bool isBlock;
    }
}