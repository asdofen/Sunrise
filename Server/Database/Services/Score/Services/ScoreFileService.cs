using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Storages;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.Score.Services;

public class ScoreFileService
{
    private const string DataPath = Configuration.DataPath;

    private readonly WatsonORM _database;

    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public ScoreFileService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<ScoreFileService>();

        _database = database;
        _redis = redis;
    }


    // TODO: Rename to insert?
    public async Task<UserFile> UploadReplay(int userId, IFormFile replay)
    {
        var fileName = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.osr";
        var filePath = $"{DataPath}Files/Replays/{fileName}";

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
        await replay.CopyToAsync(stream);
        stream.Close();

        var record = new UserFile
        {
            OwnerId = userId,
            Path = filePath,
            Type = FileType.Replay
        };

        record = await _database.InsertAsync(record);
        await _redis.Set(RedisKey.ReplayRecord(record.Id), record);
        return record;
    }

    public async Task<byte[]?> GetReplay(int replayId)
    {
        var cachedRecord = await _redis.Get<UserFile>(RedisKey.ReplayRecord(replayId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, replayId);
        var record = await _database.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
            return null;

        file = await LocalStorage.ReadFileAsync(record.Path);
        if (file == null)
            return null;

        await _redis.Set(RedisKey.ReplayRecord(replayId), record);

        return file;
    }
}