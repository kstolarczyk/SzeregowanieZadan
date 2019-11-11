using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SzeregowanieZadan
{
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    class FreeComparer : IComparer<Interval>
    {
        public int Compare(Interval x, Interval y)
        {
            return x.IsContainedIn(y);
        }
    }
    class Program
    {

        static void Main(string[] args)
        {
           
            //GeneratePro(500,4, "instance1pro.txt");
            var tasks = ParseTasks(File.ReadAllLines("instance1pro.txt"), out var maxTime);
            var machines = NaiveAlgorithm(tasks);
            CreateResultFile(machines, "naive.txt");
            machines = SortedAlgorithm(tasks);
            CreateResultFile(machines, "sorted.txt");
            machines = RandomAlgorithm(tasks);
            CreateResultFile(machines, "random.txt");
            Console.WriteLine("\n --- Algorytm Naiwny ---");
            Weryfikuj("instance1pro.txt", "naive.txt");
            Console.WriteLine("\n --- Algorytm naiwny z sortowaniem po czasie gotowości zadań ---");
            Weryfikuj("instance1pro.txt", "sorted.txt");
            Console.WriteLine("\n --- Algorytm naiwny z losowym posortowaniem");
            Weryfikuj("instance1pro.txt", "random.txt");

            var graph = new Graph(tasks);
            var ants = new Ant[Config.ANTS];
            var threads = new Thread[Config.ANTS];
            var ctk = new CancellationTokenSource(100);
            var timer = new Stopwatch();
            var whenStop = 10 * graph.Tasks.Count;
            for(var i = 0; i < Config.ANTS - 1; i++)
            {
                ants[i] = new Ant(graph, 4);
                threads[i] = new Thread(ants[i].Run);
            }
            ants[Config.ANTS - 1] = new SpecialAnt(graph, 4);
            threads[Config.ANTS - 1] = new Thread(ants[Config.ANTS - 1].Run);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            timer.Start();
            foreach (var thread in threads)
            {
                thread.Start(ctk.Token);
            }
            while(timer.ElapsedMilliseconds < whenStop)
            {
                Thread.Sleep(100);
            }
            ctk.Cancel();
            foreach(var thread in threads)
            {
                thread.Join();
            }
            timer.Stop();

            Console.WriteLine($"\nUpłynęło {timer.ElapsedMilliseconds} ms");
            ExportResult(graph, "ACO.txt");
            Console.WriteLine("\n Algorytm Mrówkowy (ACO)");
            Weryfikuj("instance1pro.txt", "ACO.txt");
            Console.ReadKey();
        }

        private static void ExportResult(Graph graph, string fileName)
        {
            var taskList = new List<Task>(graph.Tasks.Count);
            foreach (var taskId in graph.BestResult)
            {
                taskList.Add(graph.Tasks[taskId]);
            }
            var machines = NaiveAlgorithm(taskList);
            CreateResultFile(machines, fileName);
        }

        public static void CreateResultFile(IEnumerable<MachineTest> machines, string resultName)
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

        public static void GeneratePro(int tasksCount, int machinesCount, string fileName) {
            var endTime = RandomGen.Next(tasksCount, tasksCount*tasksCount);
            var machinesRands = new int[machinesCount];
            var machinesEndTime = new int[machinesCount];
            var sum = 0;
            var machines = InitializeMachines(Config.MACHINES_COUNT);
            for (var i = 0; i < machinesCount; i++) {
                sum += machinesRands[i] = RandomGen.Next(10) + 1;
            }                                                 
            var tasks = new List<Task>(tasksCount);
            var taskId = 0;
            for(var i = 0; i < machinesCount; i++)
            {
                machinesRands[i] *= tasksCount / sum;
                var currentTime = 0;
                var avgTime = endTime / machinesRands[i];
                while (currentTime < endTime)
                {
                    var taskDuration = RandomGen.Next(avgTime * 2) + 1;
                    var taskReady = RandomGen.Next(currentTime + 1);
                    var estimatedEnd = currentTime + taskDuration + RandomGen.Next(avgTime);
                    tasks.Add(new Task { Duration = taskDuration, Estimated = estimatedEnd, Start = taskReady });
                    machines[i].AddTask(new Task {Duration = taskDuration, Estimated = estimatedEnd, Start = currentTime, Id = taskId++});
                    currentTime += taskDuration;
                }

                machinesEndTime[i] = currentTime;
            }

            for (var i = tasks.Count; i < tasksCount; i++)
            {
                var j = i % machinesCount;
                var avgTime = endTime / machinesRands[j];
                var taskDuration = RandomGen.Next(avgTime * 2) + 1;
                var taskReady = RandomGen.Next(machinesEndTime[j] + 1);
                var estimatedEnd = machinesEndTime[j] + taskDuration + RandomGen.Next(avgTime);
                tasks.Add(new Task { Duration = taskDuration, Estimated = estimatedEnd, Start = taskReady });
                machines[j].AddTask(new Task {Duration = taskDuration, Estimated = estimatedEnd, Start = machinesEndTime[j], Id = taskId++});
                machinesEndTime[j] += taskDuration;
            }

            CreateResultFile(machines, "optimum.txt");
            var strBuilder = new StringBuilder($"{tasksCount}\n");
            var shuffled = tasks.OrderBy(t => Guid.NewGuid()).ToList();
            for (var i = 0; i < tasksCount; i++)
            {
                strBuilder.AppendLine($"{shuffled[i].Duration} {shuffled[i].Start} {shuffled[i].Estimated}");
            }
            File.WriteAllText(fileName, strBuilder.ToString());
        }

        public static IEnumerable<MachineTest> SortedAlgorithm(IEnumerable<Task> tasks)
        {
            var sorted = tasks.OrderBy(t => t.Duration);
            return NaiveAlgorithm(sorted);
        }

        public static IEnumerable<MachineTest> RandomAlgorithm(IEnumerable<Task> tasks)
        {
            var sorted = tasks.OrderBy(t => Guid.NewGuid());
            return NaiveAlgorithm(sorted);
        }

        public static IEnumerable<MachineTest> NaiveAlgorithm(IEnumerable<Task> tasks)
        {
            var totalDelay = 0;
            var machines = InitializeMachines(Config.MACHINES_COUNT);
            foreach (var task in tasks)
            {
                
                var added = false;
                var currentTask = task;
                var currentTime = currentTask.Start;
                var endTime = currentTask.Duration + currentTime - 1;


                for (var i = 0; i < machines.Count; i++)
                {
                    if (machines[i].AddTask(currentTask))
                    {
                        totalDelay += Math.Max(0, endTime - currentTask.Estimated);
                        added = true;
                        break;
                    }
                }

                if (added) continue;

                var machineIter = 0;
                currentTime = int.MaxValue;
                for (var i = 0; i < machines.Count; i++)
                {
                    var ff = machines[i].FirstFree(currentTask.Start, endTime);
                    if (ff < currentTime)
                    {
                        currentTime = ff;
                        machineIter = i;
                    }
                }

                endTime = currentTime + currentTask.Duration - 1;
                currentTask.Start = currentTime;
                machines[machineIter].AddTask(currentTask);
                totalDelay += Math.Max(0, endTime - currentTask.Estimated);

            }
            return machines;
        }
        public static List<MachineTest> InitializeMachines(int count)
        {
            var machines = new List<MachineTest>(count);
            for (var i = 0; i < count; i++)
            {
                machines.Add(new MachineTest());
            }
            return machines;
        }

        private static List<Task> ParseTasks(string[] lines, out int maxTime)
        {
            var tasks = new List<Task>(int.Parse(lines[0]));
            maxTime = 0;
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
                maxTime = Math.Max(maxTime, tasks[i - 1].Estimated);
            }
            return tasks;
        }

        public static void Weryfikuj(string instance, string result)
        {
            var lines = File.ReadAllLines(instance);
            var tasks = ParseTasks(lines, out var maxTime);
            var machines = InitializeMachines(Config.MACHINES_COUNT);
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

                    currentMachine.AddTask(time, time + currentTask.Duration - 1);
                    time += currentTask.Duration;
                    totalDelay += Math.Max(0, time - currentTask.Estimated + 1);
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

    public class MachineTest : Machine
    {
        public SortedDictionary<int, Task> Tasks { get; private set; }

        public MachineTest() : base()
        {
            Tasks = new SortedDictionary<int, Task>();
        }
        public bool AddTask(Task task)
        {
            var result = base.AddTask(task.Start, task.Start + task.Duration - 1);
            if (result)
            {
                Tasks.Add(task.Start, task);
            }

            return result;
        }
    }
}