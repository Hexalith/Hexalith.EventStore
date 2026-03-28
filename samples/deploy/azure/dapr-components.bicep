// DAPR Components for Azure Container Apps Environment
// =====================================================
// Reference Bicep module showing how to define DAPR state store (Cosmos DB)
// and pub/sub (Service Bus) components in the Container Apps Environment.
//
// This supplements the Aspire-generated Bicep output, which does NOT include
// DAPR configuration. See docs/guides/deployment-azure-container-apps.md.

@description('Name of the Container Apps Environment')
param environmentName string

@description('Azure Cosmos DB account endpoint URL')
param cosmosDbUrl string

@description('Azure Cosmos DB database name')
param cosmosDbDatabase string = 'eventstore'

@description('Azure Cosmos DB collection/container name')
param cosmosDbCollection string = 'actorstate'

@description('Managed identity client ID for Azure service authentication (omit for system-assigned)')
param managedIdentityClientId string = ''

@description('Azure Service Bus namespace name (e.g., mynamespace.servicebus.windows.net)')
param serviceBusNamespace string

// Reference the existing Container Apps Environment
resource environment 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: environmentName
}

// DAPR State Store Component — Azure Cosmos DB (Tier 1)
resource statestore 'Microsoft.App/managedEnvironments/daprComponents@2025-01-01' = {
  parent: environment
  name: 'statestore'
  properties: {
    componentType: 'state.azure.cosmosdb'
    version: 'v1'
    metadata: concat([
      {
        name: 'url'
        value: cosmosDbUrl
      }
      {
        name: 'database'
        value: cosmosDbDatabase
      }
      {
        name: 'collection'
        value: cosmosDbCollection
      }
      {
        name: 'actorStateStore'
        value: 'true'
      }
    ], empty(managedIdentityClientId) ? [] : [
      {
        name: 'azureClientId'
        value: managedIdentityClientId
      }
    ])
    scopes: [
      'eventstore'
    ]
  }
}

// DAPR Pub/Sub Component — Azure Service Bus (Tier 1)
resource pubsub 'Microsoft.App/managedEnvironments/daprComponents@2025-01-01' = {
  parent: environment
  name: 'pubsub'
  properties: {
    componentType: 'pubsub.azure.servicebus.topics'
    version: 'v1'
    metadata: concat([
      {
        name: 'namespaceName'
        value: serviceBusNamespace
      }
      {
        name: 'enableDeadLetter'
        value: 'true'
      }
      {
        name: 'deadLetterTopic'
        value: 'deadletter'
      }
    ], empty(managedIdentityClientId) ? [] : [
      {
        name: 'azureClientId'
        value: managedIdentityClientId
      }
    ])
    scopes: [
      'eventstore'
    ]
  }
}
