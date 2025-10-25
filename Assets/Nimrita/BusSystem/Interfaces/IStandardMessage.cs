public interface IStandardMessage
{
    string MessageId { get; }
    bool RequiresResponse { get; }
}