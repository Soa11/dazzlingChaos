// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Log;
    using Logger = Log.Logger;

    public class CodeGenRunner
    {
        private readonly Logger logger = Log.GetLogger<CodeGenRunner>();

        private readonly List<IBaker> activeBakers = new()
        {
            new EntitiesBaker(),
            new SchemaDefinitionBaker(),
            new DefinitionBaker(),
            new ExtendedDefinitionBaker(),
        };

        /// <param name="executeInBackgroundThread">
        /// If set to true, the code generation will be performed asynchronously in a background thread using the ThreadPool;
        /// otherwise, the method will complete synchronously.
        /// </param>
        public async Task<bool> RunAsync(CodeGenData codeGenData, bool executeInBackgroundThread)
        {
            var environment = CodeGenRunnerEnvironment.Create(logger);
            if (!environment.IsReady())
            {
                return false;
            }

            var bakeDirectory = codeGenData.OutputDirectory;
            if (!EnsureBakeDirectoryExistence(bakeDirectory))
            {
                return false;
            }

            using var envGuard = environment.WrapCodeGen();

            if (executeInBackgroundThread)
            {
                return await Task.Run(Execute);
            }

            return Execute();

            bool Execute()
            {
                var taskFns = GetBakeFunctors(codeGenData);
                var results = RunBakeTasks(taskFns);
                var bakeResult = AggregateBakeResults(results);
                DeleteFiles(bakeDirectory, bakeResult);
                return bakeResult.Success;
            }
        }

        public bool Run(CodeGenData codeGenData)
        {
            var environment = CodeGenRunnerEnvironment.Create(logger);
            if (!environment.IsReady())
            {
                return false;
            }

            var bakeDirectory = codeGenData.OutputDirectory;
            if (!EnsureBakeDirectoryExistence(bakeDirectory))
            {
                return false;
            }

            using var envGuard = environment.WrapCodeGen();

            var taskFns = GetBakeFunctors(codeGenData);
            var results = RunBakeTasks(taskFns);
            var bakeResult = AggregateBakeResults(results);

            DeleteFiles(bakeDirectory, bakeResult);

            return bakeResult.Success;
        }

        private static void DeleteFiles(string bakeDirectory, BakeResult result)
        {
            var enumerableFiles = Directory.EnumerateFiles(bakeDirectory, "*.cs", SearchOption.AllDirectories);
            var files = enumerableFiles as string[] ?? enumerableFiles.ToArray();

            if (result.Success)
            {
                files = files.Where(file => !result.GeneratedFiles.Contains(file.Replace('\\', '/'))).ToArray();
            }

            foreach (var file in files)
            {
                File.Delete(file);
                File.Delete($"{file}.meta");
            }
        }

        private static List<BakeResult> RunBakeTasks(List<Func<BakeResult>> taskFns)
        {
            var tasks = new List<BakeResult>();

            foreach (var taskFn in taskFns)
            {
                tasks.Add(taskFn());
            }

            return tasks;
        }

        private List<Func<BakeResult>> GetBakeFunctors(CodeGenData codeGenData)
        {
            var functors = new List<Func<BakeResult>>();

            foreach (var baker in activeBakers)
            {
                functors.Add(() => baker.Bake(codeGenData));
            }

            return functors;
        }

        private static BakeResult AggregateBakeResults(List<BakeResult> results)
        {
            var success = true;
            var generatedFiles = new HashSet<string>();

            foreach (var result in results)
            {
                if (!result.Success)
                {
                    success = false;
                }

                generatedFiles.UnionWith(result.GeneratedFiles);
            }

            return new BakeResult
            {
                Success = success,
                GeneratedFiles = generatedFiles,
            };
        }

        private bool EnsureBakeDirectoryExistence(string bakeDirectory)
        {
            var interopDir = System.IO.Path.Combine(bakeDirectory, "Interop");

            if (!string.IsNullOrEmpty(bakeDirectory) && Directory.Exists(bakeDirectory) && Directory.Exists(interopDir))
            {
                return true;
            }

            try
            {
                _ = Directory.CreateDirectory(bakeDirectory);
                _ = Directory.CreateDirectory(interopDir);
            }
            catch (Exception exception)
            {
                logger.Error(Error.CodeGenError,
                    "Failed to create baking output directory",
                    ("path", bakeDirectory),
                    ("exception", exception));
                return false;
            }

            return true;
        }
    }
}
