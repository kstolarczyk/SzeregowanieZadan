namespace SzeregowanieZadan
{
    using System;
    using System.Collections.Generic;

    public class Graph
    {
        public Graph(List<Task> tasks)
        {
            Tasks = tasks;
            var tasksCount = tasks.Count;
            Pheromones = new double[tasksCount, tasksCount];
            InitPheromonesOnEdges(tasksCount);

            Edges = new List<List<Edge>>(tasksCount);
            InitEdges(tasks, tasksCount);
            BestResult = tasksCount * 2;
        }

        public List<Task> Tasks { get; }
        public int BestResult { get; set; }
        public int MaxTime { get; private set; }
        private void InitEdges(List<Task> tasks, int tasksCount)
        {
            MaxTime = 0;
            for (var i = 0; i < tasksCount; i++)
            {
                Edges.Add(new List<Edge>(tasksCount));
                var currentTask = tasks[i];
                for (var j = 0; j < tasksCount; j++)
                {
                    var targetTask = tasks[j];
                    var distance = Math.Max(0, targetTask.Start - currentTask.Start);
                    Edges[i].Add(new Edge(distance));
                }
                MaxTime = Math.Max(MaxTime, currentTask.Estimated);
            }
        }

        public List<List<Edge>> Edges { get; set; }

        private void InitPheromonesOnEdges(int count)
        {
            for (var i = 0; i < count; i++)
            {
                for (var j = i; j < count; j++)
                {
                    Pheromones[i, j] = Pheromones[j, i] = Config.INIT_PHEROMONES;
                }
            }
        }

        public double[,] Pheromones { get; set; }
    }
}