namespace HMS.Shared.Core.Interfaces
{
    public interface ISmsSender
    {
        Task SendSmsAsync(string phoneNumber, string message);
    }
}
