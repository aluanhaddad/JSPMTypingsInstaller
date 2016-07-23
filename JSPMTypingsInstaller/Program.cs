﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static System.Console;

namespace JSPMTypingsInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var options = ParseArgs(args);
                Func<string, bool> matchesFilter = t => t.StartsWith(options.FrameworkNameOrPrefix, StringComparison.CurrentCultureIgnoreCase);

                var rawConfig = File.ReadAllText(options.JspmConfig);
                var declaractiveConfigs = rawConfig.SplitRemoveEmpty("SystemJS.config(", "(", ")", ";");

                var configs = JArray.FromObject(
                    from systemJSConfigCall in declaractiveConfigs
                    where systemJSConfigCall.StartsWith("{", StringComparison.CurrentCulture)
                    where systemJSConfigCall.EndsWith("}", StringComparison.CurrentCulture)
                    select JObject.Parse(systemJSConfigCall)
                );
                var matches =
                    from config in configs.DescendantsAndSelf().OfType<JObject>()
                    from property in config.Properties()
                    let aureliaPackageName = property.Name
                    where matchesFilter(aureliaPackageName)
                    let value = (string)property.Value
                    let replacement = ($"github:{options.FrameworkNameOrPrefix}") + (value.StartsWith(options.FrameworkNameOrPrefix, StringComparison.CurrentCulture) && value.IndexOf('-') == -1 ? string.Empty : "/")
                    let split = value.SplitRemoveEmpty($"npm:{options.FrameworkNameOrPrefix}-", $"npm:{options.FrameworkNameOrPrefix}")
                    let target = replacement + split.FirstOrDefault().Replace('@', '#')
                    select $"typings install {target}";
                var output = options.Outfile;

                matches.Select(cmd => $"Added command: {cmd}").ToList().ForEach(WriteLine);

                using (var outfile = File.CreateText(output))
                {
                    matches.ToList().ForEach(outfile.WriteLine);
                }
                WriteLine($"Successfully created {Path.GetFullPath(options.Outfile)}");
            }
            catch
            {
                WriteLine("Usage: JSPMTypingsInstaller [--jspmconfig path-to-jspm-config] [--framework name-or-prefix] [--out .\\install-typings.ps1]");
            }
        }

        private static Config ParseArgs(string[] args)
        {
            var jspmConfig = args.SkipWhile(a => a != "--jspmconfig").Skip(1).DefaultIfEmpty("jspm.config.js").First();
            var frameworkName = args.SkipWhile(a => a != "--framework").Skip(1).DefaultIfEmpty("aurelia").First();
            var outfile = args.SkipWhile(a => a != "--out").Skip(1).DefaultIfEmpty(Directory.GetCurrentDirectory() + "\\install-typings.ps1").First();
            return new Config(jspmConfig, frameworkName, outfile);
        }
    }

    public static class StringExtensions
    {
        public static string[] SplitRemoveEmpty(this string value, string dilimiter, params string[] additionalDilimiters) =>
            value.Split(new[] { dilimiter }.Concat(additionalDilimiters).ToArray(), StringSplitOptions.RemoveEmptyEntries);
    }

    struct Config
    {

        public Config(string jspmConfig, string frameworkName, string outfile)
        {
            JspmConfig = jspmConfig;
            FrameworkNameOrPrefix = frameworkName;
            Outfile = outfile;
        }

        public string FrameworkNameOrPrefix { get; }

        public string JspmConfig { get; }

        public string Outfile { get; }
    }
}
