using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTSolution.Models
{
    class OutputData
    {
        public Dictionary<string, List<string>> Allocations { get; set; } = new();
        public Dictionary<string, HostUtilization> HostUtilizations { get; set; } = new();
        public List<string> UnderutilizedHosts { get; set; } = new();
        public List<Migration> Migrations { get; set; } = new();
        public List<string> FailedPlacements { get; set; } = new();
    }
}
