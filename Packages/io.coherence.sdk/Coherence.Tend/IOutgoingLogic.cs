namespace Coherence.Tend
{
    public interface IOutgoingLogic
    {
        int Count { get; }
        bool CanIncrementOutgoingSequence { get; }
        SequenceID LastReceivedByRemoteSequenceID { get; }
        SequenceID OutgoingSequenceID { get; set; }

        bool ReceivedByRemote(SequenceID receivedByRemoteID, ReceiveMask receivedByRemoteMask);
        SequenceID IncreaseOutgoingSequenceID();
        DeliveryInfo Dequeue();
    }
}
