namespace SzeregowanieZadan
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    class Ant
    {
        protected Graph _graph;

        private LinkedList<int> _notVisited;

        protected int _tasksCount;

        private double[] _probabilities;

        private Machine[] _machines;

        private static object _mutex = new object();

        protected static object[,] _mutexPheromones;

        private int _current;

        private List<int> _result;

        private int _visitedCount;
        private static Barrier barrier = new Barrier(Config.ANTS);

        public Ant(Graph graph, int machinesCount)
        {
            _graph = graph;
            _tasksCount = graph.Tasks.Count;
            _notVisited = new LinkedList<int>();
            _probabilities = new double[_tasksCount];
            _machines = InitializeMachines(machinesCount);
            _mutexPheromones = new object[_tasksCount, _tasksCount];
            InitializeMutexes(_tasksCount);
            _result = new List<int>(_tasksCount);
        }

        private void InitializeMutexes(int tasksCount)
        {
            for(var i = 0; i < tasksCount; i++)
            {
                for(var j = i+1; j< tasksCount; j++)
                {
                    _mutexPheromones[i, j] = new object();
                    _mutexPheromones[j, i] = new object();
                }
            }

        }

        private Machine[] InitializeMachines(int machinesCount)
        {
            var machines = new Machine[machinesCount];
            for (var i = 0; i < machinesCount; i++)
            {
                machines[i] = new Machine();
            }
            return machines;
        }

        public void Run(object obj)
        {
            var cancelToken = (CancellationToken)obj;
            barrier.SignalAndWait();
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
                if (delay < _graph.BestResultValue)
                {
                    UpdateBestResult(delay);
                }
                ApplyPheromones(_result, delay);
                if (cancelToken.IsCancellationRequested) break;
            }
            barrier.SignalAndWait();
        }

        private void UpdateBestResult(int delay)
        {
            lock (_mutex)
            {
                if(delay < _graph.BestResultValue)
                {
                    _graph.BestResultValue = delay;
                    CopyCurrentResult();
                }
            }
        }

        private void CopyCurrentResult()
        {
            for(var i = 0; i < _tasksCount; i++)
            {
                _graph.BestResult[i] = _result[i];
            }
        }

        protected virtual void ApplyPheromones(List<int> result, int delay)
        {
            var quality = (double)_graph.BestResultValue / delay;
            var pheromones = Config.QF * quality;
            for (var i = 0; i < _tasksCount - 1; i++)
            {
                lock(_mutexPheromones[result[i], result[i+1]])
                {
                    _graph.Pheromones[result[i], result[i + 1]] = pheromones;
                }
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
            var totalTasks = 0;
            foreach (var taskIter in result)
            {
                if (totalDelay > _graph.BestResultValue)
                {
                    return totalDelay * (_tasksCount / totalTasks);
                }

                var added = false;
                var currentTask = _graph.Tasks[taskIter];
                var currentTime = currentTask.Start;
                var endTime = currentTask.Duration + currentTime - 1;

         
                for (var i = 0; i < _machines.Length; i++)
                {
                    if (_machines[i].AddTask(currentTime, endTime))
                    {
                        totalDelay += Math.Max(0, endTime - currentTask.Estimated);
                        added = true;
                        totalTasks++;
                        break;
                    }
                }

                if(added) continue;
                
                var machineIter = 0;
                currentTime = int.MaxValue;
                for (var i = 0; i < _machines.Length; i++)
                {
                    var ff = _machines[i].FirstFree(currentTask.Start, endTime);
                    if (ff < currentTime)
                    {
                        currentTime = ff;
                        machineIter = i;
                    }
                }

                endTime = currentTime + currentTask.Duration - 1;
                _machines[machineIter].AddTask(currentTime, endTime);
                totalDelay += Math.Max(0, endTime - currentTask.Estimated);
                totalTasks++;
                
            }
            ClearMachines();
            return totalDelay;
        }

        private void ClearMachines()
        {
            foreach (var machine in _machines)
            {
                machine.ClearFree();
            }
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
                    lock(_mutexPheromones[i,j])
                    {
                        _graph.Pheromones[i, j] *= Config.EVAPORATION;
                    }
                    lock(_mutexPheromones[j,i])
                    {
                        _graph.Pheromones[j, i] *= Config.EVAPORATION;
                    }
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
