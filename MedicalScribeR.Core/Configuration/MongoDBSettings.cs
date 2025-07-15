namespace MedicalScribeR.Core.Configuration;

/// <summary>
/// MongoDB configuration settings
/// </summary>
public class MongoDBSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "medicalscriber";
    public MongoCollections Collections { get; set; } = new();
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ServerSelectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// MongoDB collection names configuration
/// </summary>
public class MongoCollections
{
    public string Transcriptions { get; set; } = "transcriptions";
    public string MedicalEntities { get; set; } = "medical_entities";
    public string GeneratedDocuments { get; set; } = "generated_documents";
    public string AuditLogs { get; set; } = "audit_logs";
}
