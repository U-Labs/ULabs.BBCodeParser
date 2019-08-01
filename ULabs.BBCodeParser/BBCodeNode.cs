using System;
using System.Collections.Generic;
using System.Text;

namespace ULabs.BBCodeParser {
    public class BBCodeNode {
        public string TagName { get; set; }
        public string OpenTag { get; set; }
        public string Argument { get; set; }
        public string CloseTag { get; set; }
        public bool IsClosed {
            get => !string.IsNullOrEmpty(CloseTag);
        }
        public bool HasTag {
            get => !string.IsNullOrEmpty(TagName);
        }

        public string InnerContent { get; set; }
        public string InnerHtml { get; set; }
        public List<BBCodeNode> Childs { get; set; }

        public override string ToString() {
            string str = $"{OpenTag}{InnerContent}";
            if (IsClosed) {
                str += CloseTag;
            }
            return str;
        }
    }
}
