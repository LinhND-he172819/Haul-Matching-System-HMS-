using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Core.Entities
{
    public class Hub
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public Point GeoLocation { get; set; } // Sử dụng NetTopologySuite.Geometries.Point
        public DateTime CreatedAt { get; set; }
    }
}
