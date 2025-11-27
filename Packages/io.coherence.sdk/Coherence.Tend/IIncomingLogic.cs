namespace Coherence.Tend
{
    public interface IIncomingLogic
    {
        SequenceID LastReceivedToUs { get; }
        ReceiveMask ReceiveMask { get; }

        bool ReceivedToUs(SequenceID nextId);
    }
}
