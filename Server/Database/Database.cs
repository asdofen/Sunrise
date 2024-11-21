﻿using DatabaseWrapper.Core;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Database.Services;
using Sunrise.Server.Database.Services.Beatmap;
using Sunrise.Server.Database.Services.Score;
using Sunrise.Server.Database.Services.User;
using Sunrise.Server.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database;

public sealed class DatabaseManager
{
    private const string DataPath = Configuration.DataPath;
    private const string Database = Configuration.DatabaseName;

    private readonly ILogger<DatabaseManager> _logger;
    private readonly WatsonORM _orm = new(new DatabaseSettings(DataPath + Database));
    public readonly BeatmapService BeatmapService;
    public readonly LoggerService LoggerService;
    public readonly MedalService MedalService;

    public readonly ScoreService ScoreService;

    public readonly UserService UserService;

    public DatabaseManager(RedisRepository redis)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<DatabaseManager>();

        Redis = redis;
        
        UserService = new UserService(this, Redis, _orm);
        BeatmapService = new BeatmapService(this,Redis, _orm);
        ScoreService = new ScoreService(this,Redis, _orm);
        MedalService = new MedalService(this,Redis, _orm);
        LoggerService = new LoggerService(this,Redis, _orm);

        _orm.InitializeDatabase();
        CheckMigrations();
    }

    public RedisRepository Redis { get; } // TODO: Make private

    public async Task InitializeBotInDatabase()
    {
        var isBotInitialized = await UserService.GetUser(username: Configuration.BotUsername, useCache: false);
        if (isBotInitialized != null) return;

        var bot = new User
        {
            Username = Configuration.BotUsername,
            Country = (short)CountryCodes.AQ, 
            Privilege = UserPrivileges.User,
            RegisterDate = DateTime.Now,
            Passhash = "12345678",
            Email = "bot@mail.com",
            IsRestricted = true // Bot is restricted by default to prevent users from logging in as it
        };

        bot = await UserService.InsertUser(bot);
        if (bot == null) throw new Exception("Failed to insert bot into the database");

        var botAvatar = await File.ReadAllBytesAsync($"{DataPath}Files/Assets/BotAvatar.png");
        await UserService.Files.SetAvatar(bot.Id, botAvatar);
    }

    private void CheckMigrations()
    {
        _orm.InitializeTable(typeof(Migration));

        var migrationManager = new MigrationManager(_orm);
        var appliedMigrations = migrationManager.ApplyMigrations($"{Configuration.DataPath}Migrations");

        _orm.InitializeTables([
            typeof(User), typeof(UserStats), typeof(UserFile), typeof(Restriction), typeof(BeatmapFile), typeof(Score),
            typeof(Medal), typeof(MedalFile), typeof(UserMedals), typeof(UserStatsSnapshot), typeof(LoginEvent),
            typeof(UserFavouriteBeatmap)
        ]);

        if (appliedMigrations <= 0 && !Configuration.ClearCacheOnStartup) return;

        _logger.LogInformation($"Applied {appliedMigrations} migrations");
        _logger.LogWarning("Cache will be flushed due to database changes. This may cause performance issues.");

        Redis.FlushAllCache();
        _logger.LogInformation("Cache flushed. Rebuilding user ranks...");

        for (var i = 0; i < 4; i++)
        {
            UserService.Stats.SetAllUserRanks((GameMode)i).Wait();
        }

        _logger.LogInformation("User ranks rebuilt. Cache is now up to date.");
    }
}