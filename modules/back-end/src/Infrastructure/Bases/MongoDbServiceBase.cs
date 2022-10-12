using System.Linq.Expressions;
using Application.Bases.Exceptions;
using Domain.Bases;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infrastructure.Bases;

public class MongoDbServiceBase<TEntity> where TEntity : Entity
{
    public MongoDbClient MongoDb { get; }

    public IMongoCollection<TEntity> Collection { get; }

    public IMongoQueryable<TEntity> Queryable { get; }

    public MongoDbServiceBase(MongoDbClient mongoDb)
    {
        MongoDb = mongoDb;
        Collection = MongoDb.CollectionOf<TEntity>();
        Queryable = MongoDb.QueryableOf<TEntity>();
    }

    public async Task<TEntity> GetAsync(Guid id)
    {
        var entity = await FindOneAsync(x => x.Id == id);
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity).Name, id.ToString());
        }

        return entity;
    }

    public async Task<TEntity?> FindOneAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await Queryable.FirstOrDefaultAsync(predicate);
    }

    public async Task<ICollection<TEntity>> FindManyAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await Queryable.Where(predicate).ToListAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await Queryable.AnyAsync(predicate);
    }

    public async Task AddAsync(TEntity entity)
    {
        await Collection.InsertOneAsync(entity);
    }

    public async Task UpdateAsync(TEntity replacement)
    {
        await Collection.ReplaceOneAsync(entity => entity.Id == replacement.Id, replacement);
    }
}