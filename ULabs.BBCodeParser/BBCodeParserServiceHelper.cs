using Ganss.XSS;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using System;
using System.Collections.Generic;
using System.Text;
using ULabs.BBCodeParser.Html;
using ULabs.BBCodeParser.Tools;

namespace ULabs.BBCodeParser {
    public static class BBCodeParserServiceHelper {
        /// <summary>
        /// Add all required dependencies for the BBCode parser
        /// </summary>
        /// <param name="services"></param>
        /// <param name="bbCodeHtmlMapperInstance">Providing a mapper function allows to </param>
        /// <returns></returns>
        public static IServiceCollection AddBBCodeParser(this IServiceCollection services, Func<IServiceProvider, BBCodeHtmlMapper> bbCodeHtmlMapperFunc = null) {
            if(bbCodeHtmlMapperFunc == null) {
                bbCodeHtmlMapperFunc = (sp) => new BBCodeHtmlMapper(sp.GetRequiredService<RazorLightEngine>());
            }

            services.AddScoped(bbCodeHtmlMapperFunc);
            services.AddScoped<BBCodeToHtml>();

            // Letting .NET Core create the instance will end in an empty rule set (parameters doesn't seems empty then)
            services.AddSingleton<HtmlSanitizer>(x => new HtmlSanitizer());
            services.AddSingleton<BBCodeEditorSanitizer>();

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
