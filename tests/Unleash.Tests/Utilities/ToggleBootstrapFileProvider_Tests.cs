using FluentAssertions;
using NUnit.Framework;
using System.IO;
using System.Text;
using Unleash.Internal;
using Unleash.Serialization;
using Unleash.Utilities;

namespace Unleash.Tests.Utilities
{
    public class ToggleBootstrapFileProvider_Tests
    {
        protected string AppDataFile(string filename)
        {
            var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data", filename);
            return file;
        }

        [Test]
        public void Returns_String_Empty_When_File_Does_Not_Exist()
        {
            // Arrange
            var fileSystem = new FileSystem(Encoding.UTF8);
            string toggleFileName = AppDataFile("unleash-repo-v1-missing.json");
            var toggleFileProvider = new ToggleBootstrapFileProvider(toggleFileName, new UnleashSettings() { FileSystem = fileSystem, JsonSerializer = new JsonSerializer() });

            // Act
            var emptyResult = toggleFileProvider.Read();

            // Assert
            emptyResult.Should().Equals(string.Empty);
        }

        [Test]
        public void Returns_File_Content_When_File_Exists()
        {
            // Arrange
            var fileSystem = new FileSystem(Encoding.UTF8);
            string toggleFileName = AppDataFile("unleash-repo-v1.json");
            var toggleFileProvider = new ToggleBootstrapFileProvider(toggleFileName, new UnleashSettings() { FileSystem = fileSystem, JsonSerializer = new JsonSerializer() });
            var fileContent = fileSystem.ReadAllText(toggleFileName);

            // Act
            var result = toggleFileProvider.Read();

            // Assert
            result.Should().Equals(fileContent);
        }

        [Test]
        public void Returns_File_Content_When_Configured_Through_Settings_And_File_Exists()
        {
            // Arrange
            var settings = new UnleashSettings()
            {
                JsonSerializer = new JsonSerializer(),
            };
            var toggleFileName = AppDataFile("unleash-repo-v1.json");
            settings.UseBootstrapFileProvider(toggleFileName);
            var fileSystem = new FileSystem(Encoding.UTF8);
            settings.FileSystem = fileSystem;

            var fileContent = fileSystem.ReadAllText(toggleFileName);

            // Act
            var result = settings.ToggleBootstrapProvider.Read();

            // Assert
            result.Should().Equals(fileContent);
        }
    }
}
