using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

class Program
{
    static Dictionary<string, Host> hosts = new();
    static Dictionary<string, VirtualMachine> virtualMachines = new();

    static void Main()
    {
        try
        {
            // Читаем JSON из файла
            string jsonInput = File.ReadAllText("input.json");
            var inputRounds = JsonSerializer.Deserialize<List<InputData>>(jsonInput);

            if (inputRounds == null)
            {
                Console.WriteLine("Ошибка: Невозможно загрузить входные данные.");
                return;
            }

            foreach (var roundData in inputRounds)
            {
                // Инициализируем хосты только на первом раунде
                if (hosts.Count == 0)
                    hosts = roundData.hosts;

                // Обновляем список ВМ согласно diff
                UpdateVirtualMachines(roundData.virtual_machines, roundData.diff);
                var allocationResult = AllocateVMs();

                // рез в stdout
                string outputJson = JsonSerializer.Serialize(allocationResult, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(outputJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки JSON: {ex.Message}");
        }
    }

    static void UpdateVirtualMachines(Dictionary<string, VirtualMachine> newVMs, Diff diff)
    {
        // Удаляем ВМ, если они отсутствуют в текущем раунде
        foreach (var vm in virtualMachines.Keys.ToList())
        {
            if (!newVMs.ContainsKey(vm))
                virtualMachines.Remove(vm);
        }

       
        if (diff?.add?.virtual_machines != null)
        {
            foreach (var vmId in diff.add.virtual_machines)
            {
                if (newVMs.ContainsKey(vmId))
                    virtualMachines[vmId] = newVMs[vmId]; // Добавляем новые ВМ
            }
        }

       
        if (diff?.remove?.virtual_machines != null) 
        {
            foreach (var vmId in diff.remove.virtual_machines)
            {
                virtualMachines.Remove(vmId); // Удаляем ВМ, если они указаны в diff.remove
            }
        }
    }

    static OutputData AllocateVMs()
    {
        Dictionary<string, List<string>> allocations = new();
        List<string> allocationFailures = new();
        Dictionary<string, Migration> migrations = new();
        Dictionary<string, Host> remainingResources = hosts.ToDictionary(h => h.Key, h => new Host(h.Value));
        foreach (var vm in virtualMachines)
        {
            string bestHost = FindBestHost(vm.Value, remainingResources);
            if (bestHost != null)
            {
                if (!allocations.ContainsKey(bestHost))
                    allocations[bestHost] = new List<string>();

                allocations[bestHost].Add(vm.Key);
                remainingResources[bestHost].cpu -= vm.Value.cpu;
                remainingResources[bestHost].ram -= vm.Value.ram;
            }
            else
            {
                allocationFailures.Add(vm.Key);
            }
        }
        return new OutputData
        {
            allocations = allocations,
            allocation_failures = allocationFailures,
            migrations = migrations
        };
    }
    static string FindBestHost(VirtualMachine vm, Dictionary<string, Host> resources)
    {
        string bestHost = null;
        double bestUtilization = double.MaxValue;

        foreach (var host in resources)
        {
           
            if (host.Value.cpu < vm.cpu || host.Value.ram < vm.ram) // Проверка, что хост может разместить ВМ по CPU и RAM
                continue;

            //утилизация хоста
            double cpuUsage = (host.Value.cpu - vm.cpu) / (double)hosts[host.Key].cpu;
            double ramUsage = (host.Value.ram - vm.ram) / (double)hosts[host.Key].ram;
            double avgUtilization = (cpuUsage + ramUsage) / 2;

            // Утилизация хоста должна быть в пределах от 78% до 82%, в идеале 79.сотые до 80.сотые
            if (avgUtilization >= 0.78 && avgUtilization <= 0.82)
            {
                if (avgUtilization < bestUtilization)
                {
                    bestUtilization = avgUtilization;
                    bestHost = host.Key;
                }
            }
        }

        return bestHost;
    }
}
class Host
{
    public int cpu { get; set; }
    public int ram { get; set; }

    public Host() { }

    public Host(Host other)
    {
        cpu = other.cpu;
        ram = other.ram;
    }
}

class VirtualMachine
{
    public int cpu { get; set; }
    public int ram { get; set; }
}

class InputData
{
    public Dictionary<string, Host> hosts { get; set; } = new();
    public Dictionary<string, VirtualMachine> virtual_machines { get; set; } = new();
    public Diff diff { get; set; } = new();
}

class Diff
{
    public Add add { get; set; } = new();
    public Remove remove { get; set; } = new();
}

class Add
{
    public string[] virtual_machines { get; set; } = Array.Empty<string>();
}

class Remove
{
    public string[] virtual_machines { get; set; } = Array.Empty<string>();
}

class OutputData
{
    public Dictionary<string, List<string>> allocations { get; set; } = new();
    public List<string> allocation_failures { get; set; } = new();
    public Dictionary<string, Migration> migrations { get; set; } = new();
}

class Migration
{
    public string from { get; set; }
    public string to { get; set; }
}
