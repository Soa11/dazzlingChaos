namespace Coherence.CodeGen
{
    using System;
    using System.Collections.Generic;
    using Scriban.Runtime;

    public class ImplementationsBaker : IBaker
    {
        public BakeResult Bake(CodeGenData data)
        {
            if (data.NoUnityReferences || !data.BakeToolkitImplementations)
            {
                return new BakeResult
                {
                    Success = true,
                    GeneratedFiles = new HashSet<string>(),
                };
            }

            var result = true;
            var files = new HashSet<string>();

            var scribanWriter = new ScribanWriter();

            result &= GenerateImplBridge(scribanWriter, data.OutputDirectory, files);

            result &= GenerateImplLiveQuery(scribanWriter, data.OutputDirectory, files);

            result &= GenerateImplTagQuery(scribanWriter, data.OutputDirectory, files);

            result &= GenerateImplGlobalQuery(scribanWriter, data.OutputDirectory, files);

            result &= GenerateImplSync(scribanWriter, data.OutputDirectory, files);

            return new BakeResult
            {
                Success = result,
                GeneratedFiles = files,
            };
        }

        private static bool GenerateImplBridge(ScribanWriter scribanWriter, string savePath,
            HashSet<string> files)
        {
            var scribanOptions = new ScribanOptions
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Toolkit",
                    "System",
                    "Coherence.ProtocolDef",
                    "ConnectionType = Coherence.Connection.ConnectionType",
                    "ClientID = Coherence.Connection.ClientID",
                    "Coherence.Entities",
                    "Coherence.SimulationFrame",
                    "Coherence.Core",
                },
                TemplateNames = new List<string>
                {
                    "impl_bridge",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>(),
            };

            var renderResult = scribanWriter.Render(scribanOptions, savePath, "ImplBridge");

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return renderResult.Success;
        }

        private static bool GenerateImplLiveQuery(ScribanWriter scribanWriter, string savePath,
            HashSet<string> files)
        {
            ScribanOptions scribanOptions = new()
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Entities",
                    "Toolkit",
                    "Coherence.SimulationFrame",
                },
                TemplateNames = new List<string>
                {
                    "impl_livequery",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>(),
            };

            var renderResult = scribanWriter.Render(scribanOptions, savePath, "ImplLiveQuery");

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return renderResult.Success;
        }

        private static bool GenerateImplTagQuery(ScribanWriter scribanWriter, string savePath,
            HashSet<string> files)
        {
            ScribanOptions scribanOptions = new()
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Entities",
                    "Toolkit",
                    "Coherence.SimulationFrame",
                },
                TemplateNames = new List<string>
                {
                    "impl_tagquery",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>(),
            };

            var renderResult = scribanWriter.Render(scribanOptions, savePath, "ImplTagQuery");

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return renderResult.Success;
        }

        private static bool GenerateImplGlobalQuery(ScribanWriter scribanWriter, string savePath,
            HashSet<string> files)
        {
            ScribanOptions scribanOptions = new()
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Entities",
                    "Toolkit",
                    "Coherence.SimulationFrame",
                },
                TemplateNames = new List<string>
                {
                    "impl_globalquery",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>(),
            };

            var renderResult = scribanWriter.Render(scribanOptions, savePath, "ImplGlobalQuery");

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return renderResult.Success;
        }

        private static bool GenerateImplSync(ScribanWriter scribanWriter, string savePath,
            HashSet<string> files)
        {
            ScribanOptions scribanOptions = new()
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Toolkit",
                    "System",
                    "Coherence.ProtocolDef",
                    "System.Collections.Generic",
                    "Log",
                    "Logger = Log.Logger",
                    "Coherence.Entities",
                    "Coherence.SimulationFrame",
                },
                TemplateNames = new List<string>
                {
                    "impl_sync",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>(),
            };

            var renderResult = scribanWriter.Render(scribanOptions, savePath, "ImplSync");

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return renderResult.Success;
        }
    }
}
