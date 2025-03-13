using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTSolution.Models
{
    class Resource
    {
        public int Cpu { get; }
        public int Ram { get; }
        public int CpuUsed { get; private set; } = 0;
        public int RamUsed { get; private set; } = 0;

        public Resource(int cpu, int ram)
        {
            Cpu = cpu;
            Ram = ram;
        }

        public bool CanHost(Resource vm) => CpuUsed + vm.Cpu <= Cpu && RamUsed + vm.Ram <= Ram;
        public void Allocate(Resource vm) { CpuUsed += vm.Cpu; RamUsed += vm.Ram; }
        public void Deallocate(Resource vm) { CpuUsed -= vm.Cpu; RamUsed -= vm.Ram; }
        public double TotalUsage => ((double)CpuUsed / Cpu + (double)RamUsed / Ram) / 2;
    }
}
