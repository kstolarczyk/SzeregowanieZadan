
#define INHERIT_MODE_RAND
#define SELECTION_BEST_OF

namespace SimpleGenetic
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using SzeregowanieZadan;

    using Task = SzeregowanieZadan.Task;

    class Program
    {
        public static readonly int POPULATION_COUNT = 300;

        public static readonly int GENERATION_COUNT = 500;

        public static readonly int REJECTION_COUNT = 100;

        public static readonly int MUTATION_COUNT = 3;

        public static readonly double MUTATION_PROBABILITY = 0.05;

        public static readonly int PROCESSORS_COUNT = 4;

        public static readonly int PARENTS_COUNT = 2;

        public static readonly int CHOOSE_FROM_COUNT = 3;

        private static IComparer<SimpleTask> _tasksComparer = new SimpleTaskComparer();

        private static IComparer<Individual> _individualComparer = new IndividualComparer();

        private static ThreadLocal<Machine[]> _machines = new ThreadLocal<Machine[]>(() => InitializeMachines(4));

        private static IEnumerable<SimpleTask> currentTasks;

        private static int bestValue = int.MaxValue;

        private static object mutex = new object();
  
        static void Main(string[] args)
        {
            var instFile = "instance2pro.txt";

            // Helper.GeneratePro(500, 4, instFile);
            var tasks =
                Helper.ParseTasks(File.ReadAllLines(instFile)).Select(
                    t => new SimpleTask() { Duration = t.Duration, Estimated = t.Estimated, Id = t.Id, Start = t.Start })
                    .ToList();
            var threadSafeTasks = new ThreadLocal<List<SimpleTask>>(() => new List<SimpleTask>(tasks),false);
            Helper.Weryfikuj(instFile, "test_optimal.txt");

            Helper.NaiveAlgorithm(tasks, out var naiveResult);
            Helper.NaiveAlgorithm(tasks.OrderBy(t => t.Start), out var sortedStart);
            Helper.NaiveAlgorithm(tasks.OrderBy(t => t.Estimated), out var sortedEnd);
            Helper.NaiveAlgorithm(tasks.OrderBy(t => t.Duration), out var sortedDuration);
            Helper.SortedAlgorithm(tasks, out var sortedCritical);
            Console.WriteLine($"Naive algorithm result: {naiveResult}");
            Console.WriteLine($"Sorted critical algorithm result: {sortedCritical}");
            Console.WriteLine($"Sorted start algorithm result: {sortedStart}");
            Console.WriteLine($"Sorted end algorithm result: {sortedEnd}");
            Console.WriteLine($"Sorted duration algorithm result: {sortedDuration}");
            Console.WriteLine($"Genetic algorithm started...");
            var watch = new Stopwatch();
            watch.Start();
            var population = InitializePopulation(threadSafeTasks, POPULATION_COUNT + REJECTION_COUNT);
            var totalCount = POPULATION_COUNT + REJECTION_COUNT;
            var ctkSource = new CancellationTokenSource();
            var optimals = 0;
            try
            {
                for (var i = 0; i < GENERATION_COUNT; i++)
                {
                    Selection(population);

                    Parallel.For(
                        POPULATION_COUNT,
                        totalCount,
                        new ParallelOptions()
                            {
                                MaxDegreeOfParallelism = PROCESSORS_COUNT,
                                CancellationToken = ctkSource.Token
                            },
                        j =>
                            {
                                var parents = ChooseParents(population);
                                var child = CrossOver(parents);
                                if (RandomGen.NextDouble() < MUTATION_PROBABILITY)
                                {
                                    Mutate(ref child);
                                }

                                child.Score = ComputeScore(child.Chromosome, threadSafeTasks.Value);
                                population[j] = child;
                                if (child.Score == 0) optimals++;
                            });
                    
                    if (optimals >= 5) break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                watch.Stop();
                ctkSource.Dispose();
            }
            Selection(population);
            var resultFile = $"result_of_{instFile}";
            while(optimals >= 0)
            {
                var result = ExportResult(population[optimals--].Chromosome, tasks, resultFile);
                if (result == 0) break;
            }

            Console.WriteLine($"Genetic best result: {population[0].Score} computed in {watch.ElapsedMilliseconds} ms");
            Helper.Weryfikuj(instFile, resultFile);
            Console.ReadKey();
        }

              private static Machine[] InitializeMachines(int machinesCount)
        {
            var machines = new Machine[machinesCount];
            for (var i = 0; i < machinesCount; i++)
            {
                machines[i] = new Machine();
            }
            return machines;
        }

        private static int NaiveAlgorithm(IEnumerable<SimpleTask> tasks)
        {
            var totalDelay = 0;
            var machines = _machines.Value;
            foreach (var task in tasks)
            {
                
                var added = false;
                var currentTask = task;
                var currentTime = currentTask.Start;
                var endTime = currentTask.Duration + currentTime - 1;

                for (var i = 0; i < machines.Length; i++)
                {
                    if (machines[i].AddTask(currentTime, endTime))
                    {
                        totalDelay += Math.Max(0, endTime - currentTask.Estimated+1);
                        added = true;
                        break;
                    }
                }
                if(added) continue;

                var machineIter = 0;
                currentTime = int.MaxValue;
                for (var i = 0; i < machines.Length; i++)
                {
                    var ff = machines[i].FirstFree(currentTask.Start, endTime);
                    if (ff < currentTime)
                    {
                        currentTime = ff;
                        machineIter = i;
                    }
                }

                endTime = currentTime + currentTask.Duration - 1;
                machines[machineIter].AddTask(currentTime, endTime);
                totalDelay += Math.Max(0, endTime - currentTask.Estimated+1);

            }
            ClearMachines(machines);
            return totalDelay;
        }

        private static void ClearMachines(IEnumerable<Machine> machines)
        {
            foreach (var machine in machines)
            {
                machine.ClearFree();
            }
        }


        private static int ExportResult(List<int> chromosome, List<SimpleTask> tasks, string fileName)
        {
            for (var i = 0; i < tasks.Count; i++)
            {
                var id = tasks[i].Id;
                tasks[i].OrderNumber = chromosome[id - 1];
            }

            var machines = Helper.NaiveAlgorithm(tasks.OrderBy(t => t.OrderNumber), out var result);
            var strBuilder = new StringBuilder($"{result}\n");
            foreach (var machineTest in machines)
            {
                strBuilder.AppendLine(string.Join(" ", machineTest.Tasks.Select(t => t.Value.Id)));
            }
            File.WriteAllText(fileName, strBuilder.ToString());
            return result;
        }

        private static void Mutate(ref Individual child)
        {
            var count = child.Chromosome.Count;
            var mutationCount = RandomGen.Next(1, MUTATION_COUNT + 1);
            for (var i = 0; i < mutationCount; i++)
            {
                var r1 = RandomGen.Next(count);
                var r2 = RandomGen.Next(count);
                var tmp = child.Chromosome[r1];
                child.Chromosome[r1] = child.Chromosome[r2];
                child.Chromosome[r2] = tmp;
            }
        }

        private static Individual CrossOver(Individual[] parents)
        {
            var count = parents[0].Chromosome.Count;
            var child = new Individual(count);
            for (var i = 0; i < count; i++)
            {
                child.Chromosome.Add(InheritValue(parents, i));
            }

            return child;
        }

        private static int InheritValue(Individual[] parents, int index)
        {
#if INHERIT_MODE_AVG
            var avg = 0;
            for (var i = 0; i < parents.Length; i++)
            {
                avg += parents[i].Chromosome[index];
            }

            return avg / parents.Length;
#elif INHERIT_MODE_RAND
            var r = RandomGen.Next(parents.Length);
            return parents[r].Chromosome[index];
#endif
        }

        private static Individual[] ChooseParents(Individual[] population)
        {
            var randoms = new Individual[CHOOSE_FROM_COUNT];
            for (var i = 0; i < CHOOSE_FROM_COUNT; i++)
            {
                randoms[i] = population[RandomGen.Next(POPULATION_COUNT + REJECTION_COUNT)];
            }

            Array.Sort(randoms, _individualComparer);
            return randoms.Take(PARENTS_COUNT).ToArray();
        }

        private static void Selection(Individual[] population)
        {
            Array.Sort(population, _individualComparer);
        }

        private static Individual[] InitializePopulation(ThreadLocal<List<SimpleTask>> threadLocalTasks, int count)
        {
            var population = new Individual[count];
            var firstTasks = threadLocalTasks.Value;
            var longestDuration = firstTasks.Max(t => t.Duration);
            var tCount = firstTasks.Count;
            Parallel.For(0, count, new ParallelOptions() {MaxDegreeOfParallelism = PROCESSORS_COUNT},
                i =>
                    {
                        var tasks = threadLocalTasks.Value;
                        var randDeviation = RandomGen.Next(longestDuration);
                        population[i] = new Individual(tCount);
                        for (var j = 0; j < tCount; j++)
                        {
                            var avg = tasks[j].Estimated - tasks[j].Duration;
                            var min = Math.Max(avg - randDeviation, 0);
                            var max = avg + randDeviation;
                            population[i].Chromosome.Add(RandomGen.Next(min, max+1));
                        }

                        population[i].Score = ComputeScore(population[i].Chromosome,tasks);
                    });

            return population;
        }

        private static int ComputeScore(List<int> chromosome, List<SimpleTask> tasks)
        {
            for (var i = 0; i < tasks.Count; i++)
            {
                var id = tasks[i].Id;
                tasks[i].OrderNumber = chromosome[id - 1];
            }

            return NaiveAlgorithm(tasks.OrderBy(t => t.OrderNumber));
        }
    }

    class SimpleTask : Task
    {
        public int OrderNumber { get; set; }
    }

    class Individual
    {
        public List<int> Chromosome { get; set; }

        public int Score { get; set; }

        public Individual(int tCount)
        {
            Chromosome = new List<int>(tCount);
        }
    }

    class SimpleTaskComparer : IComparer<SimpleTask>
    {
        public int Compare(SimpleTask x, SimpleTask y)
        {
            if (x == null || y == null || x.Start == y.Start) return 0;
            return x.OrderNumber > y.OrderNumber ? 1 : -1;
        }
    }

    class IndividualComparer : IComparer<Individual>
    {
        public int Compare(Individual x, Individual y)
        {
            if (x == null || y == null || x.Score == y.Score) return 0;
            return x.Score > y.Score ? 1 : -1;
        }
    }
}