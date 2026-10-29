/*
 ************************************************************************
 * Copyright (C) 2026 Clarivex Technologies Private Limited
 * All Rights Reserved.
 ************************************************************************
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Pervaxis.Vpa.Vending.Controllers;

/// <summary>
/// HORIZONTAL: Scaffold metadata endpoint.
/// Returns information about the Forge engine version, modules, and print timestamp.
/// </summary>
[ApiController]
[Route("meta")]
[AllowAnonymous]
public sealed class MetaController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public MetaController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetMeta()
    {
        // .forge-print.json lives at solution root (3 levels up from src/Project/)
        var contentRoot = _env.ContentRootPath;
        var solutionRoot = Path.GetFullPath(Path.Combine(contentRoot, "..", ".."));
        var printFile = Path.Combine(solutionRoot, ".forge-print.json");

        if (!System.IO.File.Exists(printFile))
            return Ok(new { error = "Scaffold metadata not found", searchedPath = printFile });

        var json = System.IO.File.ReadAllText(printFile);
        return Content(json, "application/json");
    }
}
