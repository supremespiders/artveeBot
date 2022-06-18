using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using artveeBot.Models;
using artveeBot.Services;
using athenaeumBot.Models;
using Newtonsoft.Json;

namespace artveeBot.Extensions
{
    public static class MultiTaskingExtensions
    {
        public static void Save<T>(this List<T> items, string path = null)
        {
            var name = typeof(T).Name;
            if (path != null) name = path;
            File.WriteAllText(name, JsonConvert.SerializeObject(items));
        }

        public static List<T> Load<T>(this string path)
        {
            return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path));
        }

        public static async Task<List<T>> Scrape<T>(this IReadOnlyList<string> inputs, Func<string, Task<T>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            Notifier.Display("Start working");

            for (var i = 0; i < remainingInputs.Count; i++)
            {
                var input = inputs[i];
                Notifier.Progress(i + 1, inputs.Count);
                Notifier.Display($"Working on {i + 1} / {inputs.Count}");
                try
                {
                    outputs.Add(await work(input));
                }
                catch (TaskCanceledException)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                }
            }

            Notifier.Display("Work completed");
            return outputs;
        }


           private static async Task<List<T>> LoopTasks<T>(this IReadOnlyList<T> inputs, List<T> outputs, int threads, Func<T, Task<T>> work) where T :IWebItem
        {
            var name = typeof(T).Name;
            Notifier.Display("Start working");
            var i = 0;
            var taskUrls = new Dictionary<int, string>();
            var tasks = new List<Task<T>>();
            do
            {
                if (i < inputs.Count)
                {
                    var item = inputs[i];
                    Notifier.Display($"Working on {i + 1} / {inputs.Count} , Total collected : {outputs.Count}");
                    var t = work(item);
                    taskUrls.Add(t.Id, item.Url);
                    tasks.Add(t);
                    i++;
                }

                if (tasks.Count != threads && i < inputs.Count) continue;
                var currentTaskId = -1;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    currentTaskId = t.Id;
                    tasks.Remove(t);
                    outputs.Add(await t);
                }
                catch (TaskCanceledException e)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{ex.Message}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{e}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == inputs.Count) break;
            } while (true);

            outputs.Save(name);
            Notifier.Display("Work completed");
            return outputs;
        }
        private static async Task<List<T>> LoopTasks<T>(this IReadOnlyList<string> inputs, List<T> outputs, int threads, Func<string, Task<T>> work)
        {
            var name = typeof(T).Name;
            Notifier.Display("Start working");
            var i = 0;
            var taskUrls = new Dictionary<int, string>();
            var tasks = new List<Task<T>>();
            do
            {
                if (i < inputs.Count)
                {
                    var item = inputs[i];
                    Notifier.Display($"Working on {i + 1} / {inputs.Count} , Total collected : {outputs.Count}");
                    var t = work(item);
                    taskUrls.Add(t.Id, item);
                    tasks.Add(t);
                    i++;
                }

                if (tasks.Count != threads && i < inputs.Count) continue;
                var currentTaskId = -1;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    currentTaskId = t.Id;
                    tasks.Remove(t);
                    outputs.Add(await t);
                }
                catch (TaskCanceledException e)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{ex.Message}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{e}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == inputs.Count) break;
            } while (true);

            outputs.Save(name);
            Notifier.Display("Work completed");
            return outputs;
        }

        private static async Task<List<T>> LoopTasks<T>(this IReadOnlyList<string> inputs, List<T> outputs, int threads, Func<string, Task<List<T>>> work)
        {
            var name = typeof(T).Name;
            if (name == "String") name = "URLS";
            Notifier.Display("Start working");
            var i = 0;
            var taskUrls = new Dictionary<int, string>();
            var tasks = new List<Task<List<T>>>();
            do
            {
                if (i < inputs.Count)
                {
                    var item = inputs[i];
                    Notifier.Display($"Working on {i + 1} / {inputs.Count} , Total collected : {outputs.Count}");
                    var t = work(item);
                    taskUrls.Add(t.Id, item);
                    tasks.Add(t);
                    i++;
                }

                if (tasks.Count != threads && i < inputs.Count) continue;
                var currentTaskId = -1;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    currentTaskId = t.Id;
                    tasks.Remove(t);
                    outputs.AddRange(await t);
                }
                catch (TaskCanceledException e)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{ex.Message}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{e}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == inputs.Count) break;
            } while (true);

            outputs.Save(name);
            Notifier.Display("Work completed");
            return outputs;
        }

        public static async Task<List<T>> ScrapeParallel<T>(this IReadOnlyList<string> inputs, int threads, Func<string, Task<T>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            outputs = outputs.GroupBy(x => x.Url).Select(x => x.First()).ToList();
            outputs.Save(name);
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }

        public static async Task<List<T>> ScrapeParallel<T>(this IReadOnlyList<string> inputs, int threads, Func<string, Task<List<T>>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }
        
        public static async Task<List<T>> ScrapeParallel<T>(this IReadOnlyList<T> inputs, int threads, Func<T, Task<T>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            outputs = outputs.GroupBy(x => x.Url).Select(x => x.First()).ToList();
            outputs.Save(name);
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x.Url));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }

        public static async Task<List<string>> ScrapeUrlsParallel(this IReadOnlyList<string> inputs, int threads, Func<string, Task<List<string>>> work)
        {
            var name = "URLS";
            var outputs = new List<string>();
            if (File.Exists(name))
                outputs = name.Load<string>();
            if (outputs == null) throw new KnownException($"Null output on file");
            outputs = outputs.Distinct().ToList();
            outputs.Save(name);
            var collected = outputs.ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }
    }
}