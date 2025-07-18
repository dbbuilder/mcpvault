using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault.Models;
using MCPVault.Core.KeyVault.Providers;

namespace MCPVault.Core.KeyVault
{
    public class KeyVaultProviderFactory : IKeyVaultProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<KeyVaultProviderFactory> _logger;

        public KeyVaultProviderFactory(
            IServiceProvider serviceProvider,
            ILogger<KeyVaultProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IKeyVaultProvider CreateProvider(KeyVaultProviderType providerType)
        {
            _logger.LogInformation("Creating key vault provider of type {ProviderType}", providerType);

            return providerType switch
            {
                KeyVaultProviderType.Azure => _serviceProvider.GetRequiredService<AzureKeyVaultProvider>(),
                KeyVaultProviderType.AWS => _serviceProvider.GetRequiredService<AwsSecretsManagerProvider>(),
                KeyVaultProviderType.GCP => _serviceProvider.GetRequiredService<GcpSecretManagerProvider>(),
                KeyVaultProviderType.HashiCorp => _serviceProvider.GetRequiredService<HashiCorpVaultProvider>(),
                KeyVaultProviderType.Local => _serviceProvider.GetRequiredService<LocalKeyVaultProvider>(),
                _ => throw new NotSupportedException($"Provider type {providerType} is not supported")
            };
        }

        public IKeyVaultProvider CreateProvider(KeyVaultConfiguration configuration)
        {
            return CreateProvider(configuration.Provider);
        }
    }
}