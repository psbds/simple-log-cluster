using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleLogCluster
{
    class Program
    {

        private static List<List<String>> clusters = new List<List<string>>();

        static void Main(string[] args)
        {
            try
            {
                var ci = new CultureInfo("en-US");

                var stopwatch = new Stopwatch();
                if (args.Length == 0)
                {
                    throw new ArgumentException("Missing Arguments Threshold,Input andOutput");
                }
                if (args.Length == 1)
                {
                    throw new ArgumentException("Missing Arguments Input and Output");
                }

                if (args.Length == 2)
                {
                    throw new ArgumentException("Missing Argument Output");
                }

                double threshold = 0;
                if (!Double.TryParse(args[0], NumberStyles.AllowDecimalPoint, ci, out threshold) || threshold <= 0 || threshold > 1)
                {
                    throw new ArgumentException("Invalid Threshold");
                }
                var input = args[1];
                var output = args[2];

                stopwatch.Start();
                using (var file = new StreamReader(input, Encoding.UTF8))
                {
                    var data = file.ReadToEnd().Split("\n").ToList()
                        .Select(x => x.ToLower())
                        .Select(x => x.Trim())
                        .Where(x => !String.IsNullOrEmpty(x))
                        .Select(x => Regex.Replace(x, @"[^\w\s-_.,]+", "", RegexOptions.Compiled))
                        .Select(x => Regex.Replace(x, @"^(\d|\s|-|\.|\,)*$", "", RegexOptions.Compiled))
                        .Select(x => RemoveDiacritics(x))
                        .Where(x => !String.IsNullOrEmpty(x) && x != "\r")
                        .Distinct()
                        .Where(x => !GetStopWords().Contains(x))
                        .OrderBy(x => x)
                        .ToList()
                        .ToHashSet();

                    var groupLength = data.GroupBy(x => (int)x.Length / 5).ToDictionary(x => x.Key, x => x.ToList());

                    var totalGroups = groupLength.Count;
                    var processedGroups = 0;
                    Action<KeyValuePair<int, List<string>>> process = (group) =>
                       {
                           var totalLines = group.Value.Count;
                           Console.WriteLine($"Processing Lines on Group {group.Key}: {totalLines}");
                           var groupsToFind = new List<int> { group.Key };
                           if (groupLength.ContainsKey(group.Key - 1))
                               groupsToFind.Add(group.Key - 1);
                           if (groupLength.ContainsKey(group.Key + 1))
                               groupsToFind.Add(group.Key + 1);

                           while (group.Value.Count() > 0)
                           {
                               var line = group.Value.First();
                               group.Value.Remove(line);

                               var proximities = new List<String>();
                               foreach (var groupToFind in groupsToFind)
                               {
                                   var prox = groupLength[groupToFind].Where(x => JaroWinklerDistance.proximity(x, line) > threshold).ToArray();
                                   proximities.AddRange(prox);
                                   foreach (var p in prox)
                                   {
                                       proximities.Add(p);
                                       groupLength[groupToFind].Remove(p);
                                   }
                               }

                               AddToGroup(line, proximities.ToArray());
                           }

                           processedGroups++;
                           PrintProgress(totalGroups, processedGroups);
                       };

                    var groupsToProcessAsync = new List<int>();
                    var groupsToProcessAsync2 = new List<int>();
                    var groupsToProcessAsync3 = new List<int>();
                    for (var i = 0; i < groupLength.Count - 1; i = i + 3)
                    {
                        groupsToProcessAsync.Add(i);
                        if (i + 1 < groupLength.Count - 1)
                            groupsToProcessAsync2.Add(i + 1);
                        if (i + 2 < groupLength.Count - 1)
                            groupsToProcessAsync2.Add(i + 2);

                    }
                    var tasks = groupsToProcessAsync.OrderByDescending(x => x)
                        .Select(x => Task.Run(() => process(groupLength.ElementAt(x)))).ToArray();

                    Task.WaitAll(tasks);

                    var tasks2 = groupsToProcessAsync2.OrderByDescending(x => x)
                        .Select(x => Task.Run(() => process(groupLength.ElementAt(x)))).ToArray();

                    Task.WaitAll(tasks2);

                    var tasks3 = groupsToProcessAsync3.OrderByDescending(x => x)
                         .Select(x => Task.Run(() => process(groupLength.ElementAt(x)))).ToArray();

                    Task.WaitAll(tasks3);

                }

                stopwatch.Stop();

                Console.WriteLine($"Elapsed time {stopwatch.ElapsedMilliseconds / 1000}");

                using (var writeStream = new StreamWriter(output, false, Encoding.UTF8))
                {
                    var groupNumber = 1;
                    clusters.OrderByDescending(x => x.Count).ToList().ForEach(x =>
                    {
                        x.ForEach(y =>
                        {
                            writeStream.WriteLine($"{groupNumber};{y}");
                        });
                        writeStream.WriteLine(" ");
                        groupNumber++;
                    });
                }
                Console.Read();
            }
            catch (Exception exc)
            {
                ShowError(exc.Message);
                ShowError(exc.StackTrace);
            }
        }
        private static void PrintProgress(double total, double current)
        {

            var percentage = (int)((current / total) * 100);
            var str = $"[{new String('#', percentage)}{new String(' ', 100 - percentage)}]";
            Console.WriteLine(str);
        }

        private static void AddToGroup(string line, String[] proximities)
        {
            var group = new List<String>();
            group.Add(line);
            group.AddRange(proximities);
            clusters.Add(group);
        }

        static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static void ShowError(String message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
        }

        private static String[] GetStopWords()
        {
            return new string[] { "oi", "ola", "bom dia", "boa tarde", "boa noite", "ok", "sim", "nao" };
        }
    }
}
