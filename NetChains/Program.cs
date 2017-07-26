using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NetChains
{
    partial class Program
    {
        const string VERSIONSTRING = "2.3.0";

        public static List<string> history = new List<string>();
        public static bool inBlock = false;
        static private Dictionary<string, string> variables = new Dictionary<string, string>();
        static private Dictionary<string, string> scriptCache = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            Console.Clear();
            Console.ResetColor();

            Console.WriteLine($"Welcome to the NetChains interpreter!\n(c) 2017 Weston Sleeman, version {VERSIONSTRING}\nType \"help\" for a brief tutorial or \"exit\" to return to the shell.\n");
            if (args == null) args = new string[0]{ };

            if (args.Length == 0)
            {
                while (true)
                {
                    int historyIndex = 0;
                    Console.Write(">>>");

                    ConsoleKeyInfo CKI;
                    string input = "";
                    bool done = false;
                    history.Insert(0, "");

                    if (inBlock)
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
                                done = true;
                                break;

                            default:
                                input = input.Insert(Console.CursorLeft - 3, CKI.KeyChar.ToString());
                                int cursorLeftBackup = Console.CursorLeft;
                                Console.Write(input.Substring(Console.CursorLeft - 3));
                                Console.CursorLeft = cursorLeftBackup + 1; //Restores cursor to user expected state
                                history[0] = input;
                                break;
                        }
                    }

                    history[0] = input;
                    PreExec(input);
                }
            }
            else
            {
                List<string> argList = new List<string>();
                argList.AddRange(args);

                while (argList.Contains("-p"))
                {
                    ExecFile(argList[argList.IndexOf("-p") + 1]);
                    argList.RemoveAt(argList.IndexOf("-p") + 1);
                    argList.RemoveAt(argList.IndexOf("-p"));
                    if (!argList.Contains("-e"))
                        Main(null);
                }
                while (argList.Contains("-e"))
                {
                    PreExec(argList[argList.IndexOf("-e") + 1]);
                    argList.RemoveAt(argList.IndexOf("-e") + 1);
                    argList.RemoveAt(argList.IndexOf("-e"));
                }
                
                foreach (string arg in argList)
                {
                    ExecFile(arg);
                }
            }
        }

        private static string LoadFile(string path, string name)
        {
            try
            {
                string[] code = File.ReadAllLines(path);

                if (code[0].ToLower().StartsWith("#name") && name == "")
                    name = ArgSplit(code[0])[1];
                else if (name == "")
                    name = path.Substring(path.LastIndexOf('\\') + 1);

                if (!scriptCache.ContainsKey(name.ToLower()))
                    scriptCache.Add(name.ToLower(), path);
                
                return name;
            }
            catch { throw new ArgumentException($"Loading script at {path} failed."); }
        }

        private static void ExecFile(string name)
        {
            try
            {
                string[] code;
                try { code = File.ReadAllLines(scriptCache[name.ToLower()]); }
                catch (KeyNotFoundException) { code = File.ReadAllLines(name); }

                foreach (string line in code) PreExec(line);
            }
            catch (KeyNotFoundException) { Console.WriteLine($"Can't find script {name}"); }
            catch (Exception Ex) { Console.WriteLine(Ex.Message); }
        }

        private static void ClearLine()
        {
            Console.CursorLeft = 100;
            while (Console.CursorLeft > 3)
                Console.Write("\b \b");
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

            Type type;
            if (input.Contains('!'.ToString()))
            {
                string typeName = input.Substring(input.LastIndexOf('!') + 1);
                string searchString = "";

                if (typeName.Contains(":"))
                {
                    searchString = typeName.Substring(typeName.LastIndexOf(':') + 1);
                    typeName = typeName.Remove(typeName.IndexOf(':'));
                }

                try { type = Type.GetType("System." + typeName, true, true); }
                catch { return null; }
                List<MemberInfo> resultInfos = new List<MemberInfo>();
                resultInfos.AddRange(type.GetMembers());
                string result = "\b\b\b";

                if (searchString != "")
                    resultInfos.RemoveAll(s => !s.Name.StartsWith(searchString));

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

        static string[] ArgSplit(string input)
        {
            return ArgSplit(input, " ");
        }

        //Parses arguments from a string, dealing with quotes and variables
        static string[] ArgSplit(string input, string splitStr)
        {
            List<string> retval = new List<string>();

            string arg = "";
            bool isInQuote = false;
            char quoteChar = '\0';

            for (ushort outCntr = 0; outCntr < input.Length; ++outCntr)
            {
                char c = input[outCntr];
                bool isSplit = true;

                for (ushort cntr = 0; cntr < splitStr.Length; ++cntr)
                {
                    if (isSplit && input[outCntr + cntr] != splitStr[cntr])
                        isSplit = false;
                }

                if (!isInQuote && isSplit)
                {
                    if (arg != "")
                        retval.Add(arg);
                    arg = "";
                    outCntr += (ushort)(splitStr.Length - 1);
                    continue;
                }

                if (c == '"' | c == '\'' | c == '`')
                {
                    if (isInQuote && c == quoteChar)
                        isInQuote = false;
                    else if (isInQuote)
                        arg += c;
                    else if (!isInQuote)
                    {
                        isInQuote = true;
                        quoteChar = c;
                    }
                    else throw new SanityException($"{c} is {(isInQuote ? " " : "not ")}in quotes");
                    continue;
                }

                arg += c;
            }

            retval.Add(arg);
            string[] outval = retval.ToArray();

            foreach (KeyValuePair<string, string> var in variables)
                foreach (string argSearch in retval)
                    if (argSearch.ToLower().Contains(var.Key.ToLower()))
                        outval[retval.FindIndex(ind => ind.Equals(argSearch))] = argSearch.Replace(var.Key, var.Value);

            return outval;
        }
    }

    public class SanityException : Exception
    {
        public SanityException() { }

        public SanityException(string message) : base(message) { }

        public SanityException(string message, Exception inner) : base(message, inner) { }
    }
}
