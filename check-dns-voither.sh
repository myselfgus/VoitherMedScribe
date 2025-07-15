#!/bin/bash

# 🌐 Script para verificar DNS do domínio voither.com

echo "🔍 Verificando DNS do domínio voither.com..."
echo "=============================================="

echo ""
echo "📋 Registros A (IPv4):"
dig +short A voither.com

echo ""
echo "📋 Registros AAAA (IPv6):"
dig +short AAAA voither.com

echo ""
echo "📋 Registros MX (Email):"
dig +short MX voither.com

echo ""
echo "📋 Registros TXT (Verificações):"
dig +short TXT voither.com

echo ""
echo "📋 Registros CNAME (Aliases):"
dig +short CNAME voither.com

echo ""
echo "📋 Registro SOA (Autoridade):"
dig +short SOA voither.com

echo ""
echo "📋 Nameservers (NS):"
dig +short NS voither.com

echo ""
echo "📋 Informações detalhadas do domínio:"
whois voither.com | grep -E "Registrar|Creation Date|Expiry Date|Name Server"

echo ""
echo "📋 Verificando subdomínios comuns:"
for subdomain in www api mail ftp admin blog app dev staging; do
    result=$(dig +short A $subdomain.voither.com)
    if [ ! -z "$result" ]; then
        echo "  $subdomain.voither.com -> $result"
    fi
done

echo ""
echo "📋 Verificando registros específicos do Azure:"
dig +short TXT _dmarc.voither.com
dig +short TXT voither.com | grep -i azure
dig +short TXT voither.com | grep -i microsoft

echo ""
echo "📋 Status do SSL/TLS:"
echo | openssl s_client -servername voither.com -connect voither.com:443 2>/dev/null | openssl x509 -noout -subject -issuer -dates

echo ""
echo "✅ Verificação DNS concluída!"
