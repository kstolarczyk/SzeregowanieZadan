using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SzeregowanieZadanTests
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Runtime.InteropServices;

    using SzeregowanieZadan;

    using Xunit;

    using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

    public class AntTests
    {
        private List<Task> _tasks;

        private Graph _graph;

        private Ant _ant;

        private SpecialAnt _specialAnt;

        private int[] _result;

        public AntTests()
        {
            _tasks = CreateTasks();
            _result = new int[] { 1, 5, 6, 3, 0, 4, 8, 7, 9, 2 };
            _graph = new Graph(_tasks);
            _ant = new Ant(_graph, 4);
            _specialAnt = new SpecialAnt(_graph, 4);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        public void ShouldUpdateBestResult(int result)
        {
            var currentResult = _graph.BestResultValue;

            _ant.UpdateBestResult(result);

            if (result < currentResult)
            {
                Assert.AreEqual(result, _graph.BestResultValue);
            }
            else
            {
                Assert.AreEqual(currentResult, _graph.BestResultValue);
            }
        }

        [Theory]
        [InlineData(new int[] {0,1,2,3,4,5,6,7,8,9}, 100000)]
        [InlineData(new int[] {2,5,7,3,4,1,9,8,6,0}, 50000)]
        [InlineData(new int[] {3,7,1,4,0,6,9,8,2,5}, 10000)]
        [InlineData(new int[] {5,6,1,0,9,3,2,8,4,7}, 1000)]
        [InlineData(new int[] {9,1,2,5,7,8,3,4,0,6}, 1000000)]
        [InlineData(new int[] {4,0,2,6,7,1,8,9,5,3}, 500000)]
        [InlineData(new int[] {2,7,6,3,1,0,4,9,5,8}, 100)]
        public void ShouldApplyPheromones(int[] result, int resultValue)
        {
            var currentValues = new double[result.Length, result.Length];
            for (var i = 0; i < result.Length - 1; i++)
            {
                var from = result[i];
                var to = result[i+1];
                currentValues[from, to] = _graph.Pheromones[from, to];
            }

            _ant.ApplyPheromones(result.ToList(), resultValue);

            for (var i = 0; i < result.Length - 1; i++)
            {
                var from = result[i];
                var to = result[i+1];
                Assert.AreNotEqual(currentValues[from,to], _graph.Pheromones[from,to]);
            }
        }

        [Theory]
        [InlineData(0,1)]
        [InlineData(1,2)]
        [InlineData(4,5)]
        [InlineData(7,8)]
        [InlineData(8,9)]
        public void ShouldApplyMorePheromonesWithBetterResult(int x, int y)
        {
            var bestResult = 100;
            var middleResult = 1000;
            var worstResult = 10000;
            
            var exampleResult = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }.ToList();

            var current = _graph.Pheromones[x, y];

            _ant.ApplyPheromones(exampleResult, worstResult);

            var worstDiff = _graph.Pheromones[x, y] - current;

            current = _graph.Pheromones[x, y];

            _ant.ApplyPheromones(exampleResult, middleResult);

            var middleDiff = _graph.Pheromones[x, y] - current;

            current = _graph.Pheromones[x, y];

            _ant.ApplyPheromones(exampleResult, bestResult);

            var bestDiff = _graph.Pheromones[x, y] - current;

            Assert.IsTrue(bestDiff > middleDiff && bestDiff > worstDiff);
            Assert.IsTrue(middleDiff > worstDiff);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(8)]
        [InlineData(9)]
        public void ShouldComputeProbability(int start)
        {
            _ant.InitializeNotVisited(start);
            var notVisited = _ant.GetNotVisited();

            _ant.ComputeProbabilities(start);
            var probabilities = _ant.GetProbabilities();

            var last = 0.0;
            foreach (var i in notVisited)
            {
                Assert.IsTrue(probabilities[i] > last);
                last = probabilities[i];
            }

        }

        [Theory]
        [InlineData(0.01)]
        [InlineData(0.05)]
        [InlineData(0.1)]
        [InlineData(0.5)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void ShouldComputeProbabilityMoreByPheromones(double diff)
        {
            _ant.InitializeNotVisited(0);
            _ant.ComputeProbabilities(0);

            var first = _ant.GetProbabilities()[1];
            _graph.Pheromones[0, 1] += diff;

            _ant.ComputeProbabilities(0);
            var second = _ant.GetProbabilities()[1];

            Assert.IsTrue(second > first);
        }

        protected List<Task> CreateTasks()
        {
            return new List<Task>(10)
                       {
                           new Task() {Id = 0, Start = 0, Duration = 5, Estimated = 6},
                           new Task() {Id = 1, Start = 4, Duration = 3, Estimated = 8},
                           new Task() {Id = 2, Start = 8, Duration = 8, Estimated = 16},
                           new Task() {Id = 3, Start = 3, Duration = 4, Estimated = 12},
                           new Task() {Id = 4, Start = 2, Duration = 6, Estimated = 10},
                           new Task() {Id = 5, Start = 2, Duration = 1, Estimated = 5},
                           new Task() {Id = 6, Start = 5, Duration = 9, Estimated = 20},
                           new Task() {Id = 7, Start = 11, Duration = 2, Estimated = 14},
                           new Task() {Id = 8, Start = 7, Duration = 5, Estimated = 19},
                           new Task() {Id = 9, Start = 10, Duration = 4, Estimated = 14},
                       };
        }
    }
}
