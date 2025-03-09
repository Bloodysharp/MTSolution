using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        string input = File.ReadAllText("input.json").Trim();
        var request = JsonConvert.DeserializeObject<InputData>(input);
        var response = new OutputData();
        var vmPlacement = new Dictionary<string, List<string>>();
        var hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));

        // Сортируем ВМ по убыванию загрузки
        var sortedVms = request.VirtualMachines.OrderByDescending(vm => vm.Value.TotalUsage).ToList();

        foreach (var vm in sortedVms)
        {
            bool placed = false;
            foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage))
            {
                if (host.Value.CanHost(vm.Value) && (host.Value.TotalUsage + vm.Value.UsageDelta) <= 0.80)
                {
                    host.Value.Allocate(vm.Value);
                    if (!vmPlacement.ContainsKey(host.Key))
                    {
                        vmPlacement[host.Key] = new List<string>();
                    }
                    vmPlacement[host.Key].Add(vm.Key);
                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                response.FailedPlacements.Add(vm.Key);
            }
        }

        // Проверяем, что загрузка хостов в пределах 70-85%
        foreach (var host in hostUsage)
        {
            if (host.Value.TotalUsage < 0.75)
            {
                response.UnderutilizedHosts.Add(host.Key);
            }
        }

        response.Allocations = vmPlacement;
        response.HostUtilizations = hostUsage.ToDictionary(h => h.Key, h => new HostUtilization
        {
            UsagePercentage = h.Value.TotalUsage * 100
        });

        string output = JsonConvert.SerializeObject(response, Formatting.Indented);
        File.WriteAllText("output.json", output);
    }
}

class InputData
{
    [JsonProperty("hosts")] public Dictionary<string, Resource> Hosts { get; set; }
    [JsonProperty("virtual_machines")] public Dictionary<string, Resource> VirtualMachines { get; set; }
}

class OutputData
{
    [JsonProperty("allocations")] public Dictionary<string, List<string>> Allocations { get; set; } = new();
    [JsonProperty("allocation_failures")] public List<string> FailedPlacements { get; set; } = new();
    [JsonProperty("host_utilizations")] public Dictionary<string, HostUtilization> HostUtilizations { get; set; } = new();
    [JsonProperty("underutilized_hosts")] public List<string> UnderutilizedHosts { get; set; } = new();
}

class HostUtilization
{
    [JsonProperty("usage_percentage")] public double UsagePercentage { get; set; }
}

class Resource
{
    [JsonProperty("cpu")] public int Cpu { get; set; }
    [JsonProperty("ram")] public int Ram { get; set; }
    public int CpuUsed { get; private set; }
    public int RamUsed { get; private set; }

    public Resource(int cpu, int ram)
    {
        Cpu = cpu;
        Ram = ram;
        CpuUsed = 0;
        RamUsed = 0;
    }

    public bool CanHost(Resource vm) => CpuUsed + vm.Cpu <= Cpu && RamUsed + vm.Ram <= Ram;
    public void Allocate(Resource vm)
    {
        CpuUsed += vm.Cpu;
        RamUsed += vm.Ram;
    }
    public double TotalUsage => ((double)CpuUsed / Cpu + (double)RamUsed / Ram) / 2;
    public double UsageDelta => ((double)CpuUsed / Cpu + (double)RamUsed / Ram) / 2;
}