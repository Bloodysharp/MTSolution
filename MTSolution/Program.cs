using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

class Program
{
    static Dictionary<string, Resource> hostUsage = new();
    static Dictionary<string, string> vmPlacement = new();
    static List<Migration> migrations = new();
    static HashSet<string> activeVMs = new();
    static Random random = new();

    static void Main()
    {
        while (true)
        {
            GenerateNewVM();
            ProcessNewRound();
            Thread.Sleep(1000);
        }
    }

    static void GenerateNewVM()
    {
        string filePath = "input.json";
        if (!File.Exists(filePath)) return;

        string json = File.ReadAllText(filePath);
        var data = JsonConvert.DeserializeObject<InputData>(json);
        if (data == null) return;

        string newVmId = $"vm{data.VirtualMachines.Count + 1}";
        var newVm = new Resource(random.Next(1, 4), random.Next(2, 6));

        data.VirtualMachines[newVmId] = newVm;
        File.WriteAllText(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    static void ProcessNewRound()
    {
        string filePath = "input.json";
        if (!File.Exists(filePath)) return;

        string input = File.ReadAllText(filePath);
        var request = JsonConvert.DeserializeObject<InputData>(input);
        var response = new OutputData();
        var newVMs = request.VirtualMachines.Keys.ToHashSet();

        if (hostUsage.Count == 0)
        {
            hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));
        }

        // Удаление ВМ, которых больше нет в входных данных
        foreach (var vm in activeVMs.Except(newVMs).ToList())
        {
            if (vmPlacement.TryGetValue(vm, out string host))
            {
                hostUsage[host].Deallocate(request.VirtualMachines[vm]);
                vmPlacement.Remove(vm);
            }
            activeVMs.Remove(vm);
        }

        // Размещение новых ВМ
        foreach (var vm in newVMs.Except(activeVMs))
        {
            if (PlaceVM(vm, request.VirtualMachines[vm]))
            {
                activeVMs.Add(vm);
            }
            else
            {
                MigrateAndPlace(vm, request.VirtualMachines[vm]);
            }
        }

        OptimizeLoad();

        // Заполнение информации о хостах
        foreach (var host in hostUsage)
        {
            double utilization = host.Value.TotalUsage;
            response.HostUtilizations[host.Key] = new HostUtilization
            {
                UsagePercentage = utilization * 100,
                Score = CalculateScore(utilization)
            };
            if (utilization < 75) response.UnderutilizedHosts.Add(host.Key);
        }

        response.Allocations = vmPlacement.GroupBy(kv => kv.Value)
                                          .ToDictionary(g => g.Key, g => g.Select(v => v.Key).ToList());
        response.Migrations = migrations;
        migrations.Clear();

        File.WriteAllText("output.json", JsonConvert.SerializeObject(response, Formatting.Indented));
    }

    static bool PlaceVM(string vmId, Resource vm)
    {
        foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage))
        {
            if (host.Value.CanHost(vm))
            {
                host.Value.Allocate(vm);
                vmPlacement[vmId] = host.Key;
                return true;
            }
        }
        return false;
    }

    static void MigrateAndPlace(string vmId, Resource vm)
    {
        foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage))
        {
            foreach (var existingVm in vmPlacement.Where(v => v.Value == host.Key).ToList())
            {
                string existingVmId = existingVm.Key;
                Resource existingVmResource = hostUsage[existingVm.Value];

                foreach (var targetHost in hostUsage.OrderBy(h => h.Value.TotalUsage))
                {
                    if (targetHost.Key != host.Key && targetHost.Value.CanHost(existingVmResource))
                    {
                        host.Value.Deallocate(existingVmResource);
                        targetHost.Value.Allocate(existingVmResource);
                        vmPlacement[existingVmId] = targetHost.Key;
                        migrations.Add(new Migration(existingVmId, host.Key, targetHost.Key));

                        if (host.Value.CanHost(vm))
                        {
                            host.Value.Allocate(vm);
                            vmPlacement[vmId] = host.Key;
                            return;
                        }
                    }
                }
            }
        }
    }

    static void OptimizeLoad()
    {
        foreach (var host in hostUsage.Where(h => h.Value.TotalUsage > 0.8).ToList())
        {
            foreach (var vm in vmPlacement.Where(v => v.Value == host.Key).ToList())
            {
                string vmId = vm.Key;
                Resource vmResource = hostUsage[vmId];

                foreach (var targetHost in hostUsage.OrderBy(h => h.Value.TotalUsage))
                {
                    if (targetHost.Key != host.Key && targetHost.Value.CanHost(vmResource))
                    {
                        host.Value.Deallocate(vmResource);
                        targetHost.Value.Allocate(vmResource);
                        vmPlacement[vmId] = targetHost.Key;
                        migrations.Add(new Migration(vmId, host.Key, targetHost.Key));
                        break;
                    }
                }
            }
        }
    }

    static double CalculateScore(double utilization)
    {
        return -0.67466 + (42.385 / (-2.5 * utilization + 5.96)) * Math.Exp(-2 * Math.Log(-2.5 * utilization + 2.96));
    }
}

class InputData
{
    public Dictionary<string, Resource> Hosts { get; set; } = new();
    public Dictionary<string, Resource> VirtualMachines { get; set; } = new();
}

class OutputData
{
    public Dictionary<string, List<string>> Allocations { get; set; } = new();
    public Dictionary<string, HostUtilization> HostUtilizations { get; set; } = new();
    public List<string> UnderutilizedHosts { get; set; } = new();
    public List<Migration> Migrations { get; set; } = new();
}

class HostUtilization
{
    public double UsagePercentage { get; set; }
    public double Score { get; set; }
}

class Migration
{
    public string Vm { get; }
    public string From { get; }
    public string To { get; }

    public Migration(string vm, string from, string to)
    {
        Vm = vm;
        From = from;
        To = to;
    }
}

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
