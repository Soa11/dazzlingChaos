// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.IO;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEditor;

    public class ZipUtilsTests : CoherenceTest
    {
        [Test]
        public void CanFind7z()
        {
            Assert.True(!string.IsNullOrEmpty(ZipUtils.ExecutablePath));
            Assert.True(File.Exists(ZipUtils.ExecutablePath));
        }

        [Test]
        public void CanZip()
        {
            var path = Path.GetFullPath(FileUtil.GetUniqueTempPathInProject());
            var zipPath = Path.Combine(path, "test.zip");

            var fileToZip = Path.GetFullPath(Paths.toolkitSchemaPath);
            Assert.DoesNotThrow(() => ZipUtils.Zip(fileToZip, zipPath));
            File.Delete(zipPath);
        }
    }
}
