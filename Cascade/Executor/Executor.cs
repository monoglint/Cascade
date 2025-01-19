using System.Diagnostics;
using Cascade.Pipeline.Frontend.Lexer;
using Cascade.Pipeline.Frontend.Parser;
using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Runtime;
using Cascade.Pipeline.Shared;

namespace Cascade.Executor
{
    public static class Executor
    {
        public static void ExecuteFile(string filePath)
        {
            Execute(FileOpener.OpenFile(filePath));
        }

        public static void Execute(string code)
        {
            Lexer lexer = new(code);

            Stopwatch lexSW = Stopwatch.StartNew();
            List<Token> tokens = lexer.ParseToEnd();
            lexSW.Stop(); 

            if (!Diagnostics.CheckDiagnostics(lexer.Diagnostics))
            {
                return;
            }


            Parser parser = new(tokens, code);

            Stopwatch parseSW = Stopwatch.StartNew();
            ProgramStatementNode programStatement = parser.ParseToEnd();
            parseSW.Stop();

            if (!Diagnostics.CheckDiagnostics(parser.Diagnostics))
            {
                return;
            }



            Interpreter interpreter = new();

            Stopwatch interpretSW = Stopwatch.StartNew();
            interpreter.EvaluateAst(programStatement);
            interpretSW.Stop();

            if (!Diagnostics.CheckDiagnostics(interpreter.Diagnostics))
            {
                return;
            }
        }
    }

    public static class Diagnostics
    {
        public static void PrintBilly()
        {
            Console.WriteLine(" _____________");
            Console.WriteLine("|      0      |");
            Console.WriteLine("|     /|\\     |");
            Console.WriteLine("|      |      |");
            Console.WriteLine("|     / \\     |");
            Console.WriteLine("|    Billy!   |");
            Console.WriteLine(" \\___________/");
        }

        // Returns whether or not it is safe to continue.
        public static bool CheckDiagnostics(IEnumerable<CascadeDiagnostic> diagnostics)
        {
            if (!diagnostics.Any())
            {
                return true;
            }

            bool safeToContinue = true;

            foreach (CascadeDiagnostic diagnostic in diagnostics)
            {
                Console.ForegroundColor = diagnostic.Type switch
                {
                    CascadeDiagnosticType.ERROR => Console.ForegroundColor = ConsoleColor.Red,
                    CascadeDiagnosticType.WARNING => Console.ForegroundColor = ConsoleColor.DarkYellow,
                    CascadeDiagnosticType.INFO => Console.ForegroundColor = ConsoleColor.Cyan,
                    _ => Console.ForegroundColor = ConsoleColor.Gray
                };

                if (diagnostic.Type == CascadeDiagnosticType.ERROR)
                {
                    safeToContinue = false;
                }

                Console.WriteLine($"Line {diagnostic.Location.Line + 1}:  {diagnostic.Message}");
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            return safeToContinue;
        }
    }

    public static class Debugger
    {
        public static void LogTokens(List<Token> tokens)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                Console.WriteLine($"Token: {token.Kind}, Value: {token.Value}, Start: {token.Location.Start}, End: {token.Location.End}, Line: {token.Location.Line}");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void LogAST(AstNode node)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            LogAST(node, 0);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void LogAST(AstNode node, int indentLevel)
        {
            // Helper method to print with indentation
            void PrintIndented(string message)
            {
                Console.WriteLine(new string(' ', indentLevel * 2) + message);
            }

            var properties = node.GetType().GetProperties();
            foreach (var property in properties)
            {
                var value = property.GetValue(node);

                if (value is StatementNode nestedNode)
                {
                    // Recursive case for nested nodes
                    PrintIndented($"{property.Name}:");
                    LogAST(nestedNode, indentLevel + 1);
                }
                else if (value is IEnumerable<StatementNode> nestedNodes)
                {
                    // Handle collections of nodes
                    PrintIndented($"{property.Name}: [");
                    foreach (var childNode in nestedNodes)
                    {
                        LogAST(childNode, indentLevel + 1);
                    }
                    PrintIndented("]");
                }
                else if (value is ExpressionNode nestedExpression)
                {
                    // Handle nested expression nodes
                    PrintIndented($"{property.Name}:");
                    LogAST(nestedExpression, indentLevel + 1);
                }
                else
                {
                    // Print scalar properties
                    PrintIndented($"{property.Name}: {value}");
                }
            }

            // Print basic node information
            Console.WriteLine();
        }
    }

    public static class FileOpener
    {
        public static string OpenFile(string path)
        {
            if (!Path.HasExtension(".cascade"))
            {
                throw new Exception($"File {path} must be a .cascade file.");
            }

            return File.ReadAllText(path);
        }
    }
}
