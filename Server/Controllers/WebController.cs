﻿using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Controllers;

[Route("/web")]
[Subdomain("osu")]
public class WebController : ControllerBase
{
    [HttpPost(RequestType.OsuScreenshot)]
    public async Task<IActionResult> OsuScreenshot(
        [FromForm(Name = "u")] string username,
        [FromForm(Name = "p")] string passhash,
        [FromForm(Name = "ss")] IFormFile screenshot)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        if (await AssetService.SaveScreenshot(session,
                screenshot,
                HttpContext.RequestAborted) is var (resultUrl, error) && (error != null || resultUrl == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, session, error);
            return BadRequest(error);
        }

        return Ok(resultUrl);
    }

    [HttpGet(RequestType.OsuGetFriends)]
    public async Task<IActionResult> OsuGetFriends(
        [FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var friends = await BanchoService.GetFriends(session.User.Username);
        if (friends == null)
            return BadRequest("error: no");

        return Ok(friends);
    }

    [HttpPost(RequestType.OsuError)]
    public IActionResult OsuError()
    {
        return Ok();
    }

    [HttpGet(RequestType.LastFm)]
    public IActionResult LastFm(
        [FromQuery(Name = "us")] string username,
        [FromQuery(Name = "ha")] string passhash,
        [FromQuery(Name = "b")] string query)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        if (query[0] != 'a')
            return Ok("-3");

        var flags = (LastFmFlags)int.Parse(query[1..]);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if ((flags & (LastFmFlags.HqAssembly | LastFmFlags.HqFile)) != 0)
        {
            _ = database.UserService.Moderation.RestrictPlayer(session.User.Id, -1, "hq!osu found running");
            return Ok("-3");
        }

        if ((flags & LastFmFlags.RegistryEdits) != 0)
        {
            _ = database.UserService.Moderation.RestrictPlayer(session.User.Id, -1, "Osu multi account registry edits found");
            return Ok("-3");
        }

        return Ok();
    }



    [HttpPost(RequestType.OsuGetBeatmapInfo)]
    public IActionResult GetBeatmapInfo()
    {
        // This is a dummy response for a multiplayer matches.
        // By my understanding, client sends us list of Filenames and/or Ids to be parsed into a list of beatmaps.
        // This is probably done to ensure that downloaded beatmaps are the same for all players.
        // BUT - we can't really find beatmaps by their filenames (check PS to find out why), so we just return a dummy response.
        // With that we both satisfy the client and don't break anything. Even if map is not ranked, we will handle it properly on submission.
        // P.S: Due to sanitization of filename, artist/title/creator not always match their actual values.
        // If you found any problem with this solution, please create an issue for it. -richardscull

        return Ok(string.Join("\n", Enumerable.Range(0, 100).Select(i => $"{i}||||1|N|N|N|N")));
    }

    [HttpGet(RequestType.OsuMarkAsRead)]
    public IActionResult OsuMarkAsRead()
    {
        return Ok();
    }

    [HttpGet(RequestType.BanchoConnect)]
    public IActionResult BanchoConnect()
    {
        return Ok();
    }

    [HttpPost(RequestType.OsuSession)]
    public IActionResult OsuConnect()
    {
        return Ok();
    }

    [HttpGet(RequestType.CheckUpdates)]
    public IActionResult CheckUpdates()
    {
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return Redirect($"https://osu.ppy.sh/web/{RequestType.CheckUpdates}{queryString}");
    }

    [HttpGet(RequestType.OsuGetSeasonalBackground)]
    public IActionResult GetSeasonal()
    {
        var result = AssetService.GetSeasonalBackgrounds();
        return Ok(result);
    }

    [HttpGet(RequestType.OsuAddFavourite)]
    public async Task<IActionResult> AddFavourite([FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash,
        [FromQuery(Name = "a")] int beatmapSetId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapSetId);
        if (beatmapSet == null)
            return Ok("error: beatmap");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Favourites.AddFavouriteBeatmap(session.User.Id, beatmapSetId);

        return Ok();
    }

    [HttpGet(RequestType.OsuGetFavourites)]
    public async Task<IActionResult> AddFavourites([FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var favourites = await database.UserService.Favourites.GetUserFavouriteBeatmaps(session.User.Id);

        return Ok(string.Join("/n", favourites.Select(x => x)));
    }

    [HttpPost(RequestType.PostRegister)]
    public async Task<IActionResult> Register()
    {
        var result = await AuthService.Register(Request);
        return result is BadRequestObjectResult ? result : Ok(result);
    }

    [HttpGet("/wiki/en/Do_you_really_want_to_ask_peppy")]
    public async Task<IActionResult> AskPeppy()
    {
        var image = await AssetService.GetPeppyImage();
        if (image == null)
            return NotFound();

        return new FileContentResult(image, "image/jpeg");
    }

    [HttpGet("/home/account/edit")]
    public IActionResult EditAvatar()
    {
        return Redirect($"https://{Configuration.Domain}/settings");
    }

    [HttpGet("/u/{id:int}")]
    public IActionResult UserProfile(int id)
    {
        return Redirect($"https://{Configuration.Domain}/user/{id}");
    }

    [Route("/beatmapsets/{*path}")]
    [HttpGet]
    public IActionResult RedirectToSet(string path)
    {
        return Redirect($"https://{Configuration.Domain}/beatmapsets/{path}");
    }

    [Route("/beatmaps/{*path}")]
    [HttpGet]
    public IActionResult RedirectToMap(string path)
    {
        return Redirect($"https://{Configuration.Domain}/beatmaps/{path}");
    }
}