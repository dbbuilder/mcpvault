using System;
using System.IO;
using Xunit;

namespace MCPVault.Core.Tests.CI
{
    public class CIPipelineTests
    {
        [Fact]
        public void SolutionFile_Exists()
        {
            var solutionPath = Path.Combine(GetSolutionRoot(), "MCPVault.sln");
            Assert.True(File.Exists(solutionPath), "Solution file should exist");
        }

        [Fact]
        public void AllProjectFiles_ExistAndAreValid()
        {
            var solutionRoot = GetSolutionRoot();
            var projectPaths = new[]
            {
                "src/MCPVault.Domain/MCPVault.Domain.csproj",
                "src/MCPVault.Core/MCPVault.Core.csproj",
                "src/MCPVault.Infrastructure/MCPVault.Infrastructure.csproj",
                "src/MCPVault.API/MCPVault.API.csproj",
                "tests/MCPVault.Core.Tests/MCPVault.Core.Tests.csproj",
                "tests/MCPVault.Infrastructure.Tests/MCPVault.Infrastructure.Tests.csproj",
                "tests/MCPVault.API.Tests/MCPVault.API.Tests.csproj",
                "tests/MCPVault.Integration.Tests/MCPVault.Integration.Tests.csproj"
            };

            foreach (var projectPath in projectPaths)
            {
                var fullPath = Path.Combine(solutionRoot, projectPath);
                Assert.True(File.Exists(fullPath), $"Project file should exist: {projectPath}");
            }
        }

        [Fact]
        public void GitHubActionsWorkflow_ShouldExist()
        {
            var workflowPath = Path.Combine(GetSolutionRoot(), ".github/workflows/ci.yml");
            Assert.True(File.Exists(workflowPath), "GitHub Actions workflow file should exist");
        }

        [Fact]
        public void DockerComposeFile_Exists()
        {
            var dockerComposePath = Path.Combine(GetSolutionRoot(), "docker-compose.yml");
            Assert.True(File.Exists(dockerComposePath), "Docker compose file should exist");
        }

        [Fact]
        public void RequiredDocumentationFiles_Exist()
        {
            var solutionRoot = GetSolutionRoot();
            var requiredDocs = new[]
            {
                "README.md",
                "REQUIREMENTS.md",
                "TODO.md",
                "CLAUDE.md",
                "DOCKER.md"
            };

            foreach (var doc in requiredDocs)
            {
                var fullPath = Path.Combine(solutionRoot, doc);
                Assert.True(File.Exists(fullPath), $"Documentation file should exist: {doc}");
            }
        }

        [Fact]
        public void CriticalConfigurationFiles_Exist()
        {
            var solutionRoot = GetSolutionRoot();
            var configFiles = new[]
            {
                ".gitignore",
                "src/MCPVault.API/appsettings.json",
                "src/MCPVault.API/appsettings.Development.json"
            };

            foreach (var config in configFiles)
            {
                var fullPath = Path.Combine(solutionRoot, config);
                Assert.True(File.Exists(fullPath), $"Configuration file should exist: {config}");
            }
        }

        private static string GetSolutionRoot()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(currentDirectory, "MCPVault.sln")))
            {
                var parent = Directory.GetParent(currentDirectory);
                if (parent == null)
                {
                    throw new InvalidOperationException("Could not find solution root");
                }
                currentDirectory = parent.FullName;
            }
            return currentDirectory;
        }
    }
}