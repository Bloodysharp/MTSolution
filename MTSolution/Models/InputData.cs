using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTSolution.Models
{
    class InputData
    {
        public Dictionary<string, Resource> Hosts { get; set; } = new();
        public Dictionary<string, Resource> VirtualMachines { get; set; } = new();
    }
}
