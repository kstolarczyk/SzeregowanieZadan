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

            var instFile = "test.txt";
            // Helper.GeneratePro(10,4, instFile);
            var tasks = Helper.ParseTasks(File.ReadAllLines(instFile));
            var machines = Helper.NaiveAlgorithm(tasks, out var totalDelay);
            Helper.CreateResultFile(machines, "naive.txt");
            machines = Helper.SortedAlgorithm(tasks, out _);
            Helper.CreateResultFile(machines, "sorted.txt");
            machines = Helper.RandomAlgorithm(tasks, out _);
            Helper.CreateResultFile(machines, "random.txt");
            Console.WriteLine("\n --- Algorytm Naiwny ---");
            Helper.Weryfikuj(instFile, "naive.txt");
            Console.WriteLine("\n --- Algorytm naiwny z sortowaniem po czasie gotowości zadań ---");
            Helper.Weryfikuj(instFile, "sorted.txt");
            Console.WriteLine("\n --- Algorytm naiwny z losowym posortowaniem");
            Helper.Weryfikuj(instFile, "random.txt");
            Helper.Weryfikuj(instFile, "optimum.txt");

            var graph = new Graph(tasks, totalDelay);
            var ants = new Ant[Config.ANTS];
            var threads = new Thread[Config.ANTS];
            var ctk = new CancellationTokenSource();
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
            Helper.ExportResult(graph, "ACO.txt");
            Console.WriteLine("\n Algorytm Mrówkowy (ACO)");
            Helper.Weryfikuj(instFile, "ACO.txt");
            Console.ReadKey();
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