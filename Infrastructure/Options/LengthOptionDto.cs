using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Options
{
    public sealed class LengthOptionDto
    {
        public string Key { get; set; } = "";   // "short" | "medium" | "long"
        public string Label { get; set; } = ""; // "Short (<100 min)" ...
        public int? Min { get; set; }           // minutes (inclusive), null = open
        public int? Max { get; set; }
    }
}
