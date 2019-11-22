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

    public class IndividualComparer : IComparer<Individual>
    {
        public int Compare(Individual x, Individual y)
        {
            if (x == null || y == null || x.Score == y.Score) return 0;
            return x.Score > y.Score ? 1 : -1;
        }
    }
}
