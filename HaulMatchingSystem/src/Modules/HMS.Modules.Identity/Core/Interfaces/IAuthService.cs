using HMS.Modules.Identity.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken);
        Task RequestLoginOtpAsync(LoginOtpRequest request);
        Task<AuthResponse?> VerifyLoginOtpAsync(VerifyLoginOtpRequest request);
        Task RequestRegisterOtpAsync(RegisterOtpRequest request);
        Task<AuthResponse?> VerifyRegisterOtpAsync(VerifyRegisterOtpRequest request);
    }
}
