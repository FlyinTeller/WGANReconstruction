﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.AspNetCore.Mvc;

namespace Warp
{

    [Route("Warp")]
    public class WebAPIController : Controller
    {
        [HttpGet]
        [Route("GetSettingsGeneral")]
        public IActionResult GetSettingsGeneral()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsImport")]
        public IActionResult GetSettingsImport()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Import, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsCTF")]
        public IActionResult GetSettingsCTF()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.CTF, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsMovement")]
        public IActionResult GetSettingsMovement()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Movement, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsGrids")]
        public IActionResult GetSettingsGrids()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Grids, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsPicking")]
        public IActionResult GetSettingsPicking()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Picking, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsExport")]
        public IActionResult GetSettingsExport()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Export, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsTomo")]
        public IActionResult GetSettingsTomo()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Tomo, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsFilter")]
        public IActionResult GetSettingsFilter()
        {
            return Ok(JsonConvert.SerializeObject(MainWindow.Options.Filter, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetSettingsAll")]
        public IActionResult GetSettingsAll()
        {
            return Ok(JsonConvert.SerializeObject(new Dictionary<string, object>()
                                                  {
                                                      {"General", MainWindow.Options },
                                                      {"Import", MainWindow.Options.Import },
                                                      {"CTF", MainWindow.Options.CTF },
                                                      {"Movement", MainWindow.Options.Movement },
                                                      {"Grids", MainWindow.Options.Grids },
                                                      {"Picking", MainWindow.Options.Picking },
                                                      {"Export", MainWindow.Options.Export },
                                                      {"Tomo", MainWindow.Options.Tomo },
                                                      {"Filter", MainWindow.Options.Filter },
                                                  }, Formatting.Indented));
        }

        [HttpGet]
        [Route("GetProcessingStatus")]
        public IActionResult GetProcessingStatus()
        {
            return Ok(MainWindow.IsPreprocessing ? "processing" :
                      (MainWindow.IsStoppingPreprocessing ? "stopping" :
                                                            "stopped"));
        }

        [HttpPost]
        [Route("StartProcessing")]
        public IActionResult StartProcessing()
        {
            Application.Current.Dispatcher.Invoke(() => ((MainWindow)Application.Current.MainWindow).StartProcessing());
            return Ok("success");
        }

        [HttpPost]
        [Route("StopProcessing")]
        public IActionResult StopProcessing()
        {
            Application.Current.Dispatcher.Invoke(() => ((MainWindow)Application.Current.MainWindow).StopProcessing());
            return Ok("success");
        }

        [HttpPost]
        [Route("LoadSettings")]
        public IActionResult LoadSettings()
        {
            IActionResult Result = null;

            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    string Received = "";
                    using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {
                        Received = await reader.ReadToEndAsync();
                    }

                    dynamic RequestJson = JsonConvert.DeserializeObject(Received);

                    string Path = RequestJson["path"];

                    if (System.IO.File.Exists(Path))
                    {
                        if (MainWindow.IsPreprocessing || MainWindow.IsStoppingPreprocessing)
                            throw new Exception("Can't change settings while processing.");

                        MainWindow.OptionsLookForFolderOptions = false;

                        MainWindow.Options.Load(Path);

                        MainWindow.OptionsLookForFolderOptions = true;

                        Result = Ok("success");
                    }
                    else
                    {
                        throw new Exception("File not found.");
                    }
                }
                catch (Exception exc)
                {
                    Result = Problem("fail: " + exc.ToString());
                }
            });

            return Result;
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(option => option.EnableEndpointRouting = false);
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseMvc();
        }
    }
}
