﻿    using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Autumn.Data.Rest.Rsql.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Serialization;

namespace Autumn.Data.Rest.Rsql
{
    public static class RsqlHelper
    {
        
        private static readonly MethodInfo MethodStringContains=typeof(string).GetMethod("Contains",new[]{typeof(string)});
        private static readonly MethodInfo MethodStringStartsWith=typeof(string).GetMethod("StartsWith",new[]{typeof(string)});
        private static readonly MethodInfo MethodStringEndsWith=typeof(string).GetMethod("EndsWith",new[]{typeof(string)});
        private static readonly MethodInfo MethodListContains = typeof(List<object>).GetMethod("Contains", new[] {typeof(object)});
        private static readonly Dictionary<string,PropertyInfo> MappingJson2PropertyInfo=new Dictionary<string, PropertyInfo>();
        
        public static Expression<Func<T, bool>> True<T> ()  { return f => true;  }
       
        public static readonly MemoryCache QueriesCache = new MemoryCache(new MemoryCacheOptions(){ExpirationScanFrequency = TimeSpan.FromMinutes(5)});

        #region GetExpression 
     
        /// <summary>
        /// create and expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="visitor"></param>
        /// <param name="context"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetAndExpression<T>(ParameterExpression parameter,
            IRsqlVisitor<Expression<Func<T, bool>>> visitor, RsqlParser.AndContext context)
        {
            if (context.constraint().Length == 0) return True<T>();
            var right = context.constraint()[0].Accept(visitor);
            if (context.constraint().Length == 1) return right;
            for (var i = 1; i < context.constraint().Length; i++)
            {
                var left = context.constraint()[i].Accept(visitor);
                right = Expression.Lambda<Func<T, bool>>(Expression.And(left.Body, right.Body), left.Parameters);
            }
            return right;
        }
        
        
        /// <summary>
        /// create or expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="visitor"></param>
        /// <param name="context"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetOrExpression<T>(ParameterExpression parameter,
            IRsqlVisitor<Expression<Func<T, bool>>> visitor, RsqlParser.OrContext context)
        {
            if (context.and().Length == 0) return True<T>();
            var right = context.and()[0].Accept(visitor);
            if (context.and().Length == 1) return right;
            for (var i = 1; i < context.and().Length; i++)
            {
                var left = context.and()[i].Accept(visitor);
                right = Expression.Lambda<Func<T, bool>>(Expression.Or(left.Body, right.Body), left.Parameters);
            }
            return right;
        }

        /// <summary>
        /// create is-null expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsNullExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(
                Expression.Property(parameter, property),
                Expression.Constant(null, typeof(object))), parameter);
        }

        /// <summary>
        /// create not-is-null expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetNotIsNullExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            return Expression.Lambda<Func<T, bool>>(Expression.NotEqual(
                Expression.Property(parameter, property),
                Expression.Constant(null, typeof(object))), parameter);
        }

        /// <summary>
        /// create eq expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetEqExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);

            return Expression.Lambda<Func<T, bool>>( Expression.Equal(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }

        /// <summary>
        /// create neq expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetNeqExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);

            return Expression.Lambda<Func<T, bool>>( Expression.NotEqual(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }

        /// <summary>
        /// create lt expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetLtExpression<T>(ParameterExpression parameter, string selector, NamingStrategy namingStrategy
            ,RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);
            
            return Expression.Lambda<Func<T, bool>>( Expression.LessThan(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }
        
        /// <summary>
        /// create le expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetLeExpression<T>(ParameterExpression parameter, string selector, NamingStrategy namingStrategy
            ,RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);
            
            return Expression.Lambda<Func<T, bool>>( Expression.LessThanOrEqual(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }
        
        /// <summary>
        /// create gt expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetGtExpression<T>(ParameterExpression parameter, string selector, NamingStrategy namingStrategy
            ,RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);
     
            return Expression.Lambda<Func<T, bool>>( Expression.GreaterThan(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }

        /// <summary>
        /// create ge expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RsqlNotEnoughtArgumentException"></exception>
        /// <exception cref="RsqlTooManyArgumentException"></exception>
        public static Expression<Func<T, bool>> GetGeExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);

            return Expression.Lambda<Func<T, bool>>( Expression.GreaterThanOrEqual(
                Expression.Property(parameter, property),
                Expression.Constant(values[0])), parameter);
        }

        /// <summary>
        /// create is-true expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsTrueExpression<T>(ParameterExpression parameter, string selector, NamingStrategy namingStrategy
            ,RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            return Expression.Lambda<Func<T, bool>>( Expression.Equal(
                Expression.Property(parameter, property),
                Expression.Constant(true)), parameter);
        }

        /// <summary>
        /// create is-false expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="selector"></param>
        /// <param name="namingStrategy"></param>
        /// <param name="arguments"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsFalseExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            return Expression.Lambda<Func<T, bool>>( Expression.Equal(
                Expression.Property(parameter, property),
                Expression.Constant(false)), parameter);
        }
        
        /// <summary>
        /// create like expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetLkExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            if (values.Count > 1) throw new RsqlTooManyArgumentException(arguments);

            var criteria = Convert.ToString(values[0]);
            var maskStar = "{" + Guid.NewGuid().ToString() + "}";
            criteria = criteria.Replace(@"\*", maskStar);
            MethodInfo method;
            if (criteria.IndexOf('*') == -1)
            {
                criteria = criteria + '*';
            }
            if (criteria.StartsWith("*") && criteria.EndsWith("*"))
            {
                method = MethodStringContains;
            }
            else if (criteria.StartsWith("*"))
            {
                method = MethodStringEndsWith;
            }
            else
            {
                method = MethodStringStartsWith;
            }
            criteria = criteria.Replace("*", "").Replace(maskStar, "*");
            return Expression.Lambda<Func<T, bool>>(Expression.Call(Expression.Property(parameter, property),
                method,
                Expression.Constant(criteria)), parameter);
        }

        /// <summary>
        /// create in expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetInExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            var property = GetProperty(typeof(T), selector, namingStrategy);
            var values = GetValues(property.PropertyType, arguments);
            if (values == null || values.Count == 0) throw new RsqlNotEnoughtArgumentException(arguments);
            return Expression.Lambda<Func<T, bool>>(Expression.Call(Expression.Constant(values), MethodListContains,
                Expression.Property(parameter, property)));
        }

        /// <summary>
        /// create out expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetOutExpression<T>(ParameterExpression parameter, string selector,
            NamingStrategy namingStrategy
            , RsqlParser.ArgumentsContext arguments)
        {
            return Expression.Lambda<Func<T, bool>>(
                Expression.Not(GetInExpression<T>(parameter, selector, namingStrategy, arguments)));
        }

        #endregion
      
        #region GetValues
        
        private static List<object> GetStringValues(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.single_quote() == null) continue;
                var text = valueContext.single_quote().GetText();
                text = text.Length == 2 ? string.Empty : text.Substring(1, text.Length - 2);
                items.Add(text);
            }
            return items;
        }
        
        private static List<object> GetShorts(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.NUMBER() != null)
                {
                    items.Add(short.Parse(valueContext.NUMBER().GetText()));
                }
            }
            return items;
        }
        
        private static List<object> GetInts(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.NUMBER() != null)
                {
                    items.Add(int.Parse(valueContext.NUMBER().GetText()));
                }
            }
            return items;
        }
        
        private static List<object> GetLongs(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.NUMBER() != null)
                {
                    items.Add(long.Parse(valueContext.NUMBER().GetText()));
                }
            }
            return items;
        }
        
        private static List<object> GetDoubles(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.NUMBER() != null)
                {
                    items.Add(double.Parse(valueContext.NUMBER().GetText()));
                }
            }
            return items;
        }

        private static List<object> GetDecimals(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.NUMBER() != null)
                {
                    items.Add(decimal.Parse(valueContext.NUMBER().GetText()));
                }
            }
            return items;
        }
        
        private static List<object> GetDateTimes(RsqlParser.ArgumentsContext argumentsContext)
        {
            // TODO
            return null;
        }

        private static List<object> GetDateTimeOffsets(RsqlParser.ArgumentsContext argumentsContext)
        {
            // TODO
            return null;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="argumentsContext"></param>
        /// <returns></returns>
        private static List<object> GetValues(Type type, RsqlParser.ArgumentsContext argumentsContext)
        {
            if (argumentsContext?.value() == null || argumentsContext.value().Length == 0) return null;
            if (type == typeof(string))
            {
                return GetStringValues(argumentsContext);
            }

            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return GetDateTimes(argumentsContext);
            }

            if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
            {
                return GetDateTimeOffsets(argumentsContext);
            }
           
            if (type == typeof(short) || type == typeof(short?))
            {
                return GetShorts(argumentsContext);
            }
            
            if (type == typeof(int) || type == typeof(int?))
            {
                return GetInts(argumentsContext);
            }
            
            if (type == typeof(long) || type == typeof(long?))
            {
                return GetLongs(argumentsContext);
            }
            
            if (type == typeof(double) || type == typeof(double?))
            {
                return GetDoubles(argumentsContext);
            }
            
            if (type == typeof(decimal) || type == typeof(decimal?))
            {
                return GetDecimals(argumentsContext);
            }
            return null;
        }

        #endregion

        #region GetProperty 
        
        private static PropertyInfo GetProperty(Type type, string name, NamingStrategy namingStrategy)
        {
            var key = string.Format("{0}|{1}", type.FullName, name);
            if (!MappingJson2PropertyInfo.ContainsKey(key))
            {
                var propertyName = name;
                if (namingStrategy != null)
                {
                    if (namingStrategy is SnakeCaseNamingStrategy)
                    {
                        propertyName = name.Split(new[] {"_"}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1))
                            .Aggregate(string.Empty, (s1, s2) => s1 + s2);
                    }
                    if (namingStrategy is CamelCaseNamingStrategy)
                    {
                        propertyName = name[0].ToString().ToUpperInvariant() + name.Substring(1);
                    }
                }
                MappingJson2PropertyInfo[key] = type.GetProperty(propertyName);
            }
            return MappingJson2PropertyInfo[key];
        }
        
        #endregion
    }
}