namespace CommandsService.EventProcessing
{
    public interface IEventProcessor
    {
        void PropcessEvent(string message);
    }
}
