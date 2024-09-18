﻿using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Attributes;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[Subdomain("a", "assets")]
public class AssetsController : ControllerBase
{
    [HttpGet(RequestType.GetAvatar)]
    [HttpGet(RequestType.GetBanchoAvatar)]
    public async Task<IActionResult> GetAvatar(int id, [FromQuery(Name = "default")] bool? fallToDefault)
    {
        if (await AssetService.GetAvatar(id, fallToDefault ?? true) is var (avatar, error) && (error != null || avatar == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetAvatar, null, error);
            return NotFound();
        }

        return new FileContentResult(avatar, $"image/{ImageTools.GetImageType(avatar) ?? "png"}");
    }

    [HttpGet(RequestType.GetBanner)]
    public async Task<IActionResult> GetBanner(int id, [FromQuery(Name = "default")] bool? fallToDefault)
    {
        if (await AssetService.GetBanner(id, fallToDefault ?? true) is var (banner, error) && (error != null || banner == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetBanner, null, error);
            return NotFound();
        }

        return new FileContentResult(banner, $"image/{ImageTools.GetImageType(banner) ?? "png"}");
    }

    [HttpGet]
    [Route(RequestType.GetScreenshot)]
    public async Task<IActionResult> GetScreenshot(int id)
    {
        if (await AssetService.GetScreenshot(id) is var (screenshot, error) && (error != null || screenshot == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, null, error);
            return NotFound();
        }

        return new FileContentResult(screenshot, "image/jpeg");
    }

    [HttpGet(RequestType.MenuContent)]
    public IActionResult GetMenuContent()
    {
        return Ok(BanchoService.GetCurrentEventJson());
    }

    [HttpGet(RequestType.EventBanner)]
    public async Task<IActionResult> GetEventBanner()
    {
        var data = await AssetService.GetEventBanner();
        if (data == null)
            return NotFound();

        return new FileContentResult(data, "image/png");
    }
}