# 🍃 MongoDB Atlas Integration - Medical Scriber

## 📋 **Status Atual**

### ✅ **MongoDB Atlas Provisionado**
- **Organization**: Voither (`6875f67eead18427cce5fb54`)
- **Plan**: Pay as You Go (Azure Native)
- **Location**: East US 2
- **Integration**: Azure Marketplace
- **Status**: ✅ Provisionado e ativo

### 🎯 **Uso Recomendado para Medical Scriber**

#### **1. Estrutura de Dados Sugerida:**
```javascript
// Collections recomendadas:
{
  "transcriptions": {
    // Transcrições de áudio completas
    "sessionId": "uuid",
    "chunks": [...], // Array de chunks processados
    "metadata": {...},
    "timestamp": "ISO Date"
  },
  
  "medical_entities": {
    // Entidades médicas extraídas
    "transcriptionId": "ref",
    "entities": [...],
    "confidence": 0.95,
    "category": "medication|diagnosis|procedure"
  },
  
  "generated_documents": {
    // Documentos gerados (PDFs, relatórios)
    "transcriptionId": "ref",
    "type": "prescription|summary|action_items",
    "content": {...},
    "format": "pdf|docx|html"
  },
  
  "audit_logs": {
    // Logs de auditoria para compliance
    "userId": "ref",
    "action": "create|read|update|delete",
    "resource": "transcription|document",
    "timestamp": "ISO Date",
    "metadata": {...}
  }
}
```

#### **2. Vantagens do MongoDB para Medical Scriber:**
- ✅ **Flexibilidade**: Schema dinâmico para diferentes tipos de dados médicos
- ✅ **Performance**: Queries rápidas para busca de entidades
- ✅ **Escalabilidade**: Horizontal scaling para grande volume
- ✅ **JSON Native**: Perfeito para APIs REST
- ✅ **Compliance**: Encryption at rest + HIPAA ready
- ✅ **Azure Integration**: Nativo no Azure

## 🔧 **Integração Técnica (.NET)**

### **1. Packages NuGet Necessários:**
```xml
<PackageReference Include="MongoDB.Driver" Version="2.28.0" />
<PackageReference Include="MongoDB.Driver.GridFS" Version="2.28.0" />
<PackageReference Include="MongoDB.Bson" Version="2.28.0" />
```

### **2. Models de Exemplo:**
```csharp
// MedicalScribeR.Core/Models/MongoDB/
public class TranscriptionDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("sessionId")]
    public string SessionId { get; set; }
    
    [BsonElement("chunks")]
    public List<TranscriptionChunk> Chunks { get; set; }
    
    [BsonElement("metadata")]
    public TranscriptionMetadata Metadata { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class MedicalEntityDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("transcriptionId")]
    public ObjectId TranscriptionId { get; set; }
    
    [BsonElement("entities")]
    public List<HealthcareEntity> Entities { get; set; }
    
    [BsonElement("confidence")]
    public double Confidence { get; set; }
    
    [BsonElement("category")]
    public string Category { get; set; }
}
```

### **3. Repository Pattern:**
```csharp
// Interface
public interface IMongoRepository<T>
{
    Task<T> GetByIdAsync(ObjectId id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> CreateAsync(T entity);
    Task UpdateAsync(ObjectId id, T entity);
    Task DeleteAsync(ObjectId id);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
}

// Implementation base
public class MongoRepository<T> : IMongoRepository<T>
{
    private readonly IMongoCollection<T> _collection;
    
    public MongoRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<T>(collectionName);
    }
    
    // Implementation methods...
}
```

## ⚙️ **Configuration Setup**

### **1. Connection String (Key Vault):**
```bash
# Adicionar no Azure Key Vault
az keyvault secret set --vault-name MedScriber --name "MongoDB-ConnectionString" --value "mongodb+srv://..."
```

### **2. appsettings.json:**
```json
{
  "MongoDB": {
    "ConnectionString": "#{MongoDB-ConnectionString}#",
    "DatabaseName": "medicalscriber",
    "Collections": {
      "Transcriptions": "transcriptions",
      "MedicalEntities": "medical_entities",
      "GeneratedDocuments": "generated_documents",
      "AuditLogs": "audit_logs"
    }
  }
}
```

### **3. DI Configuration:**
```csharp
// Program.cs
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<IMongoClient>(provider =>
{
    var settings = provider.GetService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddScoped(provider =>
{
    var client = provider.GetService<IMongoClient>();
    var settings = provider.GetService<IOptions<MongoDBSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName);
});
```

## 🎯 **Casos de Uso Específicos**

### **1. Armazenamento de Transcrições:**
- Chunks de áudio processados em tempo real
- Metadata de sessões médicas
- Backup de dados para compliance

### **2. Cache de Entidades Médicas:**
- Resultados do Azure Text Analytics
- Classificações de IA processadas
- Histórico de análises

### **3. Documentos Gerados:**
- PDFs de prescrições
- Relatórios médicos
- Action items extraídos

### **4. Analytics e Relatórios:**
- Agregações de dados médicos
- Dashboards de performance
- Compliance reports

## 🚀 **Próximos Passos**

### **Quando implementar:**
1. ✅ **Agora**: Adicionar packages e configuração base
2. 🔄 **Fase 1**: Implementar para transcriptions storage
3. 🔄 **Fase 2**: Migrar entities e documents
4. 🔄 **Fase 3**: Analytics e compliance

### **Comandos para começar:**
```bash
# 1. Adicionar packages
dotnet add MedicalScribeR.Infrastructure package MongoDB.Driver

# 2. Obter connection string do Atlas
# (Via Azure Portal ou MongoDB Atlas Dashboard)

# 3. Configurar no Key Vault
az keyvault secret set --vault-name MedScriber --name "MongoDB-ConnectionString" --value "sua_connection_string"

# 4. Implementar models e repositories
```

## 💡 **Recomendação**

**VALE A PENA SIM!** 🎯

- **SQL Server**: Para dados relacionais e transacionais
- **MongoDB**: Para dados não-estruturados e analytics
- **Redis**: Para cache e sessions
- **Blob Storage**: Para arquivos grandes

**Arquitetura híbrida** é perfeita para Medical Scriber!
