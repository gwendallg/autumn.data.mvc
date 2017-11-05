﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Autumn.Data.Rest.Queries;
using Autumn.Data.Rest.Queries.Exceptions;
using Newtonsoft.Json.Serialization;

namespace Autumn.Data.Rest.Helpers
{
    public static class RsqlHelper
    {
        private static readonly MethodInfo MethodStringContains =
            typeof(string).GetMethod("Contains", new[] {typeof(string)});

        private static readonly MethodInfo MethodStringStartsWith =
            typeof(string).GetMethod("StartsWith", new[] {typeof(string)});

        private static readonly MethodInfo MethodStringEndsWith =
            typeof(string).GetMethod("EndsWith", new[] {typeof(string)});

        private static readonly MethodInfo MethodListContains =
            typeof(List<object>).GetMethod("Contains", new[] {typeof(object)});

        #region GetExpression 

        /// <summary>
        /// create and expression
        /// </summary>
        /// <param name="visitor"></param>
        /// <param name="context"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetAndExpression<T>(
            IRsqlVisitor<Expression<Func<T, bool>>> visitor, RsqlParser.AndContext context)
        {
            if (visitor == null) throw new ArgumentException("visitor");
            if (context == null) throw new ArgumentException("context");
            if (context.constraint().Length == 0) return CommonHelper.True<T>();
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
        /// <param name="visitor"></param>
        /// <param name="context"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetOrExpression<T>(
            IRsqlVisitor<Expression<Func<T, bool>>> visitor, RsqlParser.OrContext context)
        {
            if (visitor == null) throw new ArgumentException("visitor");
            if (context == null) throw new ArgumentException("context");
            if (context.and().Length == 0) return CommonHelper.True<T>();
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
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsNullExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (!expressionValue.Property.PropertyType.IsClass)
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(
                expressionValue.Expression,
                Expression.Constant(null, typeof(object))), parameter);
        }

        /// <summary>
        /// create not-is-null expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetNotIsNullExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            var expression = GetIsNullExpression<T>(parameter, context, namingStrategy);
            var body = Expression.Not(expression.Body);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        /// <summary>
        /// create eq expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetEqExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.Equal(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create neq expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetNeqExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.NotEqual(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create lt expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetLtExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }

            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.LessThan(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create le expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetLeExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }

            
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.LessThanOrEqual(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create gt expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetGtExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }

            
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.GreaterThan(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }
        
        /// <summary>
        /// create ge expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetGeExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }

            
            if (expressionValue.Property.PropertyType == typeof(string) ||
                expressionValue.Property.PropertyType == typeof(bool) ||
                expressionValue.Property.PropertyType == typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

            return Expression.Lambda<Func<T, bool>>(Expression.GreaterThanOrEqual(
                expressionValue.Expression,
                Expression.Constant(values[0], expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create is-true expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsTrueExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType != typeof(bool) &&
                expressionValue.Property.PropertyType != typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(
                expressionValue.Expression,
                Expression.Constant(true)), parameter);
        }

        /// <summary>
        /// create is-false expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="namingStrategy"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIsFalseExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy)
        {
            if (parameter == null) throw new ArgumentException("parameter");
            if (context == null) throw new ArgumentException("context");
            
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            
            if (expressionValue.Property.PropertyType != typeof(bool) &&
                expressionValue.Property.PropertyType != typeof(bool?))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(
                expressionValue.Expression,
                Expression.Constant(false)), parameter);
        }

        /// <summary>
        /// create like expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetLkExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            if (expressionValue.Property.PropertyType != typeof(string))
            {
                throw new RsqlComparisonInvalidComparatorSelectionException(context);
            }
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            if (values.Count > 1) throw new RsqlComparisonTooManyArgumentException(context);

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
            return Expression.Lambda<Func<T, bool>>(Expression.Call(expressionValue.Expression,
                method,
                Expression.Constant(criteria, expressionValue.Property.PropertyType)), parameter);
        }

        /// <summary>
        /// create in expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetInExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy = null)
        {
            var expressionValue =
                CommonHelper.GetMemberExpressionValue<T>(parameter, context, namingStrategy);
            var values = GetValues(expressionValue.Property.PropertyType, context.arguments());
            if (values == null || values.Count == 0) throw new RsqlComparisonNotEnoughtArgumentException(context);
            return Expression.Lambda<Func<T, bool>>(
                Expression.Call(Expression.Constant(values, expressionValue.Property.PropertyType), MethodListContains,
                    expressionValue.Expression), parameter);
        }

        /// <summary>
        /// create out expression
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetOutExpression<T>(ParameterExpression parameter,
            RsqlParser.ComparisonContext context,
            NamingStrategy namingStrategy=null)
        {
            var expression = GetInExpression<T>(parameter, context, namingStrategy);
            var body = Expression.Not(expression.Body);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
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
                if (valueContext.DIGIT() != null)
                {
                    items.Add(short.Parse(valueContext.DIGIT().GetText()));
                }
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
                if (valueContext.DIGIT() != null)
                {
                    items.Add(int.Parse(valueContext.DIGIT().GetText()));
                }
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
                if (valueContext.DIGIT() != null)
                {
                    items.Add(long.Parse(valueContext.DIGIT().GetText()));
                }
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
                if (valueContext.DIGIT() != null)
                {
                    items.Add(double.Parse(valueContext.DIGIT().GetText(), CultureInfo.InvariantCulture));
                }
                if (valueContext.NUMBER() != null)
                {
                    items.Add(double.Parse(valueContext.NUMBER().GetText(), CultureInfo.InvariantCulture));
                }
            }
            return items;
        }

        private static List<object> GetFloats(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.DIGIT() != null)
                {
                    items.Add(float.Parse(valueContext.DIGIT().GetText(), CultureInfo.InvariantCulture));
                }
                if (valueContext.NUMBER() != null)
                {
                    items.Add(float.Parse(valueContext.NUMBER().GetText(), CultureInfo.InvariantCulture));
                }
            }
            return items;
        }

        private static List<object> GetDecimals(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.DIGIT() != null)
                {
                    items.Add(decimal.Parse(valueContext.DIGIT().GetText(), CultureInfo.InvariantCulture));
                }
                if (valueContext.NUMBER() != null)
                {
                    items.Add(decimal.Parse(valueContext.NUMBER().GetText(), CultureInfo.InvariantCulture));
                }
            }
            return items;
        }

        private static List<object> GetDateTimes(RsqlParser.ArgumentsContext argumentsContext)
        {
            var items = new List<object>();
            foreach (var valueContext in argumentsContext.value())
            {
                if (valueContext.DATE() != null)
                {
                    items.Add(DateTime.Parse(valueContext.DATE().GetText(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind));
                }
            }
            return items;
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

            if (type == typeof(float) || type == typeof(float?))
            {
                return GetFloats(argumentsContext);
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
    }
}