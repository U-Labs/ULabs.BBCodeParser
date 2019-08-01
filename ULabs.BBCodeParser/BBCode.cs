using System;

namespace ULabs.BBCodeParser {
    public class BBCode {
        public string BBCodeTag { get; set; }
        public string BBCodeName {
            get => BBCodeTag.Substring(1, BBCodeTag.Length - 2);
        }
        public string HtmlTag { get; set; }
        public string ArgumentHtmlAttribute { get; set; }

        public BBCode NestedChild { get; set; }
        public Func<BBCodeNode, string> ParserFunc { get; set; }

        public BBCode(string bbCodeTag, string htmlTag, string argumentHtmlAttribute = "", BBCode nestedChild = null) {
            BBCodeTag = bbCodeTag;
            HtmlTag = htmlTag;
            ArgumentHtmlAttribute = argumentHtmlAttribute;
            NestedChild = nestedChild;
        }

        public BBCode(string bbCodeTag, Func<BBCodeNode, string> parserFunc, BBCode nestedChild = null) : this(bbCodeTag, "", "", nestedChild) {
            ParserFunc = parserFunc;
        }
    }
}