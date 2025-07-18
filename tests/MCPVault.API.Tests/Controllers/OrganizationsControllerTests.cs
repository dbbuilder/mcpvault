using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.API.Controllers;
using MCPVault.API.DTOs;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;

namespace MCPVault.API.Tests.Controllers
{
    public class OrganizationsControllerTests
    {
        private readonly Mock<IOrganizationRepository> _repositoryMock;
        private readonly Mock<ILogger<OrganizationsController>> _loggerMock;
        private readonly OrganizationsController _controller;

        public OrganizationsControllerTests()
        {
            _repositoryMock = new Mock<IOrganizationRepository>();
            _loggerMock = new Mock<ILogger<OrganizationsController>>();
            _controller = new OrganizationsController(_repositoryMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkResult_WithListOfOrganizations()
        {
            var organizations = new List<Organization>
            {
                new Organization { Id = Guid.NewGuid(), Name = "Org 1", Slug = "org-1", IsActive = true },
                new Organization { Id = Guid.NewGuid(), Name = "Org 2", Slug = "org-2", IsActive = true }
            };

            _repositoryMock.Setup(x => x.GetAllAsync())
                .ReturnsAsync(organizations);

            var result = await _controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<OrganizationDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count());
        }

        [Fact]
        public async Task GetById_WithValidId_ReturnsOkResult()
        {
            var orgId = Guid.NewGuid();
            var organization = new Organization
            {
                Id = orgId,
                Name = "Test Org",
                Slug = "test-org",
                IsActive = true
            };

            _repositoryMock.Setup(x => x.GetByIdAsync(orgId))
                .ReturnsAsync(organization);

            var result = await _controller.GetById(orgId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<OrganizationDto>(okResult.Value);
            Assert.Equal(orgId, returnValue.Id);
        }

        [Fact]
        public async Task GetById_WithInvalidId_ReturnsNotFound()
        {
            var orgId = Guid.NewGuid();

            _repositoryMock.Setup(x => x.GetByIdAsync(orgId))
                .ReturnsAsync((Organization)null);

            var result = await _controller.GetById(orgId);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetBySlug_WithValidSlug_ReturnsOkResult()
        {
            var slug = "test-org";
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Test Org",
                Slug = slug,
                IsActive = true
            };

            _repositoryMock.Setup(x => x.GetBySlugAsync(slug))
                .ReturnsAsync(organization);

            var result = await _controller.GetBySlug(slug);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<OrganizationDto>(okResult.Value);
            Assert.Equal(slug, returnValue.Slug);
        }

        [Fact]
        public async Task Create_WithValidData_ReturnsCreatedResult()
        {
            var createDto = new CreateOrganizationDto
            {
                Name = "New Organization",
                Slug = "new-org"
            };

            var createdOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Slug = createDto.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _repositoryMock.Setup(x => x.ExistsBySlugAsync(createDto.Slug))
                .ReturnsAsync(false);

            _repositoryMock.Setup(x => x.CreateAsync(It.IsAny<Organization>()))
                .ReturnsAsync(createdOrg);

            var result = await _controller.Create(createDto);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<OrganizationDto>(createdResult.Value);
            Assert.Equal(createDto.Name, returnValue.Name);
            Assert.Equal(createDto.Slug, returnValue.Slug);
        }

        [Fact]
        public async Task Create_WithDuplicateSlug_ReturnsBadRequest()
        {
            var createDto = new CreateOrganizationDto
            {
                Name = "New Organization",
                Slug = "existing-org"
            };

            _repositoryMock.Setup(x => x.ExistsBySlugAsync(createDto.Slug))
                .ReturnsAsync(true);

            var result = await _controller.Create(createDto);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("slug already exists", badRequestResult.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_WithValidData_ReturnsNoContent()
        {
            var orgId = Guid.NewGuid();
            var updateDto = new UpdateOrganizationDto
            {
                Name = "Updated Organization",
                IsActive = true
            };

            var existingOrg = new Organization
            {
                Id = orgId,
                Name = "Original Name",
                Slug = "original-slug",
                IsActive = true
            };

            _repositoryMock.Setup(x => x.GetByIdAsync(orgId))
                .ReturnsAsync(existingOrg);

            _repositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Organization>()))
                .ReturnsAsync(true);

            var result = await _controller.Update(orgId, updateDto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Update_WithNonExistentId_ReturnsNotFound()
        {
            var orgId = Guid.NewGuid();
            var updateDto = new UpdateOrganizationDto
            {
                Name = "Updated Organization",
                IsActive = true
            };

            _repositoryMock.Setup(x => x.GetByIdAsync(orgId))
                .ReturnsAsync((Organization)null);

            var result = await _controller.Update(orgId, updateDto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_WithValidId_ReturnsNoContent()
        {
            var orgId = Guid.NewGuid();

            _repositoryMock.Setup(x => x.DeleteAsync(orgId))
                .ReturnsAsync(true);

            var result = await _controller.Delete(orgId);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WithNonExistentId_ReturnsNotFound()
        {
            var orgId = Guid.NewGuid();

            _repositoryMock.Setup(x => x.DeleteAsync(orgId))
                .ReturnsAsync(false);

            var result = await _controller.Delete(orgId);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetActive_ReturnsOnlyActiveOrganizations()
        {
            var organizations = new List<Organization>
            {
                new Organization { Id = Guid.NewGuid(), Name = "Active Org 1", Slug = "active-1", IsActive = true },
                new Organization { Id = Guid.NewGuid(), Name = "Active Org 2", Slug = "active-2", IsActive = true }
            };

            _repositoryMock.Setup(x => x.GetActiveAsync())
                .ReturnsAsync(organizations);

            var result = await _controller.GetActive();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<OrganizationDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count());
            Assert.All(returnValue, org => Assert.True(org.IsActive));
        }
    }
}