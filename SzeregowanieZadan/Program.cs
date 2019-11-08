using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SzeregowanieZadan
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {

        static void Main(string[] args)
        {
            // Generate(500, "instance1.txt");
            var tasks = ParseTasks(File.ReadAllLines("instance1.txt"));
            var machines = NaiveAlgorithm(tasks);
            CreateResultFile(machines, "naive.txt");
            machines = SortedAlgorithm(tasks);
            CreateResultFile(machines, "sorted.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random1.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random2.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random3.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random4.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random5.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random6.txt");
            Weryfikuj("instance1.txt", "naive.txt");
            Weryfikuj("instance1.txt", "sorted.txt");
            Weryfikuj("instance1.txt", "random1.txt");
            Weryfikuj("instance1.txt", "random2.txt");
            Weryfikuj("instance1.txt", "random3.txt");
            Weryfikuj("instance1.txt", "random4.txt");
            Weryfikuj("instance1.txt", "random5.txt");
            Weryfikuj("instance1.txt", "random6.txt");
            var graph = new Graph(tasks);
            var ants = new Ant[Config.ANTS];
            var threads = new Thread[Config.ANTS];
            for(var i = 0; i < Config.ANTS - 1; i++)
            {
                ants[i] = new Ant(graph, 4);
                threads[i] = new Thread(ants[i].Run);
            }
            ants[Config.ANTS - 1] = new SpecialAnt(graph, 4);
            threads[Config.ANTS - 1] = new Thread(ants[Config.ANTS - 1].Run);
            foreach (var thread in threads)
            {
                thread.Start();
            }
            // threads[0].Start();
            Console.ReadKey();
        }

        private static void CreateResultFile(IEnumerable<Machine> machines, string resultName)
        {
            var totalDelay = 0;
            foreach (var machine in machines)
            {
                foreach (var machineTask in machine.Tasks)
                {
                    var endTime = machineTask.Key + machineTask.Value.Duration;
                    totalDelay += Math.Max(0, endTime - machineTask.Value.Estimated);
                }
            }
            var strBuilder = new StringBuilder($"{totalDelay}\n");
            foreach (var machine in machines)
            {
                strBuilder.AppendLine(string.Join(" ", machine.Tasks.Select(t => $"{t.Value.Id}")));
            }
            File.WriteAllText(resultName, strBuilder.ToString());
        }

        public static void Generate(int n, string fileName)
        {
            var rnd = new Random();
            var tasks = new List<Task>(n);
            var sum = 0;
            var maxDuration = Config.MAX_TIME * 4 / n;
            for(var i = 0; i < n; i++)
            {
                var duration = rnd.Next(maxDuration * 2) + 1;
                tasks.Add(new Task { Duration = duration });
                sum += duration;
            }
            var avgTaskTime = sum / n;
            var avgMachineTime = sum / Config.MACHINES_COUNT;
            var strBuilder = new StringBuilder().AppendLine(n.ToString());
            for(var i = 0; i < n; i++)
            {
                var startTime = rnd.Next(avgMachineTime);
                var d = (startTime + tasks[i].Duration) * (100 + Config.MAX_DUE)/100.0;
                tasks[i].Start = startTime;
                tasks[i].Estimated = (int)d;
                strBuilder.AppendLine($"{tasks[i].Duration} {tasks[i].Start} {tasks[i].Estimated}");
            }
            File.WriteAllText(fileName, strBuilder.ToString());
        }

        public static IEnumerable<Machine> SortedAlgorithm(IEnumerable<Task> tasks)
        {
            var sorted = tasks.OrderBy(t => t.Start);
            return NaiveAlgorithm(sorted);
        }

        public static IEnumerable<Machine> RandomAlgorithm(IEnumerable<Task> tasks)
        {
            var sorted = tasks.OrderBy(t => Guid.NewGuid());
            return NaiveAlgorithm(sorted);
        }

        public static IEnumerable<Machine> NaiveAlgorithm(IEnumerable<Task> tasks)
        {
            var machines = InitializeMachines(Config.MAX_TIME*2);
            foreach (var task in tasks)
            {
                var added = false;
                for(var i = 0; i < machines.Count; i++)
                {
                    if (machines[i].AddTask(task)) { added = true; break; }
                }
                if(!added)
                {
                    var bestIndex = 0;
                    var bestResult = int.MaxValue;
                    for(var i = 0; i < machines.Count; i++)
                    {
                        var current = machines[i].FirstFree(task);
                        if(current < bestResult)
                        {
                            bestResult = current;
                            bestIndex = i;
                        }
                    }
                    machines[bestIndex].AddTask(task, bestResult);
                }
            }
            return machines;
        }

        private static List<Machine> InitializeMachines(int max)
        {
            var machines = new List<Machine>(Config.MACHINES_COUNT);
            for (var i = 0; i < Config.MACHINES_COUNT; i++)
            {
                machines.Add(new Machine(max));
            }
            return machines;
        }

        private static List<Task> ParseTasks(string[] lines)
        {
            var tasks = new List<Task>(int.Parse(lines[0]));
            for (var i = 1; i < lines.Length; i++)
            {
                var split = lines[i].Split(' ');
                tasks.Add(
                    new Task
                        {
                            Duration = int.Parse(split[0]),
                            Start = int.Parse(split[1]),
                            Estimated = int.Parse(split[2]),
                            Id = i-1
                        });
            }
            return tasks;
        }

        public static void Weryfikuj(string instance, string result)
        {
            var lines = File.ReadAllLines(instance);
            var tasks = ParseTasks(lines);
            var machines = InitializeMachines(Config.MAX_TIME*2);
            lines = File.ReadAllLines(result);
            var totalDelay = 0;
            for (var i = 1; i < lines.Length; i++)
            {
                var taskIds = lines[i].Split(' ');
                var time = 0;
                foreach (var taskId in taskIds)
                {
                    var id = int.Parse(taskId);
                    var currentTask = tasks[id];
                    var currentMachine = machines[i-1];

                    currentTask.Start = Math.Max(currentTask.Start, time);
                    time = currentTask.Start;

                    currentMachine.AddTask(currentTask, time);
                    time += currentTask.Duration;
                    totalDelay += Math.Max(0, time - currentTask.Estimated);
                }
            }
            Console.WriteLine($"Całkowity czas opóźnienia: {totalDelay}");
            Console.WriteLine($"W pliku wynikowym obliczony całkowity czas opóźnienia: {lines[0]}");
        }
    }

    public class Task
    {
        public int Id { get; set; }
        public int Start { get; set; }
        public int Duration { get; set; }
        public int Estimated { get; set; }
    }

    class Machine
    {
        private bool[] locked;
        public SortedDictionary<int, Task> Tasks { get; private set; }
        public Machine(int max)
        {
            locked = new bool[max];
            Tasks = new SortedDictionary<int, Task>();
        }

        private bool CheckFree(int start, int duration)
        {
            for(var i = start; i < start + duration; i++)
            {
                if (locked[i])
                    return false;
            }
            return true;
        }

        public bool AddTask(Task task)
        {
            if (!CheckFree(task.Start, task.Duration)) return false;
            for(var i = task.Start; i < task.Start + task.Duration; i++)
            {
                locked[i] = true;
            }
            Tasks.Add(task.Start, task);
            return true;
        }

        public void AddTask(Task task, int start) {
            for(var i = start; i < start + task.Duration; i++)
            {
                locked[i] = true;
            }
            Tasks.Add(start, task);
        }

        public int FirstFree(Task task)
        {
            var start = task.Start;
            while(!CheckFree(start, task.Duration)) { start++; }
            return start;
        }
    }
}