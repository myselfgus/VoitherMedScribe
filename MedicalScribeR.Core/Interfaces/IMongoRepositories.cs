using MongoDB.Bson;
using System.Linq.Expressions;
using MedicalScribeR.Core.Models.MongoDB;

namespace MedicalScribeR.Core.Interfaces;

/// <summary>
/// Generic MongoDB repository interface
/// </summary>
/// <typeparam name="T">Document type that inherits from BaseMongoDocument</typeparam>
public interface IMongoRepository<T> where T : BaseMongoDocument
{
    // Basic CRUD operations
    Task<T?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default);
    Task<T> UpdateAsync(ObjectId id, T entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default);
    
    // Query operations
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    // Pagination
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    
    // Bulk operations
    Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<long> UpdateManyAsync(Expression<Func<T, bool>> predicate, IDictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<long> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized repository for transcription documents
/// </summary>
public interface ITranscriptionRepository : IMongoRepository<TranscriptionDocument>
{
    Task<IEnumerable<TranscriptionDocument>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<TranscriptionDocument?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TranscriptionDocument>> GetRecentAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<TranscriptionDocument>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IEnumerable<TranscriptionDocument>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized repository for medical entities
/// </summary>
public interface IMedicalEntityRepository : IMongoRepository<MedicalEntityDocument>
{
    Task<IEnumerable<MedicalEntityDocument>> GetByTranscriptionIdAsync(ObjectId transcriptionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalEntityDocument>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalEntityDocument>> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetEntityCategoriesStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized repository for generated documents
/// </summary>
public interface IGeneratedDocumentRepository : IMongoRepository<GeneratedDocumentDocument>
{
    Task<IEnumerable<GeneratedDocumentDocument>> GetByTranscriptionIdAsync(ObjectId transcriptionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GeneratedDocumentDocument>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IEnumerable<GeneratedDocumentDocument>> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<long> GetTotalFileSizeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized repository for audit logs
/// </summary>
public interface IAuditLogRepository : IMongoRepository<AuditLogDocument>
{
    Task<IEnumerable<AuditLogDocument>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogDocument>> GetByActionAsync(string action, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogDocument>> GetByResourceAsync(string resource, string resourceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogDocument>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetActionStatsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    
    // Compliance methods
    Task CreateAuditEntryAsync(string userId, string action, string resource, string resourceId, Dictionary<string, object>? details = null, CancellationToken cancellationToken = default);
}
