using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Domain.Entities;
using MCPVault.Infrastructure.Database;
using MCPVault.Infrastructure.Repositories;
using Dapper;
using Npgsql;
using System.Collections.Generic;
using System.Linq;

namespace MCPVault.Infrastructure.Tests.Repositories
{
    public class OrganizationRepositoryTests
    {
        private readonly Mock<IDbConnection> _dbConnectionMock;
        private readonly Mock<ILogger<OrganizationRepository>> _loggerMock;
        private readonly OrganizationRepository _repository;

        public OrganizationRepositoryTests()
        {
            _dbConnectionMock = new Mock<IDbConnection>();
            _loggerMock = new Mock<ILogger<OrganizationRepository>>();
            _repository = new OrganizationRepository(_dbConnectionMock.Object, _loggerMock.Object);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetByIdAsync_WithValidId_ReturnsOrganization()
        {
            var orgId = Guid.NewGuid();
            var expectedOrg = new Organization
            {
                Id = orgId,
                Name = "Test Organization",
                Slug = "test-org",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Organization>(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("id") != null)))
                .ReturnsAsync(expectedOrg);

            var result = await _repository.GetByIdAsync(orgId);

            Assert.NotNull(result);
            Assert.Equal(expectedOrg.Id, result.Id);
            Assert.Equal(expectedOrg.Name, result.Name);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
        {
            var orgId = Guid.NewGuid();

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Organization>(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync((Organization)null);

            var result = await _repository.GetByIdAsync(orgId);

            Assert.Null(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetBySlugAsync_WithValidSlug_ReturnsOrganization()
        {
            var slug = "test-org";
            var expectedOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Test Organization",
                Slug = slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Organization>(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("slug") != null)))
                .ReturnsAsync(expectedOrg);

            var result = await _repository.GetBySlugAsync(slug);

            Assert.NotNull(result);
            Assert.Equal(expectedOrg.Slug, result.Slug);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task CreateAsync_WithValidOrganization_ReturnsCreatedOrganization()
        {
            var newOrg = new Organization
            {
                Name = "New Organization",
                Slug = "new-org",
                IsActive = true
            };

            var createdOrgId = Guid.NewGuid();

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Guid>(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(createdOrgId);

            var result = await _repository.CreateAsync(newOrg);

            Assert.NotNull(result);
            Assert.Equal(createdOrgId, result.Id);
            Assert.Equal(newOrg.Name, result.Name);
            Assert.Equal(newOrg.Slug, result.Slug);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task UpdateAsync_WithValidOrganization_ReturnsTrue()
        {
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Updated Organization",
                Slug = "updated-org",
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            };

            _dbConnectionMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(1);

            var result = await _repository.UpdateAsync(org);

            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task UpdateAsync_WithNonExistentOrganization_ReturnsFalse()
        {
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Non-existent Organization",
                Slug = "non-existent",
                IsActive = true
            };

            _dbConnectionMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(0);

            var result = await _repository.UpdateAsync(org);

            Assert.False(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task DeleteAsync_WithValidId_ReturnsTrue()
        {
            var orgId = Guid.NewGuid();

            _dbConnectionMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("id") != null)))
                .ReturnsAsync(1);

            var result = await _repository.DeleteAsync(orgId);

            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
        {
            var orgId = Guid.NewGuid();

            _dbConnectionMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(0);

            var result = await _repository.DeleteAsync(orgId);

            Assert.False(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task ExistsBySlugAsync_WithExistingSlug_ReturnsTrue()
        {
            var slug = "existing-org";

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("slug") != null)))
                .ReturnsAsync(1);

            var result = await _repository.ExistsBySlugAsync(slug);

            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task ExistsBySlugAsync_WithNonExistentSlug_ReturnsFalse()
        {
            var slug = "non-existent-org";

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(0);

            var result = await _repository.ExistsBySlugAsync(slug);

            Assert.False(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetAllAsync_ReturnsListOfOrganizations()
        {
            var organizations = new[]
            {
                new Organization { Id = Guid.NewGuid(), Name = "Org 1", Slug = "org-1", IsActive = true },
                new Organization { Id = Guid.NewGuid(), Name = "Org 2", Slug = "org-2", IsActive = true },
                new Organization { Id = Guid.NewGuid(), Name = "Org 3", Slug = "org-3", IsActive = false }
            };

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Organization[]>(
                It.IsAny<string>(),
                null))
                .ReturnsAsync(organizations);

            var result = await _repository.GetAllAsync();

            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetActiveAsync_ReturnsOnlyActiveOrganizations()
        {
            var organizations = new[]
            {
                new Organization { Id = Guid.NewGuid(), Name = "Org 1", Slug = "org-1", IsActive = true },
                new Organization { Id = Guid.NewGuid(), Name = "Org 2", Slug = "org-2", IsActive = true }
            };

            _dbConnectionMock.Setup(x => x.ExecuteScalarAsync<Organization[]>(
                It.IsAny<string>(),
                null))
                .ReturnsAsync(organizations);

            var result = await _repository.GetActiveAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.All(result, org => Assert.True(org.IsActive));
        }
    }
}