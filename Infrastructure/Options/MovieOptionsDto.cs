using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Options
{
    public sealed class MovieOptionsDto
    {
        public List<LengthOptionDto> Lengths { get; set; } = [];
        public List<object> Genres { get; set; } = []; // [{ id, name }]
    }
}
