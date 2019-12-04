namespace Genetyk
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SzeregowanieZadan;

    public class Individual
    {

        public Individual(int tasksCount)
        {
            Chromosome = new List<int>(tasksCount + Config.MACHINES_COUNT);
        }

        public List<int> Chromosome { get; set; }
        public int Score { get; set; }

        public void GenerateChromosome(List<Task> tasks)
        {
            var tCount = tasks.Count;
            var chromosome = new List<int>(tCount + Config.MACHINES_COUNT);
            tasks = tasks.OrderBy(t => t.Estimated - t.Duration + RandomGen.Next(200)).ToList();
            var machines = Helper.NaiveAlgorithm(tasks, out var delay);
            foreach (var machine in machines)
            {
                foreach (var task in machine.Tasks)
                {
                    Chromosome.Add(task.Value.Id);
                }
                Chromosome.Add(-1);
            }
            Chromosome.RemoveAt(tCount + Config.MACHINES_COUNT - 1);
            Score = delay;
        }
    }

    public class IndividualComparer : IComparer<Individual2>
    {
        public int Compare(Individual2 x, Individual2 y)
        {
            if (x == null || y == null || x.Score == y.Score) return 0;
            return x.Score > y.Score ? 1 : -1;
        }
    }

    public class Individual2
    {
        public List<int> StartingTasks { get; set; }
        public int[] Edges { get; set; }
        public int Score { get; set; }

        public Individual2(int tasksCount)
        {
            StartingTasks = new List<int>(Config.MACHINES_COUNT);
            Edges = new int[tasksCount+1];
        }

        public void GenerateChromosome(List<Task> tasks)
        {
            var tCount = tasks.Count;
            tasks = tasks.OrderBy(t => t.Estimated - t.Duration + RandomGen.Next(200)).ToList();
            var machines = Helper.NaiveAlgorithm(tasks, out var delay).ToList();
            int prev, current = -1;
            for (var i = 0; i < Config.MACHINES_COUNT; i++)
            {
                foreach (var task in machines[i].Tasks)
                {
                    prev = current;
                    current = task.Value.Id;
                    if (prev != -1)
                    {
                        Edges[prev] = current;
                    }
                    else
                    {
                        StartingTasks.Add(current);
                    }
                }
                current = -1;
            }
            Score = delay;
        }
    }
}
