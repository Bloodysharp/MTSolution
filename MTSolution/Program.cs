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
        var vmPlacement = new Dictionary<string, List<string>>(); // Изменено для хранения списка ВМ на хостах
        var migrations = new Dictionary<string, Migration>(); // Изменено для отслеживания миграций

        var hostUsage = request.Hosts.ToDictionary(h => h.Key, h => new Resource(h.Value.Cpu, h.Value.Ram));

        // Проверка утилизации хостов перед добавлением новой ВМ
        bool canAddVm = hostUsage.All(h => h.Value.TotalUsage >= 0.75 && h.Value.TotalUsage <= 0.81);

        if (canAddVm && request.Diff?.Add?.VirtualMachines?.Count > 0)
        {
            // Добавление новых виртуальных машин
            foreach (var vmKey in request.Diff.Add.VirtualMachines)
            {
                var newVm = new Resource(4, 8); // Примерная ВМ, можно модифицировать
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

        // Первоначальное распределение ВМ по хостам
        foreach (var vm in request.VirtualMachines)
        {
            bool placed = false;
            foreach (var host in hostUsage.OrderBy(h => h.Value.TotalUsage)) // Используется общий расход ресурсов
            {
                if (host.Value.CanHost(vm.Value) && host.Value.TotalUsage < 0.81) // Проверка общей утилизации
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

        // Проверка на хосты с утилизацией выше 81% и перераспределение ВМ
        var overUtilizedHosts = hostUsage.Where(h => h.Value.TotalUsage > 0.81).ToList();
        foreach (var host in overUtilizedHosts)
        {
            var vmsToMigrate = vmPlacement.Where(v => v.Value.Contains(host.Key)).ToList();
            foreach (var vm in vmsToMigrate)
            {
                foreach (var targetHost in hostUsage.OrderBy(h => h.Value.TotalUsage))
                {
                    if (targetHost.Key != host.Key && targetHost.Value.CanHost(request.VirtualMachines[vm.Key]) && targetHost.Value.TotalUsage < 0.81)
                    {
                        host.Value.Deallocate(request.VirtualMachines[vm.Key]);
                        targetHost.Value.Allocate(request.VirtualMachines[vm.Key]);
                        vmPlacement[host.Key].Remove(vm.Key);
                        if (!vmPlacement.ContainsKey(targetHost.Key))
                        {
                            vmPlacement[targetHost.Key] = new List<string>();
                        }
                        vmPlacement[targetHost.Key].Add(vm.Key);
                        migrations[vm.Key] = new Migration { Vm = vm.Key, From = host.Key, To = targetHost.Key };
                        break;
                    }
                }
            }
        }

        // Добавление информации о утилизации в output
        response.Allocations = vmPlacement;
        response.Migrations = migrations;
        response.HostUtilizations = hostUsage.ToDictionary(h => h.Key, h => new HostUtilization
        {
            UsagePercentage = h.Value.TotalUsage * 100 // Используется общий процент
        });

        // Сериализация и запись в файл
        string output = JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Formatting.Indented });
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
    // Рассчитывается как сумма использования CPU и RAM относительно их общей вместимости
    public double TotalUsage => (double)(CpuUsed + RamUsed) / (Cpu + Ram);
}
