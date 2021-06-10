﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Steeltoe.NetCoreToolService.Services;
using Steeltoe.NetCoreToolService.Utils;

namespace Steeltoe.NetCoreToolService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewController : ControllerBase
    {
        private readonly IArchiverRegistry _archiverRegistry;

        private readonly ILogger<NewController> _logger;

        private static readonly string Dotnet = "dotnet";

        public NewController(IArchiverRegistry archiverRegistry, ILogger<NewController> logger)
        {
            _archiverRegistry = archiverRegistry;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult> GetTemplates()
        {
            return Ok(await GetTemplateList());
        }

        [HttpGet]
        [Route("{template}")]
        public async Task<ActionResult> GetTemplateProject(string template, string options)
        {
            var opts = options?.Split(',').Select(opt => opt.Trim()).ToList() ?? new List<string>();
            var pArgs = new List<string>() { "new", template };
            var name = opts.Find(opt => opt.StartsWith("output="))?.Split('=', 2)[1];
            if (name is null)
            {
                name = "Sample";
                pArgs.AddRange(new[] { "--output", name });
            }

            pArgs.AddRange(opts.Select(opt => $"--{opt}"));

            using var workDir = new TempDirectory();
            var pInfo = new ProcessStartInfo
            {
                Arguments = string.Join(' ', pArgs),
                WorkingDirectory = workDir.FullName,
            };

            var result = await ProcessToResultAsync(pInfo);
            var ok = result as ContentResult;
            if (ok is null)
            {
                return result;
            }

            if (!Directory.EnumerateFileSystemEntries(workDir.FullName).Any())
            {
                return NotFound($"template {template} does not exist");
            }


            var archivalType = "zip";
            var archiver = _archiverRegistry.Lookup(archivalType);
            if (archiver is null)
            {
                return NotFound($"Packaging '{archivalType}' not found.");
            }

            var archiveBytes = archiver.ToBytes(workDir.FullName);
            return File(archiveBytes,archiver.MimeType, $"{name}{archiver.FileExtension}");
        }

        [HttpGet]
        [Route("{template}/help")]
        public async Task<ActionResult> GetTemplateHelp(string template)
        {
            var pInfo = new ProcessStartInfo
            {
                ArgumentList = { "new", template, "--help" },
            };

            return await ProcessToResultAsync(pInfo);
        }

        [HttpPost]
        public async Task<ActionResult> InstallTemplate(string nuGetId)
        {
            if (nuGetId is null)
            {
                return BadRequest("missing NuGet ID");
            }

            var preInstallTemplates = await GetTemplateList();

            var pInfo = new ProcessStartInfo
            {
                ArgumentList = { "new", "--install", nuGetId },
            };
            await ProcessToStringAsync(pInfo);

            var postInstallTemplates = await GetTemplateList();

            foreach (var template in preInstallTemplates.Keys)
            {
                postInstallTemplates.Remove(template);
            }

            return Ok(postInstallTemplates);
        }

        private async Task<Dictionary<string, TemplateInfo>> GetTemplateList()
        {
            var pInfo = new ProcessStartInfo
            {
                ArgumentList = { "new", "--list" },
            };
            var listing = await ProcessToStringAsync(pInfo);
            var lines = listing.Split('\n').ToList().FindAll(line => !string.IsNullOrWhiteSpace(line));
            var headingIdx = lines.FindIndex(line => line.StartsWith("-"));
            var headings = lines[headingIdx].Split("  ");
            var nameColLength = headings[0].Length;
            var shortNameColStart = nameColLength + 2;
            var shortNameColLength = headings[1].Length;
            var languageColStart = shortNameColStart + shortNameColLength + 2;
            var languageColLength = headings[2].Length;
            var tagsColStart = languageColStart + languageColLength + 2;
            var tagsColLength = headings[3].Length;
            lines = lines.GetRange(headingIdx + 1, lines.Count - headingIdx - 1);

            var dict = new Dictionary<string, TemplateInfo>();
            foreach (var line in lines)
            {
                var templateInfo = new TemplateInfo();
                var template = line.Substring(shortNameColStart, shortNameColLength).Trim();
                templateInfo.Name = line.Substring(0, nameColLength).Trim();
                templateInfo.Languages = line.Substring(languageColStart, languageColLength).Trim();
                templateInfo.Tags = line.Substring(tagsColStart, tagsColLength).Trim();
                dict.Add(template, templateInfo);
            }

            return dict;
        }

        private async Task<string> ProcessToStringAsync(ProcessStartInfo processStartInfo)
        {
            processStartInfo.FileName = Dotnet;
            TempDirectory workDir = null;
            if (string.IsNullOrEmpty(processStartInfo.WorkingDirectory))
            {
                workDir = new TempDirectory();
                processStartInfo.WorkingDirectory = workDir.FullName;
            }

            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            var guid = Path.GetFileName(processStartInfo.WorkingDirectory) ?? "unknown";
            _logger.LogInformation("{Guid}: {Command} {Args}", guid, processStartInfo.FileName,
                processStartInfo.Arguments);
            var proc = Process.Start(processStartInfo);
            if (proc is null)
            {
                throw new ActionResultException(StatusCode(StatusCodes.Status503ServiceUnavailable));
            }

            await proc.WaitForExitAsync();
            workDir?.Dispose();
            if (proc.ExitCode == 0)
            {
                var output = await proc.StandardOutput.ReadToEndAsync();
                _logger.LogInformation("{Guid}>\n{Output}", guid, output);
                return output;
            }

            var error = await proc.StandardError.ReadToEndAsync();
            _logger.LogInformation("{Guid}: {Error}", guid, error);
            throw new ActionResultException(NotFound(error));
        }

        private async Task<ActionResult> ProcessToResultAsync(ProcessStartInfo processStartInfo)
        {
            try
            {
                return Content(await ProcessToStringAsync(processStartInfo));
            }
            catch (ActionResultException e)
            {
                return e.ActionResult;
            }
        }
    }

    class TemplateInfo
    {
        public string Name { get; set; }

        public string Languages { get; set; }

        public string Tags { get; set; }

        public override string ToString()
        {
            return $"[name={Name},languages={Languages},tags={Tags}";
        }
    }

    class ActionResultException : Exception
    {
        internal ActionResult ActionResult { get; }

        internal ActionResultException(ActionResult actionResult)
        {
            ActionResult = actionResult;
        }
    }
}
