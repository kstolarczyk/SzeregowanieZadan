namespace SzeregowanieZadan
{
    using System;
    using System.Collections.Generic;

    class Ant
    {
        protected Graph _graph;

        private LinkedList<int> _notVisited;

        protected int _tasksCount;

        private double[] _probabilities;

        private LightMachine[] _machines;

        private static object _mutex = new object();

        private int _current;

        private List<int> _result;

        private int _visitedCount;

        public Ant(Graph graph, int machinesCount)
        {
            _graph = graph;
            _tasksCount = graph.Tasks.Count;
            _notVisited = new LinkedList<int>();
            _probabilities = new double[_tasksCount];
            _machines = InitializeMachines(machinesCount, _graph.MaxTime*2);
            _result = new List<int>(_tasksCount);
        }

        private LightMachine[] InitializeMachines(int machinesCount, int maxTime)
        {
            var machines = new LightMachine[machinesCount];
            for (var i = 0; i < machinesCount; i++)
            {
                machines[i] = new LightMachine(maxTime);
            }
            return machines;
        }

        public void Run()
        {
            for (var i = 0; i < Config.MAX_ITERATION; i++)
            {
                _current = RandomGen.Next(_tasksCount);
                _result.Clear();
                _result.Add(_current);
                _visitedCount = 1;
                InitializeNotVisited(_current);

                while (_visitedCount++ < _tasksCount)
                {
                    _current = ChooseNext(_current);
                    _result.Add(_current);
                    _notVisited.Remove(_current);
                }

                var delay = NaiveAlgorithm(_result);
                if (delay < _graph.BestResult)
                {
                    UpdateBestResult(delay);
                }
                ApplyPheromones(_result, delay);
            }
        }

        private void UpdateBestResult(int delay)
        {
            lock (_mutex)
            {
                _graph.BestResult = Math.Min(_graph.BestResult, delay);
            }
            Console.WriteLine($"Best result: {delay}");
        }

        protected virtual void ApplyPheromones(List<int> result, int delay)
        {
            var quality = (double)_graph.BestResult / delay;
            var pheromones = Config.QF * quality;
            for (var i = 0; i < _tasksCount - 1; i++)
            {
                _graph.Pheromones[result[i], result[i + 1]] += pheromones;
            }
        }

        private int ChooseNext(int from)
        {
            ComputeProbabilities(from);
            var rnd = RandomGen.NextDouble();
            return FindNext(rnd);
        }

        private int FindNext(double rnd)
        {
            foreach (var i in _notVisited)
            {
                if (rnd < _probabilities[i]) return i;
            }
            throw new Exception($"Nie znaleziono następnego elementu dla rnd = {rnd}");
        }

        private void InitializeNotVisited(int start)
        {
            _notVisited.Clear();
            for (var i = 0; i < _tasksCount; i++)
            {
                if(i == start) continue;
                _notVisited.AddLast(i);
            }
        }

        private void ComputeProbabilities(int from)
        {
            var sum = 0.0;
            foreach (var next in _notVisited)
            {
                var distance = _graph.Edges[from][next].TimeDistance + 1;
                var pheromone = _graph.Pheromones[from, next];
                var value = Math.Pow(1.0 / distance, Config.BETA) * Math.Pow(pheromone, Config.ALPHA);
                _probabilities[next] = value;
                sum += value;
            }
            var current = 0.0;
            foreach (var next in _notVisited)
            {
                _probabilities[next] /= sum;
                _probabilities[next] += current;
                current = _probabilities[next];
            }
            _probabilities[_notVisited.Last.Value] = 1.0;
        }

        public int NaiveAlgorithm(IEnumerable<int> result)
        {
            var totalDelay = 0;
            foreach (var taskIter in result)
            {
                var added = false;
                var currentTask = _graph.Tasks[taskIter];
                var currentTime = currentTask.Start;
                while (!added)
                {
                    for(var i = 0; i < _machines.Length; i++)
                    {
                        if (_machines[i].LockTimespan(currentTime, currentTask.Duration))
                        {
                            totalDelay += Math.Max(0, currentTime + currentTask.Duration - currentTask.Estimated);
                            added = true; 
                            break;
                        }
                    }
                    currentTime++;
                }
            }
            foreach (var lightMachine in _machines)
            {
                lightMachine.ClearLocked();
            }
            return totalDelay;
        }
    }

    class SpecialAnt : Ant
    {
        protected override void ApplyPheromones(List<int> result, int delay)
        {
            base.ApplyPheromones(result, delay);
            for (var i = 0; i < _tasksCount; i++)
            {
                for (var j = i+1; j < _tasksCount; j++)
                {
                    _graph.Pheromones[i,j] *= Config.EVAPORATION;
                    _graph.Pheromones[j,i] *= Config.EVAPORATION;
                }
            }
        }

        public SpecialAnt(Graph graph, int machinesCount)
            : base(graph, machinesCount)
        {
        }
    }

    class LightMachine
    {
        private bool[] _locked;

        public LightMachine(int maxCount)
        {
            _locked = new bool[maxCount];
        }

        public bool CheckFree(int start, int duration)
        {
            for (var i = start; i < start + duration; i++)
            {
                if (_locked[i]) return false;
            }
            return true;
        }

        public bool LockTimespan(int start, int duration)
        {
            if (!CheckFree(start, duration)) return false;
            for (var i = start; i < start + duration; i++)
            {
                _locked[i] = true;
            }
            return true;
        }

        public void ClearLocked()
        {
            for (var i = 0; i < _locked.Length; i++)
            {
                _locked[i] = false;
            }
        }
    }
}
