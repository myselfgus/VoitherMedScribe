using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models.MongoDB;

namespace MedicalScribeR.Infrastructure.Repositories.MongoDB;

/// <summary>
/// Base MongoDB repository implementation
/// </summary>
/// <typeparam name="T">Document type that inherits from BaseMongoDocument</typeparam>
public class MongoRepository<T> : IMongoRepository<T> where T : BaseMongoDocument
{
    protected readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<T>(collectionName);
        
        // Create indexes if needed
        CreateIndexes();
    }

    protected virtual void CreateIndexes()
    {
        // Create common indexes
        var indexKeysDefinition = Builders<T>.IndexKeys
            .Ascending(x => x.CreatedAt)
            .Ascending(x => x.UpdatedAt);
            
        var indexModel = new CreateIndexModel<T>(indexKeysDefinition);
        _collection.Indexes.CreateOneAsync(indexModel);
    }

    public virtual async Task<T?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(ObjectId id, T entity, CancellationToken cancellationToken = default)
    {
        entity.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Version++;
        
        var result = await _collection.ReplaceOneAsync(x => x.Id == id, entity, cancellationToken: cancellationToken);
        
        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException($"Document with id {id} not found");
        }
        
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(predicate).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate == null)
        {
            return await _collection.CountDocumentsAsync(FilterDefinition<T>.Empty, cancellationToken: cancellationToken);
        }
        
        return await _collection.CountDocumentsAsync(predicate, cancellationToken: cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var count = await _collection.CountDocumentsAsync(predicate, new CountOptions { Limit = 1 }, cancellationToken);
        return count > 0;
    }

    public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;
        
        if (predicate == null)
        {
            return await _collection.Find(_ => true)
                .Skip(skip)
                .Limit(pageSize)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        
        return await _collection.Find(predicate)
            .Skip(skip)
            .Limit(pageSize)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entitiesList = entities.ToList();
        var now = DateTime.UtcNow;
        
        foreach (var entity in entitiesList)
        {
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
        }
        
        await _collection.InsertManyAsync(entitiesList, cancellationToken: cancellationToken);
        return entitiesList;
    }

    public virtual async Task<long> UpdateManyAsync(Expression<Func<T, bool>> predicate, IDictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        var updateDefinitionBuilder = Builders<T>.Update;
        var updateDefinitions = new List<UpdateDefinition<T>>();
        
        foreach (var update in updates)
        {
            updateDefinitions.Add(updateDefinitionBuilder.Set(update.Key, update.Value));
        }
        
        updateDefinitions.Add(updateDefinitionBuilder.Set(x => x.UpdatedAt, DateTime.UtcNow));
        updateDefinitions.Add(updateDefinitionBuilder.Inc(x => x.Version, 1));
        
        var combinedUpdate = updateDefinitionBuilder.Combine(updateDefinitions);
        var result = await _collection.UpdateManyAsync(predicate, combinedUpdate, cancellationToken: cancellationToken);
        
        return result.ModifiedCount;
    }

    public virtual async Task<long> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteManyAsync(predicate, cancellationToken);
        return result.DeletedCount;
    }
}
