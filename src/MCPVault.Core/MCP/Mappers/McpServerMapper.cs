using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MCPVault.Core.MCP.Models;
using MCPVault.Domain.Entities;
using DomainModels = MCPVault.Domain.Models;

namespace MCPVault.Core.MCP.Mappers
{
    public static class McpServerMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static Domain.Entities.McpServer ToDomainEntity(Core.MCP.Models.McpServer coreModel)
        {
            return new Domain.Entities.McpServer
            {
                Id = coreModel.Id,
                Name = coreModel.Name,
                Description = coreModel.Description,
                Url = coreModel.Url,
                ServerType = (Domain.Entities.McpServerType)coreModel.ServerType,
                AuthType = (Domain.Entities.McpAuthenticationType)coreModel.AuthType,
                CredentialsJson = coreModel.Credentials != null 
                    ? JsonSerializer.Serialize(coreModel.Credentials, JsonOptions) 
                    : null,
                ConnectionInfoJson = coreModel.ConnectionInfo != null
                    ? JsonSerializer.Serialize(coreModel.ConnectionInfo, JsonOptions)
                    : null,
                CapabilitiesJson = coreModel.Capabilities != null
                    ? JsonSerializer.Serialize(coreModel.Capabilities, JsonOptions)
                    : null,
                Status = (Domain.Entities.McpServerStatus)coreModel.Status,
                MetadataJson = coreModel.Metadata != null
                    ? JsonSerializer.Serialize(coreModel.Metadata, JsonOptions)
                    : null,
                IsActive = coreModel.IsActive,
                CreatedAt = coreModel.CreatedAt,
                UpdatedAt = coreModel.UpdatedAt,
                LastHealthCheck = coreModel.LastHealthCheck,
                OrganizationId = coreModel.OrganizationId,
                CreatedBy = coreModel.CreatedBy
            };
        }

        public static Core.MCP.Models.McpServer ToCoreModel(Domain.Entities.McpServer domainEntity)
        {
            return new Core.MCP.Models.McpServer
            {
                Id = domainEntity.Id,
                Name = domainEntity.Name,
                Description = domainEntity.Description,
                Url = domainEntity.Url,
                ServerType = (Core.MCP.Models.McpServerType)domainEntity.ServerType,
                AuthType = (Core.MCP.Models.McpAuthenticationType)domainEntity.AuthType,
                Credentials = !string.IsNullOrWhiteSpace(domainEntity.CredentialsJson)
                    ? JsonSerializer.Deserialize<McpCredentials>(domainEntity.CredentialsJson, JsonOptions)
                    : null,
                ConnectionInfo = !string.IsNullOrWhiteSpace(domainEntity.ConnectionInfoJson)
                    ? JsonSerializer.Deserialize<McpConnectionInfo>(domainEntity.ConnectionInfoJson, JsonOptions)
                    : new McpConnectionInfo { ServerId = domainEntity.Id, ServerUrl = domainEntity.Url },
                Capabilities = !string.IsNullOrWhiteSpace(domainEntity.CapabilitiesJson)
                    ? JsonSerializer.Deserialize<McpServerCapabilities>(domainEntity.CapabilitiesJson, JsonOptions)
                    : null,
                Status = (Core.MCP.Models.McpServerStatus)domainEntity.Status,
                Metadata = !string.IsNullOrWhiteSpace(domainEntity.MetadataJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(domainEntity.MetadataJson, JsonOptions)
                    : null,
                IsActive = domainEntity.IsActive,
                CreatedAt = domainEntity.CreatedAt,
                UpdatedAt = domainEntity.UpdatedAt,
                LastHealthCheck = domainEntity.LastHealthCheck,
                OrganizationId = domainEntity.OrganizationId,
                CreatedBy = domainEntity.CreatedBy
            };
        }

        public static DomainModels.McpServerFilter ToDomainFilter(Core.MCP.Models.McpServerFilter coreFilter)
        {
            return new DomainModels.McpServerFilter
            {
                ServerType = coreFilter.ServerType.HasValue 
                    ? (Domain.Entities.McpServerType)coreFilter.ServerType.Value 
                    : null,
                Status = coreFilter.Status.HasValue
                    ? (Domain.Entities.McpServerStatus)coreFilter.Status.Value
                    : null,
                IsActive = coreFilter.IsActive,
                OrganizationId = coreFilter.OrganizationId,
                SearchTerm = coreFilter.SearchTerm,
                PageSize = coreFilter.PageSize,
                PageNumber = coreFilter.PageNumber,
                SortBy = coreFilter.SortBy,
                SortDescending = coreFilter.SortDescending
            };
        }

        public static Core.MCP.Models.McpServerHealth ToCoreHealth(Domain.Entities.McpServerHealth domainHealth)
        {
            return new Core.MCP.Models.McpServerHealth
            {
                ServerId = domainHealth.ServerId,
                Status = (Core.MCP.Models.McpServerStatus)domainHealth.Status,
                CheckedAt = domainHealth.CheckedAt,
                ResponseTimeMs = domainHealth.ResponseTimeMs,
                ErrorMessage = domainHealth.ErrorMessage,
                DiagnosticInfo = !string.IsNullOrWhiteSpace(domainHealth.DiagnosticInfoJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(domainHealth.DiagnosticInfoJson, JsonOptions)
                    : null
            };
        }

        public static Domain.Entities.McpServerHealth ToDomainHealth(Core.MCP.Models.McpServerHealth coreHealth)
        {
            return new Domain.Entities.McpServerHealth
            {
                ServerId = coreHealth.ServerId,
                Status = (Domain.Entities.McpServerStatus)coreHealth.Status,
                CheckedAt = coreHealth.CheckedAt,
                ResponseTimeMs = coreHealth.ResponseTimeMs,
                ErrorMessage = coreHealth.ErrorMessage,
                DiagnosticInfoJson = coreHealth.DiagnosticInfo != null
                    ? JsonSerializer.Serialize(coreHealth.DiagnosticInfo, JsonOptions)
                    : null
            };
        }

        public static Core.MCP.Models.McpServerStatistics ToCoreStatistics(Domain.Entities.McpServerStatistics domainStats)
        {
            return new Core.MCP.Models.McpServerStatistics
            {
                ServerId = domainStats.ServerId,
                TotalRequests = domainStats.TotalRequests,
                SuccessfulRequests = domainStats.SuccessfulRequests,
                FailedRequests = domainStats.FailedRequests,
                AverageResponseTimeMs = domainStats.AverageResponseTimeMs,
                SuccessRate = domainStats.TotalRequests > 0 
                    ? (double)domainStats.SuccessfulRequests / domainStats.TotalRequests 
                    : 0,
                PeriodStart = domainStats.PeriodStart,
                PeriodEnd = domainStats.PeriodEnd
            };
        }
    }
}