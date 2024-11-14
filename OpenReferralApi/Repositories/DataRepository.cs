using FluentResults;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenReferralApi.Models;
using OpenReferralApi.Repositories.Interfaces;

namespace OpenReferralApi.Repositories;

public class DataRepository : IDataRepository
{
    private readonly IMongoDatabase _mongoDatabase;
    
    public DataRepository(IOptions<DatabaseSettings> databaseSettings)
    {
        var mongoClient = new MongoClient(
            databaseSettings.Value.ConnectionString);

        _mongoDatabase = mongoClient.GetDatabase(
            databaseSettings.Value.DatabaseName);
    }
    
    public async Task<Result<List<ServiceData>>> GetServices()
    {
        var collection = _mongoDatabase.GetCollection<ServiceData>("services");
        var services = await collection.Find(_ => true).ToListAsync();
        
        return services;
    }
    
    public async Task<Result<ServiceData>> GetServiceById(string id)
    {
        var collection = _mongoDatabase.GetCollection<ServiceData>("services");
        var services = await collection.Find(s => s.Id == id).FirstOrDefaultAsync();
        
        return services;
    }

    public async Task<Result<List<Field>>> GetColumns()
    {
        var collection = _mongoDatabase.GetCollection<Field>("columns");
        var columns = await collection.Find(_ => true).ToListAsync();
        
        return columns;
    }

    public async Task<Result<List<View>>> GetViews()
    {
        var collection = _mongoDatabase.GetCollection<View>("views");
        var services = await collection.Find(_ => true).ToListAsync();
        
        return services;
    }
}