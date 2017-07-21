using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NetChains
{
    class Program
    {
        const string VERSIONSTRING = "1.3.0";
        static void Main(string[] args)
        {
            Console.WriteLine($"Welcome to the NetChains interpreter!\n(c) 2017 Weston Sleeman, version {VERSIONSTRING}\nType \"help\" for a brief tutorial or \"exit\" to return to the shell.");
            if (args == null) args = new string[0]{ };

            if (args.Length == 0)
            {
                List<string> history = new List<string>();

                while (true)
                {
                    int historyIndex = 0;
                    Console.Write(">>>");

                    ConsoleKeyInfo CKI;
                    string input = "";
                    bool done = false;
                    history.Insert(0, "");
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

                    if (input == "exit")
                        break;
                    else if (input.StartsWith("load"))
                    {
                        ExecFile(input.Substring((input.Length > 4) ? 5 : 4));
                        Main(null);
                        break;
                    }
                    else if (input == "help")
                    {
                        Console.WriteLine("NetChains is copyrighted by Weston Sleeman, but feel free to redistribute this executable in its original form.");
                        Console.WriteLine("NetChains provides direct access to the .NET framework in a scriptable and chain-y format.");
                        Console.WriteLine("Commands are formed in chains, linked by a double colon (::). Types are specified with a bang (!).");
                        Console.WriteLine("\nEx. !ConsoleColor::Red::!Console::ForegroundColor = $::Write(Hello)::ResetColor");
                        Console.WriteLine("(Shifts to System.ConsoleColor; Selects Red; Shifts to System.Console; Sets ForegroundColor to the parent ($, currently Red); Writes Hello to screen; Resets colors)");
                    }

                    try { Execute(input.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries)); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            }
            else
            {
                foreach (string arg in args)
                {
                    ExecFile(arg);
                }
            }
        }

        private static void ExecFile(string path)
        {
            try
            {
                string[] code = File.ReadAllLines(path);

                foreach (string line in code) Execute(line.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception Ex) { Console.WriteLine(Ex.Message); }
            finally
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                Console.Clear();
            }
        }

        private static void Execute(string[] function)
        {
            Type type;
            try { type = Type.GetType("System." + function[0].TrimStart('!')); }
            catch { type = Type.GetType("System"); }

            int colons = function.Length;
            object obj = null;
            while (colons > 1)
            {
                string curFunc = function[function.Length - (colons - 1)];
                if (curFunc.StartsWith("!"))
                {
                    type = Type.GetType("System." + curFunc.TrimStart('!'));
                }
                else
                {
                    if (curFunc.Contains("="))
                    {
                        List<object> args = null;
                        Type[] argTypes = null;

                        curFunc = curFunc.Replace('=', '(');
                        curFunc = curFunc.Replace(" ", "");
                        curFunc = curFunc + ')';

                        ParseArgs(ref curFunc, ref args, ref argTypes, obj);

                        object castedVal = new object();
                        try { Convert.ChangeType(args[0], argTypes[0]); }
                        catch
                        {
                            if (argTypes[0] == null || String.IsNullOrEmpty(args[0].ToString()))
                                castedVal = null;
                            if (argTypes[0].IsEnum)
                            {
                                Type enumType = argTypes[0];
                                dynamic argCast = args[0];
                                castedVal = Enum.Parse(enumType, argCast.Name.ToString());
                            }

                        }

                        try { type.GetProperty(curFunc).SetValue(null, castedVal); }
                        catch (Exception ex) { throw new Exception($"Error in phrase {(function.Length - colons) + 1}: {ex.Message}"); }
                    }
                    else
                    {
                        try
                        {
                            obj = type.GetProperty(curFunc).GetValue(obj);
                            if (colons == 2)
                                Console.WriteLine(obj);
                        }
                        catch
                        {
                            List<object> args = null;
                            Type[] argTypes = null;

                            ParseArgs(ref curFunc, ref args, ref argTypes, obj);

                            try
                            {
                                if (args == null)
                                {
                                    try
                                    {
                                        try
                                        {
                                            object tmp = obj.GetType().InvokeMember(curFunc, BindingFlags.InvokeMethod, null, obj, null);
                                            if (tmp != null) obj = tmp;
                                        }
                                        catch
                                        {
                                            object tmp = type.GetMethod(curFunc).Invoke(obj, null);
                                            if (tmp != null) obj = tmp;
                                        }
                                    }
                                    catch { throw new Exception($"Function {curFunc} not found."); }
                                }
                                else
                                {
                                    try
                                    {
                                        object tmp = obj.GetType().InvokeMember(curFunc, BindingFlags.InvokeMethod, null, obj, args.ToArray());
                                        if (tmp != null) obj = tmp;
                                    }
                                    catch
                                    {
                                        object tmp = type.GetMethod(curFunc, argTypes).Invoke(obj, args.ToArray());
                                        if (tmp != null) obj = tmp;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { obj = type.GetMember(curFunc)[0]; }
                                catch { throw new Exception($"Error in phrase {(function.Length - colons) + 1}: {ex.Message}"); }
                            }
                        }
                    }
                }
                --colons;
            }
        }

        private static void ParseArgs(ref string input, ref List<object> args, ref Type[] argTypes, dynamic parent)
        {
            if (input.Contains("("))
            {
                args = new List<object>();
                args.AddRange(input.Substring(input.IndexOf('(') + 1, input.IndexOf(')') - (input.IndexOf('(') + 1)).Replace(", ", ",").Split(','));
                for (ushort cntr = 0; cntr < args.Count; ++cntr)
                    args[cntr] = Unescape(args[cntr].ToString());

                input = input.Substring(0, input.IndexOf('('));
                argTypes = new Type[args.Count];

                for (ushort cntr = 0; cntr < args.Count; ++cntr)
                {
                    if ((string)args[cntr] == "$")
                    {
                        if (parent.GetType().Name == "MdFieldInfo")
                            argTypes[cntr] = parent.ReflectedType;
                        else argTypes[cntr] = parent.GetType();

                        args[cntr] = parent;
                    }
                    else
                    {
                        if (int.TryParse((string)args[cntr], out int catchval))
                        {
                            argTypes[cntr] = typeof(int);
                            args[cntr] = catchval;
                        }
                        else argTypes[cntr] = typeof(string);
                    }
                }
            }
        }

        private static string Unescape (string input)
        {
            return Regex.Replace(input, @"\\[*]", m =>
            {
                switch (m.Value)
                {
                    case @"\r": return "\r";
                    case @"\n": return "\n";
                    case @"\t": return "\t";
                    case @"\\": return "\\";
                    default: return m.Value;
                }
            });
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
    }
}
