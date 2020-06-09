using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ULabs.BBCodeParser {
    public class BBCodeDocument {
        char[] nodeArgumentTrimChars = new char[] { '\'', '"' };
        public string Raw { get; private set; }
        public List<BBCodeNode> Nodes { get; private set; } = new List<BBCodeNode>();
        public List<BBCodeNode> TagNodes {
            get => Nodes.Where(n => n.HasTag)
                .ToList();
        }

        public BBCodeDocument(string bbCode) {
            Raw = bbCode;
            Nodes = ParseNodes(bbCode);
        }

        List<BBCodeNode> ParseNodes(string bbCode) {
            if (!bbCode.Contains("[")) {
                return new List<BBCodeNode>() { CreateTextNode(bbCode) };
            }
            var nodes = new List<BBCodeNode>();

            int firstBBCode = bbCode.IndexOf('[');
            if (firstBBCode > 0) {
                var nodeBefore = CreateStartOrEndNode(bbCode, 0, firstBBCode);
                // When no unformatted text before the first bbcode is present, we got null here. A new line \r\n would result in pseudo list items
                if (nodeBefore.InnerContent.Trim().Length > 0) {
                    nodes.Add(nodeBefore);
                }
            }

            int currentOpenTagPos = 0;
            int lastOpenTagPos = bbCode.LastIndexOf('[');
            while ((currentOpenTagPos = bbCode.IndexOf('[', currentOpenTagPos)) > -1 && currentOpenTagPos <= lastOpenTagPos) {
                if (currentOpenTagPos == -1) {
                    break;
                }

                var nodePos = GetNodePositionAndContent(bbCode, currentOpenTagPos);
                nodes.Add(nodePos.Node);

                if (nodePos.Node.InnerContent.Contains("[")) {
                    nodePos.Node.Childs = ParseNodes(nodePos.Node.InnerContent);
                }

                var textNode = GetNextTextNode(bbCode, nodePos.CloseTagEnd);
                // Previously, nodes that only contain line breaks and no content were removed here. That turned out to be no good idea: Altough we remove unwanted single line breaks with that method, 
                // no it's hard to replace them by spaces. Example: "[b]Test1[/b]\r\n[b]Test2[/b]" Here its better to keep the \r\b first to remove it later in our html parser.
                // This also keeps our Document more generic and not too special for html parsing purpose. For this reason, only real empty nodes (null) are skipped
                if (textNode != null) {
                    nodes.Add(textNode);
                }

                if (nodePos.CloseTagStart != -1) {
                    currentOpenTagPos = nodePos.CloseTagEnd;
                } else {
                    // ToDo: Shouldnt get here. Just to be sure, we lave it to avoid endless loops
                    currentOpenTagPos++;
                }
            }

            return nodes;
        }

        BBCodeNode GetNextTextNode(string bbCode, int closeTagEnd) {
            int nextOpenStartPos = bbCode.IndexOf('[', closeTagEnd);
            int textNodeLength = 0;

            if (nextOpenStartPos > -1) {
                textNodeLength = nextOpenStartPos - closeTagEnd;
            } else {
                textNodeLength = bbCode.Length - closeTagEnd;
            }

            if (textNodeLength > 0 && textNodeLength < bbCode.Length) {
                // Sometimes we get whitespaces here. Don't strip them, they divide words in text, so they're required
                var node = new BBCodeNode() {
                    InnerContent = bbCode.Substring(closeTagEnd, textNodeLength)
                };
                return node;
            }
            return null;
        }

        BBCodeNodePosition GetNodePositionAndContent(string bbCode, int openTagPos) {
            var nodePos = new BBCodeNodePosition();
            nodePos.OpenTagStart = openTagPos;
            nodePos.OpenTagEnd = bbCode.IndexOf(']', openTagPos);

            // ToDo: Workaround. We end here by a "[:" smiley at the end of the post. Example post #436911 . Workaround removes the smiley
            if (nodePos.OpenTagEnd == -1) {
                nodePos.OpenTagEnd = bbCode.IndexOf('[', openTagPos + 1);
                if (nodePos.OpenTagEnd == -1) {
                    nodePos.OpenTagEnd = bbCode.Length - 1;
                }
            }

            int openTagLength = nodePos.OpenTagEnd - nodePos.OpenTagStart + 1;
            string fullOpenTag = bbCode.Substring(nodePos.OpenTagStart, openTagLength);
            nodePos.Node = GetNodeTagWithArguments(fullOpenTag);

            GetNodeCloseTagWithContent(bbCode, ref nodePos);
            HandleDoubleTags(bbCode, ref nodePos);
            return nodePos;
        }

        void GetNodeCloseTagWithContent(string bbCode, ref BBCodeNodePosition nodePos) {
            string assumedCloseTag = $"[/{nodePos.Node.TagName}]";

            // Mostly we need to fetch the close tag yourself, then the start pos is zero. But if we have nested tags, we need to skip the first ending tag and fetch another one. 
            // In this case, the if statement prevent us from overwrite this value by fetching the start end position of the first tag (which is wrong in nested cases)
            if (nodePos.CloseTagStart == 0) {
                GetNestedEndTag(ref nodePos, assumedCloseTag, in bbCode);
            }

            int contentStartPos = nodePos.OpenTagEnd + 1;
            int contentEndPos = 0;

            if (nodePos.CloseTagStart > 0) {
                // Don't use the assumedCloseTag here, since his cases may not correct by ignoring cases in IndexOf match above
                nodePos.CloseTagEnd = nodePos.CloseTagStart + assumedCloseTag.Length;
                nodePos.Node.CloseTag = bbCode.Substring(nodePos.CloseTagStart, nodePos.CloseTagEnd - nodePos.CloseTagStart);
                contentEndPos = nodePos.CloseTagStart - nodePos.OpenTagEnd - 1;
            } else {
                // Case A Not closed tag like list items [*]: Search for following tags as possible end cause default handling can't generate the length here (-1)
                int nextTagStartPos = bbCode.IndexOf(nodePos.Node.OpenTag, nodePos.OpenTagEnd);
                // Case B: No further tags of the same name present, search for any next tag as ending
                if (nextTagStartPos == -1) {
                    nextTagStartPos = bbCode.IndexOf('[', nodePos.OpenTagEnd);
                }

                // Case C: No tags avaliable any more. Take anything to the end of the content
                if (nextTagStartPos > -1) {
                    contentEndPos = nextTagStartPos - contentStartPos;
                    // Avoid parsing the detected content again, which would result in duplicate nodes
                    nodePos.CloseTagStart = nodePos.CloseTagEnd = nextTagStartPos;
                } else {
                    contentEndPos = bbCode.Length - contentStartPos;
                    nodePos.CloseTagStart = nodePos.CloseTagEnd = contentStartPos + contentEndPos;
                }
            }

            nodePos.Node.InnerContent = bbCode.Substring(contentStartPos, contentEndPos);
        }
        /// <summary>
        /// Gets the end tag considering that tags may nested (e.g. [spoiler][spoiler]a[/spoiler][spoiler]b[/spoiler][/spoiler] here we match the latest [/spoiler])
        /// by setting CloseTagStart and CloseTagEnd in node position
        /// </summary>
        void GetNestedEndTag(ref BBCodeNodePosition nodePos, string assumedCloseTag, in string bbCode) {
            //nodePos.CloseTagStart = bbCode.IndexOf(assumedCloseTag, nodePos.OpenTagEnd, StringComparison.InvariantCultureIgnoreCase);
            // Keep open to match any arguments
            string lowerOpenTag = $"[{nodePos.Node.TagName.ToLower()}";
            string lowerAssumedCloseTag = assumedCloseTag.ToLower();
            int endTagSearchStartPos = nodePos.OpenTagEnd;
            bool openCloseTagsMatch = false;
            while (!openCloseTagsMatch) {
                // By ignoring cases, bbcodes like [QUOTE]abc[/quote] got matched correctly, Search only for end tags based on open tags. This is enough since bbcode would be invalid 
                // if tags inside doesn't match. And we can't search for all non closing tags since some tags were not closed like [hr] and we got false positive matches.
                int closeTagStart = bbCode.IndexOf(assumedCloseTag, endTagSearchStartPos, StringComparison.InvariantCultureIgnoreCase);
                if (closeTagStart == -1) {
                    // Happens if no closing tag could be found. Mostly false positive matches like "[Help] XYZ not working", smileys and so on
                    break;
                }

                nodePos.CloseTagStart = closeTagStart;
                nodePos.CloseTagEnd = closeTagStart + assumedCloseTag.Length;

                string contentLower = bbCode.Substring(nodePos.OpenTagStart, nodePos.CloseTagEnd - nodePos.OpenTagStart);
                // .NET Standard doesn't support the simple Split(string delimiter) overload, but it seems we can use the following overload without any issues
                int openTagsCount = contentLower.Split(new string[] { lowerOpenTag }, StringSplitOptions.None).Length - 1;
                int closeTagsCount = contentLower.Split(new string[] { lowerAssumedCloseTag }, StringSplitOptions.None).Length - 1;

                openCloseTagsMatch = openTagsCount == closeTagsCount;
                endTagSearchStartPos = nodePos.CloseTagStart + 1;
            }
        }

        /// <summary>
        /// Handles senseless nested tags e.g. [center][center]xyz[/center] abc[/center]. They're present in some topics like post 409605 "Rekordwerte im Anmarsch" and came from 
        /// vB's editor. The default parser would use the first [/center] as close tag, which would result in broken end tags. 
        /// </summary>
        void HandleDoubleTags(string bbCode, ref BBCodeNodePosition nodePos) {
            // ToDo: Doc
            // Self closing tags cant be double tags
            if (string.IsNullOrEmpty(nodePos.Node.CloseTag)) {
                return;
            }

            string content = nodePos.Node.InnerContent;
            // Important to search for the opened tag here, so that nested tags with different arguments got matched, too. Example: [size=3][size=2]xx[/size=2]yy[/size]
            string openedOpenTag = $"[{nodePos.Node.TagName}";
            string closeTag = openedOpenTag.Insert(1, "/");

            // ToDo: Do we have regular nested tags in itself? Doesn't think so, but also with this check it filters those problematic markup
            if (content.Contains(openedOpenTag) && !content.Contains(closeTag)) {
                // ToDo: May handle recursive multiple tags (haven't seen such worse bbcode yet)
                nodePos.CloseTagStart = bbCode.IndexOf(nodePos.Node.CloseTag, nodePos.CloseTagEnd, StringComparison.InvariantCultureIgnoreCase);
                GetNodeCloseTagWithContent(bbCode, ref nodePos);
            }
        }
        
        BBCodeNode GetNodeTagWithArguments(string fullOpenTag) {
            var node = new BBCodeNode() {
                OpenTag = fullOpenTag
            };
            // Some links (e.g. spiegel.de) have equal signs in their links. The limitation prevent those link from breaking.
            // Substring remove the [] brackets, which save headaches on argument-parsing later
            var segments = fullOpenTag.Substring(1, fullOpenTag.Length - 2)
                .Split(new char[] { '=' }, 2);
            node.TagName = segments[0];
            if (segments.Length > 1) {
                // Remove quotes, e.g. [url="https://ecosia.org"]Ecosia[/url]
                node.Argument = segments[1].Trim(nodeArgumentTrimChars);
                if (node.Argument.StartsWith("\"")) {
                    node.Argument = node.Argument.Substring(1, node.Argument.Length - 2);
                }
            }
            return node;
        }

        BBCodeNode CreateTextNode(string text) {
            var node = new BBCodeNode() {
                InnerContent = text
            };
            return node;
        }

        BBCodeNode CreateStartOrEndNode(string bbCode, int startPos, int length) {
            var node = new BBCodeNode() {
                InnerContent = bbCode.Substring(startPos, length)
            };
            return node;
        }
    }

}
