using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Application.Services;
using HMS.Modules.Identity.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace HMS.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        public AuthController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        /// <summary>
        /// API Đăng nhập hệ thống
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous] // Cho phép tất cả mọi người truy cập không cần token
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _authService.LoginAsync(request);

                if (result == null)
                    return Unauthorized(new { message = "Email, số điện thoại hoặc mật khẩu không chính xác!" });

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// API Làm mới AccessToken khi bị hết hạn
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous] // Vì lúc này accessToken cũ đã hết hạn nên API này không được chặn [Authorize]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.AccessToken, request.RefreshToken);

            if (result == null)
                return BadRequest(new { message = "RefreshToken không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại!" });

            return Ok(result);
        }

        /// <summary>
        /// API Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var isSuccess = await _userService.RegisterAsync(request);
                if (!isSuccess)
                    return BadRequest(new { message = "Đăng ký tài khoản thất bại, vui lòng thử lại!" });

                return Ok(new { message = "Đăng ký tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                // Bắt lỗi trùng email được quăng ra từ Service
                return BadRequest(new { message = ex.Message });
            }
        }
        /// <summary>
        /// API Yêu cầu OTP Đăng nhập
        /// </summary>
        [HttpPost("login-otp/request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestLoginOtp([FromBody] LoginOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                await _authService.RequestLoginOtpAsync(request);
                return Ok(new { message = "Mã OTP đã được gửi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// API Xác thực OTP Đăng nhập
        /// </summary>
        [HttpPost("login-otp/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyLoginOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _authService.VerifyLoginOtpAsync(request);
                if (result == null) return Unauthorized(new { message = "Xác thực thất bại." });
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// API Yêu cầu OTP Đăng ký
        /// </summary>
        [HttpPost("register-otp/request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestRegisterOtp([FromBody] RegisterOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                await _authService.RequestRegisterOtpAsync(request);
                return Ok(new { message = "Mã OTP đã được gửi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// API Xác thực OTP Đăng ký
        /// </summary>
        [HttpPost("register-otp/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyRegisterOtp([FromBody] VerifyRegisterOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _authService.VerifyRegisterOtpAsync(request);
                if (result == null) return BadRequest(new { message = "Xác thực thất bại." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
