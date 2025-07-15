using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MedicalScribeR.Core.Models.MongoDB;

/// <summary>
/// Base document class for MongoDB collections
/// </summary>
public abstract class BaseMongoDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("version")]
    public int Version { get; set; } = 1;
}

/// <summary>
/// MongoDB document for storing transcription sessions
/// </summary>
public class TranscriptionDocument : BaseMongoDocument
{
    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("chunks")]
    public List<TranscriptionChunkDocument> Chunks { get; set; } = new();
    
    [BsonElement("metadata")]
    public TranscriptionMetadata Metadata { get; set; } = new();
    
    [BsonElement("status")]
    public string Status { get; set; } = "processing"; // processing, completed, failed
    
    [BsonElement("totalDuration")]
    public TimeSpan TotalDuration { get; set; }
    
    [BsonElement("language")]
    public string Language { get; set; } = "pt-BR";
}

/// <summary>
/// Individual transcription chunk within a session
/// </summary>
public class TranscriptionChunkDocument
{
    [BsonElement("chunkId")]
    public string ChunkId { get; set; } = string.Empty;
    
    [BsonElement("sequence")]
    public int Sequence { get; set; }
    
    [BsonElement("startTime")]
    public TimeSpan StartTime { get; set; }
    
    [BsonElement("endTime")]
    public TimeSpan EndTime { get; set; }
    
    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;
    
    [BsonElement("confidence")]
    public double Confidence { get; set; }
    
    [BsonElement("speaker")]
    public string? Speaker { get; set; }
}

/// <summary>
/// Medical entities extracted from transcriptions
/// </summary>
public class MedicalEntityDocument : BaseMongoDocument
{
    [BsonElement("transcriptionId")]
    public ObjectId TranscriptionId { get; set; }
    
    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [BsonElement("entities")]
    public List<HealthcareEntityDocument> Entities { get; set; } = new();
    
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty; // medication, diagnosis, procedure, etc.
    
    [BsonElement("confidence")]
    public double Confidence { get; set; }
    
    [BsonElement("extractedBy")]
    public string ExtractedBy { get; set; } = "azure-text-analytics";
}

/// <summary>
/// Individual healthcare entity
/// </summary>
public class HealthcareEntityDocument
{
    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;
    
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;
    
    [BsonElement("subcategory")]
    public string? Subcategory { get; set; }
    
    [BsonElement("confidence")]
    public double Confidence { get; set; }
    
    [BsonElement("offset")]
    public int Offset { get; set; }
    
    [BsonElement("length")]
    public int Length { get; set; }
    
    [BsonElement("links")]
    public List<EntityLinkDocument> Links { get; set; } = new();
}

/// <summary>
/// Links to external medical databases
/// </summary>
public class EntityLinkDocument
{
    [BsonElement("dataSource")]
    public string DataSource { get; set; } = string.Empty;
    
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("url")]
    public string? Url { get; set; }
}

/// <summary>
/// Generated documents (PDFs, reports, etc.)
/// </summary>
public class GeneratedDocumentDocument : BaseMongoDocument
{
    [BsonElement("transcriptionId")]
    public ObjectId TranscriptionId { get; set; }
    
    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // prescription, summary, action_items
    
    [BsonElement("format")]
    public string Format { get; set; } = "pdf"; // pdf, docx, html
    
    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;
    
    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    [BsonElement("fileSize")]
    public long FileSize { get; set; }
    
    [BsonElement("filePath")]
    public string? FilePath { get; set; }
    
    [BsonElement("generatedBy")]
    public string GeneratedBy { get; set; } = string.Empty; // agent name
}

/// <summary>
/// Audit logs for compliance
/// </summary>
public class AuditLogDocument : BaseMongoDocument
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty; // create, read, update, delete, export
    
    [BsonElement("resource")]
    public string Resource { get; set; } = string.Empty; // transcription, document, entity
    
    [BsonElement("resourceId")]
    public string ResourceId { get; set; } = string.Empty;
    
    [BsonElement("details")]
    public Dictionary<string, object> Details { get; set; } = new();
    
    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }
    
    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
    
    [BsonElement("sessionId")]
    public string? SessionId { get; set; }
}
