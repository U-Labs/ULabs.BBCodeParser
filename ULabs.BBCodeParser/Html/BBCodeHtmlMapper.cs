using RazorLight;
using System;
using System.Collections.Generic;
using System.Text;

namespace ULabs.BBCodeParser.Html {
    public class BBCodeHtmlMapper {
        readonly RazorLightEngine razor;

        public List<BBCode> Codes { get; set; }
        /// <summary>
        /// Holds mappings of BBCode to HTML generating mapping functions. Dont use this class directly, use <seealso cref="BBCodeToHtml"/> instead.
        /// </summary>
        /// <param name="razor">Injected dependency</param>
        /// <param name="bbCodes">Provide a list of BBCodes with mapping functions for parsing</param>
        /// <param name="overrideDefaultBBCodes">If true, all default BBCodes were deleted and just <paramref name="bbCodes"/> are used for parsing. If false, <paramref name="bbCodes"/> got added to the defaults.</param>
        public BBCodeHtmlMapper(RazorLightEngine razor, List<BBCode> bbCodes = null, bool overrideDefaultBBCodes = false) {
            this.razor = razor;

            // Needn't to care about security here since BBCodeToHtml.Parse() will sanitize all output to be sure
            Codes = new List<BBCode>() {
                new BBCode("[b]", "<strong>"),
                new BBCode("[i]", "<i>"),
                new BBCode("[u]", "<u>"),
                new BBCode("[s]", "<strike>"),
                new BBCode("[center]", "<center>"),
                new BBCode("[right]", (node) => $"<span class=\"text-xs-right\">{node.InnerHtml}</span>"),
                new BBCode("[code]", "<pre>"),
                new BBCode("[quote]", ParseQuoteFunc),
                new BBCode("[color]", (node) => $"<span style=\"color: {node.Argument}\">{node.InnerHtml}</span>"),
                new BBCode("[url]", "<a>", argumentHtmlAttribute: "href"),
                new BBCode("[img]", (node) => $"<img src=\"{node.InnerContent}\" />"),
                new BBCode("[list]", "<ul>", nestedChild: new BBCode("[*]", "<li>")),
                new BBCode("[list=1]", "<ol>", nestedChild: new BBCode("[*]", "<li>")),
                // new BBCode("[attach]", ParseAttachmentFunc),
                new BBCode("[size]", ParseHadlines),
                new BBCode("[sup]", "<sup>"),
                new BBCode("[font]", (node) => $"<span style='font-family: {node.Argument}'>{node.InnerHtml}</span>"),
                new BBCode("[shadow]", (node) => $"<span class='text-shadow'>{node.InnerHtml}</span>"),
                new BBCode("[spoiler]", (node) => GetEmbeddRazorTemplate("Spoiler", node)),
                new BBCode("[video]", ParseVideo),
                new BBCode("[youtube]", (node) => EmbeddedYouTube(node.InnerContent))
            };

            if (overrideDefaultBBCodes) {
                Codes.Clear();
            }

            if(bbCodes != null) {
                Codes.AddRange(bbCodes);
            }
        }

        string ParseVideo(BBCodeNode node) {
            // Ignore malformed video tags
            if (string.IsNullOrEmpty(node.Argument) || !node.Argument.Contains(";")) {
                return node.ToString();
            }

            var argSegments = node.Argument.Split(';');
            string siteName = argSegments[0].ToLower();
            string videoId = argSegments[1];
            if(siteName == "youtube") {
                return EmbeddedYouTube(videoId);
            }
            // Currently not supported providers
            string rawNode = node.ToString();
            return rawNode;
        }
        string EmbeddedYouTube(string videoId) {
            string html = $"<iframe width='560' height='315' frameborder='0' src='https://www.youtube-nocookie.com/embed/{videoId}' data-youtube-id='{videoId}' allowfullscreen></iframe>";
            return html;
        }
        string ParseHadlines(BBCodeNode node) {
            if (!int.TryParse(node.Argument, out int headlineSize)) {
                return $"{node.OpenTag}{node.InnerHtml}{node.CloseTag}";
            }
            if (headlineSize == 1) {
                return $"<small class='text-muted d-block'>{node.InnerHtml}</small>";
            }
            // ToDo: May replace by hX headlines
            return $"<font size='{headlineSize}'>{node.InnerHtml}</font>";
        }

        string ParseQuoteFunc(BBCodeNode node) {
            // Using InnerHTML to make sure that bbcode inside the quote (e.g. formattings or urls) got parsed, too
            string html = $"<blockquote class=\"blockquote\">{node.InnerHtml}";
            if (!string.IsNullOrEmpty(node.Argument)) {
                var authorSegments = node.Argument.Split(';');
                string author = "";

                if (authorSegments.Length > 1) {
                    // ToDo: We need to verify on which page the post exists since this anker can't work if the post is on the previous or next page
                    author = $"<a href=\"#post{authorSegments[1]}\">{authorSegments[0]}</a>";
                } else {
                    author = authorSegments[0];
                }
                html += $"<footer class=\"blockquote-footer\">{author}</footer>";
            }
            return html + "</blockquote>";
        }
        string HtmlWarning(string text) {
            return $"<div class='alert alert-warning' role='alert'>{text}</div>";
        }
        /// <summary>
        /// Fetches embedded Razor views for more complex HTML code from the dll (requires to set "build" to "embedded ressource" in the propertys of the cshtml file)
        /// </summary>
        string GetEmbeddRazorTemplate(string name, BBCodeNode node) {
            string key = $"Html.Templates.{name}.cshtml";
            
            string template = razor.CompileRenderAsync(key, node).Result;
            return template;
        }
    }
}
