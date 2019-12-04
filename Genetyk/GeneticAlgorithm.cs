using System;

namespace Genetyk
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using SzeregowanieZadan;

    using Task = SzeregowanieZadan.Task;

    class GeneticAlgorithm
    {
        private List<Task> _tasks;

        private Individual2[] _population;

        private object _mutex = new object();

        private IComparer<Individual2> _comparer;

        private ThreadLocal<LinkedList<int>> _notVisited;

        private ThreadLocal<bool[]> _visited;

        public GeneticAlgorithm(IEnumerable<Task> tasks)
        {
            _tasks = tasks.ToList();
            _comparer = new IndividualComparer();
            _notVisited = new ThreadLocal<LinkedList<int>>(() => new LinkedList<int>());
            _visited = new ThreadLocal<bool[]>(() => new bool[_tasks.Count]);
            _population = InitialPopulation();
        }

        public void Run()
        {
            var totalIndividuals = Config.POPULATION_COUNT + Config.REJECTION_COUNT;
            for (var i = 0; i < Config.GENERATION_COUNT; i++)
            {
                Parallel.For(
                    Config.POPULATION_COUNT,
                    totalIndividuals,
                    new ParallelOptions() { MaxDegreeOfParallelism = Config.PROCESSORS_COUNT },
                    (j) =>
                        {
                            var r1 = RandomGen.Next(totalIndividuals);
                            var r2 = RandomGen.Next(totalIndividuals);
                            var r3 = RandomGen.Next(totalIndividuals);
                            var individuals = BestOf3(r1, r2, r3);
                            var child = CrossOver2(individuals[0], individuals[1]);
                            if (RandomGen.NextDouble() < Config.MUTATION_PROBABILITY)
                            {
                                // Mutation(ref child);
                            }

                            _population[j] = child;
                        });
                Selection();
            }

            Console.WriteLine($"Zakończono wyszukiwanie rozwiązania\nNajlepszy wynik: {_population[0].Score}");
        }

        private Individual2[] BestOf3(int r1, int r2, int r3)
        {
            var individuals = new Individual2[2];
            if (_population[r1].Score < _population[r2].Score)
            {
                individuals[0] = _population[r1];
                individuals[1] = _population[r2].Score < _population[r3].Score ? _population[r2] : _population[r3];
            }
            else
            {
                individuals[0] = _population[r2];
                individuals[1] = _population[r1].Score < _population[r3].Score ? _population[r1] : _population[r3];
            }

            return individuals;
        }

        public Individual CrossOver(Individual parent1, Individual parent2)
        {
            var child = new Individual(_tasks.Count);
            var chromosomeLen = child.Chromosome.Capacity - 1;
            var parents = new[] { parent1, parent2 };
            var visited = new bool[_tasks.Count];
            var iters = new[] { 0, 0 };

            for (var i = 0; i < chromosomeLen; i++)
            {
                var rnd = RandomGen.Next(2);
                var thisIter = rnd;
                var thatIter = Math.Abs(rnd - 1);
                var thisId = parents[thisIter].Chromosome[iters[thisIter]];
                var nextId = parents[thatIter].Chromosome[iters[thatIter]];

                if (thisId >= 0 && !visited[thisId])
                {
                    child.Chromosome.Add(thisId);
                    visited[thisId] = true;
                    if (nextId == -1)
                    {
                        child.Chromosome.Add(-1);
                        i++;
                        while (thisId != -1)
                        {
                            thisId = parents[thisIter].Chromosome[++iters[thisIter]];
                            if (iters[thisIter] >= chromosomeLen - 1) break;
                        }
                    }
                }
                else if (thisId == -1)
                {
                    if (nextId == -1)
                    {
                        child.Chromosome.Add(-1);
                    }
                    else
                    {
                        if (visited[nextId])
                        {
                            child.Chromosome.Add(-2);
                        }
                        else
                        {
                            child.Chromosome.Add(nextId);
                            visited[nextId] = true;
                        }

                        child.Chromosome.Add(-1);
                        i++;
                        while (nextId != -1)
                        {
                            nextId = parents[thatIter].Chromosome[++iters[thatIter]];
                            if (iters[thatIter] >= chromosomeLen - 1) break;
                        }
                    }
                }
                else
                {
                    if (nextId == -1)
                    {
                        child.Chromosome.Add(-2);
                        child.Chromosome.Add(-1);
                        i++;
                        while (thisId != -1)
                        {
                            thisId = parents[thisIter].Chromosome[++iters[thisIter]];
                            if (iters[thisIter] >= chromosomeLen - 1) break;
                        }
                    }
                    else
                    {
                        if (visited[nextId])
                        {
                            child.Chromosome.Add(-2);
                        }
                        else
                        {
                            child.Chromosome.Add(nextId);
                            visited[nextId] = true;
                        }
                    }
                }

                iters[thisIter]++;
                iters[thatIter]++;
                if (iters[thisIter] >= chromosomeLen || iters[thatIter] >= chromosomeLen) break;
            }

            for (var i = 0; i < _tasks.Count; i++)
            {
                if (!visited[i])
                {
                    _notVisited.Value.AddLast(i);
                }
            }

            for (var i = child.Chromosome.Count; i < chromosomeLen; i++)
            {
                child.Chromosome.Add(-2);
            }

            var score = 0;
            var currentTime = 0;
            for (var i = 0; i < chromosomeLen; i++)
            {
                if (child.Chromosome[i] == -2)
                {
                    // child.Chromosome[i] = _notVisited.Value.Last.Value;
                    var bestTask = _notVisited.Value.First.Value;
                    var bestValue = int.MaxValue;
                    foreach (var taskId in _notVisited.Value)
                    {
                        var currentTask = _tasks[taskId];
                        var differ = currentTime - (currentTask.Estimated - currentTask.Duration);
                        if (differ < bestValue)
                        {
                            bestValue = differ;
                            bestTask = taskId;
                        }
                    }
                    child.Chromosome[i] = bestTask;
                    _notVisited.Value.Remove(bestTask);
                    // _notVisited.Value.RemoveLast();
                }

                if (child.Chromosome[i] == -1)
                {
                    currentTime = 0;
                }
                else
                {
                    var currentTask = _tasks[child.Chromosome[i]];
                    currentTime = Math.Max(currentTime, currentTask.Start) + currentTask.Duration;
                    score += Math.Max(0, currentTime - currentTask.Estimated);
                }
            }

            child.Score = score;
            return child;
        }

        public Individual2 CrossOver2(Individual2 parent1, Individual2 parent2)
        {
            var tCount = _tasks.Count;
            var child = new Individual2(tCount);
            var visited = new bool[tCount+1];
            var fifo = new Queue<int>(4);
            var machines = new Queue<int>(4);
            var currentTimes = new int[Config.MACHINES_COUNT];

            var parents = new Individual2[] {parent1, parent2};
            var dodano = 0;
            var totalLate = 0;
            while (dodano < 4)
            {
                for (var i = 0; i < Config.MACHINES_COUNT; i++)
                {
                    var thisIter = RandomGen.Next(2);
                    var current = parents[thisIter].StartingTasks[i];
                    if (visited[current])
                    {
                        current = parents[-thisIter+1].StartingTasks[i];
                    }
                    if (visited[current]) continue;
                    child.StartingTasks.Add(current);
                    visited[current] = true;
                    fifo.Enqueue(current);
                    machines.Enqueue(i);
                    currentTimes[i] = Math.Max(currentTimes[i], _tasks[current].Start) + _tasks[current].Duration;
                    dodano++;
                    if (dodano >= 4) break;
                }
            }
            var currentIds = child.StartingTasks.ToArray();
            while (fifo.Count > 0)
            {
                var current = fifo.Dequeue();
                var thisIter = RandomGen.Next(2);
                var machineId = machines.Dequeue();
                var next = parents[thisIter].Edges[current];
                if (next == 0 || visited[next])
                {
                    next = parents[-thisIter + 1].Edges[current];
                }
                if (next == 0 || visited[next])
                {
                    continue;
                }
                child.Edges[current] = next;
                visited[next] = true;
                fifo.Enqueue(next);
                machines.Enqueue(machineId);
                currentIds[machineId] = next;
                var currentTask = _tasks[next-1];
                currentTimes[machineId] = Math.Max(currentTimes[machineId], currentTask.Start) + currentTask.Duration;
                totalLate += Math.Max(0, currentTimes[machineId] - currentTask.Estimated);
            }

            for (var i = 1; i <= tCount; i++)
            {
                if (visited[i]) continue;
                var currentTask = _tasks[i-1];
                var bestMachine = 0;
                var bestTime = int.MaxValue;
                for (var j = 0; j < currentIds.Length; j++)
                {
                    if (currentTimes[j] < bestTime)
                    {
                        bestTime = currentTimes[j];
                        bestMachine = j;
                    }
                }
                child.Edges[currentIds[bestMachine]] = currentTask.Id;
                currentTimes[bestMachine] = Math.Max(currentTimes[bestMachine], currentTask.Start) + currentTask.Duration;
                currentIds[bestMachine] = currentTask.Id;
                totalLate += Math.Max(0, currentTimes[bestMachine] - currentTask.Estimated);
            }
            child.Score = totalLate;
            return child;
        }


        public void Selection()
        {
            Array.Sort(_population, _comparer);
        }

        public void Mutation(ref Individual individual)
        {
            var count = individual.Chromosome.Count;
            for (var i = 0; i < Config.MUTATION_COUNT; i++)
            {
                var r1 = RandomGen.Next(count);
                while (individual.Chromosome[r1] == -1)
                {
                    r1 = RandomGen.Next(count);
                }

                int r2;
                while (true)
                {
                    r2 = RandomGen.Next(count);
                    if (individual.Chromosome[r2] == -1) continue;
                    var taskR1 = _tasks[individual.Chromosome[r1]];
                    var taskR2 = _tasks[individual.Chromosome[r2]];
                    if (r1 <= r2)
                    {
                        if (taskR1.Estimated < taskR2.Estimated) continue;
                    }
                    else
                    {
                        if (taskR2.Estimated < taskR1.Estimated) continue;
                    }

                    break;
                }

                var tmp = individual.Chromosome[r1];
                individual.Chromosome[r1] = individual.Chromosome[r2];
                individual.Chromosome[r2] = tmp;
            }

            ComputeScore(ref individual);
        }

        public void ComputeScore(ref Individual individual)
        {
            var currentTime = 0;
            var delay = 0;
            for (var i = 0; i < individual.Chromosome.Count; i++)
            {
                var iter = individual.Chromosome[i];
                if (iter == -1)
                {
                    currentTime = 0;
                    continue;
                }

                var currentTask = _tasks[iter];
                currentTime = Math.Max(currentTime, currentTask.Start) + currentTask.Duration;
                delay += Math.Max(0, currentTime - currentTask.Estimated);
            }

            individual.Score = delay;
        }

        public Individual2[] InitialPopulation()
        {
            var population = new Individual2[Config.POPULATION_COUNT + Config.REJECTION_COUNT];
            Parallel.For(
                0,
                Config.POPULATION_COUNT + Config.REJECTION_COUNT,
                new ParallelOptions() { MaxDegreeOfParallelism = Config.PROCESSORS_COUNT },
                i =>
                    {
                        var individual = new Individual2(_tasks.Count);
                        individual.GenerateChromosome(_tasks);
                        population[i] = individual;
                    });
            return population;
        }
    }

    public class TaskComparer : IComparer<Task>
    {
        public int Compare(Task x, Task y)
        {
            if (x == null || y == null) return 0;
            return (x.Estimated - x.Duration) > (y.Estimated - y.Duration) ? 1 : -1;
        }
    }
}