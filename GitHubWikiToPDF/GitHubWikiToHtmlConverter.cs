﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;

namespace GitHubWikiToPDF
{
    class GitHubWikiToHtmlConverter
    {
        List<string> ConvertedPages = new List<string>();
        List<string> LinkedPages = new List<string>();
        List<string> DownloadedImages = new List<string>();

        WebClient webClient= new WebClient();

        int m_numOpenLists = 0;
        string OpenList()
        {
            //string openingTag;
            if (m_numOpenLists > 0) return "";
            m_numOpenLists++;
            return "<ul>";

        }
        string CloseAllOpenLists()
        {
            string closingTags;
            if (m_numOpenLists == 0) return "";
            closingTags = "";
            for (int i = 0; i < m_numOpenLists; i++) closingTags += "</ul>";
            m_numOpenLists = 0;
            return closingTags;
        }

        string AsItemList(string line) { return OpenList() + "<li>" + line.Substring(2) + "</li>"; }
        string AsParagraph(string line) { return "<p>" + line + "</p>"; }

        void DownloadImage(string url, string localFile)
        {
            string outputFolder = Path.GetDirectoryName(localFile);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            webClient.DownloadFile(url, localFile);
        }
        string FromUrlToLocalFile(string url, string localFolder)
        {
            string localFile = localFolder;
            if (!localFile.EndsWith("\\") && !localFile.EndsWith("/"))
                localFile += "/";
            return localFile + "img/" + Path.GetFileName(url);
        }
        string FromUrlToLocalFileRelativeToHtml(string url)
        {
            return "img/" + Path.GetFileName(url);
        }

        string ParseImages(string line, string regExpr, string folder)
        {
            Match match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;
                string url = match.Groups[2].Value;
                string localFile = FromUrlToLocalFile(url, folder);
                if (!DownloadedImages.Contains(url))
                {
                    DownloadImage(url, localFile);
                    DownloadedImages.Add(url);
                }
                string htmlLink = "<img src=\"" + FromUrlToLocalFileRelativeToHtml(url) + "\" alt=\"" + text + "\">";
                line = line.Substring(0, match.Index) + htmlLink + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }

        string WikifyLink(string url)
        {
            return url.Replace(' ', '-');
        }

        string ParseLinks(string line, string regExpr)
        {
            Match match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;
                string wikiLink;

                if (match.Groups.Count > 2)
                    wikiLink = WikifyLink(match.Groups[2].Value);
                else wikiLink = WikifyLink(text);

                string htmlLinkRef= wikiLink + ".html";
                
                //Add to the list of linked pages
                LinkedPages.Add(wikiLink + ".md");

                string htmlLink= "<a href=\"" + htmlLinkRef + "\">" + text + "</a>";
                line = line.Substring(0, match.Index) + htmlLink + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }

        string ConvertTitles(string line)
        {
            //<h1> is reserved for the title of the document
            if (line.StartsWith("# ")) return CloseAllOpenLists() + "<h2>" + line.Substring(2) + "</h2>";
            if (line.StartsWith("## ")) return CloseAllOpenLists() + "<h3>" + line.Substring(3) + "</h3>";
            if (line.StartsWith("### ")) return CloseAllOpenLists() + "<h4>" + line.Substring(4) + "</h4>";
            if (line.StartsWith("#### ")) return CloseAllOpenLists() + "<h5>" + line.Substring(5) + "</h5>";
            return line;
        }
 
        string SubstitutePattern(string line, string regExpr, string outPrefix, string outPostfix)
        {
            Match match;
            match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;

                if (match.Groups.Count > 2) // if the pattern includes a character after the main one, we add it to the output. See the pattern used for italics
                    line = line.Substring(0, match.Index) + outPrefix + text + outPostfix + match.Groups[2].Value + line.Substring(match.Index + match.Length);
                else line = line.Substring(0, match.Index) + outPrefix + text + outPostfix + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }

        string DocNameFromFilename(string htmlDocFilename)
        {
            string docName = Path.GetFileNameWithoutExtension(htmlDocFilename);
            docName = docName.Replace('-', ' ');
            var result = Regex.Replace(docName, @"\b(\w)", m => m.Value.ToUpper());
            docName = Regex.Replace(result, @"(\s(of|in|by|and|the)|\'[st])\b", m => m.Value.ToLower(), RegexOptions.IgnoreCase);

            return docName;
        }

        public string Convert(string folder, string markdownDocFilename)
        {
            //we ignore external references
            List<string> ignoredPrefixes = new List<string>(){ "http://", "https://", "./", "../"};
            foreach (string ignoredPrefix in ignoredPrefixes)
            {
                if (markdownDocFilename.StartsWith(ignoredPrefix))
                    return null;
            }
            //we ignore references to anchors
            if (markdownDocFilename.Contains("#"))
                return null;

            string localFilename = folder + "\\" + markdownDocFilename;

            if (!File.Exists(localFilename))
            {
                Console.WriteLine("Warning: Invalid reference to " + markdownDocFilename + " found");
                return null;
            }

            string htmlDocFilename = markdownDocFilename.Substring(0, markdownDocFilename.Length - 3) + ".html";

            string[] lines = File.ReadAllLines(localFilename);

            if (lines == null)
            {
                Console.WriteLine("ERROR. Couldn't find referenced page: " + markdownDocFilename);
                return null;
            }

            List<string> parsedLines = new List<string>();

            foreach (string line in lines)
            {
                string parsedLine = line.Trim(' ');

                parsedLine = ConvertTitles(parsedLine);

                if (parsedLine.StartsWith("* ")) parsedLine = AsItemList(parsedLine);
                else if (parsedLine.StartsWith("- ")) parsedLine = AsItemList(parsedLine);
                else parsedLine = AsParagraph(parsedLine);
                //parse images, ALWAYS BEFORE REGULAR LINKS
                parsedLine = ParseImages(parsedLine, @"!\[([^\]]+)\]\(([^\)]+)\)", folder);
                //parse links
                parsedLine = ParseLinks(parsedLine, @"\[\[([^\]]+)\|([^\]]+)\]\]"); //[[text|url]]
                parsedLine = ParseLinks(parsedLine, @"\[([^\]]+)\]\(([^\)]+)\)"); //[text](url)
                parsedLine = ParseLinks(parsedLine, @"\[\[([^\]]+)\]\]"); //[[url]]
                //parse bolds
                parsedLine = SubstitutePattern(parsedLine, @"\*\*([^\*]+)\*\*", "<b>", "</b>");
                parsedLine = SubstitutePattern(parsedLine, @"\*([^\*]+)\*", "<b>", "</b>");
                //parse italics
                parsedLine = SubstitutePattern(parsedLine, @"_([^_]+)_(\s|,|\.|\:|\))", "<em>", "</em>");
                //parse code
                parsedLine = SubstitutePattern(parsedLine, @"`([^`]+)`", "<code>", "</code>");

                parsedLines.Add(parsedLine);
            }

            using (StreamWriter writer = File.CreateText(folder + "\\" + htmlDocFilename))
            {
                string title = DocNameFromFilename(htmlDocFilename);
                writer.WriteLine("<html><header><title>" + title + "</title></header><body>");
                writer.WriteLine("<h1>" + title + "</h1>");
                foreach (string line in parsedLines)
                    writer.WriteLine(line);
                writer.WriteLine("</body></html>");
            }

            ConvertedPages.Add(markdownDocFilename);
            while (LinkedPages.Count > 0)
            {
                string linkedPage = LinkedPages[0];
                LinkedPages.RemoveAt(0);

                if (!ConvertedPages.Contains(linkedPage))
                    Convert(folder, linkedPage);
            }

            return htmlDocFilename;
        }
    }
}
