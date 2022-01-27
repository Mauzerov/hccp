using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HtmlAgilityPack;

namespace hccp
{
    static class StringExt
    {
        public static string Multiply(this string s, int times)
        {
            return string.Concat(Enumerable.Repeat(s, times));
        }
    }
    
    internal class Program
    {
        private const string indent = "    ";
        public static void DisplayUsage()
        {
            Console.WriteLine("Hcpp usage:");
            Console.WriteLine("\t[file source]");
        }
        private static void WriteNode(TextWriter writer, HtmlNode node, int depth)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Document:
                    break;
                case HtmlNodeType.Element when node.Name != "main":
                    // Todo: Write Different Segments
                    
                    switch (node.Name)
                    {
                        case "div":
                            // Statement (for, if, while, do(do while), else, elif(else if))
                            var @class = node.Attributes["class"].Value;
                            var @contentAttr = node.Attributes["content"];
                            var @content = (@contentAttr == null) ? "" : @contentAttr.Value;
               
                            switch (@class)
                            {
                                case "while":
                                case "for":
                                    writer.WriteLine(indent.Multiply(depth) + $"{@class} ({@content}) {{");
                                    foreach (var nextNode in node.ChildNodes)
                                    {
                                        WriteNode(writer, nextNode, depth + 1);   
                                    }
                                    writer.WriteLine(indent.Multiply(depth) + "}");
                                    break;
                                case "do":
                                    writer.WriteLine(indent.Multiply(depth) + $"do {{");
                                    foreach (var nextNode in node.ChildNodes)
                                    {
                                        WriteNode(writer, nextNode, depth + 1);   
                                    }
                                    writer.WriteLine(indent.Multiply(depth) + $"}} ({@content});");
                                    break;
                                case "if":
                                case "else if":
                                    writer.WriteLine(indent.Multiply(depth) + $"{@class} ({@content}) {{");
                                    foreach (var nextNode in node.ChildNodes)
                                    {
                                        WriteNode(writer, nextNode, depth + 1);   
                                    }
                                    writer.WriteLine(indent.Multiply(depth) + $"}}");
                                    break;
                                case "else":
                                    writer.WriteLine(indent.Multiply(depth) + $"{@class} {{");
                                    foreach (var nextNode in node.ChildNodes)
                                    {
                                        WriteNode(writer, nextNode, depth + 1);
                                    }
                                    writer.WriteLine(indent.Multiply(depth) + $"}}");
                                    break;
                            }
                            break;
                        case "section":
                            // Namespace
                            var name = node.Attributes["class"].Value;
                            
                            writer.WriteLine(indent.Multiply(depth) + $"namespace {@name} {{");
                            foreach (var nextNode in node.ChildNodes)
                            {
                                WriteNode(writer, nextNode, depth + 1);   
                            }
                            writer.WriteLine(indent.Multiply(depth) + $"}}");
                            break;
                        case "form":
                            var @params = node.Attributes["content"].Value;
                            var funcName = node.Attributes["action"].Value;
                            var @return = node.Attributes["method"].Value;

                            writer.WriteLine(indent.Multiply(depth) + $"{@return} {funcName}({@params}) {{");
                            foreach (var nextNode in node.ChildNodes)
                            {
                                WriteNode(writer, nextNode, depth + 1);   
                            }
                            writer.WriteLine(indent.Multiply(depth) + $"}}");
                            // Function
                            break;
                        case "object":
                            var @inherits = node.Attributes["type"].Value.Split(' ').ToList();
                            var className = node.Attributes["data"].Value;

                            var betterInherits = (@inherits.Count > 0 && inherits[0] != "")?@inherits.Aggregate(": ", (prev, curr) => prev + " public " + curr + ","):",";
                            betterInherits = betterInherits.Substring(0, betterInherits.Length - 1);
                            // Class
                            writer.WriteLine(indent.Multiply(depth) + $"class {className} {betterInherits} {{");
                            foreach (var nextNode in node.ChildNodes)
                            {
                                WriteNode(writer, nextNode, depth + 1);   
                            }
                            writer.WriteLine(indent.Multiply(depth) + $"}};");
                            break;
                        case "var":
                            // Variable
                            var varName = node.Attributes["id"].Value;
                            var varType = node.Attributes["class"].Value;
                            var varValue = node.InnerText.Trim();
                            
                            writer.WriteLine(indent.Multiply(depth) + $"{varType} {varName} = {varValue};");
                            break;
                        
                        case "param":
                            var paramHer = node.Attributes["name"].Value;
                            var paramValue = node.Attributes["value"].Value;
                            var paramType = node.Attributes["class"].Value;
                            var paramName = node.Attributes["id"].Value;
                            
                            writer.WriteLine(indent.Multiply(depth) + $"{paramHer}: {paramType} {paramName} = {paramValue};");
                            break;
                        case "input":
                            var inputSource = node.Attributes["name"].Value;
                            var inputElements = node.Attributes["class"].Value.Split(' ');

                            var inputString = inputElements.Aggregate("", (current, t) => current + (" >> " + t));

                            writer.WriteLine(indent.Multiply(depth) + $"{inputSource}{inputString};");
                            break;
                        case "output":
                            var outputSource = node.Attributes["name"].Value;
                            var outputElements = node.Attributes["class"].Value.Split(' ');

                            var outputString = outputElements.Aggregate("", (current, t) => current + (" << " + t));

                            writer.WriteLine(indent.Multiply(depth) + $"{outputSource}{outputString};");
                            break;
                    }
                    break;
                case HtmlNodeType.Element:
                case HtmlNodeType.Comment:
                    break;
                case HtmlNodeType.Text:
                    if (node.InnerText.Trim().Length > 0)
                        writer.WriteLine(indent.Multiply(depth) + node.InnerText);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string ConvertToCpp(HtmlDocument document)
        {
            var head = document.DocumentNode.SelectSingleNode("//head");
            Console.WriteLine(head);
            
            var title = head.SelectSingleNode("//title").InnerText;
            var includes = head.SelectNodes("//link");
            var metas = head.SelectNodes("//meta[@name]");

            var author = metas.Where(n => n.Attributes["name"].Value == "author").ToList();
            var description= metas.Where(n => n.Attributes["name"].Value == "description").ToList();
            var body = document.DocumentNode.SelectSingleNode("//body");
            var namespaces = metas.Where(n => n.Attributes["name"].Value == "keywords").ToList();

            using (var writer = new StreamWriter(title))
            {
                #region Author & Description
                
                    writer.WriteLine("/**");
                    if (author.Count != 0)
                    {
                        writer.WriteLine(author[0].Attributes["content"].Value);
                    }
                    if (description.Count != 0)
                    {
                        writer.WriteLine(description[0].Attributes["content"].Value);
                    }
                    writer.WriteLine("**/");
                    
                #endregion
                
                #region Includes

                foreach (var link in includes)
                {
                    var rel = link.Attributes["rel"].Value;
                    var href = link.Attributes["href"].Value;

                    writer.WriteLine(rel == "local" ? $"#include \"{href}\"" : $"#include <{href}>");
                }
                
                #endregion

                #region Namespaces

                if (namespaces.Count != 0)
                {
                    var @namespace = namespaces[0];
                    var strings = @namespace.Attributes["content"].Value;
                    foreach (var @string in strings.Split(' '))
                    {
                        writer.WriteLine($"using namespace {@string};");
                    }
                }

                #endregion
    
                foreach (var node in body.ChildNodes)
                {
                    WriteNode(writer, node, 0);
                }
                
                #region Main Entry Point

                    var main = body.SelectSingleNode("//main");
                    
                    writer.WriteLine();
                    writer.WriteLine("int main() {");
                    
                    foreach (var nextNode in main.ChildNodes)
                    {
                        WriteNode(writer, nextNode, 1);   
                    }
                    
                    writer.WriteLine("\treturn 0;");
                    writer.WriteLine("}");

                #endregion
            }

            return title;
        }
        
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                DisplayUsage();
                return;
            }
            try
            {
                var doc = new HtmlDocument();
                doc.Load(args[0]);

                var outputFileName = ConvertToCpp(doc);
                var outputExecutable = Path.GetFileNameWithoutExtension(outputFileName);

                if (args.Length <= 1) return;
                if (!args[1].StartsWith("r")) return;
                var proc = new ProcessStartInfo
                {
                    FileName = @"C:\windows\system32\cmd.exe",
                    Arguments = $"/c g++ --std=c++14 {outputFileName} -o {outputExecutable}.exe & .\\main.exe & pause"
                };
                Process.Start(proc);
            }
            catch (Exception ignored)
            {
                Console.WriteLine(ignored);
            }
        }
    }
}