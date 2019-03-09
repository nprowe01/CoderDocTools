﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHubWikiToPDF
{
    class Program
    {
        const string userNameArg = "-user=";
        static string userName = null;
        const string projectNameArg = "-project=";
        static string projectName = null;

        static bool ParseArguments(string [] args)
        {
            foreach(string arg in args)
            {
                if (arg.StartsWith(projectNameArg)) projectName = arg.Substring(projectNameArg.Length);
                if (arg.StartsWith(userNameArg)) userName = arg.Substring(userNameArg.Length);
            }
            if (projectName != null && userName != null) return true;
            return false; //error parsing arguments
        }
        static void Main(string[] args)
        {
            if (!ParseArguments(args))
            {
                Console.WriteLine("ERROR. Incorrect arguments.\nUsage: GitHubWikiToPDF -user=<github-user> -project=<github-project>\nFor example: GitHubWikiToPDF -user=simionsoft -project=SimionZoo");
                return;
            }

            GitHubWikiDownloader downloader = new GitHubWikiDownloader();
            downloader.CloneWikiGitRepo(userName + "/" + projectName, "temp");

            GitHubWikiToHtmlConverter converter = new GitHubWikiToHtmlConverter();
            using (StreamWriter htmlWriter = File.CreateText("temp/" + projectName + ".html"))
            {
                converter.Convert(htmlWriter, "temp", "Home.md");
            }
        }
    }
}
