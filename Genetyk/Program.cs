namespace Genetyk
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using SzeregowanieZadan;

    class Program
    {
        static void Main(string[] args)
        {
            // Helper.GeneratePro(40,4,"test.txt");
            var tasks = Helper.ParseTasks(File.ReadAllLines("test.txt"));
            var machines = Helper.SortedAlgorithm(tasks, out var delay);
            Console.WriteLine($"Sorted naive algorithm delay: {delay}");
            var watch = new Stopwatch();
            var genetic = new GeneticAlgorithm(tasks);
            watch.Start();
            genetic.Run();
            watch.Stop();
            Console.WriteLine($"Elapsed time: {watch.ElapsedMilliseconds} ms");
            Console.ReadKey();

        }
    }
}
