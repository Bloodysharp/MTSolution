﻿using Newtonsoft.Json;
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

    static void Main()
    {
        while (true)
        {
            ProcessNewRound();
            Thread.Sleep(1000); // Ждём 1 секунду перед следующим раундом
        }
    }

    static void ProcessNewRound()
    {
        if (!File.Exists("input.json")) return;
        string input = File.ReadAllText("input.json").Trim();
        var request = JsonConvert.DeserializeObject<InputData>(input);
        var response = new OutputData();
        var newVMs = request.VirtualMachines.Keys.ToHashSet();

        // Инициализация хостов
        if (hostUsage.Count == 0)
        {
            hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));
        }

        // Определяем удалённые ВМ
        var removedVMs = activeVMs.Except(newVMs).ToList();
        foreach (var vm in removedVMs)
        {
            if (vmPlacement.ContainsKey(vm))
            {
                string host = vmPlacement[vm];
                hostUsage[host].Deallocate(request.VirtualMachines[vm]);
                vmPlacement.Remove(vm);
            }
            activeVMs.Remove(vm);
        }

        // Определяем новые ВМ
        var addedVMs = newVMs.Except(activeVMs).ToList();
        foreach (var vm in addedVMs)
        {
            if (!PlaceVM(vm, request.VirtualMachines[vm]))
            {
                response.FailedPlacements.Add(vm);
            }
            else
            {
                activeVMs.Add(vm);
            }
        }

        // Оптимизируем загрузку
        OptimizeLoad();

        // Расчёт утилизации
        foreach (var host in hostUsage)
        {
            double utilization = host.Value.TotalUsage;
            response.HostUtilizations[host.Key] = new HostUtilization
            {
                UsagePercentage = utilization * 100,
                Score = CalculateScore(utilization)
            };
            if (utilization < 0.75) response.UnderutilizedHosts.Add(host.Key);
        }

        response.Allocations = vmPlacement.GroupBy(kv => kv.Value)
                                          .ToDictionary(g => g.Key, g => g.Select(v => v.Key).ToList());
        response.Migrations = migrations;
        migrations.Clear();

        string output = JsonConvert.SerializeObject(response, Formatting.Indented);
        File.WriteAllText("output.json", output);
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

    static void OptimizeLoad()
    {
        foreach (var host in hostUsage.Where(h => h.Value.TotalUsage > 0.8).ToList())
        {
            var vmsToMigrate = vmPlacement.Where(v => v.Value == host.Key).ToList();
            foreach (var vm in vmsToMigrate)
            {
                foreach (var targetHost in hostUsage.OrderBy(h => h.Value.TotalUsage))
                {
                    if (targetHost.Key != host.Key && targetHost.Value.CanHost(hostUsage[vm.Key]))
                    {
                        host.Value.Deallocate(hostUsage[vm.Key]);
                        targetHost.Value.Allocate(hostUsage[vm.Key]);
                        vmPlacement[vm.Key] = targetHost.Key;
                        migrations.Add(new Migration { Vm = vm.Key, From = host.Key, To = targetHost.Key });
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
    [JsonProperty("hosts")] public Dictionary<string, Resource> Hosts { get; set; }
    [JsonProperty("virtual_machines")] public Dictionary<string, Resource> VirtualMachines { get; set; }
}

class OutputData
{
    [JsonProperty("allocations")] public Dictionary<string, List<string>> Allocations { get; set; } = new();
    [JsonProperty("allocation_failures")] public List<string> FailedPlacements { get; set; } = new();
    [JsonProperty("host_utilizations")] public Dictionary<string, HostUtilization> HostUtilizations { get; set; } = new();
    [JsonProperty("underutilized_hosts")] public List<string> UnderutilizedHosts { get; set; } = new();
    [JsonProperty("migrations")] public List<Migration> Migrations { get; set; } = new();
}

class HostUtilization
{
    [JsonProperty("usage_percentage")] public double UsagePercentage { get; set; }
    [JsonProperty("score")] public double Score { get; set; }
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
