using HMS.Modules.Identity.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Core.Interfaces
{
    public interface IUserService
    {
        Task<bool> RegisterAsync(RegisterRequest request);
    }
}
