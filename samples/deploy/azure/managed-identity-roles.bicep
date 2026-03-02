// Managed Identity Role Assignments for Azure Container Apps
// ==========================================================
// Reference Bicep module showing the role assignments needed for
// managed identity to access Azure Cosmos DB and Azure Service Bus.
//
// See docs/guides/deployment-azure-container-apps.md for the full walkthrough.

@description('Principal ID of the container app managed identity')
param principalId string

@description('Azure Cosmos DB account resource ID')
param cosmosDbAccountId string

@description('Azure Service Bus namespace resource ID')
param serviceBusNamespaceId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  scope: resourceGroup(subscription().subscriptionId, split(serviceBusNamespaceId, '/')[4])
  name: split(serviceBusNamespaceId, '/')[8]
}

// Cosmos DB Built-in Data Contributor role
// Allows read/write access to Cosmos DB data
var cosmosDbDataContributorRoleId = '00000000-0000-0000-0000-000000000002'

// Azure Service Bus Data Sender role
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'

// Azure Service Bus Data Receiver role
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

// Cosmos DB — Data Contributor
resource cosmosDbRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  name: '${guid(cosmosDbAccountId, principalId, cosmosDbDataContributorRoleId)}'
  properties: {
    principalId: principalId
    roleDefinitionId: '${cosmosDbAccountId}/sqlRoleDefinitions/${cosmosDbDataContributorRoleId}'
    scope: cosmosDbAccountId
  }
}

// Service Bus — Data Sender
resource serviceBusSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, principalId, serviceBusDataSenderRoleId)
  scope: serviceBusNamespace
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalType: 'ServicePrincipal'
  }
}

// Service Bus — Data Receiver
resource serviceBusReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, principalId, serviceBusDataReceiverRoleId)
  scope: serviceBusNamespace
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalType: 'ServicePrincipal'
  }
}
