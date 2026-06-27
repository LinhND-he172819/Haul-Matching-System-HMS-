using System.Threading.Tasks;

namespace HMS.Shared.Core.Interfaces
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string phoneNumber, string content);
    }
}
