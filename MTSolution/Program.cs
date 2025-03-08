using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        // Чтение данных из input.json
        string input = File.ReadAllText("input.json").Trim();
        var request = JsonConvert.DeserializeObject<InputData>(input);
        var response = new OutputData();
        var vmPlacement = new Dictionary<string, List<string>>();
        var migrations = new Dictionary<string, Migration>();

        var hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));

        // Заполняем хосты виртуальными машинами
        foreach (var vm in request.VirtualMachines)
        {
            bool placed = false;
            foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage))
            {
                if (host.Value.CanHost(vm.Value))
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

        // Проверяем, нужно ли добавлять новую ВМ
        bool canAddVm = hostUsage.Values.All(h => h.TotalUsage >= 0.75 && h.TotalUsage <= 0.81);
        if (canAddVm && request.Diff?.Add?.VirtualMachines?.Count > 0)
        {
            foreach (var vmKey in request.Diff.Add.VirtualMachines)
            {
                var newVm = new Resource(4, 8); // Новая ВМ с примерными параметрами
                bool placed = false;

                foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage))
                {
                    if (host.Value.CanHost(newVm) && host.Value.TotalUsage < 0.81)
                    {
                        host.Value.Allocate(newVm);
                        if (!vmPlacement.ContainsKey(host.Key))
                        {
                            vmPlacement[host.Key] = new List<string>();
                        }
                        vmPlacement[host.Key].Add(vmKey);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    response.FailedPlacements.Add(vmKey);
                }
            }
        }

        // Добавление информации о утилизации в output
        response.Allocations = vmPlacement;
        response.Migrations = migrations;
        response.HostUtilizations = hostUsage.ToDictionary(h => h.Key, h => new HostUtilization
        {
            UsagePercentage = h.Value.TotalUsage * 100
        });

        // Запись результата в output.json
        string output = JsonConvert.SerializeObject(response, Formatting.Indented);
        File.WriteAllText("output.json", output);
    }
}

class InputData
{
    [JsonProperty("hosts")] public Dictionary<string, Resource> Hosts { get; set; }
    [JsonProperty("virtual_machines")] public Dictionary<string, Resource> VirtualMachines { get; set; }
    [JsonProperty("diff")] public DiffChanges Diff { get; set; }
}

class DiffChanges
{
    [JsonProperty("add")] public DiffAdd Add { get; set; }
}

class DiffAdd
{
    [JsonProperty("virtual_machines")] public List<string> VirtualMachines { get; set; }
}

class OutputData
{
    [JsonProperty("allocations")] public Dictionary<string, List<string>> Allocations { get; set; } = new();
    [JsonProperty("allocation_failures")] public List<string> FailedPlacements { get; set; } = new();
    [JsonProperty("migrations")] public Dictionary<string, Migration> Migrations { get; set; } = new();
    [JsonProperty("host_utilizations")] public Dictionary<string, HostUtilization> HostUtilizations { get; set; } = new();
}

class HostUtilization
{
    [JsonProperty("usage_percentage")] public double UsagePercentage { get; set; }
}

class Migration
{
    [JsonProperty("vm")] public string Vm { get; set; }
    [JsonProperty("from")] public string From { get; set; }
    [JsonProperty("to")] public string To { get; set; }
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
    public void Deallocate(Resource vm)
    {
        CpuUsed -= vm.Cpu;
        RamUsed -= vm.Ram;
    }

    public double TotalUsage => ((double)CpuUsed / Cpu + (double)RamUsed / Ram) / 2;
}