﻿#region License
//  Copyright 2015-2018 John Källén
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pytocs.TypeInference;
using Pytocs.Types;

namespace Pytocs
{
    class Program
    {
        static void Main(string[] args)
        {
            var fs = new FileSystem();
            var logger = new ConsoleLogger();
            if (args.Length == 0)
            {
                var xlator = new Translator("", "module_name", fs, logger);
                xlator.Translate("-", Console.In, Console.Out);
                Console.Out.Flush();
                return;
            }

            if (args[0].ToLower() == "-r")
            {
                var startDir = args.Length == 2
                    ? args[1]
                    : Directory.GetCurrentDirectory();
                var options = new Dictionary<string, object>();
#if !NOT_READY_FOR_TYPES
                var typeAnalysis = new AnalyzerImpl(fs, logger, options, DateTime.Now);
                typeAnalysis.Analyze(startDir);
                typeAnalysis.Finish();
                var types =
                    (from b in typeAnalysis.GetAllBindings()
                     group b by b.node into g
                     select new { g.Key, Type = UnionType.CreateUnion(g.Select(bb => bb.type)) })
                    .ToDictionary(d => d.Key, d => d.Type);

                //Console.WriteLine($"== Type dictionary: {types.Count}");
                //foreach (var de in types.OrderBy(d => d.Key.ToString()))
                //{
                //    Console.WriteLine("{0}: {1} {2}", de.Key, de.Key.Start, de.Value);
                //}

                var walker = new DirectoryWalker(fs, startDir, "*.py");
                walker.Enumerate(state =>
                {
                    foreach (var file in fs.GetFiles(state.DirectoryName, "*.py", SearchOption.TopDirectoryOnly))
                    {
                        var path = fs.GetFullPath(file);
                        var xlator = new Translator(
                             state.Namespace,
                             fs.GetFileNameWithoutExtension(file),
                             fs,
                             logger);
                        var module = typeAnalysis.GetAstForFile(path);
                        var moduleBinding = typeAnalysis.ModuleTable.Values.SelectMany(s => s).FirstOrDefault(b => b.node == module);
                        xlator.TranslateModuleStatements(
                            module.body.stmts,
                            moduleBinding.type.Table,
                            Path.ChangeExtension(path, ".cs"));
                    }
                });
#else
                var walker = new DirectoryWalker(fs, startDir, "*.py");
                walker.Enumerate(walker.ProcessDirectoryFiles);
#endif
            }
            else
            {
                foreach (var fileName in args)
                {
                    var xlator = new Translator(
                        "",
                        fs.GetFileNameWithoutExtension(fileName),
                        fs,
                        new ConsoleLogger());
                    xlator.TranslateFile(fileName, fileName + ".cs");
                }
            }
        }
    }
}
