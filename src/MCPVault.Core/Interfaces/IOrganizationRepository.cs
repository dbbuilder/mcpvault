using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Interfaces
{
    public interface IOrganizationRepository
    {
        Task<Organization?> GetByIdAsync(Guid id);
        Task<Organization?> GetBySlugAsync(string slug);
        Task<IEnumerable<Organization>> GetAllAsync();
        Task<IEnumerable<Organization>> GetActiveAsync();
        Task<Organization> CreateAsync(Organization organization);
        Task<bool> UpdateAsync(Organization organization);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ExistsBySlugAsync(string slug);
    }
}