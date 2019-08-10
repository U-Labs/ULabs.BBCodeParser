using Ganss.XSS;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text;

namespace ULabs.BBCodeParser.Tools {
    public class BBCodeEditorSanitizer {
        readonly HtmlSanitizer sanitizer;
        public BBCodeEditorSanitizer(HtmlSanitizer sanitizer) {
            this.sanitizer = sanitizer;
        }

        public string Sanitize(string input) {
            return sanitizer.Sanitize(input);
        }

        /// <summary>
        /// Removes all HTML Tags from a string, but keeps their raw text content. So "<b>Test</b>123" would become "Test 123"
        /// </summary>
        public string RemoveAllHtmlTags(string data) {
            if (string.IsNullOrEmpty(data)) return string.Empty;

            var document = new HtmlDocument();
            document.LoadHtml(data);

            return document.DocumentNode.InnerText;
        }
    }
}
