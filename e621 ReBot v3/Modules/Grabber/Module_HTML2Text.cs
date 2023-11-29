using HtmlAgilityPack;
using System.Linq;
using System;
using System.Net;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal class Module_Html2Text
    {
        internal static string? DecodeText(string? InputString)
        {
            string? OutputString = null;
            if (InputString != null)
            {
                OutputString = WebUtility.HtmlDecode(InputString).Trim();
                OutputString = OutputString.Replace("http://", "https://");
            }
            if (OutputString != null)
            {
                OutputString = string.IsNullOrEmpty(OutputString) ? null : OutputString;
            }

            return OutputString;
        }

        // - - - - - - - - - - - - - - - -

        internal static string? Html2Text_FurAffinity(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            //remove colors
            HtmlNodeCollection SelectColorNodes = TextHolderNode.SelectNodes(".//span[starts-with(@style, 'color:')]");
            if (SelectColorNodes != null)
            {
                foreach (HtmlNode SpanNode in SelectColorNodes)
                {
                    SpanNode.ParentNode.RemoveChild(SpanNode, true);
                }

            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_FurAffinity(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_FurAffinity(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "i":
                    {
                        TextHolder += $"[i]{TextHolderNode.InnerText} [/i]";
                        break;
                    }

                case "u":
                    {
                        TextHolder += $"[u]{TextHolderNode.InnerText} [/u]";
                        break;
                    }

                case "hr":
                    {
                        TextHolder += "--------------------------------";
                        break;
                    }

                case "strong":
                    {
                        if (TextHolderNode.FirstChild != null) // can be blank sometimes https://www.furaffinity.net/view/36905527/
                        {
                            if (TextHolderNode.ChildNodes.Count > 1)
                            {
                                TextHolder += $"[b]{Html2Text_FurAffinity(TextHolderNode)} [/b]";
                            }
                            else
                            {
                                TextHolder += $"[b]{ParseNode_FurAffinity(TextHolderNode.FirstChild)} [/b]";
                            }
                        }
                        break;
                    }

                case "a":
                    {
                        string aURL = $"https://www.furaffinity.net{WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value)}";
                        string TempTextHolder = TextHolderNode.InnerText.Replace("&nbsp;", " ").Trim();                 
                        if (TextHolderNode.Attributes["class"] != null) // parsed_nav_links doesn't have class attribute
                        {
                            if (TextHolderNode.Attributes["class"].Value.Equals("linkusername") || TextHolderNode.Attributes["class"].Value.Equals("iconusername"))
                            {
                                TextHolder += "🦊";
                            }
                            TempTextHolder = TempTextHolder ?? TextHolderNode.Attributes["title"].Value.Trim();
                        }
                        TextHolder += $"\"{TempTextHolder}\":{aURL}";
                        break;
                    }

                case "span":
                    {
                        switch (TextHolderNode.Attributes["class"].Value)
                        {
                            case "bbcode_quote":
                                {
                                    TextHolder += $"[quote]{(TextHolderNode.ChildNodes.Count > 1 ? Html2Text_FurAffinity(TextHolderNode) : ParseNode_FurAffinity(TextHolderNode.FirstChild))} [/quote]";
                                    break;
                                }

                            default:
                                {
                                    TextHolder += TextHolderNode.ChildNodes.Count > 1 ? Html2Text_FurAffinity(TextHolderNode) : ParseNode_FurAffinity(TextHolderNode.FirstChild);
                                    break;
                                }
                        }
                        break;
                    }

                case "code":
                case "h2": //https://www.furaffinity.net/view/39735601/
                case "sub": // https://www.furaffinity.net/view/36370763/
                    {
                        TextHolder += Html2Text_FurAffinity(TextHolderNode);
                        break;
                    }

                case "div":
                    {
                        switch (TextHolderNode.Attributes["class"].Value)
                        {
                            case "submission-footer": //https://www.furaffinity.net/view/36368046/
                                {
                                    TextHolder += $"\n\n{Html2Text_FurAffinity(TextHolderNode)}";
                                    break;
                                }

                            default:
                                {
                                    TextHolder += "UNKNOWN DIV";
                                    break;
                                }
                        }
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_Inkbunny(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            if (TextHolderNode.InnerHtml.Contains("<span"))
            {
                foreach (HtmlNode SpanNode in TextHolderNode.SelectNodes(".//span"))
                {
                    SpanNode.ParentNode.RemoveChild(SpanNode, true);
                }
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_Inkbunny(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_Inkbunny(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        int TabTest = TextHolderNode.InnerText.Count(ch => ch.Equals('\t'));
                        TextHolder += TabTest > 2 ? $"{TextHolderNode.InnerText.Trim()} ": TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += Environment.NewLine;
                        break;
                    }

                case "strong":
                    {
                        if (TextHolderNode.FirstChild != null) // can be blank sometimes
                        {
                            if (TextHolderNode.ChildNodes.Count > 1)
                            {
                                TextHolder += $"[b]{Html2Text_Inkbunny(TextHolderNode)} [/b]";
                            }
                            else
                            {
                                TextHolder += $"[b]{ParseNode_Inkbunny(TextHolderNode.FirstChild)} [/b]";
                            }
                        }
                        break;
                    }

                case "em":
                    {
                        if (TextHolderNode.FirstChild != null) // can be blank sometimes
                        {
                            if (!TextHolderNode.FirstChild.Name.Equals("#text"))
                            {
                                if (TextHolderNode.ChildNodes.Count > 1)
                                {
                                    TextHolder += $"[i]{Html2Text_Inkbunny(TextHolderNode)} [/i]";
                                }
                                else
                                {
                                    TextHolder += $"[i]{ParseNode_Inkbunny(TextHolderNode.FirstChild)} [/i]";
                                }
                            }
                            else
                            {
                                TextHolder += $"[i]{TextHolderNode.InnerText} [/i]";
                            }
                        }
                        break;
                    }

                case "a":
                    {
                        // Skip image icons
                        if (TextHolderNode.FirstChild != null) // can be blank sometimes https://inkbunny.net/s/2153286
                        {
                            if (TextHolderNode.FirstChild.Name.Equals("img"))
                            {
                                if (TextHolderNode.FirstChild.Attributes["src"].Value.Contains("internet-furaffinity.png"))
                                {
                                    TextHolder += "🦊";
                                }
                            }
                            else
                            {
                                TextHolder += $"\"{TextHolderNode.InnerText}\":{TextHolderNode.Attributes["href"].Value}";
                            }
                        }
                        break;
                    }

                case "table":
                    {
                        HtmlNode SubElement = TextHolderNode.SelectSingleNode(".//a[@class='widget_userNameSmall']");
                        if (SubElement != null)
                        {
                            TextHolder += $"🐰\"{SubElement.InnerText}\":{SubElement.Attributes["href"].Value}";
                            break;
                        }

                        SubElement = TextHolderNode.SelectSingleNode(".//div[@class='widget_imageFromSubmission ']");
                        if (SubElement != null)
                        {
                            string PicUrl = SubElement.SelectSingleNode(".//img").Attributes["src"].Value;
                            string? PostURL = null;
                            if (PicUrl.Contains("overlays/blocked.png")) // https://inkbunny.net/s/2163614
                            {
                                PostURL = $"https://inkbunny.net{TextHolderNode.SelectSingleNode(".//a").Attributes["href"].Value}";
                                TextHolder += $"🐰\"{PostURL}\":{PostURL}";
                            }
                            else
                            {
                                PostURL = TextHolderNode.SelectSingleNode(".//a").Attributes["href"].Value;
                                if (PostURL.Substring(0, 3).Equals("/s/"))
                                {
                                    PostURL = $"https://inkbunny.net{PostURL}";
                                }
                                TextHolder += $"🐰\"{SubElement.SelectSingleNode(".//img").Attributes["title"].Value}\":{PostURL}";
                            }
                            break;
                        }
                        break;
                    }

                default:
                    {
                        if (TextHolderNode.Name.Equals("div"))
                        {
                            switch (TextHolderNode.Attributes["class"].Value)
                            {
                                case "bbcode_quote":
                                    {
                                        TextHolder += $"[quote]{TextHolderNode.InnerText} [/quote]";
                                        break;
                                    }

                                case "align_center":
                                    {
                                        TextHolder += Html2Text_Inkbunny(TextHolderNode);
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            TextHolder += "UNKNOWN ELEMENT";
                        }
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_Pixiv(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_Pixiv(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_Pixiv(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "strong":
                    {
                        TextHolder +=  $"[b]{(!TextHolderNode.FirstChild.Name.Equals("#text") ? ParseNode_Pixiv(TextHolderNode.FirstChild) : TextHolderNode.InnerText)} [/b]";
                        break;
                    }

                case "a":
                    {
                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        if (aURL.StartsWith("/jump.php?", StringComparison.OrdinalIgnoreCase))
                        {
                            aURL = aURL.Substring(10);
                        }
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} ";
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_Newgrounds(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            if (TextHolderNode == null)
            {
                return null;
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_Newgrounds(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_Newgrounds(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "p":
                    {
                        TextHolder += TextHolderNode.FirstChild != null ? ParseNode_Newgrounds(TextHolderNode.FirstChild) : null;
                        break;
                    }

                case "#text":
                    {
                        string TempTextHolder = TextHolderNode.InnerText;
                        if (TempTextHolder.Length > 0)
                        {
                            TextHolder += $"{TempTextHolder}\n";
                        }
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "i":
                    {
                        TextHolder += $"[i]{TextHolderNode.InnerText} [/i]";
                        break;
                    }

                case "a":
                    {
                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} \n";
                        break;
                    }

                case "img":
                    {
                        string aURL = TextHolderNode.Attributes["src"].Value;
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} \n";
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT\n";
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_SoFurry(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            if (TextHolderNode == null)
            {
                return null;
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_SoFurry(Line);
            }

            return DecodeText(TextString);
        }
        
        private static string? ParseNode_SoFurry(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += $"{TextHolderNode.InnerText}\n";;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "a":
                    {
                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} \n";
                        break;
                    }

                case "div":
                    {
                        TextHolder += Html2Text_SoFurry(TextHolderNode);
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_Weasyl(HtmlNode TextHolderNode)
        {
            string? TextString = null;


            if (TextHolderNode == null)
            {
                return null;
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_Weasyl(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_Weasyl(HtmlNode TextHolderNode)
        {
            string TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "a":
                    {

                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} ";
                        break;
                    }

                case "em":
                    {
                        TextHolder += $"[i]{TextHolderNode.InnerText} [/i]";
                        break;
                    }

                case "p":
                case "div":
                    {
                        TextHolder += Html2Text_Weasyl(TextHolderNode);
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }


        internal static string? Html2Text_Mastodon(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            if (TextHolderNode == null)
            {
                return null;
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_Mastodon(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_Mastodon(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "a":
                    {
                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        TextHolder += $"\"{TextHolderNode.InnerText}\":{aURL} ";
                        break;
                    }

                case "p":
                    {
                        TextHolder += $"{Html2Text_Mastodon(TextHolderNode)}\n\n";
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }



        internal static string? Html2Text_HentaiFoundry(HtmlNode TextHolderNode)
        {
            string? TextString = null;

            if (TextHolderNode == null)
            {
                return null;
            }

            foreach (HtmlNode Line in TextHolderNode.ChildNodes)
            {
                TextString += ParseNode_HentaiFoundry(Line);
            }

            return DecodeText(TextString);
        }

        private static string? ParseNode_HentaiFoundry(HtmlNode TextHolderNode)
        {
            string? TextHolder = null;
            switch (TextHolderNode.Name)
            {
                case "#text":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "br":
                    {
                        TextHolder += "\n";
                        break;
                    }

                case "a":
                    {
                        string aURL = WebUtility.UrlDecode(TextHolderNode.Attributes["href"].Value);
                        TextHolder += $"\"{ TextHolderNode.InnerText}\":{aURL} ";
                        break;
                    }

                case "span":
                    {
                        TextHolder += TextHolderNode.InnerText;
                        break;
                    }

                case "em":
                    {
                        TextHolder += $"[i]{TextHolderNode.InnerText} [/i]";
                        break;
                    }

                case "u":
                    {
                        TextHolder += $"[u]{TextHolderNode.InnerText} [/u]";
                        break;
                    }

                default:
                    {
                        TextHolder += "UNKNOWN ELEMENT";
                        break;
                    }
            }

            return TextHolder;
        }
    }
}