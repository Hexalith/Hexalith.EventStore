// Azure Container Apps Deployment — Reference Parameters
// =====================================================
// Customize these parameters for your Aspire-generated Bicep output.
// See docs/guides/deployment-azure-container-apps.md for the full walkthrough.
//
// Usage:
//   az deployment group create \
//     --resource-group <your-rg> \
//     --template-file ./publish-output/azure/main.bicep \
//     --parameters ./samples/deploy/azure/main.parameters.bicepparam

using '../../../publish-output/azure/main.bicep'

// Resource group and location
param location = 'eastus'

// Container image tags (update after each build)
param commandapiImageTag = 'latest'
param sampleImageTag = 'latest'

// Container Apps Environment name
param environmentName = 'hexalith-env'

// Azure Container Registry name (must be globally unique)
param acrName = 'hexalithacr'
