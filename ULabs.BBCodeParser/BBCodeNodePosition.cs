using System;
using System.Collections.Generic;
using System.Text;

namespace ULabs.BBCodeParser {
    public class BBCodeNodePosition {
        public int OpenTagStart { get; set; }
        public int OpenTagEnd { get; set; }

        public int CloseTagStart { get; set; }
        public int CloseTagEnd { get; set; }

        public BBCodeNode Node { get; set; }
    }
}
