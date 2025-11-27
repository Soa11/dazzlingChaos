// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.IO;
    using System.Linq;
    using Coherence.Tests;
    using Editor.Toolkit;
    using NUnit.Framework;
    using Portal;
    using UnityEngine;

    public class PostProcessorTests : CoherenceTest
    {
        private string runtimeSettingsStateBackup;

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void UpdateRuntimeSettings_Updates_Schemas_Data_In_RuntimeSettings(bool isBuildingSimulator, bool isDevelopmentMode)
        {
            var expectedSchemaNames = Paths.AllSchemas.Where(File.Exists).Select(Path.GetFileNameWithoutExtension);
            var expectedSchemaId = BakeUtil.SchemaID;

            Postprocessor.UpdateRuntimeSettings(isBuildingSimulator, isDevelopmentMode);

            var runtimeSettings = RuntimeSettings.Instance;
            Assert.That(runtimeSettings.DefaultSchemas.Select(schema => schema.name), Is.EquivalentTo(expectedSchemaNames).IgnoreCase);
            Assert.That(runtimeSettings.SchemaID, Is.EqualTo(expectedSchemaId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PostProcessor_Updates_RuntimeSettings_Data_For_Active_Project_When_Not_Building_Simulator(bool isDevelopmentMode)
        {
            var developmentProject = ConditionalProject.ForDevelopment(new(){ id = "development-project", name = "Development Project", runtime_key = "development-key" });
            var releaseProject = ConditionalProject.ForRelease(new(){ id = "release-project", name = "Release Project", runtime_key = "release-key" });
            var projects = new[] { developmentProject, releaseProject };
            var runtimeSettings = RuntimeSettings.Instance;
            var expectedProject = (isDevelopmentMode ? developmentProject : releaseProject).Project;
            var expectedSimulatorSlug = ProjectSimulatorSlugStore.Get(expectedProject.id);

            Postprocessor.UpdateRuntimeSettings(runtimeSettings, projects, isBuildingSimulator: false, isDevelopmentMode);

            Assert.That(runtimeSettings.ProjectID, Is.EqualTo(expectedProject.id));
            Assert.That(runtimeSettings.ProjectName, Is.EqualTo(expectedProject.name));
            Assert.That(runtimeSettings.RuntimeKey, Is.EqualTo(expectedProject.runtime_key));
            Assert.That(runtimeSettings.SimulatorSlug, Is.EqualTo(expectedSimulatorSlug));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PostProcessor_Does_Not_Update_RuntimeSettings_Data_For_Active_Project_When_Building_Simulator(bool isDevelopmentMode)
        {
            var developmentProject = ConditionalProject.ForDevelopment(new(){ id = "development-project", name = "Development Project", runtime_key = "development-key" });
            var releaseProject = ConditionalProject.ForRelease(new(){ id = "release-project", name = "Release Project", runtime_key = "release-key" });
            var projects = new[] { developmentProject, releaseProject };
            var runtimeSettings = RuntimeSettings.Instance;
            var expectedProject = new ProjectInfo { id = runtimeSettings.ProjectID, name = runtimeSettings.ProjectName, runtime_key = runtimeSettings.RuntimeKey };
            var expectedSimulatorSlug = runtimeSettings.SimulatorSlug;

            Postprocessor.UpdateRuntimeSettings(runtimeSettings, projects, isBuildingSimulator: true, isDevelopmentMode);

            Assert.That(runtimeSettings.ProjectID, Is.EqualTo(expectedProject.id));
            Assert.That(runtimeSettings.ProjectName, Is.EqualTo(expectedProject.name));
            Assert.That(runtimeSettings.RuntimeKey, Is.EqualTo(expectedProject.runtime_key));
            Assert.That(runtimeSettings.SimulatorSlug, Is.EqualTo(expectedSimulatorSlug));
        }

        public override void SetUp()
        {
            base.SetUp();
            runtimeSettingsStateBackup = JsonUtility.ToJson(RuntimeSettings.Instance);
        }

        public override void TearDown()
        {
            JsonUtility.FromJsonOverwrite(runtimeSettingsStateBackup, RuntimeSettings.Instance);
            base.TearDown();
        }
    }
}
