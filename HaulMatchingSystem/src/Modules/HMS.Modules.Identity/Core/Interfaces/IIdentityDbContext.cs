using HMS.Modules.Identity.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Core.Interfaces
{
    public interface IIdentityDbContext
    {
        DbSet<User> Users { get; set; }
        DbSet<Hub> Hubs { get; set; }
        DbSet<Vehicle> Vehicles { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
