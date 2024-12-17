using FluentResults;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenReferralApi.Models;
using OpenReferralApi.Repositories.Interfaces;
using Success = OpenReferralApi.Models.Success;

namespace OpenReferralApi.Repositories;

public class DataRepository : IDataRepository
{
    private readonly IMongoDatabase _mongoDatabase;
    private readonly DatabaseSettings _databaseSettings;
    
    public DataRepository(IOptions<DatabaseSettings> databaseSettings)
    {
        _databaseSettings = databaseSettings.Value;
        var mongoClient = new MongoClient(_databaseSettings.ConnectionString);
        _mongoDatabase = mongoClient.GetDatabase(_databaseSettings.DatabaseName);
    }
    
    public async Task<Result<List<ServiceData>>> GetServices()
    {
        var collection = _mongoDatabase.GetCollection<ServiceData>(_databaseSettings.ServicesCollection);
        var services = await collection.Find(_ => true).ToListAsync();
        
        return services;
    }
    
    public async Task<Result<ServiceData>> GetServiceById(string id)
    {
        var collection = _mongoDatabase.GetCollection<ServiceData>(_databaseSettings.ServicesCollection);
        var services = await collection.Find(s => s.Id == id).FirstOrDefaultAsync();
        
        return services;
    }

    public async Task<Result<List<Field>>> GetColumns()
    {
        var collection = _mongoDatabase.GetCollection<Field>(_databaseSettings.ColumnsCollection);
        var columns = await collection.Find(_ => true).ToListAsync();
        
        return columns;
    }

    public async Task<Result<List<View>>> GetViews()
    {
        var collection = _mongoDatabase.GetCollection<View>(_databaseSettings.ViewsCollection);
        var services = await collection.Find(_ => true).ToListAsync();
        
        return services;
    }

    public async Task<Result> UpdateServiceTestStatus(string id, Success apiStatus, Success testStatus)
    {
        var testDate = DateTime.Now;
        
        var collection = _mongoDatabase.GetCollection<ServiceData>(_databaseSettings.ServicesCollection);
        
        var filter = Builders<ServiceData>.Filter
            .Eq(s => s.Id, id);
        
        var update = Builders<ServiceData>.Update
            .Set(s => s.LastTested!.Value, testDate)
            .Set(s => s.TestDate!.Value, testDate)
            .Set(s => s.StatusIsUp!.Value, apiStatus)
            .Set(s => s.StatusOverall!.Value, testStatus)
            .Set(s => s.StatusIsValid!.Value, testStatus);

        var output = await collection.UpdateManyAsync(filter, update);

        return output.IsAcknowledged 
            ? Result.Ok()
            : Result.Fail("Update was not acknowledged");
    }
}