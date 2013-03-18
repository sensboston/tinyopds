using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TinyOPDS.Data
{
    public class Genre
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public string Translation { get; set; }
        public List<Genre> Subgenres = new List<Genre>();
    }
}
