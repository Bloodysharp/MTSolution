
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Xml;

class Program
{
    static void Main()
    {
        string input = Console.In.ReadToEnd().Trim();
        var request = JsonConvert.DeserializeObject<InputData>(input);
        var response = new OutputData();
        var vmPlacement = new Dictionary<string, string>();
        var migrations = new List<Migration>();

        var hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));

        foreach (var vm in request.VirtualMachines)
        {
            bool placed = false;
            foreach (var host in hostUsage.OrderBy(h => h.Value.CpuUsage))
            {
                if (host.Value.CanHost(vm.Value))
                {
                    host.Value.Allocate(vm.Value);
                    vmPlacement[vm.Key] = host.Key;
                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                response.FailedPlacements.Add(vm.Key);
            }
        }

        // Миграция ВМ при перегрузке хоста
        foreach (var host in hostUsage)
        {
            if (host.Value.CpuUsage > 0.8)
            {
                var vmsToMigrate = vmPlacement.Where(v => v.Value == host.Key).ToList();
                foreach (var vm in vmsToMigrate)
                {
                    foreach (var targetHost in hostUsage.OrderBy(h => h.Value.CpuUsage))
                    {
                        if (targetHost.Key != host.Key && targetHost.Value.CanHost(request.VirtualMachines[vm.Key]))
                        {
                            host.Value.Deallocate(request.VirtualMachines[vm.Key]);
                            targetHost.Value.Allocate(request.VirtualMachines[vm.Key]);
                            vmPlacement[vm.Key] = targetHost.Key;
                            migrations.Add(new Migration { Vm = vm.Key, From = host.Key, To = targetHost.Key });
                            break;
                        }
                    }
                }
            }
        }

        response.Placements = vmPlacement;
        response.Migrations = migrations;
        Console.WriteLine(JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented }));
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
    [JsonProperty("placements")] public Dictionary<string, string> Placements { get; set; } = new();
    [JsonProperty("failed_placements")] public List<string> FailedPlacements { get; set; } = new();
    [JsonProperty("migrations")] public List<Migration> Migrations { get; set; } = new();
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
    public double CpuUsage => (double)CpuUsed / Cpu;
}
