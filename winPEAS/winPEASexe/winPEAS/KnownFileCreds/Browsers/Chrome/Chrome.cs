﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using winPEAS.Checks;
using winPEAS.Helpers;
using winPEAS.KnownFileCreds.Browsers.Decryptor;
using winPEAS.KnownFileCreds.Browsers.Models;
using winPEAS._3rdParty.SQLite;

namespace winPEAS.KnownFileCreds.Browsers.Chrome
{
    internal class Chrome : BrowserBase, IBrowser
    {
        public override string Name => "Chrome";

        private const string LOGIN_DATA_PATH = "\\..\\Local\\Google\\Chrome\\User Data\\Default\\Login Data";

        public override void PrintInfo()
        {
            PrintSavedCredentials();
            PrintDBsChrome();
            PrintHistBookChrome();
        }

        private static void PrintDBsChrome()
        {
            try
            {
                Beaprint.MainPrint("Looking for Chrome DBs");
                Beaprint.LinkPrint("https://book.hacktricks.xyz/windows/windows-local-privilege-escalation#browsers-history");
                Dictionary<string, string> chromeDBs = Chrome.GetChromeDbs();

                if (chromeDBs.ContainsKey("userChromeCookiesPath"))
                {
                    Beaprint.BadPrint("    Chrome cookies database exists at " + chromeDBs["userChromeCookiesPath"]);
                    Beaprint.InfoPrint("Follow the provided link for further instructions.");
                }

                if (chromeDBs.ContainsKey("userChromeLoginDataPath"))
                {
                    Beaprint.BadPrint("    Chrome saved login database exists at " + chromeDBs["userChromeCookiesPath"]);
                    Beaprint.InfoPrint("Follow the provided link for further instructions.");
                }

                if ((!chromeDBs.ContainsKey("userChromeLoginDataPath")) &&
                    (!chromeDBs.ContainsKey("userChromeCookiesPath")))
                {
                    Beaprint.NotFoundPrint();
                }
            }
            catch (Exception ex)
            {
                Beaprint.PrintException(ex.Message);
            }
        }

        private static void PrintHistBookChrome()
        {
            try
            {
                Beaprint.MainPrint("Looking for GET credentials in Chrome history");
                Beaprint.LinkPrint("https://book.hacktricks.xyz/windows/windows-local-privilege-escalation#browsers-history");
                Dictionary<string, List<string>> chromeHistBook = Chrome.GetChromeHistBook();
                List<string> history = chromeHistBook["history"];
                List<string> bookmarks = chromeHistBook["bookmarks"];

                if (history.Count > 0)
                {
                    Dictionary<string, string> colorsB = new Dictionary<string, string>()
                    {
                        { Globals.PrintCredStrings, Beaprint.ansi_color_bad },
                    };

                    foreach (string url in history)
                    {
                        if (MyUtils.ContainsAnyRegex(url.ToUpper(), Browser.CredStringsRegex))
                        {
                            Beaprint.AnsiPrint("    " + url, colorsB);
                        }
                    }

                    Console.WriteLine();
                }
                else
                {
                    Beaprint.NotFoundPrint();
                }

                Beaprint.MainPrint("Chrome bookmarks");
                Beaprint.ListPrint(bookmarks);
            }
            catch (Exception ex)
            {
                Beaprint.PrintException(ex.Message);
            }
        }

        private static Dictionary<string, string> GetChromeDbs()
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            // checks if Chrome has a history database
            try
            {
                if (MyUtils.IsHighIntegrity())
                {
                    string userFolder = $"{Environment.GetEnvironmentVariable("SystemDrive")}\\Users\\";
                    var dirs = Directory.EnumerateDirectories(userFolder);
                    foreach (string dir in dirs)
                    {
                        string[] parts = dir.Split('\\');
                        string userName = parts[parts.Length - 1];

                        if (!(dir.EndsWith("Public") || dir.EndsWith("Default") || dir.EndsWith("Default User") || dir.EndsWith("All Users")))
                        {
                            string userChromeCookiesPath =
                                $"{dir}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Cookies";
                            if (File.Exists(userChromeCookiesPath))
                            {
                                results["userChromeCookiesPath"] = userChromeCookiesPath;
                            }

                            string userChromeLoginDataPath =
                                $"{dir}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Login Data";
                            if (File.Exists(userChromeLoginDataPath))
                            {
                                results["userChromeLoginDataPath"] = userChromeLoginDataPath;
                            }
                        }
                    }
                }
                else
                {
                    string userChromeCookiesPath =
                        $"{System.Environment.GetEnvironmentVariable("USERPROFILE")}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Cookies";
                    if (File.Exists(userChromeCookiesPath))
                    {
                        results["userChromeCookiesPath"] = userChromeCookiesPath;
                    }

                    string userChromeLoginDataPath =
                        $"{System.Environment.GetEnvironmentVariable("USERPROFILE")}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Login Data";
                    if (File.Exists(userChromeLoginDataPath))
                    {
                        results["userChromeLoginDataPath"] = userChromeLoginDataPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Beaprint.PrintException(ex.Message);
            }
            return results;
        }

        private static List<string> ParseChromeHistory(string path)
        {
            List<string> results = new List<string>();

            // parses a Chrome history file via regex
            if (System.IO.File.Exists(path))
            {
                Regex historyRegex = new Regex(@"(http|ftp|https|file)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?");

                try
                {
                    using (StreamReader r = new StreamReader(path))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            Match m = historyRegex.Match(line);
                            if (m.Success)
                            {
                                results.Add(m.Groups[0].ToString().Trim());
                            }
                        }
                    }
                }
                catch (IOException exception)
                {
                    Console.WriteLine("\r\n    [x] IO exception, history file likely in use (i.e. Browser is likely running): ", exception.Message);
                }
                catch (Exception ex)
                {
                    Beaprint.PrintException(ex.Message);
                }
            }
            return results;
        }

        private static Dictionary<string, List<string>> GetChromeHistBook()
        {
            Dictionary<string, List<string>> results = new Dictionary<string, List<string>>()
            {
                { "history", new List<string>() },
                { "bookarks", new List<string>() },
            };
            try
            {
                if (MyUtils.IsHighIntegrity())
                {
                    Console.WriteLine("\r\n\r\n=== Chrome (All Users) ===");

                    string userFolder = string.Format("{0}\\Users\\", Environment.GetEnvironmentVariable("SystemDrive"));
                    var dirs = Directory.EnumerateDirectories(userFolder);
                    foreach (string dir in dirs)
                    {
                        string[] parts = dir.Split('\\');
                        if (!(dir.EndsWith("Public") || dir.EndsWith("Default") || dir.EndsWith("Default User") || dir.EndsWith("All Users")))
                        {
                            string userChromeHistoryPath = string.Format("{0}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\History", dir);
                            results["history"] = ParseChromeHistory(userChromeHistoryPath);

                            string userChromeBookmarkPath = string.Format("{0}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Bookmarks", dir);
                            results["bookmarks"] = ParseChromeBookmarks(userChromeBookmarkPath);
                        }
                    }
                }
                else
                {
                    string userChromeHistoryPath = string.Format("{0}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\History", System.Environment.GetEnvironmentVariable("USERPROFILE"));
                    results["history"] = ParseChromeHistory(userChromeHistoryPath);

                    string userChromeBookmarkPath = string.Format("{0}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Bookmarks", System.Environment.GetEnvironmentVariable("USERPROFILE"));

                    results["bookmarks"] = ParseChromeBookmarks(userChromeBookmarkPath);
                }
            }
            catch (Exception ex)
            {
                Beaprint.PrintException(ex.Message);
            }

            return results;
        }

        private static List<string> ParseChromeBookmarks(string path)
        {
            List<string> results = new List<string>();
            // parses a Chrome bookmarks
            if (File.Exists(path))
            {
                try
                {
                    string contents = System.IO.File.ReadAllText(path);

                    // reference: http://www.tomasvera.com/programming/using-javascriptserializer-to-parse-json-objects/
                    JavaScriptSerializer json = new JavaScriptSerializer();
                    Dictionary<string, object> deserialized = json.Deserialize<Dictionary<string, object>>(contents);
                    Dictionary<string, object> roots = (Dictionary<string, object>)deserialized["roots"];
                    Dictionary<string, object> bookmark_bar = (Dictionary<string, object>)roots["bookmark_bar"];
                    System.Collections.ArrayList children = (System.Collections.ArrayList)bookmark_bar["children"];

                    foreach (Dictionary<string, object> entry in children)
                    {
                        //Console.WriteLine("      Name: {0}", entry["name"].ToString().Trim());
                        if (entry.ContainsKey("url"))
                        {
                            results.Add(entry["url"].ToString().Trim());
                        }
                    }
                }
                catch (IOException exception)
                {
                    Console.WriteLine("\r\n    [x] IO exception, Bookmarks file likely in use (i.e. Chrome is likely running).", exception.Message);
                }
                catch (Exception ex)
                {
                    Beaprint.PrintException(ex.Message);
                }
            }
            return results;
        }

        public override IEnumerable<CredentialModel> GetSavedCredentials()
        {
            var result = new List<CredentialModel>();

            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);// APPDATA
            var p = Path.GetFullPath(appdata + LOGIN_DATA_PATH);

            if (File.Exists(p))
            {
                SQLiteDatabase database = new SQLiteDatabase(p);
                string query = "SELECT action_url, username_value, password_value FROM logins";
                DataTable resultantQuery = database.ExecuteQuery(query);

                if (resultantQuery.Rows.Count > 0)
                {
                    var key = GCDecryptor.GetChromeKey();

                    foreach (DataRow row in resultantQuery.Rows)
                    {
                        byte[] nonce, ciphertextTag;
                        byte[] encryptedData = Convert.FromBase64String((string)row["password_value"]);
                        GCDecryptor.Prepare(encryptedData, out nonce, out ciphertextTag);
                        var pass = GCDecryptor.Decrypt(ciphertextTag, key, nonce);

                        string actionUrl = row["action_url"] is System.DBNull ? string.Empty : (string)row["action_url"];
                        string usernameValue = row["username_value"] is System.DBNull ? string.Empty : (string)row["username_value"];

                        result.Add(new CredentialModel()
                        {
                            Url = actionUrl,
                            Username = usernameValue,
                            Password = pass
                        });
                    }

                    database.CloseDatabase();
                }
            }

            return result;
        }
    }
}
