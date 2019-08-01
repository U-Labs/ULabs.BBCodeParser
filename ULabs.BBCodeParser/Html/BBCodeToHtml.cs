using Ganss.XSS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ULabs.BBCodeParser.Html {
    public class BBCodeToHtml {
        readonly HtmlSanitizer sanitizer;
        List<BBCode> bbCodes;
        List<string> trimStrings = new List<string> { "\r\n", "\n" };
        string[] trimLineBreakTags = new string[] { "quote", "spoiler", "code", "center" };

        public BBCodeToHtml(BBCodeHtmlMapper htmlMapper, HtmlSanitizer sanitizer) {
            bbCodes = htmlMapper.Codes;
            this.sanitizer = SetSanitizerConfig(sanitizer);
            // ToDo: Consider a better solution here for our custom classes, so that none classjacking would be possible. 
            // Currently the inpact on security doesn't seem that huge, but for long term it would be better to sanitize specific model attributes.
        }

        HtmlSanitizer SetSanitizerConfig(HtmlSanitizer sanitizer) {
            // Avoid multiple initialization
            if (sanitizer.AllowDataAttributes) {
                return sanitizer;
            }
            // Its not known that we need those tags and it could be a security hole if user e.g. create faked forms inside posts
            // footer is not removed cause we need it in quotes to display content author
            var disallowedTags = new List<string>() { "form", "input", "html", "body", "option", "textarea", "header", "head" };
            disallowedTags.ForEach(tag => sanitizer.AllowedTags.Remove(tag));
            // Required for many parsings where bootstrap classes were set
            sanitizer.AllowedAttributes.Add("class");
            // Spoiler bbcode use ids to divide between multiple spoilers on the same page
            sanitizer.AllowedAttributes.Add("id");
            sanitizer.AllowDataAttributes = true;
            return sanitizer;
        }

        public string Parse(string bbCode) {
            var doc = new BBCodeDocument(bbCode);
            // No new line to html parsing here. This would create too much new lines by templates like spoiler. We do this inside the parse method on the inner html.
            RemoveLineBreaksAroundBlockElements(doc.Nodes);

            string html = "";
            doc.Nodes.ForEach(node => {
                string nodeHtml = ParseNode(node);
                string saveHtml = sanitizer.Sanitize(nodeHtml);
                html += saveHtml;
            });
            return html;
        }

        string ParseNewLines(string html) {
            string paragraph = "<br /><br />";
            // Currently, we parse \r\n and \n here to the corresponding HTML where more than 2 following new lines got combined to a paragraph replacement (<br /><br />)
            // [\r\n]{2,} would replace \n\n correctly to <br /><br /> but \r\n would be threaded as two items (<br /><br /><br /><br />)
            // https://stackoverflow.com/a/1725967/3276634
            html = Regex.Replace(html, @"(?:\r\n|\r(?!\n)|(?<!\r)\n){2,}", paragraph, RegexOptions.Compiled);
            html = html.Replace("\n", "<br />");
            return html;
        }

        // Sometimes we have \r\n\r\n after quotes, which result in huge spaces. This methods remove them
        void RemoveLineBreaksAroundBlockElements(List<BBCodeNode> nodes) {
            // ToDo: We need this for all blocking tags like code or lists
            var blockNodes = nodes.Where(n => !string.IsNullOrEmpty(n.TagName))
                .Where(n => trimLineBreakTags.Contains(n.TagName.ToLower()))
                .ToList();
            blockNodes.ForEach(node => {
                // Avoid leading and trailing line breaks in e.g. quotes
                node.InnerContent = RemoveLeadingNewLines(node.InnerContent);
                node.InnerContent = RemoveTrailingNewLines(node.InnerContent);

                TrimNodeBeforeAfter(node, nodes);
                // For consistencym, line break removing need to be done on all childs recursively
                if (node.Childs != null) {
                    RemoveLineBreaksAroundBlockElements(node.Childs);
                }
            });
        }

        void TrimNodeBeforeAfter(BBCodeNode node, List<BBCodeNode> nodes) {
            int index = nodes.IndexOf(node);
            // Only remove leading new lines on next node and trailing on previos. Do both would result in broken tags, e.g. wenn the previous item has \n\n at the beginning as delimiter
            int nextIndex = index + 1;
            if (nextIndex < nodes.Count) {
                var nextNode = nodes[nextIndex];
                nextNode.InnerContent = RemoveLeadingNewLines(nextNode.InnerContent);
            }

            int previousIndex = index - 1;
            if (previousIndex >= 0) {
                var previousNode = nodes[previousIndex];
                previousNode.InnerContent = RemoveTrailingNewLines(previousNode.InnerContent);
            }
        }

        string RemoveLeadingNewLines(string inputContent) {
            trimStrings.ForEach(str => {
                while (inputContent.StartsWith(str)) {
                    inputContent = inputContent.Substring(str.Length);
                }
            });
            return inputContent;
        }

        string RemoveTrailingNewLines(string inputContent) {
            trimStrings.ForEach(str => {
                while (inputContent.EndsWith(str)) {
                    inputContent = inputContent.Substring(0, inputContent.Length - str.Length);
                }
            });
            return inputContent;
        }

        BBCode GetBBCodeForNode(BBCodeNode node) {
            // First search for exact open tag to match [list=1] pattern before non-specific [list] pattern
            var code = bbCodes.FirstOrDefault(x => x.BBCodeTag == node.OpenTag.ToLower());
            if (code == null) {
                code = bbCodes.FirstOrDefault(x => x.BBCodeName == node.TagName.ToLower());
            }
            return code;
        }

        string ParseNode(BBCodeNode node, BBCode code = null) {
            // Text only nodes doesn't need any parsing
            if (string.IsNullOrEmpty(node.TagName)) {
                string innerHtml = ParseNewLines(node.InnerContent);
                return innerHtml;
            }

            if (code == null) {
                code = GetBBCodeForNode(node);
            }
            if (code == null) {
                string innerHtml = node.OpenTag;

                // If we find unknown bbcodes, try parsing their inner content html and display the raw outer bbcode so that the user know it's not parseable.
                if (node.Childs != null) {
                    node.Childs.ForEach(childNode => innerHtml += ParseNode(childNode));
                } else {
                    // We also end up here if square brackets were used in other ways like quote, which shouldnt got destroyed: "He [mr xyz] said that..."
                    // In this case, InnerContent is the content after the brackets and childs may be null. No childs exists.
                    innerHtml += ParseNewLines(node.InnerContent);
                }
                // ParseNewLines() call is exceptional here. Regularly it's handled by GetNodeInnerContentHtml()
                return innerHtml + node.CloseTag;
            }

            string html = "";
            if (code.ParserFunc != null) {
                // From a parser function, we expect to handle the ENTIRE node. Only for this reason, the inner html is required and only provided here
                node.InnerHtml = GetNodeInnerContentHtml(node, code.NestedChild);
                html = code.ParserFunc(node);
            } else {
                string openTag = code.HtmlTag;
                if (!string.IsNullOrEmpty(code.ArgumentHtmlAttribute)) {
                    openTag = openTag.Replace(">", $" {code.ArgumentHtmlAttribute}=\"{node.Argument}\">");
                }
                string closeTag = code.HtmlTag.Replace("<", "</");

                html += openTag + GetNodeInnerContentHtml(node, code.NestedChild) + closeTag;
            }
            return html;
        }

        private string GetNodeInnerContentHtml(BBCodeNode node, BBCode nestedChild = null) {
            string innerHtml = "";
            if (node.Childs != null) {
                // If we have childs, the Content attribute isn't interesting since it's contained in the childs
                node.Childs.ForEach(childNode => {
                    // Do not parse new lines here! This would also parse new lines on razor templates and create a lot of unwanted new lines. The last child will 
                    // reach the else branch where only the new lines of inner content got parsed.
                    string childHtml = ParseNode(childNode, nestedChild);
                    innerHtml += childHtml;
                });
            } else {
                innerHtml += ParseNewLines(node.InnerContent);
            }
            return innerHtml;
        }

        string ReplaceTag(string bbCode, int bbCodeOpenTagStartPosition, int bbCodeOpenTagEndPosition, string newHtmlTag) {
            int openTagLength = bbCodeOpenTagEndPosition - bbCodeOpenTagStartPosition + 1;
            bbCode = bbCode.Remove(bbCodeOpenTagStartPosition, openTagLength);
            bbCode = bbCode.Insert(bbCodeOpenTagStartPosition, newHtmlTag);
            return bbCode;
        }
    }
}
