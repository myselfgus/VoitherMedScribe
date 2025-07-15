#!/bin/bash

# 🔑 Script para testar Azure Key Vault

echo "🔍 Testando acesso ao Azure Key Vault..."
echo "================================================"

# Verificar se está autenticado
echo "1. Verificando autenticação Azure..."
az account show --query "user.name" -o tsv

echo ""
echo "2. Testando acesso ao Key Vault MedScriber..."
az keyvault secret list --vault-name MedScriber --query "length(@)"

echo ""
echo "3. Testando leitura de um secret específico..."
az keyvault secret show --vault-name MedScriber --name "AzureAd-ClientSecret" --query "value" -o tsv | head -c 20
echo "..."

echo ""
echo "4. Listando todos os secrets disponíveis..."
az keyvault secret list --vault-name MedScriber --query "[].{Name:name, Enabled:attributes.enabled}" -o table

echo ""
echo "✅ Teste do Key Vault concluído!"
echo ""
echo "📋 Para usar em produção:"
echo "1. Configure a Managed Identity no Azure App Service"
echo "2. Dê permissão 'Key Vault Secrets User' para a Managed Identity"
echo "3. Configure a variável de ambiente AZURE_KEY_VAULT_URL=https://medscriber.vault.azure.net/"
echo "4. O projeto já está configurado para usar automaticamente em produção"
