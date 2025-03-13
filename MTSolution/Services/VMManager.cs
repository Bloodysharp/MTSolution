using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MTSolution.Models;
using Newtonsoft.Json;

class VMManager
{
    private Dictionary<string, Resource> hostUsage = new();
    private Dictionary<string, string> vmPlacement = new();
    private List<Migration> migrations = new();
    private HashSet<string> activeVMs = new();
    private List<string> unplacedVMs = new();
    private Random random = new();

    public void GenerateNewVM()
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

    public void ProcessNewRound()
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

        RemoveDeletedVMs(newVMs, request);
        unplacedVMs.Clear();

        foreach (var vm in newVMs.Except(activeVMs))
        {
            if (PlaceVM(vm, request.VirtualMachines[vm]))
            {
                activeVMs.Add(vm);
            }
            else if (!MigrateAndPlace(vm, request.VirtualMachines[vm]))
            {
                unplacedVMs.Add(vm);
            }
        }

        OptimizeLoad();
        WriteOutput(response);
    }

    private void RemoveDeletedVMs(HashSet<string> newVMs, InputData request)
    {
        foreach (var vm in activeVMs.Except(newVMs).ToList())
        {
            if (vmPlacement.TryGetValue(vm, out string host))
            {
                hostUsage[host].Deallocate(request.VirtualMachines[vm]);
                vmPlacement.Remove(vm);
            }
            activeVMs.Remove(vm);
        }
    }

    private bool PlaceVM(string vmId, Resource vm)
    {
        var suitableHosts = hostUsage
            .Where(h => h.Value.CanHost(vm))
            .OrderByDescending(h => h.Value.TotalUsage) // Выбираем хосты с наибольшей загрузкой
            .ToList();

        foreach (var host in suitableHosts)
        {
            double projectedLoad = (host.Value.TotalUsage * 100 + ((double)vm.Cpu / host.Value.Cpu * 100 + (double)vm.Ram / host.Value.Ram * 100) / 2);

            if (projectedLoad <= 80)  // Размещаем только если итоговая загрузка ≤ 80%
            {
                host.Value.Allocate(vm);
                vmPlacement[vmId] = host.Key;
                return true;
            }
        }
        return false;
    }


    private bool MigrateAndPlace(string vmId, Resource vm)
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
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private void OptimizeLoad()
    {
        var underutilizedHosts = hostUsage.Where(h => h.Value.TotalUsage < 0.75).ToList();

        foreach (var host in underutilizedHosts)
        {
            var vmsToMigrate = vmPlacement
                .Where(v => v.Value == host.Key)
                .OrderByDescending(v => hostUsage[v.Value].TotalUsage) // Начинаем с самых больших ВМ
                .ToList();

            foreach (var vm in vmsToMigrate)
            {
                string vmId = vm.Key;
                Resource vmResource = hostUsage[vm.Value];

                var targetHost = hostUsage
                    .Where(h => h.Key != host.Key && h.Value.CanHost(vmResource))
                    .OrderByDescending(h => h.Value.TotalUsage)
                    .FirstOrDefault();

                if (targetHost.Key != null)
                {
                    host.Value.Deallocate(vmResource);
                    targetHost.Value.Allocate(vmResource);
                    vmPlacement[vmId] = targetHost.Key;
                    migrations.Add(new Migration(vmId, host.Key, targetHost.Key));
                }
            }
        }
    }

    private void WriteOutput(OutputData response)
    {
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
        response.FailedPlacements = unplacedVMs;
        migrations.Clear();

        File.WriteAllText("output.json", JsonConvert.SerializeObject(response, Formatting.Indented));
    }

    private double CalculateScore(double utilization)
    {
        return -0.67466 + (42.385 / (-2.5 * utilization + 5.96)) * Math.Exp(-2 * Math.Log(-2.5 * utilization + 2.96));
    }
}