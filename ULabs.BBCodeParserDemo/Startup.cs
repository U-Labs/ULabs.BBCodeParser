using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using ULabs.BBCodeParser;
using ULabs.BBCodeParser.Html;

namespace ULabs.BBCodeParserDemo {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            // Customization: Add an attachment BBCode that could be parsed by e.g. building a link
            var customBBCodes = new List<BBCode>();
            var attachments = new BBCode("[attach]", (node) => {
                return $"<b>Found Attachment with ID = {node.InnerContent}";
            });
            customBBCodes.Add(attachments);
            Func<IServiceProvider, BBCodeHtmlMapper> bbCodeHtmlMapperFunc = (sp) => new BBCodeHtmlMapper(sp.GetRequiredService<RazorLightEngine>(), customBBCodes);
            // If you don't want to use any customization, just call AddBBCodeParser without any arguments and it will use the default BBCodes 
            services.AddBBCodeParser(bbCodeHtmlMapperFunc);
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            } else {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
            });
        }
    }
}
