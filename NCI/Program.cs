﻿using NetChains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NCI
{
    partial class Program
    {
        const string VERSIONSTRING = "3.0.0";

        public static List<string> history = new List<string>();

        static void Main(string[] args)
        {
            Assembly.Load("System");

            try
            {
                Console.Clear();
                Console.ResetColor();
            }
            catch (System.IO.IOException) { return; } // No console present!

            Console.WriteLine($"Welcome to the NetChains interpreter!\n(c) {DateTime.Now.Year} Weston Sleeman, version {VERSIONSTRING}\nType \"help\" for a brief tutorial or \"exit\" to return to the shell.\n");
            if (args == null) args = new string[0] { };

            else
            {
#if DEBUG
                Console.WriteLine("Debug log:");
                foreach (string debugArg in args)
                    Console.WriteLine("\t" + debugArg);
#endif
            }

            if (args.Length == 0)
            {
                while (true)
                {
                    int historyIndex = 0;
                    Console.Write(">>>");

                    ConsoleKeyInfo CKI;
                    string input = "", blockCache = "";
                    bool done = false, inBlockQuote = false;
                    history.Insert(0, "");

                    if (Engine.inBlock)
                    {
                        Console.Write('\t');
                        input = "     ";
                    }

                    while (!done)
                    {
                        CKI = Console.ReadKey(true);
                        switch (CKI.Key)
                        {
                            case ConsoleKey.Backspace:
                                if (Console.CursorLeft > 3)
                                {
                                    int cLeft = Console.CursorLeft - 1;
                                    input = input.Remove(cLeft - 3, 1);
                                    ClearLine();
                                    Console.Write(input);
                                    Console.CursorLeft = cLeft;
                                    history[0] = input;
                                }
                                break;

                            case ConsoleKey.Delete:
                                if (Console.CursorLeft >= 3 && input.Length > 0)
                                {
                                    int cLeft = Console.CursorLeft;
                                    input = input.Remove(cLeft - 3, 1);
                                    ClearLine();
                                    Console.Write(input);
                                    Console.CursorLeft = cLeft;
                                    history[0] = input;
                                }
                                break;

                            case ConsoleKey.Tab:
                                ClearLine();
                                input = ShowOptions(input) ?? input;
                                Console.Write(input);
                                break;

                            case ConsoleKey.UpArrow:
                                try
                                {
                                    input = history[++historyIndex];
                                    ClearLine();
                                    Console.Write(input);
                                }
                                catch { --historyIndex; }
                                break;

                            case ConsoleKey.DownArrow:
                                try
                                {
                                    input = history[--historyIndex];
                                    ClearLine();
                                    Console.Write(input);
                                }
                                catch
                                {
                                    historyIndex = -1;
                                    ClearLine();
                                }
                                break;

                            case ConsoleKey.LeftArrow:
                                Console.CursorLeft -= (Console.CursorLeft > 3 ? 1 : 0);
                                break;

                            case ConsoleKey.RightArrow:
                                Console.CursorLeft += (Console.CursorLeft < 3 + input.Length ? 1 : 0);
                                break;

                            case ConsoleKey.Home:
                                Console.CursorLeft = 3;
                                break;

                            case ConsoleKey.End:
                                Console.CursorLeft = input.Length + 3;
                                break;

                            case ConsoleKey.Enter:
                                Console.Write(Environment.NewLine);
                                if (!inBlockQuote)
                                {
                                    done = true;
                                }
                                else
                                {
                                    history[0] = input;
                                    blockCache += input + "\n";
                                    input = "";
                                    historyIndex = 0;
                                    Console.Write(">>>");
                                }
                                break;

                            default:
                                input = input.Insert(Console.CursorLeft - 3, CKI.KeyChar.ToString());
                                if (CKI.KeyChar == '`')
                                    inBlockQuote = !inBlockQuote;
                                int cursorLeftBackup = Console.CursorLeft;
                                Console.Write(input.Substring(Console.CursorLeft - 3));
                                Console.CursorLeft = cursorLeftBackup + 1; //Restores cursor to user expected state
                                history[0] = input;
                                break;
                        }
                    }

                    if (input == "clear") Console.Clear();
                    else
                        try { Console.WriteLine(Engine.Execute(blockCache.TrimStart('\0') + input)); }
                        catch (Exception ex) { Console.WriteLine(ex.Message); }
                        finally { blockCache = ""; }
                }
            }
            else
            {
                List<string> argList = new List<string>();
                argList.AddRange(args);

                while (argList.Contains("-p"))
                {
                    Console.WriteLine(Engine.ExecFile(argList[argList.IndexOf("-p") + 1]));
                    argList.RemoveAt(argList.IndexOf("-p") + 1);
                    argList.RemoveAt(argList.IndexOf("-p"));
                    if (!argList.Contains("-e"))
                        Main(null);
                }
                while (argList.Contains("-e"))
                {
                    Console.WriteLine(Engine.Execute(argList[argList.IndexOf("-e") + 1]));
                    argList.RemoveAt(argList.IndexOf("-e") + 1);
                    argList.RemoveAt(argList.IndexOf("-e"));
                    System.Threading.Thread.Sleep(100);
                }

                foreach (string arg in argList)
                {
                    Console.WriteLine(Engine.ExecFile(arg));
                }
            }
        }

        private static void ClearLine()
        {
            Console.SetCursorPosition(3, Console.CursorTop);
            Console.Write(new string(' ', Console.BufferWidth - 4));
            Console.SetCursorPosition(3, Console.CursorTop);
        }

        private static void RemoveDuplicates(ref List<MemberInfo> input)
        {
            List<string> names = new List<string>();

            List<MemberInfo> inputDup = new List<MemberInfo>();
            inputDup.AddRange(input);

            foreach (MemberInfo info in inputDup)
            {
                if (names.Contains(info.Name))
                    input.Remove(info);
                else
                    names.Add(info.Name);
            }
        }

        private static string ShowOptions(string input)
        {
            input = input.TrimEnd(':', '.', '!', ' ', '\t');

            Type type = null;
            if (input.Contains('!'.ToString()))
            {
                string typeName = input.Substring(input.LastIndexOf('!') + 1);
                string searchString = "";

                if (typeName.Contains(":"))
                {
                    searchString = typeName.Substring(typeName.LastIndexOf(':') + 1);
                    typeName = typeName.Remove(typeName.IndexOf(':'));
                }
                
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        type = asm.GetType(typeName, true, true);
                        break;
                    }
                    catch
                    {
                        try
                        {
                            type = asm.GetType("System." + typeName, true, true);
                            break;
                        }
                        catch { }
                    }
                }
                if (type == null)
                    return null;

                List<MemberInfo> resultInfos = new List<MemberInfo>();
                resultInfos.AddRange(type.GetMembers());
                string result = "\b\b\b";

                if (searchString != "")
                    resultInfos.RemoveAll(s => !s.Name.ToLower().StartsWith(searchString.ToLower()));

                RemoveDuplicates(ref resultInfos);

                if (resultInfos.Count == 0)
                    return null;

                if (resultInfos.Count == 1)
                    return input.Remove(input.LastIndexOf(':')) + ':' + resultInfos[0].Name;

                if (resultInfos.Count > 40)
                    return null;

                foreach (MemberInfo resultInfo in resultInfos)
                {
                    result += resultInfo.Name + '\t';
                }
                Console.Write(result.TrimEnd('\t') + "\n>>>");
            }
            return null;
        }
    }
}
