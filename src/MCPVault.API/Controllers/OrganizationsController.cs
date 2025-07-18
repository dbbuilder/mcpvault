using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MCPVault.API.DTOs;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;

namespace MCPVault.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class OrganizationsController : ControllerBase
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILogger<OrganizationsController> _logger;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            ILogger<OrganizationsController> logger)
        {
            _organizationRepository = organizationRepository;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), 200)]
        public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll()
        {
            var organizations = await _organizationRepository.GetAllAsync();
            var dtos = organizations.Select(MapToDto);
            return Ok(dtos);
        }

        [HttpGet("active")]
        [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), 200)]
        public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetActive()
        {
            var organizations = await _organizationRepository.GetActiveAsync();
            var dtos = organizations.Select(MapToDto);
            return Ok(dtos);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(OrganizationDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<OrganizationDto>> GetById(Guid id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(organization));
        }

        [HttpGet("slug/{slug}")]
        [ProducesResponseType(typeof(OrganizationDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<OrganizationDto>> GetBySlug(string slug)
        {
            var organization = await _organizationRepository.GetBySlugAsync(slug);
            if (organization == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(organization));
        }

        [HttpPost]
        [ProducesResponseType(typeof(OrganizationDto), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var slugExists = await _organizationRepository.ExistsBySlugAsync(createDto.Slug);
            if (slugExists)
            {
                return BadRequest(new { error = "Organization slug already exists" });
            }

            var organization = new Organization
            {
                Name = createDto.Name,
                Slug = createDto.Slug,
                Settings = createDto.Settings,
                IsActive = true
            };

            var created = await _organizationRepository.CreateAsync(organization);
            var dto = MapToDto(created);

            _logger.LogInformation("Created organization: {OrganizationId} - {OrganizationName}", 
                created.Id, created.Name);

            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(updateDto.Name))
            {
                organization.Name = updateDto.Name;
            }

            if (updateDto.IsActive.HasValue)
            {
                organization.IsActive = updateDto.IsActive.Value;
            }

            if (updateDto.Settings != null)
            {
                organization.Settings = updateDto.Settings;
            }

            var updated = await _organizationRepository.UpdateAsync(organization);
            if (!updated)
            {
                return NotFound();
            }

            _logger.LogInformation("Updated organization: {OrganizationId}", id);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _organizationRepository.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            _logger.LogInformation("Deleted organization: {OrganizationId}", id);

            return NoContent();
        }

        private static OrganizationDto MapToDto(Organization organization)
        {
            return new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                Slug = organization.Slug,
                IsActive = organization.IsActive,
                Settings = organization.Settings,
                CreatedAt = organization.CreatedAt,
                UpdatedAt = organization.UpdatedAt
            };
        }
    }
}