using Ganss.XSS;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using System;
using System.Collections.Generic;
using System.Text;
using ULabs.BBCodeParser.Html;

namespace ULabs.BBCodeParser {
    public static class BBCodeParserServiceHelper {
        public static IServiceCollection AddBBCodeParser(this IServiceCollection services) {
            services.AddScoped<BBCodeHtmlMapper>();
            services.AddScoped<BBCodeToHtml>();

            // Letting .NET Core create the instance will end in an empty rule set (parameters doesn't seems empty then)
            services.AddSingleton<HtmlSanitizer>(x => new HtmlSanitizer());

            services.AddSingleton<RazorLightEngine>(x => {
                var engine = new RazorLightEngineBuilder()
                   // Ressource must be in the root of the project
                  .UseEmbeddedResourcesProject(typeof(BBCodeParserServiceHelper))
                  .UseMemoryCachingProvider()
                  .Build();
                return engine;
            });

            // Deprecated 
            // services.AddSingleton<BBCodeParserExperimental>();
            return services;
        }
    }
}
