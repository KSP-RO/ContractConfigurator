﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ContractConfigurator.ExpressionParser
{
    /// <summary>
    /// Common base class (without typing) for all expression parsers.
    /// </summary>
    public abstract class BaseParser
    {
        public class WrongDataType : Exception
        {
            public Type expected { get; private set; }

            public WrongDataType(Type exp, Type got)
                : base(StringBuilderCache.Format("Expected {0} got {1}", exp, got))
            {
                expected = exp;
            }
        }

        /// <summary>
        /// Types of expresion tokens.
        /// </summary>
        public enum TokenType
        {
            IDENTIFIER,
            SPECIAL_IDENTIFIER,
            DATA_STORE_IDENTIFIER,
            VALUE,
            OPERATOR,
            START_BRACKET,
            END_BRACKET,
            COMMA,
            FUNCTION,
            METHOD,
            TERNARY_START,
            TERNARY_END,
            LIST_START,
            LIST_END,
            QUOTE,
        }

        /// <summary>
        /// A parsed token.
        /// </summary>
        public class Token
        {
            public TokenType tokenType;
            public string sval;

            public Token(TokenType type)
            {
                tokenType = type;

                if (tokenType == TokenType.START_BRACKET)
                {
                    sval = "(";
                }
                else if (tokenType == TokenType.END_BRACKET)
                {
                    sval = ")";
                }
                else if (tokenType == TokenType.COMMA)
                {
                    sval = ",";
                }
                else if (tokenType == TokenType.TERNARY_START)
                {
                    sval = "?";
                }
                else if (tokenType == TokenType.TERNARY_END)
                {
                    sval = ":";
                }
                else if (tokenType == TokenType.LIST_START)
                {
                    sval = "[";
                }
                else if (tokenType == TokenType.LIST_END)
                {
                    sval = "]";
                }
                else if (tokenType == TokenType.QUOTE)
                {
                    sval = "\"";
                }
            }

            public Token(TokenType type, string s)
            {
                tokenType = type;
                sval = s;
            }
        }

        /// <summary>
        /// A token with a value type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class ValueToken<T> : Token
        {
            public T val;

            public ValueToken(T t)
                : base(TokenType.VALUE)
            {
                val = t;
                sval = t.ToString();
            }
        }

        public static char[] WHITESPACE_OR_OPERATOR =
        {
            ' ', '\t', '\n', '|', '&', '+', '-', '!', '<', '>', '=', '*', '/', '(', ')', ',', '?', ':', '[', ']'
        };

        // List of tokens and their precedence
        private static string[][] PRECENDENCE_CONSTS =
        {
            new string[] { "?", ":"},
            new string[] { "||" },
            new string[] { "&&" },
            new string[] { "!=", "==" },
            new string[] { "<", ">", "<=", ">=" },
            new string[] { "-", "+" },
            new string[] { "*", "/" },
            new string[] { "!" }
        };
        public static Dictionary<string, int> precedence = new Dictionary<string, int>();

        private static Dictionary<Type, Type> parserTypes = new Dictionary<Type, Type>();
        protected int spacing = 0;
        protected static bool verbose;
        protected BaseParser parentParser = null;

        protected static Dictionary<string, KeyValuePair<object, Type>> tempVariables = new Dictionary<string, KeyValuePair<object, Type>>();

        static BaseParser()
        {
            verbose = LoggingUtil.GetLogLevel(typeof(BaseParser)) == LoggingUtil.LogLevel.VERBOSE;

            // Create the precendence map
            if (precedence.Count == 0)
            {
                for (int i = 0; i < PRECENDENCE_CONSTS.Length; i++)
                {
                    foreach (string token in PRECENDENCE_CONSTS[i])
                    {
                        precedence[token] = i;
                    }
                }
            }

            // Register each type of expression parser
            foreach (Type subclass in ContractConfigurator.GetAllTypes<IExpressionParserRegistrer>())
            {
                if (subclass.IsClass && !subclass.IsAbstract)
                {
                    IExpressionParserRegistrer r = Activator.CreateInstance(subclass) as IExpressionParserRegistrer;
                    var method = subclass.GetMethod("RegisterExpressionParsers");
                    method.Invoke(r, new object[] { });
                }
            }
        }

        /// <summary>
        /// Registers a parser for the given type of object.
        /// </summary>
        /// <param name="objectType">Type of object that the given parser will handle expressions for.</param>
        /// <param name="parserType">Type of the parser.</param>
        public static void RegisterParserType(Type objectType, Type parserType)
        {
            parserTypes[objectType] = parserType;

            // Also add the list type
            Type listType = typeof(List<>).MakeGenericType(new Type[] { objectType });
            Type listParserType = typeof(ListExpressionParser<>).MakeGenericType(new Type[] { objectType });
            parserTypes[listType] = listParserType;
        }

        /// <summary>
        /// Gets an ExpressionParser for the given type.
        /// </summary>
        /// <typeparam name="T">The type to get a parser for</typeparam>
        /// <returns>An instance of the expression parser, or null if none can be created</returns>
        public static ExpressionParser<T> GetParser<T>()
        {
            if (parserTypes.TryGetValue(typeof(T), out Type t))
            {
                return (ExpressionParser<T>)Activator.CreateInstance(t);
            }
            else if (typeof(T).IsEnum)
            {
                Type enumParser = typeof(EnumExpressionParser<>).MakeGenericType(new Type[] { typeof(T) });
                return (ExpressionParser<T>)Activator.CreateInstance(enumParser);
            }

            return null;
        }

        public static BaseParser NewParser(Type type)
        {
            MethodInfo getParserMethod = methodGetParserPublic.MakeGenericMethod(new Type[] { type });
            return (BaseParser)getParserMethod.Invoke(null, new object[] { });
        }

        protected static ExpressionParser<T> GetParser<T>(BaseParser orig)
        {
            ExpressionParser<T> newParser = GetParser<T>();

            if (newParser == null)
            {
                throw new NotSupportedException(StringBuilderCache.Format("Unsupported type: {0}", typeof(T)));
            }

            newParser.Init(orig.expression);
            newParser.parseMode = orig.parseMode;
            newParser.currentDataNode = orig.currentDataNode;
            newParser.currentKey = orig.currentKey;
            newParser.spacing = orig.spacing;
            newParser.parentParser = orig;

            return newParser;
        }

        protected BaseParser GetParser(Type type)
        {
            MethodInfo getParserMethod = methodGetParserProtected.MakeGenericMethod(new Type[] { type });
            return (BaseParser)getParserMethod.Invoke(null, new object[] { this });
        }

        protected static Dictionary<string, List<Function>> globalFunctions = new Dictionary<string, List<Function>>();
        public string expression;
        protected bool parseMode = true;
        public DataNode currentDataNode = null;
        public string currentKey = null;
        protected static BaseParser currentParser = null;

        /// <summary>
        /// Initialize for parsing.
        /// </summary>
        /// <param name="expression">Expression being parsed</param>
        protected void Init(string expression)
        {
            // Create a copy of the expression being parsed
            this.expression = string.Copy(expression);
        }

        /// <summary>
        /// Registers a function that is available globally.
        /// </summary>
        /// <param name="method">The callable function.</param>
        protected static void RegisterGlobalFunction(Function function)
        {
            if (!globalFunctions.TryGetValue(function.Name, out List<Function> list))
            {
                globalFunctions[function.Name] = list = new List<Function>();
            }
            list.Add(function);
        }

        protected static Type GetRequiredType(Exception e)
        {
            if (e.GetType() == typeof(WrongDataType))
            {
                WrongDataType ex = (WrongDataType)e;
                return ex.expected;
            }
            else if (e.GetType() == typeof(DataStoreCastException))
            {
                DataStoreCastException ex = (DataStoreCastException)e;
                return ex.FromType;
            }

            return null;
        }

        protected bool LogEntryDebug<TResult>(string function, params object[] args)
        {
            StringBuilder sb = StringBuilderCache.Acquire();
            if (spacing > 0)
            {
                sb.Append(' ', spacing * 2);
            }
            sb.Append("-> ");
            sb.Append(GetType().Name);
            if (GetType().IsGenericType)
            {
                sb.AppendFormat("[{0}]", GetType().GetGenericArguments()[0].Name);
            }
            sb.AppendFormat(".{0}<{1}>(", function, typeof(TResult).Name);
            bool first = true;
            foreach (object arg in args)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append(arg != null ? arg.ToString() : "null");
            }
            sb.Append("), expression = ");
            sb.Append(expression);
            LoggingUtil.LogVerbose(typeof(BaseParser), sb.ToStringAndRelease());
            spacing++;

            return true;
        }

        protected bool LogExitDebug<TResult>(string function, string result)
        {
            spacing--;
            StringBuilder sb = StringBuilderCache.Acquire();
            if (spacing > 0)
            {
                sb.Append(' ', spacing * 2);
            }
            sb.Append("<- ");
            sb.Append(GetType().Name);
            if (GetType().IsGenericType)
            {
                sb.AppendFormat("[{0}]", GetType().GetGenericArguments()[0].Name);
            }
            sb.AppendFormat(".{0}<{1}>() = ", function, typeof(TResult).Name);
            sb.Append(result);
            sb.Append(", expression = ");
            sb.Append(expression);
            LoggingUtil.LogVerbose(typeof(BaseParser), sb.ToStringAndRelease());

            return true;
        }

        protected bool LogExitDebug<TResult>(string function, TResult result)
        {
            return LogExitDebug<TResult>(function, (result != null ? result.ToString() : "null"));
        }

        protected bool LogException<TResult>(string function)
        {
            return LogExitDebug<TResult>(function, "(EXCEPTION)");
        }

        public abstract void ExecuteAndStoreExpression(string key, string expression, DataNode dataNode);
        public abstract object ParseExpressionGeneric(string key, string expression, DataNode dataNode);

        protected enum ReflectionMethod
        {
            ParseStatementInner,
        }

        public abstract MethodInfo methodParseStatementInner { get; }
        public abstract MethodInfo methodGetRval { get; }
        public abstract MethodInfo methodApplyBooleanOperator { get; }
        public abstract MethodInfo methodParseStatement { get; }
        public abstract MethodInfo methodParseMethod { get; }
        public abstract MethodInfo methodCompleteIdentifierParsing { get; }
        public abstract MethodInfo method_ConvertType { get; }

        static MethodInfo methodGetParserPublic = typeof(BaseParser).GetMethods(BindingFlags.Static | BindingFlags.Public).
            Where(m => m.Name == "GetParser" && m.GetParameters().Count() == 0).Single();
        static MethodInfo methodGetParserProtected = typeof(BaseParser).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).
            Where(m => m.Name == "GetParser").Single();
    }
}
