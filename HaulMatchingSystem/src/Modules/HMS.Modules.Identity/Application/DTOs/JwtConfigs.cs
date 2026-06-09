using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class JwtConfigs
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// TTL refresh token
        /// </summary>
        public int ExpiredDate { get; set; }

        /// <summary>
        ///  time expired access token
        /// </summary>
        public int TTL { get; set; }
    }
}
