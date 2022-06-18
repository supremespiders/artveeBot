using artveeBot.Models;
using artveeBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace artveeBot.Extensions
{
    public static class UtilityExtensions
    {
        public static string GetStringBetween(this string text, string start, string end)
        {
            var p1 = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
            if (p1 == start.Length - 1) return null;
            var p2 = text.IndexOf(end, p1, StringComparison.Ordinal);
            if (p2 == -1) return null;
            return end == "" ? text.Substring(p1) : text.Substring(p1, p2 - p1);
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static async Task Work(this List<string> items, int maxThreads, Func<string, Task> action)
        {
            var tasks = new List<Task>();
            int i = 0;
            var worked = 0;
            //var completed=
            do
            {
                if (i < items.Count)
                {
                    var item = items[i];
                    Notifier.Display($"Working on {i + 1} / {items.Count}");
                    tasks.Add(action(item));
                    i++;
                }

                if (tasks.Count != maxThreads && i < items.Count) continue;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(t);
                    await t;
                    worked++;
                    DirectoryInfo dirInfo = new DirectoryInfo(@"img");
                    long dirSize = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Sum(file => file.Length));
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == items.Count) break;
            } while (true);

            Notifier.Display($"completed {items.Count}");
        }

        public static async Task WaitThenAddResult<T>(this List<Task<T>> tasks, List<T> results)
        {
            try
            {
                var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(t);
                results.Add(t.GetAwaiter().GetResult());
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (KnownException ex)
            {
                Notifier.Error(ex.Message);
                var t = tasks.FirstOrDefault(x => x.IsFaulted);
                tasks.Remove(t);
            }
            catch (Exception e)
            {
                Notifier.Error(e.ToString());
                var t = tasks.FirstOrDefault(x => x.IsFaulted);
                tasks.Remove(t);
            }
        }

        public static async Task<List<Artwork>> Work(this List<Artwork> items, int maxThreads, Func<Artwork, Task<Artwork>> func)
        {
            var tasks = new List<Task<Artwork>>();
            int i = 0;
            var remainingArtworks = new List<Artwork>();
            var completed = new List<string>();
            if (File.Exists("completed"))
                completed = File.ReadAllLines("completed").ToList();

            foreach (var v in items)
            {
                if (completed.Contains(v.ImageLocal))
                    continue;
                remainingArtworks.Add(v);
            }

            //var completed=
            do
            {
                if (i < remainingArtworks.Count)
                {
                    var item = remainingArtworks[i];
                    Notifier.Display($"Working on {i + 1} / {remainingArtworks.Count}");
                    tasks.Add(func(item));
                    i++;
                }

                if (tasks.Count != maxThreads && i < remainingArtworks.Count) continue;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(t);
                    var artwork = await t;
                    completed.Add(artwork.ImageLocal);
                    if (completed.Count % 50 == 0)
                        File.WriteAllLines("completed", completed);
                }
                catch (TaskCanceledException)
                {
                    File.WriteAllLines("completed", completed);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == remainingArtworks.Count) break;
            } while (true);

            Notifier.Display($"completed {remainingArtworks.Count}");
            return null;
        }

        public static async Task<List<T>> Work<T, T2>(this List<T2> items, int maxThreads, Func<T2, Task<List<T>>> func)
        {
            var tasks = new List<Task<List<T>>>();
            var results = new List<T>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // if (i % 100 == 0)
                Notifier.Display($"Working on {i + 1} / {items.Count}");
                tasks.Add(Task.Run(() => func(item)));
                if (tasks.Count == maxThreads)
                {
                    try
                    {
                        var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                        results.AddRange(t.GetAwaiter().GetResult());
                        tasks.Remove(t);
                    }
                    catch (TaskCanceledException)
                    {
                        throw;
                    }
                    catch (KnownException ex)
                    {
                        Notifier.Error(ex.Message);
                        var t = tasks.FirstOrDefault(x => x.IsFaulted);
                        tasks.Remove(t);
                    }
                    catch (Exception e)
                    {
                        Notifier.Error(e.ToString());
                        var t = tasks.FirstOrDefault(x => x.IsFaulted);
                        tasks.Remove(t);
                    }
                }
            }

            while (tasks.Count != 0)
            {
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    results.AddRange(t.GetAwaiter().GetResult());
                    tasks.Remove(t);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
            }

            Notifier.Display($"completed {items.Count}");
            return results;
        }

        public static DateTime UnixTimeStampToDateTime(this long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }

        public static async Task<HtmlDocument> ToDoc(this Task<string> task)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await task);
            return doc;
        }
    }
}